using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct JumpRampDef
{
    public float Ratio;   // 0.0 ~ 1.0 (コースの進行度)
    public float Power;   // 射出上向き速度 (例: 14f)
    public float Lateral; // 横オフセット (0なら中央、正なら右、負なら左)
    public float Width;   // ジャンプ台の幅 (0ならコース幅に合わせた既定値)
}

[System.Serializable]
public struct TunnelDef
{
    public float Start; // 0.0 ~ 1.0 (コースの進行度)
    public float End;
}

[System.Serializable]
public struct BranchDef
{
    public float Start;              // 0.0 ~ 1.0 (分岐開始位置)
    public float End;                // 0.0 ~ 1.0 (分岐合流位置)
    public float LeftOffset;         // 左車線の中心横オフセット (例: -9.5f)
    public float RightOffset;        // 右車線の中心横オフセット (例: +9.5f)
    public float LaneWidth;          // 各車線の半幅 (例: 5.5f)
    public float DividerWidth;       // 中央分離帯の半幅 (例: 4.0f)
    public float RightHeight;        // 右車線の持ち上げ高さ (例: 1.8f 高架/木橋風)
}

[System.Serializable]
public struct TrackDef
{
    public TunnelDef[] Tunnels; // ビルを貫通する屋根付き区間
    public BranchDef[] Branches; // ルート分岐（二股コース）区間
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
    public int SceneryTheme; // 0 = Circuit, 1 = Desert, 2 = Snow, 3 = City, 4 = Campus (Hokkaido)
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
        },
        // 5. Hokkaido Campus - 北大札幌キャンパスの実際の道路網・形状を再現！
        // 南端正門からメインストリートを北上、東の北13条イチョウ並木、北西の第2農場モデルバーン（ビッグジャンプ）、西のポプラ並木通りを南下する精密レイアウト
        new TrackDef
        {
            Name = "HOKKAIDO CAMPUS",
            Description = "Authentic tour of Hokkaido University's Sapporo campus: Main Avenue, golden Ginkgo Avenue, Clark statue, historic Model Barn, and Poplar Avenue.",
            Control = new[]
            {
                new Vector3(-40, 0, -210),      // 0: 【南端・北大正門前】ホームストレート（スタート/フィニッシュ）
                new Vector3(20, 0, -210),       // 1: 正門前ストレート東（百年記念会館・南門前）
                new Vector3(60, 0, -180),       // 2: 東へ旋回しメインストリート南端へ
                new Vector3(60, 1, -120),       // 3: 【メインストリート南セクション】左手に中央ローン・クラーク像・古河講堂
                new Vector3(60, 1, -50),        // 4: メインストリート直線加速区間
                new Vector3(60, 1, 20),         // 5: 【北大総合博物館 & 大野池】博物館前（右手）と大野池（左手）
                new Vector3(90, 1, 65),         // 6: 【北13条交差点】東のイチョウ並木通りへターン！
                new Vector3(150, 1, 75),        // 7: 【北13条イチョウ並木】東へ真っ直ぐ伸びる黄金色のトンネル！
                new Vector3(175, 2, 105),       // 8: イチョウ並木東端（北13条門手前のヘアピンターン）
                new Vector3(130, 2, 125),       // 9: イチョウ並木から北へスイング
                new Vector3(70, 2, 135),        // 10: メインストリート北セクションへ合流
                new Vector3(60, 3, 185),        // 11: 【メインストリート北セクション】工学部・総合研究棟前の超ロング直線
                new Vector3(50, 6, 245),        // 12: 北18条門付近・最北端アプローチ
                new Vector3(0, 9, 290),         // 13: キャンパス北端（北20条付近）で西へ大きく旋回
                new Vector3(-55, 12, 285),      // 14: 第2農場へのアプローチ登り坂
                new Vector3(-105, 14, 255),     // 15: 【札幌農学校第2農場】モデルバーン（赤屋根納屋トンネル）！
                new Vector3(-115, 15, 210),     // 16: 納屋を突き抜けて牧草地へダイブする【ビッグジャンプ台】！
                new Vector3(-115, 7, 145),      // 17: 牧草地ダウンヒル（白い木柵と放牧牛）
                new Vector3(-105, 3, 75),       // 18: 【ポプラ並木通り（Poplar Avenue）】直立ポプラが並ぶ西側高速ストレート
                new Vector3(-105, 2, -10),      // 19: ポプラ並木ストレート中央
                new Vector3(-105, 1, -90),      // 20: 花木園・高等教育推進機構西側を南下
                new Vector3(-95, 0, -165),      // 21: サクシュコトニ川・中央ローン西側へのターン
                new Vector3(-65, 0, -200)       // 22: クラーク会館前・最終コーナー（ホームストレートへ）
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.695f, Power = 16f } },
            Tunnels = new[] { new TunnelDef { Start = 0.64f, End = 0.685f } }, // モデルバーン（歴史的牛舎）の屋内を駆け抜ける！
            SkyTint = new Color(0.25f, 0.55f, 0.95f),
            GroundColor = new Color(0.26f, 0.48f, 0.22f),
            SunColor = new Color(1f, 0.95f, 0.88f),
            SunIntensity = 1.38f,
            SunRotation = new Vector3(48f, -35f, 0f),
            FogColor = new Color(0.65f, 0.82f, 0.96f),
            FogDistance = 1200f,
            RoadColor = new Color(0.26f, 0.27f, 0.28f),
            CurbColorA = new Color(0.08f, 0.48f, 0.22f), // 北大エルムグリーン
            CurbColorB = Color.white,
            WallColorA = new Color(0.12f, 0.46f, 0.26f),
            WallColorB = new Color(0.95f, 0.95f, 0.92f),
            ShoulderColor = new Color(0.82f, 0.74f, 0.58f),
            SceneryTheme = 4
        },
        // 6. Skyline Overpass - 海岸ベイエリアを舞台にした立体交差（8の字）ハイウェイサーキット！
        // 序盤のシーサイド大通りからアンダーパスへ潜り、巨大高架橋の直下を通過。
        // 西側ヒルクライムスパイラルで標高15mへ駆け上がり、下界を見下ろす斜張橋（立体交差）を横断！
        // 橋上のビッグジャンプ台から一気に急降下してホームストレートへ戻るスリリングなコース
        new TrackDef
        {
            Name = "SKYLINE OVERPASS",
            Description = "Spectacular figure-8 coastal raceway featuring a high-altitude flyover bridge crossing 15m over the lower boulevard, helix climb, and bridge jump.",
            Control = new[]
            {
                new Vector3(0, 0, -160),        // 0: スタート/フィニッシュ（シーサイドメインストレート）
                new Vector3(90, 0, -160),       // 1: ストレート加速区間
                new Vector3(170, 2, -135),      // 2: 第1コーナー（高速イーストスイーパー）
                new Vector3(220, 3, -60),       // 3: ベイサイドカーブ
                new Vector3(210, 2, 25),        // 4: マリーナ沿岸アプローチ
                new Vector3(150, 1, 60),        // 5: S字カーブからアンダーパス入口へ
                new Vector3(75, 0, 40),         // 6: アンダーパス手前直線
                new Vector3(0, 0, 40),          // 7: 【下層・立体交差ポイント】頭上15mを高架橋が横断！
                new Vector3(-75, 0, 40),        // 8: アンダーパス西側出口
                new Vector3(-155, 2, 60),       // 9: ウエストループ入口
                new Vector3(-210, 5, 115),      // 10: ヒルクライムスパイラル
                new Vector3(-185, 9, 185),      // 11: 登り勾配中速コーナー
                new Vector3(-110, 12, 215),     // 12: 高台リッジターン
                new Vector3(-35, 14.5f, 175),   // 13: 高架橋アプローチ（標高14.5m）
                new Vector3(0, 15, 110),        // 14: 斜張橋ゲートウェイ（標高15mピーク！）
                new Vector3(0, 15, 40),         // 15: 【上層・立体交差ポイント】下の道路を真上から見下ろす！
                new Vector3(0, 14.5f, -30),     // 16: 高架橋終端【ビッグジャンプ台設置】！
                new Vector3(-20, 10, -90),      // 17: スリリングな急降下スイーパー
                new Vector3(-65, 5, -130),      // 18: ダウンヒルS字シケイン
                new Vector3(-80, 1, -160)       // 19: 最終コーナー（ホームストレートへ合流）
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.81f, Power = 16f } },
            SkyTint = new Color(0.35f, 0.62f, 0.98f),
            GroundColor = new Color(0.16f, 0.44f, 0.60f),
            SunColor = new Color(1f, 0.98f, 0.90f),
            SunIntensity = 1.36f,
            SunRotation = new Vector3(45f, -40f, 0f),
            FogColor = new Color(0.70f, 0.85f, 0.98f),
            FogDistance = 1100f,
            RoadColor = new Color(0.24f, 0.25f, 0.27f),
            CurbColorA = new Color(0.05f, 0.85f, 0.95f),
            CurbColorB = Color.white,
            WallColorA = new Color(0.12f, 0.45f, 0.95f),
            WallColorB = new Color(1f, 0.35f, 0.55f),
            ShoulderColor = new Color(0.88f, 0.82f, 0.68f),
            SceneryTheme = 5
        },
        // 7. Canyon Split - 激流の川を挟んで道が二手に分かれる二股渓谷サーキット！
        // 序盤のオアシス峡谷から大河アプローチへ駆け上がり、運命の分岐点へ突入。
        // 左ルート：激流を臨む安全な川沿いロード（アイテムボックス多数）。
        // 右ルート：ジャンプ台で川を跳び越え、高架トレッスル橋（ダッシュ板連続）を駆け抜けるハイリスク・ショートカット！
        new TrackDef
        {
            Name = "CANYON SPLIT",
            Description = "Dramatic red rock canyon raceway featuring the Great Split: take the safe riverside cruise or leap across the rapids to the wooden trestle shortcut.",
            Control = new[]
            {
                new Vector3(0, 0, -150),        // 0: スタート/フィニッシュ（オアシス大通り）
                new Vector3(100, 0, -150),      // 1: ストレート加速区間
                new Vector3(180, 2, -120),      // 2: 第1コーナー（キャニオンイースト）
                new Vector3(215, 4, -40),       // 3: 峡谷の登り坂
                new Vector3(210, 7, 40),        // 4: リバーサイド高台
                new Vector3(165, 8, 115),       // 5: 【分岐アプローチ】分岐標識 & ジャンプ台！
                new Vector3(95, 7, 180),        // 6: 【二股区間 序盤】川を挟んで左右へ！
                new Vector3(10, 6, 215),        // 7: 【二股区間 ピーク】北端の大赤岩モニュメントを周回
                new Vector3(-80, 5, 200),       // 8: 【二股区間 終盤】左の川沿い & 右のトレッスル橋
                new Vector3(-145, 4, 140),      // 9: 【合流ポイント】二つの道が再び一つに！
                new Vector3(-200, 3, 70),       // 10: キャニオンウエスト
                new Vector3(-225, 2, -10),      // 11: 赤岩アーチのS字入口
                new Vector3(-195, 1, -75),      // 12: アーチ通過・下り坂
                new Vector3(-140, 0, -125),     // 13: 最終コーナー手前
                new Vector3(-70, 0, -150)       // 14: ホームストレートへ合流
            },
            Ramps = new[] { new JumpRampDef { Ratio = 0.36f, Power = 17.5f, Lateral = 10.0f, Width = 8.0f } },
            Branches = new[]
            {
                new BranchDef
                {
                    Start = 0.37f,
                    End = 0.62f,
                    LeftOffset = -18.5f,
                    RightOffset = 18.5f,
                    LaneWidth = 6.0f,
                    DividerWidth = 12.0f,
                    RightHeight = 4.2f
                }
            },
            SkyTint = new Color(0.38f, 0.65f, 0.98f),
            GroundColor = new Color(0.76f, 0.44f, 0.24f),
            SunColor = new Color(1f, 0.94f, 0.82f),
            SunIntensity = 1.35f,
            SunRotation = new Vector3(46f, -42f, 0f),
            FogColor = new Color(0.85f, 0.72f, 0.62f),
            FogDistance = 950f,
            RoadColor = new Color(0.32f, 0.28f, 0.27f),
            CurbColorA = new Color(0.98f, 0.52f, 0.1f),
            CurbColorB = new Color(0.95f, 0.95f, 0.95f),
            WallColorA = new Color(0.85f, 0.25f, 0.12f),
            WallColorB = new Color(1.0f, 0.85f, 0.35f),
            ShoulderColor = new Color(0.88f, 0.65f, 0.42f),
            SceneryTheme = 1
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
            if (seg < 1e-5f) continue;
            while (acc + seg >= Spacing)
            {
                float t = (Spacing - acc) / seg;
                prev = Vector3.Lerp(prev, cur, t);
                res.Add(prev);
                seg = (cur - prev).magnitude;
                acc = 0;
                if (seg < 1e-5f) break;
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
                JumpRamps.Add((ri, ramp.Lateral, ramp.Power));
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
        int best = index;
        int bestOffset = 0;
        float bd = float.MaxValue;
        for (int o = -14; o <= 14; o++)
        {
            int j = Wrap(index + o);
            float d = (Pts[j] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = j; bestOffset = o; }
        }
        if (bd > 900f || bestOffset == -14 || bestOffset == 14)
        {
            best = FindNearest(pos);
        }
        index = best;
        var rel = pos - Pts[best];
        lateral = Vector3.Dot(rel, Rights[best]);

        float d0 = Vector3.Dot(rel, Dirs[best]);
        float along = 0f;
        if (d0 >= 0f)
        {
            int next = (best + 1) % Count;
            float d1 = Vector3.Dot(pos - Pts[next], Dirs[next]);
            float denom = d0 - d1;
            float t = denom > 1e-5f ? Mathf.Clamp01(d0 / denom) : 0f;
            float segLen = (Pts[next] - Pts[best]).magnitude;
            along = t * segLen;
        }
        else
        {
            int prev = (best - 1 + Count) % Count;
            float dPrev = Vector3.Dot(pos - Pts[prev], Dirs[prev]);
            float denom = dPrev - d0;
            float t = denom > 1e-5f ? Mathf.Clamp01(-d0 / denom) : 0f;
            float segLen = (Pts[best] - Pts[prev]).magnitude;
            along = -t * segLen;
        }

        progress = Mathf.Repeat(Dist[best] + along, Length);
    }

    public bool IsInBranch(float ratio, out BranchDef branch, out float blend)
    {
        blend = 0f;
        branch = default;
        var branches = Courses[ActiveCourseIndex].Branches;
        if (branches == null || branches.Length == 0) return false;
        const float transLen = 0.035f;
        for (int i = 0; i < branches.Length; i++)
        {
            var b = branches[i];
            float startTrans = b.Start - transLen;
            float endTrans = b.End + transLen;
            if (ratio >= startTrans && ratio <= endTrans)
            {
                branch = b;
                if (ratio < b.Start)
                    blend = Mathf.SmoothStep(0f, 1f, (ratio - startTrans) / transLen);
                else if (ratio > b.End)
                    blend = Mathf.SmoothStep(1f, 0f, (ratio - b.End) / transLen);
                else
                    blend = 1f;
                return true;
            }
        }
        return false;
    }

    public Vector3 PointAt(int index, float lateral)
    {
        if (Count == 0 || Pts == null) return Vector3.zero;
        int i = Wrap(index);
        float hOffset = 0f;
        float ratio = (float)i / Count;
        if (lateral > 0f && IsInBranch(ratio, out BranchDef b, out float blend))
        {
            hOffset = b.RightHeight * blend;
        }
        return Pts[i] + Rights[i] * lateral + Normals[i] * hOffset;
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

        for (int i = 0; i < Count; i++)
        {
            float ratio = (float)i / Count;
            if (IsInBranch(ratio, out BranchDef b, out float blend) && blend > 0.05f)
            {
                // 分岐区間：左右2つのルートをスタンプ
                Vector3 leftP = PointAt(i, b.LeftOffset * blend);
                Vector3 rightP = PointAt(i, b.RightOffset * blend);
                Stamp(leftP, size / 55f, new Color(0, 0, 0, 0.6f));
                Stamp(leftP, size / 100f, new Color(1, 1, 1, 0.95f));
                Stamp(rightP, size / 55f, new Color(0, 0, 0, 0.6f));
                Stamp(rightP, size / 100f, new Color(1, 1, 1, 0.95f));
            }
            else
            {
                Stamp(Pts[i], size / 45f, new Color(0, 0, 0, 0.6f));
                Stamp(Pts[i], size / 80f, new Color(1, 1, 1, 0.95f));
            }
        }

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
