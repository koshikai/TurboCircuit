using System.Collections.Generic;
using UnityEngine;

// コース別の街並み・キャンパス・高架など大型景観の構築
public partial class Track
{

    // 都市テーマの街並み：道路に沿って摩天楼を敷き詰め、ネオンライト・サイバーツリー・発光サイネージで演出
    void BuildCityScenery(RaceManager rm, TrackDef def, System.Random rng)
    {
        var facade = CityMaterials(rm);
        var placedB = new List<Vector3>(); // (x, z, 半径)
        var antennaMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.2f, 0.25f) };
        var neonCyanMat = new Material(rm.glowMaterial) { color = new Color(0f, 0.95f, 1f, 1f) };
        var neonPinkMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.12f, 0.75f, 1f) };
        var neonYellowMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.92f, 0.15f, 1f) };
        createdMaterials.Add(antennaMat);
        createdMaterials.Add(neonCyanMat);
        createdMaterials.Add(neonPinkMat);
        createdMaterials.Add(neonYellowMat);

        int placed = 0;

        for (int tries = 0; tries < 5000 && placed < 360; tries++)
        {
            float w = 10f + (float)rng.NextDouble() * 14f;
            float d = 10f + (float)rng.NextDouble() * 14f;
            float rad = Mathf.Max(w, d) * 0.72f;
            var pos = new Vector3(
                Mathf.Lerp(bounds.min.x - 110, bounds.max.x + 110, (float)rng.NextDouble()), 0,
                Mathf.Lerp(bounds.min.z - 110, bounds.max.z + 110, (float)rng.NextDouble()));
            if (DistanceToTrack(pos, true) < WallOffset + 7f + rad) continue;
            if (new Vector2(pos.x - Pts[0].x, pos.z - Pts[0].z).magnitude < 75f + rad) continue; // スタート周辺（観客席・テント）を空ける

            bool overlap = false;
            foreach (var o in placedB)
                if (new Vector2(pos.x - o.x, pos.z - o.y).magnitude < (rad + o.z) * 0.9f) { overlap = true; break; }
            if (overlap) continue;
            placedB.Add(new Vector3(pos.x, pos.z, rad));

            // 中心部ほど高く
            float centerDist = new Vector2(pos.x - bounds.center.x, pos.z - bounds.center.z).magnitude;
            float h = 20f + (float)rng.NextDouble() * 45f + Mathf.Clamp01(1f - centerDist / 260f) * 45f;
            var b = Box("Skyscraper", pos + Vector3.up * (h * 0.5f - 0.3f), new Vector3(w, h, d), Quaternion.identity, facade[rng.Next(facade.Length)]);
            TileFacade(b, (w + d) * 0.5f, h);

            if (h > 60f)
            {
                Box("Antenna", pos + Vector3.up * (h + 4f), new Vector3(0.5f, 4f, 0.5f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
                Box("AntennaLight", pos + Vector3.up * (h + 8.3f), Vector3.one * 0.9f, Quaternion.identity, antennaMat, PrimitiveType.Sphere);
            }
            else if (rng.NextDouble() < 0.4)
            {
                // 屋上の設備ブロック
                Box("RoofUnit", pos + Vector3.up * (h + 1.2f), new Vector3(w * 0.45f, 2.4f, d * 0.45f), Quaternion.identity, rm.standMaterial);
            }

            // ビル壁面のネオンサインボード
            if (h > 36f && rng.NextDouble() < 0.65)
            {
                var signMat = (placed % 3 == 0) ? neonCyanMat : ((placed % 3 == 1) ? neonPinkMat : neonYellowMat);
                float signH = Mathf.Min(h * 0.35f, 18f);
                Box("NeonSign", pos + Vector3.up * (h * 0.45f) + new Vector3(0, 0, d * 0.51f), new Vector3(w * 0.65f, signH, 0.4f), Quaternion.identity, signMat);
            }

            placed++;
        }

        // 道路沿いのサイバー街路灯（等間隔に配置）
        var prefabLightPost = Resources.Load<GameObject>("Props/lightPostLarge");
        if (prefabLightPost != null)
        {
            for (int i = 0; i < Count; i += 7)
            {
                float lampOffset = Pts[i].y > 2.0f ? (WallOffset - 0.1f) : (WallOffset + 2.5f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var lpPos = PointAt(i, side * lampOffset);
                    var lpRot = Quaternion.LookRotation(side * Rights[i], Vector3.up);
                    SpawnProp(prefabLightPost, lpPos, lpRot, Vector3.one * 1.5f, rm.standMaterial, (i % 14 == 0) ? neonCyanMat : neonPinkMat);
                }
            }
        }

        // コースサイドのサイバー街路樹（発光ネオンカラーを纏った幻想的な樹木）
        var prefabTree1 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_1");
        var prefabTree2 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_1");
        var texNormalLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Leaves");
        var texBirchLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Leaves");
        var texNormalBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Bark");
        var cyberTrunkMat = MakeMat(rm.standMaterial, texNormalBark, new Color(0.20f, 0.22f, 0.30f));
        var cyberFoliageMat1 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.15f, 0.98f, 1.0f), new Color(0.05f, 0.72f, 0.90f));
        var cyberFoliageMat2 = MakeMat(rm.leavesMaterial, texBirchLeaves ?? texNormalLeaves, new Color(1.0f, 0.35f, 0.92f), new Color(0.88f, 0.14f, 0.68f));

        int placedCyberTrees = 0;
        for (int t = 0; t < 1500 && placedCyberTrees < 95; t++)
        {
            var p = new Vector3(
                Mathf.Lerp(bounds.min.x - 70, bounds.max.x + 70, (float)rng.NextDouble()), 0,
                Mathf.Lerp(bounds.min.z - 70, bounds.max.z + 70, (float)rng.NextDouble()));
            float dist = DistanceToTrack(p);
            if (dist < WallOffset + 4.5f || dist > WallOffset + 38f) continue;
            float s = 2.2f + (float)rng.NextDouble() * 1.3f;
            var tMat = (rng.Next(2) == 0) ? cyberFoliageMat1 : cyberFoliageMat2;
            var treePrefab = (rng.Next(2) == 0) ? prefabTree1 : prefabTree2;
            if (treePrefab != null)
            {
                SpawnNatureProp(treePrefab, p, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * s, cyberTrunkMat, tMat);
                placedCyberTrees++;
            }
        }

        // 遠景のスカイライン
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.1f;
            float r = 430f + (float)rng.NextDouble() * 140f;
            float h = 90f + (float)rng.NextDouble() * 160f;
            float w = 50f + (float)rng.NextDouble() * 60f;
            var p = bounds.center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            var b = Box("Skyline", p + Vector3.up * (h * 0.5f - 0.3f), new Vector3(w, h, w), Quaternion.identity, facade[rng.Next(facade.Length)]);
            TileFacade(b, w, h);
        }
    }

    // 北海道大学札幌キャンパス：クラーク胸像、古河記念講堂、総合博物館、大野池、イチョウ並木、第2農場モデルバーン＆サイロ＆牧草地、ポプラ並木
    void BuildCampusScenery(RaceManager rm, TrackDef def, System.Random rng)
    {
        // 1. 洗練された高品質スタイライズド 3D 自然モデル（Quaternius Stylized Nature Kit, CC0/MIT）
        var prefabMaple1 = Resources.Load<GameObject>("Props/StylizedNature/MapleTree_1");
        var prefabMaple2 = Resources.Load<GameObject>("Props/StylizedNature/MapleTree_2");
        var prefabMaple3 = Resources.Load<GameObject>("Props/StylizedNature/MapleTree_3");
        var prefabBirch1 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_1");
        var prefabBirch2 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_2");
        var prefabBirch3 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_3");
        var prefabNormal1 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_1");
        var prefabNormal2 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_2");
        var prefabPine1 = Resources.Load<GameObject>("Props/StylizedNature/PineTree_1");
        var prefabPine2 = Resources.Load<GameObject>("Props/StylizedNature/PineTree_2");
        var prefabBush = Resources.Load<GameObject>("Props/StylizedNature/Bush");
        var prefabBushFlowers = Resources.Load<GameObject>("Props/StylizedNature/Bush_Flowers");
        var prefabBushLarge = Resources.Load<GameObject>("Props/StylizedNature/Bush_Large");
        var prefabFlower1 = Resources.Load<GameObject>("Props/StylizedNature/Flower_1_Clump");
        var prefabFlower2 = Resources.Load<GameObject>("Props/StylizedNature/Flower_2_Clump");
        var prefabGrassLarge = Resources.Load<GameObject>("Props/StylizedNature/Grass_Large");
        var prefabRock1 = Resources.Load<GameObject>("Props/StylizedNature/Rock_1");
        var prefabRock2 = Resources.Load<GameObject>("Props/StylizedNature/Rock_2");
        var prefabRock3 = Resources.Load<GameObject>("Props/StylizedNature/Rock_3");

        // 建築・小道具モデル
        var prefabFence = Resources.Load<GameObject>("Props/Nature/fence_simple");
        var prefabGate = Resources.Load<GameObject>("Props/Nature/fence_gate");
        var prefabStatueHead = Resources.Load<GameObject>("Props/Nature/statue_head");
        var prefabStatueColumn = Resources.Load<GameObject>("Props/Nature/statue_column");
        var prefabStatueBlock = Resources.Load<GameObject>("Props/Nature/statue_block");
        var prefabLilyLarge = Resources.Load<GameObject>("Props/Nature/lily_large");
        var prefabLilySmall = Resources.Load<GameObject>("Props/Nature/lily_small");
        var prefabBridge = Resources.Load<GameObject>("Props/Nature/bridge_wood");
        var prefabSign = Resources.Load<GameObject>("Props/Nature/sign");
        var prefabLogStack = Resources.Load<GameObject>("Props/Nature/log_stack");
        var prefabStump = Resources.Load<GameObject>("Props/Nature/stump_old");
        var prefabLampPost = Resources.Load<GameObject>("Props/lightPostLarge");

        // 2. 高解像度スタイライズド・テクスチャの読み込み
        var texBirchBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Bark");
        var texBirchLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Leaves");
        var texMapleBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/MapleTree_Bark");
        var texMapleLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/MapleTree_Leaves");
        var texNormalBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Bark");
        var texNormalLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Leaves");
        var texPineBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Bark");
        var texPineLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Leaves");
        var texBushLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Bush_Leaves");
        var texFlowers = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Flowers");
        var texRocks = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks");


        // 専用マテリアルの初期化
        var woodWhiteMat = MakeMat(rm.wallMaterial, TextureGen.BarnWoodWhite(), Color.white);
        var roofGreenMat = MakeMat(rm.wallMaterial, null, new Color(0.12f, 0.52f, 0.36f));
        var brickMuseumMat = MakeMat(rm.wallMaterial, TextureGen.RedBrick(), Color.white);
        var siloRoofMat = MakeMat(rm.standMaterial, null, new Color(0.35f, 0.38f, 0.42f));
        var bronzeMat = MakeMat(rm.standMaterial, null, new Color(0.24f, 0.35f, 0.28f));
        var pedestalMat = MakeMat(rm.standMaterial, null, new Color(0.78f, 0.77f, 0.75f));

        // 樹木マテリアル（手描きテクスチャ＋北海道の黄金イチョウ・緑豊かなエルム＆ポプラ）
        var ginkgoTrunkMat = MakeMat(rm.trunkMaterial, texMapleBark, Color.white);
        var ginkgoLeafMatA = MakeMat(rm.leavesMaterial, texBirchLeaves ?? TextureGen.GinkgoFoliage(Color.yellow), new Color(1.0f, 0.95f, 0.25f), new Color(0.32f, 0.24f, 0.02f));
        var ginkgoLeafMatB = MakeMat(rm.leavesMaterial, texBirchLeaves ?? TextureGen.GinkgoFoliage(Color.yellow), new Color(1.0f, 0.82f, 0.12f), new Color(0.28f, 0.18f, 0.01f));
        var ginkgoCarpetMat = MakeMat(rm.leavesMaterial, TextureGen.GinkgoCarpet(), Color.white, new Color(0.18f, 0.14f, 0.02f));

        var elmTrunkMat = MakeMat(rm.trunkMaterial, texNormalBark, Color.white);
        var elmLeafMat = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.85f, 1.0f, 0.75f), new Color(0.06f, 0.14f, 0.04f));

        var birchTrunkMat = MakeMat(rm.wallMaterial, texBirchBark ?? TextureGen.BirchBark(), Color.white);
        var birchLeafMat = MakeMat(rm.leavesMaterial, texNormalLeaves ?? texBirchLeaves, new Color(0.65f, 0.98f, 0.55f), new Color(0.06f, 0.14f, 0.04f));

        var pineTrunkMat = MakeMat(rm.trunkMaterial, texPineBark, Color.white);
        var pineLeafMat = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.75f, 0.95f, 0.75f), new Color(0.04f, 0.10f, 0.05f));

        var poplarTrunkMat = MakeMat(rm.trunkMaterial, texBirchBark ?? texNormalBark, Color.white);
        var poplarLeafMat = MakeMat(rm.leavesMaterial, texNormalLeaves ?? texBirchLeaves, new Color(0.58f, 0.94f, 0.48f), new Color(0.06f, 0.14f, 0.04f));

        var waterMat = MakeMat(rm.boostPadMaterial, null, new Color(0.12f, 0.48f, 0.55f, 0.88f));
        var hayMat = MakeMat(rm.trunkMaterial, null, new Color(0.85f, 0.75f, 0.35f));
        var flowerMat = MakeMat(rm.leavesMaterial, texFlowers, Color.white);
        var bushLeafMat = MakeMat(rm.leavesMaterial, texBushLeaves, Color.white, new Color(0.04f, 0.10f, 0.03f));
        var rockMat = MakeMat(rm.standMaterial, texRocks, Color.white);
        var cowWhiteMat = MakeMat(rm.wallMaterial, null, new Color(0.96f, 0.96f, 0.94f));

        // 1. クラーク博士胸像（William S. Clark Memorial Statue）- 中央ローン広場
        int clarkIdx = (int)(0.12f * Count);
        Vector3 clarkPos = PointAt(clarkIdx, -(WallOffset + 12f));
        clarkPos.y = GetTerrainHeight(clarkPos.x, clarkPos.z, def);
        Quaternion clarkRot = Quaternion.LookRotation(Rights[clarkIdx], Vector3.up);

        if (prefabStatueBlock != null)
        {
            SpawnProp(prefabStatueBlock, clarkPos + Vector3.up * 0.2f, clarkRot, new Vector3(3.2f, 0.8f, 3.2f), pedestalMat, pedestalMat);
            SpawnProp(prefabStatueBlock, clarkPos + Vector3.up * 0.9f, clarkRot, new Vector3(2.4f, 0.8f, 2.4f), pedestalMat, pedestalMat);
        }
        else
        {
            Box("ClarkPedestal1", clarkPos + Vector3.up * 0.4f, new Vector3(3.6f, 0.8f, 3.2f), clarkRot, pedestalMat);
            Box("ClarkPedestal2", clarkPos + Vector3.up * 1.5f, new Vector3(2.6f, 1.4f, 2.2f), clarkRot, pedestalMat);
        }
        if (prefabStatueColumn != null)
            SpawnProp(prefabStatueColumn, clarkPos + Vector3.up * 1.5f, clarkRot, new Vector3(1.6f, 1.6f, 1.6f), pedestalMat, pedestalMat);

        Box("ClarkPlaque", clarkPos + clarkRot * new Vector3(0, 1.6f, 1.25f), new Vector3(1.9f, 0.65f, 0.12f), clarkRot, bronzeMat);

        Vector3 bustBase = clarkPos + Vector3.up * 2.8f;
        if (prefabStatueHead != null)
            SpawnProp(prefabStatueHead, bustBase + Vector3.up * 0.2f, clarkRot, Vector3.one * 2.2f, bronzeMat, bronzeMat);
        else
        {
            Box("ClarkTorso", bustBase + Vector3.up * 0.7f, new Vector3(1.4f, 1.2f, 0.9f), clarkRot, bronzeMat);
            Box("ClarkHead", bustBase + Vector3.up * 1.7f, new Vector3(0.75f, 0.85f, 0.75f), clarkRot, bronzeMat, PrimitiveType.Sphere);
        }
        Vector3 armBase = bustBase + clarkRot * new Vector3(0.7f, 1.0f, 0.3f);
        Box("ClarkArm", armBase, new Vector3(0.35f, 0.35f, 1.5f), clarkRot * Quaternion.Euler(-18f, 28f, 0f), bronzeMat, PrimitiveType.Cylinder);

        // クラーク像を扇状に囲む花壇（Quaternius Flower Clumps & Bush Flowers）
        for (int f = -3; f <= 3; f++)
        {
            float fAng = f * 22f;
            Quaternion fRot = clarkRot * Quaternion.Euler(0, fAng, 0);
            Vector3 fPos = clarkPos + fRot * new Vector3(0, 0, 4.2f);
            fPos.y = GetTerrainHeight(fPos.x, fPos.z, def);
            var fPrefab = (Mathf.Abs(f) % 2 == 0) ? (prefabFlower1 ?? prefabFlower2) : (prefabBushFlowers ?? prefabBush);
            if (fPrefab != null)
                SpawnProp(fPrefab, fPos, Quaternion.Euler(0, f * 55, 0), Vector3.one * 1.5f, bushLeafMat, flowerMat);
        }
        Vector3 clarkBushL = clarkPos + clarkRot * new Vector3(-3.8f, 0, 2.5f);
        clarkBushL.y = GetTerrainHeight(clarkBushL.x, clarkBushL.z, def);
        Vector3 clarkBushR = clarkPos + clarkRot * new Vector3(3.8f, 0, 2.5f);
        clarkBushR.y = GetTerrainHeight(clarkBushR.x, clarkBushR.z, def);
        SpawnProp(prefabBushFlowers ?? prefabBush, clarkBushL, Quaternion.identity, Vector3.one * 1.8f, bushLeafMat, flowerMat);
        SpawnProp(prefabBushFlowers ?? prefabBush, clarkBushR, Quaternion.identity, Vector3.one * 1.8f, bushLeafMat, flowerMat);

        // クラーク像を取り囲む雄大なエルムの大樹（Quaternius NormalTree）
        Vector3 clarkElmL = clarkPos + clarkRot * new Vector3(-10f, 0, -5f);
        clarkElmL.y = GetTerrainHeight(clarkElmL.x, clarkElmL.z, def);
        Vector3 clarkElmR = clarkPos + clarkRot * new Vector3(10f, 0, -5f);
        clarkElmR.y = GetTerrainHeight(clarkElmR.x, clarkElmR.z, def);
        SpawnNatureProp(prefabNormal1 ?? prefabNormal2, clarkElmL, Quaternion.Euler(0, 45, 0), Vector3.one * 4.2f, elmTrunkMat, elmLeafMat);
        SpawnNatureProp(prefabNormal2 ?? prefabNormal1, clarkElmR, Quaternion.Euler(0, 120, 0), Vector3.one * 4.0f, elmTrunkMat, elmLeafMat);

        // 2. 古河記念講堂（Furukawa Memorial Hall）- アメリカン・ルネサンス様式の木造洋風校舎
        int furukawaIdx = (int)(0.155f * Count);
        Vector3 furukawaPos = PointAt(furukawaIdx, -(WallOffset + 24f));
        furukawaPos.y = GetTerrainHeight(furukawaPos.x, furukawaPos.z, def);
        Quaternion furukawaRot = Quaternion.LookRotation(Rights[furukawaIdx], Vector3.up);
        Box("FurukawaBase", furukawaPos + Vector3.up * 5.5f, new Vector3(36f, 11f, 18f), furukawaRot, woodWhiteMat);
        Box("FurukawaPorch", furukawaPos + furukawaRot * new Vector3(0, 6f, 10f), new Vector3(10f, 12f, 3.5f), furukawaRot, woodWhiteMat);
        Box("FurukawaRoofLower", furukawaPos + Vector3.up * 12f, new Vector3(37f, 4f, 19f), furukawaRot, roofGreenMat);
        Box("FurukawaRoofUpper", furukawaPos + Vector3.up * 14.5f, new Vector3(28f, 2.5f, 13f), furukawaRot, roofGreenMat);
        Vector3 cupolaPos = furukawaPos + Vector3.up * 16.5f;
        Box("FurukawaCupolaBase", cupolaPos, new Vector3(4.0f, 4.0f, 4.0f), furukawaRot, woodWhiteMat, PrimitiveType.Cylinder);
        Box("FurukawaCupolaRoof", cupolaPos + Vector3.up * 3.2f, new Vector3(4.4f, 3.6f, 4.4f), furukawaRot, roofGreenMat, PrimitiveType.Cylinder);
        for (int b = -3; b <= 3; b += 2)
        {
            Vector3 fBushPos = furukawaPos + furukawaRot * new Vector3(b * 4.5f, 0, 11f);
            fBushPos.y = GetTerrainHeight(fBushPos.x, fBushPos.z, def);
            SpawnProp((b % 4 == 0 ? prefabBushFlowers : prefabBushLarge) ?? prefabBush, fBushPos, Quaternion.identity, Vector3.one * 1.6f, bushLeafMat, flowerMat);
        }

        // 3. 北大総合博物館（HU Museum）- 東側（外側）の赤レンガ重厚校舎
        int museumIdx = (int)(0.22f * Count);
        Vector3 museumPos = PointAt(museumIdx, WallOffset + 26f);
        museumPos.y = GetTerrainHeight(museumPos.x, museumPos.z, def);
        Quaternion museumRot = Quaternion.LookRotation(-Rights[museumIdx], Vector3.up);
        Box("MuseumWingL", museumPos + museumRot * new Vector3(-19f, 7f, 0), new Vector3(19f, 14f, 17f), museumRot, brickMuseumMat);
        Box("MuseumCenter", museumPos + Vector3.up * 9.5f, new Vector3(20f, 19f, 21f), museumRot, brickMuseumMat);
        Box("MuseumWingR", museumPos + museumRot * new Vector3(19f, 7f, 0), new Vector3(19f, 14f, 17f), museumRot, brickMuseumMat);
        Box("MuseumTower", museumPos + Vector3.up * 20.5f, new Vector3(8.5f, 5.0f, 8.5f), museumRot, brickMuseumMat);
        // エントランス前の白樺（Quaternius BirchTree）
        Vector3 musBirchL = museumPos + museumRot * new Vector3(-8f, 0, 13f);
        musBirchL.y = GetTerrainHeight(musBirchL.x, musBirchL.z, def);
        Vector3 musBirchR = museumPos + museumRot * new Vector3(8f, 0, 13f);
        musBirchR.y = GetTerrainHeight(musBirchR.x, musBirchR.z, def);
        SpawnNatureProp(prefabBirch1 ?? prefabBirch2, musBirchL, Quaternion.identity, Vector3.one * 3.2f, birchTrunkMat, birchLeafMat);
        SpawnNatureProp(prefabBirch2 ?? prefabBirch1, musBirchR, Quaternion.identity, Vector3.one * 3.2f, birchTrunkMat, birchLeafMat);

        // 4. 大野池（Ono Pond）- 西側（内側）の睡蓮の池、木造アーチ橋、自然岩
        Vector3 pondPos = PointAt(museumIdx, -(WallOffset + 18f));
        pondPos.y = GetTerrainHeight(pondPos.x, pondPos.z, def) + 0.05f;
        Box("OnoPondWater", pondPos + Vector3.up * 0.08f, new Vector3(38f, 26f, 1f), Quaternion.Euler(90, 25, 0), waterMat, PrimitiveType.Quad);
        for (int p = 0; p < 14; p++)
        {
            float rad = 3.5f + (p % 5) * 2.5f;
            float ang = p * 0.75f + (p * 0.3f);
            Vector3 padPos = pondPos + new Vector3(Mathf.Cos(ang) * rad, 0.12f, Mathf.Sin(ang) * rad);
            var lPrefab = (p % 2 == 0) ? prefabLilyLarge : prefabLilySmall;
            if (lPrefab != null)
                SpawnProp(lPrefab, padPos, Quaternion.Euler(0, p * 47f, 0), Vector3.one * 1.5f, bushLeafMat, flowerMat);
        }
        if (prefabBridge != null)
            SpawnProp(prefabBridge, pondPos + new Vector3(2f, 0.3f, 0f), Quaternion.Euler(0, 30f, 0), new Vector3(2.4f, 2.0f, 2.2f), rm.trunkMaterial, rm.wallMaterial);

        // 池の周囲を取り囲むスタイライズド自然岩（Quaternius Rock_1..3）
        for (int r = 0; r < 8; r++)
        {
            float rAng = r * 0.8f;
            Vector3 rPos = pondPos + new Vector3(Mathf.Cos(rAng) * 15f, 0, Mathf.Sin(rAng) * 10f);
            rPos.y = GetTerrainHeight(rPos.x, rPos.z, def);
            var rPrefab = (r % 3 == 0) ? prefabRock1 : ((r % 3 == 1) ? prefabRock2 : prefabRock3);
            if (rPrefab != null)
                SpawnProp(rPrefab, rPos, Quaternion.Euler(0, r * 45f, 0), Vector3.one * (1.8f + (r % 3) * 0.4f), rockMat, rockMat);
        }
        Vector3 pondBenchPos = pondPos + new Vector3(16f, 0f, -4f);
        pondBenchPos.y = GetTerrainHeight(pondBenchPos.x, pondBenchPos.z, def) + 0.4f;
        Box("PondBench", pondBenchPos, new Vector3(2.6f, 0.8f, 0.8f), Quaternion.Euler(0, -20f, 0), rm.trunkMaterial);
        Vector3 pondElmPos = pondPos + new Vector3(-15f, 0, 9f);
        pondElmPos.y = GetTerrainHeight(pondElmPos.x, pondElmPos.z, def);
        SpawnNatureProp(prefabNormal1 ?? prefabNormal2, pondElmPos, Quaternion.identity, Vector3.one * 4.0f, elmTrunkMat, elmLeafMat);

        // 5. 北13条イチョウ並木（Ginkgo Avenue）- 東へ伸びる壮大な黄金のアーチトンネル！（Quaternius MapleTree）
        int ginkgoStart = (int)(0.26f * Count);
        int ginkgoEnd = (int)(0.39f * Count);
        for (int i = ginkgoStart; i <= ginkgoEnd; i += 3)
        {
            float s = 4.2f + (i % 3) * 0.4f;
            var gMat = (i % 2 == 0) ? ginkgoLeafMatA : ginkgoLeafMatB;
            var gTreePrefab = (i % 3 == 0) ? prefabMaple1 : ((i % 3 == 1) ? prefabMaple2 : prefabMaple3);
            foreach (float side in new[] { -1f, 1f })
            {
                // 内側列（道路沿い）：頭上を覆うように道路側へ傾斜（黄金のアーチキャノピー）
                Vector3 treePos1 = PointAt(i, side * (WallOffset + 4.2f));
                treePos1.y = GetTerrainHeight(treePos1.x, treePos1.z, def);
                Quaternion leanRot = Quaternion.AngleAxis(-side * 14f, Dirs[i]) * Quaternion.Euler(0, (i * 37) % 360, 0);
                SpawnNatureProp(gTreePrefab ?? prefabNormal1, treePos1, leanRot, Vector3.one * s, ginkgoTrunkMat, gMat);

                // 外側列（並木の厚み・二重列）
                if (i % 5 == 0)
                {
                    Vector3 treePos2 = PointAt(i, side * (WallOffset + 8.8f));
                    treePos2.y = GetTerrainHeight(treePos2.x, treePos2.z, def);
                    Quaternion rot2 = Quaternion.Euler(0, (i * 53) % 360, 0);
                    var gTree2 = (i % 2 == 0) ? prefabMaple2 : prefabMaple3;
                    SpawnNatureProp(gTree2 ?? prefabNormal2, treePos2, rot2, Vector3.one * (s * 1.15f), ginkgoTrunkMat, (i % 4 == 0) ? ginkgoLeafMatB : ginkgoLeafMatA);
                }

                // イチョウの足元の草花（ふんわり咲く花と黄金の草むら）
                if (i % 6 == 0)
                {
                    Vector3 fPos = PointAt(i, side * (WallOffset + 2.4f));
                    fPos.y = GetTerrainHeight(fPos.x, fPos.z, def);
                    if (prefabFlower1 != null)
                        SpawnProp(prefabFlower1, fPos, Quaternion.Euler(0, i * 29, 0), Vector3.one * 1.4f, bushLeafMat, flowerMat);
                    if (prefabGrassLarge != null)
                    {
                        Vector3 gPos = fPos + Vector3.forward * 1.2f;
                        gPos.y = GetTerrainHeight(gPos.x, gPos.z, def);
                        SpawnProp(prefabGrassLarge, gPos, Quaternion.identity, Vector3.one * 1.4f, ginkgoCarpetMat);
                    }
                }
            }
        }

        // 6. メインストリート北セクション（白樺 & エルム並木 & エゾマツ）
        int mainNorthStart = (int)(0.40f * Count);
        int mainNorthEnd = (int)(0.53f * Count);
        for (int i = mainNorthStart; i <= mainNorthEnd; i += 7)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 treePos = PointAt(i, side * (WallOffset + 5.0f));
                treePos.y = GetTerrainHeight(treePos.x, treePos.z, def);
                Quaternion rot = Quaternion.Euler(0, (i * 43) % 360, 0);
                if (i % 3 == 0) // 白樺（Quaternius BirchTree）
                    SpawnNatureProp(prefabBirch1 ?? prefabBirch2, treePos, rot, Vector3.one * 3.4f, birchTrunkMat, birchLeafMat);
                else if (i % 3 == 1) // エゾマツ（Quaternius PineTree）
                    SpawnNatureProp(prefabPine1 ?? prefabPine2, treePos, rot, Vector3.one * 3.6f, pineTrunkMat, pineLeafMat);
                else // エルム（Quaternius NormalTree）
                    SpawnNatureProp(prefabNormal1 ?? prefabNormal2, treePos, rot, Vector3.one * 3.8f, elmTrunkMat, elmLeafMat);
            }
        }

        // 7. 第2農場モデルバーン＆サイロ＆牧草地（Historic Model Barn & Silo & Pasture）
        int barnIdx = (int)(0.66f * Count);
        Vector3 siloPos = PointAt(barnIdx, WallOffset + 19f);
        siloPos.y = GetTerrainHeight(siloPos.x, siloPos.z, def);
        Box("BarnSiloBody", siloPos + Vector3.up * 8.5f, new Vector3(8.0f, 17f, 8.0f), Quaternion.identity, woodWhiteMat, PrimitiveType.Cylinder);
        Box("BarnSiloCap", siloPos + Vector3.up * 18.5f, new Vector3(8.6f, 4.5f, 8.6f), Quaternion.identity, siloRoofMat, PrimitiveType.Cylinder);
        for (int h = 0; h < 6; h++)
        {
            Vector3 hayPos = siloPos + new Vector3(-9f + h * 3.2f, 0f, 9f);
            hayPos.y = GetTerrainHeight(hayPos.x, hayPos.z, def) + 1.2f;
            Box("HayBale", hayPos, new Vector3(2.3f, 2.1f, 2.3f), Quaternion.Euler(0, h * 30f, 90f), hayMat, PrimitiveType.Cylinder);
        }
        if (prefabLogStack != null)
        {
            Vector3 logPos = siloPos + new Vector3(10f, 0, -4f);
            logPos.y = GetTerrainHeight(logPos.x, logPos.z, def);
            SpawnProp(prefabLogStack, logPos, Quaternion.Euler(0, 45, 0), Vector3.one * 2.2f, rm.trunkMaterial);
        }
        if (prefabStump != null)
        {
            Vector3 stumpPos = siloPos + new Vector3(8f, 0, 6f);
            stumpPos.y = GetTerrainHeight(stumpPos.x, stumpPos.z, def);
            SpawnProp(prefabStump, stumpPos, Quaternion.identity, Vector3.one * 2.0f, rm.trunkMaterial);
        }

        // 牧草地の木柵モデルと放牧ホルスタイン牛
        int pastureStart = (int)(0.68f * Count);
        int pastureEnd = (int)(0.76f * Count);
        for (int i = pastureStart; i <= pastureEnd; i += 6)
        {
            Vector3 fencePos = PointAt(i, -(WallOffset + 5.5f));
            fencePos.y = GetTerrainHeight(fencePos.x, fencePos.z, def);
            Quaternion fRot = Quaternion.LookRotation(Dirs[i], Vector3.up);
            if (i == pastureStart + 6 && prefabGate != null)
                SpawnProp(prefabGate, fencePos, fRot, new Vector3(2.2f, 1.8f, 2.2f), woodWhiteMat, woodWhiteMat);
            else if (prefabFence != null)
                SpawnNatureProp(prefabFence, fencePos, fRot, new Vector3(2.2f, 1.6f, 2.2f), woodWhiteMat, woodWhiteMat);
        }

        // 8. ポプラ並木通り（Poplar Avenue）- 西側高速ストレートに沿った天を突く直立ポプラ！（Quaternius BirchTree_3 / NormalTree_2 縦長プロポーション）
        int poplarStart = (int)(0.77f * Count);
        int poplarEnd = (int)(0.91f * Count);
        for (int i = poplarStart; i <= poplarEnd; i += 4)
        {
            float s = 3.2f + (i % 3) * 0.35f;
            var popPrefab = (i % 2 == 0) ? prefabBirch3 : prefabNormal2;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 pPos = PointAt(i, side * (WallOffset + 4.5f));
                pPos.y = GetTerrainHeight(pPos.x, pPos.z, def);
                Quaternion rot = Quaternion.Euler(0, (i * 29) % 360, 0);
                SpawnNatureProp(popPrefab ?? prefabBirch1, pPos, rot, new Vector3(s * 0.65f, s * 1.85f, s * 0.65f), poplarTrunkMat, poplarLeafMat);
                if (prefabGrassLarge != null && i % 8 == 0)
                {
                    Vector3 gPos = pPos + Vector3.forward * 1.5f;
                    gPos.y = GetTerrainHeight(gPos.x, gPos.z, def);
                    SpawnProp(prefabGrassLarge, gPos, Quaternion.identity, Vector3.one * 1.5f, bushLeafMat);
                }
            }
        }

        // 9. メインストリート沿いのクラシック街灯（Street Lamps）と木造案内看板（Signs）
        for (int i = 0; i < Count; i += 12)
        {
            if (i >= ginkgoStart && i <= ginkgoEnd) continue;
            Vector3 lampPos = PointAt(i, WallOffset + 2.4f);
            lampPos.y = GetTerrainHeight(lampPos.x, lampPos.z, def);
            Quaternion lRot = Quaternion.LookRotation(Dirs[i], Vector3.up);
            if (prefabLampPost != null)
                SpawnProp(prefabLampPost, lampPos, lRot, Vector3.one * 1.2f, rm.standMaterial, rm.glowMaterial);

            if (i % 36 == 0 && prefabSign != null)
            {
                Vector3 sPos = PointAt(i, -(WallOffset + 2.6f));
                sPos.y = GetTerrainHeight(sPos.x, sPos.z, def);
                SpawnProp(prefabSign, sPos, Quaternion.LookRotation(-Rights[i], Vector3.up), Vector3.one * 1.8f, rm.trunkMaterial);
            }
        }

        // 10. コース全域の路肩を彩るスタイライズド草花・低木・小岩（Quaternius assets）
        for (int i = 0; i < Count; i += 3)
        {
            float side = (i % 2 == 0) ? 1f : -1f;
            float offset = WallOffset + 2.5f + (float)rng.NextDouble() * 5.0f;
            Vector3 pos = PointAt(i, side * offset);
            pos.y = GetTerrainHeight(pos.x, pos.z, def);
            int itemType = rng.Next(5);
            if (itemType == 0 && prefabGrassLarge != null)
                SpawnProp(prefabGrassLarge, pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * (1.1f + (float)rng.NextDouble() * 0.6f), bushLeafMat);
            else if (itemType == 1 && prefabFlower1 != null)
                SpawnProp(prefabFlower1, pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * (1.2f + (float)rng.NextDouble() * 0.6f), bushLeafMat, flowerMat);
            else if (itemType == 2 && prefabBush != null)
                SpawnProp(prefabBush, pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * (1.2f + (float)rng.NextDouble() * 0.6f), bushLeafMat);
            else if (itemType == 3 && prefabRock1 != null)
                SpawnProp(prefabRock1, pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * (1.2f + (float)rng.NextDouble() * 0.6f), rockMat);
            else if (prefabBushFlowers != null)
                SpawnProp(prefabBushFlowers, pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one * (1.2f + (float)rng.NextDouble() * 0.6f), bushLeafMat, flowerMat);
        }

        // 11. キャンパス全域を取り囲む広大なスタイライズド森林（白樺・春楡・エゾマツ針葉樹林 約280本）
        int placedTrees = 0;
        for (int tries = 0; tries < 4000 && placedTrees < 280; tries++)
        {
            var pos = new Vector3(
                Mathf.Lerp(bounds.min.x - 120, bounds.max.x + 120, (float)rng.NextDouble()), 0,
                Mathf.Lerp(bounds.min.z - 120, bounds.max.z + 120, (float)rng.NextDouble()));
            pos.y = GetTerrainHeight(pos.x, pos.z, def);
            if (DistanceToTrack(pos) < WallOffset + 4.5f) continue;
            float s = 2.8f + (float)rng.NextDouble() * 1.5f;
            Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            double roll = rng.NextDouble();
            if (roll < 0.35) // 白樺（Quaternius BirchTree）
            {
                var bTree = (rng.Next(2) == 0) ? prefabBirch1 : prefabBirch2;
                SpawnNatureProp(bTree ?? prefabNormal1, pos, rot, Vector3.one * s, birchTrunkMat, birchLeafMat);
            }
            else if (roll < 0.65) // エゾマツ（Quaternius PineTree）
            {
                var pTree = (rng.Next(2) == 0) ? prefabPine1 : prefabPine2;
                SpawnNatureProp(pTree ?? prefabNormal1, pos, rot, Vector3.one * (s * 1.1f), pineTrunkMat, pineLeafMat);
            }
            else // エルムの大樹（Quaternius NormalTree）
            {
                var eTree = (rng.Next(2) == 0) ? prefabNormal1 : prefabNormal2;
                SpawnNatureProp(eTree ?? prefabBirch1, pos, rot, Vector3.one * (s * 1.15f), elmTrunkMat, elmLeafMat);
            }
            placedTrees++;
        }
    }

    // スカイライン・オーバーパス（立体交差・海岸ベイエリア・斜張橋）の景観構築
    void BuildSkylineOverpassScenery(RaceManager rm, TrackDef def, System.Random rng)
    {
        // 1. 海岸ベイウォーター（碧く輝く水面プレーン）
        var waterMat = new Material(rm.wallMaterial)
        {
            color = new Color(0.08f, 0.42f, 0.65f, 0.92f)
        };
        createdMaterials.Add(waterMat);
        var sea = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(sea.GetComponent<Collider>());
        sea.name = "BayWaterSurface";
        sea.transform.SetParent(transform, false);
        sea.transform.position = new Vector3(bounds.center.x, -0.65f, bounds.center.z);
        sea.transform.rotation = Quaternion.Euler(90, 0, 0);
        sea.transform.localScale = new Vector3(2200, 2200, 1);
        sea.GetComponent<Renderer>().sharedMaterial = waterMat;

        // 2. 立体交差・巨大斜張橋（Cable-Stayed Bridge）ツインタワー（North: Z=75, South: Z=5）
        // 下層道路（Z=40）を跨ぐため、主塔はZ=75とZ=5の2箇所に配置し、中央クリアランスを完全確保
        int overpassIdx = 604;
        if (Count > 0)
        {
            float bestCrossingDist = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (Pts[i].y > 10f)
                {
                    float d = Mathf.Abs(Pts[i].x) + Mathf.Abs(Pts[i].z - 40f);
                    if (d < bestCrossingDist) { bestCrossingDist = d; overpassIdx = i; }
                }
            }
        }

        Vector3 bridgeCenter = Pts[overpassIdx];
        Vector3 bridgeDir = Dirs[overpassIdx];
        Vector3 bridgeRight = Rights[overpassIdx];
        var bridgeRot = Quaternion.LookRotation(bridgeDir, Vector3.up);

        var towerWhiteMat = rm.standMaterial;
        var pylonRedMat = new Material(rm.wallMaterial) { color = new Color(0.95f, 0.25f, 0.2f) };
        var cableMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.88f, 0.35f) };
        var beaconMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.15f, 0.15f) };
        var signMat = new Material(rm.glowMaterial) { color = new Color(0.05f, 0.95f, 1f) };
        var palmTrunkM = MakeMat(rm.trunkMaterial, null, new Color(0.42f, 0.30f, 0.20f));
        var palmLeafM = MakeMat(rm.leavesMaterial, null, new Color(0.18f, 0.65f, 0.28f));
        createdMaterials.Add(pylonRedMat);
        createdMaterials.Add(cableMat);
        createdMaterials.Add(beaconMat);
        createdMaterials.Add(signMat);

        float pylonHeight = 44f;
        float pylonOffset = WallOffset + 3.8f;

        // ツインタワー主塔（北側タワー: cStep ≈ -18, 南側タワー: cStep ≈ +18）
        int[] towerOffsets = { -18, 18 };
        foreach (int tOff in towerOffsets)
        {
            int tIdx = Wrap(overpassIdx + tOff);
            Vector3 tCenter = Pts[tIdx];
            Vector3 tDir = Dirs[tIdx];
            Vector3 tRight = Rights[tIdx];
            var tRot = Quaternion.LookRotation(tDir, Vector3.up);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pylonBase = tCenter + tRight * (side * pylonOffset);
                pylonBase.y = -0.35f;

                // コンクリート台座
                Box("PylonBase_" + tOff + "_" + side, pylonBase + Vector3.up * 2.5f, new Vector3(4.5f, 5f, 7.5f), tRot, towerWhiteMat);
                // 主塔ピラー（地上から上空44mへ直立）
                Box("PylonPillar_" + tOff + "_" + side, pylonBase + Vector3.up * (pylonHeight * 0.5f), new Vector3(2.8f, pylonHeight, 3.8f), tRot, towerWhiteMat);
                // 塔上部の赤白ストライプ装飾帯
                Box("PylonStripe_" + tOff + "_" + side, pylonBase + Vector3.up * (pylonHeight - 4f), new Vector3(2.9f, 4f, 3.9f), tRot, pylonRedMat);

                // 塔頂の赤い航空障害灯（ビーコン）
                var beacon = Box("PylonBeacon_" + tOff + "_" + side, pylonBase + Vector3.up * (pylonHeight + 1.2f), Vector3.one * 1.8f, Quaternion.identity, beaconMat, PrimitiveType.Sphere);
                var bLight = beacon.AddComponent<Light>();
                bLight.type = LightType.Point;
                bLight.color = new Color(1f, 0.2f, 0.2f);
                bLight.range = 35f;
                bLight.intensity = 3.5f;

                // 斜張ステイケーブル（塔頂付近から前後の橋桁へ放射状に伸びる）
                Vector3 cableAnchorTop = pylonBase + Vector3.up * (pylonHeight - 6f);
                int[] cableSpans = { -24, -16, -8, 8, 16, 24 };
                foreach (int cSpan in cableSpans)
                {
                    int cIdx = Wrap(tIdx + cSpan);
                    Vector3 deckAnchor = Pts[cIdx] + Rights[cIdx] * (side * (WallOffset - 0.2f));
                    Vector3 cableVec = deckAnchor - cableAnchorTop;
                    float cLen = cableVec.magnitude;
                    Vector3 cMid = (cableAnchorTop + deckAnchor) * 0.5f;
                    var cRot = Quaternion.LookRotation(cableVec.normalized, Vector3.up);
                    Box("StayCable_" + tOff + "_" + side + "_" + cSpan, cMid, new Vector3(0.22f, 0.22f, cLen), cRot, cableMat);
                    Box("CableNode_" + tOff + "_" + side + "_" + cSpan, deckAnchor + Vector3.up * 0.4f, Vector3.one * 0.5f, Quaternion.identity, cableMat, PrimitiveType.Sphere);
                }
            }

            // 主塔同士を上空で繋ぐトラスビーム（Y=36m）
            Vector3 topBeamPos = new Vector3(tCenter.x, -0.35f + pylonHeight - 8f, tCenter.z);
            Box("PylonCrossBeam_" + tOff, topBeamPos, new Vector3((pylonOffset * 2f) + 3f, 2.4f, 3.6f), tRot, towerWhiteMat);
        }

        // 高架橋アプローチの頭上サインボード（ワイドブリッジバナー）
        string bDir = Application.dataPath + "/Resources/Banners/";
        var overpassBannerTex = RaceManager.LoadTexture("Banners/banner_turbo_bridge", bDir + "banner_turbo_bridge.png") ?? TextureGen.SponsorBanner(0);
        var underpassBannerTex = RaceManager.LoadTexture("Banners/banner_nitro_bridge", bDir + "banner_nitro_bridge.png") ?? overpassBannerTex;

        var bridgeBannerMat = new Material(rm.bannerMaterial ?? rm.wallMaterial)
        {
            mainTexture = overpassBannerTex,
            color = Color.white
        };
        bridgeBannerMat.SetTexture("_BaseMap", overpassBannerTex);
        bridgeBannerMat.SetTexture("_MainTex", overpassBannerTex);
        bridgeBannerMat.SetColor("_BaseColor", Color.white);
        createdMaterials.Add(bridgeBannerMat);

        var underPortalBannerMat = new Material(rm.bannerMaterial ?? rm.wallMaterial)
        {
            mainTexture = underpassBannerTex,
            color = Color.white
        };
        underPortalBannerMat.SetTexture("_BaseMap", underpassBannerTex);
        underPortalBannerMat.SetTexture("_MainTex", underpassBannerTex);
        underPortalBannerMat.SetColor("_BaseColor", Color.white);
        createdMaterials.Add(underPortalBannerMat);

        Vector3 bridgeSignPos = bridgeCenter + Vector3.up * 6.5f;
        float trussW = WallOffset * 2f + 1f;
        Box("OverpassBridgeSignBeamTop", bridgeSignPos + Vector3.up * 1.5f, new Vector3(trussW, 0.45f, 0.45f), bridgeRot, rm.chromeMaterial);
        Box("OverpassBridgeSignBeamBot", bridgeSignPos - Vector3.up * 1.5f, new Vector3(trussW, 0.45f, 0.45f), bridgeRot, rm.chromeMaterial);

        float signH = 2.6f;
        float signW = trussW - 1.2f;
        var signFrameMat = rm.tireMaterial ?? rm.chromeMaterial;
        Box("OverpassBridgeSignFrame", bridgeSignPos, new Vector3(signW + 0.3f, signH + 0.3f, 0.4f), bridgeRot, signFrameMat);
        Box("OverpassBridgeSignFront", bridgeSignPos - bridgeDir * 0.22f, new Vector3(signW, signH, 1f), bridgeRot, bridgeBannerMat, PrimitiveType.Quad);
        Box("OverpassBridgeSignBack", bridgeSignPos + bridgeDir * 0.22f, new Vector3(signW, signH, 1f), bridgeRot * Quaternion.Euler(0, 180, 0), bridgeBannerMat, PrimitiveType.Quad);

        // 3. 下層アンダーパス（下側を走る道路：Z=40付近）のアンダーパス照明＆ポータル
        int underpassIdx = 289;
        if (Count > 0)
        {
            float bestUnderDist = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (Pts[i].y < 3f)
                {
                    float d = Mathf.Abs(Pts[i].x) + Mathf.Abs(Pts[i].z - 40f);
                    if (d < bestUnderDist) { bestUnderDist = d; underpassIdx = i; }
                }
            }
        }

        // 高架橋の真下（道路底面）に設置されるアンダーパス照明
        for (int lampOff = -12; lampOff <= 12; lampOff += 6)
        {
            Vector3 underLampPos = bridgeCenter + bridgeDir * lampOff - Vector3.up * 1.8f;
            var bulb = Box("UnderpassFloodlight", underLampPos, new Vector3(WallOffset * 1.4f, 0.4f, 0.8f), bridgeRot, signMat);
            var lampLt = bulb.AddComponent<Light>();
            lampLt.type = LightType.Point;
            lampLt.color = new Color(1f, 0.95f, 0.75f);
            lampLt.range = 28f;
            lampLt.intensity = 2.4f;
        }

        // アンダーパス入口・出口のクリアランス警告ポータルゲート
        foreach (int portalOff in new[] { -18, 18 })
        {
            int pIdx = Wrap(underpassIdx + portalOff);
            Vector3 pCenter = Pts[pIdx];
            Vector3 pDir = Dirs[pIdx];
            Vector3 pRight = Rights[pIdx];
            var pRot = Quaternion.LookRotation(pDir, Vector3.up);

            // 門型アーチ支柱
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pCol = pCenter + pRight * (side * (WallOffset + 0.8f));
                Box("UnderPortalCol_" + side, pCol + Vector3.up * 4.5f, new Vector3(1.2f, 9f, 1.2f), pRot, pylonRedMat);
            }
            // 横桁とバナーサイン
            float portalW = WallOffset * 2f + 2.8f;
            Box("UnderPortalBeam", pCenter + Vector3.up * 8.4f, new Vector3(portalW, 2.5f, 1.0f), pRot, signFrameMat);
            Box("UnderPortalSignFront", pCenter + Vector3.up * 8.4f - pDir * 0.52f, new Vector3(portalW - 0.6f, 2.2f, 1f), pRot, underPortalBannerMat, PrimitiveType.Quad);
            Box("UnderPortalSignBack", pCenter + Vector3.up * 8.4f + pDir * 0.52f, new Vector3(portalW - 0.6f, 2.2f, 1f), pRot * Quaternion.Euler(0, 180, 0), underPortalBannerMat, PrimitiveType.Quad);
        }

        // 4. ベイエリアのシーサイド灯台（Lighthouse：X=245, Z=-110）
        Vector3 lhPos = new Vector3(245f, -0.3f, -110f);
        float lhHeight = 28f;
        Box("LighthouseBase", lhPos + Vector3.up * 2f, new Vector3(9f, 4f, 9f), Quaternion.identity, towerWhiteMat, PrimitiveType.Cylinder);
        Box("LighthouseBody", lhPos + Vector3.up * (lhHeight * 0.5f + 2f), new Vector3(6.5f, lhHeight, 6.5f), Quaternion.identity, towerWhiteMat, PrimitiveType.Cylinder);
        Box("LighthouseStripe", lhPos + Vector3.up * (lhHeight * 0.65f), new Vector3(6.7f, 5f, 6.7f), Quaternion.identity, pylonRedMat, PrimitiveType.Cylinder);
        Box("LighthouseCrown", lhPos + Vector3.up * (lhHeight + 2.5f), new Vector3(7.5f, 1.5f, 7.5f), Quaternion.identity, towerWhiteMat, PrimitiveType.Cylinder);
        var lantern = Box("LighthouseLantern", lhPos + Vector3.up * (lhHeight + 4.2f), new Vector3(4.2f, 3f, 4.2f), Quaternion.identity, signMat, PrimitiveType.Cylinder);
        var lhLt = lantern.AddComponent<Light>();
        lhLt.type = LightType.Spot;
        lhLt.spotAngle = 45f;
        lhLt.color = new Color(1f, 0.98f, 0.75f);
        lhLt.range = 140f;
        lhLt.intensity = 4.5f;
        lhLt.transform.rotation = Quaternion.Euler(12f, 225f, 0);

        // 5. ベイエリアのセーリングヨット（マリーナ沖合に浮かぶ船）
        Vector3[] boatSpots =
        {
            new Vector3(120f, -0.65f, -90f),
            new Vector3(160f, -0.65f, -40f),
            new Vector3(80f, -0.65f, 120f)
        };
        for (int b = 0; b < boatSpots.Length; b++)
        {
            Vector3 bPos = boatSpots[b];
            var bRot = Quaternion.Euler(0, 35f + b * 60f, 0);
            // 船体（ハル）
            Box("YachtHull_" + b, bPos + Vector3.up * 0.6f, new Vector3(4.5f, 1.4f, 14f), bRot, towerWhiteMat);
            // マスト
            Box("YachtMast_" + b, bPos + Vector3.up * 7f, new Vector3(0.4f, 12f, 0.4f), bRot, rm.chromeMaterial, PrimitiveType.Cylinder);
            // 三角セール（帆）
            Box("YachtSail_" + b, bPos + Vector3.up * 8f - bRot * Vector3.forward * 2.2f, new Vector3(0.15f, 9f, 6.5f), bRot, towerWhiteMat);
        }

        // 6. コース沿いのヤシの木＆シーサイドプロムナード装飾
        var prefabPalm1 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_1");
        var prefabPalm2 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_2");
        var prefabPalm3 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_3");
        for (int i = 8; i < Count; i += 16)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var p = PointAt(i, side * (WallOffset + 5.5f));
                p.y = GetTerrainHeight(p.x, p.z, def);
                if (p.y < 5f && DistanceToTrack(p, true) >= WallOffset + 4.2f)
                {
                    var palmPrefab = (rng.Next(3) == 0) ? prefabPalm1 : ((rng.Next(2) == 0) ? prefabPalm2 : prefabPalm3);
                    if (palmPrefab != null)
                    {
                        var rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                        float s = 2.6f + (float)rng.NextDouble() * 1.2f;
                        SpawnNatureProp(palmPrefab, p, rot, Vector3.one * s, palmTrunkM, palmLeafM);
                    }
                }
            }
        }
    }
}
