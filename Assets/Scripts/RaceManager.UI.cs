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
        int bid = b == sBig ? 0 : b == sMid ? 1 : b == sNum ? 3 : b == sMono ? 4 : 2;
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
            sMono = new GUIStyle(sBig) { font = fontMono, fontSize = 24, fontStyle = FontStyle.Bold };
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
            case State.Results:
                if (Time.time <= finishAt + 2.5f) DrawHuds(w, h, false);
                else DrawResults(w, h);
                break;
        }

        if (paused)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var pr = new Rect((w - 480) * 0.5f, (h - 440) * 0.5f, 480, 440);
            DrawPopCard(pr);
            DrawPopRibbon(new Rect(pr.x + 20, pr.y + 14, pr.width - 40, 36), ribbonDriverTex, "⏸  PAUSED  ⏸");

            // サブタイトル（白カード上で読みやすい濃紺）
            GUI.Label(new Rect(pr.x + 20, pr.y + 54, pr.width - 40, 20), "GAME IS PAUSED", St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.35f, 0.42f, 0.55f)));

            // 5つのポップなメニューボタン
            float bx = pr.x + 40f, bw = pr.width - 80f, by = pr.y + 82f, bh = 46f, bGap = 12f;

            if (DrawModernButton(new Rect(bx, by, bw, bh), "▶  RESUME RACE", "[ESC / START]", new Color(0.18f, 0.78f, 0.42f), true))
            {
                paused = false;
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), "🔄  RESTART RACE", "[R / X]", new Color(1f, 0.62f, 0.15f)))
            {
                paused = false;
                ResetRace();
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), "⚙  SETTINGS & KEYS", "[O]", new Color(0.18f, 0.62f, 0.95f)))
            {
                showSettings = true;
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), "🏠  TITLE MENU", "[T / Y]", new Color(0.65f, 0.35f, 0.92f)))
            {
                paused = false;
                ReturnToTitle();
            }
            by += bh + bGap;

            if (DrawModernButton(new Rect(bx, by, bw, bh), "🚪  QUIT GAME", "[Q / BACK]", new Color(0.95f, 0.32f, 0.32f)))
            {
                Application.Quit();
            }
        }
    }

    // 現代的なポップ立体ボタン（ホバー発光、下部シャドウ、ショートカットキー表記、クリック判定）
    bool DrawModernButton(Rect r, string label, string shortcut = "", Color? accentColor = null, bool isPrimary = false)
    {
        Vector2 mouse = Event.current.mousePosition;
        bool hover = r.Contains(mouse);
        Color acc = accentColor ?? (isPrimary ? new Color(1f, 0.62f, 0.1f) : new Color(0.15f, 0.62f, 0.95f));

        // ドロップシャドウ
        GUI.color = new Color(0f, 0f, 0f, hover ? 0.35f : 0.25f);
        GUI.DrawTexture(new Rect(r.x, r.y + (hover ? 2f : 3f), r.width, r.height), Texture2D.whiteTexture);

        // ボタン本体背景（ホバー時は少し白をブレンドして発光、通常時は発色の良いアクセントカラー）
        Color bg = hover ? Color.Lerp(acc, Color.white, 0.22f) : acc;
        GUI.color = bg;
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        // ボタン下部の立体段差シャドウ
        GUI.color = new Color(0f, 0f, 0f, 0.2f);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 3f, r.width, 3f), Texture2D.whiteTexture);

        // 外枠ストローク（ホバー時はホワイト、通常時は濃紺の引き締まったフチ）
        Color border = hover ? Color.white : new Color(0.08f, 0.12f, 0.22f, 0.85f);
        DrawFrame(r, hover ? 2.5f : 1.8f, border);
        GUI.color = Color.white;

        // ラベル描画（白太文字＋黒アウトラインで抜群の視認性）
        float pad = 16f;
        var lblStyle = St(sSmall, 15, string.IsNullOrEmpty(shortcut) ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
        Rect textRect = string.IsNullOrEmpty(shortcut) ? r : new Rect(r.x + pad, r.y, r.width - pad * 2, r.height);
        Outlined(textRect, label, lblStyle, Color.white, 1.5f);

        // ショートカットキー描画
        if (!string.IsNullOrEmpty(shortcut))
        {
            var scStyle = St(sMono, 13, TextAnchor.MiddleRight, FontStyle.Bold);
            Color scColor = new Color(1f, 1f, 1f, hover ? 1f : 0.88f);
            Outlined(new Rect(r.x + pad, r.y, r.width - pad * 2, r.height), shortcut, scStyle, scColor, 1.2f);
        }

        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    void DrawSettings(float w, float h)
    {
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var r = new Rect((w - 620) * 0.5f, (h - 540) * 0.5f, 620, 540);
        DrawPopCard(r);
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 34), ribbonDriverTex, "★ SETTINGS & OPTIONS ★");

        // ──────── セグメントコントロール風タブバー ────────
        float tabW = 260f, tabH = 34f;
        Rect tabTrack = new Rect(r.x + (r.width - tabW * 2) * 0.5f, r.y + 54, tabW * 2, tabH);
        GUI.color = new Color(0.12f, 0.16f, 0.28f, 0.9f);
        GUI.DrawTexture(tabTrack, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(tabTrack, 1.5f, new Color(0.3f, 0.4f, 0.6f, 0.7f));

        // GENERAL タブ
        Rect tabGen = new Rect(tabTrack.x + 3, tabTrack.y + 3, tabW - 6, tabH - 6);
        bool genActive = settingsTab == 0;
        if (genActive)
        {
            GUI.color = new Color(0.1f, 0.75f, 0.95f, 0.95f);
            GUI.DrawTexture(tabGen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(tabGen, 1.5f, Color.white);
        }
        if (GUI.Button(tabGen, "⚙  GENERAL", St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, false, genActive ? Color.white : new Color(0.6f, 0.7f, 0.85f))))
            settingsTab = 0;

        // KEY CONFIG タブ
        Rect tabKey = new Rect(tabTrack.x + tabW + 3, tabTrack.y + 3, tabW - 6, tabH - 6);
        bool keyActive = settingsTab == 1;
        if (keyActive)
        {
            GUI.color = new Color(1f, 0.65f, 0.15f, 0.95f);
            GUI.DrawTexture(tabKey, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(tabKey, 1.5f, Color.white);
        }
        if (GUI.Button(tabKey, "⌨  KEY CONFIG", St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, false, keyActive ? Color.white : new Color(0.6f, 0.7f, 0.85f))))
            settingsTab = 1;

        // キー入力待ちの検知
        if (!string.IsNullOrEmpty(waitingKeyAction))
        {
            var e = Event.current;
            if (e != null && e.isKey && e.keyCode != KeyCode.None)
            {
                if (e.keyCode != KeyCode.Escape)
                {
                    switch (waitingKeyAction)
                    {
                        case "accel": InputSettings.KeyAccel = e.keyCode; break;
                        case "brake": InputSettings.KeyBrake = e.keyCode; break;
                        case "left": InputSettings.KeySteerLeft = e.keyCode; break;
                        case "right": InputSettings.KeySteerRight = e.keyCode; break;
                        case "drift": InputSettings.KeyDrift = e.keyCode; break;
                        case "item": InputSettings.KeyItem = e.keyCode; break;
                        case "rear": InputSettings.KeyRearView = e.keyCode; break;
                    }
                    InputSettings.Save();
                }
                waitingKeyAction = null;
                e.Use();
            }
        }

        if (settingsTab == 0)
        {
            float y = r.y + 102;
            float lw = 150, sw = 220;

            // BGM 音量
            GUI.Label(new Rect(r.x + 30, y, lw, 30), "BGM VOLUME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newBgm = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 8, sw, 20), RaceAudio.MasterBgmVolume, 0f, 1f);
            if (Mathf.Abs(newBgm - RaceAudio.MasterBgmVolume) > 0.01f) Audio.SetMasterBgm(newBgm);
            // 数値チップ
            Rect bgmChip = new Rect(r.x + 30 + lw + sw + 16, y + 4, 60, 22);
            DrawPopPill(bgmChip, Mathf.RoundToInt(RaceAudio.MasterBgmVolume * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 40;
            // SFX 音量
            GUI.Label(new Rect(r.x + 30, y, lw, 30), "SFX VOLUME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newSfx = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 8, sw, 20), RaceAudio.MasterSfxVolume, 0f, 1f);
            if (Mathf.Abs(newSfx - RaceAudio.MasterSfxVolume) > 0.01f) Audio.SetMasterSfx(newSfx);
            Rect sfxChip = new Rect(r.x + 30 + lw + sw + 16, y + 4, 60, 22);
            DrawPopPill(sfxChip, Mathf.RoundToInt(RaceAudio.MasterSfxVolume * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 40;
            // スティックデッドゾーン
            GUI.Label(new Rect(r.x + 30, y, lw, 30), "STICK DEADZONE", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newDz = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 8, sw, 20), InputSettings.StickDeadzone, 0.05f, 0.35f);
            if (Mathf.Abs(newDz - InputSettings.StickDeadzone) > 0.01f) { InputSettings.StickDeadzone = newDz; InputSettings.Save(); }
            Rect dzChip = new Rect(r.x + 30 + lw + sw + 16, y + 4, 60, 22);
            DrawPopPill(dzChip, Mathf.RoundToInt(InputSettings.StickDeadzone * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 40;
            // 画面モード（トグルボタン）
            GUI.Label(new Rect(r.x + 30, y, lw, 30), "DISPLAY", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            string dispText = Screen.fullScreen ? "⛶  FULLSCREEN  [CLICK TO TOGGLE]" : "⮂  WINDOWED  [CLICK TO TOGGLE]";
            Color dispColor = Screen.fullScreen ? new Color(0.1f, 0.75f, 0.95f) : new Color(0.45f, 0.55f, 0.7f);
            if (DrawModernButton(new Rect(r.x + 30 + lw, y, 290, 32), dispText, "", dispColor))
            {
                Screen.fullScreen = !Screen.fullScreen;
                PlayerPrefs.SetInt("tc_fullscreen", Screen.fullScreen ? 1 : 0);
            }

            y += 42;
            // プレイヤーネーム
            GUI.Label(new Rect(r.x + 30, y, lw, 30), "PLAYER NAME", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            string newName = GUI.TextField(new Rect(r.x + 30 + lw, y, 200, 30), playerName, 12, sField);
            SetPlayerName(newName);

            y += 42;
            // ガイドボックス
            var cBox = new Rect(r.x + 30, y, r.width - 60, 108);
            GUI.color = new Color(0.12f, 0.16f, 0.28f, 0.08f);
            GUI.DrawTexture(cBox, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(cBox, 1, new Color(0.7f, 0.75f, 0.85f));
            var cStyle = St(sSmall, 12, TextAnchor.UpperLeft, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f));
            string cHelp = "QUICK CONTROLS OVERVIEW:\n" +
                           "• Gas/Brake: Custom Keys or Pad Triggers (RT/LT) / A / B\n" +
                           "• Drift: Space / Shift / Pad Bumpers (RB/LB)  |  Item: E / Ctrl / Pad X/Y\n" +
                           "• Rear View: C / Pad Right Stick  |  Time Attack Toggle: [M] on Title\n" +
                           "• Switch to [KEY CONFIG] tab above to rebind your keyboard keys!";
            GUI.Label(new Rect(cBox.x + 10, cBox.y + 6, cBox.width - 20, cBox.height - 12), cHelp, cStyle);
        }
        else
        {
            // タブ 1: KEY CONFIG
            float ky = r.y + 98;
            var keyEntries = new[]
            {
                ("ACCELERATE", "accel", InputSettings.KeyAccel),
                ("BRAKE / REVERSE", "brake", InputSettings.KeyBrake),
                ("STEER LEFT", "left", InputSettings.KeySteerLeft),
                ("STEER RIGHT", "right", InputSettings.KeySteerRight),
                ("DRIFT / HOP", "drift", InputSettings.KeyDrift),
                ("USE ITEM", "item", InputSettings.KeyItem),
                ("REAR VIEW", "rear", InputSettings.KeyRearView),
            };

            for (int i = 0; i < keyEntries.Length; i++)
            {
                var (label, id, curKey) = keyEntries[i];
                GUI.Label(new Rect(r.x + 40, ky, 220, 28), label, St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
                bool waitingThis = waitingKeyAction == id;
                string btnText = waitingThis ? "< PRESS KEY >" : $"[ {curKey} ]";
                Color btnCol = waitingThis ? new Color(1f, 0.85f, 0.2f) : new Color(0.18f, 0.3f, 0.5f);
                if (DrawModernButton(new Rect(r.x + 280, ky, 210, 28), btnText, "", btnCol, waitingThis))
                {
                    waitingKeyAction = waitingThis ? null : id;
                }
                ky += 34;
            }

            var kBox = new Rect(r.x + 40, ky + 8, r.width - 80, 44);
            GUI.color = new Color(0.12f, 0.16f, 0.28f, 0.08f);
            GUI.DrawTexture(kBox, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(kBox, 1, new Color(0.7f, 0.75f, 0.85f));
            string kHelp = "Click any button above then press any key to rebind.\nPress ESC to cancel rebind. Settings save automatically.";
            GUI.Label(new Rect(kBox.x + 10, kBox.y + 4, kBox.width - 20, kBox.height - 8), kHelp, St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f)));

            // デフォルトに戻すボタン（中央配置）
            if (DrawModernButton(new Rect(r.x + (r.width - 240) * 0.5f, ky + 60, 240, 32), "↺  RESET TO DEFAULTS", "", new Color(1f, 0.45f, 0.35f)))
            {
                InputSettings.ResetDefaults();
                waitingKeyAction = null;
            }
        }

        // 閉じるボタン（モダンピルボタン）
        if (DrawModernButton(new Rect(r.x + (r.width - 200) * 0.5f, r.yMax - 48, 200, 38), "✓  CLOSE", "[ESC]", new Color(0.2f, 0.85f, 0.45f)))
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
            float bSize = 130f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 46, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        var courseTitleStyle = St(sSmall, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.12f, 0.18f, 0.35f));
        GUI.Label(new Rect(leftX + 8, cardY + 182, cardW - 16, 26), $"< {curDef.Name.ToUpper()} >", courseTitleStyle);

        string diffStr = SelectedCourse == 0 ? "★☆☆  NOVICE" : SelectedCourse == 1 ? "★★☆  ADVANCED" : SelectedCourse == 2 ? "★★★  EXPERT" : "★★☆  URBAN";
        Color diffBg = SelectedCourse == 0 ? new Color(0.2f, 0.78f, 0.42f) : SelectedCourse == 1 ? new Color(1f, 0.65f, 0.15f) : SelectedCourse == 2 ? new Color(1f, 0.28f, 0.38f) : new Color(0.62f, 0.3f, 0.95f);
        DrawPopPill(new Rect(leftX + (cardW - 140) * 0.5f, cardY + 212, 140, 22), diffStr, diffBg);

        var descStyle = St(sSmall, 13, TextAnchor.UpperCenter, FontStyle.Normal, true, new Color(0.26f, 0.30f, 0.42f));
        GUI.Label(new Rect(leftX + 18, cardY + 238, cardW - 36, 52), curDef.Description, descStyle);

        float bTime = GetBestTime(SelectedCourse);
        float bLap = GetBestLap(SelectedCourse);
        string recStr = bTime > 0 ? $"RECORD: {FormatTime(bTime)}  (LAP {FormatTime(bLap)})" : "NO RECORD YET";
        Color recCol = bTime > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.45f, 0.5f, 0.65f);
        DrawPopPill(new Rect(leftX + (cardW - 270) * 0.5f, cardY + 296, 270, 22), recStr, recCol);

        string modeLabel = IsTimeAttack ? "⏱  TIME ATTACK" : $"🏆  {totalLaps} LAPS GP";
        Color modeCol = IsTimeAttack ? new Color(1f, 0.45f, 0.15f) : new Color(0.12f, 0.58f, 0.95f);
        if (DrawModernButton(new Rect(leftX + (cardW - 260) * 0.5f, cardY + 326, 260, 30), modeLabel, "[M]", modeCol, true))
        {
            IsTimeAttack = !IsTimeAttack;
            if (Audio != null) Audio.Select();
        }


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

        // プレイ人数の切り替え（モダンボタン）
        var modeBg = TwoPlayer ? new Color(0.95f, 0.3f, 0.5f) : new Color(0.18f, 0.52f, 0.88f);
        if (Online)
        {
            string codeStr = string.IsNullOrEmpty(Net.JoinCode) ? "" : $"  |  CODE: {Net.JoinCode}";
            string statusStr = Net.IsHost
                ? $"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  YOU = HOST [ENTER TO RACE]"
                : $"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  HOST DECIDES START";
            DrawPopPill(new Rect((w - 560) * 0.5f, 554f, 560, 32), statusStr, new Color(0.18f, 0.7f, 0.4f));
        }
        else
        {
            string pCountLabel = TwoPlayer ? "👥  2 PLAYERS (SPLIT SCREEN)" : "👤  1 PLAYER SOLO";
            if (DrawModernButton(new Rect((w - 320) * 0.5f, 554f, 320, 32), pCountLabel, "[TAB / Y]", modeBg, TwoPlayer))
            {
                SetTwoPlayer(!TwoPlayer);
                if (Audio != null) Audio.Select();
            }
        }


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

    void DrawResults(float w, float h)
    {
        // レース背景の半透明オーバーレイ
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var panel = new Rect(w / 2 - 320, 65, 640, 545);
        DrawPopCard(panel);

        // ヘッダータイトル
        string head = "🏁  RACE RESULTS  🏁";
        if (TwoPlayer)
        {
            head = Player.Place < Player2.Place ? "🏆  PLAYER 1 WINS!  🏆" : "🏆  PLAYER 2 WINS!  🏆";
        }
        else if (Online)
        {
            head = Player.Place == 1 ? "🏆  VICTORY! YOU WIN!  🏆" : $"{Ordinal(Player.Place)} PLACE";
        }
        else if (Player.Place == 1)
        {
            head = "🏆  1ST PLACE - VICTORY!  🏆";
        }

        DrawPopRibbon(new Rect(panel.x + 20, panel.y + 12, panel.width - 40, 38), ribbonTrackTex, head);

        float curY = panel.y + 54f;

        if (newRecordTime || newRecordLap)
        {
            string recBanner = newRecordTime ? "★ NEW COURSE RECORD! ★" : "★ NEW FASTEST LAP RECORD! ★";
            DrawPopPill(new Rect(panel.x + (panel.width - 280) * 0.5f, curY, 280, 22), recBanner, new Color(1f, 0.72f, 0.1f));
            curY += 28f;
        }

        // ──────── 表ヘッダー行 (POS / DRIVER / TIME / DIFF) ────────
        float hx = panel.x + 24f, hw = panel.width - 48f;
        var headerBg = new Rect(hx, curY, hw, 26f);
        GUI.color = new Color(0.88f, 0.91f, 0.96f, 1f);
        GUI.DrawTexture(headerBg, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(headerBg, 1, new Color(0.72f, 0.78f, 0.88f));

        var headStyle = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        var headRight = St(sSmall, 12, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        GUI.Label(new Rect(hx + 12, curY, 65, 26), "POS", headStyle);
        GUI.Label(new Rect(hx + 86, curY, 220, 26), "DRIVER / KART", headStyle);
        GUI.Label(new Rect(hx + hw - 180, curY, 90, 26), "TIME", headRight);
        GUI.Label(new Rect(hx + hw - 85, curY, 75, 26), "DIFF", headRight);

        // ──────── 各行の描画 ────────
        var order = placeOrder;
        float rowStartY = curY + 28f;
        float rowH = 36f;
        float firstTime = order.Count > 0 && order[0].Finished ? order[0].FinishTime : 0f;

        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            var row = new Rect(hx, rowStartY + i * (rowH + 2f), hw, rowH);
            bool isMe = k.IsPlayer || (Online && playerKarts.ContainsKey(Karts.IndexOf(k)));

            // 行背景（プレイヤー行は鮮やかなシアンブルー、他行は爽やかなゼブラストライプ）
            if (isMe)
            {
                GUI.color = new Color(0.85f, 0.94f, 1f, 0.95f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                DrawFrame(row, 2f, new Color(0.15f, 0.65f, 0.95f));
            }
            else
            {
                GUI.color = i % 2 == 0 ? new Color(0.96f, 0.97f, 0.99f, 0.9f) : new Color(0.91f, 0.93f, 0.97f, 0.6f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                DrawFrame(row, 1f, new Color(0.82f, 0.86f, 0.92f, 0.8f));
            }
            GUI.color = Color.white;

            // 順位バッジ・メダル色
            Color posColor = i == 0 ? new Color(0.85f, 0.55f, 0.05f) : i == 1 ? new Color(0.35f, 0.50f, 0.75f) : i == 2 ? new Color(0.80f, 0.45f, 0.15f) : new Color(0.40f, 0.45f, 0.60f);
            string posIcon = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : "  ";
            string posText = $"{posIcon} {Ordinal(k.Place)}";
            GUI.Label(new Rect(row.x + 8, row.y, 75, rowH), posText, St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, posColor));

            // カートカラーピル
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(row.x + 88, row.y + 9, 18, 18), Texture2D.whiteTexture);
            DrawFrame(new Rect(row.x + 88, row.y + 9, 18, 18), 1, new Color(0.1f, 0.15f, 0.25f, 0.6f));
            GUI.color = Color.white;

            // ドライバー名
            string dName = isMe ? $"★ {k.Name}" : k.Name;
            Color nameColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + 114, row.y, 210, rowH), dName, St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, nameColor));

            // タイム
            string timeStr = k.Finished ? FormatTime(k.FinishTime) : "--:--.--";
            Color timeColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + hw - 180, row.y, 90, rowH), timeStr, St(sMono, 16, TextAnchor.MiddleRight, FontStyle.Bold, false, timeColor));

            // タイム差 (DIFF)
            string diffStr = i == 0 ? "WINNER" : (k.Finished && firstTime > 0 ? $"+{k.FinishTime - firstTime:0.00}" : "-");
            Color diffColor = i == 0 ? new Color(0.85f, 0.55f, 0.05f) : new Color(0.25f, 0.55f, 0.85f);
            GUI.Label(new Rect(row.x + hw - 85, row.y, 75, rowH), diffStr, St(sMono, 14, TextAnchor.MiddleRight, FontStyle.Bold, false, diffColor));
        }

        // ──────── 最下部のアクションボタン (RACE AGAIN / TITLE) ────────
        float btnY = panel.yMax - 54f;
        float halfW = (hw - 16f) * 0.5f;

        if (DrawModernButton(new Rect(hx, btnY, halfW, 44), "🔄  RACE AGAIN", "[ENTER / A]", new Color(1f, 0.62f, 0.1f), true))
        {
            ResetRace();
            state = State.Racing;
        }

        if (DrawModernButton(new Rect(hx + halfW + 16f, btnY, halfW, 44), "🏠  BACK TO TITLE", "[ESC / B]", new Color(0.18f, 0.62f, 0.95f)))
        {
            ReturnToTitle();
        }
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
