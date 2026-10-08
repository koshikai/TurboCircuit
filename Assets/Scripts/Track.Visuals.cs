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
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

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
            : def.SceneryTheme == 3 ? TextureGen.Concrete() : TextureGen.Grass();
        var groundMat = new Material(rm.grassMaterial) { mainTexture = groundTex, color = Color.white };
        groundMat.mainTextureScale = new Vector2(160, 160);

        Strip("Road", new Vector2(-HalfWidth, 0.03f), new Vector2(HalfWidth, 0.03f), roadMat, HalfWidth * 2f);
        Strip("CurbL", new Vector2(-HalfWidth - CurbWidth, 0.05f), new Vector2(-HalfWidth, 0.05f), curbMat, 4f);
        Strip("CurbR", new Vector2(HalfWidth, 0.05f), new Vector2(HalfWidth + CurbWidth, 0.05f), curbMat, 4f);

        // 路肩
        var shoulderMat = new Material(rm.grassMaterial) { color = def.ShoulderColor, mainTexture = groundTex };
        Strip("ShoulderL", new Vector2(-WallOffset, 0.02f), new Vector2(-HalfWidth - CurbWidth, 0.02f), shoulderMat, 6f);
        Strip("ShoulderR", new Vector2(HalfWidth + CurbWidth, 0.02f), new Vector2(WallOffset, 0.02f), shoulderMat, 6f);

        // 壁は両面
        Strip("WallL", new Vector2(-WallOffset, 0f), new Vector2(-WallOffset, 1.2f), wallMat, 6f);
        Strip("WallL2", new Vector2(-WallOffset, 1.2f), new Vector2(-WallOffset, 0f), wallMat, 6f);
        Strip("WallR", new Vector2(WallOffset, 1.2f), new Vector2(WallOffset, 0f), wallMat, 6f);
        Strip("WallR2", new Vector2(WallOffset, 0f), new Vector2(WallOffset, 1.2f), wallMat, 6f);
        Strip("WallTopL", new Vector2(-WallOffset - 0.4f, 1.2f), new Vector2(-WallOffset, 1.2f), rm.chromeMaterial, 6f);
        Strip("WallTopR", new Vector2(WallOffset, 1.2f), new Vector2(WallOffset + 0.4f, 1.2f), rm.chromeMaterial, 6f);

        // 高架橋の底面（下から見上げたときに道路の裏が見えるように）
        Strip("RoadUnder", new Vector2(WallOffset, -0.35f), new Vector2(-WallOffset, -0.35f), rm.standMaterial, 8f);

        // 地面
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

        BuildStartLine(rm, def);
        BuildBoostPads(rm);
        PlanItemBoxes();
        BuildBridgePillars(rm);
        BuildJumpRamps(rm);
        BuildTunnels(rm, def);
        BuildScenery(rm, def);
        BuildSponsorBoards(rm);
        BuildCircuitProps(rm, def);
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

    // 高架橋・立体交差セクションの橋脚（ピラー）
    void BuildBridgePillars(RaceManager rm)
    {
        var pillarMat = rm.standMaterial;
        int step = 12;
        float groundY = -0.3f;
        float beamH = 1.4f;

        for (int i = 0; i < Count; i += step)
        {
            float y = Pts[i].y;
            if (y > 4.5f)
            {
                var p = Pts[i];

                // 道路底面（-0.35f）に接するように横ビームを配置
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(beam.GetComponent<Collider>());
                beam.name = "BridgeBeam_" + i;
                beam.transform.SetParent(transform, false);
                Vector3 beamPos = p - Normals[i] * (0.35f + beamH * 0.5f);
                beam.transform.position = beamPos;
                beam.transform.rotation = Quaternion.LookRotation(Dirs[i], Normals[i]);
                beam.transform.localScale = new Vector3(WallOffset * 1.8f, beamH, 3.8f);
                beam.GetComponent<Renderer>().sharedMaterial = pillarMat;

                // コンクリート橋脚（上端はビーム中心高さに接続し、路面を絶対に貫通しない）
                float topY = beamPos.y;
                float colH = topY - groundY;
                if (colH > 0.8f)
                {
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

    // ビルを貫通する屋根付き区間：天井・側壁・照明と、その上に載るビル本体
    void BuildTunnels(RaceManager rm, TrackDef def)
    {
        if (def.Tunnels == null) return;
        const float H = 10f;
        var concrete = new Material(rm.wallMaterial) { mainTexture = TextureGen.Concrete(), color = new Color(0.7f, 0.72f, 0.8f) };
        var lightMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.92f, 0.75f) };
        var neonA = new Material(rm.glowMaterial) { color = def.CurbColorA };
        var neonB = new Material(rm.glowMaterial) { color = def.CurbColorB };
        var facade = CityMaterials(rm);

        for (int t = 0; t < def.Tunnels.Length; t++)
        {
            int i0 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].Start * Count), 0, Count - 2);
            int i1 = Mathf.Clamp(Mathf.RoundToInt(def.Tunnels[t].End * Count), i0 + 1, Count - 1);
            int segs = i1 - i0;
            string id = "Tunnel" + t;

            Strip(id + "Ceiling", new Vector2(WallOffset, H), new Vector2(-WallOffset, H), concrete, 8f, i0, segs);
            // 側壁は両面
            Strip(id + "WallL", new Vector2(-WallOffset, 0f), new Vector2(-WallOffset, H), concrete, 8f, i0, segs);
            Strip(id + "WallL2", new Vector2(-WallOffset, H), new Vector2(-WallOffset, 0f), concrete, 8f, i0, segs);
            Strip(id + "WallR", new Vector2(WallOffset, H), new Vector2(WallOffset, 0f), concrete, 8f, i0, segs);
            Strip(id + "WallR2", new Vector2(WallOffset, 0f), new Vector2(WallOffset, H), concrete, 8f, i0, segs);

            // 壁際のネオンライン
            Strip(id + "NeonL", new Vector2(-WallOffset + 0.1f, 3.6f), new Vector2(-WallOffset + 0.1f, 4.1f), neonA, 8f, i0, segs);
            Strip(id + "NeonR", new Vector2(WallOffset - 0.1f, 4.1f), new Vector2(WallOffset - 0.1f, 3.6f), neonB, 8f, i0, segs);

            for (int i = i0; i <= i1; i += 4)
            {
                var rot = Quaternion.LookRotation(Dirs[i], Normals[i]);
                if ((i - i0) % 8 == 0)
                    Box("TunnelLight", Pts[i] + Normals[i] * (H - 0.15f), new Vector3(WallOffset * 1.2f, 0.2f, 0.8f), rot, lightMat);
            }

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

    // 都市テーマの街並み：道路に沿って摩天楼を敷き詰める
    void BuildCityScenery(RaceManager rm, TrackDef def, System.Random rng)
    {
        var facade = CityMaterials(rm);
        var placedB = new List<Vector3>(); // (x, z, 半径)
        var antennaMat = new Material(rm.glowMaterial) { color = new Color(1f, 0.2f, 0.25f) };
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
            placed++;
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

    // 立体ジャンプ台（カタパルトランプ）
    void BuildJumpRamps(RaceManager rm)
    {
        for (int r = 0; r < JumpRamps.Count; r++)
        {
            var ramp = JumpRamps[r];
            int i = ramp.index;
            var pos = Pts[i];
            var fwd = Dirs[i];
            var up = Normals[i];

            var rampGo = new GameObject("JumpRamp_" + r);
            rampGo.transform.SetParent(transform, false);
            rampGo.transform.position = pos;
            rampGo.transform.rotation = Quaternion.LookRotation(fwd, up);

            float w = HalfWidth * 1.7f;
            float len = 8.5f;
            float h = 1.6f;

            var rampMesh = new Mesh();
            rampMesh.name = "RampMesh_" + r;
            rampMesh.vertices = new[]
            {
                new Vector3(-w * 0.5f, 0.05f, -len * 0.5f), // 0: 手前左
                new Vector3(w * 0.5f, 0.05f, -len * 0.5f),  // 1: 手前右
                new Vector3(-w * 0.5f, h, len * 0.5f),      // 2: 奥左上
                new Vector3(w * 0.5f, h, len * 0.5f),       // 3: 奥右上
                new Vector3(-w * 0.5f, 0f, len * 0.5f),     // 4: 奥左下
                new Vector3(w * 0.5f, 0f, len * 0.5f)       // 5: 奥右下
            };
            rampMesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 4), new Vector2(1, 4),
                new Vector2(0, 0), new Vector2(1, 0)
            };
            rampMesh.triangles = new[]
            {
                0, 2, 1, 1, 2, 3,  // スロープ面
                2, 4, 3, 3, 4, 5,  // 背面
                0, 4, 2,           // 左側面
                1, 3, 5            // 右側面
            };
            rampMesh.RecalculateNormals();
            rampMesh.RecalculateBounds();

            rampGo.AddComponent<MeshFilter>().sharedMesh = rampMesh;
            var mr = rampGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rm.boostPadMaterial; // ネオンオレンジ発光

            // ランプ先端の両脇に警告サインポール
            var poleMat = rm.chromeMaterial;
            var poleL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(poleL.GetComponent<Collider>());
            poleL.transform.SetParent(rampGo.transform, false);
            poleL.transform.localPosition = new Vector3(-w * 0.5f, h + 0.9f, len * 0.5f);
            poleL.transform.localScale = new Vector3(0.24f, 0.9f, 0.24f);
            poleL.GetComponent<Renderer>().sharedMaterial = poleMat;

            var poleR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(poleR.GetComponent<Collider>());
            poleR.transform.SetParent(rampGo.transform, false);
            poleR.transform.localPosition = new Vector3(w * 0.5f, h + 0.9f, len * 0.5f);
            poleR.transform.localScale = new Vector3(0.24f, 0.9f, 0.24f);
            poleR.GetComponent<Renderer>().sharedMaterial = poleMat;
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

    void BuildStartLine(RaceManager rm, TrackDef def)
    {
        var checker = new Material(rm.roadMaterial) { mainTexture = TextureGen.Checker(), color = Color.white };
        checker.mainTexture.filterMode = FilterMode.Point;
        var rot = Quaternion.LookRotation(Dirs[0]);
        Box("StartLine", Pts[0] + Vector3.up * 0.04f, new Vector3(HalfWidth * 2f, 2.5f, 1), rot * Quaternion.Euler(90, 0, 0), checker, PrimitiveType.Quad);
        checker.mainTextureScale = new Vector2(12, 2);

        // スタートゲート
        foreach (float side in new[] { -1f, 1f })
            Box("GatePillar", PointAt(0, side * (HalfWidth + 3.5f)) + Vector3.up * 4.5f, new Vector3(1.2f, 9f, 1.2f), rot, rm.chromeMaterial);
        var beamMat = new Material(rm.roadMaterial) { mainTexture = TextureGen.Checker(), color = Color.white };
        beamMat.mainTexture.filterMode = FilterMode.Point;
        beamMat.mainTextureScale = new Vector2(16, 2);
        Box("GateBeam", Pts[0] + Vector3.up * 8.5f, new Vector3((HalfWidth + 3.5f) * 2f + 1.2f, 1.6f, 0.6f), rot, beamMat);

        // スタートシグナルランプ（赤・黄・青の3連ライト）
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

        // 観客席（Circuitテーマ時）
        if (def.SceneryTheme == 0)
        {
            for (int row = 0; row < 5; row++)
            {
                float h = 1.2f + row * 2.2f;
                var p = PointAt(12, -(WallOffset + 5f + row * 2f)) + Vector3.up * h * 0.5f;
                Box("Stand", p, new Vector3(2f, h, 60f), Quaternion.LookRotation(Dirs[12]), rm.standMaterial);
            }
            var rng = new System.Random(3);
            for (int i = 0; i < 90; i++)
            {
                int row = rng.Next(5);
                float along = (float)rng.NextDouble() * 56f - 28f;
                var basePos = PointAt(12, -(WallOffset + 5f + row * 2f)) + Dirs[12] * along;
                basePos.y = 1.2f + row * 2.2f + 0.35f;
                var fan = Box("Fan", basePos, Vector3.one * 0.6f, Quaternion.identity, rm.kartPaintMaterial, PrimitiveType.Sphere);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", Color.HSVToRGB((float)rng.NextDouble(), 0.7f, 1f));
                mpb.SetColor("_BaseColor", Color.HSVToRGB((float)rng.NextDouble(), 0.7f, 1f));
                fan.GetComponent<Renderer>().SetPropertyBlock(mpb);
                fan.AddComponent<Bouncer>().Init((float)rng.NextDouble() * 5f);
            }
        }
    }

    void BuildBoostPads(RaceManager rm)
    {
        var padMat = new Material(rm.boostPadMaterial) { mainTexture = TextureGen.Chevrons() };
        padMat.SetTexture("_EmissionMap", padMat.mainTexture);
        int[] spots = { Count * 22 / 100, Count * 48 / 100, Count * 81 / 100 };
        float[] lats = { 4.5f, -4.5f, 0f };
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
        int[] rows = { Count * 12 / 100, Count * 38 / 100, Count * 63 / 100, Count * 90 / 100 };
        foreach (int idx in rows)
            for (int k = -2; k <= 2; k++)
                ItemBoxSpots.Add((idx, k * 3.4f));
    }

    void BuildScenery(RaceManager rm, TrackDef def)
    {
        var rng = new System.Random(11);
        int placed = 0;

        var cactusMat = new Material(rm.leavesMaterial) { color = new Color(0.18f, 0.55f, 0.22f) };
        var sandstoneMat = new Material(rm.mountainMaterial) { color = new Color(0.85f, 0.55f, 0.32f) };
        var snowPineMat = new Material(rm.leavesMaterial) { color = new Color(0.15f, 0.35f, 0.25f) };
        var crystalMat = new Material(rm.glowMaterial) { color = new Color(0.4f, 0.85f, 1f, 0.7f) };
        var leafMats = new[]
        {
            rm.leavesMaterial,
            new Material(rm.leavesMaterial) { color = new Color(0.25f, 0.6f, 0.2f) },
            new Material(rm.leavesMaterial) { color = new Color(0.45f, 0.7f, 0.2f) },
        };

        bool city = def.SceneryTheme == 3;
        if (city) BuildCityScenery(rm, def, rng);

        for (int tries = 0; tries < 3000 && placed < 260 && !city; tries++)
        {
            var pos = new Vector3(
                Mathf.Lerp(bounds.min.x - 90, bounds.max.x + 90, (float)rng.NextDouble()), 0,
                Mathf.Lerp(bounds.min.z - 90, bounds.max.z + 90, (float)rng.NextDouble()));
            if (DistanceToTrack(pos) < WallOffset + 4f) continue;
            float s = 0.8f + (float)rng.NextDouble() * 0.9f;

            if (def.SceneryTheme == 0) // Circuit: スタンダードな木と茂み
            {
                if (rng.NextDouble() < 0.7)
                {
                    Box("Trunk", pos + Vector3.up * 1.5f * s, new Vector3(0.6f, 1.5f, 0.6f) * s, Quaternion.identity, rm.trunkMaterial, PrimitiveType.Cylinder);
                    Box("Leaves", pos + Vector3.up * 4f * s, new Vector3(3.6f, 3.2f, 3.6f) * s, Quaternion.identity, leafMats[rng.Next(3)], PrimitiveType.Sphere);
                    Box("Leaves2", pos + Vector3.up * 5.6f * s, new Vector3(2.4f, 2.2f, 2.4f) * s, Quaternion.identity, leafMats[rng.Next(3)], PrimitiveType.Sphere);
                }
                else
                {
                    bool rock = rng.NextDouble() < 0.4;
                    Box(rock ? "Rock" : "Bush", pos + Vector3.up * 0.4f * s, new Vector3(2.2f, 1.4f, 2f) * s,
                        Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), rock ? rm.mountainMaterial : leafMats[rng.Next(3)], PrimitiveType.Sphere);
                }
            }
            else if (def.SceneryTheme == 1) // Desert Dunes: サボテン、砂岩、ヤシの木
            {
                double r = rng.NextDouble();
                if (r < 0.55) // サボテン
                {
                    Box("CactusTrunk", pos + Vector3.up * 2.2f * s, new Vector3(0.7f, 2.2f, 0.7f) * s, Quaternion.identity, cactusMat, PrimitiveType.Cylinder);
                    var armL = Box("CactusArmL1", pos + Vector3.up * 2.6f * s - Vector3.right * 0.8f * s, new Vector3(0.8f, 0.45f, 0.45f) * s, Quaternion.identity, cactusMat, PrimitiveType.Cube);
                    Box("CactusArmL2", armL.transform.position + Vector3.up * 0.7f * s - Vector3.right * 0.2f * s, new Vector3(0.5f, 0.8f, 0.5f) * s, Quaternion.identity, cactusMat, PrimitiveType.Cylinder);
                    var armR = Box("CactusArmR1", pos + Vector3.up * 1.8f * s + Vector3.right * 0.8f * s, new Vector3(0.8f, 0.45f, 0.45f) * s, Quaternion.identity, cactusMat, PrimitiveType.Cube);
                    Box("CactusArmR2", armR.transform.position + Vector3.up * 0.6f * s + Vector3.right * 0.2f * s, new Vector3(0.5f, 0.7f, 0.5f) * s, Quaternion.identity, cactusMat, PrimitiveType.Cylinder);
                }
                else if (r < 0.85) // 砂岩の岩山・ピラミッド風
                {
                    float rh = 3f + (float)rng.NextDouble() * 5f;
                    Box("SandstoneRock", pos + Vector3.up * rh * 0.5f * s, new Vector3(4.5f * s, rh * s, 4.5f * s),
                        Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), sandstoneMat, PrimitiveType.Cube);
                }
                else // ヤシの木
                {
                    Box("PalmTrunk", pos + Vector3.up * 2.8f * s, new Vector3(0.5f, 2.8f, 0.5f) * s, Quaternion.Euler(6, (float)rng.NextDouble() * 360f, 0), rm.trunkMaterial, PrimitiveType.Cylinder);
                    Box("PalmCrown", pos + Vector3.up * 5.8f * s, new Vector3(4.8f, 0.8f, 4.8f) * s, Quaternion.identity, leafMats[1], PrimitiveType.Sphere);
                }
            }
            else // Frost Peak: スノーパイン、氷柱結晶
            {
                if (rng.NextDouble() < 0.65) // スノーパイン
                {
                    Box("PineTrunk", pos + Vector3.up * 1.2f * s, new Vector3(0.5f, 1.2f, 0.5f) * s, Quaternion.identity, rm.trunkMaterial, PrimitiveType.Cylinder);
                    Box("PineTier1", pos + Vector3.up * 2.8f * s, new Vector3(3.6f, 1.6f, 3.6f) * s, Quaternion.identity, snowPineMat, PrimitiveType.Sphere);
                    Box("PineTier2", pos + Vector3.up * 4.2f * s, new Vector3(2.6f, 1.4f, 2.6f) * s, Quaternion.identity, snowPineMat, PrimitiveType.Sphere);
                    Box("PineCap", pos + Vector3.up * 5.2f * s, new Vector3(1.6f, 1.0f, 1.6f) * s, Quaternion.identity, rm.snowMaterial, PrimitiveType.Sphere);
                }
                else // 氷柱・クリスタル
                {
                    float ch = 2.5f + (float)rng.NextDouble() * 3.5f;
                    Box("IceCrystal", pos + Vector3.up * ch * 0.5f * s, new Vector3(1.2f, ch, 1.2f) * s,
                        Quaternion.Euler((float)rng.NextDouble() * 12f - 6f, (float)rng.NextDouble() * 360f, 0), crystalMat, PrimitiveType.Cylinder);
                }
            }
            placed++;
        }

        // 遠くの山
        Material bgMountainMat = def.SceneryTheme == 1 ? sandstoneMat : (def.SceneryTheme == 2 ? rm.snowMaterial : rm.mountainMaterial);
        for (int i = 0; i < (city ? 0 : 22); i++)
        {
            float a = i / 22f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float r = 480f + (float)rng.NextDouble() * 120f;
            float h = 60f + (float)rng.NextDouble() * 90f;
            var p = bounds.center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Box("Mountain", p, new Vector3(h * 2.2f, h * 2f, h * 2.2f), Quaternion.identity, bgMountainMat, PrimitiveType.Sphere);
            if (def.SceneryTheme == 0 && h > 110f)
                Box("Snow", p + Vector3.up * h * 0.72f, new Vector3(h * 1.05f, h * 0.7f, h * 1.05f), Quaternion.identity, rm.snowMaterial, PrimitiveType.Sphere);
        }

        // 所々の旗
        for (int i = 0; i < Count; i += 25)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var p = PointAt(i, side * (WallOffset + 1.2f));
                Box("Pole", p + Vector3.up * 2.5f, new Vector3(0.15f, 2.5f, 0.15f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
                var flag = Box("Flag", p + Vector3.up * 4.3f + Dirs[i] * 0.6f, new Vector3(0.05f, 0.8f, 1.2f), Quaternion.LookRotation(Dirs[i]), rm.kartPaintMaterial);
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", Color.HSVToRGB((i / 25 % 6) / 6f, 0.8f, 1f));
                mpb.SetColor("_BaseColor", Color.HSVToRGB((i / 25 % 6) / 6f, 0.8f, 1f));
                flag.GetComponent<Renderer>().SetPropertyBlock(mpb);
            }
        }
    }

    void BuildSponsorBoards(RaceManager rm)
    {
        string dir = Application.dataPath + "/Resources/Banners/";
        var bannerTurbo = RaceManager.LoadTexture("Banners/banner_turbo", dir + "banner_turbo.jpg");
        var bannerNitro = RaceManager.LoadTexture("Banners/banner_nitro", dir + "banner_nitro.jpg");

        var customBanners = new[] { bannerTurbo, bannerNitro };
        var bannerMats = new Material[4];
        for (int v = 0; v < 4; v++)
        {
            var tex = (v < 2 && customBanners[v] != null) ? customBanners[v] : TextureGen.SponsorBanner(v);
            bannerMats[v] = new Material(rm.bannerMaterial ?? rm.wallMaterial)
            {
                mainTexture = tex
            };
        }

        // コース壁沿いのスポンサー看板
        for (int i = 18; i < Count - 15; i += 22)
        {
            float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 12)], Vector3.up);
            float side = bend > 2f ? -1f : 1f; // カーブ外側を優先
            var pos = PointAt(i, side * (WallOffset + 0.6f)) + Vector3.up * 1.6f;
            var rot = Quaternion.LookRotation(Dirs[i]);
            var mat = bannerMats[(i / 22) % 4];

            // 看板の支柱とボード
            Box("BoardPole1", pos - Dirs[i] * 1.8f - Vector3.up * 0.3f, new Vector3(0.18f, 1.3f, 0.18f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
            Box("BoardPole2", pos + Dirs[i] * 1.8f - Vector3.up * 0.3f, new Vector3(0.18f, 1.3f, 0.18f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
            Box("SponsorBoard", pos, new Vector3(0.15f, 1.3f, 4.6f), rot, mat);
        }

        // オーバーヘッド・ブリッジ看板（コースをまたぐ大型ゲート 2箇所）
        int[] bridgeSpots = { Count * 33 / 100, Count * 72 / 100 };
        for (int b = 0; b < bridgeSpots.Length; b++)
        {
            int idx = bridgeSpots[b];
            var center = Pts[idx] + Vector3.up * 7.5f;
            var rot = Quaternion.LookRotation(Dirs[idx]);
            var bridgeTex = b == 0 ? (bannerTurbo ?? customBanners[0]) : (bannerNitro ?? customBanners[1]);
            var mat = (bridgeTex != null)
                ? new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = bridgeTex }
                : bannerMats[(b + 1) % 4];

            // 左右の巨大支柱
            foreach (float side in new[] { -1f, 1f })
            {
                var pillarPos = PointAt(idx, side * (HalfWidth + 3.0f)) + Vector3.up * 4.0f;
                Box("BridgePillar", pillarPos, new Vector3(1.2f, 8.0f, 1.2f), rot, rm.chromeMaterial);
            }
            // 横梁
            Box("BridgeBeam", center, new Vector3((HalfWidth + 3.0f) * 2f + 1.2f, 2.0f, 0.8f), rot, mat);
        }
    }

    void BuildCircuitProps(RaceManager rm, TrackDef def)
    {
        var conePrefab = Resources.Load<GameObject>("Props/cone");
        var lightPost = Resources.Load<GameObject>("Props/lightPostLarge");
        var checkersFlag = Resources.Load<GameObject>("Props/flagCheckers");
        var tent = Resources.Load<GameObject>("Props/tent");

        // 1. 照明塔（サーキット全体を照らす大型ナイターポール 8箇所）
        if (lightPost != null)
        {
            var poleMat = rm.chromeMaterial;
            for (int i = 0; i < Count; i += Count / 8)
            {
                var pos = PointAt(i, WallOffset + 4.2f);
                var rot = Quaternion.LookRotation(-Rights[i], Vector3.up);
                var go = Instantiate(lightPost, pos, rot, transform);
                go.transform.localScale = Vector3.one * 1.5f;
                StripColliders(go);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = poleMat;
            }
        }

        // 2. コーナーのコーン（パイロン：イン側クリッピングポイント）
        if (conePrefab != null)
        {
            var coneMat = new Material(rm.boostPadMaterial) { color = def.SceneryTheme == 2 ? new Color(0.2f, 0.7f, 1f) : new Color(1f, 0.45f, 0.05f) };
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

        // 3. ピットテントとチェッカーフラッグ（スタート付近）
        if (tent != null)
        {
            var tentColor = def.SceneryTheme == 1 ? new Color(0.9f, 0.5f, 0.1f) : (def.SceneryTheme == 2 ? new Color(0.2f, 0.5f, 0.85f) : new Color(0.15f, 0.45f, 0.95f));
            var tentMat = new Material(rm.wallMaterial) { color = tentColor };
            for (int t = 1; t <= 3; t++)
            {
                int idx = Wrap(-t * 9);
                var pos = PointAt(idx, WallOffset + 10f); // 壁の上の縁(+0.4m)にテントの屋根が食い込まないよう外側へ
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
        if (checkersFlag != null)
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
        }
        return Mathf.Sqrt(best);
    }

}
