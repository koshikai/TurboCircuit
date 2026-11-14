using UnityEngine;

public partial class RaceManager
{
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
            float bSize = 116f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 46, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        if (titleFocus == 5)
            DrawFocusHighlight(new Rect(leftX + 14, cardY + 163, cardW - 28, 30), 8f);
        string cTitle = Loc.CourseName(SelectedCourse, curDef.Name);
        var courseTitleStyle = St(sMid, Loc.IsJa ? 19 : 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.12f, 0.18f, 0.35f));
        GUI.Label(new Rect(leftX + 8, cardY + 166, cardW - 16, 26), $"< {cTitle} >", courseTitleStyle);

        string diffStr = Loc.CourseDifficulty(SelectedCourse);
        Color diffBg = SelectedCourse == 0 ? new Color(0.2f, 0.78f, 0.42f) : SelectedCourse == 1 ? new Color(1f, 0.65f, 0.15f) : SelectedCourse == 2 ? new Color(1f, 0.28f, 0.38f) : SelectedCourse == 3 ? new Color(0.62f, 0.3f, 0.95f) : SelectedCourse == 4 ? new Color(0.12f, 0.68f, 0.36f) : SelectedCourse == 5 ? new Color(0.08f, 0.65f, 0.98f) : new Color(0.95f, 0.42f, 0.18f);
        DrawPopPill(new Rect(leftX + (cardW - 154) * 0.5f, cardY + 195, 154, 22), diffStr, diffBg);

        var descStyle = St(sSmall, Loc.IsJa ? 11 : 12, TextAnchor.UpperCenter, FontStyle.Normal, true, new Color(0.26f, 0.30f, 0.42f));
        string descText = IsCupMode ? Loc.CupDesc(DefaultCups[cupIndex]) : Loc.CourseDesc(SelectedCourse, curDef.Description);
        GUI.Label(new Rect(leftX + 16, cardY + 221, cardW - 32, 48), descText, descStyle);

        if (IsCupMode)
        {
            var cup = DefaultCups[cupIndex];
            int bestRank = GetCupBestRank(cup.id);
            string cupRecStr = bestRank > 0
                ? Loc.T($"TROPHY BEST: {Loc.PlaceText(bestRank)}", $"最高順位: {Loc.PlaceText(bestRank)}")
                : Loc.T("NO TROPHY YET", "トロフィー未獲得");
            Color cupRecCol = bestRank == 1 ? new Color(0.96f, 0.72f, 0.08f) : bestRank > 0 ? new Color(0.25f, 0.68f, 0.95f) : new Color(0.45f, 0.5f, 0.65f);
            DrawPopPill(new Rect(leftX + (cardW - 280) * 0.5f, cardY + 272, 280, 22), cupRecStr, cupRecCol);
        }
        else
        {
            float bTime = GetBestTime(SelectedCourse);
            float bLap = GetBestLap(SelectedCourse);
            string recStr = bTime > 0
                ? Loc.T($"RECORD: {FormatTime(bTime)}  (LAP {FormatTime(bLap)})", $"記録: {FormatTime(bTime)}  (最速ラップ {FormatTime(bLap)})")
                : Loc.T("NO RECORD YET", "まだ記録がありません");
            Color recCol = bTime > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.45f, 0.5f, 0.65f);
            DrawPopPill(new Rect(leftX + (cardW - 280) * 0.5f, cardY + 272, 280, 22), recStr, recCol);
        }

        string modeLabel = CurrentMode == GameMode.TimeAttack
            ? Loc.T("★  TIME ATTACK", "★  タイムアタック")
            : CurrentMode == GameMode.Cup
                ? Loc.T("★  CUP GRAND PRIX", "★  カップ戦 (GP)")
                : Loc.T($"★  {totalLaps} LAPS GP", $"★  {totalLaps}周 グランプリ");
        Color modeCol = CurrentMode == GameMode.TimeAttack
            ? new Color(1f, 0.45f, 0.15f)
            : CurrentMode == GameMode.Cup
                ? new Color(0.62f, 0.32f, 0.94f)
                : new Color(0.12f, 0.58f, 0.95f);
        if (DrawModernButton(new Rect(leftX + (cardW - 260) * 0.5f, cardY + 299, 260, 28), modeLabel, "[M]", modeCol, true, titleFocus == 6))
        {
            CycleGameMode(1);
        }

        if (IsTimeAttack)
        {
            DrawPopPill(new Rect(leftX + (cardW - 260) * 0.5f, cardY + 332, 260, 28), Loc.T("★  100cc (TIME ATTACK)", "★  100cc (タイムアタック固定)"), new Color(0.45f, 0.50f, 0.62f));
        }
        else
        {
            string ccLabel = Loc.SpeedClassLabel(CurrentSpeedClass);
            Color ccCol = CurrentSpeedClass == SpeedClass.Cc50
                ? new Color(0.20f, 0.78f, 0.42f)
                : CurrentSpeedClass == SpeedClass.Cc150
                    ? new Color(0.95f, 0.30f, 0.25f)
                    : new Color(0.14f, 0.62f, 0.95f);
            if (DrawModernButton(new Rect(leftX + (cardW - 260) * 0.5f, cardY + 332, 260, 28), ccLabel, "[C]", ccCol, false, titleFocus == 15))
            {
                CycleSpeedClass(1);
            }
        }

        if (IsCupMode)
        {
            var cup = DefaultCups[cupIndex];
            if (titleFocus == 13)
                DrawFocusHighlight(new Rect(leftX + 16, cardY + 363, cardW - 32, 32), 7f);
            if (DrawModernButton(new Rect(leftX + 18, cardY + 365, 30, 28), "◄", "", cup.color))
                SwitchCup(-1);
            var cupTitleSt = St(sSmall, Loc.IsJa ? 13 : 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            Outlined(new Rect(leftX + 50, cardY + 365, cardW - 100, 28), $"{Loc.CupName(cup)} ({cup.courses.Length})", cupTitleSt, cup.color, 1.5f);
            if (DrawModernButton(new Rect(leftX + cardW - 48, cardY + 365, 30, 28), "►", "", cup.color))
                SwitchCup(1);
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
            float rowW = Mathf.Min(516f, w - (cardW + 35f) * 2f - 16f);
            float rowX = (w - rowW) * 0.5f;
            float gap = 8f;
            float b1W = Mathf.Round(rowW * 0.35f);
            float b2W = Mathf.Round(rowW * 0.32f);
            float b3W = rowW - b1W - b2W - gap * 2f;
            string pCountLabel = TwoPlayer ? Loc.T("★ 2P SPLIT", "★ 2P 画面分割") : Loc.T("★ 1P SOLO", "★ 1P ソロ");
            if (DrawModernButton(new Rect(rowX, 554f, b1W, 32), pCountLabel, "[TAB / Y]", modeBg, TwoPlayer, titleFocus == 7))
            {
                if (IsTimeAttack) IsTimeAttack = false;
                SetTwoPlayer(!TwoPlayer);
                if (Audio != null) Audio.Select();
            }
            if (DrawModernButton(new Rect(rowX + b1W + gap, 554f, b2W, 32), Loc.T("★ ONLINE", "★ オンライン"), "[O / X]", new Color(0.15f, 0.72f, 0.52f), false, titleFocus == 8))
            {
                if (IsTimeAttack) IsTimeAttack = false;
                cupActive = false;
                netMenu = true;
                netFocus = 0;
                if (Audio != null) Audio.Select();
            }
            if (DrawModernButton(new Rect(rowX + b1W + b2W + gap * 2f, 554f, b3W, 32), Loc.T("★ RECORDS", "★ 記録"), "[R]", new Color(0.95f, 0.58f, 0.12f), false, titleFocus == 14))
            {
                OpenRecordsModal();
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
            ? Loc.T("P1: [W/S] Drv [Q/E] Kart  •  P2: [↑/↓] Drv [←/→] Kart  •  [A/D / LB/RB] Track  •  [M] Mode  •  [C] Class  •  [R] Records  •  [TAB/Y] 1P/2P  •  [ESC] Quit",
                    "1P: [W/S][Q/E]  •  2P: [↑/↓][←/→]  •  [A/D] コース  •  [M] モード  •  [C] クラス  •  [R] 記録  •  [TAB/Y] 1P/2P  •  [ESC] 終了")
            : Loc.T("[D-Pad/Stick] Select  •  [A/Enter] OK  •  [LB/RB] Track  •  [M] Mode  •  [C] Class  •  [R] Records  •  [O/X] Online  •  [P] Options  •  [ESC] Quit",
                    "[十字キー] 選択  •  [A/Enter] 決定  •  [LB/RB] コース  •  [M] モード  •  [C] クラス  •  [R] 記録  •  [O/X] オンライン  •  [P] 設定  •  [ESC] 終了");
        Outlined(new Rect(0, h - 32, w, 28), guideText, barStyle, new Color(0.9f, 0.95f, 1f), 1);
    }
}
