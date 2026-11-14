using System.Collections.Generic;
using UnityEngine;

// コースのプロシージャルメッシュ生成・景観・ギミック配置
public partial class Track
{
    // ───────────────────────── メッシュ ─────────────────────────

    readonly List<Mesh> createdMeshes = new List<Mesh>();
    readonly List<Material> createdMaterials = new List<Material>();
    readonly Renderer[] startSignalBulbs = new Renderer[3];
    readonly Light[] startSignalLights = new Light[3];
    MaterialPropertyBlock signalMpb;

    static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else DestroyImmediate(obj);
    }

    void OnDestroy() => ClearVisuals();

    public void ClearVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            SafeDestroy(transform.GetChild(i).gameObject);

        foreach (var m in createdMeshes)
            if (m != null) SafeDestroy(m);
        createdMeshes.Clear();

        foreach (var mat in createdMaterials)
            if (mat != null) SafeDestroy(mat);
        createdMaterials.Clear();

        if (cityMats != null)
        {
            foreach (var cm in cityMats)
                if (cm != null) SafeDestroy(cm);
            cityMats = null;
        }

        for (int i = 0; i < 3; i++)
        {
            startSignalBulbs[i] = null;
            startSignalLights[i] = null;
        }

        BoostPads.Clear();
        ItemBoxSpots.Clear();
    }

    public void BuildVisuals(RaceManager rm, int courseIndex = -1)
    {
        ResourceCache.PreloadRaceAssets();
        if (courseIndex >= 0) ActiveCourseIndex = Mathf.Clamp(courseIndex, 0, Courses.Length - 1);
        var def = Courses[ActiveCourseIndex];
        ClearVisuals();

        var roadMat = new Material(rm.roadMaterial) { mainTexture = TextureGen.Asphalt(def.RoadColor) };
        var curbMat = new Material(rm.curbMaterial) { mainTexture = TextureGen.Stripes(def.CurbColorA, def.CurbColorB) };
        var wallMat = new Material(rm.wallMaterial) { mainTexture = TextureGen.Stripes(def.WallColorA, def.WallColorB) };
        createdMaterials.Add(roadMat);
        createdMaterials.Add(curbMat);
        createdMaterials.Add(wallMat);

        Texture2D groundTex = def.SceneryTheme == 1 ? TextureGen.Sand()
            : def.SceneryTheme == 2 ? TextureGen.Snow()
            : (def.SceneryTheme == 3 || def.SceneryTheme == 5) ? TextureGen.Concrete() : TextureGen.Grass();
        var groundMat = new Material(rm.grassMaterial) { mainTexture = groundTex, color = def.GroundColor };
        groundMat.mainTextureScale = new Vector2(160, 160);
        createdMaterials.Add(groundMat);

        var shoulderMat = new Material(rm.grassMaterial) { color = def.ShoulderColor, mainTexture = groundTex };
        createdMaterials.Add(shoulderMat);

        bool hasBranches = def.Branches != null && def.Branches.Length > 0;
        if (hasBranches)
        {
            BuildSplitTrack(rm, def, roadMat, curbMat, wallMat, shoulderMat, groundTex);
        }
        else
        {
            Strip("Road", new Vector2(-HalfWidth, 0.03f), new Vector2(HalfWidth, 0.03f), roadMat, HalfWidth * 2f);
            Strip("CurbL", new Vector2(-HalfWidth - CurbWidth, 0.05f), new Vector2(-HalfWidth, 0.05f), curbMat, 4f);
            Strip("CurbR", new Vector2(HalfWidth, 0.05f), new Vector2(HalfWidth + CurbWidth, 0.05f), curbMat, 4f);

            // 路肩
            Strip("ShoulderL", new Vector2(-WallOffset, 0.02f), new Vector2(-HalfWidth - CurbWidth, 0.02f), shoulderMat, 6f);
            Strip("ShoulderR", new Vector2(HalfWidth + CurbWidth, 0.02f), new Vector2(WallOffset, 0.02f), shoulderMat, 6f);

            // 北大キャンパス専用：北13条イチョウ並木区間の路肩＆路面に敷き詰められる黄金の落ち葉の絨毯
            if (def.SceneryTheme == 4)
            {
                var ginkgoCarpetMat = new Material(rm.grassMaterial) { color = Color.white, mainTexture = TextureGen.GinkgoCarpet() };
                createdMaterials.Add(ginkgoCarpetMat);
                int gStart = (int)(0.26f * Count);
                int gEnd = (int)(0.39f * Count);
                int gSegs = gEnd - gStart;
                // 路肩の絨毯
                Strip("GinkgoCarpetL", new Vector2(-WallOffset + 0.1f, 0.035f), new Vector2(-HalfWidth - CurbWidth, 0.035f), ginkgoCarpetMat, 4f, gStart, gSegs);
                Strip("GinkgoCarpetR", new Vector2(HalfWidth + CurbWidth, 0.035f), new Vector2(WallOffset - 0.1f, 0.035f), ginkgoCarpetMat, 4f, gStart, gSegs);
                // 道路端（アスファルトの左右端に溜まった落ち葉帯）
                Strip("GinkgoRoadEdgeL", new Vector2(-HalfWidth, 0.038f), new Vector2(-HalfWidth + 1.2f, 0.038f), ginkgoCarpetMat, 3f, gStart, gSegs);
                Strip("GinkgoRoadEdgeR", new Vector2(HalfWidth - 1.2f, 0.038f), new Vector2(HalfWidth, 0.038f), ginkgoCarpetMat, 3f, gStart, gSegs);
            }

            // 壁は両面
            Strip("WallL", new Vector2(-WallOffset, 0f), new Vector2(-WallOffset, 1.2f), wallMat, 6f);
            Strip("WallL2", new Vector2(-WallOffset, 1.2f), new Vector2(-WallOffset, 0f), wallMat, 6f);
            Strip("WallR", new Vector2(WallOffset, 1.2f), new Vector2(WallOffset, 0f), wallMat, 6f);
            Strip("WallR2", new Vector2(WallOffset, 0f), new Vector2(WallOffset, 1.2f), wallMat, 6f);
            Strip("WallTopL", new Vector2(-WallOffset - 0.4f, 1.2f), new Vector2(-WallOffset, 1.2f), rm.chromeMaterial, 6f);
            Strip("WallTopR", new Vector2(WallOffset, 1.2f), new Vector2(WallOffset + 0.4f, 1.2f), rm.chromeMaterial, 6f);

            // 高架橋の底面（下から見上げたときに道路の裏が見えるように）
            Strip("RoadUnder", new Vector2(WallOffset, -0.35f), new Vector2(-WallOffset, -0.35f), rm.standMaterial, 8f);
        }

        // 地面（コースの起伏に寄り添って自然に盛り上がるプロシージャル地形システム）
        BuildProceduralTerrain(rm, def, groundMat);

        BuildStartLine(rm, def);
        BuildBoostPads(rm);
        PlanItemBoxes();
        BuildBridgePillars(rm, def);
        BuildJumpRamps(rm);
        BuildTunnels(rm, def);
        int decorStart = transform.childCount;
        BuildScenery(rm, def);
        BuildSponsorBoards(rm, def);
        BuildCircuitProps(rm, def);
        ApplyDecorOverrides(decorStart);
        BuildAnimals(rm, def);
    }

    public void ApplyEnvironment(RaceManager rm, int courseIndex = -1)
    {
        if (courseIndex >= 0) ActiveCourseIndex = Mathf.Clamp(courseIndex, 0, Courses.Length - 1);
        var def = Courses[ActiveCourseIndex];

        if (RenderSettings.skybox != null)
        {
            RenderSettings.skybox.SetColor("_SkyTint", def.SkyTint);
            RenderSettings.skybox.SetColor("_GroundColor", def.GroundColor);
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 220f;
        RenderSettings.fogEndDistance = def.FogDistance;
        RenderSettings.fogColor = def.FogColor;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = def.SkyTint * 0.9f;
        RenderSettings.ambientEquatorColor = Color.Lerp(def.SkyTint, def.GroundColor, 0.5f);
        RenderSettings.ambientGroundColor = def.GroundColor * 0.7f;

        if (rm.Sun != null)
        {
            rm.Sun.color = def.SunColor;
            rm.Sun.intensity = def.SunIntensity;
            rm.Sun.transform.rotation = Quaternion.Euler(def.SunRotation);
        }
    }

    // a/b は (横オフセット, 高さ)。路面法線 Normals に沿って生成するため立体コースでも歪まない。
    // from/segments を指定すると周回の一部区間だけを生成する（トンネル用）
    void Strip(string name, Vector2 a, Vector2 b, Material mat, float vScale, int from = 0, int segments = -1)
    {
        int c = Count;
        bool full = segments < 0;
        int segs = full ? c : segments;
        var verts = new Vector3[(segs + 1) * 2];
        var uvs = new Vector2[(segs + 1) * 2];
        var tris = new int[segs * 6];
        for (int i = 0; i <= segs; i++)
        {
            int k = (from + i) % c;
            float v = (full && i == c ? Length : Dist[k]) / vScale;
            verts[i * 2] = Pts[k] + Rights[k] * a.x + Normals[k] * a.y;
            verts[i * 2 + 1] = Pts[k] + Rights[k] * b.x + Normals[k] * b.y;
            uvs[i * 2] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);
        }
        for (int i = 0; i < segs; i++)
        {
            int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
            tris[i * 6 + 0] = a0; tris[i * 6 + 1] = a1; tris[i * 6 + 2] = b0;
            tris[i * 6 + 3] = b0; tris[i * 6 + 4] = a1; tris[i * 6 + 5] = b1;
        }
        var mesh = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshObject(name, mesh, mat);
    }

    // 動的オフセットによるメッシュ生成（ルート分岐や可変幅用）
    void StripDynamic(string name, System.Func<int, (Vector2 a, Vector2 b)> getOffsets, Material mat, float vScale, int from = 0, int segments = -1)
    {
        int c = Count;
        bool full = segments < 0;
        int segs = full ? c : segments;
        var verts = new Vector3[(segs + 1) * 2];
        var uvs = new Vector2[(segs + 1) * 2];
        var tris = new int[segs * 6];
        for (int i = 0; i <= segs; i++)
        {
            int k = (from + i) % c;
            float v = (full && i == c ? Length : Dist[k]) / vScale;
            var (a, b) = getOffsets(k);
            verts[i * 2] = Pts[k] + Rights[k] * a.x + Normals[k] * a.y;
            verts[i * 2 + 1] = Pts[k] + Rights[k] * b.x + Normals[k] * b.y;
            uvs[i * 2] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);
        }
        for (int i = 0; i < segs; i++)
        {
            int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
            tris[i * 6 + 0] = a0; tris[i * 6 + 1] = a1; tris[i * 6 + 2] = b0;
            tris[i * 6 + 3] = b0; tris[i * 6 + 4] = a1; tris[i * 6 + 5] = b1;
        }
        var mesh = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshObject(name, mesh, mat);
    }

    void BuildSplitTrack(RaceManager rm, TrackDef def, Material roadMat, Material curbMat, Material wallMat, Material shoulderMat, Texture2D groundTex)
    {
        // 1. 左車線道路 & 右車線道路
        StripDynamic("RoadL", k =>
        {
            float ratio = (float)k / Count;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                float leftEdge = Mathf.Lerp(-HalfWidth, b.LeftOffset - b.LaneWidth, blend);
                float rightEdge = Mathf.Lerp(0f, b.LeftOffset + b.LaneWidth, blend);
                return (new Vector2(leftEdge, 0.03f), new Vector2(rightEdge, 0.03f));
            }
            return (new Vector2(-HalfWidth, 0.03f), new Vector2(0f, 0.03f));
        }, roadMat, HalfWidth * 2f);

        // 木橋（ウッドデッキ）風マテリアル
        var woodMat = new Material(rm.standMaterial) { color = new Color(0.55f, 0.35f, 0.20f) };
        createdMaterials.Add(woodMat);

        StripDynamic("RoadR", k =>
        {
            float ratio = (float)k / Count;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                float leftEdge = Mathf.Lerp(0f, b.RightOffset - b.LaneWidth, blend);
                float rightEdge = Mathf.Lerp(HalfWidth, b.RightOffset + b.LaneWidth, blend);
                float h = 0.03f + b.RightHeight * blend;
                return (new Vector2(leftEdge, h), new Vector2(rightEdge, h));
            }
            return (new Vector2(0f, 0.03f), new Vector2(HalfWidth, 0.03f));
        }, roadMat, HalfWidth * 2f);

        // 2. 外側縁石 (CurbL, CurbR)
        StripDynamic("CurbL", k =>
        {
            float ratio = (float)k / Count;
            float leftEdge = -HalfWidth;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
                leftEdge = Mathf.Lerp(-HalfWidth, b.LeftOffset - b.LaneWidth, blend);
            return (new Vector2(leftEdge - CurbWidth, 0.05f), new Vector2(leftEdge, 0.05f));
        }, curbMat, 4f);

        StripDynamic("CurbR", k =>
        {
            float ratio = (float)k / Count;
            float rightEdge = HalfWidth;
            float h = 0.05f;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                rightEdge = Mathf.Lerp(HalfWidth, b.RightOffset + b.LaneWidth, blend);
                h += b.RightHeight * blend;
            }
            return (new Vector2(rightEdge, h), new Vector2(rightEdge + CurbWidth, h));
        }, curbMat, 4f);

        // 3. 外側路肩 (ShoulderL, ShoulderR)
        StripDynamic("ShoulderL", k =>
        {
            float ratio = (float)k / Count;
            float leftEdge = -HalfWidth;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
                leftEdge = Mathf.Lerp(-HalfWidth, b.LeftOffset - b.LaneWidth, blend);
            return (new Vector2(leftEdge - CurbWidth - 4f, 0.02f), new Vector2(leftEdge - CurbWidth, 0.02f));
        }, shoulderMat, 6f);

        StripDynamic("ShoulderR", k =>
        {
            float ratio = (float)k / Count;
            float rightEdge = HalfWidth;
            float h = 0.02f;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                rightEdge = Mathf.Lerp(HalfWidth, b.RightOffset + b.LaneWidth, blend);
                h += b.RightHeight * blend;
            }
            return (new Vector2(rightEdge + CurbWidth, h), new Vector2(rightEdge + CurbWidth + 4f, h));
        }, shoulderMat, 6f);

        // 4. 外壁 (WallL, WallR)
        StripDynamic("WallL", k =>
        {
            float ratio = (float)k / Count;
            float leftEdge = -HalfWidth;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
                leftEdge = Mathf.Lerp(-HalfWidth, b.LeftOffset - b.LaneWidth, blend);
            float wallX = leftEdge - CurbWidth - 4f;
            return (new Vector2(wallX, 0f), new Vector2(wallX, 1.2f));
        }, wallMat, 6f);

        StripDynamic("WallL2", k =>
        {
            float ratio = (float)k / Count;
            float leftEdge = -HalfWidth;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
                leftEdge = Mathf.Lerp(-HalfWidth, b.LeftOffset - b.LaneWidth, blend);
            float wallX = leftEdge - CurbWidth - 4f;
            return (new Vector2(wallX, 1.2f), new Vector2(wallX, 0f));
        }, wallMat, 6f);

        StripDynamic("WallR", k =>
        {
            float ratio = (float)k / Count;
            float rightEdge = HalfWidth;
            float h = 0f;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                rightEdge = Mathf.Lerp(HalfWidth, b.RightOffset + b.LaneWidth, blend);
                h = b.RightHeight * blend;
            }
            float wallX = rightEdge + CurbWidth + 4f;
            return (new Vector2(wallX, h), new Vector2(wallX, h + 1.2f));
        }, wallMat, 6f);

        StripDynamic("WallR2", k =>
        {
            float ratio = (float)k / Count;
            float rightEdge = HalfWidth;
            float h = 0f;
            if (IsInBranch(ratio, out BranchDef b, out float blend))
            {
                rightEdge = Mathf.Lerp(HalfWidth, b.RightOffset + b.LaneWidth, blend);
                h = b.RightHeight * blend;
            }
            float wallX = rightEdge + CurbWidth + 4f;
            return (new Vector2(wallX, h + 1.2f), new Vector2(wallX, h));
        }, wallMat, 6f);

        // 5. 分岐区間の中央ディバイダー（激流リバー、中央壁、トレッスル支柱、分岐標識）
        BuildSplitFeatures(rm, def, curbMat, wallMat, woodMat);
    }

    void BuildSplitFeatures(RaceManager rm, TrackDef def, Material curbMat, Material wallMat, Material woodMat)
    {
        if (def.Branches == null) return;

        // 激流エメラルドグリーンの水面マテリアル
        var riverMat = new Material(rm.boostPadMaterial)
        {
            mainTexture = TextureGen.RiverWater(),
            color = Color.white
        };
        riverMat.SetTexture("_BaseMap", TextureGen.RiverWater());
        riverMat.EnableKeyword("_EMISSION");
        riverMat.SetTexture("_EmissionMap", TextureGen.RiverWater());
        riverMat.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 0.9f) * 0.5f);
        riverMat.mainTextureScale = new Vector2(2f, 16f);
        createdMaterials.Add(riverMat);

        for (int bi = 0; bi < def.Branches.Length; bi++)
        {
            var b = def.Branches[bi];
            int startIdx = Mathf.RoundToInt(b.Start * Count);
            int endIdx = Mathf.RoundToInt(b.End * Count);
            int segs = endIdx - startIdx;
            if (segs <= 0) segs += Count;

            // 1. 中央の激流リバーベッド（25m幅の大河、高低差をつけて渓谷の谷底を流れるように）
            StripDynamic("SplitRiver", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.05f)
                {
                    float leftInner = br.LeftOffset + br.LaneWidth;
                    float rightInner = br.RightOffset - br.LaneWidth;
                    float riverH = -0.35f * blend;
                    return (new Vector2(leftInner, riverH), new Vector2(rightInner, riverH));
                }
                return (Vector2.zero, Vector2.zero);
            }, riverMat, 6f, startIdx, segs);

            // 1b. 川の赤岩マテリアル（護岸・巨岩用）
            var rockMat = new Material(rm.wallMaterial) { color = new Color(0.80f, 0.42f, 0.22f) };
            createdMaterials.Add(rockMat);

            // 1c. 左岸の赤岩バンク（道路から川面へ下る斜面・護岸）
            StripDynamic("SplitRiverBankL", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.05f)
                {
                    float roadEdge = br.LeftOffset + br.LaneWidth;
                    float riverEdge = roadEdge + 0.8f;
                    return (new Vector2(roadEdge, 0.05f), new Vector2(riverEdge, -0.35f * blend));
                }
                return (Vector2.zero, Vector2.zero);
            }, rockMat, 4f, startIdx, segs);

            // 1d. 右岸の赤岩バンク（川面から木橋下・地面へ上がる斜面・護岸）
            StripDynamic("SplitRiverBankR", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.05f)
                {
                    float riverEdge = br.RightOffset - br.LaneWidth - 0.8f;
                    float groundEdge = br.RightOffset - br.LaneWidth;
                    return (new Vector2(riverEdge, -0.35f * blend), new Vector2(groundEdge, 0.05f));
                }
                return (Vector2.zero, Vector2.zero);
            }, rockMat, 4f, startIdx, segs);

            // 2. 左車線の内側縁石 & ガードレール
            StripDynamic("SplitDividerL", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.1f)
                {
                    float innerX = br.LeftOffset + br.LaneWidth;
                    return (new Vector2(innerX, 0.05f), new Vector2(innerX + CurbWidth, 0.05f));
                }
                return (Vector2.zero, Vector2.zero);
            }, curbMat, 4f, startIdx, segs);

            StripDynamic("SplitDividerWallL", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.1f)
                {
                    float wallX = br.LeftOffset + br.LaneWidth + CurbWidth;
                    return (new Vector2(wallX, 0f), new Vector2(wallX, 0.9f));
                }
                return (Vector2.zero, Vector2.zero);
            }, wallMat, 4f, startIdx, segs);

            // 3. 右車線の木製手すりフェンス
            StripDynamic("SplitWoodFenceR", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.1f)
                {
                    float wallX = br.RightOffset - br.LaneWidth;
                    float h = br.RightHeight * blend;
                    return (new Vector2(wallX, h), new Vector2(wallX, h + 0.9f));
                }
                return (Vector2.zero, Vector2.zero);
            }, woodMat, 4f, startIdx, segs);

            // 3b. 右車線（高架トレッスル橋）の木製底面
            StripDynamic("SplitBridgeUnderR", k =>
            {
                float ratio = (float)k / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.1f)
                {
                    float leftInner = br.RightOffset - br.LaneWidth;
                    float rightOuter = br.RightOffset + br.LaneWidth;
                    float h = br.RightHeight * blend - 0.25f;
                    return (new Vector2(leftInner, h), new Vector2(rightOuter, h));
                }
                return (Vector2.zero, Vector2.zero);
            }, woodMat, 6f, startIdx, segs);

            // 4. 右車線下のトレッスル木製橋脚
            for (int k = startIdx; k <= endIdx; k += 4)
            {
                int idx = k % Count;
                float ratio = (float)idx / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.3f)
                {
                    Vector3 bridgeCenter = PointAt(idx, br.RightOffset);
                    float h = br.RightHeight * blend;
                    Vector3 postPos = bridgeCenter - Normals[idx] * (h * 0.5f);
                    Box("TrestlePillar", postPos, new Vector3(br.LaneWidth * 1.6f, h + 0.4f, 1.2f),
                        Quaternion.LookRotation(Dirs[idx]), woodMat);
                }
            }

            // 4b. 川の中の巨岩・奇岩群（River Rocks & Rapids - スタイライズド自然岩）
            var rockFbxList = new[]
            {
                ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_1"),
                ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_2"),
                ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_3"),
                ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_4"),
                ResourceCache.Load<GameObject>("Props/StylizedNature/Rock_5")
            };
            var texRedDesert = ResourceCache.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Red_Desert");
            var redRockMat = new Material(rm.wallMaterial)
            {
                mainTexture = texRedDesert != null ? texRedDesert : TextureGen.Sand(),
                color = new Color(0.95f, 0.55f, 0.35f)
            };
            createdMaterials.Add(redRockMat);

            var riverRng = new System.Random(77);
            for (int k = startIdx + 3; k <= endIdx - 3; k += 5)
            {
                int idx = k % Count;
                float ratio = (float)idx / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.25f)
                {
                    // 川の中央付近（-6.0m〜+6.0m）に配置（川幅25mの中央部で白波を立てる奇岩群）
                    float rLat = (float)(riverRng.NextDouble() * 12.0 - 6.0);
                    float rScale = 1.8f + (float)riverRng.NextDouble() * 2.2f;
                    Vector3 rockPos = Pts[idx] + Rights[idx] * rLat + Vector3.up * (-0.35f * blend + 0.05f);
                    Quaternion rockRot = Quaternion.Euler((float)riverRng.NextDouble() * 12f, (float)riverRng.NextDouble() * 360f, (float)riverRng.NextDouble() * 12f);

                    var fbx = rockFbxList[riverRng.Next(rockFbxList.Length)];
                    if (fbx != null)
                    {
                        var rockGo = Instantiate(fbx, rockPos, rockRot, transform);
                        rockGo.name = "RiverRock";
                        rockGo.transform.localScale = Vector3.one * rScale;
                        StripColliders(rockGo);
                        foreach (var r in rockGo.GetComponentsInChildren<Renderer>())
                            r.sharedMaterial = redRockMat;
                    }
                    else
                    {
                        Box("RiverRock", rockPos + Vector3.up * (rScale * 0.45f), new Vector3(rScale * 1.2f, rScale * 1.3f, rScale * 1.2f), rockRot, redRockMat);
                    }
                }
            }

            // 5. 分岐ノーズ（大型案内標識「◀ SPLIT ROUTE ▶」& クッションバリア）
            int noseIdx = (startIdx - 4 + Count) % Count;
            Vector3 nosePos = Pts[noseIdx] + Normals[noseIdx] * 0.05f;
            Quaternion noseRot = Quaternion.LookRotation(Dirs[noseIdx]);

            // 黄色と黒の衝撃吸収ドラム缶 / クッションバリア
            var cushionMat = new Material(rm.wallMaterial) { mainTexture = TextureGen.HazardStripes() };
            createdMaterials.Add(cushionMat);
            Box("SplitCushion", nosePos + Vector3.up * 0.6f, new Vector3(3.0f, 1.3f, 3.0f), noseRot, cushionMat, PrimitiveType.Cylinder);

            // 大型案内看板（SPLIT SIGN - ポップなトイ・アーケードデザイン！）
            Vector3 signPos = nosePos + Vector3.up * 4.2f;
            var signMat = new Material(rm.wallMaterial)
            {
                mainTexture = TextureGen.SplitSign(),
                color = Color.white
            };
            signMat.SetTexture("_BaseMap", TextureGen.SplitSign());
            signMat.EnableKeyword("_EMISSION");
            signMat.SetTexture("_EmissionMap", TextureGen.SplitSign());
            signMat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
            createdMaterials.Add(signMat);

            // メタリック外枠フレーム
            Box("SplitSignFrame", signPos, new Vector3(10.4f, 3.8f, 0.25f), noseRot, rm.chromeMaterial);

            // サインボード（Quadメッシュで走行カートに正対）
            var boardGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            SafeDestroy(boardGo.GetComponent<Collider>());
            boardGo.name = "SplitSignBoard";
            boardGo.transform.SetParent(transform, false);
            boardGo.transform.position = signPos - noseRot * new Vector3(0, 0, 0.14f);
            boardGo.transform.rotation = noseRot;
            boardGo.transform.localScale = new Vector3(10.0f, 3.4f, 1f);
            boardGo.GetComponent<Renderer>().sharedMaterial = signMat;

            // 支柱（クロームポール）
            Box("SplitSignPoleL", signPos + noseRot * new Vector3(-4.2f, -2.3f, 0), new Vector3(0.38f, 4.6f, 0.38f), noseRot, rm.chromeMaterial, PrimitiveType.Cylinder);
            Box("SplitSignPoleR", signPos + noseRot * new Vector3(4.2f, -2.3f, 0), new Vector3(0.38f, 4.6f, 0.38f), noseRot, rm.chromeMaterial, PrimitiveType.Cylinder);

            // 川の水流アニメーションコンポーネントをアタッチ
            var riverAnim = gameObject.GetComponent<RiverFlowAnimator>();
            if (riverAnim == null) riverAnim = gameObject.AddComponent<RiverFlowAnimator>();
            riverAnim.riverMat = riverMat;
        }
    }

    Material[] cityMats;

    // 窓の明かりが付いたビル外壁マテリアル（数種類を使い回す）
    Material[] CityMaterials(RaceManager rm)
    {
        if (cityMats != null && cityMats[0] != null) return cityMats;
        var walls = new[] { new Color(0.22f, 0.24f, 0.34f), new Color(0.32f, 0.26f, 0.36f), new Color(0.18f, 0.28f, 0.34f), new Color(0.36f, 0.34f, 0.40f), new Color(0.2f, 0.2f, 0.28f) };
        var lits = new[] { new Color(1f, 0.85f, 0.45f), new Color(0.6f, 0.9f, 1f), new Color(1f, 0.6f, 0.8f), new Color(1f, 0.9f, 0.7f), new Color(0.7f, 1f, 0.8f) };
        cityMats = new Material[walls.Length];
        for (int i = 0; i < walls.Length; i++)
            cityMats[i] = new Material(rm.wallMaterial) { mainTexture = TextureGen.Windows(walls[i], lits[i], 100 + i), color = Color.white };
        return cityMats;
    }

    // 外壁の窓テクスチャを建物サイズに合わせてタイリングする（1タイル = 横12m x 縦28m）
    void TileFacade(GameObject go, float width, float height)
    {
        var mpb = new MaterialPropertyBlock();
        mpb.SetVector("_MainTex_ST", new Vector4(Mathf.Max(1f, width / 12f), Mathf.Max(1f, height / 28f), 0, 0));
        go.GetComponent<Renderer>().SetPropertyBlock(mpb);
    }

    GameObject MeshObject(string name, Mesh mesh, Material mat)
    {
        if (mesh != null) createdMeshes.Add(mesh);
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    GameObject Box(string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    Material MakeMat(Material baseMat, Texture2D tex, Color tint, Color? emission = null)
    {
        var m = new Material(baseMat);
        if (tex != null)
        {
            m.mainTexture = tex;
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
        }
        m.color = tint;
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);
        if (m.HasProperty("_AlphaClip") && baseMat != null && baseMat.GetFloat("_AlphaClip") > 0.5f)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.3f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        createdMaterials.Add(m);
        return m;
    }

    GameObject SpawnNatureProp(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale, Material trunkMat, Material leafMat)
    {
        return SpawnProp(prefab, pos, rot, scale, trunkMat, leafMat);
    }

    GameObject SpawnProp(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale, Material primaryMat, Material secondaryMat = null, Material accentMat = null)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, rot, transform);
        go.transform.localScale = scale;
        StripColliders(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterial = primaryMat;
                continue;
            }
            var newMats = new Material[mats.Length];
            for (int m = 0; m < mats.Length; m++)
            {
                string mName = mats[m] != null ? mats[m].name.ToLowerInvariant() : "";
                if (mName.Contains("bark") || mName.Contains("wood") || mName.Contains("trunk") || mName.Contains("dirt") || mName.Contains("grey") || mName.Contains("metal") || mName.Contains("pole"))
                    newMats[m] = primaryMat;
                else if (mName.Contains("light") || mName.Contains("lamp") || mName.Contains("default"))
                    newMats[m] = secondaryMat ?? primaryMat;
                else if (mName.Contains("color") || mName.Contains("flower") || mName.Contains("red") || mName.Contains("yellow") || mName.Contains("purple") || mName.Contains("pylon"))
                    newMats[m] = accentMat ?? secondaryMat ?? primaryMat;
                else if (mName.Contains("leaf") || mName.Contains("grass") || mName.Contains("stone") || mName.Contains("roof"))
                    newMats[m] = secondaryMat ?? primaryMat;
                else
                    newMats[m] = secondaryMat ?? primaryMat;
            }
            r.sharedMaterials = newMats;
        }
        return go;
    }

    void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
    }

    float DistanceToTrack(Vector3 p, bool flat = false)
    {
        float best = float.MaxValue;
        for (int i = 0; i < Count; i += 3)
        {
            var d = Pts[i] - p;
            if (flat) d.y = 0f;
            best = Mathf.Min(best, d.sqrMagnitude);

            float ratio = (float)i / Count;
            if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.05f)
            {
                var dL = (Pts[i] + Rights[i] * (br.LeftOffset * blend)) - p;
                var dR = (Pts[i] + Rights[i] * (br.RightOffset * blend)) - p;
                if (flat) { dL.y = 0f; dR.y = 0f; }
                best = Mathf.Min(best, dL.sqrMagnitude);
                best = Mathf.Min(best, dR.sqrMagnitude);
            }
        }
        return Mathf.Sqrt(best);
    }
}
