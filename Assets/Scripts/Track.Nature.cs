using System.Collections.Generic;
using UnityEngine;

// 山岳・雲・地形高さ・プロシージャル地形
public partial class Track
{
    // 札幌を囲む手稲山・藻岩山の雄大な連峰メッシュ（滑らかな稜線と手稲山アンテナ塔）
    void BuildSapporoMountains(RaceManager rm, System.Random rng)
    {
        var sapporoMtnMat = new Material(rm.mountainMaterial) { color = new Color(0.25f, 0.42f, 0.52f) };
        sapporoMtnMat.SetFloat("_Cull", 0f);
        createdMaterials.Add(sapporoMtnMat);

        int nTheta = 64;
        int nY = 12;
        float startAng = Mathf.PI * 0.45f; // 北西〜西側から
        float endAng = Mathf.PI * 1.55f;   // 南西〜南〜南東側へワイドに展開
        float baseR = 520f;

        var verts = new Vector3[(nTheta + 1) * (nY + 1)];
        var uvs = new Vector2[(nTheta + 1) * (nY + 1)];
        var tris = new int[nTheta * nY * 6];

        Vector3 teineTopPos = Vector3.zero;

        for (int i = 0; i <= nTheta; i++)
        {
            float u = i / (float)nTheta;
            float ang = Mathf.Lerp(startAng, endAng, u);
            float dirX = Mathf.Cos(ang);
            float dirZ = Mathf.Sin(ang);

            // 稜線高さプロファイル（手稲山 160m, 藻岩山 120m, 円山 90m, 奥手稲 130m）
            float teine = Mathf.Exp(-Mathf.Pow((u - 0.35f) / 0.10f, 2f)) * 160f;
            float moiwa = Mathf.Exp(-Mathf.Pow((u - 0.72f) / 0.09f, 2f)) * 120f;
            float maruyama = Mathf.Exp(-Mathf.Pow((u - 0.86f) / 0.07f, 2f)) * 90f;
            float okuTeine = Mathf.Exp(-Mathf.Pow((u - 0.18f) / 0.09f, 2f)) * 130f;

            float noise = (Mathf.PerlinNoise(u * 14f, 42.5f) - 0.4f) * 35f;
            float edgeFade = Mathf.Sin(u * Mathf.PI); // 端は滑らかに 0m に収束
            float maxH = Mathf.Max(0f, (teine + moiwa + maruyama + okuTeine + noise) * edgeFade);

            float rOffset = (Mathf.PerlinNoise(u * 8f, 17.3f) - 0.5f) * 70f;
            float r = baseR + rOffset;

            if (u > 0.34f && u < 0.36f && teineTopPos == Vector3.zero)
            {
                teineTopPos = new Vector3(bounds.center.x + dirX * r, -0.3f + maxH, bounds.center.z + dirZ * r);
            }

            for (int j = 0; j <= nY; j++)
            {
                float v = j / (float)nY;
                float y = -0.3f + maxH * Mathf.Sin(v * Mathf.PI * 0.5f);
                float curR = r - (1f - v) * 85f; // 裾野は手前に広がる
                int idx = i * (nY + 1) + j;
                verts[idx] = new Vector3(bounds.center.x + dirX * curR, y, bounds.center.z + dirZ * curR);
                uvs[idx] = new Vector2(u * 6f, v * 2f);
            }
        }

        int t = 0;
        for (int i = 0; i < nTheta; i++)
        {
            for (int j = 0; j < nY; j++)
            {
                int i0 = i * (nY + 1) + j;
                int i1 = (i + 1) * (nY + 1) + j;
                int i2 = i * (nY + 1) + j + 1;
                int i3 = (i + 1) * (nY + 1) + j + 1;

                tris[t++] = i0;
                tris[t++] = i1;
                tris[t++] = i2;

                tris[t++] = i1;
                tris[t++] = i3;
                tris[t++] = i2;
            }
        }

        var mtnMesh = new Mesh { name = "SapporoMountainsMesh", vertices = verts, uv = uvs, triangles = tris };
        mtnMesh.RecalculateNormals();
        mtnMesh.RecalculateBounds();
        MeshObject("SapporoMountains", mtnMesh, sapporoMtnMat);

        // 手稲山頂のシンボル：テレビ送信アンテナ塔と航空障害灯
        if (teineTopPos != Vector3.zero)
        {
            Box("TeineTower", teineTopPos + Vector3.up * 14f, new Vector3(1.6f, 28f, 1.6f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
            var redBeacon = new Material(rm.glowMaterial) { color = new Color(1f, 0.15f, 0.15f) };
            createdMaterials.Add(redBeacon);
            Box("TeineBeacon", teineTopPos + Vector3.up * 29f, Vector3.one * 3.2f, Quaternion.identity, redBeacon, PrimitiveType.Sphere);
        }
    }

    // 遠景の山：全周を取り囲む立体的で美麗な連峰メッシュ（アルプス連峰・砂漠メサ・雪山氷河）
    void BuildThemedMountains(RaceManager rm, TrackDef def, System.Random rng)
    {
        int theme = def.SceneryTheme;
        if (theme == 3 || theme == 4 || theme == 5) return; // Cityは摩天楼、Campusは札幌山系、Overpassはシーサイドベイ

        int nTheta = 96;
        int nY = 12;

        // --- 1. 奥の雄大な本峰連峰 (Distant Main Range) ---
        float mainR = 540f;
        var mainVerts = new Vector3[(nTheta + 1) * (nY + 1)];
        var mainUvs = new Vector2[(nTheta + 1) * (nY + 1)];
        var mainTris = new int[nTheta * nY * 6];

        // 冠雪メッシュ用（Circuitテーマの万年雪）
        var snowVerts = new List<Vector3>();
        var snowUvs = new List<Vector2>();
        var snowTris = new List<int>();

        var texRocks = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks");
        var texDesert = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Red_Desert") ?? Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Desert") ?? texRocks;

        Material mainMtnMat;
        if (theme == 0) // Circuit: アルプス調の青藍色連峰＋岩肌テクスチャ
        {
            mainMtnMat = MakeMat(rm.mountainMaterial, texRocks, new Color(0.26f, 0.38f, 0.50f));
            mainMtnMat.mainTextureScale = new Vector2(36f, 10f);
        }
        else if (theme == 1) // Sunset Dunes: 赤岩・砂岩のキャニオン＆巨大メサ
        {
            mainMtnMat = MakeMat(rm.mountainMaterial, texDesert, new Color(1.18f, 0.74f, 0.50f));
            mainMtnMat.mainTextureScale = new Vector2(28f, 8f);
        }
        else // Frost Peak: 白銀の雪山・氷河（ピュアホワイト＆淡いアイスブルー）
        {
            mainMtnMat = new Material(rm.snowMaterial) { color = new Color(0.97f, 0.99f, 1.02f) };
        }
        mainMtnMat.SetFloat("_Cull", 0f);
        createdMaterials.Add(mainMtnMat);

        float[] ridgeHeights = new float[nTheta + 1];

        for (int i = 0; i <= nTheta; i++)
        {
            float u = i / (float)nTheta;
            float ang = u * Mathf.PI * 2f;
            float dirX = Mathf.Cos(ang);
            float dirZ = Mathf.Sin(ang);

            float h = 0f;
            if (theme == 0) // Circuit: アルプスピーク（主峰群と鋭い連峰）
            {
                float p1 = Mathf.Exp(-Mathf.Pow((u - 0.22f) / 0.10f, 2f)) * 200f;
                float p2 = Mathf.Exp(-Mathf.Pow((u - 0.58f) / 0.12f, 2f)) * 225f;
                float p3 = Mathf.Exp(-Mathf.Pow((u - 0.85f) / 0.09f, 2f)) * 190f;
                float baseNoise = (Mathf.PerlinNoise(dirX * 2.2f + 5.5f, dirZ * 2.2f + 5.5f) - 0.3f) * 95f;
                float detailNoise = (Mathf.PerlinNoise(dirX * 5.5f + 12f, dirZ * 5.5f + 12f) - 0.5f) * 30f;
                h = Mathf.Max(50f, p1 + p2 + p3 + baseNoise + detailNoise + 75f);
            }
            else if (theme == 1) // Dunes: 卓状台地（メサ）＆鋭い断崖
            {
                float n = Mathf.PerlinNoise(dirX * 2.8f + 8.2f, dirZ * 2.8f + 8.2f);
                float plateau = Mathf.Clamp((n - 0.35f) * 3.5f, 0f, 1f); // ステップ状の台地
                float detail = (Mathf.PerlinNoise(dirX * 7f, dirZ * 7f) - 0.5f) * 22f;
                h = 65f + plateau * 115f + detail;
            }
            else // Frost: 鋭角な氷河・ピナクル
            {
                float n1 = Mathf.PerlinNoise(dirX * 2.5f + 3f, dirZ * 2.5f + 3f);
                float ridge1 = 1f - Mathf.Abs(n1 * 2f - 1f); // 鋭い尾根
                float n2 = Mathf.PerlinNoise(dirX * 5.0f + 9f, dirZ * 5.0f + 9f);
                float ridge2 = 1f - Mathf.Abs(n2 * 2f - 1f);
                h = 85f + Mathf.Pow(ridge1, 1.8f) * 195f + ridge2 * 55f;
            }

            ridgeHeights[i] = h;
            float rOffset = (Mathf.PerlinNoise(dirX * 3f + 1f, dirZ * 3f + 1f) - 0.5f) * 75f;
            float r = mainR + rOffset;

            for (int j = 0; j <= nY; j++)
            {
                float v = j / (float)nY;
                float y = -2.0f + h * Mathf.Pow(v, 1.35f);
                float curR = r - (1f - v) * (80f + h * 0.40f);
                int idx = i * (nY + 1) + j;
                mainVerts[idx] = new Vector3(bounds.center.x + dirX * curR, y, bounds.center.z + dirZ * curR);
                mainUvs[idx] = new Vector2(u * 16f, v * 4f);
            }
        }

        int t = 0;
        for (int i = 0; i < nTheta; i++)
        {
            for (int j = 0; j < nY; j++)
            {
                int i0 = i * (nY + 1) + j;
                int i1 = (i + 1) * (nY + 1) + j;
                int i2 = i * (nY + 1) + j + 1;
                int i3 = (i + 1) * (nY + 1) + j + 1;

                // 時計回り（プレイヤーの内側視点から見て表面）
                mainTris[t++] = i0;
                mainTris[t++] = i1;
                mainTris[t++] = i2;

                mainTris[t++] = i1;
                mainTris[t++] = i3;
                mainTris[t++] = i2;

                // Circuit テーマで山頂が 115m 以上の場合は冠雪メッシュを生成（雪渓・クーロア）
                if (theme == 0 && j >= nY - 4)
                {
                    float hAvg = (ridgeHeights[i] + ridgeHeights[i + 1]) * 0.5f;
                    if (hAvg > 115f)
                    {
                        int baseV = snowVerts.Count;
                        snowVerts.Add(mainVerts[i0] + Vector3.up * 0.35f);
                        snowVerts.Add(mainVerts[i2] + Vector3.up * 0.35f);
                        snowVerts.Add(mainVerts[i1] + Vector3.up * 0.35f);
                        snowVerts.Add(mainVerts[i3] + Vector3.up * 0.35f);
                        snowUvs.Add(mainUvs[i0]);
                        snowUvs.Add(mainUvs[i2]);
                        snowUvs.Add(mainUvs[i1]);
                        snowUvs.Add(mainUvs[i3]);

                        // 時計回り: (i0, i1, i2) と (i1, i3, i2)
                        snowTris.Add(baseV);
                        snowTris.Add(baseV + 2);
                        snowTris.Add(baseV + 1);

                        snowTris.Add(baseV + 2);
                        snowTris.Add(baseV + 3);
                        snowTris.Add(baseV + 1);
                    }
                }
            }
        }

        var mainMesh = new Mesh { name = "MainMountainMesh", vertices = mainVerts, uv = mainUvs, triangles = mainTris };
        mainMesh.RecalculateNormals();
        mainMesh.RecalculateBounds();
        MeshObject("MainMountainRange", mainMesh, mainMtnMat);

        if (snowVerts.Count > 0)
        {
            var snowCapMat = new Material(rm.snowMaterial) { color = Color.white };
            snowCapMat.SetFloat("_Cull", 0f);
            createdMaterials.Add(snowCapMat);

            var snowMesh = new Mesh { name = "SnowCapMesh", vertices = snowVerts.ToArray(), uv = snowUvs.ToArray(), triangles = snowTris.ToArray() };
            snowMesh.RecalculateNormals();
            snowMesh.RecalculateBounds();
            MeshObject("SnowCaps", snowMesh, snowCapMat);
        }

        // --- 2. 手前の前山・丘陵 (Fore Mountains / Rolling Hills) ---
        float foreR = 400f;
        int foreY = 8;
        var foreVerts = new Vector3[(nTheta + 1) * (foreY + 1)];
        var foreUvs = new Vector2[(nTheta + 1) * (foreY + 1)];
        var foreTris = new int[nTheta * foreY * 6];

        Material foreMat;
        if (theme == 0) // Circuit: 豊かな緑の丘陵
        {
            foreMat = new Material(rm.leavesMaterial) { color = new Color(0.24f, 0.55f, 0.20f) };
        }
        else if (theme == 1) // Dunes: 夕日に輝く砂丘・岩丘
        {
            foreMat = MakeMat(rm.mountainMaterial, texDesert, new Color(0.98f, 0.68f, 0.38f));
            foreMat.mainTextureScale = new Vector2(24f, 6f);
        }
        else // Frost: 純白の氷河・雪丘
        {
            foreMat = new Material(rm.snowMaterial) { color = new Color(0.94f, 0.97f, 1.0f) };
        }
        foreMat.SetFloat("_Cull", 0f);
        createdMaterials.Add(foreMat);

        for (int i = 0; i <= nTheta; i++)
        {
            float u = i / (float)nTheta;
            float ang = u * Mathf.PI * 2f;
            float dirX = Mathf.Cos(ang);
            float dirZ = Mathf.Sin(ang);

            float foreH = 28f + (Mathf.PerlinNoise(dirX * 3.5f + 15f, dirZ * 3.5f + 15f)) * 48f;
            float r = foreR + (Mathf.PerlinNoise(dirX * 4f + 25f, dirZ * 4f + 25f) - 0.5f) * 50f;

            for (int j = 0; j <= foreY; j++)
            {
                float v = j / (float)foreY;
                float y = -1.5f + foreH * Mathf.Sin(v * Mathf.PI * 0.5f);
                float curR = r - (1f - v) * 65f;
                int idx = i * (foreY + 1) + j;
                foreVerts[idx] = new Vector3(bounds.center.x + dirX * curR, y, bounds.center.z + dirZ * curR);
                foreUvs[idx] = new Vector2(u * 12f, v * 2f);
            }
        }

        int ft = 0;
        for (int i = 0; i < nTheta; i++)
        {
            for (int j = 0; j < foreY; j++)
            {
                int i0 = i * (foreY + 1) + j;
                int i1 = (i + 1) * (foreY + 1) + j;
                int i2 = i * (foreY + 1) + j + 1;
                int i3 = (i + 1) * (foreY + 1) + j + 1;

                foreTris[ft++] = i0;
                foreTris[ft++] = i1;
                foreTris[ft++] = i2;

                foreTris[ft++] = i1;
                foreTris[ft++] = i3;
                foreTris[ft++] = i2;
            }
        }

        var foreMesh = new Mesh { name = "ForeMountainMesh", vertices = foreVerts, uv = foreUvs, triangles = foreTris };
        foreMesh.RecalculateNormals();
        foreMesh.RecalculateBounds();
        MeshObject("ForeMountainRange", foreMesh, foreMat);
    }

    // 空の立体感と奥行きを高める3Dスタイライズド雲＆都市サーチライト
    void BuildSkyClouds(RaceManager rm, TrackDef def, System.Random rng)
    {
        int theme = def.SceneryTheme;
        if (theme == 3) // Neon Metropolis: 夜空を照らすサイバー・サーチライトビーム
        {
            var beamCyan = new Material(rm.glowMaterial) { color = new Color(0.1f, 0.85f, 1.0f, 0.22f) };
            var beamPink = new Material(rm.glowMaterial) { color = new Color(1.0f, 0.2f, 0.85f, 0.22f) };
            createdMaterials.Add(beamCyan);
            createdMaterials.Add(beamPink);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI * 0.25f + 0.3f;
                float dist = 260f + (i % 3) * 65f;
                Vector3 basePos = bounds.center + new Vector3(Mathf.Cos(ang) * dist, 75f, Mathf.Sin(ang) * dist);
                Quaternion tilt = Quaternion.Euler((i % 2 == 0 ? 18f : -16f), i * 45f, (i % 3 - 1) * 14f);
                var beam = Box("Searchlight_" + i, basePos, new Vector3(2.8f, 130f, 2.8f), tilt, (i % 2 == 0) ? beamCyan : beamPink, PrimitiveType.Cylinder);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return;
        }

        Color cloudTint = (theme == 1)
            ? new Color(1.0f, 0.90f, 0.78f) // 夕焼け色に染まる雲
            : new Color(0.98f, 0.99f, 1.0f); // 爽やかな純白の雲
        Color cloudEmit = (theme == 1)
            ? new Color(0.28f, 0.18f, 0.10f)
            : new Color(0.22f, 0.24f, 0.28f);

        var cloudMat = MakeMat(rm.snowMaterial, null, cloudTint, cloudEmit);

        int cloudCount = 22;
        for (int c = 0; c < cloudCount; c++)
        {
            float ang = (c / (float)cloudCount) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.22f;
            float dist = 280f + (float)rng.NextDouble() * 210f;
            float alt = 92f + (float)rng.NextDouble() * 55f;
            Vector3 cPos = bounds.center + new Vector3(Mathf.Cos(ang) * dist, alt, Mathf.Sin(ang) * dist);

            var cloudRoot = new GameObject("SkyCloud_" + c);
            cloudRoot.transform.SetParent(transform, false);
            cloudRoot.transform.position = cPos;
            cloudRoot.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            float scale = 1.0f + (float)rng.NextDouble() * 0.65f;
            // メインの雲塊＋左右のパフ（立体感のあるもくもく雲）
            var mainPuff = Box("PuffMain", Vector3.zero, new Vector3(36f, 11f, 20f) * scale, Quaternion.identity, cloudMat, PrimitiveType.Sphere);
            mainPuff.transform.SetParent(cloudRoot.transform, false);
            mainPuff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var leftPuff = Box("PuffL", new Vector3(-14f, -1.5f, 2f) * scale, new Vector3(24f, 8.5f, 15f) * scale, Quaternion.identity, cloudMat, PrimitiveType.Sphere);
            leftPuff.transform.SetParent(cloudRoot.transform, false);
            leftPuff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rightPuff = Box("PuffR", new Vector3(15f, -1.2f, -1.5f) * scale, new Vector3(26f, 9f, 16f) * scale, Quaternion.identity, cloudMat, PrimitiveType.Sphere);
            rightPuff.transform.SetParent(cloudRoot.transform, false);
            rightPuff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var topPuff = Box("PuffTop", new Vector3(3f, 4.2f, 0f) * scale, new Vector3(22f, 9.5f, 15f) * scale, Quaternion.identity, cloudMat, PrimitiveType.Sphere);
            topPuff.transform.SetParent(cloudRoot.transform, false);
            topPuff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public float GetTerrainHeight(float x, float z, TrackDef def)
    {
        float baseGroundY = -0.3f;
        if (Count == 0 || Pts == null) return baseGroundY;

        int theme = def.SceneryTheme;
        if (theme == 3 || theme == 5) // Neon Metropolis & Skyline Overpass は平坦なアスファルト/ベイウォーター
        {
            return baseGroundY;
        }

        // コース上の最近傍点を高速探索（ステップ2で絞り込み、近傍を精査）
        // Course 0 (Turbo Circuit) の高架フライオーバー区間 (y > 4.5m) は空中橋桁のため、
        // 盛土（地形）の最近傍探索から除外することで、立体交差の下層道路を優先しつつ橋台から連続的に地面をスロープさせる
        float bestSqrDist = float.MaxValue;
        int bestIdx = 0;
        for (int i = 0; i < Count; i += 2)
        {
            if (theme == 0 && Pts[i].y > 4.5f) continue;
            float dx = Pts[i].x - x;
            float dz = Pts[i].z - z;
            float sqr = dx * dx + dz * dz;
            if (sqr < bestSqrDist)
            {
                bestSqrDist = sqr;
                bestIdx = i;
            }
        }
        for (int offset = -1; offset <= 1; offset += 2)
        {
            int i = Wrap(bestIdx + offset);
            if (theme == 0 && Pts[i].y > 4.5f) continue;
            float dx = Pts[i].x - x;
            float dz = Pts[i].z - z;
            float sqr = dx * dx + dz * dz;
            if (sqr < bestSqrDist)
            {
                bestSqrDist = sqr;
                bestIdx = i;
            }
        }

        float dist = Mathf.Sqrt(bestSqrDist);
        float trackY = Pts[bestIdx].y;

        // コース直下（道路・縁石・路肩・壁の内側）は路面底面(-0.35m)に厳密固定し、
        // 道路や路肩の上に地面がはみ出したり突き抜けたりするのを100%防止
        if (dist <= WallOffset)
        {
            return trackY - 0.35f;
        }

        // 壁の外側からの距離
        float dWall = dist - WallOffset;

        if (theme == 1) // Sunset Dunes: 砂漠の雄大な砂丘
        {
            float slopeWidth = 110f;
            float t = Mathf.Clamp01(dWall / slopeWidth);
            float smoothT = Mathf.SmoothStep(1f, 0f, t);
            float courseElevation = Mathf.Lerp(baseGroundY, trackY - 0.35f, smoothT);

            // 砂漠全体の自然な砂丘ウェーブ（壁の外側から滑らかにブレンド）
            float duneWave = (Mathf.PerlinNoise(x * 0.008f + 5.2f, z * 0.008f + 5.2f) - 0.4f) * 6.5f;
            float fineRipple = (Mathf.PerlinNoise(x * 0.03f, z * 0.03f) - 0.5f) * 1.5f;
            float noiseWeight = Mathf.Clamp01(dWall / 35f);

            return courseElevation + (duneWave + fineRipple) * noiseWeight;
        }
        else if (theme == 2) // Frost Peak: アルペン雪山・氷河スロープ
        {
            float slopeWidth = 130f;
            float t = Mathf.Clamp01(dWall / slopeWidth);
            float smoothT = Mathf.SmoothStep(1f, 0f, t);
            float courseElevation = Mathf.Lerp(baseGroundY, trackY - 0.35f, smoothT);

            float snowHill = (Mathf.PerlinNoise(x * 0.007f + 12f, z * 0.007f + 12f) - 0.35f) * 7.5f;
            float snowNoise = (Mathf.PerlinNoise(x * 0.025f, z * 0.025f) - 0.5f) * 1.8f;
            float noiseWeight = Mathf.Clamp01(dWall / 40f);

            return courseElevation + (snowHill + snowNoise) * noiseWeight;
        }
        else if (theme == 0) // Turbo Circuit: 緑豊かな芝生丘陵（Rolling Hills）
        {
            float slopeWidth = 85f;
            float t = Mathf.Clamp01(dWall / slopeWidth);
            float smoothT = Mathf.SmoothStep(1f, 0f, t);
            float courseElevation = Mathf.Lerp(baseGroundY, trackY - 0.35f, smoothT);

            float hillWave = (Mathf.PerlinNoise(x * 0.01f + 3f, z * 0.01f + 3f) - 0.5f) * 3.0f;
            float noiseWeight = Mathf.Clamp01(dWall / 30f);

            return courseElevation + hillWave * noiseWeight;
        }
        else if (theme == 4) // Hokkaido Campus: 平坦キャンパス＋第2農場スロープ
        {
            float slopeWidth = 65f;
            float t = Mathf.Clamp01(dWall / slopeWidth);
            float smoothT = Mathf.SmoothStep(1f, 0f, t);
            return Mathf.Lerp(baseGroundY, trackY - 0.35f, smoothT);
        }

        return baseGroundY;
    }

    void BuildProceduralTerrain(RaceManager rm, TrackDef def, Material groundMat)
    {
        int theme = def.SceneryTheme;
        if (theme == 3 || theme == 5) // Neon Metropolis (都市) & Skyline Overpass (ベイエリア) は平坦なQuad
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(bounds.center.x, -0.3f, bounds.center.z);
            ground.transform.rotation = Quaternion.Euler(90, 0, 0);
            ground.transform.localScale = new Vector3(1800, 1800, 1);
            var groundRend = ground.GetComponent<Renderer>();
            groundRend.sharedMaterial = groundMat;
            groundRend.receiveShadows = true;
            return;
        }

        int gridSize = 128;
        float terrainSpan = 1600f;
        float halfSpan = terrainSpan * 0.5f;
        float step = terrainSpan / gridSize;

        int vertCount = (gridSize + 1) * (gridSize + 1);
        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var tris = new int[gridSize * gridSize * 6];

        Vector3 center = bounds.center;
        float startX = center.x - halfSpan;
        float startZ = center.z - halfSpan;

        for (int iz = 0; iz <= gridSize; iz++)
        {
            float z = startZ + iz * step;
            for (int ix = 0; ix <= gridSize; ix++)
            {
                float x = startX + ix * step;
                float y = GetTerrainHeight(x, z, def);

                int idx = iz * (gridSize + 1) + ix;
                verts[idx] = new Vector3(x, y, z);
                // 0〜1に正規化したUV（groundMat.mainTextureScaleの(160, 160)と連動し、10mに1回のリピート密度を実現）
                uvs[idx] = new Vector2((x - startX) / terrainSpan, (z - startZ) / terrainSpan);
            }
        }

        int t = 0;
        for (int iz = 0; iz < gridSize; iz++)
        {
            for (int ix = 0; ix < gridSize; ix++)
            {
                int i0 = iz * (gridSize + 1) + ix;
                int i1 = iz * (gridSize + 1) + (ix + 1);
                int i2 = (iz + 1) * (gridSize + 1) + ix;
                int i3 = (iz + 1) * (gridSize + 1) + (ix + 1);

                tris[t++] = i0;
                tris[t++] = i2;
                tris[t++] = i1;

                tris[t++] = i1;
                tris[t++] = i2;
                tris[t++] = i3;
            }
        }

        var terrainMesh = new Mesh();
        terrainMesh.name = "ProceduralTerrainMesh";
        terrainMesh.vertices = verts;
        terrainMesh.uv = uvs;
        terrainMesh.triangles = tris;
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();

        var groundGo = MeshObject("Ground", terrainMesh, groundMat);
        var mr = groundGo.GetComponent<Renderer>();
        mr.receiveShadows = true;
    }
}
