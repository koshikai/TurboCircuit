using UnityEngine;

public enum ItemType { None, Turbo, Banana, Missile, Shield }

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
    public bool IsPlayer;
    public int PlayerIndex;     // 人間操作時の識別（0 = P1、1 = P2）
    public bool IsRemote;       // オンライン対戦で相手側が計算しているカート（受信した状態を再現するだけ）
    public int ModelVariant = -1; // 人間操作時に使うキャラのモデル番号（-1 = 自動）

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
    public Vector3 VelDir;
    public ItemType Item;
    public float RouletteTimer;
    public bool Drifting => drifting;
    public int DriftLevel => driftCharge >= 2.6f ? 3 : driftCharge >= 1.6f ? 2 : driftCharge >= 0.8f ? 1 : 0;
    public bool Boosting => boostTimer > 0;
    public bool Shielded => shieldTimer > 0;
    public bool Offroad { get; private set; }
    public bool IsAirborne => isAirborne;
    public Vector3 Forward => Quaternion.Euler(0, Heading, 0) * Vector3.forward;

    float boostTimer, shieldTimer, spinTimer, invulnTimer, driftCharge, hopT = 1f, spinAngle, visYaw, wheelAngle, steerVis;
    bool drifting;
    int driftDir;
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
    Renderer shieldRenderer;
    MaterialPropertyBlock shieldMpb;
    Renderer[] tailLightRend = new Renderer[2];
    MaterialPropertyBlock tailMpb;
    TrailRenderer[] skidTrails = new TrailRenderer[2];
    bool isBraking;
    bool kenneyReady;
    float verticalVel;
    bool isAirborne;
    float airTrickSpin;
    float jumpCooldown;

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
        ResetTo(index, lateral);
    }

    public void RebuildModel()
    {
        if (model != null) Destroy(model.gameObject);
        var spot = transform.Find("HeadlightBeam");
        if (spot != null) Destroy(spot.gameObject);
        var sk0 = transform.Find("Skidmark_0");
        if (sk0 != null) Destroy(sk0.gameObject);
        var sk1 = transform.Find("Skidmark_1");
        if (sk1 != null) Destroy(sk1.gameObject);
        if (shieldRenderer != null) Destroy(shieldRenderer.gameObject);

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
        Lap = 0;
        MaxLap = 0;
        track.Locate(transform.position, ref Index, out Lateral, out Progress);
        Finished = false;
        Item = ItemType.None;
        RouletteTimer = 0;
        boostTimer = shieldTimer = spinTimer = invulnTimer = driftCharge = 0;
        drifting = false;
        isBraking = false;
        spinAngle = 0;
        verticalVel = 0;
        isAirborne = false;
        airTrickSpin = 0;
        jumpCooldown = 0;
        aiSteer = 0;
        hasNet = false;
        aiItemTimer = Random.Range(1f, 3f);
        if (skidTrails[0] != null) { skidTrails[0].Clear(); skidTrails[1].Clear(); }
        UpdateVisual(0, 0);
    }

    // ───────────────────────── 物理 ─────────────────────────

    public void Tick(float dt, bool canDrive)
    {
        if (IsRemote) { RemoteTick(dt); return; }
        var inp = (IsPlayer && !Finished && !rm.Demo) ? PlayerInput() : AIInput(dt);

        if (IsPlayer && !rm.Demo)
        {
            if (inp.throttle > 0) { if (throttleHeldSince < 0) throttleHeldSince = Time.time; }
            else throttleHeldSince = -1f;
        }

        if (!canDrive)
        {
            UpdateVisual(dt, inp.steer);
            return;
        }

        boostTimer -= dt;
        shieldTimer -= dt;
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
        }
        else
        {
            if (inp.useItem && Item != ItemType.None && RouletteTimer <= 0) UseItem();

            if (inp.driftDown && !drifting)
            {
                hopT = 0;
                if (Mathf.Abs(inp.steer) > 0.3f && Speed > 10f)
                {
                    drifting = true;
                    driftDir = inp.steer > 0 ? 1 : -1;
                    driftCharge = 0;
                }
            }
            if (drifting && (!inp.drift || Speed < 8f)) ReleaseDrift();

            float max = MaxSpeed * topSpeedMul * AISpeedFactor();
            if (shieldTimer > 0) max *= 1.12f;
            Offroad = Mathf.Abs(Lateral) > Track.HalfWidth + Track.CurbWidth;
            if (Offroad && boostTimer <= 0 && shieldTimer <= 0) max *= 0.45f;

            if (boostTimer > 0) Speed = Mathf.MoveTowards(Speed, MaxSpeed * 1.4f, 60f * dt);
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
            if (Lap > MaxLap) { MaxLap = Lap; rm.OnLapCompleted(this); }
        }
        else if (prevProgress < L * 0.25f && Progress > L * 0.75f) Lap--;

        // 壁
        float limit = Track.WallOffset - 1.7f; // 車体の半幅(ホイール含む)ぶん内側で止める
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
                if (impact > 0.25f && Speed > 8f)
                {
                    Fx.Burst(transform.position + outward * 1f + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.4f), 8, 5f, 0.3f);
                    if (IsPlayer) { rm.Audio.Bump(); rm.Shake(0.3f); }
                }
            }
        }

        // ダッシュボード
        foreach (var pad in track.BoostPads)
        {
            int di = track.Wrap(Index - pad.index);
            if ((di <= 1 || di >= track.Count - 2) && Mathf.Abs(Lateral - pad.lateral) < 2.4f && boostTimer < 0.8f)
            {
                Boost(1.1f);
                if (IsPlayer) rm.Audio.Boost();
            }
        }

        // ジャンプ台判定
        if (jumpCooldown > 0) jumpCooldown -= dt;
        else if (track.JumpRamps != null)
        {
            foreach (var ramp in track.JumpRamps)
            {
                int di = track.Wrap(Index - ramp.index);
                if ((di <= 1 || di >= track.Count - 2) && Mathf.Abs(Lateral) <= 7.8f && Speed > 8f)
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
            airTrickSpin += 720f * dt; // 空中トリックスピン

            if (curPos.y <= roadY)
            {
                // 着地！
                curPos.y = roadY;
                verticalVel = 0;
                isAirborne = false;
                airTrickSpin = 0;
                Boost(0.65f); // 着地ミニターボ！
                Fx.Smoke(curPos, -Forward * 3f + Vector3.up * 1.5f, new Color(0.9f, 0.9f, 0.95f, 0.6f), 1.2f, 0.5f, 3);
                if (IsPlayer) { rm.Audio.Bump(); rm.Shake(0.45f); }
            }
            transform.position = curPos;
        }
        else
        {
            curPos.y = Mathf.Lerp(curPos.y, roadY, 1f - Mathf.Exp(-28f * dt));
            if (curPos.y > roadY + 0.9f && Speed > 20f && verticalVel <= 0)
            {
                isAirborne = true;
                verticalVel = 4f;
            }
            transform.position = curPos;
        }

        // 奈落落下・コース外転落時の安全復帰（レスキュー）
        if (curPos.y < -12f || Mathf.Abs(Lateral) > Track.WallOffset + 15f)
        {
            curPos = track.PointAt(Index, 0f) + Vector3.up * 0.4f;
            Speed = Mathf.Clamp(Speed * 0.5f, -5f, 10f);
            verticalVel = 0f;
            isAirborne = false;
            airTrickSpin = 0f;
            VelDir = Forward;
            transform.position = curPos;
            if (IsPlayer)
            {
                rm.Shake(0.5f);
                rm.Audio.Bump();
            }
            Fx.Burst(curPos, new Color(0.3f, 0.8f, 1f), 18, 6f, 0.4f);
        }

        isBraking = inp.throttle < 0 && Speed > 2f;
        bool skidding = canDrive && (drifting || (isBraking && Speed > 6f) || (spinTimer > 0 && Speed > 3f));
        if (skidTrails[0] != null)
        {
            skidTrails[0].emitting = skidding;
            skidTrails[1].emitting = skidding;
        }

        Effects(dt);
        UpdateVisual(dt, spinTimer > 0 ? 0 : inp.steer);
    }

    void ReleaseDrift()
    {
        int lv = DriftLevel;
        drifting = false;
        driftCharge = 0;
        if (lv > 0)
        {
            Boost(0.35f + 0.35f * lv);
            if (IsPlayer) rm.Audio.Pop(lv);
        }
    }

    public void Boost(float t)
    {
        boostTimer = Mathf.Max(boostTimer, t);
    }

    public void LaunchJump(float power)
    {
        verticalVel = power;
        isAirborne = true;
        airTrickSpin = 0f;
        jumpCooldown = 2.2f;
        Speed = Mathf.Max(Speed, MaxSpeed * 1.15f);
        if (IsPlayer)
        {
            rm.Audio.Boost();
            rm.Shake(0.35f);
            rm.Banner("BIG JUMP!", new Color(1f, 0.7f, 0.2f), PlayerIndex);
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
                Boost(1.5f);
                rm.Audio.Boost();
                rm.Banner("ROCKET START!", new Color(1f, 0.85f, 0.2f), PlayerIndex);
            }
            else if (held > 0.85f && held <= 1.2f)
            {
                Boost(0.8f);
                rm.Audio.Boost();
                rm.Banner("GOOD START!", new Color(0.4f, 0.9f, 1f), PlayerIndex);
            }
            else if (held > 1.2f)
            {
                // 早すぎるアクセル長押しによるバーンアウト
                spinTimer = 1.2f;
                Speed = -1.5f;
                rm.Audio.Bump();
                rm.Banner("BURNOUT!", new Color(1f, 0.3f, 0.2f), PlayerIndex);
                Fx.Burst(transform.position, new Color(0.2f, 0.2f, 0.2f), 24, 7f, 0.6f);
                Fx.Smoke(transform.position, Vector3.up * 2f, new Color(0.3f, 0.3f, 0.3f, 0.9f), 1.8f, 1f, 6);
            }
        }
        else if (Random.value < 0.5f) Boost(Random.Range(0.4f, 1.0f));
    }

    public bool Spin()
    {
        if (IsRemote) return false; // スピンの判定は持ち主側で行い、状態として届く
        if (invulnTimer > 0 || shieldTimer > 0 || spinTimer > 0) return false;
        spinTimer = 1.2f;
        invulnTimer = 2.2f;
        Speed *= 0.35f;
        boostTimer = 0;
        drifting = false;
        Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.3f), 20, 7f, 0.5f);
        if (IsPlayer) { rm.Audio.Hit(); rm.Shake(0.8f); }
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

    void UseItem()
    {
        var it = Item;
        Item = ItemType.None;
        switch (it)
        {
            case ItemType.Turbo:
                Boost(1.4f);
                if (IsPlayer) rm.Audio.Boost();
                break;
            case ItemType.Banana:
                rm.SpawnBanana(transform.position - Forward * 2.8f, this);
                if (IsPlayer) rm.Audio.Drop();
                break;
            case ItemType.Missile:
                rm.SpawnMissile(this);
                if (IsPlayer) rm.Audio.Missile();
                break;
            case ItemType.Shield:
                shieldTimer = 6f;
                Boost(0.6f);
                if (IsPlayer) rm.Audio.Shield();
                break;
        }
    }
}
