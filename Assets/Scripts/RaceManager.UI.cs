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
        bool overlayModalOpen = updateModalOpen || showSettings || paused || showQuitConfirm;

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

    void DrawSettings(float w, float h)
    {
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var r = new Rect((w - 620) * 0.5f, (h - 540) * 0.5f, 620, 540);
        DrawPopCard(r);
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 34), ribbonDriverTex, Loc.T("★ SETTINGS & CONTROLS ★", "★ ゲーム設定・操作設定 ★"));

        // ──────── セグメントコントロール風タブバー（3タブ + LB/RB対応） ────────
        float tabTrackW = r.width - 60f;
        float tabW = tabTrackW / 3f, tabH = 34f;
        Rect tabTrack = new Rect(r.x + 30f, r.y + 52, tabTrackW, tabH);
        int trW = Mathf.RoundToInt(tabTrackW), trH = Mathf.RoundToInt(tabH);
        GUI.DrawTexture(tabTrack, TextureGen.RoundedPanel(trW, trH, 10f, new Color(0.12f, 0.16f, 0.28f, 0.9f), new Color(0.3f, 0.4f, 0.6f, 0.7f), 1.5f));

        int tgw = Mathf.RoundToInt(tabW - 6), tgh = Mathf.RoundToInt(tabH - 6);

        // GENERAL タブ (0)
        Rect tabGen = new Rect(tabTrack.x + 3, tabTrack.y + 3, tabW - 6, tabH - 6);
        bool genActive = settingsTab == 0;
        if (genActive)
        {
            GUI.DrawTexture(tabGen, TextureGen.RoundedPanel(tgw, tgh, 8f, new Color(0.1f, 0.75f, 0.95f, 0.95f), Color.white, 1.5f));
        }
        if (GUI.Button(tabGen, Loc.T("◄ [LB]   ★ GENERAL", "◄ [LB]   ★ 基本設定"), St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Bold, false, genActive ? Color.white : new Color(0.6f, 0.7f, 0.85f))))
        {
            settingsTab = 0;
            settingsFocus = 0;
            waitingKeyAction = null;
            waitingPadAction = null;
            GUIUtility.keyboardControl = 0;
        }

        // KEYS & PAD CONFIG タブ (1)
        Rect tabKey = new Rect(tabTrack.x + tabW + 3, tabTrack.y + 3, tabW - 6, tabH - 6);
        bool keyActive = settingsTab == 1;
        if (keyActive)
        {
            GUI.DrawTexture(tabKey, TextureGen.RoundedPanel(tgw, tgh, 8f, new Color(1f, 0.65f, 0.15f, 0.95f), Color.white, 1.5f));
        }
        if (GUI.Button(tabKey, Loc.T("► KEYS & PAD", "► キー・パッド設定"), St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Bold, false, keyActive ? Color.white : new Color(0.6f, 0.7f, 0.85f))))
        {
            settingsTab = 1;
            settingsFocus = 0;
            waitingKeyAction = null;
            waitingPadAction = null;
            GUIUtility.keyboardControl = 0;
        }

        // UPDATES タブ (2)
        Rect tabUpd = new Rect(tabTrack.x + tabW * 2 + 3, tabTrack.y + 3, tabW - 6, tabH - 6);
        bool updActive = settingsTab == 2;
        if (updActive)
        {
            GUI.DrawTexture(tabUpd, TextureGen.RoundedPanel(tgw, tgh, 8f, new Color(0.18f, 0.82f, 0.45f, 0.95f), Color.white, 1.5f));
        }
        string updTabLabel = (updater != null && updater.State == AppUpdater.UpdateState.UpdateAvailable)
            ? Loc.T("★ UPDATES (!)   [RB] ►", "★ 更新あり (!)   [RB] ►")
            : Loc.T("▲ UPDATES   [RB] ►", "▲ アップデート   [RB] ►");
        Color updLabelCol = updActive ? Color.white : (updater != null && updater.State == AppUpdater.UpdateState.UpdateAvailable) ? new Color(1f, 0.75f, 0.2f) : new Color(0.6f, 0.7f, 0.85f);
        if (GUI.Button(tabUpd, updTabLabel, St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Bold, false, updLabelCol)))
        {
            settingsTab = 2;
            settingsFocus = 0;
            waitingKeyAction = null;
            waitingPadAction = null;
            GUIUtility.keyboardControl = 0;
        }

        // キーボード入力待ちの検知
        if (!string.IsNullOrEmpty(waitingKeyAction))
        {
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None && Time.frameCount > waitingKeySkipFrame)
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

        int closeFocusIdx = settingsTab == 0 ? 6 : settingsTab == 1 ? 8 : 3;

        if (settingsTab == 0)
        {
            float y = r.y + 92;
            float lw = 155, sw = 220;

            // BGM 音量 (Focus 0)
            if (settingsFocus == 0) DrawFocusHighlight(new Rect(r.x + 24, y - 1, r.width - 48, 28));
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("BGM VOLUME", "BGM 音量"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newBgm = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 7, sw, 20), RaceAudio.MasterBgmVolume, 0f, 1f);
            if (Mathf.Abs(newBgm - RaceAudio.MasterBgmVolume) > 0.01f) Audio.SetMasterBgm(newBgm);
            Rect bgmChip = new Rect(r.x + 30 + lw + sw + 16, y + 3, 60, 22);
            DrawPopPill(bgmChip, Mathf.RoundToInt(RaceAudio.MasterBgmVolume * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 34;
            // SFX 音量 (Focus 1)
            if (settingsFocus == 1) DrawFocusHighlight(new Rect(r.x + 24, y - 1, r.width - 48, 28));
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("SFX VOLUME", "効果音 (SE) 音量"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newSfx = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 7, sw, 20), RaceAudio.MasterSfxVolume, 0f, 1f);
            if (Mathf.Abs(newSfx - RaceAudio.MasterSfxVolume) > 0.01f) Audio.SetMasterSfx(newSfx);
            Rect sfxChip = new Rect(r.x + 30 + lw + sw + 16, y + 3, 60, 22);
            DrawPopPill(sfxChip, Mathf.RoundToInt(RaceAudio.MasterSfxVolume * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 34;
            // スティックデッドゾーン (Focus 2)
            if (settingsFocus == 2) DrawFocusHighlight(new Rect(r.x + 24, y - 1, r.width - 48, 28));
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("STICK DEADZONE", "スティック遊び幅"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            float newDz = GUI.HorizontalSlider(new Rect(r.x + 30 + lw, y + 7, sw, 20), InputSettings.StickDeadzone, 0.05f, 0.35f);
            if (Mathf.Abs(newDz - InputSettings.StickDeadzone) > 0.01f) { InputSettings.StickDeadzone = newDz; InputSettings.Save(); }
            Rect dzChip = new Rect(r.x + 30 + lw + sw + 16, y + 3, 60, 22);
            DrawPopPill(dzChip, Mathf.RoundToInt(InputSettings.StickDeadzone * 100f) + "%", new Color(0.12f, 0.25f, 0.5f));

            y += 33;
            // 画面モード（トグルボタン） (Focus 3)
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("DISPLAY", "画面モード"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            string dispText = Screen.fullScreen
                ? Loc.T("★  FULLSCREEN  [TOGGLE]", "★  フルスクリーン  [切替]")
                : Loc.T("►  WINDOWED  [TOGGLE]", "►  ウィンドウ表示  [切替]");
            Color dispColor = Screen.fullScreen ? new Color(0.1f, 0.75f, 0.95f) : new Color(0.45f, 0.55f, 0.7f);
            if (DrawModernButton(new Rect(r.x + 30 + lw, y, 290, 27), dispText, "", dispColor, false, settingsFocus == 3))
            {
                Screen.fullScreen = !Screen.fullScreen;
                PlayerPrefs.SetInt("tc_fullscreen", Screen.fullScreen ? 1 : 0);
            }

            y += 32;
            // 解像度セレクター (Focus 4)
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("RESOLUTION", "画面解像度"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            string curResStr = $"{Screen.width} × {Screen.height}";
            if (DrawModernButton(new Rect(r.x + 30 + lw, y, 290, 27), $"◄  {curResStr}  ►", "[A / ◄►]", new Color(0.18f, 0.55f, 0.92f), false, settingsFocus == 4))
            {
                CycleResolution(1);
            }

            y += 32;
            // 表示言語セレクター (Focus 5)
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("LANGUAGE", "表示言語"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            string langText = Loc.IsJa ? "◄  日本語 (JAPANESE)  ►" : "◄  ENGLISH (英語)  ►";
            if (DrawModernButton(new Rect(r.x + 30 + lw, y, 290, 27), langText, "[L]", new Color(0.16f, 0.68f, 0.52f), false, settingsFocus == 5))
            {
                ToggleLanguage();
            }

            y += 33;
            // プレイヤーネーム
            GUI.Label(new Rect(r.x + 30, y, lw, 28), Loc.T("PLAYER NAME", "プレイヤー名"), St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            var nameFieldRect = new Rect(r.x + 30 + lw, y, 200, 27);
            if (Event.current != null && Event.current.type == EventType.MouseDown && !nameFieldRect.Contains(Event.current.mousePosition))
                GUIUtility.keyboardControl = 0;
            string newName = GUI.TextField(nameFieldRect, playerName, 12, sField);
            SetPlayerName(newName);

            y += 33;
            // ガイドボックス
            var cBox = new Rect(r.x + 30, y, r.width - 60, 82);
            int cbw = Mathf.RoundToInt(cBox.width), cbh = Mathf.RoundToInt(cBox.height);
            GUI.DrawTexture(cBox, TextureGen.RoundedPanel(cbw, cbh, 8f, new Color(0.12f, 0.16f, 0.28f, 0.08f), new Color(0.7f, 0.75f, 0.85f), 1.2f));
            var cStyle = St(sSmall, 11, TextAnchor.UpperLeft, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f));
            string cHelp = Loc.T(
                "GAMEPAD & KEYBOARD NAVIGATION:\n" +
                "• Menu Navigation: D-Pad / Left Stick  |  Confirm: [A / ENTER]  |  Back/Close: [B / ESC]  |  Switch Tab: [LB / RB]\n" +
                "• Switch to [KEYS & PAD] tab to customize both Keyboard keys and Controller buttons/triggers!",
                "ゲームパッド＆キーボード操作ガイド:\n" +
                "• メニュー移動: 十字キー / 左スティック  |  決定: [A / ENTER]  |  戻る: [B / ESC]  |  タブ切替: [LB / RB]\n" +
                "• [キー・パッド設定] タブでキーボードのキー配置やコントローラーのボタン/トリガーを自由に変更できます！");
            GUI.Label(new Rect(cBox.x + 10, cBox.y + 6, cBox.width - 20, cBox.height - 12), cHelp, cStyle);
        }
        else if (settingsTab == 1)
        {
            // タブ 1: KEYS & PAD CONFIG（キーボード＆コントローラー2列コンフィグ）
            float hdrY = r.y + 90f;
            var hdrSt = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.32f, 0.40f, 0.56f));
            GUI.Label(new Rect(r.x + 36, hdrY, 170, 18), Loc.T("ACTION", "操作アクション"), St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.32f, 0.40f, 0.56f)));
            GUI.Label(new Rect(r.x + 210, hdrY, 180, 18), Loc.T("KEYBOARD", "キーボード"), hdrSt);
            GUI.Label(new Rect(r.x + 402, hdrY, 186, 18), Loc.T("CONTROLLER (PAD)", "コントローラー"), hdrSt);

            float ky = r.y + 110f;
            var keyEntries = new[]
            {
                (Loc.T("ACCELERATE", "アクセル（前進）"), "accel", InputSettings.KeyAccel, true),
                (Loc.T("BRAKE / REVERSE", "ブレーキ / バック"), "brake", InputSettings.KeyBrake, true),
                (Loc.T("STEER LEFT", "左ステアリング"), "left", InputSettings.KeySteerLeft, false),
                (Loc.T("STEER RIGHT", "右ステアリング"), "right", InputSettings.KeySteerRight, false),
                (Loc.T("DRIFT / HOP", "ドリフト / ジャンプ"), "drift", InputSettings.KeyDrift, true),
                (Loc.T("USE ITEM", "アイテム使用"), "item", InputSettings.KeyItem, true),
                (Loc.T("REAR VIEW", "後方確認ミラー"), "rear", InputSettings.KeyRearView, true),
            };

            for (int i = 0; i < keyEntries.Length; i++)
            {
                var (label, id, curKey, hasPadRebind) = keyEntries[i];
                GUI.Label(new Rect(r.x + 36, ky, 170, 27), label, St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));

                // 列 0: キーボード割り当てボタン
                bool waitingKeyThis = waitingKeyAction == id;
                string keyBtnText = waitingKeyThis ? Loc.T("< PRESS KEY >", "< キーを入力 >") : $"[ {curKey} ]";
                Color keyBtnCol = waitingKeyThis ? new Color(1f, 0.80f, 0.15f) : new Color(0.18f, 0.34f, 0.58f);
                bool keyFocused = settingsFocus == i && (settingsCol == 0 || !hasPadRebind);
                if (DrawModernButton(new Rect(r.x + 210, ky, 180, 27), keyBtnText, "", keyBtnCol, waitingKeyThis, keyFocused))
                {
                    settingsFocus = i;
                    settingsCol = 0;
                    waitingPadAction = null;
                    waitingKeyAction = waitingKeyThis ? null : id;
                    waitingKeySkipFrame = Time.frameCount;
                }

                // 列 1: コントローラー割り当てボタン（ステアリングは L-STICK / D-PAD 固定ピル表示）
                var padRect = new Rect(r.x + 402, ky, 186, 27);
                if (hasPadRebind)
                {
                    bool waitingPadThis = waitingPadAction == id;
                    string padBtnText = waitingPadThis ? Loc.T("< PRESS PAD >", "< ボタンを入力 >") : InputSettings.FormatPadAction(id);
                    Color padBtnCol = waitingPadThis ? new Color(1f, 0.72f, 0.12f) : new Color(0.14f, 0.52f, 0.78f);
                    bool padFocused = settingsFocus == i && settingsCol == 1;
                    if (DrawModernButton(padRect, padBtnText, "", padBtnCol, waitingPadThis, padFocused))
                    {
                        settingsFocus = i;
                        settingsCol = 1;
                        waitingKeyAction = null;
                        waitingPadAction = waitingPadThis ? null : id;
                        waitingPadSkipFrame = Time.frameCount;
                    }
                }
                else
                {
                    DrawPopPill(new Rect(padRect.x + 8, padRect.y + 2, padRect.width - 16, 23), Loc.T("L-STICK / D-PAD", "左スティック / 十字キー"), new Color(0.42f, 0.50f, 0.65f));
                }

                ky += 32f;
            }

            var kBox = new Rect(r.x + 36, ky + 2, r.width - 72, 42);
            int kbw = Mathf.RoundToInt(kBox.width), kbh = Mathf.RoundToInt(kBox.height);
            GUI.DrawTexture(kBox, TextureGen.RoundedPanel(kbw, kbh, 8f, new Color(0.12f, 0.16f, 0.28f, 0.08f), new Color(0.7f, 0.75f, 0.85f), 1.2f));
            string kHelp = Loc.T(
                "Select a slot with Mouse or D-Pad/Stick + [A], then press any Key or Controller Button/Trigger (LT/RT).\nPress [ESC / START] to cancel. Both Keyboard and Controller bindings save automatically.",
                "スロットを選択し、割り当てたいキーやパッドのボタン/トリガー (LT/RT) を押してください。\n[ESC / START] でキャンセル。変更した設定は自動的に保存されます。");
            GUI.Label(new Rect(kBox.x + 10, kBox.y + 2, kBox.width - 20, kBox.height - 4), kHelp, St(sSmall, 11, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f)));

            // デフォルトに戻すボタン（Focus 7）
            if (DrawModernButton(new Rect(r.x + (r.width - 250) * 0.5f, ky + 48, 250, 30), Loc.T("◄  RESET TO DEFAULTS", "◄  初期設定に戻す"), "", new Color(1f, 0.45f, 0.35f), false, settingsFocus == 7))
            {
                InputSettings.ResetDefaults();
                waitingKeyAction = null;
                waitingPadAction = null;
                if (Audio != null) Audio.Select();
            }
        }
        else if (settingsTab == 2)
        {
            // タブ 2: UPDATES
            float uy = r.y + 100;
            float uw = r.width - 80f;
            float ux = r.x + 40f;

            // 情報ボックス
            var uBox = new Rect(ux, uy, uw, 160);
            int ubw = Mathf.RoundToInt(uBox.width), ubh = Mathf.RoundToInt(uBox.height);
            GUI.DrawTexture(uBox, TextureGen.RoundedPanel(ubw, ubh, 8f, new Color(0.12f, 0.16f, 0.28f, 0.08f), new Color(0.7f, 0.75f, 0.85f), 1.2f));

            float iy = uy + 14f;
            GUI.Label(new Rect(ux + 20, iy, 160, 24), Loc.T("CURRENT VERSION:", "現在のバージョン:"), St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            DrawPopPill(new Rect(ux + 190, iy, 130, 24), AppUpdater.CurrentVersion, new Color(0.18f, 0.55f, 0.95f));

            iy += 34f;
            string latVerStr = (updater != null && !string.IsNullOrEmpty(updater.LatestVersion)) ? updater.LatestVersion : Loc.T("Unknown", "未確認");
            Color latVerCol = (updater != null && updater.State == AppUpdater.UpdateState.UpdateAvailable) ? new Color(1f, 0.62f, 0.12f) : new Color(0.2f, 0.75f, 0.4f);
            GUI.Label(new Rect(ux + 20, iy, 160, 24), Loc.T("LATEST RELEASE:", "最新リリース:"), St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            DrawPopPill(new Rect(ux + 190, iy, 130, 24), latVerStr, latVerCol);

            iy += 34f;
            string statusStr = (updater == null || updater.State == AppUpdater.UpdateState.Idle)
                ? Loc.T("Ready to check for updates.", "アップデート確認が可能です。")
                : updater.StatusMessage;
            GUI.Label(new Rect(ux + 20, iy, 160, 24), Loc.T("UPDATE STATUS:", "ステータス:"), St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.2f, 0.25f, 0.4f)));
            GUI.Label(new Rect(ux + 190, iy, uw - 210, 24), statusStr, St(sSmall, 13, TextAnchor.MiddleLeft, FontStyle.Normal, false, new Color(0.25f, 0.3f, 0.45f)));

            iy += 30f;
            string checkTimeStr = (updater != null && updater.HasCheckedOnce) ? updater.LastCheckedTime.ToString("yyyy-MM-dd HH:mm:ss") : Loc.T("Not checked yet", "未チェック");
            GUI.Label(new Rect(ux + 20, iy, 160, 24), Loc.T("LAST CHECKED:", "最終確認日時:"), St(sSmall, 13, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.4f, 0.45f, 0.6f)));
            GUI.Label(new Rect(ux + 190, iy, uw - 210, 24), checkTimeStr, St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Normal, false, new Color(0.45f, 0.5f, 0.65f)));

            // ボタン一覧
            float by = uy + 175f;
            float bw = 260f, bh = 36f, gap = 12f;

            bool isChecking = updater != null && (updater.State == AppUpdater.UpdateState.Checking || updater.State == AppUpdater.UpdateState.Downloading);
            string checkBtnLabel = isChecking
                ? Loc.T("►  CHECKING GITHUB...", "►  GitHubを確認中...")
                : Loc.T("►  CHECK FOR UPDATES", "►  アップデートを確認");
            if (DrawModernButton(new Rect(ux + (uw - bw) * 0.5f, by, bw, bh), checkBtnLabel, "[C]", new Color(0.18f, 0.65f, 0.95f), true, settingsFocus == 0) && !isChecking)
            {
                if (updater != null) updater.CheckForUpdates(openModalIfNew: true);
            }
            by += bh + gap;

            if (updater != null && (updater.State == AppUpdater.UpdateState.UpdateAvailable || updater.State == AppUpdater.UpdateState.Downloaded))
            {
                string actionLabel = updater.State == AppUpdater.UpdateState.Downloaded
                    ? Loc.T("►  RESTART & INSTALL NOW", "►  再起動して更新を適用")
                    : Loc.T("►  OPEN UPDATE WIDGET", "►  更新画面を開く");
                if (DrawModernButton(new Rect(ux + (uw - bw) * 0.5f, by, bw, bh), actionLabel, "[U]", new Color(0.18f, 0.85f, 0.45f), true, settingsFocus == 1))
                {
                    updater.ShowModal = true;
                    updateModalFocus = 0;
                }
                by += bh + gap;
            }
            else
            {
                if (DrawModernButton(new Rect(ux + (uw - bw) * 0.5f, by, bw, bh), Loc.T("►  VIEW ON GITHUB", "►  GitHub リリースを開く"), "", new Color(0.45f, 0.35f, 0.85f), false, settingsFocus == 1))
                {
                    Application.OpenURL(AppUpdater.ReleasesPageUrl);
                }
                by += bh + gap;
            }

            if (DrawModernButton(new Rect(ux + (uw - 230) * 0.5f, by, 230, 28), Loc.T("★  SIMULATE UPDATE TEST", "★  更新テスト表示"), "", new Color(0.55f, 0.6f, 0.7f), false, settingsFocus == 2))
            {
                if (updater != null) updater.SimulateUpdateAvailableForTesting();
            }
        }

        // 閉じるボタン（モダンピルボタン）
        if (DrawModernButton(new Rect(r.x + (r.width - 200) * 0.5f, r.yMax - 46, 200, 36), Loc.T("►  CLOSE", "►  閉じる"), "[ESC / B]", new Color(0.2f, 0.85f, 0.45f), false, settingsFocus == closeFocusIdx))
        {
            CloseSettings();
        }
    }

    void DrawTitle(float w, float h)
    {
        float t = Time.time;

        // 1. トップ：TURBO CIRCUIT ポップロゴ ＆ バージョン ＆ 言語切替 ＆ アップデート ＆ オプションボタン
        DrawPopPill(new Rect(24, 20, 96, 28), AppUpdater.CurrentVersion, new Color(0.18f, 0.28f, 0.5f));

        if (!Net.Busy)
        {
            string langBtnLabel = Loc.IsJa ? "★ 言語: 日本語" : "★ LANG: EN";
            if (DrawModernButton(new Rect(128, 16, 178, 36), langBtnLabel, "[L]", new Color(0.16f, 0.68f, 0.52f), false, titleFocus == 11))
            {
                ToggleLanguage();
            }

            if (DrawModernButton(new Rect(w - 296, 16, 138, 36), Loc.T("★ OPTIONS", "★ 設定"), "[P]", new Color(0.18f, 0.55f, 0.92f), false, titleFocus == 9))
            {
                showSettings = true;
                settingsFocus = 0;
            }

            if (DrawModernButton(new Rect(w - 146, 16, 122, 36), Loc.T("✖ QUIT", "✖ 終了"), "[ESC]", new Color(0.92f, 0.32f, 0.32f), false, titleFocus == 12))
            {
                ShowQuitModal();
            }
        }

        if (updater != null && !Net.Busy)
        {
            if (updater.State == AppUpdater.UpdateState.UpdateAvailable || updater.State == AppUpdater.UpdateState.Downloaded)
            {
                Color updBtnCol = new Color(1f, 0.65f, 0.12f);
                string verStr = updater.LatestVersion;
                if (!string.IsNullOrEmpty(verStr) && verStr.Length > 7) verStr = verStr.Substring(0, 6) + "..";
                string btnTxt = updater.State == AppUpdater.UpdateState.Downloaded
                    ? Loc.T("★ INSTALL NOW", "★ 今すぐ更新")
                    : Loc.T($"★ UPDATE {verStr}", $"★ 更新 {verStr}");
                if (DrawModernButton(new Rect(w - 506, 16, 198, 36), btnTxt, "[U]", updBtnCol, true, titleFocus == 10))
                {
                    updater.ShowModal = true;
                    updateModalFocus = 0;
                }
            }
            else
            {
                if (DrawModernButton(new Rect(w - 468, 16, 160, 36), Loc.T("▲ UPDATES", "▲ 更新確認"), "[U]", new Color(0.28f, 0.4f, 0.6f), false, titleFocus == 10))
                {
                    updater.ShowModal = true;
                    updateModalFocus = 0;
                }
            }
        }

        if (titleLogoTex != null)
        {
            float maxLogoW = Mathf.Min(380f, w - 530f);
            float logoW = Mathf.Max(240f, maxLogoW);
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
        DrawPopRibbon(new Rect(leftX + 15, cardY + 12, cardW - 30, 32), ribbonTrackTex, Loc.T("◄ TRACK SELECT [A/D / LB/RB] ►", "◄ コース選択 [A/D / LB/RB] ►"));

        var curDef = Track.Courses[SelectedCourse];
        if (trackBadgeTex[SelectedCourse] != null)
        {
            float bSize = 130f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 46, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        if (titleFocus == 5)
            DrawFocusHighlight(new Rect(leftX + 14, cardY + 179, cardW - 28, 32), 8f);
        string cTitle = Loc.CourseName(SelectedCourse, curDef.Name);
        var courseTitleStyle = St(sMid, Loc.IsJa ? 19 : 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.12f, 0.18f, 0.35f));
        GUI.Label(new Rect(leftX + 8, cardY + 182, cardW - 16, 26), $"< {cTitle} >", courseTitleStyle);

        string diffStr = Loc.CourseDifficulty(SelectedCourse);
        Color diffBg = SelectedCourse == 0 ? new Color(0.2f, 0.78f, 0.42f) : SelectedCourse == 1 ? new Color(1f, 0.65f, 0.15f) : SelectedCourse == 2 ? new Color(1f, 0.28f, 0.38f) : SelectedCourse == 3 ? new Color(0.62f, 0.3f, 0.95f) : SelectedCourse == 4 ? new Color(0.12f, 0.68f, 0.36f) : SelectedCourse == 5 ? new Color(0.08f, 0.65f, 0.98f) : new Color(0.95f, 0.42f, 0.18f);
        DrawPopPill(new Rect(leftX + (cardW - 154) * 0.5f, cardY + 212, 154, 22), diffStr, diffBg);

        var descStyle = St(sSmall, Loc.IsJa ? 12 : 13, TextAnchor.UpperCenter, FontStyle.Normal, true, new Color(0.26f, 0.30f, 0.42f));
        GUI.Label(new Rect(leftX + 16, cardY + 238, cardW - 32, 54), Loc.CourseDesc(SelectedCourse, curDef.Description), descStyle);

        float bTime = GetBestTime(SelectedCourse);
        float bLap = GetBestLap(SelectedCourse);
        string recStr = bTime > 0
            ? Loc.T($"RECORD: {FormatTime(bTime)}  (LAP {FormatTime(bLap)})", $"記録: {FormatTime(bTime)}  (最速ラップ {FormatTime(bLap)})")
            : Loc.T("NO RECORD YET", "まだ記録がありません");
        Color recCol = bTime > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.45f, 0.5f, 0.65f);
        DrawPopPill(new Rect(leftX + (cardW - 280) * 0.5f, cardY + 296, 280, 22), recStr, recCol);

        string modeLabel = IsTimeAttack
            ? Loc.T("★  TIME ATTACK", "★  タイムアタック")
            : Loc.T($"★  {totalLaps} LAPS GP", $"★  {totalLaps}周 グランプリ");
        Color modeCol = IsTimeAttack ? new Color(1f, 0.45f, 0.15f) : new Color(0.12f, 0.58f, 0.95f);
        if (DrawModernButton(new Rect(leftX + (cardW - 260) * 0.5f, cardY + 326, 260, 30), modeLabel, "[M]", modeCol, true, titleFocus == 6))
        {
            if (TwoPlayer) SetTwoPlayer(false);
            IsTimeAttack = !IsTimeAttack;
            if (Audio != null) Audio.Select();
        }


        // 3. 右カード：ドライバー＆マシンセレクター（DRIVER & KART）
        float rightX = w - cardW - 35f;
        DrawPopCard(new Rect(rightX, cardY, cardW, cardH));
        DrawPopRibbon(new Rect(rightX + 15, cardY + 12, cardW - 30, 32), ribbonDriverTex,
            TwoPlayer ? Loc.T("★ 1P & 2P SETUP ★", "★ 1P & 2P セッティング ★") : Loc.T("★ DRIVER & KART ★", "★ ドライバー＆マシン ★"));

        if (!TwoPlayer)
        {
            int drvIdx = Mathf.Clamp(SelectedDriver, 0, KartCharacters.Length - 1);
            int krtIdx = Mathf.Clamp(SelectedKart, 0, KartMachines.Length - 1);
            var curDriver = KartCharacters[drvIdx];
            var curKart = KartMachines[krtIdx];
            GetCombinedStats(drvIdx, krtIdx, out int spd, out int acc, out int hdl, out int wgt);

            var secLabelStyle = St(sSmall, 11, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.32f, 0.38f, 0.52f));
            var idxLabelStyle = St(sMono, 11, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.38f, 0.45f, 0.60f));

            // ─── DRIVER 選択セクション ───
            GUI.Label(new Rect(rightX + 22, cardY + 46, 200, 16), Loc.T("DRIVER  [W / S]", "ドライバー  [W / S]"), secLabelStyle);
            GUI.Label(new Rect(rightX + cardW - 92, cardY + 46, 70, 16), $"{drvIdx + 1}/{KartCharacters.Length}", idxLabelStyle);

            if (titleFocus == 1)
                DrawFocusHighlight(new Rect(rightX + 16, cardY + 62, cardW - 32, 32), 8f);
            if (DrawModernButton(new Rect(rightX + 18, cardY + 64, 32, 28), "◄", "", curDriver.color))
                SwitchDriver(0, -1);
            var charTitleStyle = St(sMid, 20, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Outlined(new Rect(rightX + 52, cardY + 63, cardW - 104, 30), $"< {Loc.DriverName(drvIdx, curDriver.name)} >", charTitleStyle, curDriver.color, 2);
            if (DrawModernButton(new Rect(rightX + cardW - 50, cardY + 64, 32, 28), "►", "", curDriver.color))
                SwitchDriver(0, 1);

            DrawPopPill(new Rect(rightX + 20, cardY + 96, cardW - 40, 20), Loc.DriverSub(drvIdx, curDriver.driver, curDriver.trait), curDriver.color * 0.9f);

            // ─── KART MACHINE 選択セクション ───
            GUI.Label(new Rect(rightX + 22, cardY + 122, 200, 16), Loc.T("KART MACHINE  [Q / E]", "カートマシン  [Q / E]"), secLabelStyle);
            GUI.Label(new Rect(rightX + cardW - 92, cardY + 122, 70, 16), $"{krtIdx + 1}/{KartMachines.Length}", idxLabelStyle);

            if (titleFocus == 2)
                DrawFocusHighlight(new Rect(rightX + 16, cardY + 138, cardW - 32, 32), 8f);
            if (DrawModernButton(new Rect(rightX + 18, cardY + 140, 32, 28), "◄", "", curKart.color))
                SwitchKart(0, -1);
            var kartTitleStyle = St(sMid, Loc.IsJa ? 17 : 18, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Outlined(new Rect(rightX + 52, cardY + 139, cardW - 104, 30), $"< {Loc.KartName(krtIdx, curKart.name)} >", kartTitleStyle, curKart.color, 2);
            if (DrawModernButton(new Rect(rightX + cardW - 50, cardY + 140, 32, 28), "►", "", curKart.color))
                SwitchKart(0, 1);

            DrawPopPill(new Rect(rightX + 20, cardY + 172, cardW - 40, 20), Loc.KartSub(krtIdx, curKart.code, curKart.trait), curKart.color * 0.9f);

            // ─── 4項目 総合ステータスゲージ ───
            GUI.Label(new Rect(rightX + 22, cardY + 198, cardW - 44, 16), Loc.T("COMBINED PERFORMANCE", "総合マシン性能"), St(sSmall, 11, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.32f, 0.38f, 0.52f)));
            float statStartY = cardY + 218f;
            DrawToonStatGauge(rightX + 22, statStartY + 0,  cardW - 44, Loc.T("SPEED",  "最高速"), spd, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, statStartY + 34, cardW - 44, Loc.T("ACCEL",  "加速力"), acc, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, statStartY + 68, cardW - 44, Loc.T("STEER",  "旋回性"), hdl, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, statStartY + 102, cardW - 44, Loc.T("WEIGHT", "重さ"),   wgt, 8, new Color(1f, 0.32f, 0.38f));

            var switchGuide = St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.40f, 0.46f, 0.62f));
            GUI.Label(new Rect(rightX + 10, cardY + 365, cardW - 20, 20), Loc.T("[W][S] Driver (10)   •   [Q][E] Kart (8)", "[W][S] ドライバー (10)   •   [Q][E] マシン (8)"), switchGuide);
        }
        else
        {
            // 2P: P1 & P2 のキャラ＆カート選択と総合ステータスを上下分割表示
            int d1Idx = Mathf.Clamp(SelectedDriver, 0, KartCharacters.Length - 1);
            int k1Idx = Mathf.Clamp(SelectedKart, 0, KartMachines.Length - 1);
            var d1 = KartCharacters[d1Idx];
            var m1 = KartMachines[k1Idx];
            GetCombinedStats(d1Idx, k1Idx, out int spd1, out int acc1, out int hdl1, out int wgt1);

            if (titleFocus == 1)
                DrawFocusHighlight(new Rect(rightX + 14, cardY + 46, cardW - 28, 28), 7f);
            if (DrawModernButton(new Rect(rightX + 16, cardY + 48, 28, 24), "◄", "", d1.color)) SwitchDriver(0, -1);
            Outlined(new Rect(rightX + 46, cardY + 47, cardW - 92, 26), $"1P: {Loc.DriverName(d1Idx, d1.name)}", St(sMid, 15, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), d1.color, 1.5f);
            if (DrawModernButton(new Rect(rightX + cardW - 44, cardY + 48, 28, 24), "►", "", d1.color)) SwitchDriver(0, 1);

            if (titleFocus == 2)
                DrawFocusHighlight(new Rect(rightX + 14, cardY + 74, cardW - 28, 28), 7f);
            if (DrawModernButton(new Rect(rightX + 16, cardY + 76, 28, 24), "◄", "", m1.color)) SwitchKart(0, -1);
            Outlined(new Rect(rightX + 46, cardY + 75, cardW - 92, 26), $"1P: {Loc.KartName(k1Idx, m1.name)}", St(sMid, 15, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), m1.color, 1.5f);
            if (DrawModernButton(new Rect(rightX + cardW - 44, cardY + 76, 28, 24), "►", "", m1.color)) SwitchKart(0, 1);

            float s1Y = cardY + 106f;
            DrawToonStatGauge(rightX + 22, s1Y + 0,  cardW - 44, Loc.T("SPD", "速度"), spd1, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, s1Y + 19, cardW - 44, Loc.T("ACC", "加速"), acc1, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, s1Y + 38, cardW - 44, Loc.T("STR", "旋回"), hdl1, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, s1Y + 57, cardW - 44, Loc.T("WGT", "重さ"), wgt1, 8, new Color(1f, 0.32f, 0.38f));

            int d2Idx = Mathf.Clamp(SelectedDriver2, 0, KartCharacters.Length - 1);
            int k2Idx = Mathf.Clamp(SelectedKart2, 0, KartMachines.Length - 1);
            var d2 = KartCharacters[d2Idx];
            var m2 = KartMachines[k2Idx];
            GetCombinedStats(d2Idx, k2Idx, out int spd2, out int acc2, out int hdl2, out int wgt2);

            if (titleFocus == 3)
                DrawFocusHighlight(new Rect(rightX + 14, cardY + 194, cardW - 28, 28), 7f);
            if (DrawModernButton(new Rect(rightX + 16, cardY + 196, 28, 24), "◄", "", d2.color)) SwitchDriver(1, -1);
            Outlined(new Rect(rightX + 46, cardY + 195, cardW - 92, 26), $"2P: {Loc.DriverName(d2Idx, d2.name)}", St(sMid, 15, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), d2.color, 1.5f);
            if (DrawModernButton(new Rect(rightX + cardW - 44, cardY + 196, 28, 24), "►", "", d2.color)) SwitchDriver(1, 1);

            if (titleFocus == 4)
                DrawFocusHighlight(new Rect(rightX + 14, cardY + 222, cardW - 28, 28), 7f);
            if (DrawModernButton(new Rect(rightX + 16, cardY + 224, 28, 24), "◄", "", m2.color)) SwitchKart(1, -1);
            Outlined(new Rect(rightX + 46, cardY + 223, cardW - 92, 26), $"2P: {Loc.KartName(k2Idx, m2.name)}", St(sMid, 15, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), m2.color, 1.5f);
            if (DrawModernButton(new Rect(rightX + cardW - 44, cardY + 224, 28, 24), "►", "", m2.color)) SwitchKart(1, 1);

            float s2Y = cardY + 254f;
            DrawToonStatGauge(rightX + 22, s2Y + 0,  cardW - 44, Loc.T("SPD", "速度"), spd2, 8, new Color(0.08f, 0.72f, 0.98f));
            DrawToonStatGauge(rightX + 22, s2Y + 19, cardW - 44, Loc.T("ACC", "加速"), acc2, 8, new Color(1f, 0.72f, 0.05f));
            DrawToonStatGauge(rightX + 22, s2Y + 38, cardW - 44, Loc.T("STR", "旋回"), hdl2, 8, new Color(0.25f, 0.85f, 0.35f));
            DrawToonStatGauge(rightX + 22, s2Y + 57, cardW - 44, Loc.T("WGT", "重さ"), wgt2, 8, new Color(1f, 0.32f, 0.38f));

            var switchGuide = St(sSmall, 11, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.40f, 0.46f, 0.62f));
            GUI.Label(new Rect(rightX + 8, cardY + 365, cardW - 16, 20), Loc.T("P1: [W/S] Drv [Q/E] Kart • P2: [↑/↓] Drv [←/→] Kart", "1P: [W/S][Q/E]   •   2P: [↑/↓][←/→]"), switchGuide);
        }

        // プレイ人数 ＆ オンライン対戦の切り替え（モダンボタン）
        var modeBg = TwoPlayer ? new Color(0.95f, 0.3f, 0.5f) : new Color(0.18f, 0.52f, 0.88f);
        if (Online)
        {
            string codeStr = string.IsNullOrEmpty(Net.JoinCode) ? "" : Loc.T($"  |  CODE: {Net.JoinCode}", $"  |  コード: {Net.JoinCode}");
            string statusStr = Net.IsHost
                ? Loc.T($"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  YOU = HOST [ENTER TO RACE]", $"オンライン ({OnlinePlayerCount}/8人{codeStr})  -  ホスト [ENTERで開始]")
                : Loc.T($"ONLINE ({OnlinePlayerCount}/8 PLAYERS{codeStr})  -  HOST DECIDES START", $"オンライン ({OnlinePlayerCount}/8人{codeStr})  -  ホストの開始待ち");
            DrawPopPill(new Rect((w - 560) * 0.5f, 554f, 560, 32), statusStr, new Color(0.18f, 0.7f, 0.4f));
        }
        else
        {
            float rowW = 456f;
            float rowX = (w - rowW) * 0.5f;
            string pCountLabel = TwoPlayer ? Loc.T("★ 2P SPLIT", "★ 2P 画面分割") : Loc.T("★ 1P SOLO", "★ 1P ソロ");
            if (DrawModernButton(new Rect(rowX, 554f, 232f, 32), pCountLabel, "[TAB / Y]", modeBg, TwoPlayer, titleFocus == 7))
            {
                if (IsTimeAttack) IsTimeAttack = false;
                SetTwoPlayer(!TwoPlayer);
                if (Audio != null) Audio.Select();
            }
            if (DrawModernButton(new Rect(rowX + 242f, 554f, 214f, 32), Loc.T("★ ONLINE", "★ オンライン"), "[O / X]", new Color(0.15f, 0.72f, 0.52f), false, titleFocus == 8))
            {
                if (IsTimeAttack) IsTimeAttack = false;
                netMenu = true;
                netFocus = 0;
                if (Audio != null) Audio.Select();
            }
        }


        // 4. 画面中央下部：PRESS ENTER TO RACE（ぷっくり立体キャンディボタン）
        float pulse = (Mathf.Sin(t * 6f) + 1f) * 0.5f;
        float maxAvailableBtnW = Mathf.Max(260f, w - (cardW + 35f) * 2f - 20f);
        float baseBtnW = Mathf.Min(456f, maxAvailableBtnW);
        float baseBtnH = 50f;
        int rbw = Mathf.RoundToInt(baseBtnW);
        int rbh = Mathf.RoundToInt(baseBtnH);
        var baseBtnRect = new Rect(Mathf.Round((w - rbw) * 0.5f), 598f, rbw, rbh);
        float maxScale = maxAvailableBtnW / Mathf.Max(1f, rbw);
        float btnScale = Mathf.Min(1f + pulse * 0.04f, maxScale);

        Matrix4x4 prevMat = GUI.matrix;
        Vector3 btnPivot = new Vector3(baseBtnRect.center.x, baseBtnRect.center.y, 0f);
        GUI.matrix = prevMat
            * Matrix4x4.Translate(btnPivot)
            * Matrix4x4.Scale(new Vector3(btnScale, btnScale, 1f))
            * Matrix4x4.Translate(-btnPivot);

        var raceShTex = TextureGen.RoundedShadow(rbw, rbh, rbh * 0.5f);
        GUI.DrawTexture(new Rect(baseBtnRect.x, baseBtnRect.y + 4f, baseBtnRect.width, baseBtnRect.height), raceShTex);
        var raceBtnTex = TextureGen.RaceStartButton(rbw, rbh);
        GUI.DrawTexture(baseBtnRect, raceBtnTex);
        if (titleFocus == 0)
            DrawFocusHighlight(baseBtnRect, rbh * 0.5f);

        var startStyle = St(sMid, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
        Outlined(new Rect(baseBtnRect.x, baseBtnRect.y + 2f, baseBtnRect.width, baseBtnRect.height - 4f),
            Loc.T("►►  PRESS ENTER / [A] TO RACE!  ◄◄", "►►  レーススタート！ [ENTER / A]  ◄◄"), startStyle, Color.white, 2.5f);
        bool startClicked = !titleShotRequested && GUI.Button(baseBtnRect, GUIContent.none, GUIStyle.none);
        GUI.matrix = prevMat;
        if (startClicked)
            StartRaceFromTitle();


        // 5. 画面最下部：コントロールガイドバー
        GUI.color = new Color(0.08f, 0.12f, 0.22f, 0.94f);
        GUI.DrawTexture(new Rect(0, h - 34, w, 34), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, h - 34, w, 34), 1, new Color(0.2f, 0.3f, 0.45f, 0.6f));

        var barStyle = St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Normal);
        string guideText = TwoPlayer
            ? Loc.T("P1: [W/S] Drv [Q/E] Kart  •  P2: [↑/↓] Drv [←/→] Kart  •  [A/D / LB/RB] Track  •  [TAB/Y] 1P/2P  •  [L] Lang  •  [ESC] Quit",
                    "1P: [W/S] キャラ [Q/E] マシン  •  2P: [↑/↓] キャラ [←/→] マシン  •  [A/D / LB/RB] コース  •  [TAB/Y] 1P/2P  •  [L] 言語  •  [ESC] 終了")
            : Loc.T("[D-Pad/Stick] Select  •  [A/Enter] OK  •  [LB/RB] Track  •  [TAB/Y] 2P  •  [O/X] Online  •  [L] 日本語/EN  •  [P] Options  •  [ESC] Quit",
                    "[十字キー/スティック] 選択  •  [A/Enter] 決定  •  [LB/RB] コース  •  [TAB/Y] 2P対戦  •  [O/X] オンライン  •  [L] 言語  •  [P] 設定  •  [ESC] 終了");
        Outlined(new Rect(0, h - 32, w, 28), guideText, barStyle, new Color(0.9f, 0.95f, 1f), 1);
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

    void DrawResults(float w, float h)
    {
        // レース背景の半透明オーバーレイ
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var panel = new Rect(w / 2 - 320, 65, 640, 545);
        DrawPopCard(panel);

        // ヘッダータイトル
        string head = Loc.T("★  RACE RESULTS  ★", "★  レース結果  ★");
        if (TwoPlayer)
        {
            head = Player.Place < Player2.Place
                ? Loc.T("★  PLAYER 1 WINS!  ★", "★  1P の勝利！  ★")
                : Loc.T("★  PLAYER 2 WINS!  ★", "★  2P の勝利！  ★");
        }
        else if (Online)
        {
            head = Player.Place == 1
                ? Loc.T("★  VICTORY! YOU WIN!  ★", "★  優勝！ 1位フィニッシュ！  ★")
                : Loc.T($"{Ordinal(Player.Place)} PLACE", $"{Player.Place}位 フィニッシュ！");
        }
        else if (Player.Place == 1)
        {
            head = Loc.T("★  1ST PLACE - VICTORY!  ★", "★  優勝！ 1位フィニッシュ！  ★");
        }

        DrawPopRibbon(new Rect(panel.x + 20, panel.y + 12, panel.width - 40, 38), ribbonTrackTex, head);

        float curY = panel.y + 54f;

        if (newRecordTime || newRecordLap)
        {
            string recBanner = newRecordTime
                ? Loc.T("★ NEW COURSE RECORD! ★", "★ コース新記録達成！ ★")
                : Loc.T("★ NEW FASTEST LAP RECORD! ★", "★ ベストラップ新記録達成！ ★");
            DrawPopPill(new Rect(panel.x + (panel.width - 280) * 0.5f, curY, 280, 22), recBanner, new Color(1f, 0.72f, 0.1f));
            curY += 28f;
        }

        // ──────── 表ヘッダー行 (POS / DRIVER / TIME / DIFF) ────────
        float hx = panel.x + 24f, hw = panel.width - 48f;
        var headerBg = new Rect(hx, curY, hw, 26f);
        int hwInt = Mathf.RoundToInt(hw);
        var headTex = TextureGen.RoundedPanel(hwInt, 26, 6f, new Color(0.88f, 0.91f, 0.96f, 1f), new Color(0.72f, 0.78f, 0.88f), 1f);
        GUI.DrawTexture(headerBg, headTex);

        var headStyle = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        var headRight = St(sSmall, 12, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        GUI.Label(new Rect(hx + 12, curY, 65, 26), Loc.T("POS", "順位"), headStyle);
        GUI.Label(new Rect(hx + 86, curY, 220, 26), Loc.T("DRIVER / KART", "ドライバー / マシン"), headStyle);
        GUI.Label(new Rect(hx + hw - 180, curY, 90, 26), Loc.T("TIME", "タイム"), headRight);
        GUI.Label(new Rect(hx + hw - 85, curY, 75, 26), Loc.T("DIFF", "差"), headRight);

        // ──────── 各行の描画 ────────
        var order = placeOrder;
        float rowStartY = curY + 28f;
        float rowH = 36f;
        float firstTime = order.Count > 0 && order[0].Finished ? order[0].FinishTime : 0f;

        int drawnRows = 0;
        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            if (!k.gameObject.activeSelf) continue;
            var row = new Rect(hx, rowStartY + drawnRows * (rowH + 2f), hw, rowH);
            bool isMe = k.IsPlayer || (Online && playerKarts.ContainsKey(Karts.IndexOf(k)));

            // 行背景（プレイヤー行は鮮やかなシアンブルー、他行は爽やかなゼブラストライプ）
            int rw = Mathf.RoundToInt(row.width);
            int rh = Mathf.RoundToInt(row.height);
            if (isMe)
            {
                var rowTex = TextureGen.RoundedPanel(rw, rh, 8f, new Color(0.85f, 0.94f, 1f, 0.95f), new Color(0.15f, 0.65f, 0.95f), 2f);
                GUI.DrawTexture(row, rowTex);
            }
            else
            {
                Color bgCol = drawnRows % 2 == 0 ? new Color(0.96f, 0.97f, 0.99f, 0.9f) : new Color(0.91f, 0.93f, 0.97f, 0.6f);
                var rowTex = TextureGen.RoundedPanel(rw, rh, 6f, bgCol, new Color(0.82f, 0.86f, 0.92f, 0.8f), 1f);
                GUI.DrawTexture(row, rowTex);
            }

            // 順位メダルピルバッジ（1ST=ゴールド、2ND=シルバー、3RD=ブロンズ）
            Color medalBg = drawnRows == 0 ? new Color(0.96f, 0.72f, 0.08f)
                : drawnRows == 1 ? new Color(0.55f, 0.65f, 0.78f)
                : drawnRows == 2 ? new Color(0.85f, 0.50f, 0.22f)
                : new Color(0.32f, 0.38f, 0.52f);
            string posLabel = drawnRows < 3 ? $"★ {Loc.PlaceText(k.Place)}" : Loc.PlaceText(k.Place);
            DrawPopPill(new Rect(row.x + 8, row.y + 6, 68, 24), posLabel, medalBg);

            // カートカラーピル
            var kBadgeTex = TextureGen.RoundedPanel(18, 18, 5f, k.Color, new Color(0.1f, 0.15f, 0.25f, 0.6f), 1f);
            GUI.DrawTexture(new Rect(row.x + 88, row.y + 9, 18, 18), kBadgeTex);

            // ドライバー名
            string dName = isMe ? $"★ {k.Name}" : k.Name;
            Color nameColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + 114, row.y, 210, rowH), dName, St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, nameColor));

            // タイム
            string timeStr = k.Finished ? FormatTime(k.FinishTime) : "--:--.--";
            Color timeColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + hw - 180, row.y, 90, rowH), timeStr, St(sMono, 16, TextAnchor.MiddleRight, FontStyle.Bold, false, timeColor));

            // タイム差 (DIFF)
            string diffStr = drawnRows == 0 ? Loc.T("WINNER", "TOP") : (k.Finished && firstTime > 0 ? $"+{k.FinishTime - firstTime:0.00}" : "-");
            Color diffColor = drawnRows == 0 ? new Color(0.85f, 0.55f, 0.05f) : new Color(0.25f, 0.55f, 0.85f);
            GUI.Label(new Rect(row.x + hw - 85, row.y, 75, rowH), diffStr, St(sMono, 14, TextAnchor.MiddleRight, FontStyle.Bold, false, diffColor));
            drawnRows++;
        }

        // ──────── レースサマリー＆ベストラップ統計ボックス（6台以下のとき下部の余白をリッチに活用） ────────
        float tableBottom = rowStartY + drawnRows * (rowH + 2f);
        float btnY = panel.yMax - 54f;
        if (btnY - tableBottom >= 66f)
        {
            float sumY = tableBottom + 8f;
            float sumH = Mathf.Min(68f, btnY - tableBottom - 16f);
            var sumBox = new Rect(hx, sumY, hw, sumH);
            int sw = Mathf.RoundToInt(sumBox.width);
            int sh = Mathf.RoundToInt(sumBox.height);
            var sumTex = TextureGen.RoundedPanel(sw, sh, 10f, new Color(0.92f, 0.95f, 0.99f, 0.95f), new Color(0.72f, 0.80f, 0.92f), 1.5f);
            GUI.DrawTexture(sumBox, sumTex);

            float colW = hw / 3f;
            string cName = Loc.CourseName(SelectedCourse, Track.Courses[SelectedCourse].Name);
            float pBestLap = bestLap[0];
            if (TwoPlayer && bestLap[1] > 0 && (pBestLap <= 0 || bestLap[1] < pBestLap)) pBestLap = bestLap[1];
            float dispLap = pBestLap > 0 ? pBestLap : (firstTime > 0 ? firstTime / Mathf.Max(1, totalLaps) : 0f);
            float dispRec = GetBestTime(SelectedCourse) > 0 ? GetBestTime(SelectedCourse) : firstTime;

            var lblSt = St(sSmall, 11, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.38f, 0.46f, 0.62f));
            var valSt = St(sMono, 15, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.10f, 0.22f, 0.45f));

            GUI.Label(new Rect(hx, sumY + 6, colW, 18), Loc.T("COURSE", "走行コース"), lblSt);
            GUI.Label(new Rect(hx, sumY + 26, colW, 24), cName, St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.12f, 0.45f, 0.85f)));

            GUI.Label(new Rect(hx + colW, sumY + 6, colW, 18), Loc.T("FASTEST LAP", "ベストラップ"), lblSt);
            GUI.Label(new Rect(hx + colW, sumY + 26, colW, 24), dispLap > 0 ? FormatTime(dispLap) : "--:--.--", valSt);

            GUI.Label(new Rect(hx + colW * 2f, sumY + 6, colW, 18), Loc.T("COURSE RECORD", "コース記録"), lblSt);
            GUI.Label(new Rect(hx + colW * 2f, sumY + 26, colW, 24), dispRec > 0 ? FormatTime(dispRec) : "--:--.--", valSt);
        }

        // ──────── 最下部のアクションボタン (RACE AGAIN / TITLE) ────────
        float halfW = (hw - 16f) * 0.5f;
        bool canRestart = !Online || Net.IsHost;
        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && canRestart;
        string againLabel = canRestart
            ? Loc.T("►  RACE AGAIN", "►  もう一回レース")
            : Loc.T("►  HOST ONLY", "►  ホストのみ開始可");
        string againKey = canRestart ? "[ENTER / A]" : "";
        Color againCol = canRestart ? new Color(1f, 0.62f, 0.1f) : new Color(0.45f, 0.50f, 0.60f);
        if (DrawModernButton(new Rect(hx, btnY, halfW, 44), againLabel, againKey, againCol, canRestart, canRestart && resultsFocus == 0))
        {
            if (canRestart) StartRaceFromTitle();
        }
        GUI.enabled = prevEnabled;

        if (DrawModernButton(new Rect(hx + halfW + 16f, btnY, halfW, 44), Loc.T("◄  BACK TO TITLE", "◄  タイトルへ戻る"), "[ESC / B]", new Color(0.18f, 0.62f, 0.95f), false, !canRestart || resultsFocus == 1))
        {
            if (Online) LeaveOnline();
            else ReturnToTitle();
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

    Vector2 updateNotesScroll = Vector2.zero;

    void DrawUpdateModal(float w, float h)
    {
        if (updater == null) return;

        // 1. 背景の暗転オーバーレイ
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. モーダルポップカード
        float mw = 580f, mh = 480f;
        var r = new Rect((w - mw) * 0.5f, (h - mh) * 0.5f, mw, mh);
        DrawPopCard(r);

        // 3. ヘッダーリボン
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 36), ribbonDriverTex, Loc.T("★ TURBO CIRCUIT UPDATER ★", "★ アップデート確認 ★"));

        float cy = r.y + 58f;
        float cw = r.width - 48f;
        float cx = r.x + 24f;

        // バージョン対比バッジ
        DrawPopPill(new Rect(cx + 10, cy, 185, 26), Loc.T($"CURRENT: {AppUpdater.CurrentVersion}", $"現在: {AppUpdater.CurrentVersion}"), new Color(0.2f, 0.35f, 0.55f));
        string targetVer = string.IsNullOrEmpty(updater.LatestVersion) ? AppUpdater.CurrentVersion : updater.LatestVersion;
        Color targetCol = updater.State == AppUpdater.UpdateState.UpdateAvailable ? new Color(1f, 0.62f, 0.12f) : new Color(0.18f, 0.78f, 0.42f);
        DrawPopPill(new Rect(r.xMax - 220, cy, 185, 26), Loc.T($"LATEST: {targetVer}", $"最新: {targetVer}"), targetCol);

        cy += 38f;

        // 状態別コンテンツ描画
        switch (updater.State)
        {
            case AppUpdater.UpdateState.Checking:
                GUI.Label(new Rect(cx, cy + 60, cw, 30), Loc.T("CONNECTING TO GITHUB...", "GitHub に接続中..."), St(sSmall, 18, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.18f, 0.55f, 0.95f)));
                GUI.Label(new Rect(cx, cy + 100, cw, 24), Loc.T("Checking for latest releases and updates...", "最新リリースとアップデート情報を確認しています..."), St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.45f, 0.5f, 0.65f)));
                break;

            case AppUpdater.UpdateState.UpToDate:
            {
                GUI.Label(new Rect(cx, cy, cw, 28), Loc.T("★ YOU ARE UP TO DATE! ★", "★ 最新バージョンをお使いです！ ★"), St(sSmall, 18, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.18f, 0.75f, 0.38f)));
                cy += 30f;

                if (!string.IsNullOrEmpty(updater.ReleaseNotes))
                {
                    float noteH = r.yMax - cy - 66f;
                    var noteBox = new Rect(cx, cy, cw, noteH);
                    GUI.DrawTexture(noteBox, TextureGen.RoundedPanel(Mathf.RoundToInt(noteBox.width), Mathf.RoundToInt(noteBox.height), 8f, new Color(0.12f, 0.16f, 0.28f, 0.08f), new Color(0.65f, 0.72f, 0.84f), 1.5f));

                    var noteStyle = St(sSys, 14, TextAnchor.UpperLeft, FontStyle.Bold, true, new Color(0.16f, 0.22f, 0.36f));
                    float viewW = noteBox.width - 32f;
                    float contentH = Mathf.Max(noteBox.height - 16f, noteStyle.CalcHeight(new GUIContent(updater.ReleaseNotes), viewW) + 16f);
                    updateNotesScroll = GUI.BeginScrollView(new Rect(noteBox.x + 4, noteBox.y + 4, noteBox.width - 8, noteBox.height - 8), updateNotesScroll, new Rect(0, 0, noteBox.width - 24, contentH));
                    GUI.Label(new Rect(8, 6, viewW, contentH), updater.ReleaseNotes, noteStyle);
                    GUI.EndScrollView();
                }
                else
                {
                    GUI.Label(new Rect(cx, cy + 50, cw, 44), Loc.T($"You are running the latest version of Turbo Circuit ({AppUpdater.CurrentVersion}).\nNo new updates available right now.", $"現在、最新バージョンの Turbo Circuit ({AppUpdater.CurrentVersion}) を実行中です。\n新しいアップデートはありません。"), St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f)));
                }

                float bYUp = r.yMax - 54;
                float bW1Up = 240f, bW2Up = 210f, spUp = 16f;
                float sXUp = cx + (cw - (bW1Up + bW2Up + spUp)) * 0.5f;
                if (DrawModernButton(new Rect(sXUp, bYUp, bW1Up, 36), Loc.T("► CHECK AGAIN", "► 再チェック"), "[A]", new Color(0.18f, 0.65f, 0.95f), false, updateModalFocus == 0))
                {
                    updater.CheckForUpdates(openModalIfNew: true);
                }
                if (DrawModernButton(new Rect(sXUp + bW1Up + spUp, bYUp, bW2Up, 36), Loc.T("◄ CLOSE", "◄ 閉じる"), "[ESC/B]", new Color(0.2f, 0.82f, 0.45f), false, updateModalFocus == 1))
                {
                    updater.ShowModal = false;
                }
                break;
            }

            case AppUpdater.UpdateState.UpdateAvailable:
            {
                GUI.Label(new Rect(cx, cy, cw, 26), Loc.T($"★ NEW VERSION AVAILABLE: {updater.ReleaseName} ★", $"★ 新バージョンが利用可能: {updater.ReleaseName} ★"), St(sSys, 16, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.95f, 0.48f, 0.05f)));
                cy += 30f;

                // リリースノートスクロールビュー
                float noteH = r.yMax - cy - 66f;
                var noteBox = new Rect(cx, cy, cw, noteH);
                GUI.DrawTexture(noteBox, TextureGen.RoundedPanel(Mathf.RoundToInt(noteBox.width), Mathf.RoundToInt(noteBox.height), 8f, new Color(0.12f, 0.16f, 0.28f, 0.08f), new Color(0.65f, 0.72f, 0.84f), 1.5f));

                var noteStyle = St(sSys, 14, TextAnchor.UpperLeft, FontStyle.Bold, true, new Color(0.16f, 0.22f, 0.36f));
                float viewW = noteBox.width - 32f;
                float contentH = Mathf.Max(noteBox.height - 16f, noteStyle.CalcHeight(new GUIContent(updater.ReleaseNotes), viewW) + 16f);
                updateNotesScroll = GUI.BeginScrollView(new Rect(noteBox.x + 4, noteBox.y + 4, noteBox.width - 8, noteBox.height - 8), updateNotesScroll, new Rect(0, 0, noteBox.width - 24, contentH));
                GUI.Label(new Rect(8, 6, viewW, contentH), updater.ReleaseNotes, noteStyle);
                GUI.EndScrollView();

                float bY = r.yMax - 54;
                float bW1 = 290f, bW2 = 200f, sp = 16f;
                float sX = cx + (cw - (bW1 + bW2 + sp)) * 0.5f;
                if (DrawModernButton(new Rect(sX, bY, bW1, 38), Loc.T("► UPDATE NOW", "► 今すぐ更新する"), "[ENTER/A]", new Color(0.18f, 0.85f, 0.45f), true, updateModalFocus == 0))
                {
                    updater.StartDownload();
                }
                if (DrawModernButton(new Rect(sX + bW1 + sp, bY, bW2, 38), Loc.T("◄ LATER", "◄ あとで"), "[ESC/B]", new Color(0.55f, 0.6f, 0.7f), false, updateModalFocus == 1))
                {
                    updater.ShowModal = false;
                }
                break;
            }

            case AppUpdater.UpdateState.Downloading:
                GUI.Label(new Rect(cx, cy + 20, cw, 28), Loc.T("DOWNLOADING UPDATE PACKAGE...", "アップデートをダウンロード中..."), St(sSmall, 17, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.18f, 0.65f, 0.95f)));
                GUI.Label(new Rect(cx, cy + 52, cw, 24), updater.StatusMessage, St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.35f, 0.4f, 0.55f)));

                // プログレスバー
                float pbW = cw - 40f;
                float pbH = 34f;
                var pbRect = new Rect(cx + 20f, cy + 96f, pbW, pbH);

                // 背景トラック（ダークネイビー＋枠線）
                GUI.color = Color.white;
                GUI.DrawTexture(pbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.14f, 0.18f, 0.30f, 0.95f), 0f, 10f);

                // 進捗フィル（グリーン＋ハイライト）
                float prog = Mathf.Clamp01(updater.DownloadProgress);
                float innerMaxW = pbRect.width - 6f;
                float fillW = prog * innerMaxW;
                if (fillW > 2f)
                {
                    var fillRect = new Rect(pbRect.x + 3f, pbRect.y + 3f, Mathf.Max(12f, fillW), pbRect.height - 6f);
                    GUI.DrawTexture(fillRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.18f, 0.85f, 0.45f, 1f), 0f, 8f);
                    var glossRect = new Rect(fillRect.x + 2f, fillRect.y + 1f, Mathf.Max(8f, fillRect.width - 4f), (fillRect.height - 2f) * 0.45f);
                    GUI.DrawTexture(glossRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 0.28f), 0f, 6f);
                }

                // 外枠ボーダー
                GUI.DrawTexture(pbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.12f, 0.16f, 0.28f, 1f), 2.5f, 10f);
                GUI.color = Color.white;

                string pctText = $"{Mathf.RoundToInt(prog * 100f)}%";
                Outlined(pbRect, pctText, St(sSmall, 16, TextAnchor.MiddleCenter, FontStyle.Bold), Color.white, 1.5f);

                if (DrawModernButton(new Rect(cx + (cw - 220f) * 0.5f, r.yMax - 54, 220f, 36), Loc.T("◄ CANCEL", "◄ キャンセル"), "[ESC/B]", new Color(0.95f, 0.35f, 0.35f), false, true))
                {
                    updater.CancelDownload();
                }
                break;

            case AppUpdater.UpdateState.Downloaded:
            {
                GUI.Label(new Rect(cx, cy + 25, cw, 30), Loc.T("★ DOWNLOAD COMPLETE! ★", "★ ダウンロード完了！ ★"), St(sSmall, 20, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.18f, 0.85f, 0.45f)));
                GUI.Label(new Rect(cx, cy + 70, cw, 50), Loc.T("Update package is downloaded and verified.\nClick below to install and relaunch Turbo Circuit!", "アップデートパッケージの準備が完了しました。\n下のボタンを押すと自動更新して再起動します！"), St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.25f, 0.3f, 0.45f)));

                float bW1Done = 295f, bW2Done = 195f, spDone = 16f;
                float sXDone = cx + (cw - (bW1Done + bW2Done + spDone)) * 0.5f;
                if (DrawModernButton(new Rect(sXDone, r.yMax - 54, bW1Done, 38), Loc.T("► INSTALL & RESTART", "► 適用して再起動"), "[ENTER/A]", new Color(0.18f, 0.85f, 0.45f), true, updateModalFocus == 0))
                {
                    updater.ApplyUpdateAndRestart();
                }
                if (DrawModernButton(new Rect(sXDone + bW1Done + spDone, r.yMax - 54, bW2Done, 38), Loc.T("◄ LATER", "◄ あとで"), "[ESC/B]", new Color(0.55f, 0.6f, 0.7f), false, updateModalFocus == 1))
                {
                    updater.ShowModal = false;
                }
                break;
            }

            case AppUpdater.UpdateState.Applying:
                GUI.Label(new Rect(cx, cy + 40, cw, 30), Loc.T("RESTARTING GAME...", "ゲームを再起動中..."), St(sSmall, 20, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.18f, 0.65f, 0.95f)));
                GUI.Label(new Rect(cx, cy + 85, cw, 40), Loc.T("Turbo Circuit is applying update files and will relaunch momentarily.\nPlease wait...", "アップデートを適用して自動的に再起動しています。\nそのままお待ちください..."), St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.35f, 0.4f, 0.55f)));
                break;

            case AppUpdater.UpdateState.Error:
                GUI.Label(new Rect(cx, cy + 20, cw, 28), Loc.T("!  UPDATE FAILED  !", "！ アップデート失敗 ！"), St(sSmall, 17, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.95f, 0.35f, 0.35f)));
                GUI.Label(new Rect(cx, cy + 60, cw, 50), updater.ErrorMessage, St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.45f, 0.3f, 0.3f)));

                if (DrawModernButton(new Rect(cx + (cw - 416f) * 0.5f, r.yMax - 54, 200f, 36), Loc.T("► RETRY", "► 再試行"), "[ENTER/A]", new Color(0.18f, 0.65f, 0.95f), false, updateModalFocus == 0))
                {
                    updater.CheckForUpdates(openModalIfNew: true);
                }
                if (DrawModernButton(new Rect(cx + (cw - 416f) * 0.5f + 216f, r.yMax - 54, 200f, 36), Loc.T("◄ CLOSE", "◄ 閉じる"), "[ESC/B]", new Color(0.6f, 0.65f, 0.75f), false, updateModalFocus == 1))
                {
                    updater.ShowModal = false;
                }
                break;
        }
    }

    static readonly (int w, int h)[] SupportedResolutions = new[]
    {
        (1280, 720),
        (1366, 768),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
        (3840, 2160)
    };

    void CycleResolution(int delta)
    {
        int curIdx = 0;
        int curW = Screen.width, curH = Screen.height;
        for (int i = 0; i < SupportedResolutions.Length; i++)
        {
            if (curW == SupportedResolutions[i].w && curH == SupportedResolutions[i].h)
            {
                curIdx = i;
                break;
            }
        }
        int nextIdx = (curIdx + delta + SupportedResolutions.Length) % SupportedResolutions.Length;
        var next = SupportedResolutions[nextIdx];
        Screen.SetResolution(next.w, next.h, Screen.fullScreen);
        PlayerPrefs.SetInt("tc_res_w", next.w);
        PlayerPrefs.SetInt("tc_res_h", next.h);
        PlayerPrefs.Save();
        if (Audio != null) Audio.Select();
    }

    void DrawQuitModal(float w, float h)
    {
        // 1. 暗転オーバーレイ
        GUI.color = new Color(0f, 0f, 0f, 0.70f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 2. モーダルポップカード
        float mw = 480f, mh = 220f;
        var r = new Rect((w - mw) * 0.5f, (h - mh) * 0.5f, mw, mh);
        DrawPopCard(r);

        // 3. ヘッダーリボン
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 34), ribbonDriverTex, Loc.T("★ QUIT TURBO CIRCUIT? ★", "★ ゲームの終了 ★"));

        // 4. メッセージテキスト
        var msgStyle = St(sSmall, 18, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.14f, 0.20f, 0.35f));
        GUI.Label(new Rect(r.x + 20, r.y + 58, r.width - 40, 26), Loc.T("Exit to desktop?", "デスクトップに戻りますか？"), msgStyle);

        var subStyle = St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.42f, 0.48f, 0.62f));
        GUI.Label(new Rect(r.x + 20, r.y + 86, r.width - 40, 22), Loc.T("Are you sure you want to quit the game?", "ゲームを終了してもよろしいですか？"), subStyle);

        // 5. ボタン群 (QUIT と CANCEL)
        float bw = 185f, bh = 42f, gap = 20f;
        float bx1 = r.x + (r.width - (bw * 2 + gap)) * 0.5f;
        float bx2 = bx1 + bw + gap;
        float by = r.y + 130f;

        if (DrawModernButton(new Rect(bx1, by, bw, bh), Loc.T("✖  QUIT", "✖  終了"), "[ENTER / A]", new Color(0.92f, 0.32f, 0.32f), false, quitModalFocus == 0))
        {
            Application.Quit();
        }

        if (DrawModernButton(new Rect(bx2, by, bw, bh), Loc.T("◄  CANCEL", "◄  戻る"), "[ESC / B]", new Color(0.18f, 0.62f, 0.95f), false, quitModalFocus == 1))
        {
            CloseQuitModal();
        }
    }

}
