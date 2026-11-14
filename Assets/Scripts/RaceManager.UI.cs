using System.Collections.Generic;
using UnityEngine;

// UI（IMGUI）：HUD、タイトル、リザルト、設定メニュー、ビネット・集中線
public partial class RaceManager
{
    // ───────────────────────── UI ─────────────────────────

    GUIStyle sBig, sMid, sSmall;

    readonly List<Kart> placeOrder = new List<Kart>();

    static int ComparePlaces(Kart a, Kart b)
    {
        if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
        float ka = a.Finished ? a.FinishTime : -a.RaceDistance;
        float kb = b.Finished ? b.FinishTime : -b.RaceDistance;
        int c = ka.CompareTo(kb);
        return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
    }

    // OnGUI 内での GUIStyle 生成を避けるため、(基準, サイズ, 配置, 書体, 折返し, 色) ごとにキャッシュする
    readonly Dictionary<(int, int, int, int, bool, long), GUIStyle> styleCache = new Dictionary<(int, int, int, int, bool, long), GUIStyle>();

    GUIStyle St(GUIStyle b, int size = 0, TextAnchor? anchor = null, FontStyle? fs = null, bool wrap = false, Color? text = null)
    {
        if (b == null) b = sBig ?? GUI.skin.label;
        int bid = b == sBig ? 0 : b == sMid ? 1 : b == sSmall ? 2 : b == sNum ? 3 : b == sMono ? 4 : b == sSys ? 5 : b == sButton ? 6 : b == sField ? 7 : 8;
        FontStyle? effFs = fs;
        // 日本語フォント（MochiyPopOne / MPLUSRounded1c-ExtraBold）は元から極太ウェイトのため、
        // Unity の疑似ボールド（FontStyle.Bold）を重ねると画数の多い漢字が潰れるのを防ぐ
        if (Loc.IsJa && effFs.HasValue && (b.font == fontJpPop || b.font == fontJpRound))
        {
            effFs = FontStyle.Normal;
        }
        long col = -1L;
        if (text.HasValue) { Color32 c = text.Value; col = ((long)c.a << 24) | ((long)c.r << 16) | ((long)c.g << 8) | c.b; }
        var key = (bid, size, anchor.HasValue ? (int)anchor.Value : -1, effFs.HasValue ? (int)effFs.Value : -1, wrap, col);
        if (!styleCache.TryGetValue(key, out var st))
        {
            st = new GUIStyle(b);
            if (size > 0) st.fontSize = size;
            if (anchor.HasValue) st.alignment = anchor.Value;
            if (effFs.HasValue) st.fontStyle = effFs.Value;
            st.wordWrap = wrap;
            if (text.HasValue) st.normal.textColor = text.Value;
            styleCache[key] = st;
        }
        return st;
    }

    void OnGUI()
    {
        if (sBig == null)
        {
            var mainFs = Loc.IsJa ? FontStyle.Normal : FontStyle.BoldAndItalic;
            var subFs = Loc.IsJa ? FontStyle.Normal : FontStyle.Bold;
            var smallFont = (Loc.IsJa && fontJpRound != null) ? fontJpRound : fontMain;
            sBig = new GUIStyle(GUI.skin.label) { font = fontMain, fontSize = 90, fontStyle = mainFs, alignment = TextAnchor.MiddleCenter };
            sBig.normal.textColor = Color.white;
            sMid = new GUIStyle(sBig) { fontSize = 38 };
            sSmall = new GUIStyle(sBig) { font = smallFont, fontSize = 22, fontStyle = subFs };
            sButton = new GUIStyle(GUI.skin.button) { font = fontMain, fontSize = 18, fontStyle = subFs };
            sField = new GUIStyle(GUI.skin.textField) { font = smallFont, fontSize = 20, alignment = TextAnchor.MiddleCenter };
            sNum = new GUIStyle(sBig) { font = fontNum, fontSize = 32, fontStyle = FontStyle.BoldAndItalic };
            sMono = new GUIStyle(sBig) { font = fontMono, fontSize = 24, fontStyle = FontStyle.Bold };
            sSys = new GUIStyle(GUI.skin.label) { font = fontSys ?? smallFont, fontSize = 14, fontStyle = subFs, alignment = TextAnchor.UpperLeft, wordWrap = true };
        }
        float scale = Screen.height / 720f;
        if (previewSpectatorsRequested || suppressVerifyGui) return;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = 720f;

        if (state == State.Racing && !paused && !TwoPlayer)
        {
            DrawVignette(ViewKart, w, h);
            DrawSpeedLines(w, h);
        }

        bool updateModalOpen = updater != null && updater.ShowModal;
        bool overlayModalOpen = updateModalOpen || showSettings || paused || showQuitConfirm || showRecords;

        // 背面レイヤー（モーダル表示中は背面ボタンがマウスクリックを奪わないよう GUI.enabled = false にする）
        switch (state)
        {
            case State.Title:
                if (LobbyActive)
                {
                    GUI.enabled = !overlayModalOpen;
                    DrawLobby(w, h);
                }
                else
                {
                    GUI.enabled = !overlayModalOpen && !netMenu;
                    DrawTitle(w, h);
                    if (netMenu)
                    {
                        GUI.enabled = !overlayModalOpen;
                        DrawNetMenu(w, h);
                    }
                }
                break;
            case State.Intro:
                GUI.enabled = !overlayModalOpen;
                DrawIntroUI(w, h);
                break;
            case State.Countdown:
                GUI.enabled = !overlayModalOpen;
                DrawHuds(w, h, true);
                break;
            case State.Racing:
                GUI.enabled = !overlayModalOpen;
                DrawHuds(w, h, false);
                break;
            case State.Results:
                GUI.enabled = !overlayModalOpen;
                if (Time.time <= finishAt + 2.5f) DrawHuds(w, h, false);
                else DrawResults(w, h);
                break;
        }

        if (paused && !showSettings)
        {
            GUI.enabled = !updateModalOpen && !showQuitConfirm;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var pr = new Rect((w - 480) * 0.5f, (h - 440) * 0.5f, 480, 440);
            DrawPopCard(pr);
            DrawPopRibbon(new Rect(pr.x + 20, pr.y + 14, pr.width - 40, 36), ribbonDriverTex, Loc.T("★  PAUSED  ★", "★  ポーズメニュー  ★"));

            // サブタイトル（白カード上で読みやすい濃紺）
            GUI.Label(new Rect(pr.x + 20, pr.y + 54, pr.width - 40, 20), Loc.T("D-PAD / STICK TO SELECT  •  [A / ENTER] CONFIRM", "十字キー / スティックで選択  •  [A / ENTER] 決定"), St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.35f, 0.42f, 0.55f)));

            // 5つのポップなメニューボタン
            float bx = pr.x + 40f, bw = pr.width - 80f, by = pr.y + 82f, bh = 46f, bGap = 12f;

            if (DrawModernButton(new Rect(bx, by, bw, bh), Loc.T("►  RESUME RACE", "►  レースを再開"), "[ESC / START / B]", new Color(0.18f, 0.78f, 0.42f), true, pauseFocus == 0))
            {
                paused = false;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), Loc.T("►  RESTART RACE", "►  最初からやり直す"), "[R / X]", new Color(1f, 0.62f, 0.15f), false, pauseFocus == 1))
            {
                paused = false;
                Time.timeScale = 1f;
                AudioListener.pause = false;
                StartCountdown();
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), Loc.T("★  SETTINGS & KEYS", "★  設定・キーコンフィグ"), "[O]", new Color(0.18f, 0.62f, 0.95f), false, pauseFocus == 2))
            {
                showSettings = true;
                settingsFocus = 0;
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), Loc.T("◄  TITLE MENU", "◄  タイトル画面へ戻る"), "[T / Y]", new Color(0.65f, 0.35f, 0.92f), false, pauseFocus == 3))
            {
                paused = false;
                ReturnToTitle();
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), Loc.T("◄  QUIT GAME", "◄  ゲームを終了"), "[Q / BACK]", new Color(0.95f, 0.32f, 0.32f), false, pauseFocus == 4))
            {
                Application.Quit();
            }
        }

        if (showRecords)
        {
            GUI.enabled = !updateModalOpen && !showQuitConfirm && !showSettings;
            DrawRecordsModal(w, h);
        }

        if (showSettings)
        {
            GUI.enabled = !updateModalOpen && !showQuitConfirm;
            DrawSettings(w, h);
        }

        if (updateModalOpen)
        {
            GUI.enabled = !showQuitConfirm;
            DrawUpdateModal(w, h);
        }

        if (showQuitConfirm)
        {
            GUI.enabled = true;
            DrawQuitModal(w, h);
        }

        GUI.enabled = true;
    }

    void DrawFocusHighlight(Rect r, float radius = 8f)
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
        Color borderCol = Color.Lerp(new Color(1f, 0.72f, 0.05f, 0.98f), new Color(0.10f, 0.82f, 1f, 0.98f), pulse);
        var hr = new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f);
        DrawFrame(hr, 2.5f, borderCol);
    }

    // 現代的なポップ立体角丸ボタン（角丸カードにマッチする丸み、ホバー/パッド選択発光、下部シャドウ、ショートカットキー表記、クリック判定）
    bool DrawModernButton(Rect r, string label, string shortcut = "", Color? accentColor = null, bool isPrimary = false, bool focused = false)
    {
        Vector2 mouse = Event.current.mousePosition;
        bool hover = GUI.enabled && (r.Contains(mouse) || focused);
        Color acc = accentColor ?? (isPrimary ? new Color(1f, 0.62f, 0.1f) : new Color(0.15f, 0.62f, 0.95f));
        float radius = Mathf.Min(12f, r.height * 0.35f);

        int iw = Mathf.RoundToInt(r.width);
        int ih = Mathf.RoundToInt(r.height);

        // ドロップシャドウ（角丸）
        float shY = hover ? 2f : 3.5f;
        var shTex = TextureGen.RoundedShadow(iw, ih, radius);
        GUI.DrawTexture(new Rect(r.x, r.y + shY, r.width, r.height), shTex);

        // ボタン本体（プロシージャル立体角丸テクスチャ）
        var btnTex = TextureGen.PopButton(iw, ih, radius, acc, hover);
        GUI.DrawTexture(r, btnTex);

        if (focused && GUI.enabled)
        {
            DrawFocusHighlight(r, radius);
        }

        // ラベル描画（白太文字＋黒アウトラインで抜群の視認性）
        float pad = 16f;
        var lblStyle = St(sMid, 15, string.IsNullOrEmpty(shortcut) ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
        Rect textRect = string.IsNullOrEmpty(shortcut) ? r : new Rect(r.x + pad, r.y, r.width - pad * 2, r.height);
        Outlined(textRect, label, lblStyle, Color.white, 1.5f);

        // ショートカットキー描画
        if (!string.IsNullOrEmpty(shortcut))
        {
            var scStyle = St(sMono, 13, TextAnchor.MiddleRight, FontStyle.Bold);
            Color scColor = new Color(1f, 1f, 1f, hover ? 1f : 0.88f);
            Outlined(new Rect(r.x + pad, r.y, r.width - pad * 2, r.height), shortcut, scStyle, scColor, 1.2f);
        }

        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            GUIUtility.keyboardControl = 0;
            return true;
        }
        return false;
    }

    void DrawPopCard(Rect r)
    {
        int iw = Mathf.RoundToInt(r.width);
        int ih = Mathf.RoundToInt(r.height);
        float radius = 18f;

        var shTex = TextureGen.RoundedShadow(iw, ih, radius);
        GUI.DrawTexture(new Rect(r.x, r.y + 4.5f, r.width, r.height), shTex);

        var cardTex = TextureGen.PopCard(iw, ih, radius);
        GUI.DrawTexture(r, cardTex);
    }

    void DrawPopRibbon(Rect r, Texture2D tex, string text)
    {
        int iw = Mathf.RoundToInt(r.width);
        int ih = Mathf.RoundToInt(r.height);
        Color ribbonCol = (tex != null && tex == ribbonTrackTex)
            ? new Color(0.12f, 0.64f, 0.98f)
            : new Color(1.00f, 0.56f, 0.12f);

        var shTex = TextureGen.RoundedShadow(iw, ih, ih * 0.5f);
        GUI.DrawTexture(new Rect(r.x, r.y + 2.5f, r.width, r.height), shTex);

        var ribTex = TextureGen.PopRibbon(iw, ih, ribbonCol);
        GUI.DrawTexture(r, ribTex);

        var st = St(sMid, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
        Outlined(r, text, st, Color.white, 1.5f);
    }

    void DrawPopPill(Rect r, string text, Color bg)
    {
        int iw = Mathf.RoundToInt(r.width);
        int ih = Mathf.RoundToInt(r.height);
        var pillTex = TextureGen.PopPill(iw, ih, bg);
        GUI.DrawTexture(r, pillTex);

        var st = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        Outlined(r, text, st, Color.white, 1.2f);
    }

    void DrawToonStatGauge(float x, float y, float w, string label, int value, int maxVal, Color barColor)
    {
        var lblStyle = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.15f, 0.2f, 0.35f));
        GUI.Label(new Rect(x, y, 62, 16), label, lblStyle);

        float barX = x + 64;
        float numW = 34f;
        float barW = w - 64 - numW; // 右端に数値バッジ用スペース
        float barH = 14;
        float barY = y + 1;

        // トラフ背景（ライトグレーブルー）
        GUI.color = new Color(0.86f, 0.90f, 0.95f, 1f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
        DrawFrame(new Rect(barX, barY, barW, barH), 1, new Color(0.72f, 0.78f, 0.86f));

        int segments = maxVal;
        float gap = 2f;
        float segW = (barW - (segments - 1) * gap) / segments;
        for (int i = 0; i < segments; i++)
        {
            float sx = barX + i * (segW + gap);
            if (i < value)
            {
                // ポップなキャンディブロック
                GUI.color = barColor;
                GUI.DrawTexture(new Rect(sx, barY + 1, segW, barH - 2), Texture2D.whiteTexture);
                // 上部ツヤ
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(sx, barY + 1, segW, (barH - 2) * 0.45f), Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = new Color(0.92f, 0.94f, 0.98f, 1f);
                GUI.DrawTexture(new Rect(sx, barY + 1, segW, barH - 2), Texture2D.whiteTexture);
            }
        }
        GUI.color = Color.white;

        // 右端の数値バッジチップ（例: "5/8"）
        Rect chipRect = new Rect(barX + barW + 6f, barY - 1f, 28f, barH + 2f);
        GUI.color = new Color(0.12f, 0.18f, 0.32f, 0.85f);
        GUI.DrawTexture(chipRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(chipRect, 1f, new Color(0.35f, 0.45f, 0.65f, 0.6f));
        GUI.Label(chipRect, $"{value}/{maxVal}", St(sMono, 11, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.92f, 0.96f, 1f)));
    }

    static readonly string[] Ordinals = { "", "1ST", "2ND", "3RD", "4TH", "5TH", "6TH", "7TH", "8TH" };
    static string Ordinal(int n) => (n >= 1 && n <= 8) ? Ordinals[n] : (n + "TH");

    static readonly string[] digits2 = InitializeDigits2();
    static string[] InitializeDigits2()
    {
        var d = new string[100];
        for (int i = 0; i < 100; i++) d[i] = i.ToString("D2");
        return d;
    }

    static readonly string[] speedStrings = InitializeSpeedStrings();
    static string[] InitializeSpeedStrings()
    {
        var arr = new string[250];
        for (int i = 0; i < arr.Length; i++) arr[i] = i + " km/h";
        return arr;
    }
    public static string GetSpeedString(int spd)
    {
        spd = Mathf.Clamp(spd, 0, speedStrings.Length - 1);
        return speedStrings[spd];
    }

    static readonly string[,] lapStrings = InitializeLapStrings();
    static string[,] InitializeLapStrings()
    {
        var arr = new string[10, 10];
        for (int l = 1; l < 10; l++)
        for (int t = 1; t < 10; t++)
            arr[l, t] = $"LAP {l} / {t}";
        return arr;
    }
    public static string GetLapString(int lap, int total)
    {
        lap = Mathf.Clamp(lap, 1, 9);
        total = Mathf.Clamp(total, 1, 9);
        return lapStrings[lap, total];
    }

    static string FormatTime(float t)
    {
        if (t <= 0f) return "00:00.00";
        if (t >= 5999f) return "99:59.99";
        int totalSeconds = (int)t;
        int m = totalSeconds / 60;
        int s = totalSeconds - m * 60;
        int cs = (int)((t - totalSeconds) * 100f);
        if (cs < 0) cs = 0;
        else if (cs > 99) cs = 99;
        return string.Concat(m < 100 ? digits2[m] : m.ToString(), ":", digits2[s], ".", digits2[cs]);
    }

    static void DrawFrame(Rect r, float t, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Outlined(Rect r, string text, GUIStyle style, Color color, float thickness = 3f)
    {
        var prev = GUI.color;
        GUI.color = new Color(0, 0, 0, color.a * 0.85f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * thickness, r.y + Mathf.Sin(a) * thickness, r.width, r.height), text, style);
        }
        GUI.color = color;
        GUI.Label(r, text, style);
        GUI.color = prev;
    }

}
