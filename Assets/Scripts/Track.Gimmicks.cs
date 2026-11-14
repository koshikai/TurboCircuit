using System.Collections.Generic;
using UnityEngine;

public partial class Track
{
    void BuildStartLine(RaceManager rm, TrackDef def)
    {
        var checker = new Material(rm.roadMaterial) { mainTexture = TextureGen.Checker(), color = Color.white };
        checker.mainTexture.filterMode = FilterMode.Point;
        createdMaterials.Add(checker);
        var rot = Quaternion.LookRotation(Dirs[0]);
        Box("StartLine", Pts[0] + Vector3.up * 0.04f, new Vector3(HalfWidth * 2f, 2.5f, 1), rot * Quaternion.Euler(90, 0, 0), checker, PrimitiveType.Quad);
        checker.mainTextureScale = new Vector2(12, 2);

        // スタートゲート（北大キャンパスは赤レンガ正門調、その他はレーシングクローム）
        bool isCampus = def.SceneryTheme == 4;
        var pillarMat = isCampus ? new Material(rm.wallMaterial) { mainTexture = TextureGen.RedBrick(), color = Color.white } : rm.chromeMaterial;
        var beamMat = isCampus ? new Material(rm.wallMaterial) { mainTexture = TextureGen.CampusBanner(0), color = Color.white } : new Material(rm.roadMaterial) { mainTexture = TextureGen.Checker(), color = Color.white };
        if (isCampus) createdMaterials.Add(pillarMat);
        createdMaterials.Add(beamMat);
        if (!isCampus)
        {
            beamMat.mainTexture.filterMode = FilterMode.Point;
            beamMat.mainTextureScale = new Vector2(16, 2);
        }

        foreach (float side in new[] { -1f, 1f })
        {
            var pPos = PointAt(0, side * (HalfWidth + 3.5f)) + Vector3.up * 4.5f;
            Box("GatePillar", pPos, new Vector3(isCampus ? 1.8f : 1.2f, 9f, isCampus ? 1.8f : 1.2f), rot, pillarMat);
            if (isCampus)
            {
                // 正門門柱頭のクラシック装飾石＆球体照明
                Box("PillarCap", pPos + Vector3.up * 4.7f, new Vector3(2.2f, 0.4f, 2.2f), rot, rm.standMaterial);
                Box("PillarGlobe", pPos + Vector3.up * 5.4f, Vector3.one * 1.1f, rot, rm.glowMaterial, PrimitiveType.Sphere);
            }
        }
        float gateWidth = (HalfWidth + 3.5f) * 2f + 1.2f;
        float gateBeamH = isCampus ? 2.2f : 1.6f;
        Box("GateBeam", Pts[0] + Vector3.up * 8.5f, new Vector3(gateWidth, gateBeamH, 0.6f), rot, isCampus ? pillarMat : beamMat);
        if (isCampus)
        {
            // 北大正門の中央銘板看板（アスペクト比 4:1 に合わせた大学名プレート）
            float plaqueH = 1.8f;
            float plaqueW = plaqueH * 4.0f; // 7.2m
            var plaquePos = Pts[0] + Vector3.up * 8.5f;
            Box("GatePlaqueFrame", plaquePos, new Vector3(plaqueW + 0.24f, plaqueH + 0.24f, 0.66f), rot, rm.standMaterial);
            Box("GatePlaqueFront", plaquePos - Dirs[0] * 0.34f, new Vector3(plaqueW, plaqueH, 1f), rot, beamMat, PrimitiveType.Quad);
            Box("GatePlaqueBack", plaquePos + Dirs[0] * 0.34f, new Vector3(plaqueW, plaqueH, 1f), rot * Quaternion.Euler(0, 180, 0), beamMat, PrimitiveType.Quad);
        }
        signalMpb = new MaterialPropertyBlock();
        for (int i = 0; i < 3; i++)
        {
            float xOffset = (i - 1) * 2.4f;
            var lampPos = Pts[0] + rot * new Vector3(xOffset, 9.6f, 0.35f);
            var lampMat = new Material(rm.glowMaterial) { color = Color.white };
            createdMaterials.Add(lampMat);
            Box("SignalCase_" + i, lampPos, new Vector3(1.5f, 1.5f, 0.3f), rot, rm.tireMaterial);
            var bulb = Box("SignalBulb_" + i, lampPos + rot * Vector3.forward * 0.16f, new Vector3(1.1f, 1.1f, 0.1f), rot, lampMat, PrimitiveType.Cylinder);
            bulb.transform.localRotation = rot * Quaternion.Euler(90, 0, 0);
            startSignalBulbs[i] = bulb.GetComponent<Renderer>();

            var lightGo = new GameObject("SignalLight_" + i);
            lightGo.transform.SetParent(bulb.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 14f;
            l.intensity = 0f;
            startSignalLights[i] = l;
        }
        UpdateStartSignals(-1); // 初期状態は全消灯

        // 本格グランプリ観客席（Circuit & Overpassテーマ時：屋根キャノピー・支柱・スポンサー帯・カラフル段差シート・立体観客）
        var spectatorPalette = SpectatorPalette.Create(rm, createdMaterials);
        if (def.SceneryTheme == 0 || def.SceneryTheme == 5)
        {
            var standRot = Quaternion.LookRotation(Dirs[12]);
            var standRight = Rights[12];
            float standLen = 62f;

            var seatColors = new[]
            {
                new Color(0.95f, 0.22f, 0.25f),
                new Color(1.00f, 0.58f, 0.12f),
                new Color(1.00f, 0.85f, 0.15f),
                new Color(0.18f, 0.78f, 0.42f),
                new Color(0.18f, 0.55f, 0.98f)
            };
            var roofMat = MakeMat(rm.wallMaterial, null, new Color(0.95f, 0.96f, 0.98f));
            var fasciaTex = TextureGen.SponsorBanner(0);
            fasciaTex.wrapMode = TextureWrapMode.Repeat;
            var fasciaMat = MakeMat(rm.wallMaterial, fasciaTex, Color.white);
            float fasciaLen = standLen + 1.6f;
            float fasciaRepeat = Mathf.Round(fasciaLen / (1.45f * 4f));
            fasciaMat.mainTextureScale = new Vector2(fasciaRepeat, 1f);

            for (int row = 0; row < 5; row++)
            {
                float h = 1.4f + row * 2.1f;
                var p = PointAt(12, -(WallOffset + 5.2f + row * 2.2f)) + Vector3.up * (h * 0.5f);
                Box("StandTier_" + row, p, new Vector3(2.2f, h, standLen), standRot, rm.standMaterial);

                // カラフルなシートベンチ段
                var seatMat = MakeMat(rm.wallMaterial, null, seatColors[row]);
                var seatPos = PointAt(12, -(WallOffset + 4.8f + row * 2.2f)) + Vector3.up * (h + 0.18f);
                Box("StandSeat_" + row, seatPos, new Vector3(1.1f, 0.36f, standLen - 1.2f), standRot, seatMat);
            }

            // 後方壁＆キャノピー支柱・屋根
            float backOffset = -(WallOffset + 5.2f + 4 * 2.2f + 1.4f);
            var backWallPos = PointAt(12, backOffset) + Vector3.up * 7.2f;
            Box("StandBackWall", backWallPos, new Vector3(0.8f, 14.4f, standLen), standRot, rm.standMaterial);

            for (int c = -2; c <= 2; c++)
            {
                float zOff = c * (standLen * 0.22f);
                var colPos = PointAt(12, backOffset + 0.4f) + Dirs[12] * zOff + Vector3.up * 11.2f;
                Box("StandRoofCol_" + c, colPos, new Vector3(0.75f, 9.5f, 0.75f), standRot, rm.chromeMaterial);
            }

            // 傾斜したスタジアムルーフ（キャノピー）と前面スポンサー看板帯
            var roofCenter = PointAt(12, -(WallOffset + 9.2f)) + Vector3.up * 15.6f;
            Box("StandRoofCanopy", roofCenter, new Vector3(14.5f, 0.65f, standLen + 2f), standRot * Quaternion.Euler(0, 0, -8f), roofMat);
            var fasciaPos = PointAt(12, -(WallOffset + 2.4f)) + Vector3.up * 14.6f;
            Box("StandRoofFascia", fasciaPos, new Vector3(0.35f, 1.45f, standLen + 1.6f), standRot, fasciaMat);

            // ぴょこぴょこ跳ねて応援する3D観客（帽子・表情・服・腕・応援旗・メガホンを装備した可愛いトイキャラクター）
            var rng = new System.Random(3);
            for (int i = 0; i < 140; i++)
            {
                int row = rng.Next(5);
                float along = (float)rng.NextDouble() * (standLen - 4f) - (standLen * 0.5f - 2f);
                var basePos = PointAt(12, -(WallOffset + 4.8f + row * 2.2f)) + Dirs[12] * along;
                basePos.y = Pts[12].y + 1.4f + row * 2.1f + 0.36f; // ベンチシートの上にピッタリ接地

                SpectatorFan.CreateFan(
                    transform,
                    basePos,
                    Quaternion.LookRotation(standRight, Vector3.up),
                    rng,
                    rm,
                    spectatorPalette
                );
            }
        }
        else
        {
            // その他のコース：スタートサイド＆ピット周辺の応援サポーターたち
            var rng = new System.Random(def.SceneryTheme * 101 + 7);
            for (int i = 0; i < 28; i++)
            {
                int ptIdx = Wrap(i < 14 ? (2 + i) : (Count - 1 - (i - 14)));
                float side = (i % 2 == 0) ? 1f : -1f;
                float dist = WallOffset + 2.8f + (float)rng.NextDouble() * 2.0f;
                var pos = PointAt(ptIdx, side * dist);
                pos.y = GetTerrainHeight(pos.x, pos.z, def);

                var lookDir = (Pts[ptIdx] - pos);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude < 0.01f) lookDir = Vector3.forward;

                SpectatorFan.CreateFan(
                    transform,
                    pos,
                    Quaternion.LookRotation(lookDir.normalized, Vector3.up),
                    rng,
                    rm,
                    spectatorPalette
                );
            }
        }
    }

    void BuildBoostPads(RaceManager rm)
    {
        var def = Courses[ActiveCourseIndex];
        var padMat = new Material(rm.boostPadMaterial) { mainTexture = TextureGen.Chevrons() };
        createdMaterials.Add(padMat);
        padMat.SetTexture("_EmissionMap", padMat.mainTexture);

        int[] spots;
        float[] lats;

        if (def.Branches != null && def.Branches.Length > 0)
        {
            var b = def.Branches[0];
            // 序盤ワインディング登坂、右トレッスル橋上（2連）、ヘアピン前、ホームストレート手前
            int bMid1 = Mathf.RoundToInt(Mathf.Lerp(b.Start, b.End, 0.35f) * Count);
            int bMid2 = Mathf.RoundToInt(Mathf.Lerp(b.Start, b.End, 0.70f) * Count);
            spots = new[] { Count * 20 / 100, bMid1, bMid2, Count * 78 / 100, Count * 91 / 100 };
            lats = new[] { 3.5f, b.RightOffset, b.RightOffset, 0f, 0f };
        }
        else
        {
            spots = new[] { Count * 22 / 100, Count * 48 / 100, Count * 81 / 100 };
            lats = new[] { 4.5f, -4.5f, 0f };
        }

        for (int i = 0; i < spots.Length; i++)
        {
            int idx = spots[i];
            BoostPads.Add((idx, lats[i]));
            var p = PointAt(idx, lats[i]) + Vector3.up * 0.06f;
            Box("BoostPad", p, new Vector3(4f, 6f, 1), Quaternion.LookRotation(Dirs[idx]) * Quaternion.Euler(90, 0, 0), padMat, PrimitiveType.Quad);
        }
    }

    void PlanItemBoxes()
    {
        var def = Courses[ActiveCourseIndex];
        if (def.Branches != null && def.Branches.Length > 0)
        {
            var b = def.Branches[0];
            int bItem1 = Mathf.RoundToInt(Mathf.Lerp(b.Start, b.End, 0.30f) * Count);
            int bItem2 = Mathf.RoundToInt(Mathf.Lerp(b.Start, b.End, 0.65f) * Count);
            // 通常区間（序盤、合流後インサイド、最終コーナー手前）
            int[] normalRows = { Count * 12 / 100, Count * 64 / 100, Count * 87 / 100 };
            foreach (int idx in normalRows)
                for (int k = -2; k <= 2; k++)
                    ItemBoxSpots.Add((idx, k * 3.4f));

            // 分岐左ルート（川沿いコース）
            int[] splitRows = { bItem1, bItem2 };
            foreach (int idx in splitRows)
                for (int k = -1; k <= 1; k++)
                    ItemBoxSpots.Add((idx, b.LeftOffset + k * 2.2f));
        }
        else
        {
            int[] rows = { Count * 12 / 100, Count * 38 / 100, Count * 63 / 100, Count * 90 / 100 };
            foreach (int idx in rows)
                for (int k = -2; k <= 2; k++)
                    ItemBoxSpots.Add((idx, k * 3.4f));
        }
    }

    public void UpdateStartSignals(int beepStep)
    {
        if (signalMpb == null) signalMpb = new MaterialPropertyBlock();
        // beepStep: -1 = 全消灯, 0 = 赤1灯(カウント3), 1 = 赤2灯(カウント2), 2 = 赤3灯(カウント1), 3 = 全青(GO!)
        Color redOff = new Color(0.18f, 0.04f, 0.04f, 1f);
        Color redOn = new Color(3.2f, 0.12f, 0.12f, 1f);
        Color greenOn = new Color(0.2f, 3.4f, 0.8f, 1f);

        for (int i = 0; i < 3; i++)
        {
            if (startSignalBulbs[i] == null) continue;
            Color emit;
            float lightIntensity = 0f;
            Color lightCol = Color.red;

            if (beepStep < 0 || beepStep >= 4)
            {
                emit = redOff;
                lightIntensity = 0f;
            }
            else if (beepStep == 3)
            {
                emit = greenOn;
                lightIntensity = 3.2f;
                lightCol = new Color(0.25f, 1f, 0.45f);
            }
            else
            {
                bool on = i <= beepStep;
                emit = on ? redOn : redOff;
                lightIntensity = on ? 2.4f : 0f;
                lightCol = Color.red;
            }

            signalMpb.SetColor("_Color", emit);
            signalMpb.SetColor("_BaseColor", emit);
            signalMpb.SetColor("_EmissionColor", emit);
            startSignalBulbs[i].SetPropertyBlock(signalMpb);

            if (startSignalLights[i] != null)
            {
                startSignalLights[i].intensity = lightIntensity;
                startSignalLights[i].color = lightCol;
            }
        }
    }
}
