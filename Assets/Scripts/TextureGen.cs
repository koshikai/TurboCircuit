using System.Collections.Generic;
using UnityEngine;

// 実行時にテクスチャを生成する（画像ファイル不要、静的キャッシュで再利用）
public static class TextureGen
{
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    public static void ClearCache()
    {
        foreach (var t in cache.Values)
        {
            if (t != null) Object.Destroy(t);
        }
        cache.Clear();
    }

    static Texture2D New(int w, int h, Color[] px, bool mip = true)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, mip) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    public static Texture2D Asphalt() => Asphalt(new Color(0.28f, 0.28f, 0.29f));

    public static Texture2D Asphalt(Color baseTint)
    {
        string key = $"asphalt_{baseTint.r:F2}_{baseTint.g:F2}_{baseTint.b:F2}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int n = 512;
        var px = new Color[n * n];
        var rng = new System.Random(42);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            // 砂利・アスファルトの微細テクスチャ
            float fineNoise = (float)(rng.NextDouble() - 0.5) * 0.08f;
            float coarseNoise = (Mathf.PerlinNoise(u * 16f, v * 16f) - 0.5f) * 0.06f;
            float tireWear = Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 2f) * 0.04f;
            float g = fineNoise + coarseNoise - tireWear;
            var c = baseTint * (1f + g);
            c.a = 1f;

            // 白線（コース両端とセンター破線）
            bool edgeL = u > 0.035f && u < 0.055f;
            bool edgeR = u > 0.945f && u < 0.965f;
            bool center = u > 0.492f && u < 0.508f && Mathf.Repeat(v * 4f, 1f) < 0.55f;
            if (edgeL || edgeR || center)
            {
                float lineNoise = (float)(rng.NextDouble() - 0.5) * 0.05f;
                c = new Color(0.92f + lineNoise, 0.92f + lineNoise, 0.88f, 1f);
            }
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    public static Texture2D Sand()
    {
        const string key = "sand";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(17);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float p = Mathf.PerlinNoise(x * 0.04f, y * 0.08f);
            float r = (float)rng.NextDouble();
            var c = Color.Lerp(new Color(0.88f, 0.72f, 0.42f), new Color(0.96f, 0.82f, 0.52f), p);
            c *= 0.94f + r * 0.12f;
            c.a = 1;
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    public static Texture2D Snow()
    {
        const string key = "snow";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(23);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float p = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
            float r = (float)rng.NextDouble();
            var c = Color.Lerp(new Color(0.88f, 0.92f, 0.98f), new Color(0.98f, 0.99f, 1f), p);
            c *= 0.96f + r * 0.08f;
            c.a = 1;
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    public static Texture2D Stripes(Color a, Color b)
    {
        string key = $"stripes_{a.r:F2}_{a.g:F2}_{a.b:F2}_{b.r:F2}_{b.g:F2}_{b.b:F2}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        const int w = 16, h = 64;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            px[y * w + x] = y < h / 2 ? a : b;
        var tex = New(w, h, px);
        cache[key] = tex;
        return tex;
    }

    public static Texture2D Grass()
    {
        const string key = "grass";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(9);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float p = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            float r = (float)rng.NextDouble();
            var c = Color.Lerp(new Color(0.32f, 0.58f, 0.2f), new Color(0.42f, 0.7f, 0.26f), p);
            c *= 0.9f + r * 0.2f;
            c.a = 1;
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    // 都市の舗装（暗いコンクリート）
    public static Texture2D Concrete()
    {
        const string key = "concrete";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(23);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float p = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
            float r = (float)rng.NextDouble();
            var c = Color.Lerp(new Color(0.30f, 0.31f, 0.44f), new Color(0.40f, 0.41f, 0.44f), p);
            if (x % 32 == 0 || y % 32 == 0) c *= 0.8f; // 舗装の目地
            c *= 0.94f + r * 0.12f;
            c.a = 1;
            px[y * n + x] = c;
        }
        return New(n, n, px);
    }

    // ビルの外壁（窓の明かり付き）。1タイル = 窓4列 x 8階
    public static Texture2D Windows(Color wall, Color lit, int seed)
    {
        const int w = 64, h = 128;
        var px = new Color[w * h];
        var rng = new System.Random(seed);
        var dark = new Color(0.08f, 0.11f, 0.18f);
        bool[] on = new bool[4 * 8];
        for (int i = 0; i < on.Length; i++) on[i] = rng.NextDouble() < 0.55;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int cx = x / 16, cy = y / 16;
            int lx = x % 16, ly = y % 16;
            Color c = wall * (0.92f + (float)rng.NextDouble() * 0.08f);
            if (lx >= 3 && lx < 13 && ly >= 4 && ly < 13)
                c = on[cy * 4 + cx] ? Color.Lerp(lit, Color.white, 0.15f * (ly - 4) / 9f) : dark;
            c.a = 1;
            px[y * w + x] = c;
        }
        return New(w, h, px);
    }

    // タイトル画面用の都市コースバッジ（夜景スカイライン）
    public static Texture2D CityBadge()
    {
        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(5);
        var heights = new int[16];
        for (int i = 0; i < heights.Length; i++) heights[i] = 28 + rng.Next(60);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            var c = new Color(0, 0, 0, 0);
            if (d < 60f)
            {
                c = Color.Lerp(new Color(0.95f, 0.45f, 0.65f), new Color(0.25f, 0.18f, 0.55f), y / (float)n);
                int b = x / 8;
                if (y < heights[b] && x % 8 != 0)
                {
                    c = new Color(0.08f, 0.07f, 0.18f);
                    if (x % 4 == 2 && y % 6 < 3) c = new Color(1f, 0.85f, 0.4f);
                }
                if (d > 54f) c = Color.white;
                c.a = 1f;
            }
            px[y * n + x] = c;
        }
        return New(n, n, px, false);
    }

    // タイトル画面用の北大キャンパスコースバッジ（ポプラ並木・エルムの緑・北斗星・イチョウ）
    public static Texture2D HokkaidoBadge()
    {
        const int n = 128;
        var px = new Color[n * n];
        Color skyTop = new Color(0.18f, 0.52f, 0.88f);
        Color skyBottom = new Color(0.68f, 0.85f, 0.98f);
        Color lawnColor = new Color(0.16f, 0.52f, 0.22f);
        Color poplarColor = new Color(0.25f, 0.68f, 0.32f);
        Color poplarTrunk = new Color(0.38f, 0.28f, 0.18f);
        Color ginkgoGold = new Color(1f, 0.82f, 0.18f);
        Color starColor = new Color(1f, 0.95f, 0.6f);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            var c = new Color(0, 0, 0, 0);
            if (d < 60f)
            {
                // 空のグラデーション
                float tSky = Mathf.Clamp01((y - 36f) / 70f);
                c = Color.Lerp(skyBottom, skyTop, tSky);

                // 遠景の山並み（手稲山風）
                float mountainY = 40f + Mathf.Sin((x + 15f) * 0.06f) * 8f + Mathf.Cos(x * 0.12f) * 4f;
                if (y < mountainY && y >= 32f)
                {
                    c = Color.Lerp(new Color(0.25f, 0.42f, 0.35f), new Color(0.35f, 0.55f, 0.45f), (mountainY - y) / 10f);
                }

                // 芝生（下部）
                if (y < 36)
                {
                    float hill = 28f + Mathf.Sin(x * 0.05f) * 4f;
                    if (y < hill) c = lawnColor;
                }

                // ポプラの木（中央〜左：細く天に伸びる樹形）
                // 1本目（主ポプラ）
                float px1 = 52f;
                float distP1 = Mathf.Abs(x - px1);
                if (y >= 26 && y <= 98)
                {
                    float pyRel = (y - 26f) / 72f;
                    float width = Mathf.Sin(pyRel * Mathf.PI) * 7.5f;
                    if (distP1 <= width) c = poplarColor;
                    if (distP1 <= 1.2f && y <= 40) c = poplarTrunk;
                }
                // 2本目（奥のポプラ）
                float px2 = 40f;
                float distP2 = Mathf.Abs(x - px2);
                if (y >= 28 && y <= 85)
                {
                    float pyRel = (y - 28f) / 57f;
                    float width = Mathf.Sin(pyRel * Mathf.PI) * 5.5f;
                    if (distP2 <= width && distP1 > 5f) c = Color.Lerp(poplarColor, new Color(0.18f, 0.48f, 0.24f), 0.3f);
                }

                // クラーク博士のシルエット風レリーフ（右側 x=84, y=36〜76）
                float cx = x - 84f;
                float cy = y - 56f;
                // 頭部と胸像シルエット
                bool clarkHead = (cx * cx + (cy - 6f) * (cy - 6f)) < 64f; // 頭
                bool clarkShoulder = (cy >= -12f && cy <= 0f && Mathf.Abs(cx) < (14f - cy * 0.5f)); // 肩
                bool clarkArm = (cx > 4f && cx < 18f && cy > -4f && cy < 16f && (cy - cx * 0.8f) < 4f && (cy - cx * 0.8f) > -8f); // 掲げた腕
                if (clarkHead || clarkShoulder || clarkArm)
                {
                    c = new Color(0.15f, 0.28f, 0.24f); // 深いブロンズグリーン
                }

                // 黄金のイチョウの葉の装飾（左下 x=28, y=36）
                float gx = x - 28f;
                float gy = y - 36f;
                if (gx * gx + gy * gy < 36f && gy > -2f)
                {
                    c = ginkgoGold;
                }

                // 上空の北極星（サッポロスター / 北斗）
                float sx = Mathf.Abs(x - 96f);
                float sy = Mathf.Abs(y - 100f);
                if ((sx < 2f && sy < 8f) || (sy < 2f && sx < 8f) || (sx * sx + sy * sy < 9f))
                {
                    c = starColor;
                }

                // 外枠（白い立体リング）
                if (d > 54f) c = Color.white;
                else if (d > 52f) c = new Color(0.12f, 0.45f, 0.25f); // エルムグリーンの内枠ライン
                c.a = 1f;
            }
            px[y * n + x] = c;
        }
        return New(n, n, px, false);
    }


    public static Texture2D Checker()
    {
        var px = new[] { Color.white, new Color(0.08f, 0.08f, 0.08f), new Color(0.08f, 0.08f, 0.08f), Color.white };
        var t = New(2, 2, px, false);
        t.filterMode = FilterMode.Point;
        return t;
    }

    public static Texture2D Chevrons()
    {
        const int n = 128;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)n * 2f - 1f, v = y / (float)n;
            bool stripe = Mathf.Repeat(v * 3f + Mathf.Abs(u) * 0.9f, 1f) < 0.45f;
            bool border = Mathf.Abs(u) > 0.9f;
            px[y * n + x] = border ? new Color(0.15f, 0.15f, 0.15f) : stripe ? new Color(1f, 0.95f, 0.3f) : new Color(1f, 0.45f, 0.05f);
        }
        return New(n, n, px);
    }

    public static Texture2D SoftDot()
    {
        const int n = 64;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
            float a = Mathf.Clamp01(1f - d);
            px[y * n + x] = new Color(1, 1, 1, a * a);
        }
        var t = New(n, n, px, false);
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    // 画面周辺減光（スピード感・トンネル効果演出用）
    public static Texture2D Vignette()
    {
        const int n = 128;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float nx = (x / (float)n) * 2f - 1f;
            float ny = (y / (float)n) * 2f - 1f;
            float d = Mathf.Sqrt(nx * nx + ny * ny) * 0.72f;
            float a = Mathf.Clamp01(Mathf.Pow(d, 2.5f));
            px[y * n + x] = new Color(0, 0, 0.05f, a);
        }
        var t = New(n, n, px, false);
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    // スキッドマーク（タイヤ痕）テクスチャ
    public static Texture2D TireMark()
    {
        const int w = 32, h = 64;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)w;
            // 2本のスロット溝があるトレッドパターン
            float edgeFade = Mathf.Sin(u * Mathf.PI);
            float grooves = (u > 0.28f && u < 0.38f) || (u > 0.62f && u < 0.72f) ? 0.35f : 1f;
            float noise = 0.85f + Mathf.PerlinNoise(u * 8f, y * 0.2f) * 0.3f;
            float a = Mathf.Clamp01(edgeFade * grooves * noise);
            px[y * w + x] = new Color(0.08f, 0.08f, 0.09f, a * 0.85f);
        }
        var t = New(w, h, px, false);
        t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    // コース脇のスポンサー看板用テクスチャ
    public static Texture2D SponsorBanner(int variant)
    {
        const int w = 256, h = 64;
        var px = new Color[w * h];
        Color bg, accent, textCol;
        switch (variant % 4)
        {
            case 0: // TURBO (赤 & 白 & 黄)
                bg = new Color(0.85f, 0.12f, 0.12f);
                accent = new Color(1f, 0.85f, 0.1f);
                textCol = Color.white;
                break;
            case 1: // OCTANE (ダークブルー & シアン)
                bg = new Color(0.08f, 0.12f, 0.35f);
                accent = new Color(0.1f, 0.85f, 0.95f);
                textCol = Color.white;
                break;
            case 2: // NITRO (ブラック & ネオングリーン)
                bg = new Color(0.12f, 0.12f, 0.14f);
                accent = new Color(0.2f, 0.95f, 0.3f);
                textCol = new Color(0.95f, 1f, 0.95f);
                break;
            default: // APEX RACING (オレンジ & パープル)
                bg = new Color(0.95f, 0.45f, 0.05f);
                accent = new Color(0.55f, 0.15f, 0.85f);
                textCol = Color.white;
                break;
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)w, v = y / (float)h;
            // 上下の帯
            bool topBottom = v < 0.12f || v > 0.88f;
            // 斜めストライプ
            bool slash = Mathf.Repeat(u * 8f + v * 0.8f, 1f) < 0.25f && (v < 0.25f || v > 0.75f);
            // センターのレーシングストライプ
            bool centerStripe = Mathf.Abs(v - 0.5f) < 0.03f && u > 0.1f && u < 0.9f;

            Color c = topBottom ? accent : slash ? Color.Lerp(bg, accent, 0.6f) : centerStripe ? textCol : bg;
            // 縁取り
            if (x < 3 || x >= w - 3 || y < 3 || y >= h - 3) c = Color.white;
            px[y * w + x] = c;
        }
        var t = New(w, h, px, true);
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    // UIカード用の半透明グラデーション＆角丸パネル
    public static Texture2D CardPanel(int w, int h, Color topBg, Color botBg, Color borderColor, int borderThick)
    {
        var px = new Color[w * h];
        float radius = 10f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float cx = x < radius ? radius - x : (x >= w - radius ? x - (w - radius) : 0);
            float cy = y < radius ? radius - y : (y >= h - radius ? y - (h - radius) : 0);
            float dist = Mathf.Sqrt(cx * cx + cy * cy);
            if (dist > radius)
            {
                px[y * w + x] = Color.clear;
                continue;
            }

            float t = y / (float)h;
            Color bg = Color.Lerp(botBg, topBg, t);

            bool isBorder = x < borderThick || x >= w - borderThick || y < borderThick || y >= h - borderThick || (dist >= radius - borderThick);
            px[y * w + x] = isBorder ? borderColor : bg;
        }
        var tex = New(w, h, px, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // スピード線（集中線・ウィンドストリーム）用の先細り・フェードアウトテクスチャ
    public static Texture2D SpeedLine()
    {
        const int w = 128, h = 16;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1); // 0 = 根元 (外側), 1 = 先端 (中心側)
            float v = (y / (float)(h - 1)) * 2f - 1f; // -1 〜 +1 (中心が0)

            // 先端に向かって幅が鋭く狭くなる (u=0で1.0, u=1で0.0)
            float widthFactor = Mathf.Clamp01(1f - Mathf.Pow(u, 0.75f));
            float normY = Mathf.Abs(v) / Mathf.Max(0.001f, widthFactor);

            float alphaY = normY >= 1f ? 0f : (1f - normY * normY);

            // 長手方向のフェード（根元側はスムーズにフェードイン、先端側は綺麗に0へ消え去る）
            float alphaX = u < 0.12f ? (u / 0.12f) : Mathf.Pow(1f - u, 1.35f);

            float a = Mathf.Clamp01(alphaY * alphaX);
            px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        var t = New(w, h, px, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    // 現代的なダイアログパネル（ダークネイビーのグラデーション、上部光沢ハイライト、角丸、ネオン枠）
    public static Texture2D ModernModalPanel(int w, int h, Color topBg, Color botBg, Color glowBorder, int borderThick = 3, float radius = 14f)
    {
        string key = $"modal_{w}_{h}_{topBg.r:F2}_{glowBorder.r:F2}_{borderThick}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float cx = x < radius ? radius - x : (x >= w - radius ? x - (w - radius) : 0);
            float cy = y < radius ? radius - y : (y >= h - radius ? y - (h - radius) : 0);
            float dist = Mathf.Sqrt(cx * cx + cy * cy);
            if (dist > radius)
            {
                px[y * w + x] = Color.clear;
                continue;
            }

            float t = y / (float)h;
            Color bg = Color.Lerp(botBg, topBg, t);

            // 上部の光沢ハイライトライン
            if (y > h - 4 && dist <= radius - borderThick)
                bg = Color.Lerp(bg, Color.white, 0.35f);

            bool isBorder = x < borderThick || x >= w - borderThick || y < borderThick || y >= h - borderThick || (dist >= radius - borderThick);
            px[y * w + x] = isBorder ? glowBorder : bg;
        }
        var tex = New(w, h, px, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[key] = tex;
        return tex;
    }

    // レースゲーム風のスラント（斜め平行四辺形）バッジプレート
    public static Texture2D SlantedPlate(int w, int h, Color fill, Color border, int borderThick = 3, float slant = 0.22f)
    {
        string key = $"slant_{w}_{h}_{fill.r:F2}_{fill.g:F2}_{border.r:F2}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var px = new Color[w * h];
        float shiftWidth = h * slant;
        for (int y = 0; y < h; y++)
        {
            float leftX = (h - 1 - y) * slant;
            float rightX = leftX + (w - shiftWidth);
            for (int x = 0; x < w; x++)
            {
                if (x < leftX || x > rightX)
                {
                    px[y * w + x] = Color.clear;
                    continue;
                }
                bool isBorder = x < leftX + borderThick || x > rightX - borderThick || y < borderThick || y >= h - borderThick;
                px[y * w + x] = isBorder ? border : fill;
            }
        }
        var tex = New(w, h, px, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[key] = tex;
        return tex;
    }

    // 角丸の立体感あるアイテムスロット枠（ゴールド光沢フレーム＋ダークインナー）
    public static Texture2D ItemSlotFrame(int size = 114)
    {
        string key = $"itemslot_{size}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var px = new Color[size * size];
        float radius = 16f;
        Color outerGold = new Color(1f, 0.85f, 0.25f, 1f);
        Color innerDark = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        Color glowGold = new Color(1f, 0.65f, 0.1f, 0.7f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = x < radius ? radius - x : (x >= size - radius ? x - (size - radius) : 0);
            float cy = y < radius ? radius - y : (y >= size - radius ? y - (size - radius) : 0);
            float dist = Mathf.Sqrt(cx * cx + cy * cy);
            if (dist > radius)
            {
                px[y * size + x] = Color.clear;
                continue;
            }

            bool isBorder = x < 4 || x >= size - 4 || y < 4 || y >= size - 4 || (dist >= radius - 4);
            bool isInnerGlow = (x >= 4 && x < 8) || (x >= size - 8 && x < size - 4) || (y >= 4 && y < 8) || (y >= size - 8 && y < size - 4);

            px[y * size + x] = isBorder ? outerGold : isInnerGlow ? Color.Lerp(innerDark, glowGold, 0.5f) : innerDark;
        }
        var tex = New(size, size, px, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[key] = tex;
        return tex;
    }

    // 半透明ダークガラスミニマップ背景プレート
    public static Texture2D MinimapGlass(int size = 180)
    {
        string key = $"minimap_glass_{size}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var px = new Color[size * size];
        float radius = 18f;
        Color glassBg = new Color(0.06f, 0.08f, 0.14f, 0.75f);
        Color glassBorder = new Color(0.2f, 0.6f, 1f, 0.55f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = x < radius ? radius - x : (x >= size - radius ? x - (size - radius) : 0);
            float cy = y < radius ? radius - y : (y >= size - radius ? y - (size - radius) : 0);
            float dist = Mathf.Sqrt(cx * cx + cy * cy);
            if (dist > radius)
            {
                px[y * size + x] = Color.clear;
                continue;
            }
            bool isBorder = dist >= radius - 2f || x < 2 || x >= size - 2 || y < 2 || y >= size - 2;
            px[y * size + x] = isBorder ? glassBorder : glassBg;
        }
        var tex = New(size, size, px, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス：黄金のイチョウ葉テクスチャ
    public static Texture2D GinkgoFoliage(Color baseTint)
    {
        string key = $"ginkgo_leaf_{baseTint.r:F2}_{baseTint.g:F2}_{baseTint.b:F2}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int n = 128;
        var px = new Color[n * n];
        var rng = new System.Random(37);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float p1 = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            float p2 = Mathf.PerlinNoise((x + 60) * 0.22f, (y + 60) * 0.22f);
            float fine = (float)(rng.NextDouble() - 0.5) * 0.08f;
            float g = (p1 * 0.65f + p2 * 0.35f + fine - 0.5f) * 0.35f;

            var gold = Color.Lerp(new Color(1f, 0.85f, 0.10f), new Color(0.95f, 0.65f, 0.05f), p1);
            var c = Color.Lerp(gold, baseTint, 0.45f) * (1f + g);
            c.a = 1f;
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス：白樺の樹皮（白い樹皮に黒い横筋・節）
    public static Texture2D BirchBark()
    {
        const string key = "birch_bark";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int w = 128, h = 256;
        var px = new Color[w * h];
        var rng = new System.Random(53);
        Color baseWhite = new Color(0.94f, 0.93f, 0.89f);
        Color markDark = new Color(0.18f, 0.16f, 0.15f);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float noise = (float)(rng.NextDouble() - 0.5) * 0.05f;
            var c = baseWhite * (1f + noise);

            // 水平方向の皮目（黒い横線・斑点）
            float pY = Mathf.PerlinNoise(0, y * 0.15f);
            float pX = Mathf.PerlinNoise(x * 0.35f, y * 0.04f);
            if (pY > 0.68f && pX > 0.42f)
            {
                float markIntensity = Mathf.Clamp01((pY - 0.68f) * 4f) * Mathf.Clamp01((pX - 0.42f) * 3f);
                c = Color.Lerp(c, markDark, markIntensity * 0.85f);
            }

            // 大きめの節（knot）
            if (y % 64 < 10 && Mathf.Abs(x - 64) < 16)
            {
                float d = Mathf.Sqrt(Mathf.Pow(x - 64, 2) + Mathf.Pow((y % 64) - 5, 2) * 4f);
                if (d < 12f)
                {
                    c = Color.Lerp(c, markDark, (1f - d / 12f) * 0.9f);
                }
            }

            c.a = 1f;
            px[y * w + x] = c;
        }
        var tex = New(w, h, px);
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス：地面・路肩の黄金の落ち葉絨毯
    public static Texture2D GinkgoCarpet()
    {
        const string key = "ginkgo_carpet";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int n = 256;
        var px = new Color[n * n];
        var rng = new System.Random(71);
        Color bgSoil = new Color(0.38f, 0.32f, 0.22f);

        for (int i = 0; i < n * n; i++) px[i] = bgSoil;

        // 無数のイチョウの葉（扇形・楕円の重なり）を描画
        for (int i = 0; i < 900; i++)
        {
            int cx = rng.Next(n);
            int cy = rng.Next(n);
            int rad = rng.Next(5, 12);
            float rot = (float)rng.NextDouble() * Mathf.PI * 2f;
            Color leafCol = Color.Lerp(new Color(1f, 0.84f, 0.12f), new Color(0.96f, 0.62f, 0.08f), (float)rng.NextDouble());

            for (int dy = -rad; dy <= rad; dy++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                if (dx * dx + dy * dy <= rad * rad)
                {
                    int pxX = (cx + dx + n) % n;
                    int pxY = (cy + dy + n) % n;
                    px[pxY * n + pxX] = leafCol;
                }
            }
        }

        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス：第2農場モデルバーンの白い木造下見板張り外壁
    public static Texture2D BarnWoodWhite()
    {
        const string key = "barn_wood_white";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int n = 256;
        var px = new Color[n * n];
        var rng = new System.Random(89);
        Color woodBase = new Color(0.96f, 0.95f, 0.92f);
        Color plankShadow = new Color(0.68f, 0.65f, 0.60f);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float noise = (float)(rng.NextDouble() - 0.5) * 0.03f;
            var c = woodBase * (1f + noise);

            // 水平の板継ぎ目（16ピクセルごと）
            int row = y % 16;
            if (row == 0 || row == 1)
            {
                c = plankShadow;
            }
            else if (row == 2)
            {
                c = Color.Lerp(plankShadow, woodBase, 0.5f);
            }

            c.a = 1f;
            px[y * n + x] = c;
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス：赤レンガ（総合博物館・正門）
    public static Texture2D RedBrick()
    {
        const string key = "red_brick";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int n = 256;
        var px = new Color[n * n];
        var rng = new System.Random(97);
        Color mortar = new Color(0.85f, 0.83f, 0.80f);
        Color brickBase = new Color(0.68f, 0.28f, 0.18f);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int row = y / 16;
            int colOffset = (row % 2 == 0) ? 0 : 20;
            int col = (x + colOffset) % 40;

            bool isMortar = (y % 16 <= 1) || (col <= 1);
            if (isMortar)
            {
                px[y * n + x] = mortar;
            }
            else
            {
                float noise = (float)(rng.NextDouble() - 0.5) * 0.12f;
                px[y * n + x] = brickBase * (1f + noise);
            }
        }
        var tex = New(n, n, px);
        cache[key] = tex;
        return tex;
    }

    // 北大キャンパス専用案内看板
    public static Texture2D CampusBanner(int variant)
    {
        string key = $"campus_banner_{variant}";
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int w = 256, h = 64;
        var px = new Color[w * h];
        Color bg, border, textCol;

        switch (variant % 4)
        {
            case 0: // HOKKAIDO UNIVERSITY (北大グリーン & ゴールド)
                bg = new Color(0.10f, 0.44f, 0.24f);
                border = new Color(1f, 0.84f, 0.20f);
                textCol = Color.white;
                break;
            case 1: // BOYS, BE AMBITIOUS (クラーク博士・ディープネイビー & ゴールド)
                bg = new Color(0.12f, 0.22f, 0.38f);
                border = new Color(1f, 0.85f, 0.25f);
                textCol = new Color(1f, 0.92f, 0.55f);
                break;
            case 2: // SAPPORO 1876 (赤レンガ & クラシックアイボリー)
                bg = new Color(0.58f, 0.22f, 0.16f);
                border = new Color(0.92f, 0.88f, 0.80f);
                textCol = new Color(0.98f, 0.96f, 0.92f);
                break;
            default: // GINKGO AVENUE (黄金イチョウ & 北大グリーン)
                bg = new Color(0.96f, 0.76f, 0.12f);
                border = new Color(0.10f, 0.44f, 0.24f);
                textCol = new Color(0.10f, 0.36f, 0.18f);
                break;
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool isBorder = x < 4 || x >= w - 4 || y < 4 || y >= h - 4;
            bool isInnerLine = (x == 6 || x == w - 7 || y == 6 || y == h - 7);
            if (isBorder)
                px[y * w + x] = border;
            else if (isInnerLine)
                px[y * w + x] = Color.Lerp(bg, border, 0.7f);
            else
            {
                // 中央の飾りストライプ
                bool isCenterStripe = (y >= 28 && y <= 35) && (x < 30 || x > w - 31);
                px[y * w + x] = isCenterStripe ? Color.Lerp(bg, border, 0.5f) : bg;
            }
        }

        // 看板中央の星印やエンブレムのプロシージャル描画
        int midX = w / 2, midY = h / 2;
        for (int dy = -14; dy <= 14; dy++)
        for (int dx = -14; dx <= 14; dx++)
        {
            // 十字＋菱形星印
            if (Mathf.Abs(dx) + Mathf.Abs(dy) < 8 || (Mathf.Abs(dx) < 2 && Mathf.Abs(dy) < 14) || (Mathf.Abs(dy) < 2 && Mathf.Abs(dx) < 14))
            {
                int pxX = midX + dx;
                int pxY = midY + dy;
                if (pxX >= 0 && pxX < w && pxY >= 0 && pxY < h)
                    px[pxY * w + pxX] = border;
            }
        }

        var tex = New(w, h, px, false);
        cache[key] = tex;
        return tex;
    }
}

