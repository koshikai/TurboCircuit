using UnityEngine;

public partial class RaceManager
{
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
            if (Audio != null) Audio.Cancel();
        }
    }
}
