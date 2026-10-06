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

    // ───────────────────────── 見た目 ─────────────────────────

    static readonly string[] KenneyKarts = { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" };

    void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        var paint = new MaterialPropertyBlock();
        paint.SetColor("_Color", Color);
        var dark = new MaterialPropertyBlock();
        dark.SetColor("_Color", Color * 0.55f);

        kenneyReady = TryBuildKenneyModel();
        if (!kenneyReady)
        {
            // 従来のプロシージャルボディ
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

            // タイヤ
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
        }

        // ヘッドライト（左右フロント）
        var hlMat = rm.headlightMaterial;
        float hlZ = kenneyReady ? 1.05f : 1.46f;
        float hlY = kenneyReady ? 0.30f : 0.44f;
        float hlX = kenneyReady ? 0.38f : 0.46f;
        Part(PrimitiveType.Cube, new Vector3(-hlX, hlY, hlZ), new Vector3(0.18f, 0.12f, 0.08f), hlMat);
        Part(PrimitiveType.Cube, new Vector3(hlX, hlY, hlZ), new Vector3(0.18f, 0.12f, 0.08f), hlMat);
        if (IsPlayer)
        {
            var spotGo = new GameObject("HeadlightBeam");
            spotGo.transform.SetParent(transform, false);
            spotGo.transform.localPosition = new Vector3(0, kenneyReady ? 0.40f : 0.55f, kenneyReady ? 1.15f : 1.5f);
            spotGo.transform.localRotation = Quaternion.Euler(12, 0, 0);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 38f;
            spot.spotAngle = 55f;
            spot.intensity = 2.4f;
            spot.color = new Color(1f, 0.98f, 0.92f);
        }

        // テールランプ（左右リア・ブレーキ連動）
        tailMpb = new MaterialPropertyBlock();
        var tlMat = rm.taillightMaterial;
        float tlZ = kenneyReady ? -0.74f : -1.24f;
        float tlY = kenneyReady ? 0.36f : 0.52f;
        float tlX = kenneyReady ? 0.34f : 0.46f;
        var tl1 = Part(PrimitiveType.Cube, new Vector3(-tlX, tlY, tlZ), new Vector3(0.18f, 0.12f, 0.08f), tlMat);
        var tl2 = Part(PrimitiveType.Cube, new Vector3(tlX, tlY, tlZ), new Vector3(0.18f, 0.12f, 0.08f), tlMat);
        tailLightRend[0] = tl1.GetComponent<Renderer>();
        tailLightRend[1] = tl2.GetComponent<Renderer>();

        if (!kenneyReady)
        {
            // プロシージャルボディ時のみ排気管・ドライバー・ステアリングを生成（Kenneyモデルには元からドライバーとマフラーが造形済み）
            Part(PrimitiveType.Cylinder, new Vector3(-0.28f, 0.28f, -1.22f), new Vector3(0.14f, 0.22f, 0.14f), rm.chromeMaterial, null, Quaternion.Euler(90, 0, 0));
            Part(PrimitiveType.Cylinder, new Vector3(0.28f, 0.28f, -1.22f), new Vector3(0.14f, 0.22f, 0.14f), rm.chromeMaterial, null, Quaternion.Euler(90, 0, 0));

            var bodyGo = Part(PrimitiveType.Capsule, new Vector3(0, 0.95f, -0.22f), new Vector3(0.55f, 0.38f, 0.45f), rm.kartPaintMaterial, dark);
            driverBody = bodyGo.transform;

            var headGo = Part(PrimitiveType.Sphere, new Vector3(0, 1.45f, -0.15f), new Vector3(0.58f, 0.58f, 0.60f), rm.kartPaintMaterial, paint);
            driverHead = headGo.transform;

            Part(PrimitiveType.Cube, new Vector3(0, 1.47f, 0.1f), new Vector3(0.42f, 0.16f, 0.12f), rm.visorMaterial).transform.SetParent(driverHead, true);
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.98f, 0.35f), new Vector3(0.36f, 0.025f, 0.36f), rm.tireMaterial, null, Quaternion.Euler(-60, 0, 0));
            Part(PrimitiveType.Sphere, new Vector3(-0.25f, 0.98f, 0.3f), Vector3.one * 0.14f, rm.skinMaterial);
            Part(PrimitiveType.Sphere, new Vector3(0.25f, 0.98f, 0.3f), Vector3.one * 0.14f, rm.skinMaterial);
        }

        // スキッドマーク（タイヤ痕）
        float skidX = kenneyReady ? 0.78f : 0.88f;
        float skidZ = kenneyReady ? -0.52f : -0.85f;
        for (int i = 0; i < 2; i++)
        {
            var trGo = new GameObject("Skidmark_" + i);
            trGo.transform.SetParent(transform, false);
            trGo.transform.localPosition = new Vector3(i == 0 ? -skidX : skidX, 0.04f, skidZ);
            trGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tr = trGo.AddComponent<TrailRenderer>();
            tr.time = 4.5f;
            tr.startWidth = 0.28f;
            tr.endWidth = 0.26f;
            tr.material = rm.skidmarkMaterial;
            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.minVertexDistance = 0.35f;
            tr.emitting = false;
            skidTrails[i] = tr;
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

    bool TryBuildKenneyModel()
    {
        int variant = ModelVariant >= 0 ? ModelVariant : IsPlayer ? (rm != null ? rm.SelectedKart : 0) : (Mathf.Abs(Index + 1) % KenneyKarts.Length);
        var kartPrefab = Resources.Load<GameObject>("Karts/" + KenneyKarts[variant]);
        var wheelPrefab = Resources.Load<GameObject>("Karts/wheel-racing") ?? Resources.Load<GameObject>("Karts/wheel-default");
        if (kartPrefab == null) return false;

        var body = Instantiate(kartPrefab, model);
        body.transform.localPosition = new Vector3(0, 0.15f, 0);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * 1.5f;
        foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);

        var paint = new MaterialPropertyBlock();
        paint.SetColor("_Color", Color.Lerp(Color.white, Color, 0.65f));
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            r.SetPropertyBlock(paint);
        }

        var wheelPos = new[]
        {
            new Vector3(-0.76f, 0.32f, 0.60f),  // 前左
            new Vector3(0.76f, 0.32f, 0.60f),   // 前右
            new Vector3(-0.78f, 0.34f, -0.52f), // 後左
            new Vector3(0.78f, 0.34f, -0.52f)  // 後右
        };

        for (int i = 0; i < 4; i++)
        {
            var pivot = new GameObject("WheelPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = wheelPos[i];
            if (i < 2) frontPivot[i] = pivot;
            var spin = new GameObject("WheelSpin").transform;
            spin.SetParent(pivot, false);
            wheelSpin[i] = spin;

            if (wheelPrefab != null)
            {
                var w = Instantiate(wheelPrefab, spin);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = i % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                w.transform.localScale = Vector3.one * 1.35f;
                foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                float d = i < 2 ? 0.7f : 0.8f, width = i < 2 ? 0.18f : 0.26f;
                var tire = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(d, width, d), rm.tireMaterial, null, Quaternion.Euler(0, 0, 90));
                tire.transform.SetParent(spin, false);
            }
        }
        return true;
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
        // 路面の勾配（ピッチ角）とバンク角（ロール）
        float roadPitch = 0f;
        float roadRoll = 0f;
        if (track != null && track.Count > 0 && Index >= 0 && Index < track.Count)
        {
            var rDir = track.Dirs[Index];
            var rNorm = track.Normals[Index];
            roadPitch = -Mathf.Asin(Mathf.Clamp(rDir.y, -0.9f, 0.9f)) * Mathf.Rad2Deg;
            roadRoll = Vector3.Dot(rNorm, track.Rights[Index]) * 20f;
        }
        transform.rotation = Quaternion.Euler(roadPitch, Heading, roadRoll);

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

        float trickAngle = isAirborne ? airTrickSpin : 0f;
        model.localRotation = Quaternion.Euler(0, visYaw + spinAngle + trickAngle, roll);

        wheelAngle += Speed * dt / 0.37f * Mathf.Rad2Deg;
        for (int i = 0; i < 4; i++) wheelSpin[i].localRotation = Quaternion.Euler(wheelAngle, 0, 0);
        for (int i = 0; i < 2; i++) frontPivot[i].localRotation = Quaternion.Euler(0, steerVis * 28f, 0);

        // ドライバーのアニメーション（ドリフト時のハングオン傾きと視線）
        if (driverBody != null && driverHead != null)
        {
            float targetLean = drifting ? driftDir * 18f : -steerVis * 6f;
            float targetLook = drifting ? driftDir * 22f : steerVis * 20f;
            driverBody.localRotation = Quaternion.Euler(0, 0, -targetLean);
            driverHead.localRotation = Quaternion.Euler(0, targetLook, -targetLean * 0.5f);
        }

        // テールランプ（ブレーキ時の強烈な赤発光）
        if (tailLightRend[0] != null && tailMpb != null)
        {
            Color emitCol = isBraking ? new Color(2.4f, 0.15f, 0.15f) : new Color(0.6f, 0.04f, 0.04f);
            tailMpb.SetColor("_EmissionColor", emitCol);
            tailLightRend[0].SetPropertyBlock(tailMpb);
            tailLightRend[1].SetPropertyBlock(tailMpb);
        }

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

    // ───────────────────────── オンライン同期 ─────────────────────────

    NetKartState net;
    bool hasNet;
    float netAge;

    public NetKartState MakeNetState()
    {
        byte f = 0;
        if (drifting) f |= NetKartState.FDrift;
        if (boostTimer > 0) f |= NetKartState.FBoost;
        if (shieldTimer > 0) f |= NetKartState.FShield;
        if (spinTimer > 0) f |= NetKartState.FSpin;
        if (isAirborne) f |= NetKartState.FAir;
        if (isBraking) f |= NetKartState.FBrake;
        if (Finished) f |= NetKartState.FDone;
        return new NetKartState
        {
            Pos = transform.position, Heading = Heading, Speed = Speed, Steer = steerVis, Progress = Progress,
            FinishTime = FinishTime, Flags = f, DriftLevel = DriftLevel, DriftDir = driftDir, Lap = Lap, MaxLap = MaxLap,
        };
    }

    public void ApplyNetState(NetKartState s)
    {
        net = s;
        netAge = 0;
        if (!hasNet) { transform.position = s.Pos; Heading = s.Heading; }
        hasNet = true;
    }

    // 受信した状態に追従し、見た目とエフェクトだけを再現する
    void RemoteTick(float dt)
    {
        if (hasNet)
        {
            netAge += dt;
            Vector3 fwd = Quaternion.Euler(0, net.Heading, 0) * Vector3.forward;
            Vector3 target = net.Pos + fwd * net.Speed * Mathf.Min(netAge, 0.25f);
            float k = 1f - Mathf.Exp(-14f * dt);
            transform.position = (target - transform.position).sqrMagnitude > 64f ? target : Vector3.Lerp(transform.position, target, k);
            Heading = Mathf.LerpAngle(Heading, net.Heading, k);
            Speed = net.Speed;
            VelDir = Forward;

            byte f = net.Flags;
            drifting = (f & NetKartState.FDrift) != 0;
            driftDir = net.DriftDir == 0 ? 1 : net.DriftDir;
            driftCharge = net.DriftLevel == 3 ? 2.7f : net.DriftLevel == 2 ? 1.7f : net.DriftLevel == 1 ? 0.9f : 0f;
            boostTimer = (f & NetKartState.FBoost) != 0 ? 0.3f : 0f;
            shieldTimer = (f & NetKartState.FShield) != 0 ? 3f : 0f;
            spinTimer = (f & NetKartState.FSpin) != 0 ? 0.3f : 0f;
            isAirborne = (f & NetKartState.FAir) != 0;
            isBraking = (f & NetKartState.FBrake) != 0;
            Lap = net.Lap;
            MaxLap = net.MaxLap;
            Finished = (f & NetKartState.FDone) != 0;
            FinishTime = net.FinishTime;
        }
        airTrickSpin = isAirborne ? airTrickSpin + 720f * dt : 0f;
        track.Locate(transform.position, ref Index, out Lateral, out float localProgress);
        Progress = hasNet ? net.Progress : localProgress;
        Offroad = Mathf.Abs(Lateral) > Track.HalfWidth + Track.CurbWidth;

        bool skidding = drifting || (isBraking && Speed > 6f) || (spinTimer > 0 && Speed > 3f);
        if (skidTrails[0] != null)
        {
            skidTrails[0].emitting = skidding;
            skidTrails[1].emitting = skidding;
        }
        Effects(dt);
        UpdateVisual(dt, spinTimer > 0 ? 0 : net.Steer);
    }

    // ───────────────────────── 入力 ─────────────────────────

    KartInput PlayerInput()
    {
        var inp = new KartInput();
        if (rm.TwoPlayer) return SplitScreenInput(inp);
        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.JoystickButton0);
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.JoystickButton1);
        inp.throttle = up ? 1f : down ? -1f : 0f;
        inp.steer = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        inp.drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.JoystickButton5);
        inp.driftDown = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.JoystickButton5);
        inp.useItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton4);
        return inp;
    }

    // 2P 対戦時のキー割り当て：P1 = WASD、P2 = 矢印キー（キーボード 1 台で操作できる）
    KartInput SplitScreenInput(KartInput inp)
    {
        if (PlayerIndex == 0)
        {
            inp.throttle = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
            inp.steer = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            inp.drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);
            inp.driftDown = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
            inp.useItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl);
        }
        else
        {
            inp.throttle = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
            inp.steer = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            inp.drift = Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.Period);
            inp.driftDown = Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Period);
            inp.useItem = Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Slash);
        }
        return inp;
    }

    // ───────────────────────── AI (Tanaka Control / Optimal Racing) ─────────────────────────

    KartInput AIInput(float dt)
    {
        var inp = new KartInput();
        if (track == null || track.Count == 0) return inp;

        float v = Mathf.Abs(Speed);
        float currentLateral = Lateral;
        Vector3 curPos = transform.position;
        Vector3 fwd = Forward;

        // 1. 曲率プロファイル予測と許容限界速度の計算 (MPC / Receding Horizon Prediction)
        // 速度に応じた先読みステップ数 (約14〜38ステップ = 28〜76m 先までスキャン)
        int scanSteps = Mathf.Clamp(14 + Mathf.RoundToInt(v * 0.6f), 14, 38);
        float minSafeSpeed = MaxSpeed * AISpeedFactor();
        float distAccum = 0f;
        float apexCurvature = 0f;
        float apexBend = 0f;
        int apexOffset = 0;

        for (int step = 2; step <= scanSteps; step += 2)
        {
            int i0 = track.Wrap(Index + step - 2);
            int i1 = track.Wrap(Index + step);
            int i2 = track.Wrap(Index + step + 2);

            // ウェイポイント間の角度変化から曲率 kappa を計算
            float segAngle = Vector3.SignedAngle(track.Dirs[i0], track.Dirs[i2], Vector3.up);
            float segDist = (track.Pts[i1] - track.Pts[i0]).magnitude + (track.Pts[i2] - track.Pts[i1]).magnitude;
            if (segDist < 0.1f) segDist = 4f;
            float curvature = Mathf.Abs(segAngle) * Mathf.Deg2Rad / segDist;
            distAccum += segDist * 0.5f;

            if (curvature > apexCurvature)
            {
                apexCurvature = curvature;
                apexBend = segAngle;
                apexOffset = step;
            }

            // 最大許容横加速度 a_lat = v^2 * kappa <= a_max
            // 通常時: 16.5m/s^2, ドリフト時: 22.0m/s^2
            float aLatMax = drifting ? 22f : 16.5f;
            float cornerSpeedLimit = Mathf.Sqrt(aLatMax / Mathf.Max(curvature, 0.004f));

            // 制動安全速度: v_safe = sqrt(v_limit^2 + 2 * a_brake * dist)
            float safeSpeed = Mathf.Sqrt(cornerSpeedLimit * cornerSpeedLimit + 2f * 22f * distAccum);
            if (safeSpeed < minSafeSpeed)
            {
                minSafeSpeed = safeSpeed;
            }
        }

        // 2. 動的レーシングライン生成 (Smooth Racing Line)
        // 極端な端への振りを抑え、コース中央寄りの安全なラインを滑らかにトレース
        float roadMargin = 3.0f; // 目標オフセットの最大幅（安全余裕を十分確保）
        float targetLane = 0f;

        if (apexCurvature > 0.015f && Mathf.Abs(apexBend) > 12f)
        {
            float cornerDir = Mathf.Sign(apexBend); // 1 = 右カーブ, -1 = 左カーブ
            if (apexOffset > 10)
            {
                // コーナー手前（アプローチ）：わずかにアウト側に振る
                targetLane = -cornerDir * 1.5f;
            }
            else
            {
                // エペックス付近〜通過：適度にイン側へ寄せる（極端なインベタを避ける）
                targetLane = cornerDir * 2.2f;
            }
        }
        else
        {
            // 直線：個別の走行レーンを維持
            targetLane = Mathf.Clamp(aiLane, -1.8f, 1.8f);
        }

        // 3. 非線形 Stanley 操舵制御 (Smooth Path Tracking Control)
        // 先読み距離を長めに取り、遠くを見て滑らかな円弧を描いて曲がる
        int look = Mathf.Clamp(7 + Mathf.RoundToInt(v * 0.55f), 9, 22);
        int targetIdx = track.Wrap(Index + look);
        Vector3 targetPos = track.PointAt(targetIdx, targetLane);

        // 方位角誤差 theta_e
        Vector3 toTarget = (targetPos - curPos).normalized;
        float headingError = Vector3.SignedAngle(fwd, toTarget, Vector3.up);

        // 横偏差 e_lateral (目標レーシングラインからのズレ)
        float lateralError = currentLateral - targetLane;
        // Stanley クロストラック制御則: delta = arctan(k * e / (v + v0))
        float kCross = 1.0f;
        float crossSteer = -Mathf.Atan2(kCross * lateralError, Mathf.Max(v, 4.0f)) * Mathf.Rad2Deg;

        // 先読み曲率フィードフォワード（旋回遅れを緩やかに先回り補正）
        float futureAngle = Vector3.SignedAngle(track.Dirs[Index], track.Dirs[track.Wrap(Index + 12)], Vector3.up);
        float ffSteer = Mathf.Clamp(futureAngle * 0.02f, -0.30f, 0.30f);

        // 操舵入力の合算（ゲインを落ち着かせ、過敏な急ハンドルを防止）
        float steerCmd = (headingError * 0.028f) + (crossSteer * 0.015f) + ffSteer;
        inp.steer = Mathf.Clamp(steerCmd, -1f, 1f);

        // 4. 速度・制動制御 (Speed Profiling & Longitudinal Control)
        // 注意: inp.throttle < 0 は物理的に「バック走行（後退）」を引き起こすため、
        // 前進中 (Speed > 6.0m/s) かつ 安全速度を大きく超えている時のみ一時的なブレーキとして使用する。
        float speedMargin = Speed - minSafeSpeed;
        if (speedMargin > 2.5f && Speed > 7.0f)
        {
            // コーナー手前での的確なブレーキ（前進速度が十分ある時のみ減速）
            inp.throttle = -1f;
        }
        else if (speedMargin > 0.5f && Speed > minSafeSpeed)
        {
            // 目標速度を超えている場合はコースティング（アクセルオフ）
            inp.throttle = 0f;
        }
        else
        {
            // 基本は常に前進全開加速！
            inp.throttle = 1f;
        }

        // 逆走ガード：コース順方向と逆を向いている場合は、素早く前進ステアで方向転換
        float forwardDot = Vector3.Dot(fwd, track.Dirs[Index]);
        if (forwardDot < -0.1f)
        {
            float correctAngle = Vector3.SignedAngle(fwd, track.Dirs[Index], Vector3.up);
            inp.steer = Mathf.Sign(correctAngle);
            inp.throttle = 1f; // バックではなく前進で方向転換
            inp.drift = false;
            aiSteer = inp.steer;
            return inp;
        }

        // 5. 自己駆動ドリフト制御 (Self-Triggered Smooth Drift)
        // ドリフト発動トリガー：
        // 中速カーブは通常ステアで曲がり、ヘアピン等の急カーブ（曲率 > 0.026 または旋回角 > 34度）でのみ発動
        bool canStartDrift = !drifting 
            && (apexCurvature > 0.026f || Mathf.Abs(futureAngle) > 34f) 
            && v > 15f 
            && Mathf.Sign(futureAngle) == Mathf.Sign(inp.steer) 
            && Mathf.Abs(headingError) > 8f;

        if (canStartDrift)
        {
            inp.drift = true;
            inp.driftDown = true;
        }
        else if (drifting)
        {
            // ドリフト継続トリガー：カーブが続いており、かつ外壁に突っ込まない安全領域
            bool curveRemains = Mathf.Abs(futureAngle) > 14f && Mathf.Sign(futureAngle) == driftDir;
            bool wallSafe = Mathf.Abs(currentLateral) < Track.HalfWidth - 0.5f;

            // ミニターボがチャージされ、コーナーの出口が見えたら即座に解除してロケットダッシュ！
            if (curveRemains && wallSafe && DriftLevel < 2)
            {
                inp.drift = true;
                // ドリフト中はイン固定ではなく、目標方位に合わせて微調整（カウンターステア／微インステア）
                // これによりイン側への極端な巻き込み内壁激突を完全に防止！
                float driftSteer = headingError * 0.022f;
                inp.steer = Mathf.Clamp(driftSteer, -0.45f, 0.45f);
            }
            else
            {
                // ドリフト解除！ミニターボ発動！
                inp.drift = false;
            }
        }

        // 6. コントロール・バリア・ファンクション (CBF / 滑らかな境界反発)
        float safeBoundary = Track.HalfWidth - 0.8f; // 8.2m
        float distToEdge = safeBoundary - Mathf.Abs(currentLateral);
        float outwardVelocity = Vector3.Dot(VelDir, track.Rights[Index] * Mathf.Sign(currentLateral));

        if (distToEdge < 2.0f && outwardVelocity > 0.05f)
        {
            // 距離に応じた滑らかな比例反発（急激なフルステア反発で蛇行するのを防ぐ）
            float repelIntensity = Mathf.Clamp01((2.0f - distToEdge) / 1.5f);
            float repelSteer = -Mathf.Sign(currentLateral) * repelIntensity * 0.55f;
            inp.steer = Mathf.Clamp(inp.steer + repelSteer, -1f, 1f);
            inp.drift = false; // コース端での横滑りを即座に収束
            if (distToEdge < 0.6f && outwardVelocity > 0.15f && Speed > 7.0f)
            {
                inp.throttle = -1f; // 十分な前進速度がある場合のみ減速ブレーキ
            }
        }

        // ステアリングの滑らかな補間（カクカクした急ハンドルを根絶）
        aiSteer = Mathf.MoveTowards(aiSteer, inp.steer, dt * 4.5f);
        inp.steer = aiSteer;

        // 7. スタック脱出 (Anti-Stuck Logic)
        // スタート直後（レース開始後3秒以内）は絶対にスタック判定を行わない！
        // また、前進入力中 (inp.throttle > 0) なのに壁に引っかかって車速がほぼゼロ (< 1.2m/s) の状態が
        // 2.5秒以上続いた場合のみ、一時的なバック脱出 (0.4秒間) を行う。
        if (rm != null && rm.RaceRunning && rm.RaceTime > 3.0f && Speed < 1.2f && inp.throttle > 0)
        {
            aiStuck += dt;
        }
        else
        {
            aiStuck = 0;
        }

        if (aiStuck > 2.5f)
        {
            aiReverse = 0.5f;
            aiStuck = 0;
        }

        if (aiReverse > 0)
        {
            aiReverse -= dt;
            inp.throttle = -1f; // 0.5秒だけ後退脱出
            inp.steer = -Mathf.Sign(headingError != 0 ? headingError : (Lateral >= 0 ? 1f : -1f));
            aiSteer = inp.steer;
            inp.drift = false;
        }

        // 8. 戦略的アイテム使用 (Strategic Item Usage)
        if (Item != ItemType.None && RouletteTimer <= 0)
        {
            aiItemTimer -= dt;
            bool canUse = false;
            switch (Item)
            {
                case ItemType.Missile:
                    canUse = rm.KartAheadWithin(this, 55f);
                    break;
                case ItemType.Banana:
                    canUse = rm.KartBehindWithin(this, 18f);
                    break;
                case ItemType.Turbo:
                    canUse = Mathf.Abs(futureAngle) < 15f && Mathf.Abs(currentLateral) < Track.HalfWidth - 1.5f;
                    break;
                case ItemType.Shield:
                    canUse = true;
                    break;
            }
            if (aiItemTimer <= 0 && (canUse || aiItemTimer < -3.5f))
            {
                inp.useItem = true;
                aiItemTimer = Random.Range(1.5f, 3.0f);
            }
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
            f += Mathf.Clamp(diff / 220f, -0.06f, 0.08f);
        }
        return Mathf.Clamp(f, 0.90f, 1.05f);
    }

    // ───────────────────────── 物理 ─────────────────────────

    public void Tick(float dt, bool canDrive)
    {
        if (IsRemote) { RemoteTick(dt); return; }
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

    void Effects(float dt)
    {
        float rz = kenneyReady ? -0.52f : -1.0f;
        float rx = kenneyReady ? 0.78f : 0.9f;
        var rearL = transform.TransformPoint(new Vector3(-rx, 0.15f, rz));
        var rearR = transform.TransformPoint(new Vector3(rx, 0.15f, rz));
        var back = -Forward;

        if (drifting)
        {
            int lv = DriftLevel;
            Color c = lv == 3 ? new Color(1f, 0.3f, 1f) : lv == 2 ? new Color(1f, 0.55f, 0.1f) : lv == 1 ? new Color(0.3f, 0.65f, 1f) : new Color(0.8f, 0.8f, 0.8f, 0.5f);
            float size = lv == 0 ? 0.5f : 0.35f + lv * 0.08f;
            var vel = back * 3f + Vector3.up * (lv > 0 ? 3f : 1f);
            Fx.Emit(rearL, vel, c, size, lv == 0 ? 0.4f : 0.25f, 1, lv > 0 ? 2.5f : 0.8f);
            Fx.Emit(rearR, vel, c, size, lv == 0 ? 0.4f : 0.25f, 1, lv > 0 ? 2.5f : 0.8f);

            // タイヤスモーク（モクモク広がる白煙）
            var smokeCol = new Color(0.88f, 0.88f, 0.92f, lv > 0 ? 0.4f : 0.22f);
            Fx.Smoke(rearL, back * 1.5f + Vector3.up * 1f, smokeCol, 0.6f + lv * 0.15f, 0.55f, 1, 0.8f);
            Fx.Smoke(rearR, back * 1.5f + Vector3.up * 1f, smokeCol, 0.6f + lv * 0.15f, 0.55f, 1, 0.8f);

            // ドリフト火花スパーク（マリオカート風にタイヤから弾け飛ぶ粒子）
            if (lv > 0)
            {
                var sparkCol = lv == 3 ? new Color(1f, 0.4f, 1f) : lv == 2 ? new Color(1f, 0.7f, 0.1f) : new Color(0.4f, 0.8f, 1f);
                Vector3 sparkVelL = -transform.right * Random.Range(2.5f, 5.5f) + Vector3.up * Random.Range(1.5f, 4.5f) + back * Random.Range(0.5f, 2f);
                Vector3 sparkVelR = transform.right * Random.Range(2.5f, 5.5f) + Vector3.up * Random.Range(1.5f, 4.5f) + back * Random.Range(0.5f, 2f);
                Fx.Emit(rearL, sparkVelL, sparkCol, 0.22f, 0.18f, 2, 2.5f);
                Fx.Emit(rearR, sparkVelR, sparkCol, 0.22f, 0.18f, 2, 2.5f);
            }
        }

        // スピン中のピヨピヨ星（頭上を回転するスター）
        if (spinTimer > 0)
        {
            float ang = Time.time * 12f;
            Vector3 starPos1 = transform.position + Vector3.up * 1.6f + new Vector3(Mathf.Cos(ang), 0.1f * Mathf.Sin(ang * 2f), Mathf.Sin(ang)) * 0.75f;
            Vector3 starPos2 = transform.position + Vector3.up * 1.6f + new Vector3(Mathf.Cos(ang + Mathf.PI), -0.1f * Mathf.Sin(ang * 2f), Mathf.Sin(ang + Mathf.PI)) * 0.75f;
            Fx.Emit(starPos1, Vector3.up * 0.3f, new Color(1f, 0.95f, 0.2f), 0.26f, 0.18f, 1, 1.5f);
            Fx.Emit(starPos2, Vector3.up * 0.3f, new Color(1f, 0.85f, 0.2f), 0.26f, 0.18f, 1, 1.5f);
        }

        // 急ブレーキ白煙
        if (isBraking && Speed > 6f && Random.value < 0.7f)
        {
            var brakeSmoke = new Color(0.9f, 0.9f, 0.92f, 0.3f);
            Fx.Smoke(rearL, back * 2f + Vector3.up * 0.8f, brakeSmoke, 0.55f, 0.45f, 1, 0.6f);
            Fx.Smoke(rearR, back * 2f + Vector3.up * 0.8f, brakeSmoke, 0.55f, 0.45f, 1, 0.6f);
        }

        if (boostTimer > 0)
        {
            float exZ = kenneyReady ? -0.72f : -1.35f;
            float exY = kenneyReady ? 0.46f : 0.28f;
            float exX = kenneyReady ? 0.28f : 0.35f;
            var exL = transform.TransformPoint(new Vector3(-exX, exY, exZ));
            var exR = transform.TransformPoint(new Vector3(exX, exY, exZ));
            // オレンジのアフターバーナー炎
            Fx.Emit(exL, back * 9f + VelDir * Speed * 0.8f, new Color(1f, 0.6f, 0.15f), 0.75f, 0.18f, 2, 1.2f);
            Fx.Emit(exR, back * 9f + VelDir * Speed * 0.8f, new Color(1f, 0.6f, 0.15f), 0.75f, 0.18f, 2, 1.2f);
            // コアの青白熱炎
            Fx.Emit(exL, back * 7f + VelDir * Speed * 0.8f, new Color(0.35f, 0.75f, 1f), 0.45f, 0.12f, 1, 0.4f);
            Fx.Emit(exR, back * 7f + VelDir * Speed * 0.8f, new Color(0.35f, 0.75f, 1f), 0.45f, 0.12f, 1, 0.4f);
        }
        if (Offroad && Speed > 6f && Random.value < 0.6f)
            Fx.Smoke((rearL + rearR) * 0.5f, back * 2.5f + Vector3.up * 1.5f, new Color(0.65f, 0.55f, 0.35f, 0.45f), 0.8f, 0.6f, 1, 1.2f);
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
        // スタートダッシュ：「1」が出てから GO までにアクセルを押し始めると成功
        if (IsPlayer)
        {
            float held = throttleHeldSince < 0 ? -1f : Time.time - throttleHeldSince;
            if (held >= 0 && held < 1.1f) { Boost(1.2f); rm.Audio.Boost(); rm.Banner("ROCKET START!", new Color(1f, 0.8f, 0.2f), PlayerIndex); }
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
