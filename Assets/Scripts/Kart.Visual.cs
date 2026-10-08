using UnityEngine;

// カートの見た目：モデル生成、姿勢・ホイール・ライトの更新、走行エフェクト
public partial class Kart
{
    // ───────────────────────── 見た目 ─────────────────────────

    static readonly string[] KenneyKarts = { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" };

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
        paint.SetColor("_BaseColor", Color.Lerp(Color.white, Color, 0.65f));
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
            shieldMpb.SetColor("_BaseColor", c);
            shieldRenderer.SetPropertyBlock(shieldMpb);
        }
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
        // スリップストリーム（ドラフティング）の風切りストリーム演出
        if (InDraftStream && Speed > 13f)
        {
            float sideX = kenneyReady ? 0.72f : 0.88f;
            var draftPosL = transform.TransformPoint(new Vector3(-sideX, 0.45f, 0.6f));
            var draftPosR = transform.TransformPoint(new Vector3(sideX, 0.45f, 0.6f));
            var streamCol = new Color(0.6f, 0.9f, 1f, 0.75f);
            Fx.Emit(draftPosL, back * 7f + Vector3.up * 0.5f, streamCol, 0.22f, 0.16f, 1, 0.25f);
            Fx.Emit(draftPosR, back * 7f + Vector3.up * 0.5f, streamCol, 0.22f, 0.16f, 1, 0.25f);
        }

        if (shieldTimer > 0 && Random.value < 0.5f)
            Fx.Emit(transform.position + Vector3.up * 0.8f + Random.onUnitSphere * 1.5f, Vector3.up, Color.HSVToRGB(Random.value, 0.6f, 1f), 0.3f, 0.4f);
    }
}
