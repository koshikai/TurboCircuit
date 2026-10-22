using UnityEngine;

public enum ItemType { None, Turbo, Banana, Missile, Star, Bomb, Thunder }

public struct KartInput
{
    public float throttle, steer;
    public bool drift, driftDown, useItem;
}

// カート本体。プレイヤーも CPU も同じ物理で動き、入力の出どころだけが違う。
public partial class Kart : MonoBehaviour
{
    public const float MaxSpeed = 30f + (10f / 3.6f); // 基準最高速度: 108km/h + 10km/h = 約 118km/h (32.78m/s)
    public const float Radius = 1.15f;

    // キャラ性能（1〜8、5 が標準）。選択キャラのステータスを物理に反映する。
    public float Mass { get; private set; } = 5f;
    float topSpeedMul = 1f, accelMul = 1f, turnMul = 1f;

    public string Name;
    public Color Color;
    public Color DriverColor = Color.white;
    public bool IsPlayer;
    public int PlayerIndex;     // 人間操作時の識別（0 = P1、1 = P2）
    public bool IsRemote;       // オンライン対戦で相手側が計算しているカート（受信した状態を再現するだけ）
    public int ModelVariant = -1;  // ドライバーモデル番号（-1 = 自動）
    public int KartVariant = -1;   // カートマシン番号（-1 = 自動）
    public IKartInputProvider InputProvider { get; set; }

    // レース状況
    public int Index;
    public int Lap;
    public int MaxLap;
    public float Progress, Lateral;
    public int Place;
    public bool Finished;
    public float FinishTime;
    public float RaceDistance => Lap * track.Length + Progress;

    public float Speed;
    public float Heading;
    public float CurrentThrottle { get; private set; }
    public Vector3 VelDir;
    public ItemType Item;
    public float RouletteTimer;
    public bool Drifting => drifting;
    public float DriftCharge => driftCharge;
    public int DriftLevel => driftCharge >= 2.6f ? 3 : driftCharge >= 1.6f ? 2 : driftCharge >= 0.8f ? 1 : 0;
    public bool Boosting => boostTimer > 0;
    public bool StarInvincible => starTimer > 0;
    public bool Shielded => StarInvincible; // 後方互換エイリアス
    public bool Shocked => shockTimer > 0;
    public bool Offroad { get; private set; }
    public bool IsAirborne => isAirborne;
    public Vector3 Forward => Quaternion.Euler(0, Heading, 0) * Vector3.forward;

    // スリップストリーム & ジャンプトリック
    public bool InDraftStream { get; private set; }
    public bool Slipstreaming => slipstreamTimer > 0;
    public float SlipstreamCharge => slipstreamDraftTimer;
    public bool TrickSuccess => trickDone;

    float slipstreamDraftTimer, slipstreamTimer;
    float trickWindowTimer;
    bool trickDone;
    bool aiTrickDecided;
    AudioSource engine3D;

    float boostTimer, starTimer, shockTimer, spinTimer, invulnTimer, driftCharge, hopT = 1f, spinAngle, visYaw, wheelAngle, steerVis;
    float driftFlarePopTimer;
    bool drifting;
    int driftDir, lastDriftLevel, boostColorLevel;
    ItemType pendingItem;
    float throttleHeldSince = -1f;

    float aiSkill, aiLane, aiSeed, aiItemTimer, aiStuck, aiReverse, aiSteer;

    Track track;
    RaceManager rm;
    Transform model;
    Transform driverBody;
    Transform driverHead;
    Transform[] wheelSpin = new Transform[4];
    Transform[] frontPivot = new Transform[2];
    Renderer starRenderer;
    MaterialPropertyBlock starMpb;
    Renderer[] tailLightRend = new Renderer[2];
    MaterialPropertyBlock tailMpb;
    TrailRenderer[] skidTrails = new TrailRenderer[2];
    bool isBraking;
    bool kenneyReady;
    float verticalVel;
    bool isAirborne;
    float airTrickSpin;
    float jumpCooldown;
    float bumpCooldown;

    public void Init(RaceManager rm, Track track, string name, Color color, bool isPlayer, int index, float lateral, float aiSkill)
    {
        this.rm = rm;
        this.track = track;
        Name = name;
        Color = color;
        IsPlayer = isPlayer;
        this.aiSkill = aiSkill;
        aiSeed = Random.value * 100f;
        aiLane = lateral * 0.5f;
        BuildModel();
        Setup3DEngineAudio();
        ResetTo(index, lateral);
    }

    void Setup3DEngineAudio()
    {
        if (IsPlayer) return;
        if (engine3D == null)
        {
            engine3D = gameObject.AddComponent<AudioSource>();
            engine3D.spatialBlend = 1.0f; // 3D音響
            engine3D.rolloffMode = AudioRolloffMode.Logarithmic;
            engine3D.minDistance = 2.0f;
            engine3D.maxDistance = 22f;
            engine3D.dopplerLevel = 0.25f;
            engine3D.loop = true;
            engine3D.playOnAwake = false;
            var clip = Resources.Load<AudioClip>("Audio/sfx_engine");
            if (clip != null)
            {
                engine3D.clip = clip;
                engine3D.volume = 0f;
                engine3D.Play();
            }
        }
    }

    public void RebuildModel()
    {
        if (model != null) { model.SetParent(null); model.gameObject.SetActive(false); Destroy(model.gameObject); model = null; }
        if (blobShadow != null) { blobShadow.SetParent(null); blobShadow.gameObject.SetActive(false); Destroy(blobShadow.gameObject); blobShadow = null; }
        var spot = transform.Find("HeadlightBeam");
        if (spot != null) { spot.SetParent(null); spot.gameObject.SetActive(false); Destroy(spot.gameObject); }
        var sk0 = transform.Find("Skidmark_0");
        if (sk0 != null) { sk0.SetParent(null); sk0.gameObject.SetActive(false); Destroy(sk0.gameObject); }
        var sk1 = transform.Find("Skidmark_1");
        if (sk1 != null) { sk1.SetParent(null); sk1.gameObject.SetActive(false); Destroy(sk1.gameObject); }
        if (starRenderer != null) { starRenderer.transform.SetParent(null); starRenderer.gameObject.SetActive(false); Destroy(starRenderer.gameObject); starRenderer = null; }

        BuildModel();
    }

    public void Hop() => hopT = 0f;

    public void ApplyStats(int speed, int accel, int handling, int weight)
    {
        topSpeedMul = 1f + (speed - 5) * 0.02f;     // 0.94〜1.06
        accelMul = 1f + (accel - 5) * 0.07f;        // 0.79〜1.21
        turnMul = 1f + (handling - 5) * 0.04f;      // 0.88〜1.12
        Mass = weight;
    }

    public void SetSkill(float skill) => aiSkill = skill;

    public void ResetTo(int index, float lateral)
    {
        Index = track.Wrap(index);
        transform.position = track.PointAt(Index, lateral);
        Heading = Quaternion.LookRotation(track.Dirs[Index]).eulerAngles.y;
        VelDir = Forward;
        Speed = 0;
        CurrentThrottle = 0f;
        throttleHeldSince = -1f;
        Lap = 0;
        MaxLap = 0;
        track.Locate(transform.position, ref Index, out Lateral, out Progress);
        Finished = false;
        Item = ItemType.None;
        RouletteTimer = 0;
        boostTimer = starTimer = spinTimer = invulnTimer = driftCharge = 0;
        slipstreamDraftTimer = slipstreamTimer = 0;
        trickWindowTimer = 0;
        trickDone = false;
        aiTrickDecided = false;
        InDraftStream = false;
        drifting = false;
        lastDriftLevel = 0;
        boostColorLevel = 0;
        driftFlarePopTimer = 0f;
        previewLockTimer = 0f;
        isBraking = false;
        spinAngle = 0;
        visYaw = 0f;
        visPitch = 0f;
        steerVis = 0f;
        hopT = 1f;
        verticalVel = 0;
        isAirborne = false;
        airTrickSpin = 0;
        jumpCooldown = 0;
        bumpCooldown = 0;
        aiSteer = 0;
        aiStuck = 0f;
        aiReverse = 0f;
        hasNet = false;
        snapBuffer.Clear();
        aiItemTimer = Random.Range(1f, 3f);
        if (engine3D != null) engine3D.volume = 0f;
        UpdateVisual(0, 0);
        if (skidTrails[0] != null)
        {
            skidTrails[0].emitting = false;
            skidTrails[1].emitting = false;
            skidTrails[0].Clear();
            skidTrails[1].Clear();
        }
    }

    float previewLockTimer;

    // ───────────────────────── 物理 ─────────────────────────

    public void Tick(float dt, bool canDrive)
    {
        if (!gameObject.activeSelf) return; // タイムアタックで非表示のカートはシミュレートしない
        if (IsRemote) { RemoteTick(dt); return; }
        if (previewLockTimer > 0f)
        {
            previewLockTimer -= dt;
            UpdateVisual(dt, drifting ? driftDir * 0.85f : 0f);
            Effects(dt);
            return;
        }
        if (!canDrive)
        {
            if (IsPlayer && !Finished && !rm.Demo)
            {
                var preInp = InputProvider != null ? InputProvider.GetInput(this, dt) : PlayerInput();
                CurrentThrottle = preInp.throttle;
                if (preInp.throttle > 0) { if (throttleHeldSince < 0) throttleHeldSince = Time.time; }
                else throttleHeldSince = -1f;
            }
            else
            {
                CurrentThrottle = 0f;
                throttleHeldSince = -1f;
            }
            if (engine3D != null) engine3D.volume = 0f;
            if (skidTrails[0] != null)
            {
                skidTrails[0].emitting = false;
                skidTrails[1].emitting = false;
            }
            UpdateVisual(dt, 0f);
            return;
        }

        var inp = InputProvider != null 
            ? InputProvider.GetInput(this, dt) 
            : ((IsPlayer && !Finished && !rm.Demo) ? PlayerInput() : AIInput(dt));
        CurrentThrottle = inp.throttle;

        if (IsPlayer && !rm.Demo)
        {
            if (inp.throttle > 0) { if (throttleHeldSince < 0) throttleHeldSince = Time.time; }
            else throttleHeldSince = -1f;
        }

        boostTimer -= dt;
        if (boostTimer <= 0) boostColorLevel = 0;
        driftFlarePopTimer = Mathf.Max(0f, driftFlarePopTimer - dt);
        bumpCooldown = Mathf.Max(0f, bumpCooldown - dt);
        starTimer -= dt;
        invulnTimer -= dt;

        if (RouletteTimer > 0)
        {
            float before = RouletteTimer;
            RouletteTimer -= dt;
            if (IsPlayer && Mathf.Floor(before / 0.08f) != Mathf.Floor(RouletteTimer / 0.08f)) rm.Audio.Tick();
            if (RouletteTimer <= 0)
            {
                Item = pendingItem;
                if (IsPlayer) rm.Audio.GotItem();
            }
        }

        if (spinTimer > 0)
        {
            spinTimer -= dt;
            Speed = Mathf.MoveTowards(Speed, 0, 22f * dt);
            drifting = false;
            driftCharge = 0;
            lastDriftLevel = 0;
        }
        else
        {
            if (inp.useItem && Item != ItemType.None && RouletteTimer <= 0) UseItem(inp);

            slipstreamTimer -= dt;

            // スリップストリーム（ドラフティング）判定
            bool draftingCandidate = false;
            if (canDrive && Speed > 13f && rm != null && rm.Karts != null)
            {
                var myPos = transform.position;
                var myFwd = Forward;
                for (int kIdx = 0; kIdx < rm.Karts.Count; kIdx++)
                {
                    var other = rm.Karts[kIdx];
                    if (other == this || other == null || !other.gameObject.activeSelf) continue;
                    var toOther = other.transform.position - myPos;
                    if (Mathf.Abs(toOther.y) > 2.2f) continue;
                    float fwdDist = Vector3.Dot(toOther, myFwd);
                    if (fwdDist > 2.5f && fwdDist < 16.0f)
                    {
                        float sideDist = Mathf.Abs(Vector3.Dot(toOther, transform.right));
                        if (sideDist < 1.9f && other.Speed > 10f)
                        {
                            draftingCandidate = true;
                            break;
                        }
                    }
                }
            }

            InDraftStream = draftingCandidate;
            if (draftingCandidate)
            {
                slipstreamDraftTimer += dt;
                if (slipstreamDraftTimer >= 1.15f)
                {
                    slipstreamDraftTimer = 0f;
                    slipstreamTimer = 1.6f;
                    boostColorLevel = 4; // スリップストリーム専用シアンジェット色
                    Boost(1.6f);
                    if (IsPlayer)
                    {
                        rm.Audio.Boost();
                        if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.35f, 0.85f, 0.35f);
                    }
                    Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.4f, 0.95f, 1f), 18, 6.5f, 0.38f);
                }
            }
            else
            {
                slipstreamDraftTimer = Mathf.MoveTowards(slipstreamDraftTimer, 0, dt * 1.5f);
            }

            if (!isAirborne)
            {
                if (inp.driftDown && !drifting)
                {
                    hopT = 0;
                    if (Mathf.Abs(inp.steer) > 0.3f && Speed > 10f)
                    {
                        drifting = true;
                        driftDir = inp.steer > 0 ? 1 : -1;
                        driftCharge = 0;
                        lastDriftLevel = 0;
                    }
                }
                if (drifting && (!inp.drift || Speed < 8f)) ReleaseDrift();
            }
            else if (drifting)
            {
                ReleaseDrift();
            }

            float max = MaxSpeed * topSpeedMul * AISpeedFactor();
            if (starTimer > 0) max *= 1.15f;
            if (slipstreamTimer > 0) max *= 1.18f;
            if (shockTimer > 0) max *= 0.72f;
            Offroad = Mathf.Abs(Lateral) > Track.HalfWidth + Track.CurbWidth;
            if (Offroad && boostTimer <= 0 && starTimer <= 0 && slipstreamTimer <= 0) max *= 0.45f;

            if (boostTimer > 0) Speed = Mathf.MoveTowards(Speed, Mathf.Max(max * 1.25f, MaxSpeed * 1.4f * topSpeedMul * AISpeedFactor()), 60f * dt);
            else if (slipstreamTimer > 0) Speed = Mathf.MoveTowards(Speed, max, 45f * dt);
            else if (Speed > max) Speed = Mathf.MoveTowards(Speed, max, 20f * dt);
            else if (inp.throttle > 0) Speed = Mathf.MoveTowards(Speed, max, 15f * accelMul * (1f - 0.55f * Speed / max) * dt);
            else if (inp.throttle < 0) Speed = Mathf.MoveTowards(Speed, -9f, (Speed > 0 ? 32f : 12f) * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, 7f * dt);

            float grip = Mathf.Clamp01(Mathf.Abs(Speed) / 5f) * (Speed < 0 ? -1f : 1f);
            float turn;
            if (drifting)
            {
                float s = Mathf.Lerp(0.45f, 1.4f, (inp.steer * driftDir + 1f) * 0.5f);
                turn = driftDir * s * 80f;
                driftCharge += dt * (0.75f + 0.5f * Mathf.Max(0, inp.steer * driftDir));
                int curLv = DriftLevel;
                if (curLv > lastDriftLevel)
                {
                    lastDriftLevel = curLv;
                    driftFlarePopTimer = 0.28f;
                    OnDriftLevelUp(curLv);
                }
            }
            else
            {
                turn = inp.steer * 95f * (1f - 0.3f * Mathf.Clamp01(Speed / MaxSpeed));
            }
            Heading += turn * turnMul * grip * dt;
        }

        var fwd = Forward;
        VelDir = Speed < 0 ? fwd : Vector3.Slerp(VelDir, fwd, (drifting ? 2.6f : 10f) * dt).normalized;
        Vector3 moveDelta = new Vector3(VelDir.x, 0, VelDir.z).normalized * Speed * dt;
        transform.position += moveDelta;

        // コース上の位置と周回
        float prevProgress = Progress;
        track.Locate(transform.position, ref Index, out Lateral, out Progress);
        float L = track.Length;
        if (prevProgress > L * 0.75f && Progress < L * 0.25f)
        {
            Lap++;
            if (Lap > MaxLap)
            {
                MaxLap = Lap;
                rm.OnLapCompleted(this);
                if (IsPlayer && Finished) GamepadHaptics.SetDrift(PlayerIndex, 0);
            }
        }
        else if (prevProgress < L * 0.25f && Progress > L * 0.75f) Lap--;

        // 壁（外壁 & 分岐区間の中央分離帯）
        float limit = Track.WallOffset - 1.7f; // 車体の半幅(ホイール含む)ぶん内側で止める
        float progressRatio = (float)Index / track.Count;
        bool inBranch = track.IsInBranch(progressRatio, out BranchDef branch, out float branchBlend);

        if (inBranch && branchBlend > 0.05f)
        {
            // 分岐区間の外壁リミット
            limit = Mathf.Lerp(limit, Mathf.Abs(branch.LeftOffset) + branch.LaneWidth + Track.CurbWidth + 1.5f, branchBlend);

            // 中央分離帯（川・内壁）の壁判定：実際に道路が二股に分かれている区間（Start〜End）でのみ有効
            if (progressRatio >= branch.Start && progressRatio <= branch.End)
            {
                float dividerLimit = (branch.DividerWidth + 0.8f) * branchBlend;
                if (Mathf.Abs(Lateral) < dividerLimit)
                {
                    float side = Lateral >= 0 ? 1f : -1f;
                    // 分離帯から外側（左車線または右車線）へ押し戻す
                    var pushDir = track.Rights[Index] * side;
                    transform.position += pushDir * (dividerLimit - Mathf.Abs(Lateral));
                    Lateral = side * dividerLimit;

                    // 中央に向かう速度成分（-pushDir）に対する衝突判定
                    float impact = -Vector3.Dot(VelDir, pushDir);
                    if (impact > 0)
                    {
                        VelDir = (VelDir + pushDir * impact * 1.4f).normalized;
                        Speed *= 1f - 0.45f * impact;
                        if (impact > 0.25f && Speed > 8f && bumpCooldown <= 0f)
                        {
                            bumpCooldown = 0.25f;
                            Fx.Burst(transform.position - pushDir * 1f + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.4f), 8, 5f, 0.3f);
                            if (IsPlayer)
                            {
                                float bumpInt = Mathf.Clamp01(impact * 1.25f * (Speed / MaxSpeed));
                                rm.Audio.Bump(bumpInt);
                                rm.Shake(0.25f * bumpInt);
                                if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.65f * bumpInt, 0.22f, 0.18f);
                            }
                        }
                    }
                }
            }
        }

        // 外壁判定
        if (Mathf.Abs(Lateral) > limit)
        {
            float side = Mathf.Sign(Lateral);
            var outward = track.Rights[Index] * side;
            transform.position -= outward * (Mathf.Abs(Lateral) - limit);
            Lateral = side * limit;
            float impact = Vector3.Dot(VelDir, outward);
            if (impact > 0)
            {
                VelDir = (VelDir - outward * impact * 1.4f).normalized;
                Speed *= 1f - 0.45f * impact;
                if (impact > 0.25f && Speed > 8f && bumpCooldown <= 0f)
                {
                    bumpCooldown = 0.25f;
                    Fx.Burst(transform.position + outward * 1f + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.4f), 8, 5f, 0.3f);
                    if (IsPlayer)
                    {
                        float bumpInt = Mathf.Clamp01(impact * 1.25f * (Speed / MaxSpeed));
                        rm.Audio.Bump(bumpInt);
                        rm.Shake(0.25f * bumpInt);
                        if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.65f * bumpInt, 0.22f, 0.18f);
                    }
                }
            }
        }

        // ダッシュボード
        foreach (var pad in track.BoostPads)
        {
            int di = track.Wrap(Index - pad.index);
            if ((di <= 1 || di >= track.Count - 2) && Mathf.Abs(Lateral - pad.lateral) < 2.4f && boostTimer < 0.8f)
            {
                boostColorLevel = 2;
                Boost(1.1f);
                if (IsPlayer)
                {
                    rm.Audio.Boost();
                    if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.2f, 0.85f, 0.25f);
                }
            }
        }

        // ジャンプ台判定
        if (jumpCooldown > 0) jumpCooldown -= dt;
        else if (track.JumpRamps != null)
        {
            foreach (var ramp in track.JumpRamps)
            {
                int di = track.Wrap(Index - ramp.index);
                float tol = Mathf.Abs(ramp.lateral) > 1f ? 4.8f : 7.8f;
                if ((di <= 1 || di >= track.Count - 2) && Mathf.Abs(Lateral - ramp.lateral) <= tol && Speed > 8f)
                {
                    LaunchJump(ramp.power);
                    break;
                }
            }
        }

        // 垂直方向の接地・重力・着地
        Vector3 roadGround = track.PointAt(Index, Lateral);
        float roadY = roadGround.y;
        var curPos = transform.position;

        if (isAirborne)
        {
            verticalVel -= 28f * dt; // 重力加速度
            curPos.y += verticalVel * dt;

            // 空中トリックの入力判定（踏み切り直後）
            if (trickWindowTimer > 0)
            {
                trickWindowTimer -= dt;
                bool doTrick = false;
                if (IsPlayer && !Finished && !rm.Demo)
                {
                    doTrick = inp.driftDown;
                }
                else if (!aiTrickDecided)
                {
                    aiTrickDecided = true;
                    doTrick = Random.value < (0.65f * aiSkill);
                }

                if (doTrick && !trickDone)
                {
                    trickDone = true;
                    trickWindowTimer = 0f;
                    Fx.Burst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.9f, 0.2f), 20, 7.5f, 0.45f);
                    if (IsPlayer)
                    {
                        rm.Audio.Boost();
                        rm.Shake(0.3f);
                        if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.45f, 0.95f, 0.35f);
                    }
                }
            }

            if (trickDone)
            {
                airTrickSpin += 1080f * dt; // トリック成功時のみ空中スピン演出
            }

            if (curPos.y <= roadY)
            {
                // 着地！
                curPos.y = roadY;
                verticalVel = 0;
                isAirborne = false;
                airTrickSpin = Mathf.Repeat(airTrickSpin + 180f, 360f) - 180f;

                if (trickDone)
                {
                    boostColorLevel = 1;
                    Boost(1.3f); // トリック成功スーパー着地ミニターボ！
                    Fx.Burst(curPos, new Color(0.2f, 0.9f, 1f), 18, 6.5f, 0.4f);
                }
                else
                {
                    Boost(0.65f); // 通常着地ミニターボ！
                }
                trickDone = false;
                trickWindowTimer = 0f;

                Fx.Smoke(curPos, -Forward * 3f + Vector3.up * 1.5f, new Color(0.9f, 0.9f, 0.95f, 0.6f), 1.2f, 0.5f, 3);
                if (IsPlayer)
                {
                    rm.Audio.Bump(0.42f);
                    rm.Shake(0.35f);
                    if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.50f, 0.2f, 0.15f);
                }
            }
            transform.position = curPos;
        }
        else
        {
            if (curPos.y > roadY + 0.9f && Speed > 20f && verticalVel <= 0)
            {
                if (drifting) ReleaseDrift();
                isAirborne = true;
                verticalVel = 4f;
                trickWindowTimer = 0.25f;
                trickDone = false;
                aiTrickDecided = false;
            }
            else
            {
                curPos.y = Mathf.Lerp(curPos.y, roadY, 1f - Mathf.Exp(-28f * dt));
            }
            transform.position = curPos;
        }

        // 奈落落下・コース外転落時の安全復帰（レスキュー）
        if (curPos.y < -12f)
        {
            curPos = track.PointAt(Index, 0f) + Vector3.up * 0.4f;
            Speed = Mathf.Clamp(Speed * 0.5f, -5f, 10f);
            verticalVel = 0f;
            isAirborne = false;
            airTrickSpin = 0f;
            trickDone = false;
            aiTrickDecided = false;
            trickWindowTimer = 0f;
            VelDir = Forward;
            transform.position = curPos;
            if (skidTrails[0] != null) { skidTrails[0].Clear(); skidTrails[1].Clear(); }
            if (IsPlayer)
            {
                rm.Shake(0.5f);
                rm.Audio.Bump();
                if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.8f, 0.4f, 0.3f);
            }
            Fx.Burst(curPos, new Color(0.3f, 0.8f, 1f), 18, 6f, 0.4f);
        }

        isBraking = inp.throttle < 0 && Speed > 2f;
        bool skidding = canDrive && !isAirborne && (drifting || (isBraking && Speed > 6f) || (spinTimer > 0 && Speed > 3f));
        if (skidTrails[0] != null)
        {
            skidTrails[0].emitting = skidding;
            skidTrails[1].emitting = skidding;
        }

        if (IsPlayer) GamepadHaptics.SetDrift(PlayerIndex, (!Finished && !rm.Demo && drifting) ? DriftLevel : 0);

        // ライバルカートの 3D エンジン音更新
        if (IsPlayer)
        {
            if (engine3D != null) engine3D.volume = 0f;
        }
        else
        {
            if (engine3D == null) Setup3DEngineAudio();
            if (engine3D != null && engine3D.isPlaying)
            {
                float spd01 = Mathf.Clamp01(Mathf.Abs(Speed) / MaxSpeed);
                float throttle01 = Mathf.Max(0f, inp.throttle);
                float targetPitch = Mathf.Lerp(0.78f, 1.25f, spd01 * 0.7f + throttle01 * 0.3f);
                engine3D.pitch = Mathf.MoveTowards(engine3D.pitch, targetPitch, dt * 2.5f);
                float targetVol = Mathf.Lerp(0.015f, 0.085f, spd01) * RaceAudio.MasterSfxVolume;
                engine3D.volume = Mathf.MoveTowards(engine3D.volume, targetVol, dt * 2.0f);
            }
        }

        bool wasShocked = shockTimer > 0;
        if (shockTimer > 0)
        {
            shockTimer -= dt;
            if (Random.value < 0.35f)
            {
                Vector3 sparkPos = transform.position + Vector3.up * (0.35f * transform.localScale.y) + Random.insideUnitSphere * 0.45f;
                Fx.Emit(sparkPos, Random.insideUnitSphere * 1.5f + Vector3.up * 0.5f, new Color(1.0f, 0.92f, 0.2f), 0.35f, 0.2f, 1, 0.9f);
                Fx.Emit(sparkPos, Random.insideUnitSphere * 1.2f, new Color(0.4f, 0.85f, 1f), 0.30f, 0.15f, 1, 0.9f);
            }
        }
        float targetScale = shockTimer > 0 ? 0.58f : 1.0f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, 10f * dt);
        if (wasShocked && shockTimer <= 0)
        {
            Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(1f, 0.95f, 0.4f), 14, 4f, 0.4f);
        }

        UpdateVisual(dt, spinTimer > 0 ? 0 : inp.steer);
        Effects(dt);
    }

    void ReleaseDrift()
    {
        int lv = DriftLevel;
        drifting = false;
        driftCharge = 0;
        lastDriftLevel = 0;
        if (IsPlayer)
        {
            GamepadHaptics.SetDrift(PlayerIndex, 0);
            if (lv > 0)
            {
                boostColorLevel = lv;
                Boost(0.35f + 0.35f * lv);
                OnDriftReleaseBoost(lv);
                rm.Audio.Pop(lv);
                if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.28f, 0.68f + 0.10f * lv, 0.26f);
            }
        }
        else if (lv > 0)
        {
            boostColorLevel = lv;
            Boost(0.35f + 0.35f * lv);
            OnDriftReleaseBoost(lv);
        }
    }

    public void Boost(float t)
    {
        boostTimer = Mathf.Max(boostTimer, t);
    }

    public void LaunchJump(float power)
    {
        if (drifting) ReleaseDrift();
        hopT = 1f;
        verticalVel = power;
        isAirborne = true;
        airTrickSpin = 0f;
        jumpCooldown = 2.2f;
        trickWindowTimer = 0.35f;
        trickDone = false;
        aiTrickDecided = false;
        Speed = Mathf.Max(Speed, MaxSpeed * 1.15f);
        if (IsPlayer)
        {
            rm.Audio.Boost();
            rm.Shake(0.35f);
        }
        Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(1f, 0.75f, 0.2f), 16, 7f, 0.4f);
    }

    public void OnGo()
    {
        if (IsRemote) return;
        // スタートダッシュ判定：GOの直前（約0.2〜0.85秒前）にアクセルを踏み始めると成功！
        // 逆に1.2秒以上前から押しっぱなしだとバーンアウト（空転ストール）！
        if (IsPlayer && !rm.Demo)
        {
            float held = throttleHeldSince < 0 ? -1f : Time.time - throttleHeldSince;
            if (held >= 0.15f && held <= 0.85f)
            {
                boostColorLevel = 2;
                Boost(1.5f);
                rm.Audio.Boost();
                GamepadHaptics.Vibrate(PlayerIndex, 0.4f, 1.0f, 0.45f);
                Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.2f), 18, 6.5f, 0.4f);
            }
            else if (held > 0.85f && held <= 1.2f)
            {
                boostColorLevel = 1;
                Boost(0.8f);
                rm.Audio.Boost();
                GamepadHaptics.Vibrate(PlayerIndex, 0.25f, 0.65f, 0.3f);
                Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.4f, 0.9f, 1f), 14, 5.5f, 0.35f);
            }
            else if (held > 1.2f)
            {
                // 早すぎるアクセル長押しによるバーンアウト
                spinTimer = 1.2f;
                Speed = -1.5f;
                rm.Audio.Bump();
                GamepadHaptics.Vibrate(PlayerIndex, 0.85f, 0.15f, 0.5f);
                Fx.Burst(transform.position, new Color(0.2f, 0.2f, 0.2f), 24, 7f, 0.6f);
                Fx.Smoke(transform.position, Vector3.up * 2f, new Color(0.3f, 0.3f, 0.3f, 0.9f), 1.8f, 1f, 6);
            }
        }
        else if (Random.value < 0.5f) Boost(Random.Range(0.4f, 1.0f));
    }

    public bool Spin()
    {
        if (IsRemote) return false; // スピンの判定は持ち主側で行い、状態として届く
        if (invulnTimer > 0 || starTimer > 0 || spinTimer > 0) return false;
        spinTimer = 1.2f;
        invulnTimer = 2.2f;
        Speed *= 0.35f;
        boostTimer = 0;
        drifting = false;
        Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.3f), 20, 7f, 0.5f);
        if (IsPlayer)
        {
            rm.Audio.Hit();
            rm.Shake(0.8f);
            if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 0.95f, 0.75f, 0.5f);
        }
        return true;
    }

    public bool Shock(float duration = 6.5f)
    {
        if (IsRemote) return false;
        if (starTimer > 0) return false;
        shockTimer = duration;
        spinTimer = 1.3f;
        Speed *= 0.25f;
        boostTimer = 0;
        drifting = false;
        // 所持アイテムを落とす
        Item = ItemType.None;
        RouletteTimer = 0;

        // 落雷エフェクト：頭上から地面へ稲妻スパーク
        Vector3 top = transform.position + Vector3.up * 7f;
        for (int i = 0; i <= 6; i++)
        {
            Vector3 seg = Vector3.Lerp(top, transform.position, i / 6f) + Random.insideUnitSphere * 0.45f;
            Fx.Burst(seg, new Color(1.0f, 0.95f, 0.25f), 6, 3.5f, 0.3f);
        }
        Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.4f, 0.85f, 1f), 24, 7f, 0.6f);
        Fx.Smoke(transform.position, Vector3.up * 1.5f, new Color(0.2f, 0.2f, 0.2f, 0.8f), 1.5f, 0.8f, 5);

        if (IsPlayer)
        {
            rm.Audio.Hit();
            rm.Shake(1.0f);
            if (!Finished && !rm.Demo) GamepadHaptics.Vibrate(PlayerIndex, 1.0f, 0.85f, 0.65f);
        }
        return true;
    }

    public bool GiveItem(ItemType it)
    {
        if (Item != ItemType.None || RouletteTimer > 0) return false;
        pendingItem = it;
        RouletteTimer = IsPlayer ? 1.3f : 0.6f;
        aiItemTimer = Random.Range(0.8f, 3.5f);
        return true;
    }

    void UseItem(KartInput inp)
    {
        var it = Item;
        Item = ItemType.None;
        switch (it)
        {
            case ItemType.Turbo:
                boostColorLevel = 3;
                Boost(1.4f);
                if (IsPlayer) rm.Audio.Boost();
                break;
            case ItemType.Banana:
                Vector3 bPos = transform.position - Forward * 2.8f;
                int bIdx = Index;
                track.Locate(bPos, ref bIdx, out float bLat, out _);
                bPos.y = track.PointAt(bIdx, bLat).y;
                rm.SpawnBanana(bPos, this);
                if (IsPlayer) rm.Audio.Drop();
                break;
            case ItemType.Missile:
                rm.SpawnMissile(this);
                if (IsPlayer) rm.Audio.Missile();
                break;
            case ItemType.Star:
                starTimer = 7.5f;
                Boost(0.8f);
                if (IsPlayer) rm.Audio.Star();
                break;
            case ItemType.Bomb:
                bool throwBackward = inp.throttle < -0.1f;
                Vector3 bombPos;
                Vector3 bombVel;
                if (throwBackward)
                {
                    bombPos = transform.position - Forward * 2.2f + Vector3.up * 0.7f;
                    bombVel = -Forward * (Speed * 0.35f + 5f) + Vector3.up * 2.2f;
                }
                else
                {
                    bombPos = transform.position + Forward * 2.2f + Vector3.up * 0.9f;
                    bombVel = Forward * (Speed + 15f) + Vector3.up * 7.5f;
                }
                rm.SpawnBomb(bombPos, bombVel, this);
                if (IsPlayer) rm.Audio.Drop();
                break;
            case ItemType.Thunder:
                rm.UseThunder(this);
                break;
        }
    }
}
