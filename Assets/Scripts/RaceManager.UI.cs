using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    readonly Dictionary<(int, int, int, int, bool, int), GUIStyle> styleCache = new Dictionary<(int, int, int, int, bool, int), GUIStyle>();

    GUIStyle St(GUIStyle b, int size = 0, TextAnchor? anchor = null, FontStyle? fs = null, bool wrap = false, Color? text = null)
    {
        if (b == null) b = sBig ?? GUI.skin.label;
        int bid = b == sBig ? 0 : b == sMid ? 1 : b == sNum ? 3 : 2;
        int col = -1;
        if (text.HasValue) { Color32 c = text.Value; col = c.r << 16 | c.g << 8 | c.b; }
        var key = (bid, size, anchor.HasValue ? (int)anchor.Value : -1, fs.HasValue ? (int)fs.Value : -1, wrap, col);
        if (!styleCache.TryGetValue(key, out var st))
        {
            st = new GUIStyle(b);
            if (size > 0) st.fontSize = size;
            if (anchor.HasValue) st.alignment = anchor.Value;
            if (fs.HasValue) st.fontStyle = fs.Value;
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
            sBig = new GUIStyle(GUI.skin.label) { font = fontMain, fontSize = 90, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            sBig.normal.textColor = Color.white;
            sMid = new GUIStyle(sBig) { fontSize = 38 };
            sSmall = new GUIStyle(sBig) { fontSize = 22, fontStyle = FontStyle.Bold };
            sButton = new GUIStyle(GUI.skin.button) { font = fontMain, fontSize = 18, fontStyle = FontStyle.Bold };
            sField = new GUIStyle(GUI.skin.textField) { font = fontMain, fontSize = 20, alignment = TextAnchor.MiddleCenter };
            sNum = new GUIStyle(sBig) { font = fontNum, fontSize = 32 };
        }
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = 720f;

        if (state != State.Title && !TwoPlayer)
        {
            DrawVignette(ViewKart, w, h);
            if (RaceRunning) DrawSpeedLines(w, h);
        }

        if (showSettings)
        {
            DrawSettings(w, h);
            return;
        }

        switch (state)
        {
            case State.Title:
                if (LobbyActive) DrawLobby(w, h);
                else { DrawTitle(w, h); if (netMenu) DrawNetMenu(w, h); }
                break;
            case State.Countdown: DrawHuds(w, h, true); break;
            case State.Racing: DrawHuds(w, h, false); break;
            case State.Results: DrawHuds(w, h, false); if (Time.time > finishAt + 2.5f) DrawResults(w, h); break;
        }

        if (paused)
        {
            GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var pr = new Rect((w - 560) * 0.5f, (h - 260) * 0.5f, 560, 260);
            if (modalBgTex != null) GUI.DrawTexture(pr, modalBgTex);
            Outlined(new Rect(pr.x, pr.y + 16, pr.width, 70), "PAUSED", sBig, Color.white, 3);
            Outlined(new Rect(pr.x + 20, pr.y + 90, pr.width - 40, 50), "ESC / START : Resume     R [X] : Restart\nO : Settings     T [Y] : Title     Q [BACK] : Quit", St(sSmall, 16, TextAnchor.MiddleCenter), new Color(0.85f, 0.92f, 1f), 1.5f);
            if (GUI.Button(new Rect(pr.x + (pr.width - 220) * 0.5f, pr.y + 175, 220, 42), "SETTINGS  [O]", sButton))
            {
                showSettings = true;
            }
        }
    }

    void DrawSettings(float w, float h)
    {
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var r = new Rect((w - 580) * 0.5f, (h - 520) * 0.5f, 580, 520);
        DrawPopCard(r);
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 34), ribbonDriverTex, "★ SETTINGS & OPTIONS ★");

        float y = r.y + 60;
        float lw = 150, sw = 220, vw = 90;

        // BGM 音量
        GUI.Label(new Rect(r.x + 30, y, lw, 30), "BGM VOLUME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
        float newBgm = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 8, sw, 20), RaceAudio.MasterBgmVolume, 0f, 1f);
        if (Mathf.Abs(newBgm - RaceAudio.MasterBgmVolume) > 0.01f) Audio.SetMasterBgm(newBgm);
        GUI.Label(new Rect(r.x + 30 + lw + sw + 10, y, vw, 30), Mathf.RoundToInt(RaceAudio.MasterBgmVolume * 100f) + "%", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold));

        y += 42;
        // SE 音量
        GUI.Label(new Rect(r.x + 30, y, lw, 30), "SFX VOLUME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
        float newSfx = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 8, sw, 20), RaceAudio.MasterSfxVolume, 0f, 1f);
        if (Mathf.Abs(newSfx - RaceAudio.MasterSfxVolume) > 0.01f) Audio.SetMasterSfx(newSfx);
        GUI.Label(new Rect(r.x + 30 + lw + sw + 10, y, vw, 30), Mathf.RoundToInt(RaceAudio.MasterSfxVolume * 100f) + "%", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold));

        y += 42;
        // 画面モード
        GUI.Label(new Rect(r.x + 30, y, lw, 30), "DISPLAY", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
        string dispText = Screen.fullScreen ? "FULLSCREEN" : "WINDOWED";
        if (GUI.Button(new Rect(r.x + 30 + lw, y, 160, 32), dispText, sButton))
        {
            Screen.fullScreen = !Screen.fullScreen;
            PlayerPrefs.SetInt("tc_fullscreen", Screen.fullScreen ? 1 : 0);
        }

        y += 44;
        // プレイヤーネーム
        GUI.Label(new Rect(r.x + 30, y, lw, 30), "PLAYER NAME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
        string newName = GUI.TextField(new Rect(r.x + 30 + lw, y, 200, 32), playerName, 12, sField);
        SetPlayerName(newName);

        y += 46;
        // 操作説明ボックス
        var cBox = new Rect(r.x + 30, y, r.width - 60, 150);
        GUI.color = new Color(0.12f, 0.16f, 0.28f, 0.08f);
        GUI.DrawTexture(cBox, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(cBox, 1, new Color(0.7f, 0.75f, 0.85f));
        var cStyle = St(sSmall, 12, TextAnchor.UpperLeft, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f));
        string cHelp = "CONTROLS GUIDE:\n" +
                       "• Drive: W / S / Arrows / Gamepad RT (Gas) / LT (Brake) / A / B\n" +
                       "• Steer: A / D / Left Stick\n" +
                       "• Drift / Hop: Space / Left Shift / Gamepad RB or LB\n" +
                       "• Item: E / Left Ctrl / Gamepad X or Y\n" +
                       "• Rear View: C / Gamepad R3 (Press Right Stick)\n" +
                       "• Spectate (Finished): A / D / Left Stick to switch driver\n" +
                       "• Pause: ESC / Gamepad Start";
        GUI.Label(new Rect(cBox.x + 10, cBox.y + 6, cBox.width - 20, cBox.height - 12), cHelp, cStyle);

        // 閉じるボタン
        if (GUI.Button(new Rect(r.x + (r.width - 180) * 0.5f, r.yMax - 44, 180, 36), "CLOSE  [ESC]", sButton))
        {
            CloseSettings();
        }
    }

    void DrawTitle(float w, float h)
    {
        float t = Time.time;

        // 1. トップ：TURBO CIRCUIT ポップロゴ
        if (!Net.Busy && GUI.Button(new Rect(w - 180, 16, 150, 36), "⚙ OPTIONS [P]", sButton))
        {
            showSettings = true;
        }

        if (titleLogoTex != null)
        {
            float logoW = 380f;
            float logoH = logoW * (titleLogoTex.height / (float)titleLogoTex.width);
            float logoY = 8f + Mathf.Sin(t * 2.2f) * 2f;
            GUI.DrawTexture(new Rect((w - logoW) * 0.5f, logoY, logoW, logoH), titleLogoTex, ScaleMode.ScaleToFit);
        }
        else
        {
            var title = St(sBig, 76);
            Outlined(new Rect(0, 16, w, 80), "TURBO CIRCUIT", title, Color.HSVToRGB(Mathf.Repeat(t * 0.1f, 1f), 0.6f, 1f), 5);
        }

        float cardY = 175f;
        float cardH = 412f;
        float cardW = 330f;

        // 2. 左カード：コースセレクター（TRACK SELECTION）
        float leftX = 35f;
        DrawPopCard(new Rect(leftX, cardY, cardW, cardH));
        DrawPopRibbon(new Rect(leftX + 15, cardY + 12, cardW - 30, 32), ribbonTrackTex, "◄ TRACK SELECT [A][D] ►");

        var curDef = Track.Courses[SelectedCourse];
        if (trackBadgeTex[SelectedCourse] != null)
        {
            float bSize = 138f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 48, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        var courseTitleStyle = St(sSmall, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.12f, 0.18f, 0.35f));
        GUI.Label(new Rect(leftX + 8, cardY + 192, cardW - 16, 28), $"< {curDef.Name.ToUpper()} >", courseTitleStyle);

        string diffStr = SelectedCourse == 0 ? "★☆☆  NOVICE" : SelectedCourse == 1 ? "★★☆  ADVANCED" : SelectedCourse == 2 ? "★★★  EXPERT" : "★★☆  URBAN";
        Color diffBg = SelectedCourse == 0 ? new Color(0.2f, 0.78f, 0.42f) : SelectedCourse == 1 ? new Color(1f, 0.65f, 0.15f) : SelectedCourse == 2 ? new Color(1f, 0.28f, 0.38f) : new Color(0.62f, 0.3f, 0.95f);
        DrawPopPill(new Rect(leftX + (cardW - 140) * 0.5f, cardY + 224, 140, 22), diffStr, diffBg);

        var descStyle = St(sSmall, 13, TextAnchor.UpperCenter, FontStyle.Normal, true, new Color(0.26f, 0.30f, 0.42f));
        GUI.Label(new Rect(leftX + 18, cardY + 252, cardW - 36, 60), curDef.Description, descStyle);

        float bTime = GetBestTime(SelectedCourse);
        float bLap = GetBestLap(SelectedCourse);
        string recStr = bTime > 0 ? $"RECORD: {FormatTime(bTime)}  (LAP {FormatTime(bLap)})" : "NO RECORD YET";
        Color recCol = bTime > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.45f, 0.5f, 0.65f);
        DrawPopPill(new Rect(leftX + (cardW - 270) * 0.5f, cardY + 322, 270, 22), recStr, recCol);

        DrawPopPill(new Rect(leftX + (cardW - 200) * 0.5f, cardY + 356, 200, 24), $"{totalLaps} LAPS   |   8 KARTS GP", new Color(0.18f, 0.52f, 0.88f));


        // 3. 右カード：ドライバー＆マシンセレクター（DRIVER & MACHINE）
        float rightX = w - cardW - 35f;
        DrawPopCard(new Rect(rightX, cardY, cardW, cardH));
        DrawPopRibbon(new Rect(rightX + 15, cardY + 12, cardW - 30, 32), ribbonDriverTex, TwoPlayer ? "◄ P1 DRIVER [W][S] ►" : "◄ DRIVER & KART [W][S] ►");

        if (!TwoPlayer)
        {
            var curChar = KartCharacters[SelectedKart];
            var charTitleStyle = St(sSmall, 23, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Outlined(new Rect(rightX + 8, cardY + 48, cardW - 16, 30), $"< {curChar.name} >", charTitleStyle, curChar.color, 2);

            var driverNickStyle = St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.22f, 0.26f, 0.38f));
            GUI.Label(new Rect(rightX + 10, cardY + 80, cardW - 20, 20), curChar.driver, driverNickStyle);

            DrawPopPill(new Rect(rightX + 22, cardY + 106, cardW - 44, 24), curChar.trait, curChar.color * 0.9f);

            // 4項目ポップキャンディステータスゲージ
            float statStartY = cardY + 146f;
            DrawToonStatGauge(rightX + 22, statStartY + 0, cardW - 44, "SPEED", curChar.speed, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, statStartY + 42, cardW - 44, "ACCEL", curChar.accel, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, statStartY + 84, cardW - 44, "STEER", curChar.handling, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, statStartY + 126, cardW - 44, "WEIGHT", curChar.weight, 8, new Color(1f, 0.32f, 0.38f));

            var switchGuide = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.45f, 0.5f, 0.65f));
            GUI.Label(new Rect(rightX + 10, cardY + 365, cardW - 20, 20), "Press [W][S] to switch machine", switchGuide);
        }
        else
        {
            // 2P: P1 & P2 の両方の情報をすっきり分割表示
            var c1 = KartCharacters[SelectedKart];
            Outlined(new Rect(rightX + 8, cardY + 44, cardW - 16, 26), $"P1: < {c1.name} >", St(sSmall, 19, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), c1.color, 2);
            DrawPopPill(new Rect(rightX + 22, cardY + 72, cardW - 44, 20), c1.trait, c1.color * 0.9f);
            float s1Y = cardY + 98f;
            DrawToonStatGauge(rightX + 22, s1Y + 0, cardW - 44, "SPD", c1.speed, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, s1Y + 20, cardW - 44, "ACC", c1.accel, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, s1Y + 40, cardW - 44, "STR", c1.handling, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, s1Y + 60, cardW - 44, "WGT", c1.weight, 8, new Color(1f, 0.32f, 0.38f));

            var c2 = KartCharacters[SelectedKart2];
            Outlined(new Rect(rightX + 8, cardY + 194, cardW - 16, 26), $"P2: < {c2.name} >", St(sSmall, 19, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), c2.color, 2);
            DrawPopPill(new Rect(rightX + 22, cardY + 222, cardW - 44, 20), c2.trait, c2.color * 0.9f);
            float s2Y = cardY + 248f;
            DrawToonStatGauge(rightX + 22, s2Y + 0, cardW - 44, "SPD", c2.speed, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, s2Y + 20, cardW - 44, "ACC", c2.accel, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, s2Y + 40, cardW - 44, "STR", c2.handling, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, s2Y + 60, cardW - 44, "WGT", c2.weight, 8, new Color(1f, 0.32f, 0.38f));

            var switchGuide = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.45f, 0.5f, 0.65f));
            GUI.Label(new Rect(rightX + 10, cardY + 365, cardW - 20, 20), "P1: [W][S]   •   P2: [UP][DOWN]", switchGuide);
        }

        // プレイ人数の切り替え
        var modeBg = TwoPlayer ? new Color(0.95f, 0.3f, 0.5f) : new Color(0.18f, 0.52f, 0.88f);
        if (Online)
        {
            string codeStr = string.IsNullOrEmpty(Net.JoinCode) ? "" : $"  |  CODE: {Net.JoinCode}";
            string statusStr = Net.IsHost
                ? $"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  YOU = HOST [ENTER TO RACE]"
                : $"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  HOST DECIDES START";
            DrawPopPill(new Rect((w - 560) * 0.5f, 556f, 560, 28), statusStr, new Color(0.18f, 0.7f, 0.4f));
        }
        else
            DrawPopPill(new Rect((w - 300) * 0.5f, 556f, 300, 28), TwoPlayer ? "[TAB]  2 PLAYERS  (SPLIT SCREEN)" : "[TAB]  1 PLAYER", modeBg);


        // 4. 画面中央下部：PRESS ENTER TO RACE（ぷっくり立体キャンディボタン）
        float pulse = (Mathf.Sin(t * 6f) + 1f) * 0.5f;
        float maxAvailableBtnW = Mathf.Max(260f, w - (cardW + 35f) * 2f - 20f);
        float btnW = Mathf.Min(440f + pulse * 10f, maxAvailableBtnW);
        float btnH = 50f + pulse * 4f;
        float btnX = (w - btnW) * 0.5f;
        float btnY = 598f - pulse * 2f;

        if (btnRaceTex != null)
        {
            GUI.DrawTexture(new Rect(btnX, btnY, btnW, btnH), btnRaceTex, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.color = new Color(1f, 0.7f, 0.1f);
            GUI.DrawTexture(new Rect(btnX, btnY, btnW, btnH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(new Rect(btnX, btnY, btnW, btnH), 3, new Color(0.1f, 0.15f, 0.3f));
        }

        var startStyle = St(sMid, (int)(24 + pulse * 2), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
        Outlined(new Rect(btnX, btnY + 2, btnW, btnH - 4), "►►  PRESS ENTER TO RACE!  ◄◄", startStyle, Color.white, 2.5f);


        // 5. 画面最下部：コントロールガイドバー
        GUI.color = new Color(0.08f, 0.12f, 0.22f, 0.94f);
        GUI.DrawTexture(new Rect(0, h - 34, w, 34), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, h - 34, w, 34), 1, new Color(0.2f, 0.3f, 0.45f, 0.6f));

        var barStyle = St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Normal);
        string guideText = TwoPlayer
            ? "P1: [WASD] [SPACE] drift [E/LB] item  •  P2: [ARROWS] [R-SHIFT] drift [R-CTRL] item  •  [TAB/Y] 1P/2P  •  [ESC/START] Pause"
            : "[W][S] Driver  •  [A][D/LB/RB] Track  •  [SPACE/RB] Drift  •  [E/LB/X] Item  •  [TAB/Y] 2P  •  [O/X] Net  •  [ESC/START] Pause";
        Outlined(new Rect(0, h - 32, w, 28), guideText, barStyle, new Color(0.9f, 0.95f, 1f), 1);
    }

    void DrawPopCard(Rect r)
    {
        if (cardPopTex != null)
        {
            GUI.DrawTexture(r, cardPopTex, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.color = Color.white;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            DrawFrame(r, 3, new Color(0.1f, 0.15f, 0.25f));
        }
    }

    void DrawPopRibbon(Rect r, Texture2D tex, string text)
    {
        if (tex != null)
        {
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.color = new Color(0.15f, 0.65f, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(r, 2, new Color(0.1f, 0.15f, 0.25f));
        }
        var st = St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
        Outlined(r, text, st, Color.white, 1.5f);
    }

    void DrawPopPill(Rect r, string text, Color bg)
    {
        GUI.color = bg;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(r, 1.5f, new Color(0.1f, 0.15f, 0.25f, 0.8f));

        var st = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        Outlined(r, text, st, Color.white, 1.2f);
    }

    void DrawToonStatGauge(float x, float y, float w, string label, int value, int maxVal, Color barColor)
    {
        var lblStyle = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.15f, 0.2f, 0.35f));
        GUI.Label(new Rect(x, y, 70, 16), label, lblStyle);

        float barX = x + 72;
        float barW = w - 72;
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
    }

    void DrawResults(float w, float h)
    {
        var panel = new Rect(w / 2 - 280, 110, 560, 470);
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(panel, 3, new Color(1f, 0.85f, 0.2f));
        string head = "RESULTS";
        if (TwoPlayer) head = Player.Place < Player2.Place ? "P1 WINS!" : "P2 WINS!";
        else if (Online) head = Player.Place == 1 ? "YOU WIN!" : $"{Ordinal(Player.Place)} PLACE";
        Outlined(new Rect(panel.x, panel.y + 10, panel.width, 50), head, sMid, new Color(1f, 0.85f, 0.2f));

        if (newRecordTime || newRecordLap)
        {
            string recBanner = newRecordTime ? "★ NEW COURSE RECORD! ★" : "★ NEW LAP RECORD! ★";
            Outlined(new Rect(panel.x, panel.y + 54, panel.width, 24), recBanner, St(sSmall, 16, TextAnchor.MiddleCenter, FontStyle.Bold), new Color(1f, 0.85f, 0.2f), 2);
        }

        var order = placeOrder;
        var left = St(sSmall, 0, TextAnchor.MiddleLeft);
        var rightS = St(sSmall, 0, TextAnchor.MiddleRight);
        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            var row = new Rect(panel.x + 30, panel.y + 80 + i * 44, panel.width - 60, 40);
            if (k.IsPlayer || (Online && playerKarts.ContainsKey(Karts.IndexOf(k))))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.18f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(row.x + 70, row.y + 10, 20, 20), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Outlined(new Rect(row.x + 8, row.y, 60, 40), Ordinal(k.Place), left, i == 0 ? new Color(1f, 0.85f, 0.2f) : Color.white, 1);
            Outlined(new Rect(row.x + 104, row.y, 200, 40), k.Name, left, Color.white, 1);
            Outlined(new Rect(row.x, row.y, row.width - 10, 40), k.Finished ? FormatTime(k.FinishTime) : "--:--.--", rightS, Color.white, 1);
        }
        if (Mathf.Repeat(Time.time, 1.1f) < 0.75f)
            Outlined(new Rect(0, panel.yMax + 18, w, 40), "ENTER [A] : Race Again       ESC / T [B] : Title", St(sMid, 23), Color.white);
    }

    static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");

    static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return string.Format("{0}:{1:00.00}", m, s);
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
