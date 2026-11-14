using UnityEngine;

public partial class RaceManager
{
    Vector2 updateNotesScroll = Vector2.zero;
    string cachedReleaseNotesStr;
    readonly GUIContent cachedReleaseNotesContent = new GUIContent();

    GUIContent GetReleaseNotesContent(string notes)
    {
        if (!string.Equals(cachedReleaseNotesStr, notes, System.StringComparison.Ordinal))
        {
            cachedReleaseNotesStr = notes;
            cachedReleaseNotesContent.text = notes ?? string.Empty;
        }
        return cachedReleaseNotesContent;
    }

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
                    float contentH = Mathf.Max(noteBox.height - 16f, noteStyle.CalcHeight(GetReleaseNotesContent(updater.ReleaseNotes), viewW) + 16f);
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
                float contentH = Mathf.Max(noteBox.height - 16f, noteStyle.CalcHeight(GetReleaseNotesContent(updater.ReleaseNotes), viewW) + 16f);
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

    void DrawRecordsModal(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float mw = 720f, mh = 560f;
        var r = new Rect((w - mw) * 0.5f, (h - mh) * 0.5f, mw, mh);
        DrawPopCard(r);
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 36), ribbonTrackTex, Loc.T("★ RECORDS & TROPHIES ★", "★ レコード＆トロフィー ★"));

        float cx = r.x + 24f;
        float cw = r.width - 48f;
        float curY = r.y + 56f;

        var secHdrSt = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.24f, 0.32f, 0.48f));
        var colHdrRight = St(sSmall, 11, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.35f, 0.42f, 0.58f));
        GUI.Label(new Rect(cx + 8, curY, 240, 20), Loc.T("COURSE BEST RECORDS", "コース別ベスト記録"), secHdrSt);
        GUI.Label(new Rect(cx + cw - 240, curY, 110, 20), Loc.T("BEST TIME", "ベストタイム"), colHdrRight);
        GUI.Label(new Rect(cx + cw - 120, curY, 110, 20), Loc.T("BEST LAP", "ベストラップ"), colHdrRight);
        curY += 22f;

        float rowH = 30f;
        float gap = 3f;
        var nameSt = St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.18f, 0.32f));
        var timeSt = St(sMono, 14, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.08f, 0.32f, 0.65f));
        var noneSt = St(sMono, 13, TextAnchor.MiddleRight, FontStyle.Normal, false, new Color(0.50f, 0.56f, 0.68f));

        for (int i = 0; i < Track.Courses.Length; i++)
        {
            var row = new Rect(cx, curY + i * (rowH + gap), cw, rowH);
            bool isSel = (i == SelectedCourse);
            Color bg = isSel ? new Color(0.86f, 0.94f, 1f, 0.95f) : (i % 2 == 0 ? new Color(0.96f, 0.97f, 0.99f, 0.95f) : new Color(0.91f, 0.94f, 0.98f, 0.85f));
            Color bd = isSel ? new Color(0.18f, 0.62f, 0.95f) : new Color(0.80f, 0.85f, 0.92f);
            GUI.DrawTexture(row, TextureGen.RoundedPanel(Mathf.RoundToInt(row.width), Mathf.RoundToInt(row.height), 6f, bg, bd, isSel ? 1.5f : 1f));

            if (recordsFocus == i) DrawFocusHighlight(row, 6f);

            string cName = Loc.CourseName(i, Track.Courses[i].Name);
            GUI.Label(new Rect(row.x + 12, row.y, 240, rowH), $"{i + 1}. {cName}", nameSt);

            string sDiff = Loc.CourseShortDifficulty(i);
            Color dCol = i == 0 ? new Color(0.2f, 0.78f, 0.42f) : i == 1 ? new Color(1f, 0.65f, 0.15f) : i == 2 ? new Color(1f, 0.28f, 0.38f) : i == 3 ? new Color(0.62f, 0.3f, 0.95f) : i == 4 ? new Color(0.12f, 0.68f, 0.36f) : i == 5 ? new Color(0.08f, 0.65f, 0.98f) : new Color(0.95f, 0.42f, 0.18f);
            DrawPopPill(new Rect(row.x + 258, row.y + 5, 118, 20), sDiff, dCol);

            float bt = GetBestTime(i);
            float bl = GetBestLap(i);
            GUI.Label(new Rect(row.x + cw - 240, row.y, 110, rowH), bt > 0 ? FormatTime(bt) : "--:--.--", bt > 0 ? timeSt : noneSt);
            GUI.Label(new Rect(row.x + cw - 120, row.y, 110, rowH), bl > 0 ? FormatTime(bl) : "--:--.--", bl > 0 ? timeSt : noneSt);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                LoadCourse(i);
                CloseRecordsModal();
            }
        }

        curY += Track.Courses.Length * (rowH + gap) + 10f;
        GUI.Label(new Rect(cx + 8, curY, 300, 20), Loc.T("CUP TROPHIES", "カップ戦トロフィー"), secHdrSt);
        curY += 22f;

        float cupRowH = 32f;
        for (int c = 0; c < DefaultCups.Length; c++)
        {
            var cup = DefaultCups[c];
            var crow = new Rect(cx, curY + c * (cupRowH + 3f), cw, cupRowH);
            GUI.DrawTexture(crow, TextureGen.RoundedPanel(Mathf.RoundToInt(crow.width), Mathf.RoundToInt(crow.height), 6f, new Color(0.95f, 0.96f, 0.99f, 0.95f), new Color(0.80f, 0.85f, 0.92f), 1f));
            GUI.Label(new Rect(crow.x + 12, crow.y, 260, cupRowH), Loc.CupName(cup), nameSt);
            GUI.Label(new Rect(crow.x + 280, crow.y, 130, cupRowH), Loc.T($"{cup.courses.Length} COURSES", $"{cup.courses.Length}コース"), St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.38f, 0.45f, 0.60f)));

            int bestRank = GetCupBestRank(cup.id);
            string rankTxt = bestRank > 0 ? Loc.T($"BEST: {Loc.PlaceText(bestRank)}", $"最高: {Loc.PlaceText(bestRank)}") : Loc.T("NO TROPHY", "未獲得");
            Color rankCol = bestRank == 1 ? new Color(0.95f, 0.72f, 0.08f)
                : bestRank == 2 ? new Color(0.55f, 0.65f, 0.78f)
                : bestRank == 3 ? new Color(0.85f, 0.50f, 0.22f)
                : bestRank > 0 ? new Color(0.28f, 0.58f, 0.88f)
                : new Color(0.52f, 0.58f, 0.68f);
            DrawPopPill(new Rect(crow.xMax - 150, crow.y + 5, 138, 22), rankTxt, rankCol);
        }

        if (DrawModernButton(new Rect(r.x + (r.width - 240f) * 0.5f, r.yMax - 50f, 240f, 38f), Loc.T("◄  CLOSE", "◄  閉じる"), "[ESC / R / B]", new Color(0.18f, 0.62f, 0.95f), false, recordsFocus == Track.Courses.Length))
        {
            CloseRecordsModal();
        }
    }
}
