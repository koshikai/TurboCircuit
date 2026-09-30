using UnityEngine;

public enum ItemType { None, Turbo, Banana, Missile, Shield }

public struct KartInput
{
    public float throttle, steer;
    public bool drift, driftDown, useItem;
}

// カート本体。プレイヤーも CPU も同じ物理で動き、入力の出どころだけが違う。
public class Kart : MonoBehaviour
{
    public const float MaxSpeed = 30f;
    public const float Radius = 1.15f;

    public string Name;
    public Color Color;
    public bool IsPlayer;

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
    public Vector3 Forward => Quaternion.Euler(0, Heading, 0) * Vector3.forward;

    float boostTimer, shieldTimer, spinTimer, invulnTimer, driftCharge, hopT = 1f, spinAngle, visYaw, wheelAngle, steerVis;
    bool drifting;
    int driftDir;
    ItemType pendingItem;
    float throttleHeldSince = -1f;

    float aiSkill, aiLane, aiSeed, aiItemTimer, aiStuck, aiReverse;

    Track track;
    RaceManager rm;
    Transform model;
    Transform[] wheelSpin = new Transform[4];
    Transform[] frontPivot = new Transform[2];
    Renderer shieldRenderer;
    MaterialPropertyBlock shieldMpb;

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
        spinAngle = 0;
        aiItemTimer = Random.Range(1f, 3f);
        UpdateVisual(0, 0);
    }

    // ───────────────────────── 見た目 ─────────────────────────

    void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        var paint = new MaterialPropertyBlock();
        paint.SetColor("_Color", Color);
        var dark = new MaterialPropertyBlock();
        dark.SetColor("_Color", Color * 0.55f);

        Part(PrimitiveType.Cube, new Vector3(0, 0.38f, 0), new Vector3(1.4f, 0.28f, 2.3f), rm.kartPaintMaterial, paint);
        Part(PrimitiveType.Cube, new Vector3(0, 0.42f, 1.3f), new Vector3(1.0f, 0.24f, 0.6f), rm.kartPaintMaterial, paint, Quaternion.Euler(12, 0, 0));
        Part(PrimitiveType.Cube, new Vector3(0, 0.3f, 1.62f), new Vector3(1.7f, 0.16f, 0.22f), rm.chromeMaterial);
        Part(PrimitiveType.Cube, new Vector3(-0.78f, 0.38f, 0.05f), new Vector3(0.3f, 0.32f, 1.3f), rm.kartPaintMaterial, dark);
        Part(PrimitiveType.Cube, new Vector3(0.78f, 0.38f, 0.05f), new Vector3(0.3f, 0.32f, 1.3f), rm.kartPaintMaterial, dark);
        Part(PrimitiveType.Cube, new Vector3(0, 1.0f, -1.2f), new Vector3(1.7f, 0.08f, 0.45f), rm.kartPaintMaterial, paint);
        Part(PrimitiveType.Cube, new Vector3(-0.55f, 0.72f, -1.15f), new Vector3(0.08f, 0.5f, 0.1f), rm.chromeMaterial);
        Part(PrimitiveType.Cube, new Vector3(0.55f, 0.72f, -1.15f), new Vector3(0.08f, 0.5f, 0.1f), rm.chromeMaterial);
        Part(PrimitiveType.Cube, new Vector3(0, 0.62f, -0.85f), new Vector3(0.8f, 0.36f, 0.5f), rm.chromeMaterial);
        Part(PrimitiveType.Cylinder, new Vector3(-0.25f, 0.62f, -1.2f), new Vector3(0.16f, 0.18f, 0.16f), rm.chromeMaterial, null, Quaternion.Euler(90, 0, 0));
        Part(PrimitiveType.Cylinder, new Vector3(0.25f, 0.62f, -1.2f), new Vector3(0.16f, 0.18f, 0.16f), rm.chromeMaterial, null, Quaternion.Euler(90, 0, 0));
        Part(PrimitiveType.Cube, new Vector3(0, 0.72f, -0.5f), new Vector3(0.7f, 0.55f, 0.18f), rm.tireMaterial);
        // ドライバー
        Part(PrimitiveType.Capsule, new Vector3(0, 0.95f, -0.22f), new Vector3(0.55f, 0.38f, 0.45f), rm.kartPaintMaterial, dark);
        Part(PrimitiveType.Sphere, new Vector3(0, 1.45f, -0.15f), new Vector3(0.58f, 0.58f, 0.6f), rm.kartPaintMaterial, paint);
        Part(PrimitiveType.Cube, new Vector3(0, 1.47f, 0.1f), new Vector3(0.42f, 0.16f, 0.12f), rm.visorMaterial);
        Part(PrimitiveType.Cylinder, new Vector3(0, 0.98f, 0.35f), new Vector3(0.36f, 0.025f, 0.36f), rm.tireMaterial, null, Quaternion.Euler(-60, 0, 0));
        Part(PrimitiveType.Sphere, new Vector3(-0.25f, 0.98f, 0.3f), Vector3.one * 0.14f, rm.skinMaterial);
        Part(PrimitiveType.Sphere, new Vector3(0.25f, 0.98f, 0.3f), Vector3.one * 0.14f, rm.skinMaterial);

        // タイヤ（前輪はステアリングで向きが変わる）
        var wheelPos = new[] { new Vector3(-0.88f, 0.35f, 0.9f), new Vector3(0.88f, 0.35f, 0.9f), new Vector3(-0.9f, 0.4f, -0.85f), new Vector3(0.9f, 0.4f, -0.85f) };
        for (int i = 0; i < 4; i++)
        {
            var pivot = new GameObject("WheelPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = wheelPos[i];
            if (i < 2) frontPivot[i] = pivot;
            var spin = new GameObject("WheelSpin").transform;
            spin.SetParent(pivot, false);
            wheelSpin[i] = spin;
            float d = i < 2 ? 0.7f : 0.8f, w = i < 2 ? 0.18f : 0.26f;
            var tire = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(d, w, d), rm.tireMaterial, null, Quaternion.Euler(0, 0, 90));
            tire.transform.SetParent(spin, false);
            var hub = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(d * 0.5f, w * 1.1f, d * 0.5f), rm.chromeMaterial, null, Quaternion.Euler(0, 0, 90));
            hub.transform.SetParent(spin, false);
        }

        // シールド時のバリア
        var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bubble.GetComponent<Collider>());
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0, 0.8f, 0);
        bubble.transform.localScale = new Vector3(3.2f, 2.6f, 3.8f);
        shieldRenderer = bubble.GetComponent<Renderer>();
        shieldRenderer.sharedMaterial = rm.glowMaterial;
        shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shieldMpb = new MaterialPropertyBlock();
        bubble.SetActive(false);
    }

    GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, MaterialPropertyBlock mpb = null, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(model, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (mpb != null) r.SetPropertyBlock(mpb);
        return go;
    }

    void UpdateVisual(float dt, float steer)
    {
        transform.rotation = Quaternion.Euler(0, Heading, 0);

        float targetYaw = drifting ? driftDir * 24f : 0f;
        visYaw = Mathf.Lerp(visYaw, targetYaw, 1f - Mathf.Exp(-10f * dt));

        hopT += dt;
        float hop = hopT < 0.28f ? Mathf.Sin(Mathf.PI * hopT / 0.28f) * 0.4f : 0f;

        if (spinTimer > 0) spinAngle += 700f * dt;
        else spinAngle = Mathf.MoveTowards(Mathf.Repeat(spinAngle + 180f, 360f) - 180f, 0, 900f * dt);

        steerVis = Mathf.Lerp(steerVis, steer, 1f - Mathf.Exp(-12f * dt));
        float roll = -steerVis * 4f * Mathf.Clamp01(Speed / MaxSpeed) - (drifting ? driftDir * 5f : 0f);
        float bob = Offroad && Speed > 5f ? Mathf.Sin(Time.time * 40f) * 0.04f : 0f;
        model.localPosition = new Vector3(0, hop + bob, 0);
        model.localRotation = Quaternion.Euler(0, visYaw + spinAngle, roll);

        wheelAngle += Speed * dt / 0.37f * Mathf.Rad2Deg;
        for (int i = 0; i < 4; i++) wheelSpin[i].localRotation = Quaternion.Euler(wheelAngle, 0, 0);
        for (int i = 0; i < 2; i++) frontPivot[i].localRotation = Quaternion.Euler(0, steerVis * 28f, 0);

        bool sh = shieldTimer > 0;
        if (shieldRenderer.gameObject.activeSelf != sh) shieldRenderer.gameObject.SetActive(sh);
        if (sh)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(Time.time * 1.5f, 1f), 0.7f, 1f) * 0.35f;
            if (shieldTimer < 1.5f && Mathf.Repeat(Time.time, 0.2f) < 0.1f) c *= 0.2f;
            shieldMpb.SetColor("_TintColor", c);
            shieldMpb.SetColor("_Color", c);
            shieldRenderer.SetPropertyBlock(shieldMpb);
        }
    }

    // ───────────────────────── 入力 ─────────────────────────

    KartInput PlayerInput()
    {
        var inp = new KartInput();
        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.JoystickButton0);
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.JoystickButton1);
        inp.throttle = up ? 1f : down ? -1f : 0f;
        inp.steer = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        inp.drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.JoystickButton5);
        inp.driftDown = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.JoystickButton5);
        inp.useItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.JoystickButton2);
        return inp;
    }

    KartInput AIInput(float dt)
    {
        var inp = new KartInput();
        int look = 5 + Mathf.RoundToInt(Mathf.Abs(Speed) * 0.28f);
        int ti = track.Wrap(Index + look);
        float lane = aiLane + (Mathf.PerlinNoise(Time.time * 0.12f, aiSeed) - 0.5f) * 7f;
        lane = Mathf.Clamp(lane, -Track.HalfWidth + 2f, Track.HalfWidth - 2f);
        var target = track.PointAt(ti, lane);
        float ang = Vector3.SignedAngle(Forward, target - transform.position, Vector3.up);
        float bend = Vector3.SignedAngle(track.Dirs[Index], track.Dirs[track.Wrap(Index + 16)], Vector3.up);

        inp.steer = Mathf.Clamp(ang / 18f, -1f, 1f);
        inp.throttle = Mathf.Abs(ang) > 80f ? -1f : 1f;

        // 急カーブではドリフトしてミニターボを狙う
        if (!drifting && Mathf.Abs(bend) > 32f && Speed > 17f && Mathf.Sign(bend) == Mathf.Sign(ang) && Mathf.Abs(ang) > 4f)
        {
            inp.drift = true; inp.driftDown = true;
        }
        else if (drifting && Mathf.Abs(bend) > 10f && Mathf.Sign(bend) == driftDir) inp.drift = true;

        // 壁に引っかかったらバック
        if (Speed < 3f && rm.RaceRunning) aiStuck += dt; else aiStuck = 0;
        if (aiStuck > 1.2f) { aiReverse = 0.9f; aiStuck = 0; }
        if (aiReverse > 0)
        {
            aiReverse -= dt;
            inp.throttle = -1f;
            inp.steer = -Mathf.Sign(ang);
            inp.drift = false;
        }

        if (Item != ItemType.None && RouletteTimer <= 0)
        {
            aiItemTimer -= dt;
            bool good = Item != ItemType.Banana || rm.KartBehindWithin(this, 25f);
            if (aiItemTimer <= 0 && (good || aiItemTimer < -4f)) inp.useItem = true;
        }
        return inp;
    }

    float AISpeedFactor()
    {
        if (IsPlayer && !Finished && !rm.Demo) return 1f;
        float f = aiSkill;
        var player = rm.Player;
        if (player != null && !player.Finished && player != this)
        {
            // ラバーバンド：プレイヤーとの差に応じて少し速さを調整
            float diff = player.RaceDistance - RaceDistance;
            f += Mathf.Clamp(diff / 250f, -0.08f, 0.07f);
        }
        return Mathf.Clamp(f, 0.84f, 1.02f);
    }

    // ───────────────────────── 物理 ─────────────────────────

    public void Tick(float dt, bool canDrive)
    {
        var inp = (IsPlayer && !Finished && !rm.Demo) ? PlayerInput() : AIInput(dt);

        if (IsPlayer)
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

            float max = MaxSpeed * AISpeedFactor();
            if (shieldTimer > 0) max *= 1.12f;
            Offroad = Mathf.Abs(Lateral) > Track.HalfWidth + Track.CurbWidth;
            if (Offroad && boostTimer <= 0 && shieldTimer <= 0) max *= 0.45f;

            if (boostTimer > 0) Speed = Mathf.MoveTowards(Speed, MaxSpeed * 1.4f, 60f * dt);
            else if (Speed > max) Speed = Mathf.MoveTowards(Speed, max, 20f * dt);
            else if (inp.throttle > 0) Speed = Mathf.MoveTowards(Speed, max, 15f * (1f - 0.55f * Speed / max) * dt);
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
            Heading += turn * grip * dt;
        }

        var fwd = Forward;
        VelDir = Speed < 0 ? fwd : Vector3.Slerp(VelDir, fwd, (drifting ? 2.6f : 10f) * dt).normalized;
        transform.position += VelDir * Speed * dt;

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
        float limit = Track.WallOffset - 0.9f;
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

        Effects(dt);
        UpdateVisual(dt, spinTimer > 0 ? 0 : inp.steer);
    }

    void Effects(float dt)
    {
        var rearL = transform.TransformPoint(new Vector3(-0.9f, 0.15f, -1.0f));
        var rearR = transform.TransformPoint(new Vector3(0.9f, 0.15f, -1.0f));
        var back = -Forward;

        if (drifting)
        {
            int lv = DriftLevel;
            Color c = lv == 3 ? new Color(1f, 0.3f, 1f) : lv == 2 ? new Color(1f, 0.55f, 0.1f) : lv == 1 ? new Color(0.3f, 0.65f, 1f) : new Color(0.8f, 0.8f, 0.8f, 0.5f);
            float size = lv == 0 ? 0.5f : 0.35f + lv * 0.08f;
            var vel = back * 3f + Vector3.up * (lv > 0 ? 3f : 1f);
            Fx.Emit(rearL, vel, c, size, lv == 0 ? 0.4f : 0.25f, 1, lv > 0 ? 2.5f : 0.8f);
            Fx.Emit(rearR, vel, c, size, lv == 0 ? 0.4f : 0.25f, 1, lv > 0 ? 2.5f : 0.8f);
        }
        if (boostTimer > 0)
        {
            var ex = transform.TransformPoint(new Vector3(0, 0.62f, -1.35f));
            Fx.Emit(ex, back * 8f + VelDir * Speed * 0.8f, new Color(1f, 0.6f, 0.15f), 0.7f, 0.18f, 2, 1f);
            Fx.Emit(ex, back * 6f + VelDir * Speed * 0.8f, new Color(0.4f, 0.7f, 1f), 0.4f, 0.12f, 1, 0.5f);
        }
        if (Offroad && Speed > 6f && Random.value < 0.6f)
            Fx.Emit((rearL + rearR) * 0.5f, back * 2f + Vector3.up * 2f, new Color(0.7f, 0.6f, 0.4f, 0.6f), 0.9f, 0.5f, 1, 1.5f);
        if (shieldTimer > 0 && Random.value < 0.5f)
            Fx.Emit(transform.position + Vector3.up * 0.8f + Random.onUnitSphere * 1.5f, Vector3.up, Color.HSVToRGB(Random.value, 0.6f, 1f), 0.3f, 0.4f);
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

    public void OnGo()
    {
        // スタートダッシュ：「1」が出てから GO までにアクセルを押し始めると成功
        if (IsPlayer)
        {
            float held = throttleHeldSince < 0 ? -1f : Time.time - throttleHeldSince;
            if (held >= 0 && held < 1.1f) { Boost(1.2f); rm.Audio.Boost(); rm.Banner("ROCKET START!", new Color(1f, 0.8f, 0.2f)); }
        }
        else if (Random.value < 0.5f) Boost(Random.Range(0.4f, 1.0f));
    }

    public bool Spin()
    {
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
