using UnityEngine;

// カートの見た目：モデル生成、姿勢・ホイール・ライトの更新、走行エフェクト
public partial class Kart
{
    // ───────────────────────── 見た目 ─────────────────────────

    static readonly string[] KenneyKarts = { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" };
    static readonly string[] AnimalDrivers = { "panda", "penguin", "fox", "bunny", "lion" };

    public int DriftDirection => driftDir;
    public float SteerVisual => steerVis;

    Transform[] boostFlames = new Transform[2];
    Renderer[] boostFlameRends = new Renderer[2];
    MaterialPropertyBlock flameMpb;
    Transform blobShadow;
    float visPitch;
    static Material sharedBlobShadowMat;
    static Material sharedAnimalDriverMat;

    Material GetAnimalDriverMaterial()
    {
        if (sharedAnimalDriverMat == null && rm != null && rm.wallMaterial != null)
        {
            sharedAnimalDriverMat = new Material(rm.wallMaterial);
            string animalTexPath = Application.dataPath + "/Resources/Animals/Textures/colormap.png";
            var colormapTex = RaceManager.LoadTexture("Animals/Textures/colormap", animalTexPath);
            if (colormapTex != null)
            {
                sharedAnimalDriverMat.mainTexture = colormapTex;
                sharedAnimalDriverMat.SetTexture("_BaseMap", colormapTex);
                sharedAnimalDriverMat.SetTexture("_MainTex", colormapTex);
            }
            sharedAnimalDriverMat.color = Color.white;
            sharedAnimalDriverMat.SetColor("_BaseColor", Color.white);
            sharedAnimalDriverMat.SetColor("_Color", Color.white);
            sharedAnimalDriverMat.SetFloat("_Smoothness", 0.18f);
        }
        return sharedAnimalDriverMat;
    }

    void BuildModel()
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        var paint = new MaterialPropertyBlock();
        paint.SetColor("_Color", Color);
        paint.SetColor("_BaseColor", Color);
        var dark = new MaterialPropertyBlock();
        dark.SetColor("_Color", Color * 0.55f);
        dark.SetColor("_BaseColor", Color * 0.55f);

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

        // 車体直下の接地ブロブシャドウ（路面の密着感と立体感を大幅に強化）
        if (sharedBlobShadowMat == null && rm.skidmarkMaterial != null)
        {
            sharedBlobShadowMat = new Material(rm.skidmarkMaterial);
            var blobTex = TextureGen.BlobShadow();
            sharedBlobShadowMat.mainTexture = blobTex;
            sharedBlobShadowMat.SetTexture("_BaseMap", blobTex);
            sharedBlobShadowMat.SetTexture("_MainTex", blobTex);
            sharedBlobShadowMat.color = Color.white;
            sharedBlobShadowMat.SetColor("_BaseColor", Color.white);
            sharedBlobShadowMat.SetColor("_Color", Color.white);
        }
        if (sharedBlobShadowMat != null)
        {
            var shadowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(shadowGo.GetComponent<Collider>());
            shadowGo.name = "BlobShadow";
            shadowGo.transform.SetParent(transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, 0.035f, -0.05f);
            shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowGo.transform.localScale = new Vector3(2.15f, 2.55f, 1f);
            var sr = shadowGo.GetComponent<Renderer>();
            sr.sharedMaterial = sharedBlobShadowMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            blobShadow = shadowGo.transform;
        }

        // ヘッドライト（左右フロント）
        var hlMat = rm.headlightMaterial;
        float hlZ = kenneyReady ? 1.02f : 1.46f;
        float hlY = kenneyReady ? 0.30f : 0.44f;
        float hlX = kenneyReady ? 0.36f : 0.46f;
        Part(PrimitiveType.Cube, new Vector3(-hlX, hlY, hlZ), new Vector3(0.16f, 0.07f, 0.05f), hlMat);
        Part(PrimitiveType.Cube, new Vector3(hlX, hlY, hlZ), new Vector3(0.16f, 0.07f, 0.05f), hlMat);
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

        // テールランプ（スリムなLEDバー・ブレーキ連動）
        tailMpb = new MaterialPropertyBlock();
        var tlMat = rm.taillightMaterial;
        float tlZ = kenneyReady ? -0.66f : -1.22f;
        float tlY = kenneyReady ? 0.34f : 0.50f;
        float tlX = kenneyReady ? 0.32f : 0.44f;
        var tl1 = Part(PrimitiveType.Cube, new Vector3(-tlX, tlY, tlZ), new Vector3(0.20f, 0.06f, 0.04f), tlMat);
        var tl2 = Part(PrimitiveType.Cube, new Vector3(tlX, tlY, tlZ), new Vector3(0.20f, 0.06f, 0.04f), tlMat);
        tailLightRend[0] = tl1.GetComponent<Renderer>();
        tailLightRend[1] = tl2.GetComponent<Renderer>();

        // 3D ブースト・アフターバーナージェット炎コーン（左右マフラー先端）
        flameMpb = new MaterialPropertyBlock();
        float flZ = kenneyReady ? -0.84f : -1.42f;
        float flY = kenneyReady ? 0.44f : 0.28f;
        float flX = kenneyReady ? 0.27f : 0.32f;
        for (int i = 0; i < 2; i++)
        {
            var flGo = Part(PrimitiveType.Capsule, new Vector3(i == 0 ? -flX : flX, flY, flZ), new Vector3(0.18f, 0.38f, 0.18f), rm.glowMaterial, null, Quaternion.Euler(90f, 0f, 0f));
            flGo.name = "BoostFlame_" + i;
            var flRend = flGo.GetComponent<Renderer>();
            flRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flRend.receiveShadows = false;
            boostFlames[i] = flGo.transform;
            boostFlameRends[i] = flRend;
            flGo.SetActive(false);
        }

        if (!kenneyReady)
        {
            // プロシージャルボディ時のみ排気管・ドライバー・ステアリングを生成
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
            tr.sharedMaterial = rm.skidmarkMaterial;
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
        int slotFallback = (rm != null && rm.Karts != null) ? Mathf.Max(0, rm.Karts.IndexOf(this)) : 0;
        int driverIdx = ModelVariant >= 0 ? ModelVariant : IsPlayer ? (rm != null ? rm.SelectedDriver : 0) : ((slotFallback + 1) % 10);
        int kartIdx = KartVariant >= 0 ? KartVariant : IsPlayer ? (rm != null ? rm.SelectedKart : 0) : ((slotFallback + 2) % 8);

        var baseKartPrefab = Resources.Load<GameObject>("Karts/kart-oobi");
        if (baseKartPrefab == null) return false;

        float liftY = (kartIdx == 3) ? 0.10f : 0f; // MONSTER のみハイリフト車高

        // 1. カート車体ベース（付属の character と静的 wheel-* を除去し、純粋なシャシーのみを使用）
        var body = Instantiate(baseKartPrefab, model);
        body.name = "KartChassis";
        body.transform.localPosition = new Vector3(0, 0.15f + liftY, 0);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * 1.5f;
        foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);

        for (int i = body.transform.childCount - 1; i >= 0; i--)
        {
            var ch = body.transform.GetChild(i);
            if (ch.name.StartsWith("wheel") || ch.name.StartsWith("character"))
            {
                ch.SetParent(null);
                ch.gameObject.SetActive(false);
                Destroy(ch.gameObject);
            }
        }

        var bodyPaint = new MaterialPropertyBlock();
        Color bodyTint = Color.Lerp(Color.white, Color, 0.72f);
        bodyPaint.SetColor("_Color", bodyTint);
        bodyPaint.SetColor("_BaseColor", bodyTint);
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            r.SetPropertyBlock(bodyPaint);
        }

        // 2. カート種類別（0〜7）のカスタム3Dパーツ造形＆ホイール設定
        BuildCustomKartParts(kartIdx, liftY, out Vector3[] wheelPos, out bool useRacingWheel, out float frontWheelScale, out float rearWheelScale);

        var wheelPrefab = useRacingWheel
            ? (Resources.Load<GameObject>("Karts/wheel-racing") ?? Resources.Load<GameObject>("Karts/wheel-default"))
            : (Resources.Load<GameObject>("Karts/wheel-default") ?? Resources.Load<GameObject>("Karts/wheel-racing"));

        for (int i = 0; i < 4; i++)
        {
            var pivot = new GameObject("WheelPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = wheelPos[i];
            if (i < 2) frontPivot[i] = pivot;
            var spin = new GameObject("WheelSpin").transform;
            spin.SetParent(pivot, false);
            wheelSpin[i] = spin;

            float wScale = i < 2 ? frontWheelScale : rearWheelScale;
            if (wheelPrefab != null)
            {
                var w = Instantiate(wheelPrefab, spin);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = i % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                w.transform.localScale = Vector3.one * wScale;
                foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                float d = i < 2 ? 0.7f : 0.8f, width = i < 2 ? 0.18f : 0.26f;
                var tire = Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(d, width, d), rm.tireMaterial, null, Quaternion.Euler(0, 0, 90));
                tire.transform.SetParent(spin, false);
            }

            // CYBER GT (4) はホイール側面に発光ネオンリングを追加
            if (kartIdx == 4 && rm != null && rm.glowMaterial != null)
            {
                var rimMpb = new MaterialPropertyBlock();
                Color neonCol = new Color(0.15f, 0.95f, 1f, 0.9f);
                rimMpb.SetColor("_Color", neonCol);
                rimMpb.SetColor("_BaseColor", neonCol);
                rimMpb.SetColor("_TintColor", neonCol);
                float sideX = (i % 2 == 0) ? -0.18f : 0.18f;
                var rim = Part(PrimitiveType.Cylinder, new Vector3(sideX, 0f, 0f), new Vector3(0.36f * wScale, 0.015f, 0.36f * wScale), rm.glowMaterial, rimMpb, Quaternion.Euler(0, 0, 90));
                rim.transform.SetParent(spin, false);
            }
            // MONSTER (3) や ROADSTER (5) はクロームセンターハブキャップを追加
            else if ((kartIdx == 3 || kartIdx == 5) && rm != null && rm.chromeMaterial != null)
            {
                float sideX = (i % 2 == 0) ? -0.19f * (wScale / 1.35f) : 0.19f * (wScale / 1.35f);
                var hub = Part(PrimitiveType.Cylinder, new Vector3(sideX, 0f, 0f), new Vector3(0.24f * wScale, 0.025f, 0.24f * wScale), rm.chromeMaterial, null, Quaternion.Euler(0, 0, 90));
                hub.transform.SetParent(spin, false);
            }
        }

        // 3. ドライバー（キャラクター 0〜9）を独立してシートに搭乗させる
        BuildCustomDriver(driverIdx, liftY);

        return true;
    }

    void BuildCustomKartParts(int kartIdx, float liftY, out Vector3[] wheelPos, out bool useRacingWheel, out float frontScale, out float rearScale)
    {
        var paint = new MaterialPropertyBlock();
        paint.SetColor("_Color", Color);
        paint.SetColor("_BaseColor", Color);

        var dark = new MaterialPropertyBlock();
        dark.SetColor("_Color", Color * 0.45f);
        dark.SetColor("_BaseColor", Color * 0.45f);

        Color dCol = DriverColor.maxColorComponent > 0.05f ? DriverColor : Color.white;
        var accent = new MaterialPropertyBlock();
        accent.SetColor("_Color", dCol);
        accent.SetColor("_BaseColor", dCol);

        wheelPos = new[]
        {
            new Vector3(-0.74f, 0.30f, 0.56f),  // 前左
            new Vector3( 0.74f, 0.30f, 0.56f),  // 前右
            new Vector3(-0.76f, 0.32f, -0.52f), // 後左
            new Vector3( 0.76f, 0.32f, -0.52f)  // 後右
        };
        useRacingWheel = true;
        frontScale = 1.35f;
        rearScale = 1.38f;

        switch (kartIdx)
        {
            case 0: // STANDARD (STD-01): クラシック・レーシングカート（フロントゼッケンプレート＋ツインクロームマフラー）
                Part(PrimitiveType.Cube, new Vector3(0f, 0.44f, 0.92f), new Vector3(0.52f, 0.14f, 0.24f), rm.kartPaintMaterial, accent, Quaternion.Euler(22f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.26f, 0.42f, -0.72f), new Vector3(0.13f, 0.18f, 0.13f), rm.chromeMaterial, null, Quaternion.Euler(78f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.26f, 0.42f, -0.72f), new Vector3(0.13f, 0.18f, 0.13f), rm.chromeMaterial, null, Quaternion.Euler(78f, 0f, 0f));
                break;

            case 1: // PIPE BUGGY (BUG-02): パイプフレーム・ロールケージバギー＋ブルバー＋サスペンション
                useRacingWheel = false;
                frontScale = 1.32f;
                rearScale = 1.36f;
                // フロントブルバー（バンパーガード）
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.36f, 1.06f), new Vector3(0.08f, 0.52f, 0.08f), rm.chromeMaterial, null, Quaternion.Euler(0f, 0f, 90f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.32f, 0.42f, 1.00f), new Vector3(0.07f, 0.14f, 0.07f), rm.chromeMaterial, null, Quaternion.Euler(-25f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.32f, 0.42f, 1.00f), new Vector3(0.07f, 0.14f, 0.07f), rm.chromeMaterial, null, Quaternion.Euler(-25f, 0f, 0f));
                // サイドロールバー＆ルーフパイプフレーム
                Part(PrimitiveType.Cylinder, new Vector3(-0.46f, 0.62f, 0.28f), new Vector3(0.07f, 0.28f, 0.07f), rm.kartPaintMaterial, paint, Quaternion.Euler(-28f, 0f, -10f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.46f, 0.62f, 0.28f), new Vector3(0.07f, 0.28f, 0.07f), rm.kartPaintMaterial, paint, Quaternion.Euler(-28f, 0f,  10f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.46f, 0.66f, -0.52f), new Vector3(0.075f, 0.32f, 0.075f), rm.kartPaintMaterial, paint, Quaternion.Euler(16f, 0f, -8f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.46f, 0.66f, -0.52f), new Vector3(0.075f, 0.32f, 0.075f), rm.kartPaintMaterial, paint, Quaternion.Euler(16f, 0f,  8f));
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.92f, -0.44f), new Vector3(0.08f, 0.44f, 0.08f), rm.kartPaintMaterial, accent, Quaternion.Euler(0f, 0f, 90f));
                // リアコイルスプリング
                Part(PrimitiveType.Cylinder, new Vector3(-0.48f, 0.46f, -0.56f), new Vector3(0.12f, 0.16f, 0.12f), rm.bananaMaterial);
                Part(PrimitiveType.Cylinder, new Vector3( 0.48f, 0.46f, -0.56f), new Vector3(0.12f, 0.16f, 0.12f), rm.bananaMaterial);
                break;

            case 2: // MACH FORMULA (GP-03): F1フォーミュラ・フロントウイング＋サイドポッド＋大型リアGTウイング
                frontScale = 1.28f;
                rearScale = 1.50f;
                wheelPos[2] = new Vector3(-0.82f, 0.34f, -0.54f);
                wheelPos[3] = new Vector3( 0.82f, 0.34f, -0.54f);
                // ロングノーズ＆ワイドフロントウイング
                Part(PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.98f), new Vector3(0.46f, 0.16f, 0.48f), rm.kartPaintMaterial, paint, Quaternion.Euler(10f, 0f, 0f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.22f, 1.16f), new Vector3(1.56f, 0.06f, 0.28f), rm.kartPaintMaterial, accent);
                Part(PrimitiveType.Cube, new Vector3(-0.78f, 0.28f, 1.14f), new Vector3(0.06f, 0.18f, 0.34f), rm.kartPaintMaterial, paint);
                Part(PrimitiveType.Cube, new Vector3( 0.78f, 0.28f, 1.14f), new Vector3(0.06f, 0.18f, 0.34f), rm.kartPaintMaterial, paint);
                // サイドエアロポッド
                Part(PrimitiveType.Cube, new Vector3(-0.68f, 0.34f, -0.02f), new Vector3(0.28f, 0.22f, 0.76f), rm.kartPaintMaterial, paint);
                Part(PrimitiveType.Cube, new Vector3( 0.68f, 0.34f, -0.02f), new Vector3(0.28f, 0.22f, 0.76f), rm.kartPaintMaterial, paint);
                // 大型リアGTウイング＋翼端板
                Part(PrimitiveType.Cube, new Vector3(-0.36f, 0.68f, -0.78f), new Vector3(0.06f, 0.42f, 0.14f), rm.chromeMaterial, null, Quaternion.Euler(-12f, 0f, 0f));
                Part(PrimitiveType.Cube, new Vector3( 0.36f, 0.68f, -0.78f), new Vector3(0.06f, 0.42f, 0.14f), rm.chromeMaterial, null, Quaternion.Euler(-12f, 0f, 0f));
                Part(PrimitiveType.Cube, new Vector3(0f, 0.90f, -0.84f), new Vector3(1.62f, 0.07f, 0.34f), rm.kartPaintMaterial, paint, Quaternion.Euler(8f, 0f, 0f));
                Part(PrimitiveType.Cube, new Vector3(-0.82f, 0.90f, -0.84f), new Vector3(0.06f, 0.24f, 0.40f), rm.kartPaintMaterial, accent);
                Part(PrimitiveType.Cube, new Vector3( 0.82f, 0.90f, -0.84f), new Vector3(0.06f, 0.24f, 0.40f), rm.kartPaintMaterial, accent);
                break;

            case 3: // MONSTER (MON-04): 超巨大タイヤ・ハイリフトモンスタートラック＋煙突マフラー
                useRacingWheel = false;
                frontScale = 1.74f;
                rearScale = 1.80f;
                wheelPos = new[]
                {
                    new Vector3(-0.90f, 0.38f, 0.58f),
                    new Vector3( 0.90f, 0.38f, 0.58f),
                    new Vector3(-0.92f, 0.40f, -0.54f),
                    new Vector3( 0.92f, 0.40f, -0.54f)
                };
                // ヘビーデューティ・サブフレーム＆フロントバッシュガード
                Part(PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(1.10f, 0.16f, 1.35f), rm.tireMaterial);
                Part(PrimitiveType.Cube, new Vector3(0f, 0.38f, 1.06f), new Vector3(1.18f, 0.22f, 0.14f), rm.chromeMaterial);
                // 左右直立クローム煙突マフラー
                Part(PrimitiveType.Cylinder, new Vector3(-0.44f, 0.78f, -0.62f), new Vector3(0.14f, 0.38f, 0.14f), rm.chromeMaterial);
                Part(PrimitiveType.Cylinder, new Vector3( 0.44f, 0.78f, -0.62f), new Vector3(0.14f, 0.38f, 0.14f), rm.chromeMaterial);
                break;

            case 4: // CYBER GT (CYB-05): サイバーネオン・インターセプター（発光ネオンブレード＋双尾翼）
                frontScale = 1.35f;
                rearScale = 1.44f;
                var neonMpb = new MaterialPropertyBlock();
                Color neon = new Color(0.10f, 0.95f, 1.0f, 0.95f);
                neonMpb.SetColor("_Color", neon);
                neonMpb.SetColor("_BaseColor", neon);
                neonMpb.SetColor("_TintColor", neon);
                // フロントサイバースプリッター＆ネオンバー
                Part(PrimitiveType.Cube, new Vector3(0f, 0.24f, 1.08f), new Vector3(1.38f, 0.07f, 0.26f), rm.kartPaintMaterial, dark);
                Part(PrimitiveType.Cube, new Vector3(0f, 0.25f, 1.18f), new Vector3(1.32f, 0.045f, 0.06f), rm.glowMaterial, neonMpb);
                // 左右サイドネオンスカート
                Part(PrimitiveType.Cube, new Vector3(-0.72f, 0.24f, 0.02f), new Vector3(0.08f, 0.05f, 1.15f), rm.glowMaterial, neonMpb);
                Part(PrimitiveType.Cube, new Vector3( 0.72f, 0.24f, 0.02f), new Vector3(0.08f, 0.05f, 1.15f), rm.glowMaterial, neonMpb);
                // 双尾翼スタビライザーフィン
                Part(PrimitiveType.Cube, new Vector3(-0.48f, 0.72f, -0.72f), new Vector3(0.07f, 0.38f, 0.46f), rm.kartPaintMaterial, paint, Quaternion.Euler(14f, 0f,  18f));
                Part(PrimitiveType.Cube, new Vector3( 0.48f, 0.72f, -0.72f), new Vector3(0.07f, 0.38f, 0.46f), rm.kartPaintMaterial, paint, Quaternion.Euler(14f, 0f, -18f));
                break;

            case 5: // ROADSTER (RET-06): クラシック・ヴィンテージクーペ（葉巻型ロングノーズ＋風防バイザー＋サイドマフラー）
                useRacingWheel = false;
                frontScale = 1.36f;
                rearScale = 1.38f;
                // ヴィンテージ・ラウンドノーズ＆クロームグリル
                Part(PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0.76f), new Vector3(0.68f, 0.42f, 0.62f), rm.kartPaintMaterial, paint, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.40f, 1.12f), new Vector3(0.46f, 0.04f, 0.44f), rm.chromeMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                // レトロ風防ウインドシールド
                Part(PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.32f), new Vector3(0.56f, 0.16f, 0.05f), rm.visorMaterial, null, Quaternion.Euler(-28f, 0f, 0f));
                // ロングクローム・サイドエキゾーストパイプ
                Part(PrimitiveType.Cylinder, new Vector3(-0.66f, 0.34f, -0.05f), new Vector3(0.11f, 0.56f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.66f, 0.34f, -0.05f), new Vector3(0.11f, 0.56f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                break;

            case 6: // MUSCLE V8 (MUS-07): スーパーチャージャー搭載マッスルカー＋4本出しマフラー＋ダックテール
                frontScale = 1.28f;
                rearScale = 1.54f;
                wheelPos[2] = new Vector3(-0.82f, 0.35f, -0.54f);
                wheelPos[3] = new Vector3( 0.82f, 0.35f, -0.54f);
                // フロントV8スーパーチャージャー・ブロワースクープ
                Part(PrimitiveType.Cube, new Vector3(0f, 0.52f, 0.56f), new Vector3(0.38f, 0.18f, 0.38f), rm.chromeMaterial);
                Part(PrimitiveType.Cube, new Vector3(0f, 0.64f, 0.58f), new Vector3(0.44f, 0.12f, 0.32f), rm.chromeMaterial);
                Part(PrimitiveType.Cylinder, new Vector3(-0.10f, 0.65f, 0.74f), new Vector3(0.10f, 0.04f, 0.10f), rm.taillightMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.10f, 0.65f, 0.74f), new Vector3(0.10f, 0.04f, 0.10f), rm.taillightMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                // リアダックテールスポイラー＆4本出しマフラー
                Part(PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.74f), new Vector3(1.18f, 0.16f, 0.18f), rm.kartPaintMaterial, paint, Quaternion.Euler(-24f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.34f, 0.34f, -0.78f), new Vector3(0.11f, 0.16f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(85f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.20f, 0.34f, -0.78f), new Vector3(0.11f, 0.16f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(85f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.20f, 0.34f, -0.78f), new Vector3(0.11f, 0.16f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(85f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.34f, 0.34f, -0.78f), new Vector3(0.11f, 0.16f, 0.11f), rm.chromeMaterial, null, Quaternion.Euler(85f, 0f, 0f));
                break;

            case 7: // JET POD (JET-08): ツインタービン・ジェットロケットカート＋デルタウイング
                frontScale = 1.26f;
                rearScale = 1.36f;
                var jetGlow = new MaterialPropertyBlock();
                Color jCol = new Color(0.25f, 0.85f, 1.0f, 0.95f);
                jetGlow.SetColor("_Color", jCol);
                jetGlow.SetColor("_BaseColor", jCol);
                jetGlow.SetColor("_TintColor", jCol);
                // 左右大型ジェットタービンポッド
                Part(PrimitiveType.Cylinder, new Vector3(-0.64f, 0.50f, -0.26f), new Vector3(0.32f, 0.44f, 0.32f), rm.kartPaintMaterial, paint, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.64f, 0.50f, -0.26f), new Vector3(0.32f, 0.44f, 0.32f), rm.kartPaintMaterial, paint, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.64f, 0.50f,  0.18f), new Vector3(0.34f, 0.05f, 0.34f), rm.chromeMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, new Vector3( 0.64f, 0.50f,  0.18f), new Vector3(0.34f, 0.05f, 0.34f), rm.chromeMaterial, null, Quaternion.Euler(90f, 0f, 0f));
                Part(PrimitiveType.Sphere, new Vector3(-0.64f, 0.50f, -0.70f), new Vector3(0.26f, 0.26f, 0.18f), rm.glowMaterial, jetGlow);
                Part(PrimitiveType.Sphere, new Vector3( 0.64f, 0.50f, -0.70f), new Vector3(0.26f, 0.26f, 0.18f), rm.glowMaterial, jetGlow);
                // デルタカナード翼
                Part(PrimitiveType.Cube, new Vector3(-0.88f, 0.46f, -0.34f), new Vector3(0.36f, 0.05f, 0.42f), rm.kartPaintMaterial, accent, Quaternion.Euler(0f, -18f, -8f));
                Part(PrimitiveType.Cube, new Vector3( 0.88f, 0.46f, -0.34f), new Vector3(0.36f, 0.05f, 0.42f), rm.kartPaintMaterial, accent, Quaternion.Euler(0f,  18f,  8f));
                break;
        }
    }

    void BuildCustomDriver(int driverIdx, float liftY)
    {
        driverBody = new GameObject("DriverBody").transform;
        driverBody.SetParent(model, false);
        driverBody.localPosition = new Vector3(0f, 0.565f + liftY, -0.102f);

        driverHead = new GameObject("DriverHead").transform;
        driverHead.SetParent(driverBody, false);
        driverHead.localPosition = Vector3.zero;

        Color cCol = DriverColor.maxColorComponent > 0.05f ? DriverColor : Color;

        if (driverIdx >= 0 && driverIdx < KenneyKarts.Length)
        {
            // 0〜4: Kenney キャラクター (OOBI, OODI, OOLI, OOPI, OOZI)
            var charKartPrefab = Resources.Load<GameObject>("Karts/" + KenneyKarts[driverIdx]);
            if (charKartPrefab != null)
            {
                var tempGo = Instantiate(charKartPrefab);
                var charTr = tempGo.transform.Find("character");
                if (charTr != null)
                {
                    charTr.SetParent(driverHead, false);
                    charTr.localPosition = Vector3.zero;
                    charTr.localRotation = Quaternion.identity;
                    charTr.localScale = Vector3.one * 1.5f;
                    foreach (var col in charTr.GetComponentsInChildren<Collider>()) Destroy(col);

                    var charMpb = new MaterialPropertyBlock();
                    Color tint = Color.Lerp(Color.white, cCol, 0.62f);
                    charMpb.SetColor("_Color", tint);
                    charMpb.SetColor("_BaseColor", tint);
                    foreach (var r in charTr.GetComponentsInChildren<Renderer>())
                    {
                        r.SetPropertyBlock(charMpb);
                    }
                }
                tempGo.SetActive(false);
                Destroy(tempGo);
            }
        }
        else
        {
            // 5〜9: 動物レーサー (PANDA, PENGUIN, FOX, BUNNY, LION)
            int animIdx = Mathf.Clamp(driverIdx - KenneyKarts.Length, 0, AnimalDrivers.Length - 1);
            string animName = AnimalDrivers[animIdx];
            var animPrefab = Resources.Load<GameObject>("Animals/animal-" + animName);
            if (animPrefab != null)
            {
                var animGo = Instantiate(animPrefab, driverHead);
                float aScale = animName == "bunny" ? 0.66f : animName == "lion" ? 0.70f : 0.72f;
                animGo.transform.localPosition = new Vector3(0f, -0.10f, 0.02f);
                animGo.transform.localRotation = Quaternion.identity;
                animGo.transform.localScale = Vector3.one * aScale;
                foreach (var col in animGo.GetComponentsInChildren<Collider>()) Destroy(col);

                // シート下に突き出る脚パーツを除去して座り姿勢にする
                for (int i = animGo.transform.childCount - 1; i >= 0; i--)
                {
                    var ch = animGo.transform.GetChild(i);
                    if (ch.name.StartsWith("leg-"))
                    {
                        ch.SetParent(null);
                        ch.gameObject.SetActive(false);
                        Destroy(ch.gameObject);
                    }
                }

                var animMat = GetAnimalDriverMaterial();
                if (animMat != null)
                {
                    foreach (var r in animGo.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = animMat;
                    }
                }
            }
        }
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
        float targetPitch = isAirborne
            ? Mathf.Clamp(-verticalVel * 1.6f, -20f, 20f)
            : ((boostTimer > 0 || slipstreamTimer > 0) ? -3.4f : (isBraking && Speed > 4f ? 2.4f : 0f));
        visPitch = Mathf.Lerp(visPitch, targetPitch, 1f - Mathf.Exp(-10f * dt));
        float bob = Offroad && Speed > 5f ? Mathf.Sin(Time.time * 40f) * 0.04f : 0f;
        model.localPosition = new Vector3(0, hop + bob, 0);

        if (!isAirborne)
        {
            airTrickSpin = Mathf.MoveTowards(Mathf.Repeat(airTrickSpin + 180f, 360f) - 180f, 0f, 900f * dt);
        }
        model.localRotation = Quaternion.Euler(visPitch, visYaw + spinAngle + airTrickSpin, roll);

        if (blobShadow != null)
        {
            float shadowScale = isAirborne ? 0.65f : (1f - hop * 0.3f);
            blobShadow.localScale = new Vector3(2.15f * shadowScale, 2.55f * shadowScale, 1f);
            blobShadow.localRotation = Quaternion.Euler(90f, visYaw + spinAngle + airTrickSpin, 0f);
        }

        wheelAngle += Speed * dt / 0.37f * Mathf.Rad2Deg;
        for (int i = 0; i < 4; i++) wheelSpin[i].localRotation = Quaternion.Euler(wheelAngle, 0, 0);
        for (int i = 0; i < 2; i++) frontPivot[i].localRotation = Quaternion.Euler(0, steerVis * 28f, 0);

        // スキッドマークの発生位置もドリフトでヨー回転した実リアタイヤの接地面に追従させる
        if (skidTrails[0] != null && skidTrails[1] != null && wheelSpin[2] != null && wheelSpin[3] != null)
        {
            Vector3 groundOffset = -transform.up * 0.28f;
            skidTrails[0].transform.position = wheelSpin[2].position + groundOffset;
            skidTrails[1].transform.position = wheelSpin[3].position + groundOffset;
        }

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

        // 3D ブースト・アフターバーナージェット炎コーン（ブースト・スリップストリーム時に激しく脈動発光）
        bool showFlame = boostTimer > 0 || slipstreamTimer > 0;
        for (int i = 0; i < 2; i++)
        {
            if (boostFlames[i] == null) continue;
            if (boostFlames[i].gameObject.activeSelf != showFlame)
                boostFlames[i].gameObject.SetActive(showFlame);
            if (showFlame && flameMpb != null)
            {
                float pulse = 0.88f + Mathf.Sin(Time.time * 52f + i * 1.5f) * 0.25f;
                float lenY = (boostTimer > 0 ? 0.42f : 0.30f) * pulse;
                float radXZ = (boostTimer > 0 ? 0.17f : 0.13f) * (0.9f + pulse * 0.15f);
                boostFlames[i].localScale = new Vector3(radXZ, lenY, radXZ);
                float pp = Mathf.PingPong(Time.time * 20f, 1f);
                Color fCol;
                if (slipstreamTimer > 0 || boostColorLevel == 4)
                {
                    fCol = Color.Lerp(new Color(0.20f, 0.88f, 1.0f, 0.92f), new Color(0.75f, 0.98f, 1.0f, 0.96f), pp);
                }
                else if (boostTimer > 0)
                {
                    if (boostColorLevel == 1)
                        fCol = Color.Lerp(new Color(0.05f, 0.55f, 1.0f, 0.90f), new Color(0.25f, 0.88f, 1.0f, 0.94f), pp);
                    else if (boostColorLevel == 3)
                        fCol = Color.Lerp(new Color(0.95f, 0.12f, 0.85f, 0.90f), new Color(0.55f, 0.22f, 1.0f, 0.94f), pp);
                    else
                        fCol = Color.Lerp(new Color(1.0f, 0.42f, 0.04f, 0.90f), new Color(1.0f, 0.80f, 0.12f, 0.94f), pp);
                }
                else
                {
                    fCol = new Color(0.20f, 0.78f, 1.0f, 0.85f);
                }
                flameMpb.SetColor("_TintColor", fCol);
                flameMpb.SetColor("_Color", fCol);
                flameMpb.SetColor("_BaseColor", fCol);
                flameMpb.SetColor("_EmissionColor", fCol * 1.6f);
                boostFlameRends[i].SetPropertyBlock(flameMpb);
            }
        }

        bool sh = shieldTimer > 0;
        if (shieldRenderer.gameObject.activeSelf != sh) shieldRenderer.gameObject.SetActive(sh);
        if (sh)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(Time.time * 1.5f, 1f), 0.7f, 1f) * 0.35f;
            if (shieldTimer < 1.5f && Mathf.Repeat(Time.time, 0.2f) < 0.1f) c *= 0.2f;
            shieldMpb.SetColor("_TintColor", c);
            shieldMpb.SetColor("_Color", c);
            shieldMpb.SetColor("_BaseColor", c);
            shieldRenderer.SetPropertyBlock(shieldMpb);
        }
    }

    public static void GetDriftSparkColors(int lv, out Color colMain, out Color colSub, out Color colCore)
    {
        if (lv >= 3)
        {
            // Lv3 ウルトラターボ：高彩度ネオンマゼンタ ＆ エレクトリックパープル ＆ ピンクコア
            colMain = new Color(0.96f, 0.10f, 0.86f, 0.95f);
            colSub  = new Color(0.52f, 0.14f, 1.00f, 0.95f);
            colCore = new Color(1.00f, 0.56f, 0.98f, 0.98f);
        }
        else if (lv == 2)
        {
            // Lv2 スーパーターボ：鮮烈なブレイズオレンジ ＆ ゴールデンアンバー ＆ イエローコア
            colMain = new Color(1.00f, 0.36f, 0.02f, 0.95f);
            colSub  = new Color(1.00f, 0.76f, 0.06f, 0.95f);
            colCore = new Color(1.00f, 0.94f, 0.45f, 0.98f);
        }
        else
        {
            // Lv1 ミニターボ：鮮やかなディープエレクトリックブルー ＆ ネオンシアン ＆ アイスブルーコア
            colMain = new Color(0.04f, 0.50f, 1.00f, 0.95f);
            colSub  = new Color(0.10f, 0.85f, 1.00f, 0.95f);
            colCore = new Color(0.58f, 0.94f, 1.00f, 0.98f);
        }
    }

    // ドリフトでヨー回転した実リアタイヤ（wheelSpin[2], wheelSpin[3]）の真下・接地後端座標を取得
    void GetRearTireSparkPoints(out Vector3 rearL, out Vector3 rearR, out Vector3 sparkBack, out Vector3 sparkRight)
    {
        sparkRight = transform.right;
        // 進行方向真後ろ（-Forward）をベースに、ドリフト外向きへわずかに傾けた自然な後方ベクトル
        sparkBack = (-Forward - sparkRight * (drifting ? driftDir * 0.20f : 0f)).normalized;

        if (wheelSpin != null && wheelSpin.Length >= 4 && wheelSpin[2] != null && wheelSpin[3] != null)
        {
            // 各リアタイヤの中心（wheelSpin）から真下（路面接地部）＋進行方向後ろ側0.18mに配置
            Vector3 contactOffset = -transform.up * 0.24f - Forward * 0.18f;
            rearL = wheelSpin[2].position + contactOffset;
            rearR = wheelSpin[3].position + contactOffset;
        }
        else
        {
            float rz = kenneyReady ? -0.52f : -1.0f;
            float rx = kenneyReady ? 0.78f : 0.9f;
            Transform refTr = model != null ? model : transform;
            rearL = refTr.TransformPoint(new Vector3(-rx, 0.08f, rz - 0.18f));
            rearR = refTr.TransformPoint(new Vector3(rx, 0.08f, rz - 0.18f));
        }
    }

    // ドリフトターボの段階（青→オレンジ→紫）が上がった瞬間のド派手なイグニッション火花バースト
    void OnDriftLevelUp(int lv)
    {
        GetRearTireSparkPoints(out Vector3 rearL, out Vector3 rearR, out Vector3 sparkBack, out Vector3 sparkRight);
        Vector3 carry = (previewLockTimer > 0f) ? Vector3.zero : VelDir * (Speed * 0.78f);
        var baseVel = carry + sparkBack * (6.8f + lv * 1.4f) + Vector3.up * 1.1f;

        GetDriftSparkColors(lv, out Color colMain, out Color colSub, out Color colCore);
        int burstCount = 12 + lv * 4;
        float spread = 1.8f + lv * 0.35f;
        Fx.SparkBurst(rearL, baseVel - sparkRight * 0.4f, colMain, colCore, burstCount, spread, 0.20f + lv * 0.025f);
        Fx.SparkBurst(rearR, baseVel + sparkRight * 0.4f, colSub, colCore, burstCount, spread, 0.20f + lv * 0.025f);
        Fx.Emit(rearL, carry + sparkBack * 1.8f + Vector3.up * 0.4f, colMain, 0.50f + lv * 0.08f, 0.13f, 2, 0.4f);
        Fx.Emit(rearR, carry + sparkBack * 1.8f + Vector3.up * 0.4f, colSub, 0.50f + lv * 0.08f, 0.13f, 2, 0.4f);

        if (IsPlayer && !Finished && rm != null && !rm.Demo)
        {
            rm.Audio.DriftSpark(lv);
            GamepadHaptics.Vibrate(PlayerIndex, 0.18f * lv, 0.45f + 0.15f * lv, 0.14f);
        }
    }

    // ドリフト解除によるミニターボ発動時の解放スパーク爆発
    void OnDriftReleaseBoost(int lv)
    {
        GetRearTireSparkPoints(out Vector3 rearL, out Vector3 rearR, out Vector3 sparkBack, out Vector3 sparkRight);
        Vector3 carry = (previewLockTimer > 0f) ? Vector3.zero : VelDir * (Speed * 0.78f);
        var baseVel = carry + sparkBack * (7.8f + lv * 1.6f) + Vector3.up * 1.3f;

        GetDriftSparkColors(lv, out Color colMain, out Color colSub, out Color colCore);
        int count = 16 + lv * 5;
        Fx.SparkBurst(rearL, baseVel - sparkRight * 0.6f, colMain, colCore, count, 2.2f + lv * 0.4f, 0.22f + lv * 0.03f);
        Fx.SparkBurst(rearR, baseVel + sparkRight * 0.6f, colSub, colCore, count, 2.2f + lv * 0.4f, 0.22f + lv * 0.03f);
        Fx.Emit(rearL, carry + sparkBack * 2.5f + Vector3.up * 0.6f, colMain, 0.60f, 0.16f, 3, 0.8f);
        Fx.Emit(rearR, carry + sparkBack * 2.5f + Vector3.up * 0.6f, colSub, 0.60f, 0.16f, 3, 0.8f);
    }

    void Effects(float dt)
    {
        GetRearTireSparkPoints(out Vector3 rearL, out Vector3 rearR, out Vector3 sparkBack, out Vector3 sparkRight);
        var back = -Forward;
        Vector3 carry = (previewLockTimer > 0f) ? Vector3.zero : VelDir * (Speed * 0.78f);

        if (drifting)
        {
            int lv = DriftLevel;
            if (lv == 0)
            {
                // チャージ開始前：白煙＋タイヤ接地面から真後ろへ跳ねる摩擦火花
                var preCol = new Color(0.85f, 0.88f, 0.95f, 0.42f);
                Fx.Emit(rearL, carry + sparkBack * 3f + Vector3.up * 0.6f, preCol, 0.38f, 0.22f, 1, 0.45f);
                Fx.Emit(rearR, carry + sparkBack * 3f + Vector3.up * 0.6f, preCol, 0.38f, 0.22f, 1, 0.45f);
                if (Random.value < 0.70f)
                {
                    var frictionCol = new Color(1f, 0.82f, 0.32f, 0.90f);
                    Vector3 fVel = carry + sparkBack * Random.Range(5.0f, 7.5f) + sparkRight * Random.Range(-1.2f, 1.2f) + Vector3.up * Random.Range(0.4f, 1.6f);
                    Fx.Spark(rearL, fVel, frictionCol, 0.13f, 0.14f, 1, 0.4f);
                    Fx.Spark(rearR, fVel, frictionCol, 0.13f, 0.14f, 1, 0.4f);
                }
            }
            else
            {
                GetDriftSparkColors(lv, out Color colMain, out Color colSub, out Color colCore);
                float popBonus = driftFlarePopTimer > 0f ? 1.22f : 1.0f;

                // 1) 左右リアタイヤの接地後端に密着するプラズマ発光コア（両輪の真下で青/橙/紫に輝く）
                float orbSize = (0.40f + lv * 0.07f) * popBonus;
                var orbVel = carry + sparkBack * 1.6f + Vector3.up * 0.35f;
                Fx.Emit(rearL, orbVel, colMain, orbSize, 0.11f, 1, 0.30f);
                Fx.Emit(rearR, orbVel, colMain, orbSize, 0.11f, 1, 0.30f);
                Fx.Emit(rearL, orbVel, colCore, orbSize * 0.62f, 0.09f, 1, 0.18f);
                Fx.Emit(rearR, orbVel, colCore, orbSize * 0.62f, 0.09f, 1, 0.18f);

                // 2) 左右それぞれのリアタイヤ接地後端から、真後ろへ均等な扇状に迸る高密度レーザー火花シャワー
                int sparkPairs = 4 + lv * 2; // Lv1=6組(12本), Lv2=8組(16本), Lv3=10組(20本)/frame
                float spkSize = (0.15f + lv * 0.025f) * popBonus;
                float spkLife = 0.15f + lv * 0.02f;

                for (int s = 0; s < sparkPairs; s++)
                {
                    Color sCol = (s % 3 == 0) ? colCore : ((s % 2 == 1) ? colMain : colSub);
                    float backSpd = Random.Range(6.0f, 10.0f + lv * 1.6f);
                    // 両タイヤともタイヤ後端から真後ろを中心に±1.8m/sの自然な扇状コーンで噴射（片側のタイヤ側面へ飛び出すのを防ぐ）
                    float coneL = Random.Range(-1.8f, 1.4f);
                    float coneR = Random.Range(-1.4f, 1.8f);
                    float upSpd = Random.Range(0.35f, 2.2f + lv * 0.45f);

                    Vector3 posL = rearL + sparkRight * Random.Range(-0.10f, 0.10f);
                    Vector3 posR = rearR + sparkRight * Random.Range(-0.10f, 0.10f);
                    Vector3 vL = carry + sparkBack * backSpd + sparkRight * coneL + Vector3.up * upSpd;
                    Vector3 vR = carry + sparkBack * backSpd + sparkRight * coneR + Vector3.up * upSpd;
                    Fx.Spark(posL, vL, sCol, spkSize, spkLife, 1, 0.40f);
                    Fx.Spark(posR, vR, sCol, spkSize, spkLife, 1, 0.40f);
                }
            }

            // タイヤスモーク（火花の輝きを引き立てる控えめな白煙）
            var smokeCol = new Color(0.88f, 0.90f, 0.95f, lv > 0 ? 0.15f : 0.22f);
            Fx.Smoke(rearL, carry * 0.5f + sparkBack * 1.8f + Vector3.up * 0.4f, smokeCol, 0.46f + lv * 0.07f, 0.38f, 1, 0.5f);
            Fx.Smoke(rearR, carry * 0.5f + sparkBack * 1.8f + Vector3.up * 0.4f, smokeCol, 0.46f + lv * 0.07f, 0.38f, 1, 0.5f);
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
            Fx.Smoke(rearL, carry * 0.5f + back * 2f + Vector3.up * 0.8f, brakeSmoke, 0.55f, 0.45f, 1, 0.6f);
            Fx.Smoke(rearR, carry * 0.5f + back * 2f + Vector3.up * 0.8f, brakeSmoke, 0.55f, 0.45f, 1, 0.6f);
        }

        if (boostTimer > 0)
        {
            float exZ = kenneyReady ? -0.72f : -1.35f;
            float exY = kenneyReady ? 0.46f : 0.28f;
            float exX = kenneyReady ? 0.28f : 0.35f;
            var exL = transform.TransformPoint(new Vector3(-exX, exY, exZ));
            var exR = transform.TransformPoint(new Vector3(exX, exY, exZ));

            Color outerFlame = new Color(1.0f, 0.52f, 0.08f, 0.92f);
            Color innerCore  = new Color(0.20f, 0.82f, 1.0f, 0.95f);
            if (slipstreamTimer > 0 || boostColorLevel == 4)
            {
                outerFlame = new Color(0.22f, 0.90f, 1.0f, 0.92f);
                innerCore  = new Color(0.85f, 0.98f, 1.0f, 0.96f);
            }
            else if (boostColorLevel == 1)
            {
                outerFlame = new Color(0.04f, 0.58f, 1.0f, 0.92f);
                innerCore  = new Color(0.55f, 0.94f, 1.0f, 0.95f);
            }
            else if (boostColorLevel == 3)
            {
                outerFlame = new Color(0.96f, 0.14f, 0.88f, 0.92f);
                innerCore  = new Color(0.55f, 0.22f, 1.0f, 0.95f);
            }

            // アフターバーナー炎（車体と並走させつつ後方へ噴射することでカメラへの飛び込みを防ぐ）
            Fx.Emit(exL, carry + back * 5.5f, outerFlame, 0.48f, 0.12f, 1, 0.6f);
            Fx.Emit(exR, carry + back * 5.5f, outerFlame, 0.48f, 0.12f, 1, 0.6f);
            Fx.Emit(exL, carry + back * 4.0f, innerCore, 0.32f, 0.09f, 1, 0.3f);
            Fx.Emit(exR, carry + back * 4.0f, innerCore, 0.32f, 0.09f, 1, 0.3f);

            // ブースト中もタイヤ脇から火花ストリークがV字に迸る
            Fx.Spark(rearL, carry + back * 6.5f - transform.right * Random.Range(2.0f, 4.8f) + Vector3.up * Random.Range(1.0f, 3.2f), outerFlame, 0.15f, 0.16f, 1, 1.2f);
            Fx.Spark(rearR, carry + back * 6.5f + transform.right * Random.Range(2.0f, 4.8f) + Vector3.up * Random.Range(1.0f, 3.2f), innerCore, 0.15f, 0.16f, 1, 1.2f);
        }
        if (Offroad && Speed > 6f && Random.value < 0.6f)
            Fx.Smoke((rearL + rearR) * 0.5f, carry * 0.4f + back * 2.5f + Vector3.up * 1.5f, new Color(0.65f, 0.55f, 0.35f, 0.45f), 0.8f, 0.6f, 1, 1.2f);

        // スリップストリーム（ドラフティング）の風切りストリーム演出（文字なしで直感的に認知できるよう強化）
        if ((InDraftStream || slipstreamTimer > 0) && Speed > 13f)
        {
            float charge01 = slipstreamTimer > 0 ? 1f : Mathf.Clamp01(slipstreamDraftTimer / 1.15f);
            float sideX = kenneyReady ? 0.72f : 0.88f;
            var draftPosL = transform.TransformPoint(new Vector3(-sideX, 0.48f, 0.7f));
            var draftPosR = transform.TransformPoint(new Vector3( sideX, 0.48f, 0.7f));
            var nosePos   = transform.TransformPoint(new Vector3(0f, 0.55f, 0.95f));
            var streamCol = Color.Lerp(new Color(0.75f, 0.94f, 1f, 0.65f), new Color(0.30f, 0.95f, 1f, 0.92f), charge01);
            Vector3 windVel = carry + back * Mathf.Lerp(7f, 11f, charge01) + Vector3.up * 0.25f;
            Fx.Spark(draftPosL, windVel - transform.right * 0.6f, streamCol, 0.14f + charge01 * 0.05f, 0.16f, 1, 0.2f);
            Fx.Spark(draftPosR, windVel + transform.right * 0.6f, streamCol, 0.14f + charge01 * 0.05f, 0.16f, 1, 0.2f);
            if (charge01 > 0.35f || slipstreamTimer > 0)
            {
                Fx.Spark(nosePos + transform.right * Random.Range(-0.45f, 0.45f), windVel + Vector3.up * 0.5f, new Color(0.9f, 0.99f, 1f, 0.85f), 0.12f, 0.14f, 1, 0.15f);
            }
        }

        if (shieldTimer > 0 && Random.value < 0.5f)
            Fx.Emit(transform.position + Vector3.up * 0.8f + Random.onUnitSphere * 1.5f, carry + Vector3.up, Color.HSVToRGB(Random.value, 0.6f, 1f), 0.3f, 0.4f);
    }

    public void SetPreviewSpeedAndBoost(float previewSpeed, float boostDur = 0f, ItemType previewItem = ItemType.None)
    {
        Fx.Clear();
        previewLockTimer = 0.18f;
        Speed = previewSpeed;
        VelDir = Forward;
        drifting = false;
        driftDir = 0;
        driftCharge = 0f;
        lastDriftLevel = 0;
        boostTimer = boostDur;
        if (boostDur > 0f && boostColorLevel == 0) boostColorLevel = 1;
        if (previewItem != ItemType.None) Item = previewItem;
        visYaw = 0f;
        UpdateVisual(0.05f, 0f);
        if (boostDur > 0f) Effects(0.016f);
    }

    public void SetPreviewDrift(int dir, float charge, float previewSpeed = 27.5f, ItemType previewItem = ItemType.None)
    {
        Fx.Clear();
        previewLockTimer = 0.18f;
        Speed = previewSpeed;
        VelDir = Forward;
        drifting = dir != 0 && charge > 0f;
        driftDir = dir;
        driftCharge = charge;
        lastDriftLevel = DriftLevel;
        driftFlarePopTimer = drifting ? 0.20f : 0f;
        boostTimer = 0f;
        if (previewItem != ItemType.None) Item = previewItem;
        visYaw = drifting ? driftDir * 22f : 0f;
        steerVis = dir * 0.85f;
        UpdateVisual(0.05f, dir * 0.85f);
        if (drifting)
        {
            OnDriftLevelUp(DriftLevel);
            Effects(0.016f);
        }
    }
}
