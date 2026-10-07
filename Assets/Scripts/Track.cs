using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct JumpRampDef
{
    public float Ratio; // 0.0 ~ 1.0 (コースの進行度)
    public float Power; // 射出上向き速度 (例: 14f)
}

[System.Serializable]
public struct TunnelDef
{
    public float Start; // 0.0 ~ 1.0 (コースの進行度)
    public float End;
}

[System.Serializable]
public struct TrackDef
{
    public TunnelDef[] Tunnels; // ビルを貫通する屋根付き区間
    public string Name;
    public string Description;
    public Vector3[] Control;
    public JumpRampDef[] Ramps;
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
    public int SceneryTheme; // 0 = Circuit, 1 = Desert, 2 = Snow, 3 = City
}

// コース。制御点からスプラインを作り、等間隔のサンプル点・路面メッシュ・装飾を生成する。
// カートの位置判定（コース上の進み具合・中心からの横ずれ）もここで行う。
public partial class Track : MonoBehaviour
{
    public const float HalfWidth = 9f;
    public const float CurbWidth = 1.4f;
    public const float WallOffset = 15f;
    const float Spacing = 2f;

    public static readonly TrackDef[] DefaultCourses =
    {
        // 1. Turbo Circuit - 緩やかな丘陵と高架橋立体交差、終盤のビッグジャンプ
        new TrackDef
        {
            Name = "TURBO CIRCUIT",
            Description = "Grand Prix circuit with dynamic flyover bridge, high-speed sweeping hills, and a stadium ramp.",
            Control = new[]
            {
                new Vector3(0, 0, -120),        // スタート/フィニッシュ
                new Vector3(120, 3, -128),      // 緩やかな上り坂
                new Vector3(200, 7, -100),      // 第1コーナー
                new Vector3(232, 14, -30),      // 高架橋へのアプローチ
                new Vector3(192, 18, 28),       // 高架橋ピーク（絶景パノラマ！）
                new Vector3(122, 12, 40),       // ダウンヒル下り坂
                new Vector3(92, 6, 92),         // 低速ヘアピン
                new Vector3(132, 8, 150),       // 北側ストレート（ジャンプ台設置）
                new Vector3(82, 11, 204),       // 高台シケイン
                new Vector3(-20, 6, 192),       // ダウンヒル
                new Vector3(-82, 2, 140),       // 中速S字
                new Vector3(-60, 0, 72),        // 平坦セクション
                new Vector3(-128, 4, 38),       // 丘越え
                new Vector3(-200, 7, 62),       // 高速コーナー
                new Vector3(-244, 4, 0),        // 西側ストレート
                new Vector3(-214, 1, -82),      // 最終コーナー手前
                new Vector3(-120, 0, -122)      // ホームストレートへ
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.44f, Power = 15f } },
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
        // 2. Sunset Dunes - 砂漠の超巨大デューン急上昇とオアシスへの急降下ジェットコースター
        new TrackDef
        {
            Name = "SUNSET DUNES",
            Description = "Rollercoaster desert raceway climbing massive sand dunes and plunging into ancient ruins.",
            Control = new[]
            {
                new Vector3(0, 0, -150),        // スタート
                new Vector3(130, 4, -145),      // 砂漠平原
                new Vector3(210, 15, -90),      // 巨大砂丘の登り口
                new Vector3(240, 26, 20),       // 砂丘頂上！（巨大夕日クライマックス）
                new Vector3(170, 22, 90),       // 砂丘の尾根滑走
                new Vector3(80, 16, 50),        // 下り坂
                new Vector3(20, 18, 110),       // 砂丘陵
                new Vector3(70, 14, 190),       // 高台コーナー（ジャンプ台設置）
                new Vector3(-20, 5, 220),       // 急降下ジェットコースター！
                new Vector3(-110, 0, 180),      // オアシス低地
                new Vector3(-80, 6, 100),       // 古代ピラミッド谷
                new Vector3(-150, 12, 40),      // 遺跡の丘
                new Vector3(-230, 16, 20),      // 峡谷の登り
                new Vector3(-250, 10, -60),     // 峡谷出口
                new Vector3(-180, 4, -130),     // ホームストレートへ下る
                new Vector3(-90, 0, -140)       // 平坦ストレート
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.48f, Power = 16f } },
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
        // 3. Frost Peak - 標高差35mの本格アルペン雪山クライム＆クレバス滑降
        new TrackDef
        {
            Name = "FROST PEAK",
            Description = "Alpine mountain track carving up to snowy summits with 35m drop and a daring crevasse leap.",
            Control = new[]
            {
                new Vector3(0, 0, -130),        // スタート（山麓の谷）
                new Vector3(110, 6, -120),      // 雪山登山口
                new Vector3(180, 18, -70),      // つづら折りクライム
                new Vector3(140, 28, -10),      // 雪の尾根道
                new Vector3(200, 36, 30),       // 最高峰ピーク（標高36m！）
                new Vector3(160, 33, 90),       // 氷河の台地（ジャンプ台設置）
                new Vector3(110, 28, 140),      // 氷の急斜面
                new Vector3(40, 22, 130),       // つづら折りダウンヒル開始
                new Vector3(10, 18, 180),       // 急勾配ダウンヒル
                new Vector3(-60, 14, 190),      // 雪煙ヘアピン
                new Vector3(-90, 9, 130),       // 氷柱の谷
                new Vector3(-40, 5, 70),        // 緩やかな下り
                new Vector3(-110, 2, 20),       // 山麓への出口
                new Vector3(-190, 6, 50),       // 針葉樹林帯クライム
                new Vector3(-220, 7, -30),      // 氷雪コーナー
                new Vector3(-160, 4, -90),      // 下り坂
                new Vector3(-90, 1, -80),       // ベースキャンプ手前
                new Vector3(-60, 0, -130)       // ホームストレートへ
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.36f, Power = 16f } },
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
        },
        // 4. Neon Metropolis - 摩天楼の谷間を抜け、ビルの中を貫通する都市コース
        new TrackDef
        {
            Name = "NEON METROPOLIS",
            Description = "Dusk city dash through skyscraper canyons, an elevated expressway, and tunnels straight through the buildings.",
            Control = new[]
            {
                new Vector3(0, 0, -140),        // スタート（中央大通り）
                new Vector3(110, 0, -140),
                new Vector3(190, 1, -130),      // 交差点を右折
                new Vector3(235, 2, -70),
                new Vector3(235, 4, 10),        // 高架への登り
                new Vector3(215, 10, 70),
                new Vector3(150, 13, 105),      // 高架ハイウェイ（ジャンプ台設置）
                new Vector3(60, 13, 110),
                new Vector3(-10, 9, 120),
                new Vector3(-70, 3, 150),       // 高架から降下
                new Vector3(-130, 1, 170),      // ビル貫通トンネルA
                new Vector3(-190, 0, 130),
                new Vector3(-215, 0, 60),
                new Vector3(-175, 0, 0),        // ビル貫通トンネルB
                new Vector3(-100, 0, -20),
                new Vector3(-70, 0, -70),
                new Vector3(-100, 0, -130)      // ホームストレートへ
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.43f, Power = 15f } },
            Tunnels = new[]
            {
                new TunnelDef { Start = 0.555f, End = 0.64f },
                new TunnelDef { Start = 0.775f, End = 0.865f }
            },
            SkyTint = new Color(0.38f, 0.28f, 0.65f),
            GroundColor = new Color(0.16f, 0.13f, 0.28f),
            SunColor = new Color(1f, 0.62f, 0.45f),
            SunIntensity = 1.05f,
            SunRotation = new Vector3(18f, -50f, 0f),
            FogColor = new Color(0.46f, 0.34f, 0.62f),
            FogDistance = 650f,
            RoadColor = new Color(0.2f, 0.2f, 0.24f),
            CurbColorA = new Color(1f, 0.2f, 0.6f),
            CurbColorB = new Color(0.1f, 0.9f, 1f),
            WallColorA = new Color(0.12f, 0.14f, 0.3f),
            WallColorB = new Color(0.55f, 0.6f, 0.75f),
            ShoulderColor = new Color(0.5f, 0.52f, 0.6f),
            SceneryTheme = 3
        }
    };

    static TrackDef[] runtimeCourses;
    public static TrackDef[] Courses
    {
        get
        {
            if (runtimeCourses == null)
            {
                var loaded = Resources.LoadAll<CourseData>("Data/Courses");
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runtimeCourses = loaded.Select(c => c.ToDef()).ToArray();
                }
                else
                {
                    runtimeCourses = DefaultCourses;
                }
            }
            return runtimeCourses;
        }
    }

    public int ActiveCourseIndex { get; private set; }
    public Vector3[] Pts { get; private set; }
    public Vector3[] Dirs { get; private set; }
    public Vector3[] Rights { get; private set; }
    public Vector3[] Normals { get; private set; }
    public float[] Dist { get; private set; }
    public float Length { get; private set; }
    public int Count => Pts != null ? Pts.Length : 0;

    public readonly List<(int index, float lateral)> BoostPads = new List<(int, float)>();
    public readonly List<(int index, float lateral)> ItemBoxSpots = new List<(int, float)>();
    public readonly List<(int index, float lateral, float power)> JumpRamps = new List<(int, float, float)>();

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
            Vector3 p0 = control[(i - 1 + n) % n], p1 = control[i], p2 = control[(i + 1) % n], p3 = control[(i + 2) % n];
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
        Normals = new Vector3[c];
        Dist = new float[c];
        for (int i = 0; i < c; i++)
        {
            Dirs[i] = (Pts[(i + 1) % c] - Pts[(i - 1 + c) % c]).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, Dirs[i]).normalized;
            Rights[i] = r;
            Normals[i] = Vector3.Cross(Dirs[i], r).normalized;
            if (i > 0) Dist[i] = Dist[i - 1] + (Pts[i] - Pts[i - 1]).magnitude;
        }
        Length = Dist[c - 1] + (Pts[0] - Pts[c - 1]).magnitude;

        JumpRamps.Clear();
        if (Courses[ActiveCourseIndex].Ramps != null)
        {
            foreach (var ramp in Courses[ActiveCourseIndex].Ramps)
            {
                int ri = Mathf.Clamp(Mathf.RoundToInt(ramp.Ratio * c), 0, c - 1);
                JumpRamps.Add((ri, 0f, ramp.Power));
            }
        }

        bounds = new Bounds(Pts[0], Vector3.zero);
        foreach (var p in Pts) bounds.Encapsulate(p);
    }

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

// コース定義の ScriptableObject
[CreateAssetMenu(fileName = "NewCourseData", menuName = "Turbo Circuit/Course Data")]
public class CourseData : ScriptableObject
{
    [Header("Information")]
    public string courseName = "TURBO CIRCUIT";
    [TextArea(2, 4)]
    public string description = "Grand Prix circuit with sweeping hills and flyover bridge.";

    [Header("Geometry")]
    public Vector3[] controlPoints;
    public JumpRampDef[] ramps;
    public TunnelDef[] tunnels;

    [Header("Environment & Lighting")]
    public Color skyTint = new Color(0.45f, 0.6f, 1f);
    public Color groundColor = new Color(0.45f, 0.55f, 0.4f);
    public Color sunColor = new Color(1f, 0.96f, 0.88f);
    public float sunIntensity = 1.25f;
    public Vector3 sunRotation = new Vector3(48f, -35f, 0f);
    public Color fogColor = new Color(0.72f, 0.82f, 0.95f);
    public float fogDistance = 900f;

    [Header("Materials & Colors")]
    public Color roadColor = new Color(0.28f, 0.28f, 0.29f);
    public Color curbColorA = new Color(0.9f, 0.1f, 0.1f);
    public Color curbColorB = Color.white;
    public Color wallColorA = new Color(0.15f, 0.35f, 0.95f);
    public Color wallColorB = Color.white;
    public Color shoulderColor = new Color(0.85f, 0.75f, 0.55f);

    [Header("Theme (0=Circuit, 1=Desert, 2=Snow, 3=City)")]
    [Range(0, 3)]
    public int sceneryTheme = 0;

    public TrackDef ToDef()
    {
        return new TrackDef
        {
            Name = courseName,
            Description = description,
            Control = controlPoints,
            Ramps = ramps,
            Tunnels = tunnels,
            SkyTint = skyTint,
            GroundColor = groundColor,
            SunColor = sunColor,
            SunIntensity = sunIntensity,
            SunRotation = sunRotation,
            FogColor = fogColor,
            FogDistance = fogDistance,
            RoadColor = roadColor,
            CurbColorA = curbColorA,
            CurbColorB = curbColorB,
            WallColorA = wallColorA,
            WallColorB = wallColorB,
            ShoulderColor = shoulderColor,
            SceneryTheme = sceneryTheme
        };
    }
}
