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
        var groundMat = new Material(rm.grassMaterial) { mainTexture = groundTex, color = def.GroundColor };
        groundMat.mainTextureScale = new Vector2(160, 160);

        Strip("Road", new Vector2(-HalfWidth, 0.03f), new Vector2(HalfWidth, 0.03f), roadMat, HalfWidth * 2f);
        Strip("CurbL", new Vector2(-HalfWidth - CurbWidth, 0.05f), new Vector2(-HalfWidth, 0.05f), curbMat, 4f);
        Strip("CurbR", new Vector2(HalfWidth, 0.05f), new Vector2(HalfWidth + CurbWidth, 0.05f), curbMat, 4f);

        // 路肩
        var shoulderMat = new Material(rm.grassMaterial) { color = def.ShoulderColor, mainTexture = groundTex };
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

        // 地面（コースの起伏に寄り添って自然に盛り上がるプロシージャル地形システム）
        BuildProceduralTerrain(rm, def, groundMat);

        BuildStartLine(rm, def);
        BuildBoostPads(rm);
        PlanItemBoxes();
        BuildBridgePillars(rm, def);
        BuildJumpRamps(rm);
        BuildTunnels(rm, def);
        BuildScenery(rm, def);
        BuildSponsorBoards(rm, def);
        BuildCircuitProps(rm, def);
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
                foreach (float side in new[] { -1f, 1f })
                {
                    var lpPos = PointAt(i, side * (WallOffset + 2.5f));
                    var lpRot = Quaternion.LookRotation(side * Rights[i], Vector3.up);
                    SpawnProp(prefabLightPost, lpPos, lpRot, Vector3.one * 1.5f, rm.standMaterial, (i % 14 == 0) ? neonCyanMat : neonPinkMat);
                }
            }
        }

        // コースサイドのサイバー街路樹（発光ネオンカラーを纏った幻想的な樹木）
        var prefabTree1 = Resources.Load<GameObject>("Props/StylizedNature/NormalTree_1");
        var prefabTree2 = Resources.Load<GameObject>("Props/StylizedNature/BirchTree_1");
        var texNormalLeaves = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Leaves");
        var texNormalBark = Resources.Load<Texture2D>("Props/StylizedNature/Textures/NormalTree_Bark");
        var cyberTrunkMat = MakeMat(rm.standMaterial, texNormalBark, new Color(0.20f, 0.22f, 0.30f));
        var cyberFoliageMat1 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.05f, 0.95f, 1.0f));
        var cyberFoliageMat2 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(1.0f, 0.22f, 0.88f));

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

        // 樹木マテリアル（手描きテクスチャ＋北海道の紅葉・黄葉ティント）
        var ginkgoTrunkMat = MakeMat(rm.trunkMaterial, texMapleBark, Color.white);
        var ginkgoLeafMatA = MakeMat(rm.leavesMaterial, texMapleLeaves ?? TextureGen.GinkgoFoliage(Color.yellow), new Color(1.0f, 0.88f, 0.16f));
        var ginkgoLeafMatB = MakeMat(rm.leavesMaterial, texMapleLeaves ?? TextureGen.GinkgoFoliage(Color.yellow), new Color(0.98f, 0.74f, 0.08f));
        var ginkgoCarpetMat = MakeMat(rm.leavesMaterial, TextureGen.GinkgoCarpet(), Color.white);

        var elmTrunkMat = MakeMat(rm.trunkMaterial, texNormalBark, Color.white);
        var elmLeafMat = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.85f, 1.0f, 0.75f));

        var birchTrunkMat = MakeMat(rm.wallMaterial, texBirchBark ?? TextureGen.BirchBark(), Color.white);
        var birchLeafMat = MakeMat(rm.leavesMaterial, texBirchLeaves, new Color(0.92f, 1.0f, 0.85f));

        var pineTrunkMat = MakeMat(rm.trunkMaterial, texPineBark, Color.white);
        var pineLeafMat = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.70f, 0.90f, 0.70f));

        var poplarTrunkMat = MakeMat(rm.trunkMaterial, texBirchBark ?? texNormalBark, Color.white);
        var poplarLeafMat = MakeMat(rm.leavesMaterial, texBirchLeaves ?? texNormalLeaves, new Color(0.78f, 0.96f, 0.72f));

        var waterMat = MakeMat(rm.boostPadMaterial, null, new Color(0.12f, 0.48f, 0.55f, 0.88f));
        var hayMat = MakeMat(rm.trunkMaterial, null, new Color(0.85f, 0.75f, 0.35f));
        var flowerMat = MakeMat(rm.leavesMaterial, texFlowers, Color.white);
        var bushLeafMat = MakeMat(rm.leavesMaterial, texBushLeaves, Color.white);
        var rockMat = MakeMat(rm.standMaterial, texRocks, Color.white);
        var cowWhiteMat = MakeMat(rm.wallMaterial, null, new Color(0.96f, 0.96f, 0.94f));

        // 1. クラーク博士胸像（William S. Clark Memorial Statue）- 中央ローン広場
        int clarkIdx = (int)(0.12f * Count);
        Vector3 clarkPos = PointAt(clarkIdx, -(WallOffset + 12f));
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
            var fPrefab = (Mathf.Abs(f) % 2 == 0) ? (prefabFlower1 ?? prefabFlower2) : (prefabBushFlowers ?? prefabBush);
            if (fPrefab != null)
                SpawnProp(fPrefab, fPos, Quaternion.Euler(0, f * 55, 0), Vector3.one * 1.5f, bushLeafMat, flowerMat);
        }
        SpawnProp(prefabBushFlowers ?? prefabBush, clarkPos + clarkRot * new Vector3(-3.8f, 0, 2.5f), Quaternion.identity, Vector3.one * 1.8f, bushLeafMat, flowerMat);
        SpawnProp(prefabBushFlowers ?? prefabBush, clarkPos + clarkRot * new Vector3(3.8f, 0, 2.5f), Quaternion.identity, Vector3.one * 1.8f, bushLeafMat, flowerMat);

        // クラーク像を取り囲む雄大なエルムの大樹（Quaternius NormalTree）
        SpawnNatureProp(prefabNormal1 ?? prefabNormal2, clarkPos + clarkRot * new Vector3(-10f, 0, -5f), Quaternion.Euler(0, 45, 0), Vector3.one * 4.2f, elmTrunkMat, elmLeafMat);
        SpawnNatureProp(prefabNormal2 ?? prefabNormal1, clarkPos + clarkRot * new Vector3(10f, 0, -5f), Quaternion.Euler(0, 120, 0), Vector3.one * 4.0f, elmTrunkMat, elmLeafMat);

        // 2. 古河記念講堂（Furukawa Memorial Hall）- アメリカン・ルネサンス様式の木造洋風校舎
        int furukawaIdx = (int)(0.155f * Count);
        Vector3 furukawaPos = PointAt(furukawaIdx, -(WallOffset + 24f));
        Quaternion furukawaRot = Quaternion.LookRotation(Rights[furukawaIdx], Vector3.up);
        Box("FurukawaBase", furukawaPos + Vector3.up * 5.5f, new Vector3(36f, 11f, 18f), furukawaRot, woodWhiteMat);
        Box("FurukawaPorch", furukawaPos + furukawaRot * new Vector3(0, 6f, 10f), new Vector3(10f, 12f, 3.5f), furukawaRot, woodWhiteMat);
        Box("FurukawaRoofLower", furukawaPos + Vector3.up * 12f, new Vector3(37f, 4f, 19f), furukawaRot, roofGreenMat);
        Box("FurukawaRoofUpper", furukawaPos + Vector3.up * 14.5f, new Vector3(28f, 2.5f, 13f), furukawaRot, roofGreenMat);
        Vector3 cupolaPos = furukawaPos + Vector3.up * 16.5f;
        Box("FurukawaCupolaBase", cupolaPos, new Vector3(4.0f, 4.0f, 4.0f), furukawaRot, woodWhiteMat, PrimitiveType.Cylinder);
        Box("FurukawaCupolaRoof", cupolaPos + Vector3.up * 3.2f, new Vector3(4.4f, 3.6f, 4.4f), furukawaRot, roofGreenMat, PrimitiveType.Cylinder);
        for (int b = -3; b <= 3; b += 2)
            SpawnProp((b % 4 == 0 ? prefabBushFlowers : prefabBushLarge) ?? prefabBush, furukawaPos + furukawaRot * new Vector3(b * 4.5f, 0, 11f), Quaternion.identity, Vector3.one * 1.6f, bushLeafMat, flowerMat);

        // 3. 北大総合博物館（HU Museum）- 東側（外側）の赤レンガ重厚校舎
        int museumIdx = (int)(0.22f * Count);
        Vector3 museumPos = PointAt(museumIdx, WallOffset + 26f);
        Quaternion museumRot = Quaternion.LookRotation(-Rights[museumIdx], Vector3.up);
        Box("MuseumWingL", museumPos + museumRot * new Vector3(-19f, 7f, 0), new Vector3(19f, 14f, 17f), museumRot, brickMuseumMat);
        Box("MuseumCenter", museumPos + Vector3.up * 9.5f, new Vector3(20f, 19f, 21f), museumRot, brickMuseumMat);
        Box("MuseumWingR", museumPos + museumRot * new Vector3(19f, 7f, 0), new Vector3(19f, 14f, 17f), museumRot, brickMuseumMat);
        Box("MuseumTower", museumPos + Vector3.up * 20.5f, new Vector3(8.5f, 5.0f, 8.5f), museumRot, brickMuseumMat);
        // エントランス前の白樺（Quaternius BirchTree）
        SpawnNatureProp(prefabBirch1 ?? prefabBirch2, museumPos + museumRot * new Vector3(-8f, 0, 13f), Quaternion.identity, Vector3.one * 3.2f, birchTrunkMat, birchLeafMat);
        SpawnNatureProp(prefabBirch2 ?? prefabBirch1, museumPos + museumRot * new Vector3(8f, 0, 13f), Quaternion.identity, Vector3.one * 3.2f, birchTrunkMat, birchLeafMat);

        // 4. 大野池（Ono Pond）- 西側（内側）の睡蓮の池、木造アーチ橋、自然岩
        Vector3 pondPos = PointAt(museumIdx, -(WallOffset + 18f));
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
            var rPrefab = (r % 3 == 0) ? prefabRock1 : ((r % 3 == 1) ? prefabRock2 : prefabRock3);
            if (rPrefab != null)
                SpawnProp(rPrefab, rPos, Quaternion.Euler(0, r * 45f, 0), Vector3.one * (1.8f + (r % 3) * 0.4f), rockMat, rockMat);
        }
        Box("PondBench", pondPos + new Vector3(16f, 0.4f, -4f), new Vector3(2.6f, 0.8f, 0.8f), Quaternion.Euler(0, -20f, 0), rm.trunkMaterial);
        SpawnNatureProp(prefabNormal1 ?? prefabNormal2, pondPos + new Vector3(-15f, 0, 9f), Quaternion.identity, Vector3.one * 4.0f, elmTrunkMat, elmLeafMat);

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
                Quaternion leanRot = Quaternion.AngleAxis(-side * 14f, Dirs[i]) * Quaternion.Euler(0, (i * 37) % 360, 0);
                SpawnNatureProp(gTreePrefab ?? prefabNormal1, treePos1, leanRot, Vector3.one * s, ginkgoTrunkMat, gMat);

                // 外側列（並木の厚み・二重列）
                if (i % 5 == 0)
                {
                    Vector3 treePos2 = PointAt(i, side * (WallOffset + 8.8f));
                    Quaternion rot2 = Quaternion.Euler(0, (i * 53) % 360, 0);
                    var gTree2 = (i % 2 == 0) ? prefabMaple2 : prefabMaple3;
                    SpawnNatureProp(gTree2 ?? prefabNormal2, treePos2, rot2, Vector3.one * (s * 1.15f), ginkgoTrunkMat, (i % 4 == 0) ? ginkgoLeafMatB : ginkgoLeafMatA);
                }

                // イチョウの足元の草花（ふんわり咲く花と黄金の草むら）
                if (i % 6 == 0)
                {
                    Vector3 fPos = PointAt(i, side * (WallOffset + 2.4f));
                    if (prefabFlower1 != null)
                        SpawnProp(prefabFlower1, fPos, Quaternion.Euler(0, i * 29, 0), Vector3.one * 1.4f, bushLeafMat, flowerMat);
                    if (prefabGrassLarge != null)
                        SpawnProp(prefabGrassLarge, fPos + Vector3.forward * 1.2f, Quaternion.identity, Vector3.one * 1.4f, ginkgoCarpetMat);
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
        Box("BarnSiloBody", siloPos + Vector3.up * 8.5f, new Vector3(8.0f, 17f, 8.0f), Quaternion.identity, woodWhiteMat, PrimitiveType.Cylinder);
        Box("BarnSiloCap", siloPos + Vector3.up * 18.5f, new Vector3(8.6f, 4.5f, 8.6f), Quaternion.identity, siloRoofMat, PrimitiveType.Cylinder);
        for (int h = 0; h < 6; h++)
        {
            Vector3 hayPos = siloPos + new Vector3(-9f + h * 3.2f, 1.2f, 9f);
            Box("HayBale", hayPos, new Vector3(2.3f, 2.1f, 2.3f), Quaternion.Euler(0, h * 30f, 90f), hayMat, PrimitiveType.Cylinder);
        }
        if (prefabLogStack != null)
            SpawnProp(prefabLogStack, siloPos + new Vector3(10f, 0, -4f), Quaternion.Euler(0, 45, 0), Vector3.one * 2.2f, rm.trunkMaterial);
        if (prefabStump != null)
            SpawnProp(prefabStump, siloPos + new Vector3(8f, 0, 6f), Quaternion.identity, Vector3.one * 2.0f, rm.trunkMaterial);

        // 牧草地の木柵モデルと放牧ホルスタイン牛
        int pastureStart = (int)(0.68f * Count);
        int pastureEnd = (int)(0.76f * Count);
        for (int i = pastureStart; i <= pastureEnd; i += 6)
        {
            Vector3 fencePos = PointAt(i, -(WallOffset + 5.5f));
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
                Quaternion rot = Quaternion.Euler(0, (i * 29) % 360, 0);
                SpawnNatureProp(popPrefab ?? prefabBirch1, pPos, rot, new Vector3(s * 0.65f, s * 1.85f, s * 0.65f), poplarTrunkMat, poplarLeafMat);
                if (prefabGrassLarge != null && i % 8 == 0)
                    SpawnProp(prefabGrassLarge, pPos + Vector3.forward * 1.5f, Quaternion.identity, Vector3.one * 1.5f, bushLeafMat);
            }
        }

        // 9. メインストリート沿いのクラシック街灯（Street Lamps）と木造案内看板（Signs）
        for (int i = 0; i < Count; i += 12)
        {
            if (i >= ginkgoStart && i <= ginkgoEnd) continue;
            Vector3 lampPos = PointAt(i, WallOffset + 2.4f);
            Quaternion lRot = Quaternion.LookRotation(Dirs[i], Vector3.up);
            if (prefabLampPost != null)
                SpawnProp(prefabLampPost, lampPos, lRot, Vector3.one * 1.2f, rm.standMaterial, rm.glowMaterial);

            if (i % 36 == 0 && prefabSign != null)
            {
                Vector3 sPos = PointAt(i, -(WallOffset + 2.6f));
                SpawnProp(prefabSign, sPos, Quaternion.LookRotation(-Rights[i], Vector3.up), Vector3.one * 1.8f, rm.trunkMaterial);
            }
        }

        // 10. コース全域の路肩を彩るスタイライズド草花・低木・小岩（Quaternius assets）
        for (int i = 0; i < Count; i += 3)
        {
            float side = (i % 2 == 0) ? 1f : -1f;
            float offset = WallOffset + 2.5f + (float)rng.NextDouble() * 5.0f;
            Vector3 pos = PointAt(i, side * offset);
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
            createdMeshes.Add(rampMesh);
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

    Material MakeMat(Material baseMat, Texture2D tex, Color tint)
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
        Box("GateBeam", Pts[0] + Vector3.up * 8.5f, new Vector3((HalfWidth + 3.5f) * 2f + 1.2f, isCampus ? 2.2f : 1.6f, 0.6f), rot, beamMat);

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
        createdMaterials.Add(padMat);
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
        if (theme == 3 || theme == 4) return; // Cityは摩天楼、Campusは札幌山系

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
        var circuitLeavesMat1 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.38f, 0.88f, 0.28f));
        var circuitLeavesMat2 = MakeMat(rm.leavesMaterial, texBirchLeaves, new Color(0.48f, 0.94f, 0.32f));
        var circuitLeavesMat3 = MakeMat(rm.leavesMaterial, texNormalLeaves, new Color(0.22f, 0.72f, 0.22f));
        var circuitBushMat = MakeMat(rm.leavesMaterial, texBushLeaves, Color.white);
        var circuitFlowerMat = MakeMat(rm.leavesMaterial, texFlowers, Color.white);
        var circuitRockMat = MakeMat(rm.standMaterial, texRocks, Color.white);

        // Theme 1 (Sunset Dunes): 夕焼け砂漠、ヤシの木、枯れ木、砂岩
        var palmTrunkMat = MakeMat(rm.trunkMaterial, texPalmTrunk, Color.white);
        var palmLeafMat = MakeMat(rm.leavesMaterial, texPalmLeaves, new Color(0.92f, 0.96f, 0.35f));
        var deadTreeMat = MakeMat(rm.trunkMaterial, texNormalBark, new Color(0.68f, 0.60f, 0.50f));
        var desertRockMat1 = MakeMat(rm.mountainMaterial, texRocksDesert ?? texRocks, new Color(1.05f, 0.85f, 0.65f));
        var desertRockMat2 = MakeMat(rm.mountainMaterial, texRocksRedDesert ?? texRocksDesert ?? texRocks, new Color(1.15f, 0.72f, 0.50f));

        // Theme 2 (Frost Peak): 白銀の雪山、氷河、積雪針葉樹
        var frostTrunkMat = MakeMat(rm.trunkMaterial, texPineBark, new Color(0.55f, 0.60f, 0.68f));
        var frostLeafMat1 = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.85f, 0.96f, 1.0f));
        var frostLeafMat2 = MakeMat(rm.leavesMaterial, texPineLeaves, new Color(0.50f, 0.75f, 0.70f));
        var frostRockMat = MakeMat(rm.mountainMaterial, texRocks, new Color(0.82f, 0.90f, 0.98f));
        var frostBushMat = MakeMat(rm.leavesMaterial, texBushLeaves, new Color(0.88f, 0.94f, 1.0f));
        var crystalMat = new Material(rm.glowMaterial) { color = new Color(0.35f, 0.88f, 1.0f, 0.82f) };
        createdMaterials.Add(crystalMat);

        bool city = def.SceneryTheme == 3;
        if (city) BuildCityScenery(rm, def, rng);

        bool campus = def.SceneryTheme == 4;
        if (campus) BuildCampusScenery(rm, def, rng);

        // 自然物・木・岩の配置ループ（起伏に富んだプロシージャル地表に追従）
        for (int tries = 0; tries < 3500 && placed < 280 && !city && !campus; tries++)
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
            var tex = isCampus ? TextureGen.CampusBanner(v) : ((v < 2 && customBanners[v] != null) ? customBanners[v] : TextureGen.SponsorBanner(v));
            bannerMats[v] = new Material(rm.bannerMaterial ?? rm.wallMaterial)
            {
                mainTexture = tex
            };
            createdMaterials.Add(bannerMats[v]);
        }

        var poleMat = isCampus ? new Material(rm.wallMaterial) { color = new Color(0.12f, 0.38f, 0.22f) } : rm.chromeMaterial;
        if (isCampus) createdMaterials.Add(poleMat);

        // コース壁沿いのスポンサー/キャンパス案内看板
        for (int i = 18; i < Count - 15; i += 22)
        {
            float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 12)], Vector3.up);
            float side = bend > 2f ? -1f : 1f; // カーブ外側を優先
            var pos = PointAt(i, side * (WallOffset + 0.6f)) + Vector3.up * 1.6f;
            var rot = Quaternion.LookRotation(Dirs[i]);
            var mat = bannerMats[(i / 22) % 4];

            // 看板の支柱とボード
            Box("BoardPole1", pos - Dirs[i] * 1.8f - Vector3.up * 0.3f, new Vector3(0.18f, 1.3f, 0.18f), Quaternion.identity, poleMat, PrimitiveType.Cylinder);
            Box("BoardPole2", pos + Dirs[i] * 1.8f - Vector3.up * 0.3f, new Vector3(0.18f, 1.3f, 0.18f), Quaternion.identity, poleMat, PrimitiveType.Cylinder);
            Box("SponsorBoard", pos, new Vector3(0.15f, 1.3f, 4.6f), rot, mat);
        }

        // オーバーヘッド・ブリッジ看板（コースをまたぐ大型ゲート 2箇所）
        Material[] overheadMats = new Material[2];
        if (!isCampus)
        {
            if (bannerTurbo != null) { overheadMats[0] = new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = bannerTurbo }; createdMaterials.Add(overheadMats[0]); }
            if (bannerNitro != null) { overheadMats[1] = new Material(rm.bannerMaterial ?? rm.wallMaterial) { mainTexture = bannerNitro }; createdMaterials.Add(overheadMats[1]); }
        }
        int[] bridgeSpots = { Count * 33 / 100, Count * 72 / 100 };
        for (int b = 0; b < bridgeSpots.Length; b++)
        {
            int idx = bridgeSpots[b];
            var center = Pts[idx] + Vector3.up * 7.5f;
            var rot = Quaternion.LookRotation(Dirs[idx]);
            var mat = isCampus ? bannerMats[b % 2] : ((b < 2 && overheadMats[b] != null) ? overheadMats[b] : bannerMats[(b + 1) % 4]);

            // 左右の巨大支柱
            foreach (float side in new[] { -1f, 1f })
            {
                var pillarPos = PointAt(idx, side * (HalfWidth + 3.0f)) + Vector3.up * 4.0f;
                Box("BridgePillar", pillarPos, new Vector3(1.2f, 8.0f, 1.2f), rot, poleMat);
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
        }
        return Mathf.Sqrt(best);
    }

    public float GetTerrainHeight(float x, float z, TrackDef def)
    {
        float baseGroundY = -0.3f;
        if (Count == 0 || Pts == null) return baseGroundY;

        int theme = def.SceneryTheme;
        if (theme == 3) // Neon Metropolis: 都市は平坦なアスファルト
        {
            return baseGroundY;
        }

        // コース上の最近傍点を高速探索（ステップ2で絞り込み、近傍を精査）
        float bestSqrDist = float.MaxValue;
        int bestIdx = 0;
        for (int i = 0; i < Count; i += 2)
        {
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
        if (theme == 3) // Neon Metropolis (都市) は平坦なQuad
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

    // コースに生き生きとした息吹を与えるスタイライズドアニマルシステム (Kenney Cube Pets, CC0)
    void BuildAnimals(RaceManager rm, TrackDef def)
    {
        string dir = Application.dataPath + "/Resources/Animals/";
        var colormapTex = RaceManager.LoadTexture("Animals/Textures/colormap", dir + "Textures/colormap.png");
        if (colormapTex == null) return;

        var animalMat = new Material(rm.wallMaterial)
        {
            mainTexture = colormapTex,
            color = Color.white
        };
        animalMat.SetColor("_BaseColor", Color.white);
        animalMat.SetFloat("_Smoothness", 0.15f);
        createdMaterials.Add(animalMat);

        var prefabBunny = Resources.Load<GameObject>("Animals/animal-bunny");
        var prefabDeer = Resources.Load<GameObject>("Animals/animal-deer");
        var prefabPenguin = Resources.Load<GameObject>("Animals/animal-penguin");
        var prefabPolar = Resources.Load<GameObject>("Animals/animal-polar");
        var prefabFox = Resources.Load<GameObject>("Animals/animal-fox");
        var prefabLion = Resources.Load<GameObject>("Animals/animal-lion");
        var prefabCrab = Resources.Load<GameObject>("Animals/animal-crab");
        var prefabCat = Resources.Load<GameObject>("Animals/animal-cat");
        var prefabDog = Resources.Load<GameObject>("Animals/animal-dog");
        var prefabCow = Resources.Load<GameObject>("Animals/animal-cow");
        var prefabBird = Resources.Load<GameObject>("Animals/animal-parrot");
        var prefabChick = Resources.Load<GameObject>("Animals/animal-chick");

        var rng = new System.Random(def.SceneryTheme * 777 + 42);
        int theme = def.SceneryTheme;

        // 1. 全コース共通：空を優雅に飛ぶ鳥（旋回飛行）
        if (prefabBird != null)
        {
            int birdFlocks = (theme == 3) ? 1 : 2;
            for (int f = 0; f < birdFlocks; f++)
            {
                int spotIdx = (int)(Count * ((f * 0.45f + 0.2f) % 1f));
                Vector3 center = Pts[spotIdx];
                float radius = 70f + f * 40f;
                float altitude = (theme == 3 ? 42f : 24f) + f * 6f;
                float speed = 18f + f * 3f;
                for (int b = 0; b < 3; b++)
                {
                    var birdGo = SpawnFlyingBird(prefabBird, center + new Vector3(b * 4f, 0, b * 3f), radius + b * 5f, altitude + b * 1.5f, speed, animalMat);
                    if (birdGo != null) birdGo.transform.localScale = Vector3.one * 1.5f;
                }
            }
        }

        // 2. テーマ別地上動物の配置
        if (theme == 0) // TURBO CIRCUIT: 豊かな芝生丘陵
        {
            // スタート直後（右側テント横）のウサギ応援スポット（HopBounce）
            if (prefabBunny != null)
            {
                // スタート直後の特等席
                for (int b = 0; b < 3; b++)
                {
                    Vector3 p = PointAt(7 + b * 2, WallOffset + 4.2f + (float)rng.NextDouble() * 2.5f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    SpawnAnimal(prefabBunny, p, Quaternion.Euler(0, -70f + (float)rng.NextDouble() * 30f, 0), 1.5f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
                // コース全体の群れ
                for (int g = 1; g < 4; g++)
                {
                    int idx = Wrap(Count * g / 4 + 18);
                    float side = (g % 2 == 0) ? 1f : -1f;
                    Vector3 basePos = PointAt(idx, side * (WallOffset + 5.5f + (float)rng.NextDouble() * 6f));
                    for (int b = 0; b < 3; b++)
                    {
                        Vector3 p = basePos + new Vector3((float)rng.NextDouble() * 4f - 2f, 0, (float)rng.NextDouble() * 4f - 2f);
                        p.y = GetTerrainHeight(p.x, p.z, def);
                        Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                        SpawnAnimal(prefabBunny, p, rot, 1.4f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                    }
                }
            }

            // シカ（草を食む IdleGraze）
            if (prefabDeer != null)
            {
                // スタート直後の木陰
                Vector3 p0 = PointAt(14, WallOffset + 12f);
                p0.y = GetTerrainHeight(p0.x, p0.z, def);
                SpawnAnimal(prefabDeer, p0, Quaternion.Euler(0, -110f, 0), 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int d = 1; d < 3; d++)
                {
                    int idx = Wrap(Count * d / 3 + 35);
                    float side = (d % 2 == 0) ? -1f : 1f;
                    Vector3 p = PointAt(idx, side * (WallOffset + 14f + (float)rng.NextDouble() * 10f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabDeer, p, rot, 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // ヒヨコ（スタート左側芝生・花壇近く）
            if (prefabChick != null)
            {
                for (int c = 0; c < 4; c++)
                {
                    Vector3 p = PointAt(6 + c * 2, -(WallOffset + 3.8f + (float)rng.NextDouble() * 2f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    SpawnAnimal(prefabChick, p, Quaternion.Euler(0, 80f + (float)rng.NextDouble() * 40f, 0), 1.3f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
            }
        }
        else if (theme == 1) // SUNSET DUNES: 夕焼け砂漠・キャニオン
        {
            // スタート直後の岩の上に砂漠のライオン（IdleGraze）
            if (prefabLion != null)
            {
                Vector3 pStart = PointAt(10, WallOffset + 6.5f);
                pStart.y = GetTerrainHeight(pStart.x, pStart.z, def);
                SpawnAnimal(prefabLion, pStart, Quaternion.Euler(0, -85f, 0), 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int l = 1; l < 3; l++)
                {
                    int idx = Wrap(Count * l / 3 + 30);
                    Vector3 p = PointAt(idx, -(WallOffset + 14f + l * 6f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.LookRotation(Dirs[idx], Vector3.up);
                    SpawnAnimal(prefabLion, p, rot, 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // 砂漠キツネ（LookAround）
            if (prefabFox != null)
            {
                // スタート直後の左側
                Vector3 pFox = PointAt(7, -(WallOffset + 5f));
                pFox.y = GetTerrainHeight(pFox.x, pFox.z, def);
                SpawnAnimal(prefabFox, pFox, Quaternion.Euler(0, 75f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int f = 1; f < 4; f++)
                {
                    int idx = Wrap(Count * f / 4 + 22);
                    float side = (f % 2 == 0) ? 1f : -1f;
                    Vector3 p = PointAt(idx, side * (WallOffset + 8f + (float)rng.NextDouble() * 8f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabFox, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }

            // カニ（砂地の横歩き CrabSidestep）
            if (prefabCrab != null)
            {
                // スタート直後
                Vector3 pCrab = PointAt(12, WallOffset + 4.2f);
                pCrab.y = GetTerrainHeight(pCrab.x, pCrab.z, def);
                SpawnAnimal(prefabCrab, pCrab, Quaternion.Euler(0, -90f, 0), 1.8f, animalMat, AnimalBehavior.BehaviorType.CrabSidestep);

                for (int c = 1; c < 5; c++)
                {
                    int idx = Wrap(Count * c / 5 + 18);
                    Vector3 p = PointAt(idx, WallOffset + 5f + (float)rng.NextDouble() * 6f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.LookRotation(-Rights[idx], Vector3.up);
                    SpawnAnimal(prefabCrab, p, rot, 1.8f, animalMat, AnimalBehavior.BehaviorType.CrabSidestep);
                }
            }
        }
        else if (theme == 2) // FROST PEAK: 白銀の雪山・氷河
        {
            // ペンギン応援団（PenguinWaddle）！スタート直後・コーナー外側にずらり
            if (prefabPenguin != null)
            {
                // スタート直後の特等席！右側フェンス際によちよち並ぶ
                for (int p = 0; p < 4; p++)
                {
                    Vector3 pos = PointAt(6 + p * 2, WallOffset + 3.6f);
                    pos.y = GetTerrainHeight(pos.x, pos.z, def);
                    Quaternion rot = Quaternion.Euler(0, -90f + (p % 2 == 0 ? 15f : -15f), 0);
                    SpawnAnimal(prefabPenguin, pos, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.PenguinWaddle);
                }

                // コース全体の群れ
                for (int g = 1; g < 4; g++)
                {
                    int idx = Wrap(Count * g / 4 + 16);
                    float side = (g % 2 == 0) ? 1f : -1f;
                    Vector3 basePos = PointAt(idx, side * (WallOffset + 4.5f + (float)rng.NextDouble() * 3f));
                    for (int p = 0; p < 3; p++)
                    {
                        Vector3 pos = basePos + new Vector3((p % 2) * 2.2f - 1.1f, 0, (p / 2) * 2.2f);
                        pos.y = GetTerrainHeight(pos.x, pos.z, def);
                        Quaternion rot = Quaternion.LookRotation(-side * Rights[idx], Vector3.up) * Quaternion.Euler(0, (float)rng.NextDouble() * 24f - 12f, 0);
                        SpawnAnimal(prefabPenguin, pos, rot, 1.6f, animalMat, AnimalBehavior.BehaviorType.PenguinWaddle);
                    }
                }
            }

            // シロクマ / ホッキョクグマ（IdleGraze）
            if (prefabPolar != null)
            {
                // スタート直後の左側雪丘
                Vector3 pPolar0 = PointAt(10, -(WallOffset + 9f));
                pPolar0.y = GetTerrainHeight(pPolar0.x, pPolar0.z, def);
                SpawnAnimal(prefabPolar, pPolar0, Quaternion.Euler(0, 95f, 0), 2.5f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);

                for (int pb = 1; pb < 3; pb++)
                {
                    int idx = Wrap(Count * pb / 3 + 28);
                    float side = (pb % 2 == 0) ? -1f : 1f;
                    Vector3 p = PointAt(idx, side * (WallOffset + 16f + (float)rng.NextDouble() * 10f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabPolar, p, rot, 2.5f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // 雪山のキタキツネ（LookAround）
            if (prefabFox != null)
            {
                for (int f = 0; f < 3; f++)
                {
                    int idx = Wrap(Count * f / 3 + 24);
                    Vector3 p = PointAt(idx, WallOffset + 8f + (float)rng.NextDouble() * 6f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabFox, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }
        }
        else if (theme == 3) // NEON METROPOLIS: サイバー未来都市
        {
            // サイバーキャット（LookAround）- ビルのエントランスやサイバーツリーのふもと
            if (prefabCat != null)
            {
                Vector3 pCat0 = PointAt(8, WallOffset + 3.8f);
                pCat0.y = -0.3f;
                SpawnAnimal(prefabCat, pCat0, Quaternion.Euler(0, -80f, 0), 1.6f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int c = 1; c < 4; c++)
                {
                    int idx = Wrap(Count * c / 4 + 16);
                    Vector3 p = PointAt(idx, WallOffset + 4.2f + (float)rng.NextDouble() * 3f);
                    p.y = -0.3f;
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabCat, p, rot, 1.6f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }

            // サイバードッグ（LookAround）- 歩道沿い
            if (prefabDog != null)
            {
                Vector3 pDog0 = PointAt(12, -(WallOffset + 4.0f));
                pDog0.y = -0.3f;
                SpawnAnimal(prefabDog, pDog0, Quaternion.Euler(0, 85f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);

                for (int d = 1; d < 3; d++)
                {
                    int idx = Wrap(Count * d / 3 + 32);
                    Vector3 p = PointAt(idx, -(WallOffset + 4.5f + (float)rng.NextDouble() * 3f));
                    p.y = -0.3f;
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabDog, p, rot, 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
                }
            }
        }
        else if (theme == 4) // HOKKAIDO CAMPUS: 北海道大学キャンパス
        {
            // ホルスタイン牛（第2農場の放牧地）- キュートなKenney Cow！
            if (prefabCow != null)
            {
                int pastureStart = (int)(0.48f * Count);
                int pastureEnd = (int)(0.63f * Count);
                for (int i = pastureStart; i <= pastureEnd; i += 6)
                {
                    Vector3 cowPos = PointAt(i, -(WallOffset + 12f + (i % 3) * 3.5f));
                    cowPos.y = GetTerrainHeight(cowPos.x, cowPos.z, def);
                    Quaternion cowRot = Quaternion.Euler(0, (i * 47) % 360, 0);
                    SpawnAnimal(prefabCow, cowPos, cowRot, 2.4f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // エゾシカ（中央ローン広場やエルムの森の奥）
            if (prefabDeer != null)
            {
                for (int d = 0; d < 3; d++)
                {
                    int idx = (int)(Count * (0.09f + d * 0.12f));
                    Vector3 p = PointAt(idx, -(WallOffset + 12f + d * 5f));
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, 60f + d * 30f, 0);
                    SpawnAnimal(prefabDeer, p, rot, 2.2f, animalMat, AnimalBehavior.BehaviorType.IdleGraze);
                }
            }

            // エゾリス / ウサギ（イチョウ並木や大野池周辺）
            if (prefabBunny != null)
            {
                for (int b = 0; b < 5; b++)
                {
                    int idx = (int)(Count * (0.07f + b * 0.08f));
                    Vector3 p = PointAt(idx, WallOffset + 4.5f + (float)rng.NextDouble() * 5f);
                    p.y = GetTerrainHeight(p.x, p.z, def);
                    Quaternion rot = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SpawnAnimal(prefabBunny, p, rot, 1.5f, animalMat, AnimalBehavior.BehaviorType.HopBounce);
                }
            }

            // 北キツネ（ポプラ並木の木陰）
            if (prefabFox != null)
            {
                int popIdx = (int)(0.84f * Count);
                Vector3 p = PointAt(popIdx, WallOffset + 7.5f);
                p.y = GetTerrainHeight(p.x, p.z, def);
                SpawnAnimal(prefabFox, p, Quaternion.Euler(0, 160f, 0), 1.7f, animalMat, AnimalBehavior.BehaviorType.LookAround);
            }
        }
    }

    GameObject SpawnAnimal(GameObject prefab, Vector3 pos, Quaternion rot, float scale, Material mat, AnimalBehavior.BehaviorType bType)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, rot, transform);
        go.transform.localScale = Vector3.one * scale;
        StripColliders(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
        var beh = go.AddComponent<AnimalBehavior>();
        beh.behavior = bType;
        return go;
    }

    GameObject SpawnFlyingBird(GameObject prefab, Vector3 center, float radius, float altitude, float speed, Material mat)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, center + Vector3.up * altitude, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * 1.5f;
        StripColliders(go);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
        var bird = go.AddComponent<FlyingBird>();
        bird.centerPos = center;
        bird.radius = radius;
        bird.altitude = altitude;
        bird.speed = speed;
        return go;
    }
}
