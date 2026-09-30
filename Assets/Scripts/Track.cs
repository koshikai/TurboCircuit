using System.Collections.Generic;
using UnityEngine;

public struct TrackDef
{
    public string Name;
    public string Description;
    public Vector2[] Control;
    public Color SkyTint;
    public Color GroundColor;
    public Color SunColor;
    public float SunIntensity;
    public Vector3 SunRotation;
    public Color FogColor;
    public float FogDistance;
    public Color RoadColor;
    public Color CurbColorA;
    public Color CurbColorB;
    public Color WallColorA;
    public Color WallColorB;
    public Color ShoulderColor;
    public int SceneryTheme; // 0 = Circuit, 1 = Desert, 2 = Snow
}

// コース。制御点からスプラインを作り、等間隔のサンプル点・路面メッシュ・装飾を生成する。
// カートの位置判定（コース上の進み具合・中心からの横ずれ）もここで行う。
public class Track : MonoBehaviour
{
    public const float HalfWidth = 9f;
    public const float CurbWidth = 1.4f;
    public const float WallOffset = 15f;
    const float Spacing = 2f;

    public static readonly TrackDef[] Courses =
    {
        // 1. Turbo Circuit
        new TrackDef
        {
            Name = "TURBO CIRCUIT",
            Description = "Standard Grand Prix track with fast chicanes and packed grandstands.",
            Control = new[]
            {
                new Vector2(0, -120), new Vector2(120, -128), new Vector2(200, -100), new Vector2(232, -30),
                new Vector2(192, 28), new Vector2(122, 40), new Vector2(92, 92), new Vector2(132, 150),
                new Vector2(82, 204), new Vector2(-20, 192), new Vector2(-82, 140), new Vector2(-60, 72),
                new Vector2(-128, 38), new Vector2(-200, 62), new Vector2(-244, 0), new Vector2(-214, -82),
                new Vector2(-120, -122)
            },
            SkyTint = new Color(0.45f, 0.6f, 1f),
            GroundColor = new Color(0.45f, 0.55f, 0.4f),
            SunColor = new Color(1f, 0.96f, 0.88f),
            SunIntensity = 1.25f,
            SunRotation = new Vector3(48f, -35f, 0f),
            FogColor = new Color(0.72f, 0.82f, 0.95f),
            FogDistance = 900f,
            RoadColor = new Color(0.28f, 0.28f, 0.29f),
            CurbColorA = new Color(0.9f, 0.1f, 0.1f),
            CurbColorB = Color.white,
            WallColorA = new Color(0.15f, 0.35f, 0.95f),
            WallColorB = Color.white,
            ShoulderColor = new Color(0.85f, 0.75f, 0.55f),
            SceneryTheme = 0
        },
        // 2. Sunset Dunes
        new TrackDef
        {
            Name = "SUNSET DUNES",
            Description = "High-speed desert raceway with sweeping curves and ancient ruins.",
            Control = new[]
            {
                new Vector2(0, -150), new Vector2(130, -145), new Vector2(210, -90), new Vector2(240, 20),
                new Vector2(170, 90), new Vector2(80, 50), new Vector2(20, 110), new Vector2(70, 190),
                new Vector2(-20, 220), new Vector2(-110, 180), new Vector2(-80, 100), new Vector2(-150, 40),
                new Vector2(-230, 20), new Vector2(-250, -60), new Vector2(-180, -130), new Vector2(-90, -140)
            },
            SkyTint = new Color(1.0f, 0.45f, 0.25f),
            GroundColor = new Color(0.85f, 0.65f, 0.35f),
            SunColor = new Color(1.0f, 0.65f, 0.35f),
            SunIntensity = 1.4f,
            SunRotation = new Vector3(25f, -60f, 0f),
            FogColor = new Color(0.95f, 0.68f, 0.45f),
            FogDistance = 800f,
            RoadColor = new Color(0.46f, 0.36f, 0.28f),
            CurbColorA = new Color(0.98f, 0.85f, 0.1f),
            CurbColorB = new Color(0.12f, 0.12f, 0.12f),
            WallColorA = new Color(0.95f, 0.42f, 0.08f),
            WallColorB = Color.white,
            ShoulderColor = new Color(0.92f, 0.75f, 0.48f),
            SceneryTheme = 1
        },
        // 3. Frost Peak
        new TrackDef
        {
            Name = "FROST PEAK",
            Description = "Twisting alpine circuit carved through snow-capped mountains and ice.",
            Control = new[]
            {
                new Vector2(0, -130), new Vector2(110, -120), new Vector2(180, -70), new Vector2(140, -10),
                new Vector2(200, 30), new Vector2(160, 90), new Vector2(110, 140), new Vector2(40, 130),
                new Vector2(10, 180), new Vector2(-60, 190), new Vector2(-90, 130), new Vector2(-40, 70),
                new Vector2(-110, 20), new Vector2(-190, 50), new Vector2(-220, -30), new Vector2(-160, -90),
                new Vector2(-90, -80), new Vector2(-60, -130)
            },
            SkyTint = new Color(0.55f, 0.72f, 1.0f),
            GroundColor = new Color(0.9f, 0.94f, 1.0f),
            SunColor = new Color(0.95f, 0.98f, 1.0f),
            SunIntensity = 1.35f,
            SunRotation = new Vector3(55f, -30f, 0f),
            FogColor = new Color(0.85f, 0.92f, 1.0f),
            FogDistance = 750f,
            RoadColor = new Color(0.38f, 0.45f, 0.55f),
            CurbColorA = new Color(0.1f, 0.75f, 0.95f),
            CurbColorB = Color.white,
            WallColorA = new Color(0.2f, 0.45f, 0.85f),
            WallColorB = new Color(0.88f, 0.92f, 0.98f),
            ShoulderColor = new Color(0.82f, 0.88f, 0.96f),
            SceneryTheme = 2
        }
    };

    public int ActiveCourseIndex { get; private set; }
    public Vector3[] Pts { get; private set; }
    public Vector3[] Dirs { get; private set; }
    public Vector3[] Rights { get; private set; }
    public float[] Dist { get; private set; }
    public float Length { get; private set; }
    public int Count => Pts != null ? Pts.Length : 0;

    public readonly List<(int index, float lateral)> BoostPads = new List<(int, float)>();
    public readonly List<(int index, float lateral)> ItemBoxSpots = new List<(int, float)>();

    Bounds bounds;

    // ───────────────────────── スプライン ─────────────────────────

    public void BuildPath(int courseIndex = 0)
    {
        ActiveCourseIndex = Mathf.Clamp(courseIndex, 0, Courses.Length - 1);
        var control = Courses[ActiveCourseIndex].Control;
        int n = control.Length;
        var dense = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = V(control[(i - 1 + n) % n]), p1 = V(control[i]), p2 = V(control[(i + 1) % n]), p3 = V(control[(i + 2) % n]);
            for (int s = 0; s < 60; s++) dense.Add(CatmullRom(p0, p1, p2, p3, s / 60f));
        }

        // 等間隔にリサンプル
        var res = new List<Vector3> { dense[0] };
        Vector3 prev = dense[0];
        float acc = 0;
        for (int k = 1; k <= dense.Count; k++)
        {
            Vector3 cur = dense[k % dense.Count];
            float seg = (cur - prev).magnitude;
            while (acc + seg >= Spacing)
            {
                float t = (Spacing - acc) / seg;
                prev = Vector3.Lerp(prev, cur, t);
                res.Add(prev);
                seg = (cur - prev).magnitude;
                acc = 0;
            }
            acc += seg;
            prev = cur;
        }
        if ((res[res.Count - 1] - res[0]).magnitude < Spacing * 0.6f) res.RemoveAt(res.Count - 1);

        int c = res.Count;
        Pts = res.ToArray();
        Dirs = new Vector3[c];
        Rights = new Vector3[c];
        Dist = new float[c];
        for (int i = 0; i < c; i++)
        {
            Dirs[i] = (Pts[(i + 1) % c] - Pts[(i - 1 + c) % c]).normalized;
            Rights[i] = Vector3.Cross(Vector3.up, Dirs[i]);
            if (i > 0) Dist[i] = Dist[i - 1] + (Pts[i] - Pts[i - 1]).magnitude;
        }
        Length = Dist[c - 1] + (Pts[0] - Pts[c - 1]).magnitude;

        bounds = new Bounds(Pts[0], Vector3.zero);
        foreach (var p in Pts) bounds.Encapsulate(p);
    }

    static Vector3 V(Vector2 p) => new Vector3(p.x, 0, p.y);

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public int Wrap(int i) => Count == 0 ? 0 : (((i % Count) + Count) % Count);

    public int FindNearest(Vector3 pos)
    {
        if (Count == 0 || Pts == null) return 0;
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            float d = (Pts[i] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // index は前回値を渡し、近傍だけ探索して更新する
    public void Locate(Vector3 pos, ref int index, out float lateral, out float progress)
    {
        if (Count == 0 || Pts == null) { lateral = 0; progress = 0; return; }
        int best = index; float bd = float.MaxValue;
        for (int o = -14; o <= 14; o++)
        {
            int j = Wrap(index + o);
            float d = (Pts[j] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = j; }
        }
        index = best;
        var rel = pos - Pts[best];
        lateral = Vector3.Dot(rel, Rights[best]);
        progress = Mathf.Repeat(Dist[best] + Vector3.Dot(rel, Dirs[best]), Length);
    }

    public Vector3 PointAt(int index, float lateral)
    {
        if (Count == 0 || Pts == null) return Vector3.zero;
        int i = Wrap(index);
        return Pts[i] + Rights[i] * lateral;
    }

    // ───────────────────────── メッシュ ─────────────────────────

    public void ClearVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
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

        Texture2D groundTex = def.SceneryTheme == 1 ? TextureGen.Sand() : (def.SceneryTheme == 2 ? TextureGen.Snow() : TextureGen.Grass());
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

        // 地面
        var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(ground.GetComponent<Collider>());
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);
        ground.transform.position = new Vector3(bounds.center.x, 0, bounds.center.z);
        ground.transform.rotation = Quaternion.Euler(90, 0, 0);
        ground.transform.localScale = new Vector3(1800, 1800, 1);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        BuildStartLine(rm, def);
        BuildBoostPads(rm);
        PlanItemBoxes();
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

    // a/b は (横オフセット, 高さ)。a→b が進行方向に対して右向きになる面が表。
    void Strip(string name, Vector2 a, Vector2 b, Material mat, float vScale)
    {
        int c = Count;
        var verts = new Vector3[(c + 1) * 2];
        var uvs = new Vector2[(c + 1) * 2];
        var tris = new int[c * 6];
        for (int i = 0; i <= c; i++)
        {
            int k = i % c;
            float v = (i == c ? Length : Dist[k]) / vScale;
            verts[i * 2] = Pts[k] + Rights[k] * a.x + Vector3.up * a.y;
            verts[i * 2 + 1] = Pts[k] + Rights[k] * b.x + Vector3.up * b.y;
            uvs[i * 2] = new Vector2(0, v);
            uvs[i * 2 + 1] = new Vector2(1, v);
        }
        for (int i = 0; i < c; i++)
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

    GameObject MeshObject(string name, Mesh mesh, Material mat)
    {
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
        var signalColors = new[] { new Color(1f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.15f), new Color(0.2f, 0.95f, 0.35f) };
        for (int i = 0; i < 3; i++)
        {
            float xOffset = (i - 1) * 2.4f;
            var lampPos = Pts[0] + rot * new Vector3(xOffset, 9.6f, 0.35f);
            var lampMat = new Material(rm.glowMaterial) { color = signalColors[i] };
            Box("SignalCase_" + i, lampPos, new Vector3(1.5f, 1.5f, 0.3f), rot, rm.tireMaterial);
            Box("SignalBulb_" + i, lampPos + rot * Vector3.forward * 0.16f, new Vector3(1.1f, 1.1f, 0.1f), rot, lampMat, PrimitiveType.Cylinder).transform.localRotation = rot * Quaternion.Euler(90, 0, 0);
        }

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

        for (int tries = 0; tries < 3000 && placed < 260; tries++)
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
        for (int i = 0; i < 22; i++)
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
            var pos = PointAt(i, side * (WallOffset + 0.1f)) + Vector3.up * 1.6f;
            var rot = Quaternion.LookRotation(Dirs[i]);
            var mat = bannerMats[(i / 22) % 4];

            // 看板の支柱とボード
            Box("BoardPole1", pos - Dirs[i] * 1.8f - Vector3.up * 0.8f, new Vector3(0.18f, 1.8f, 0.18f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
            Box("BoardPole2", pos + Dirs[i] * 1.8f - Vector3.up * 0.8f, new Vector3(0.18f, 1.8f, 0.18f), Quaternion.identity, rm.chromeMaterial, PrimitiveType.Cylinder);
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
        var barrierRed = Resources.Load<GameObject>("Props/barrierRed");
        var barrierWhite = Resources.Load<GameObject>("Props/barrierWhite");
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

        // 2. タイヤバリア（急カーブ外側にテーマ色で交互配置）
        if (barrierRed != null && barrierWhite != null)
        {
            var matA = new Material(rm.wallMaterial) { color = def.CurbColorA };
            var matB = new Material(rm.wallMaterial) { color = def.CurbColorB };
            for (int i = 10; i < Count - 10; i += 3)
            {
                float bend = Vector3.SignedAngle(Dirs[i], Dirs[Wrap(i + 10)], Vector3.up);
                if (Mathf.Abs(bend) > 12f)
                {
                    float side = bend > 0 ? -1f : 1f;
                    var pos = PointAt(i, side * (WallOffset - 0.25f));
                    var rot = Quaternion.LookRotation(Dirs[i]);
                    var prefab = (i / 3 % 2 == 0) ? barrierRed : barrierWhite;
                    var anchor = new GameObject("BarrierAnchor").transform;
                    anchor.SetParent(transform, false);
                    anchor.position = pos;
                    anchor.rotation = rot;
                    var go = Instantiate(prefab, anchor);
                    go.transform.localPosition = new Vector3(-1.25f, 0, -0.62f);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one * 1.3f;
                    StripColliders(anchor.gameObject);
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = (i / 3 % 2 == 0) ? matA : matB;
                }
            }
        }

        // 3. コーナーのコーン（パイロン：イン側クリッピングポイント）
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

        // 4. ピットテントとチェッカーフラッグ（スタート付近）
        if (tent != null)
        {
            var tentColor = def.SceneryTheme == 1 ? new Color(0.9f, 0.5f, 0.1f) : (def.SceneryTheme == 2 ? new Color(0.2f, 0.5f, 0.85f) : new Color(0.15f, 0.45f, 0.95f));
            var tentMat = new Material(rm.wallMaterial) { color = tentColor };
            for (int t = 1; t <= 3; t++)
            {
                int idx = Wrap(-t * 9);
                var pos = PointAt(idx, WallOffset + 8.5f);
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

    void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
    }

    float DistanceToTrack(Vector3 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < Count; i += 3) best = Mathf.Min(best, (Pts[i] - p).sqrMagnitude);
        return Mathf.Sqrt(best);
    }

    // ミニマップ用テクスチャ
    public Texture2D MakeMinimap(int size, out System.Func<Vector3, Vector2> toMap)
    {
        float span = Mathf.Max(bounds.size.x, bounds.size.z) + 40f;
        var center = bounds.center;
        toMap = p => new Vector2((p.x - center.x) / span + 0.5f, (p.z - center.z) / span + 0.5f);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        var local = toMap;
        void Stamp(Vector3 world, float radius, Color col)
        {
            var uv = local(world) * size;
            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                int px0 = (int)uv.x + x, py0 = (int)uv.y + y;
                if (px0 < 0 || py0 < 0 || px0 >= size || py0 >= size) continue;
                px[py0 * size + px0] = col;
            }
        }
        foreach (var p in Pts) Stamp(p, size / 45f, new Color(0, 0, 0, 0.6f));
        foreach (var p in Pts) Stamp(p, size / 80f, new Color(1, 1, 1, 0.95f));
        for (int k = -2; k <= 2; k++) Stamp(PointAt(0, k * 3f), size / 110f, new Color(0.1f, 0.1f, 0.1f, 1));
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

// 観客がぴょこぴょこ跳ねる
public class Bouncer : MonoBehaviour
{
    float phase; Vector3 basePos;
    public void Init(float p) { phase = p; basePos = transform.position; }
    void Update() => transform.position = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 5f + phase)) * 0.35f;
}
