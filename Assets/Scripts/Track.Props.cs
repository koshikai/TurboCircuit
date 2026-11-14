using System.Collections.Generic;
using UnityEngine;

public partial class Track
{
    void BuildScenery(RaceManager rm, TrackDef def)
    {
        var rng = new System.Random(11);
        int placed = 0;

        // スタイライズド 3D 自然モデル (Quaternius Stylized Nature Kit, CC0/MIT)
        var prefabBirch1 = ResourceCache.Load<GameObject>("Props/StylizedNature/BirchTree_1");
        var prefabBirch2 = ResourceCache.Load<GameObject>("Props/StylizedNature/BirchTree_2");
        var prefabNormal1 = ResourceCache.Load<GameObject>("Props/StylizedNature/NormalTree_1");
        var prefabNormal2 = ResourceCache.Load<GameObject>("Props/StylizedNature/NormalTree_2");
        var prefabPine1 = ResourceCache.Load<GameObject>("Props/StylizedNature/PineTree_1");
        var prefabPine2 = ResourceCache.Load<GameObject>("Props/StylizedNature/PineTree_2");
        var prefabPine3 = ResourceCache.Load<GameObject>("Props/StylizedNature/PineTree_3");
        var prefabPalm1 = ResourceCache.Load<GameObject>("Props/StylizedNature/PalmTree_1");
        var prefabPalm2 = ResourceCache.Load<GameObject>("Props/StylizedNature/PalmTree_2");
        var prefabPalm3 = ResourceCache.Load<GameObject>("Props/StylizedNature/PalmTree_3");
        var prefabDead1 = ResourceCache.Load<GameObject>("Props/StylizedNature/DeadTree_1");
        var prefabDead2 = ResourceCache.Load<GameObject>("Props/StylizedNature/DeadTree_2");
        var prefabDead3 = ResourceCache.Load<GameObject>("Props/StylizedNature/DeadTree_3");
        var prefabBush = ResourceCache.Load<GameObject>("Props/StylizedNature/Bush");
        var prefabBushFlowers = ResourceCache.Load<GameObject>("Props/StylizedNature/Bush_Flowers");
        var prefabBushLarge = ResourceCache.Load<GameObject>("Props/StylizedNature/Bush_Large");
        var prefabFlower1 = ResourceCache.Load<GameObject>("Props/StylizedNature/Flower_1_Clump");
        var prefabFlower2 = ResourceCache.Load<GameObject>("Props/StylizedNature/Flower_2_Clump");
        var prefabGrassLarge = ResourceCache.Load<GameObject>("Props/StylizedNature/Grass_Large");
        var prefabRock1 = ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_1");
        var prefabRock2 = ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_2");
        var prefabRock3 = ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_3");
        var prefabRock4 = ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_4");
        var prefabRock5 = ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_5");

        // テクスチャ
        var texBirchBark = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Bark");
        var texBirchLeaves = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Leaves");
        var texNormalBark = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Bark");
        var texNormalLeaves = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Leaves");
        var texPineBark = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Bark");
        var texPineLeaves = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Leaves");
        var texPalmTrunk = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/PalmTree_Trunk");
        var texPalmLeaves = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/PalmTree_Leaves");
        var texBushLeaves = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Bush_Leaves");
        var texFlowers = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Flowers");
        var texRocks = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Rocks");
        var texRocksDesert = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Desert");
        var texRocksRedDesert = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Red_Desert");

        // テーマ別マテリアル
        // Theme 0 (Turbo Circuit): 鮮やかで親しみやすいアーケードグリーン
        var circuitTrunkMat = MakeMat(rm.trunkMaterial, texNormalBark, Color.white);
        var circuitLeavesMat1 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.45f, 0.94f, 0.35f), new Color(0.05f, 0.14f, 0.03f));
        var circuitLeavesMat2 = MakeMat(rm.leavesMaterial, texNormalLeaves ?? texBirchLeaves, new Color(0.58f, 0.98f, 0.40f), new Color(0.06f, 0.16f, 0.04f));
        var circuitLeavesMat3 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.30f, 0.80f, 0.28f), new Color(0.04f, 0.10f, 0.03f));
        var circuitBushMat = MakeMat(rm.leavesMaterial, texBushLeaves, Color.white, new Color(0.04f, 0.10f, 0.03f));
        var circuitFlowerMat = MakeMat(rm.leavesMaterial, texFlowers, Color.white);
        var circuitRockMat = MakeMat(rm.standMaterial, texRocks, Color.white);

        // Theme 1 (Sunset Dunes): 夕焼け砂漠、ヤシの木、枯れ木、砂岩
        var palmTrunkMat = MakeMat(rm.trunkMaterial, texPalmTrunk, Color.white);
        var palmLeafMat = MakeMat(rm.leavesMaterial, texPalmLeaves, new Color(0.95f, 0.98f, 0.42f), new Color(0.14f, 0.12f, 0.02f));
        var deadTreeMat = MakeMat(rm.trunkMaterial, texNormalBark, new Color(0.68f, 0.60f, 0.50f));
        var desertRockMat1 = MakeMat(rm.mountainMaterial, texRocksDesert ?? texRocks, new Color(1.05f, 0.85f, 0.65f));
        var desertRockMat2 = MakeMat(rm.mountainMaterial, texRocksRedDesert ?? texRocksDesert ?? texRocks, new Color(1.15f, 0.72f, 0.50f));

        // Theme 2 (Frost Peak): 白銀の雪山、氷河、積雪針葉樹
        var frostTrunkMat = MakeMat(rm.trunkMaterial, texPineBark, new Color(0.55f, 0.60f, 0.68f));
        var frostLeafMat1 = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.88f, 0.97f, 1.0f), new Color(0.06f, 0.12f, 0.16f));
        var frostLeafMat2 = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.58f, 0.82f, 0.78f), new Color(0.04f, 0.10f, 0.12f));
        var frostRockMat = MakeMat(rm.mountainMaterial, texRocks, new Color(0.82f, 0.90f, 0.98f));
        var frostBushMat = MakeMat(rm.leavesMaterial, texBushLeaves, new Color(0.88f, 0.94f, 1.0f), new Color(0.05f, 0.10f, 0.14f));
        var crystalMat = new Material(rm.glowMaterial) { color = new Color(0.35f, 0.88f, 1.0f, 0.82f) };
        createdMaterials.Add(crystalMat);

        bool city = def.SceneryTheme == 3;
        if (city) BuildCityScenery(rm, def, rng);

        bool campus = def.SceneryTheme == 4;
        if (campus) BuildCampusScenery(rm, def, rng);

        bool overpass = def.SceneryTheme == 5;
        if (overpass) BuildSkylineOverpassScenery(rm, def, rng);

        bool frost = def.SceneryTheme == 2;
        if (frost) BuildFrostPeakScenery(rm, def, rng);

        // 自然物・木・岩の配置ループ（起伏に富んだプロシージャル地表に追従）
        for (int tries = 0; tries < 3500 && placed < 280 && !city && !campus && !overpass; tries++)
        {
            float px = Mathf.Lerp(bounds.min.x - 90, bounds.max.x + 90, (float)rng.NextDouble());
            float pz = Mathf.Lerp(bounds.min.z - 90, bounds.max.z + 90, (float)rng.NextDouble());
            float py = GetTerrainHeight(px, pz, def);
            var pos = new Vector3(px, py, pz);
            if (DistanceToTrack(pos, true) < WallOffset + 4.2f) continue;
            float s = 2.4f + (float)rng.NextDouble() * 1.5f;
            Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            if (def.SceneryTheme == 0) // Circuit: スタイライズド大樹・白樺・花壇・低木・自然岩
            {
                double r = rng.NextDouble();
                if (r < 0.55) // 大樹（NormalTree / BirchTree）
                {
                    var treePrefab = (rng.Next(2) == 0) ? (prefabNormal1 ?? prefabNormal2) : (prefabBirch1 ?? prefabBirch2);
                    var leafMat = (rng.Next(3) == 0) ? circuitLeavesMat1 : ((rng.Next(2) == 0) ? circuitLeavesMat2 : circuitLeavesMat3);
                    SpawnNatureProp(treePrefab, pos, rot, Vector3.one * s, circuitTrunkMat, leafMat);
                }
                else if (r < 0.78) // 低木・花壇
                {
                    var bushPrefab = (rng.Next(3) == 0) ? (prefabFlower1 ?? prefabFlower2) : ((rng.Next(2) == 0) ? prefabBushFlowers : prefabBush);
                    SpawnProp(bushPrefab, pos, rot, Vector3.one * (s * 0.55f), circuitBushMat, circuitFlowerMat);
                }
                else if (r < 0.90) // スタイライズド自然岩
                {
                    var rockPrefab = (rng.Next(3) == 0) ? prefabRock1 : ((rng.Next(2) == 0) ? prefabRock2 : prefabRock3);
                    SpawnProp(rockPrefab, pos, rot, Vector3.one * (s * 0.75f), circuitRockMat);
                }
                else // 芝生パッチ
                {
                    SpawnProp(prefabGrassLarge ?? prefabBush, pos, rot, Vector3.one * (s * 0.6f), circuitBushMat);
                }
            }
            else if (def.SceneryTheme == 1) // Desert Dunes / CANYON SPLIT
            {
                bool isCanyon = (def.Branches != null && def.Branches.Length > 0);
                double r = rng.NextDouble();
                if (isCanyon)
                {
                    // CANYON SPLIT: 雄大な赤岩キャニオン・ビュート・メサ・乾燥した枯れ木
                    // ヤシの木はスタート・フィニッシュ（オアシス低地）付近のみ限定的に出現
                    bool nearOasis = (pos.y < 4.0f && DistanceToTrack(pos, true) < 80f);
                    if (nearOasis && r < 0.15) // オアシス低地のごく一部にのみヤシの木
                    {
                        var palmPrefab = (rng.Next(3) == 0) ? prefabPalm1 : ((rng.Next(2) == 0) ? prefabPalm2 : prefabPalm3);
                        SpawnNatureProp(palmPrefab, pos, rot, Vector3.one * (s * 1.15f), palmTrunkMat, palmLeafMat);
                    }
                    else if (r < 0.25) // 砂漠の枯れ木
                    {
                        var deadPrefab = (rng.Next(3) == 0) ? prefabDead1 : ((rng.Next(2) == 0) ? prefabDead2 : prefabDead3);
                        SpawnNatureProp(deadPrefab, pos, rot, Vector3.one * (s * 1.05f), deadTreeMat, deadTreeMat);
                    }
                    else // 雄大な赤岩・断崖・モニュメント群（キャニオンの主役）
                    {
                        var rockPrefab = (rng.Next(5) == 0) ? prefabRock1 : ((rng.Next(4) == 0) ? prefabRock2 : ((rng.Next(3) == 0) ? prefabRock3 : ((rng.Next(2) == 0) ? prefabRock4 : prefabRock5)));
                        var rMat = (rng.Next(4) == 0) ? desertRockMat1 : desertRockMat2; // 赤岩マテリアルを優先
                        float canyonScale = s * (1.5f + (float)rng.NextDouble() * 0.9f); // ダイナミックな巨岩
                        SpawnProp(rockPrefab, pos, rot, Vector3.one * canyonScale, rMat);
                    }
                }
                else
                {
                    // SUNSET DUNES: 従来の砂漠オアシス景観
                    if (r < 0.45) // ヤシの木
                    {
                        var palmPrefab = (rng.Next(3) == 0) ? prefabPalm1 : ((rng.Next(2) == 0) ? prefabPalm2 : prefabPalm3);
                        SpawnNatureProp(palmPrefab, pos, rot, Vector3.one * (s * 1.15f), palmTrunkMat, palmLeafMat);
                    }
                    else if (r < 0.72) // 砂漠の枯れ木
                    {
                        var deadPrefab = (rng.Next(3) == 0) ? prefabDead1 : ((rng.Next(2) == 0) ? prefabDead2 : prefabDead3);
                        SpawnNatureProp(deadPrefab, pos, rot, Vector3.one * (s * 0.95f), deadTreeMat, deadTreeMat);
                    }
                    else // 砂漠の自然岩・砂岩群
                    {
                        var rockPrefab = (rng.Next(5) == 0) ? prefabRock1 : ((rng.Next(4) == 0) ? prefabRock2 : ((rng.Next(3) == 0) ? prefabRock3 : ((rng.Next(2) == 0) ? prefabRock4 : prefabRock5)));
                        var rMat = (rng.Next(2) == 0) ? desertRockMat1 : desertRockMat2;
                        SpawnProp(rockPrefab, pos, rot, Vector3.one * (s * 1.4f), rMat);
                    }
                }
            }
            else // Frost Peak: スノーパイン、氷柱結晶、雪岩、雪低木
            {
                double r = rng.NextDouble();
                if (r < 0.65) // スノーパイン（雪化粧のエゾマツ針葉樹）
                {
                    var pinePrefab = (rng.Next(3) == 0) ? prefabPine1 : ((rng.Next(2) == 0) ? prefabPine2 : prefabPine3);
                    var lMat = (rng.Next(3) == 0) ? frostLeafMat2 : frostLeafMat1;
                    SpawnNatureProp(pinePrefab, pos, rot, Vector3.one * (s * 1.15f), frostTrunkMat, lMat);
                }
                else if (r < 0.82) // 雪をかぶった自然岩
                {
                    var rockPrefab = (rng.Next(3) == 0) ? prefabRock1 : ((rng.Next(2) == 0) ? prefabRock2 : prefabRock3);
                    SpawnProp(rockPrefab, pos, rot, Vector3.one * (s * 1.0f), frostRockMat);
                }
                else if (r < 0.92) // 氷柱結晶（クリスタル）
                {
                    float ch = 3.0f + (float)rng.NextDouble() * 4.0f;
                    Box("IceCrystal", pos + Vector3.up * ch * 0.5f, new Vector3(1.3f, ch, 1.3f) * (s * 0.35f),
                        Quaternion.Euler((float)rng.NextDouble() * 14f - 7f, (float)rng.NextDouble() * 360f, 0), crystalMat, PrimitiveType.Cylinder);
                }
                else // 雪をかぶった低木
                {
                    SpawnProp(prefabBushLarge ?? prefabBush, pos, rot, Vector3.one * (s * 0.65f), frostBushMat);
                }
            }
            placed++;
        }

        // 遠くの山（テーマ 4: 北大キャンパスは札幌の実景に合わせて手稲山・藻岩山の連峰メッシュと都心スカイライン）
        if (def.SceneryTheme == 4)
        {
            BuildSapporoMountains(rm, rng);

            // 南〜東側：札幌都心部・JRタワー方面の遠景スカイライン
            var cityFacades = CityMaterials(rm);

            // ランドマーク：JRタワー（高さ125mのひときわ高い摩天楼）
            Vector3 jrTowerPos = new Vector3(bounds.center.x + Mathf.Cos(-0.35f) * 440f, -0.3f + 62.5f, bounds.center.z + Mathf.Sin(-0.35f) * 440f);
            var jrTower = Box("JRTower", jrTowerPos, new Vector3(38f, 125f, 38f), Quaternion.Euler(0, 18f, 0), cityFacades[0]);
            TileFacade(jrTower, 38f, 125f);
            Box("JRTowerCrown", jrTowerPos + Vector3.up * 64.5f, new Vector3(32f, 4.0f, 32f), Quaternion.Euler(0, 18f, 0), rm.standMaterial);
            Box("JRTowerAntenna", jrTowerPos + Vector3.up * 72f, new Vector3(1.2f, 12f, 1.2f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);

            for (int i = 0; i < 22; i++)
            {
                float a = -Mathf.PI * 0.42f + (i / 21f) * Mathf.PI * 0.65f;
                float r = 450f + (float)rng.NextDouble() * 120f;
                float bh = 42f + (float)rng.NextDouble() * 65f;
                float bw = 28f + (float)rng.NextDouble() * 32f;
                var p = new Vector3(bounds.center.x + Mathf.Cos(a) * r, -0.3f + bh * 0.5f, bounds.center.z + Mathf.Sin(a) * r);
                var b = Box("DistantCity", p, new Vector3(bw, bh, bw), Quaternion.Euler(0, (i * 37) % 360, 0), cityFacades[i % cityFacades.Length]);
                TileFacade(b, bw, bh);
            }
        }
        else if (!city)
        {
            BuildThemedMountains(rm, def, rng);
        }

        // 大空を彩る立体ローポリ雲クラスター＆ナイターサーチライト
        BuildSkyClouds(rm, def, rng);

        // 所々の旗
        for (int i = 0; i < Count; i += 25)
        {
            float ratio = (float)i / Count;
            if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.05f)
            {
                // 分岐区間内は川や車線との干渉を防ぐため旗をスキップ
                continue;
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var p = PointAtOuterWall(i, side, 1.2f);
                Box("Pole", p + Vector3.up * 2.5f, new Vector3(0.15f, 2.5f, 0.15f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
                var flag = Box("Flag", p + Vector3.up * 4.3f + Dirs[i] * 0.6f, new Vector3(0.05f, 0.8f, 1.2f), Quaternion.LookRotation(Dirs[i]), rm.kartPaintMaterial);
                var mpb = new MaterialPropertyBlock();
                Color flagCol = (def.SceneryTheme == 4)
                    ? ((i / 25 % 2 == 0) ? new Color(0.12f, 0.65f, 0.32f) : new Color(1f, 0.85f, 0.2f))
                    : Color.HSVToRGB((i / 25 % 6) / 6f, 0.8f, 1f);
                mpb.SetColor("_Color", flagCol);
                mpb.SetColor("_BaseColor", flagCol);
                flag.GetComponent<Renderer>().SetPropertyBlock(mpb);
            }
        }
    }

    void BuildSponsorBoards(RaceManager rm, TrackDef def)
    {
        bool isCampus = def.SceneryTheme == 4;
        string dir = Application.dataPath + "/Resources/Banners/";
        var bannerTurbo = isCampus ? null : RaceManager.LoadTexture("Banners/banner_turbo", dir + "banner_turbo.jpg");
        var bannerNitro = isCampus ? null : RaceManager.LoadTexture("Banners/banner_nitro", dir + "banner_nitro.jpg");

        var customBanners = new[] { bannerTurbo, bannerNitro };
        var bannerMats = new Material[4];
        for (int v = 0; v < 4; v++)
        {
            var tex = isCampus ? TextureGen.CampusBanner(v) : ((customBanners[v % 2] != null) ? customBanners[v % 2] : TextureGen.SponsorBanner(v));
            bannerMats[v] = new Material(rm.bannerMaterial ?? rm.wallMaterial)
            {
                mainTexture = tex
            };
            bannerMats[v].SetTexture("_BaseMap", tex);
            bannerMats[v].SetTexture("_MainTex", tex);
            bannerMats[v].color = Color.white;
            bannerMats[v].SetColor("_BaseColor", Color.white);
            createdMaterials.Add(bannerMats[v]);
        }

        var poleMat = isCampus ? new Material(rm.wallMaterial) { color = new Color(0.12f, 0.38f, 0.22f) } : rm.chromeMaterial;
        if (isCampus) createdMaterials.Add(poleMat);
        var frameMat = rm.tireMaterial ?? poleMat;

        // 1. コース壁沿いのスポンサー/キャンパス案内看板（画像のアスペクト比に完全一致＆ドライバー視線に合わせて正対）
        for (int i = 18; i < Count - 15; i += 22)
        {
            float ratio = (float)i / Count;
            if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.08f)
            {
                // 分岐区間内は中央の川や二股車線があり、固定 WallOffset では車線内・川沿いに浮くためスキップ
                // （分岐区間内は専用の「SPLIT SIGN」案内看板や自然の赤岩景観が主役）
                continue;
            }
            float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 12)], Vector3.up);
            float side = bend > 2f ? -1f : 1f; // カーブ外側を優先
            var pos = PointAtOuterWall(i, side, 0.6f) + Vector3.up * 1.9f;
            var mat = bannerMats[(i / 22) % 4];

            var tex = mat.mainTexture;
            float aspect = (tex != null && tex.height > 0) ? (float)tex.width / tex.height : 4f;

            // テクスチャのアスペクト比に合わせて看板オブジェクトの縦横比を完全に一致させる
            // 16:9画像なら高さ1.60m × 幅2.87m、4:1画像なら高さ1.35m × 幅5.40m
            float boardH = (aspect > 2.5f) ? 1.35f : 1.60f;
            float boardW = boardH * aspect;

            // コース内側（走行中のドライバー側）を向く法線
            Vector3 inwardDir = (side > 0) ? -Rights[i] : Rights[i];
            // 手前から接近してくるドライバーに視認しやすいよう、少し斜め手前（進行逆方向）にアングルをつける（約20度）
            Vector3 faceNormal = (inwardDir * 0.94f - Dirs[i] * 0.34f).normalized;
            Quaternion frameRot = Quaternion.LookRotation(faceNormal, Vector3.up);

            // 看板の幅方向（水平方向の接線ベクトル）
            Vector3 boardTangent = Vector3.Cross(Vector3.up, faceNormal);

            // 支柱（看板の上端とぴったり揃い、裏側から地面へ支える構造）
            float poleSpan = boardW * 0.38f;
            float poleH = boardH + 1.2f;
            Vector3 polePos1 = pos - faceNormal * 0.08f - boardTangent * poleSpan;
            Vector3 polePos2 = pos - faceNormal * 0.08f + boardTangent * poleSpan;
            float poleYOffset = -(poleH * 0.5f - boardH * 0.5f);
            polePos1.y += poleYOffset;
            polePos2.y += poleYOffset;
            Box("BoardPole1", polePos1, new Vector3(0.16f, poleH, 0.16f), Quaternion.identity, poleMat, PrimitiveType.Cylinder);
            Box("BoardPole2", polePos2, new Vector3(0.16f, poleH, 0.16f), Quaternion.identity, poleMat, PrimitiveType.Cylinder);

            // 看板のバックフレーム（額縁・裏板）
            Box("BoardFrame", pos, new Vector3(boardW + 0.16f, boardH + 0.16f, 0.12f), frameRot, frameMat);

            // 看板の広告面（Quad）:
            // Quadの法線はローカル -Z なので、LookRotation(-faceNormal, Vector3.up) で表面がドライバーを向き、
            // 左右反転することなく正しく読める
            Quaternion quadRot = Quaternion.LookRotation(-faceNormal, Vector3.up);
            Box("SponsorBoardFace", pos + faceNormal * 0.065f, new Vector3(boardW, boardH, 1f), quadRot, mat, PrimitiveType.Quad);
        }

        // 2. オーバーヘッド・ブリッジ看板（コースをまたぐ大型ゲート 2箇所：横梁全体をカバーする超ワイドバナー）
        Material[] overheadMats = new Material[2];
        var wideTurbo = RaceManager.LoadTexture("Banners/banner_turbo_bridge", dir + "banner_turbo_bridge.png");
        var wideNitro = RaceManager.LoadTexture("Banners/banner_nitro_bridge", dir + "banner_nitro_bridge.png");
        var wideCampus = RaceManager.LoadTexture("Banners/banner_campus_bridge", dir + "banner_campus_bridge.png");

        if (isCampus && wideCampus != null)
        {
            overheadMats[0] = new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = wideCampus };
            overheadMats[0].SetTexture("_BaseMap", wideCampus);
            overheadMats[0].SetTexture("_MainTex", wideCampus);
            overheadMats[0].color = Color.white;
            overheadMats[0].SetColor("_BaseColor", Color.white);
            overheadMats[1] = overheadMats[0];
            createdMaterials.Add(overheadMats[0]);
        }
        else
        {
            if (wideTurbo != null)
            {
                overheadMats[0] = new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = wideTurbo };
                overheadMats[0].SetTexture("_BaseMap", wideTurbo);
                overheadMats[0].SetTexture("_MainTex", wideTurbo);
                overheadMats[0].color = Color.white;
                overheadMats[0].SetColor("_BaseColor", Color.white);
                createdMaterials.Add(overheadMats[0]);
            }
            if (wideNitro != null)
            {
                overheadMats[1] = new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = wideNitro };
                overheadMats[1].SetTexture("_BaseMap", wideNitro);
                overheadMats[1].SetTexture("_MainTex", wideNitro);
                overheadMats[1].color = Color.white;
                overheadMats[1].SetColor("_BaseColor", Color.white);
                createdMaterials.Add(overheadMats[1]);
            }
        }

        int[] bridgeSpots = GetSafeBridgeSpots();
        for (int b = 0; b < bridgeSpots.Length; b++)
        {
            int idx = bridgeSpots[b];
            var center = Pts[idx] + Vector3.up * 7.5f;
            var rot = Quaternion.LookRotation(Dirs[idx]);
            var mat = (overheadMats[b % 2] != null) ? overheadMats[b % 2] : bannerMats[(b + 1) % 4];

            // 左右の巨大支柱
            foreach (float side in new[] { -1f, 1f })
            {
                var pillarPos = PointAt(idx, side * (HalfWidth + 3.0f)) + Vector3.up * 4.0f;
                Box("BridgePillar", pillarPos, new Vector3(1.2f, 8.0f, 1.2f), rot, poleMat);
            }

            // コースを跨ぐ横梁構造体（トラスビームフレーム）
            float beamW = (HalfWidth + 3.0f) * 2f + 1.2f;
            float beamH = 2.4f;
            float beamD = 0.8f;
            Box("BridgeBeam", center, new Vector3(beamW, beamH, beamD), rot, poleMat);
            Box("BridgeBeamRimTop", center + Vector3.up * (beamH * 0.5f + 0.1f), new Vector3(beamW + 0.4f, 0.2f, beamD + 0.2f), rot, rm.chromeMaterial);
            Box("BridgeBeamRimBot", center - Vector3.up * (beamH * 0.5f + 0.1f), new Vector3(beamW + 0.4f, 0.2f, beamD + 0.2f), rot, rm.chromeMaterial);

            // 横梁の端から端までぴったり覆う超ワイド大型広告看板（アスペクト比 9.25:1 に完全一致）
            Box("BridgeAdFaceFront", center - Dirs[idx] * (beamD * 0.5f + 0.04f), new Vector3(beamW, beamH, 1f), rot, mat, PrimitiveType.Quad);
            Box("BridgeAdFaceBack", center + Dirs[idx] * (beamD * 0.5f + 0.04f), new Vector3(beamW, beamH, 1f), rot * Quaternion.Euler(0, 180, 0), mat, PrimitiveType.Quad);
        }
    }

    void BuildCircuitProps(RaceManager rm, TrackDef def)
    {
        var conePrefab = ResourceCache.Load<GameObject>("Props/cone");
        var lightPost = ResourceCache.Load<GameObject>("Props/lightPostLarge");
        var checkersFlag = ResourceCache.Load<GameObject>("Props/flagCheckers");
        var tent = ResourceCache.Load<GameObject>("Props/tent");

        // 1. 照明塔（北大キャンパスは深緑のクラシック街灯、その他はサーキットナイターポール）
        bool isCampus = def.SceneryTheme == 4;
        if (lightPost != null)
        {
            var poleMat = isCampus ? new Material(rm.wallMaterial) { color = new Color(0.14f, 0.22f, 0.16f) } : rm.chromeMaterial;
            if (isCampus) createdMaterials.Add(poleMat);
            for (int i = 0; i < Count; i += Count / 8)
            {
                var pos = PointAtOuterWall(i, 1f, isCampus ? 3.0f : 4.2f);
                pos.y = GetTerrainHeight(pos.x, pos.z, def);
                var rot = Quaternion.LookRotation(-Rights[i], Vector3.up);
                var go = Instantiate(lightPost, pos, rot, transform);
                go.transform.localScale = Vector3.one * (isCampus ? 1.25f : 1.5f);
                StripColliders(go);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = poleMat;
            }
        }

        // 2. コーナーのコーン（パイロン：イン側クリッピングポイント）
        if (conePrefab != null)
        {
            var coneMat = new Material(rm.boostPadMaterial) { color = def.SceneryTheme == 2 ? new Color(0.2f, 0.7f, 1f) : (isCampus ? new Color(1f, 0.78f, 0.12f) : new Color(1f, 0.45f, 0.05f)) };
            createdMaterials.Add(coneMat);
            for (int i = 15; i < Count - 15; i += 14)
            {
                float ratio = (float)i / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.08f)
                {
                    // 分岐区間は中央が川およびガードレール/手すりで完全に分離されているためコーン配置をスキップ
                    continue;
                }
                float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 8)], Vector3.up);
                if (Mathf.Abs(bend) > 10f)
                {
                    float side = bend > 0 ? 1f : -1f;
                    for (int c = 0; c < 2; c++)
                    {
                        var pos = PointAt(Wrap(i + c * 2), side * (HalfWidth - 0.8f));
                        var go = Instantiate(conePrefab, pos, Quaternion.identity, transform);
                        go.transform.localScale = Vector3.one * 1.4f;
                        StripColliders(go);
                        foreach (var r in go.GetComponentsInChildren<Renderer>())
                            r.sharedMaterial = coneMat;
                    }
                }
            }
        }

        // 3. ピットテントと大学旗（スタート付近）
        if (tent != null)
        {
            var tentColor = def.SceneryTheme == 1 ? new Color(0.9f, 0.5f, 0.1f) : (def.SceneryTheme == 2 ? new Color(0.2f, 0.5f, 0.85f) : (isCampus ? new Color(0.10f, 0.44f, 0.24f) : new Color(0.15f, 0.45f, 0.95f)));
            var tentMat = new Material(rm.wallMaterial) { color = tentColor };
            createdMaterials.Add(tentMat);
            for (int t = 1; t <= 3; t++)
            {
                int idx = Wrap(-t * 9);
                var pos = PointAtOuterWall(idx, 1f, 10f); // 壁の上の縁(+0.4m)にテントの屋根が食い込まないよう外側へ
                pos.y = GetTerrainHeight(pos.x, pos.z, def);
                var rot = Quaternion.LookRotation(-Rights[idx], Vector3.up);
                var anchor = new GameObject("TentAnchor").transform;
                anchor.SetParent(transform, false);
                anchor.position = pos;
                anchor.rotation = rot;
                var go = Instantiate(tent, anchor);
                go.transform.localPosition = new Vector3(-5f, 0, -5f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 1.4f;
                StripColliders(anchor.gameObject);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = tentMat;
            }
        }
        if (checkersFlag != null && !isCampus)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var pos = PointAtRoadEdge(0, side, 1.2f);
                var go = Instantiate(checkersFlag, pos, Quaternion.LookRotation(Dirs[0]), transform);
                go.transform.localScale = Vector3.one * 1.6f;
                StripColliders(go);
            }
        }
    }
}
