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
                Resources.Load<GameObject>("Props/StylizedNature/Rock_1"),
                Resources.Load<GameObject>("Props/StylizedNature/Rock_2"),
                Resources.Load<GameObject>("Props/StylizedNature/Rock_3"),
                Resources.Load<GameObject>("Props/StylizedNature/Rock_4"),
                Resources.Load<GameObject>("Props/StylizedNature/Rock_5")
            };
            var texRedDesert = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Red_Desert");
            var redRockMat = new Material(rm.wallMaterial)
            {
                mainTexture = texRedDesert != null ? texRedDesert : TextureGen.Sand(),
                color = new Color(0.95f, 0.55f, 0.35f)
            };
            createdMaterials.Add(redRockMat);

            var riverRng = new System.Random(77);
            for (int k = startIdx + 2; k <= endIdx - 2; k += 3)
            {
                int idx = k % Count;
                float ratio = (float)idx / Count;
                if (IsInBranch(ratio, out BranchDef br, out float blend) && blend > 0.25f)
                {
                    // 川の中央付近（-6.5m〜+6.5m）に配置（川幅25mの中央部）
                    float rLat = (float)(riverRng.NextDouble() * 13.0 - 6.5);
                    float rScale = 2.0f + (float)riverRng.NextDouble() * 2.8f;
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

    // 高架橋・立体交差セクションの橋脚（ピラー）
    void BuildBridgePillars(RaceManager rm, TrackDef def)
    {
        var pillarMat = rm.standMaterial;
        int step = 12;
        float beamH = 1.4f;

        for (int i = 0; i < Count; i += step)
        {
            var p = Pts[i];
            Vector3 beamPos = p - Normals[i] * (0.35f + beamH * 0.5f);
            float groundY = GetTerrainHeight(beamPos.x, beamPos.z, def);

            // ビーム中心から地面までの高低差
            float topY = beamPos.y;
            float colH = topY - groundY;

            // 地面から実際に 2.8m 以上浮いている空中区間（都市高架等）のみ橋脚とビームを配置
            if (colH > 2.8f)
            {
                // 下を別の道路が通っているか（立体交差）を判定
                bool crossesOverTrack = false;
                for (int j = 0; j < Count; j += 4)
                {
                    int indexDiff = Mathf.Abs(i - j);
                    if (indexDiff > 40 && indexDiff < Count - 40)
                    {
                        float hDistSqr = (Pts[j].x - beamPos.x) * (Pts[j].x - beamPos.x) + (Pts[j].z - beamPos.z) * (Pts[j].z - beamPos.z);
                        if (hDistSqr < (WallOffset + 5f) * (WallOffset + 5f) && Pts[j].y < beamPos.y - 3.0f)
                        {
                            crossesOverTrack = true;
                            break;
                        }
                    }
                }

                if (crossesOverTrack)
                {
                    // 立体交差部（下の道路を跨ぐ区間）：
                    // 下の道路の走行路面を完全にクリアに保つため、道路上には柱を一切置かない（オープンスパン構造）
                    // 道路底面に接する頑丈なスチール横桁ビームのみ配置
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(beam.GetComponent<Collider>());
                    beam.name = "OverpassSpanBeam_" + i;
                    beam.transform.SetParent(transform, false);
                    beam.transform.position = beamPos;
                    beam.transform.rotation = Quaternion.LookRotation(Dirs[i], Normals[i]);
                    beam.transform.localScale = new Vector3(WallOffset * 2.2f, beamH, 4.6f);
                    beam.GetComponent<Renderer>().sharedMaterial = pillarMat;
                }
                else
                {
                    // 道路底面（-0.35f）に接するように横ビームを配置
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(beam.GetComponent<Collider>());
                    beam.name = "BridgeBeam_" + i;
                    beam.transform.SetParent(transform, false);
                    beam.transform.position = beamPos;
                    beam.transform.rotation = Quaternion.LookRotation(Dirs[i], Normals[i]);
                    beam.transform.localScale = new Vector3(WallOffset * 1.8f, beamH, 3.8f);
                    beam.GetComponent<Renderer>().sharedMaterial = pillarMat;

                    // コンクリート橋脚（上端はビーム中心高さに接続し、路面を絶対に貫通しない）
                    var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(col.GetComponent<Collider>());
                    col.name = "BridgePillar_" + i;
                    col.transform.SetParent(transform, false);
                    col.transform.position = new Vector3(beamPos.x, groundY + colH * 0.5f, beamPos.z);
                    col.transform.localScale = new Vector3(3.4f, colH * 0.5f, 3.4f);
                    col.GetComponent<Renderer>().sharedMaterial = pillarMat;
                }
            }
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

    // ビルまたは木造納屋を貫通する屋根付き区間
    void BuildTunnels(RaceManager rm, TrackDef def)
    {
        if (def.Tunnels == null) return;
        bool isCampus = def.SceneryTheme == 4;
        const float H = 10f;
        // 壁・天井マテリアル（北大キャンパスは木造下見板張りのオフホワイト、都市はコンクリート）
        var wallTunnelMat = new Material(rm.wallMaterial) {
            mainTexture = isCampus ? TextureGen.BarnWoodWhite() : TextureGen.Concrete(),
            color = Color.white
        };
        var lightMat = new Material(rm.glowMaterial) { color = isCampus ? new Color(1f, 0.78f, 0.38f) : new Color(1f, 0.92f, 0.75f) };
        var trimA = new Material(rm.wallMaterial) { color = isCampus ? new Color(0.12f, 0.48f, 0.24f) : def.CurbColorA };
        var trimB = new Material(rm.wallMaterial) { color = isCampus ? new Color(0.12f, 0.48f, 0.24f) : def.CurbColorB };
        var barnRedMat = new Material(rm.wallMaterial) { color = new Color(0.76f, 0.18f, 0.12f) };
        var woodDarkMat = new Material(rm.trunkMaterial) { color = new Color(0.34f, 0.22f, 0.15f) };
        createdMaterials.Add(wallTunnelMat);
        createdMaterials.Add(lightMat);
        createdMaterials.Add(trimA);
        createdMaterials.Add(trimB);
        createdMaterials.Add(barnRedMat);
        createdMaterials.Add(woodDarkMat);
        var facade = isCampus ? null : CityMaterials(rm);

        for (int t = 0; t < def.Tunnels.Length; t++)
        {
            int i0 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].Start * Count), 0, Count - 2);
            int i1 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].End * Count), i0 + 1, Count - 1);
            int segs = i1 - i0;
            string id = "Tunnel" + t;

            Strip(id + "Ceiling", new Vector2(WallOffset, H), new Vector2(-WallOffset, H), wallTunnelMat, 8f, i0, segs);
            // 側壁は両面
            Strip(id + "WallL", new Vector2(-WallOffset, 0f), new Vector2(-WallOffset, H), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallL2", new Vector2(-WallOffset, H), new Vector2(-WallOffset, 0f), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallR", new Vector2(WallOffset, H), new Vector2(WallOffset, 0f), wallTunnelMat, 8f, i0, segs);
            Strip(id + "WallR2", new Vector2(WallOffset, 0f), new Vector2(WallOffset, H), wallTunnelMat, 8f, i0, segs);

            // 壁際のトリム（キャンパスは木製巾木、都市はネオン）
            Strip(id + "TrimL", new Vector2(-WallOffset + 0.1f, 3.6f), new Vector2(-WallOffset + 0.1f, 4.1f), trimA, 8f, i0, segs);
            Strip(id + "TrimR", new Vector2(WallOffset - 0.1f, 4.1f), new Vector2(WallOffset - 0.1f, 3.6f), trimB, 8f, i0, segs);

            for (int i = i0; i <= i1; i += 4)
            {
                var rot = Quaternion.LookRotation(Dirs[i], Normals[i]);
                if (!isCampus && (i - i0) % 8 == 0)
                    Box("TunnelLight", Pts[i] + Normals[i] * (H - 0.15f), new Vector3(WallOffset * 1.2f, 0.25f, 0.8f), rot, lightMat);

                // 天井の太い木造梁トラス & 温かい納屋ランタン照明
                if (isCampus && (i - i0) % 4 == 0)
                {
                    var beam = Box("BarnBeam", Pts[i] + Normals[i] * (H - 0.5f), new Vector3(WallOffset * 2f, 0.7f, 0.7f), rot, woodDarkMat);
                    // 左右の柱
                    Box("BarnPostL", Pts[i] - Rights[i] * (WallOffset - 0.35f) + Vector3.up * (H * 0.5f), new Vector3(0.7f, H, 0.7f), rot, woodDarkMat);
                    Box("BarnPostR", Pts[i] + Rights[i] * (WallOffset - 0.35f) + Vector3.up * (H * 0.5f), new Vector3(0.7f, H, 0.7f), rot, woodDarkMat);

                    // 天井梁から吊り下がるクラシック納屋ランタン照明（8ステップごと）
                    if ((i - i0) % 8 == 0)
                    {
                        Vector3 lampPos = Pts[i] + Normals[i] * (H - 1.4f);
                        Box("BarnLanternCap", lampPos + Vector3.up * 0.2f, new Vector3(0.8f, 0.25f, 0.8f), rot, woodDarkMat);
                        var bulb = Box("BarnLanternBulb", lampPos, new Vector3(0.6f, 0.6f, 0.6f), rot, lightMat, PrimitiveType.Sphere);

                        var ltGo = new GameObject("BarnLight");
                        ltGo.transform.SetParent(bulb.transform, false);
                        var lt = ltGo.AddComponent<Light>();
                        lt.type = LightType.Point;
                        lt.color = new Color(1f, 0.72f, 0.35f);
                        lt.range = 18f;
                        lt.intensity = 2.6f;
                    }
                }
            }

            if (isCampus)
            {
                // 北大第2農場モデルバーン：優美な赤屋根マンサード（腰折れ屋根）と換気クーポラ
                for (int i = i0; i <= i1; i += 6)
                {
                    var rot = Quaternion.LookRotation(Dirs[i], Vector3.up);
                    float w = WallOffset * 2f + 4f, len = 15f;
                    // 下段急勾配屋根
                    Box("BarnMansardLower", Pts[i] + Vector3.up * (H + 3.2f), new Vector3(w, 6.5f, len), rot, barnRedMat);
                    // 上段緩勾配屋根
                    Box("BarnMansardUpper", Pts[i] + Vector3.up * (H + 7.5f), new Vector3(w * 0.65f, 4.5f, len), rot, barnRedMat);
                }

                // 屋根の上の換気小塔（クーポラ / 鐘楼）3基
                int[] cupolaIdxs = { i0 + (int)(segs * 0.25f), i0 + (int)(segs * 0.5f), i0 + (int)(segs * 0.75f) };
                foreach (int ci in cupolaIdxs)
                {
                    var rot = Quaternion.LookRotation(Dirs[ci], Vector3.up);
                    Vector3 cPos = Pts[ci] + Vector3.up * (H + 10.5f);
                    Box("BarnCupolaBase", cPos, new Vector3(3.2f, 3.5f, 3.2f), rot, wallTunnelMat);
                    Box("BarnCupolaRoof", cPos + Vector3.up * 2.8f, new Vector3(3.8f, 2.2f, 3.8f), rot, barnRedMat);
                }

                // 入口・出口の大破風トラスファサード（白下見板壁、赤屋根破風、X字トラス木組み）
                foreach (int portalIdx in new[] { i0, i1 })
                {
                    var rot = Quaternion.LookRotation(Dirs[portalIdx], Vector3.up);
                    Vector3 pPos = Pts[portalIdx];
                    float pW = WallOffset * 2f + 4.5f;
                    // 妻壁ベース（白木造）
                    Box("BarnPortalWall", pPos + Vector3.up * (H + 4.0f), new Vector3(pW, 8.0f, 1.2f), rot, wallTunnelMat);
                    // 赤屋根の大破風
                    Box("BarnPortalGable", pPos + Vector3.up * (H + 7.8f), new Vector3(pW + 1.2f, 2.5f, 1.8f), rot, barnRedMat);
                    // X字木造トラスブレース（左右の交差斜め梁）
                    Box("BarnPortalTruss1", pPos + Vector3.up * (H + 4.0f) + rot * Vector3.forward * 0.7f, new Vector3(0.55f, 7.5f, 0.55f), rot * Quaternion.Euler(0, 0, 36f), woodDarkMat);
                    Box("BarnPortalTruss2", pPos + Vector3.up * (H + 4.0f) + rot * Vector3.forward * 0.7f, new Vector3(0.55f, 7.5f, 0.55f), rot * Quaternion.Euler(0, 0, -36f), woodDarkMat);
                    // 開拓使の赤い星印（シンボルエンブレム）
                    Box("BarnPortalStar", pPos + Vector3.up * (H + 5.2f) + rot * Vector3.forward * 0.8f, new Vector3(1.4f, 1.4f, 0.2f), rot, barnRedMat);
                }
            }
            else
            {
                // 上に載るビル本体（路面に沿って並べる）
                for (int i = i0; i <= i1; i += 8)
                {
                    float bh = 28f + ((i * 37) % 23);
                    var rot = Quaternion.LookRotation(Dirs[i], Vector3.up);
                    float w = WallOffset * 2f + 2f, len = 18f;
                    var b = Box("TunnelBuilding", Pts[i] + Vector3.up * (H + bh * 0.5f), new Vector3(w, bh, len), rot, facade[(t + i / 8) % facade.Length]);
                    TileFacade(b, (w + len) * 0.5f, bh);
                }
            }
        }
    }

    // 立体ジャンプ台（カタパルトランプ）
    void BuildJumpRamps(RaceManager rm)
    {
        if (JumpRamps.Count == 0) return;

        // ジャンプ台スロープ面用マテリアル（アーケードシェブロン＆発光）
        var rampSurfaceMat = new Material(rm.boostPadMaterial)
        {
            mainTexture = TextureGen.JumpRamp(),
            color = Color.white
        };
        rampSurfaceMat.SetTexture("_BaseMap", TextureGen.JumpRamp());
        rampSurfaceMat.EnableKeyword("_EMISSION");
        rampSurfaceMat.SetTexture("_EmissionMap", TextureGen.JumpRampEmission());
        rampSurfaceMat.SetColor("_EmissionColor", new Color(1.2f, 0.9f, 0.5f));
        createdMaterials.Add(rampSurfaceMat);

        // ジャンプ台台座・側面用マテリアル（堅牢なハザードストライプ＆スチール）
        var rampBaseMat = new Material(rm.wallMaterial)
        {
            mainTexture = TextureGen.HazardStripes(),
            color = Color.white
        };
        rampBaseMat.SetTexture("_BaseMap", TextureGen.HazardStripes());
        rampBaseMat.SetFloat("_Smoothness", 0.35f);
        createdMaterials.Add(rampBaseMat);

        // ポール頂点の発光警告ビーコン用マテリアル
        var beaconMat = new Material(rm.glowMaterial)
        {
            color = new Color(1f, 0.75f, 0.1f)
        };
        createdMaterials.Add(beaconMat);

        var curDef = Courses[ActiveCourseIndex];
        for (int r = 0; r < JumpRamps.Count; r++)
        {
            var ramp = JumpRamps[r];
            int i = ramp.index;
            var pos = PointAt(i, ramp.lateral);
            var fwd = Dirs[i];
            var up = Normals[i];

            var rampGo = new GameObject("JumpRamp_" + r);
            rampGo.transform.SetParent(transform, false);
            rampGo.transform.position = pos;
            rampGo.transform.rotation = Quaternion.LookRotation(fwd, up);

            float w = (curDef.Ramps != null && r < curDef.Ramps.Length && curDef.Ramps[r].Width > 0f) ? curDef.Ramps[r].Width : HalfWidth * 1.7f;
            float len = 8.5f;
            float h = 1.6f;

            // 1. スロープ面（上部走行面）
            var slopeMesh = new Mesh();
            slopeMesh.name = "RampSlopeMesh_" + r;
            createdMeshes.Add(slopeMesh);
            slopeMesh.vertices = new[]
            {
                new Vector3(-w * 0.5f, 0.06f, -len * 0.5f), // 0: 手前左
                new Vector3(w * 0.5f, 0.06f, -len * 0.5f),  // 1: 手前右
                new Vector3(-w * 0.5f, h, len * 0.5f),      // 2: 奥左上
                new Vector3(w * 0.5f, h, len * 0.5f)        // 3: 奥右上
            };
            slopeMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            slopeMesh.triangles = new[]
            {
                0, 2, 1,
                1, 2, 3
            };
            slopeMesh.RecalculateNormals();
            slopeMesh.RecalculateBounds();

            var slopeGo = new GameObject("RampSlope");
            slopeGo.transform.SetParent(rampGo.transform, false);
            slopeGo.AddComponent<MeshFilter>().sharedMesh = slopeMesh;
            slopeGo.AddComponent<MeshRenderer>().sharedMaterial = rampSurfaceMat;

            // 2. 台座・側面・背面フレーム（ハザードストライプ）
            var baseMesh = new Mesh();
            baseMesh.name = "RampBaseMesh_" + r;
            createdMeshes.Add(baseMesh);

            baseMesh.vertices = new[]
            {
                // 左側面（三角）
                new Vector3(-w * 0.5f, 0f, -len * 0.5f),
                new Vector3(-w * 0.5f, h, len * 0.5f),
                new Vector3(-w * 0.5f, 0f, len * 0.5f),

                // 右側面（三角）
                new Vector3(w * 0.5f, 0f, -len * 0.5f),
                new Vector3(w * 0.5f, 0f, len * 0.5f),
                new Vector3(w * 0.5f, h, len * 0.5f),

                // 背面（四角）
                new Vector3(-w * 0.5f, 0f, len * 0.5f),
                new Vector3(-w * 0.5f, h, len * 0.5f),
                new Vector3(w * 0.5f, h, len * 0.5f),
                new Vector3(w * 0.5f, 0f, len * 0.5f)
            };

            baseMesh.uv = new[]
            {
                // 左側面
                new Vector2(0f, 0f), new Vector2(3f, 1f), new Vector2(3f, 0f),
                // 右側面
                new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 1f),
                // 背面
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 1f), new Vector2(4f, 0f)
            };

            baseMesh.triangles = new[]
            {
                // 左側面
                0, 1, 2,
                // 右側面
                3, 4, 5,
                // 背面
                6, 7, 8,
                6, 8, 9
            };
            baseMesh.RecalculateNormals();
            baseMesh.RecalculateBounds();

            var baseGo = new GameObject("RampBase");
            baseGo.transform.SetParent(rampGo.transform, false);
            baseGo.AddComponent<MeshFilter>().sharedMesh = baseMesh;
            baseGo.AddComponent<MeshRenderer>().sharedMaterial = rampBaseMat;

            // 3. ランプ先端の両脇に警告サインポール＆発光ビーコン
            var poleMat = rm.chromeMaterial;
            foreach (float side in new[] { -1f, 1f })
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                SafeDestroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(rampGo.transform, false);
                pole.transform.localPosition = new Vector3(side * (w * 0.5f), h + 0.8f, len * 0.5f);
                pole.transform.localScale = new Vector3(0.24f, 0.8f, 0.24f);
                pole.GetComponent<Renderer>().sharedMaterial = poleMat;

                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SafeDestroy(beacon.GetComponent<Collider>());
                beacon.transform.SetParent(rampGo.transform, false);
                beacon.transform.localPosition = new Vector3(side * (w * 0.5f), h + 1.65f, len * 0.5f);
                beacon.transform.localScale = Vector3.one * 0.55f;
                beacon.GetComponent<Renderer>().sharedMaterial = beaconMat;
            }

            // 4. スロープ先端の離陸リップフレーム（テイクオフエッジバー）
            float slopeAngle = Mathf.Atan2(h - 0.06f, len) * Mathf.Rad2Deg;
            float slopeLen = Mathf.Sqrt(len * len + (h - 0.06f) * (h - 0.06f));

            var lipGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SafeDestroy(lipGo.GetComponent<Collider>());
            lipGo.name = "TakeoffLip";
            lipGo.transform.SetParent(rampGo.transform, false);
            lipGo.transform.localPosition = new Vector3(0f, h + 0.04f, len * 0.5f);
            lipGo.transform.localRotation = Quaternion.Euler(-slopeAngle, 0f, 0f);
            lipGo.transform.localScale = new Vector3(w + 0.05f, 0.12f, 0.35f);
            lipGo.GetComponent<Renderer>().sharedMaterial = rampBaseMat;

            // 5. スロープ左右のサイドガードレール（走行安全バー）
            foreach (float side in new[] { -1f, 1f })
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SafeDestroy(rail.GetComponent<Collider>());
                rail.transform.SetParent(rampGo.transform, false);
                rail.transform.localPosition = new Vector3(side * (w * 0.5f + 0.1f), (h + 0.06f) * 0.5f + 0.15f, 0f);
                rail.transform.localRotation = Quaternion.Euler(-slopeAngle, 0f, 0f);
                rail.transform.localScale = new Vector3(0.18f, 0.35f, slopeLen);
                rail.GetComponent<Renderer>().sharedMaterial = rm.standMaterial;
            }

            // 最初のジャンプ台にアニメーション制御コンポーネントをアタッチ（共有マテリアルを一括駆動）
            if (r == 0)
            {
                var anim = rampGo.AddComponent<JumpRampAnimator>();
                anim.Init(rampSurfaceMat, beaconMat);
            }
        }
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


    void BuildScenery(RaceManager rm, TrackDef def)
    {
        var rng = new System.Random(11);
        int placed = 0;

        // スタイライズド 3D 自然モデル (Quaternius Stylized Nature Kit, CC0/MIT)
        var prefabBirch1 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_1");
        var prefabBirch2 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_2");
        var prefabNormal1 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_1");
        var prefabNormal2 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_2");
        var prefabPine1 = Resources.Load<GameObject>("Props/StylizedNature/PineTree_1");
        var prefabPine2 = Resources.Load<GameObject>("Props/StylizedNature/PineTree_2");
        var prefabPine3 = Resources.Load<GameObject>("Props/StylizedNature/PineTree_3");
        var prefabPalm1 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_1");
        var prefabPalm2 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_2");
        var prefabPalm3 = Resources.Load<GameObject>("Props/StylizedNature/PalmTree_3");
        var prefabDead1 = Resources.Load<GameObject>("Props/StylizedNature/DeadTree_1");
        var prefabDead2 = Resources.Load<GameObject>("Props/StylizedNature/DeadTree_2");
        var prefabDead3 = Resources.Load<GameObject>("Props/StylizedNature/DeadTree_3");
        var prefabBush = Resources.Load<GameObject>("Props/StylizedNature/Bush");
        var prefabBushFlowers = Resources.Load<GameObject>("Props/StylizedNature/Bush_Flowers");
        var prefabBushLarge = Resources.Load<GameObject>("Props/StylizedNature/Bush_Large");
        var prefabFlower1 = Resources.Load<GameObject>("Props/StylizedNature/Flower_1_Clump");
        var prefabFlower2 = Resources.Load<GameObject>("Props/StylizedNature/Flower_2_Clump");
        var prefabGrassLarge = Resources.Load<GameObject>("Props/StylizedNature/Grass_Large");
        var prefabRock1 = Resources.Load<GameObject>("Props/StylizedNature/Rock_1");
        var prefabRock2 = Resources.Load<GameObject>("Props/StylizedNature/Rock_2");
        var prefabRock3 = Resources.Load<GameObject>("Props/StylizedNature/Rock_3");
        var prefabRock4 = Resources.Load<GameObject>("Props/StylizedNature/Rock_4");
        var prefabRock5 = Resources.Load<GameObject>("Props/StylizedNature/Rock_5");

        // テクスチャ
        var texBirchBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Bark");
        var texBirchLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/BirchTree_Leaves");
        var texNormalBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Bark");
        var texNormalLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Leaves");
        var texPineBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Bark");
        var texPineLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PineTree_Leaves");
        var texPalmTrunk = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PalmTree_Trunk");
        var texPalmLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/PalmTree_Leaves");
        var texBushLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Bush_Leaves");
        var texFlowers = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Flowers");
        var texRocks = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks");
        var texRocksDesert = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Desert");
        var texRocksRedDesert = Resources.Load<Texture2D>("Props/StylizedNature/Textures/Rocks_Red_Desert");

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
            else if (def.SceneryTheme == 1) // Desert Dunes: ヤシの木、砂漠枯れ木、砂岩・赤岩
            {
                double r = rng.NextDouble();
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
            foreach (float side in new[] { -1f, 1f })
            {
                var p = PointAt(i, side * (WallOffset + 1.2f));
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
            float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 12)], Vector3.up);
            float side = bend > 2f ? -1f : 1f; // カーブ外側を優先
            var pos = PointAt(i, side * (WallOffset + 0.6f)) + Vector3.up * 1.9f;
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

        int[] bridgeSpots = { Count * 33 / 100, Count * 72 / 100 };
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
        var conePrefab = Resources.Load<GameObject>("Props/cone");
        var lightPost = Resources.Load<GameObject>("Props/lightPostLarge");
        var checkersFlag = Resources.Load<GameObject>("Props/flagCheckers");
        var tent = Resources.Load<GameObject>("Props/tent");

        // 1. 照明塔（北大キャンパスは深緑のクラシック街灯、その他はサーキットナイターポール）
        bool isCampus = def.SceneryTheme == 4;
        if (lightPost != null)
        {
            var poleMat = isCampus ? new Material(rm.wallMaterial) { color = new Color(0.14f, 0.22f, 0.16f) } : rm.chromeMaterial;
            if (isCampus) createdMaterials.Add(poleMat);
            for (int i = 0; i < Count; i += Count / 8)
            {
                var pos = PointAt(i, WallOffset + (isCampus ? 3.0f : 4.2f));
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
                var pos = PointAt(idx, WallOffset + 10f); // 壁の上の縁(+0.4m)にテントの屋根が食い込まないよう外側へ
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
                var pos = PointAt(0, side * (HalfWidth + 1.2f));
                var go = Instantiate(checkersFlag, pos, Quaternion.LookRotation(Dirs[0]), transform);
                go.transform.localScale = Vector3.one * 1.6f;
                StripColliders(go);
            }
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

// 激流・川の水面テクスチャを滑らかにスクロールさせるアニメーションコンポーネント
public class RiverFlowAnimator : MonoBehaviour
{
    public Material riverMat;

    void Update()
    {
        if (riverMat != null)
        {
            float vOffset = -Mathf.Repeat(Time.time * 0.75f, 1f);
            var offset = new Vector2(0f, vOffset);
            riverMat.mainTextureOffset = offset;
            riverMat.SetTextureOffset("_BaseMap", offset);
            riverMat.SetTextureOffset("_EmissionMap", offset);
        }
    }
}

// ジャンプ台のシェブロン矢印推進アニメーション＆警告ビーコンのストロボ点滅制御
public class JumpRampAnimator : MonoBehaviour
{
    Material slopeMat;
    Material beaconMat;

    public void Init(Material sMat, Material bMat)
    {
        slopeMat = sMat;
        beaconMat = bMat;
    }

    void Update()
    {
        // 1. スロープ面の矢印推進アニメーション (手前から奥・テイクオフ側へ滑らかに高速スクロール)
        if (slopeMat != null)
        {
            float vOffset = -Mathf.Repeat(Time.time * 1.6f, 1f);
            var offset = new Vector2(0f, vOffset);
            slopeMat.mainTextureOffset = offset;
            slopeMat.SetTextureOffset("_BaseMap", offset);
            slopeMat.SetTextureOffset("_EmissionMap", offset);
        }

        // 2. 警告ビーコンのストロボパルス点滅 (0.4秒周期でピカッと強く発光し、余韻を持って減衰)
        if (beaconMat != null)
        {
            float t = Mathf.Repeat(Time.time * 2.5f, 1f);
            float flash = Mathf.Pow(Mathf.Clamp01(1f - t), 2.2f);
            beaconMat.color = Color.Lerp(new Color(0.7f, 0.4f, 0.05f), new Color(1.8f, 1.4f, 0.25f), flash);
        }
    }
}

