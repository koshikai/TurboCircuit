using UnityEngine;

// 実行時にテクスチャを生成する（画像ファイル不要）
public static class TextureGen
{
    static Texture2D New(int w, int h, Color[] px, bool mip = true)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, mip) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    public static Texture2D Asphalt()
    {
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
            float tireWear = Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 2f) * 0.04f; // 走行ラインの黒ずみ（ラバー乗り）
            float g = 0.28f + fineNoise + coarseNoise - tireWear;
            var c = new Color(g, g, g * 1.03f, 1f);

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
        return New(n, n, px);
    }

    public static Texture2D Stripes(Color a, Color b)
    {
        const int w = 16, h = 64;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            px[y * w + x] = y < h / 2 ? a : b;
        return New(w, h, px);
    }

    public static Texture2D Grass()
    {
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
        return New(n, n, px);
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
}
