using UnityEngine;

public partial class RaceManager
{
    void DrawResults(float w, float h)
    {
        // レース背景の半透明オーバーレイ
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var panel = new Rect(w / 2 - 320, 65, 640, 545);
        DrawPopCard(panel);

        // ヘッダータイトル
        bool isCupFinal = cupActive && (cupStage + 1 >= DefaultCups[cupIndex].courses.Length);
        string head = Loc.T("★  RACE RESULTS  ★", "★  レース結果  ★");
        if (cupActive)
        {
            var cup = DefaultCups[cupIndex];
            head = isCupFinal
                ? Loc.T($"★ {cup.nameEn} - FINAL STANDINGS! ★", $"★ {cup.nameJa} - 総合結果！ ★")
                : Loc.T($"★ {cup.nameEn} - RACE {cupStage + 1}/{cup.courses.Length} ★", $"★ {cup.nameJa} - 第{cupStage + 1}/{cup.courses.Length}戦 ★");
        }
        else if (TwoPlayer)
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

        if (cupActive && isCupFinal && cupFinalRank > 0)
        {
            string trophyTxt = cupFinalRank == 1
                ? Loc.T("★ CHAMPION! GOLD TROPHY! ★", "★ 総合優勝！ゴールドトロフィー！ ★")
                : cupFinalRank == 2
                    ? Loc.T("★ 2ND PLACE! SILVER TROPHY! ★", "★ 総合2位！シルバートロフィー！ ★")
                    : cupFinalRank == 3
                        ? Loc.T("★ 3RD PLACE! BRONZE TROPHY! ★", "★ 総合3位！ブロンズトロフィー！ ★")
                        : Loc.T($"FINAL CUP RANK: {Loc.PlaceText(cupFinalRank)}", $"カップ総合順位: {Loc.PlaceText(cupFinalRank)}");
            Color trophyCol = cupFinalRank == 1 ? new Color(0.96f, 0.72f, 0.08f)
                : cupFinalRank == 2 ? new Color(0.55f, 0.65f, 0.78f)
                : cupFinalRank == 3 ? new Color(0.85f, 0.50f, 0.22f)
                : new Color(0.32f, 0.48f, 0.72f);
            DrawPopPill(new Rect(panel.x + (panel.width - 340) * 0.5f, curY, 340, 22), trophyTxt, trophyCol);
            curY += 28f;
        }
        else if (newRecordTime || newRecordLap)
        {
            string recBanner = newRecordTime
                ? Loc.T("★ NEW COURSE RECORD! ★", "★ コース新記録達成！ ★")
                : Loc.T("★ NEW FASTEST LAP RECORD! ★", "★ ベストラップ新記録達成！ ★");
            DrawPopPill(new Rect(panel.x + (panel.width - 280) * 0.5f, curY, 280, 22), recBanner, new Color(1f, 0.72f, 0.1f));
            curY += 28f;
        }

        // ──────── 表ヘッダー行 (POS / DRIVER / TIME / DIFF or PTS) ────────
        float hx = panel.x + 24f, hw = panel.width - 48f;
        var headerBg = new Rect(hx, curY, hw, 26f);
        int hwInt = Mathf.RoundToInt(hw);
        var headTex = TextureGen.RoundedPanel(hwInt, 26, 6f, new Color(0.88f, 0.91f, 0.96f, 1f), new Color(0.72f, 0.78f, 0.88f), 1f);
        GUI.DrawTexture(headerBg, headTex);

        var headStyle = St(sSmall, 12, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        var headRight = St(sSmall, 12, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.22f, 0.30f, 0.45f));
        GUI.Label(new Rect(hx + 12, curY, 65, 26), Loc.T("POS", "順位"), headStyle);
        GUI.Label(new Rect(hx + 86, curY, 220, 26), Loc.T("DRIVER / KART", "ドライバー / マシン"), headStyle);
        GUI.Label(new Rect(hx + hw - 180, curY, 90, 26), cupActive && !isCupFinal ? Loc.T("RACE PTS", "獲得PT") : Loc.T("TIME", "タイム"), headRight);
        GUI.Label(new Rect(hx + hw - 85, curY, 75, 26), cupActive ? Loc.T("TOTAL", "合計PT") : Loc.T("DIFF", "差"), headRight);

        // ──────── 各行の描画 ────────
        var order = placeOrder;
        if (cupActive && isCupFinal)
        {
            order = new System.Collections.Generic.List<Kart>(placeOrder);
            order.Sort((a, b) =>
            {
                int ia = Karts.IndexOf(a);
                int ib = Karts.IndexOf(b);
                int pa = (ia >= 0 && ia < cupPoints.Length) ? cupPoints[ia] : 0;
                int pb = (ib >= 0 && ib < cupPoints.Length) ? cupPoints[ib] : 0;
                int cmp = pb.CompareTo(pa);
                return cmp != 0 ? cmp : a.Place.CompareTo(b.Place);
            });
        }
        float rowStartY = curY + 28f;
        float rowH = 36f;
        float firstTime = placeOrder.Count > 0 && placeOrder[0].Finished ? placeOrder[0].FinishTime : 0f;

        int drawnRows = 0;
        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            if (!k.gameObject.activeSelf) continue;
            var row = new Rect(hx, rowStartY + drawnRows * (rowH + 2f), hw, rowH);
            int kIdx = Karts.IndexOf(k);
            bool isMe = k.IsPlayer || (Online && playerKarts.ContainsKey(kIdx));

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
            int dispPlace = (cupActive && isCupFinal) ? (drawnRows + 1) : k.Place;
            string posLabel = drawnRows < 3 ? $"★ {Loc.PlaceText(dispPlace)}" : Loc.PlaceText(dispPlace);
            DrawPopPill(new Rect(row.x + 8, row.y + 6, 68, 24), posLabel, medalBg);

            // カートカラーピル
            var kBadgeTex = TextureGen.RoundedPanel(18, 18, 5f, k.Color, new Color(0.1f, 0.15f, 0.25f, 0.6f), 1f);
            GUI.DrawTexture(new Rect(row.x + 88, row.y + 9, 18, 18), kBadgeTex);

            // ドライバー名
            string dName = isMe ? $"★ {k.Name}" : k.Name;
            Color nameColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + 114, row.y, 210, rowH), dName, St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, nameColor));

            // タイム or 獲得ポイント
            string timeStr = (cupActive && !isCupFinal)
                ? $"+{GetCupPointsForPlace(k.Place)} PT"
                : (k.Finished ? FormatTime(k.FinishTime) : "--:--.--");
            Color timeColor = isMe ? new Color(0.05f, 0.25f, 0.65f) : new Color(0.12f, 0.16f, 0.28f);
            GUI.Label(new Rect(row.x + hw - 180, row.y, 90, rowH), timeStr, St(sMono, 16, TextAnchor.MiddleRight, FontStyle.Bold, false, timeColor));

            // タイム差 (DIFF) or カップ合計PT
            string diffStr;
            Color diffColor;
            if (cupActive)
            {
                int totalPt = (kIdx >= 0 && kIdx < cupPoints.Length) ? cupPoints[kIdx] : 0;
                diffStr = $"{totalPt} PT";
                diffColor = drawnRows == 0 ? new Color(0.85f, 0.55f, 0.05f) : new Color(0.25f, 0.55f, 0.85f);
            }
            else
            {
                diffStr = drawnRows == 0 ? Loc.T("WINNER", "TOP") : (k.Finished && firstTime > 0 ? $"+{k.FinishTime - firstTime:0.00}" : "-");
                diffColor = drawnRows == 0 ? new Color(0.85f, 0.55f, 0.05f) : new Color(0.25f, 0.55f, 0.85f);
            }
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

        // ──────── 最下部のアクションボタン (NEXT RACE / RACE AGAIN / TITLE) ────────
        float halfW = (hw - 16f) * 0.5f;
        bool canRestart = !Online || Net.IsHost;
        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && canRestart;
        string againLabel;
        if (cupActive && !isCupFinal)
        {
            int totalC = DefaultCups[cupIndex].courses.Length;
            againLabel = Loc.T($"►  NEXT RACE ({cupStage + 2}/{totalC})", $"►  次のレース ({cupStage + 2}/{totalC})");
        }
        else if (cupActive && isCupFinal)
        {
            againLabel = Loc.T("►  NEW CUP", "►  もう一回カップ");
        }
        else
        {
            againLabel = canRestart
                ? Loc.T("►  RACE AGAIN", "►  もう一回レース")
                : Loc.T("►  HOST ONLY", "►  ホストのみ開始可");
        }
        string againKey = canRestart ? "[ENTER / A]" : "";
        Color againCol = canRestart ? new Color(1f, 0.62f, 0.1f) : new Color(0.45f, 0.50f, 0.60f);
        if (DrawModernButton(new Rect(hx, btnY, halfW, 44), againLabel, againKey, againCol, canRestart, canRestart && resultsFocus == 0))
        {
            if (canRestart)
            {
                if (cupActive && !isCupFinal) AdvanceCupStage();
                else StartRaceFromTitle();
            }
        }
        GUI.enabled = prevEnabled;

        string backLabel = (cupActive && !isCupFinal)
            ? Loc.T("◄  QUIT CUP", "◄  カップを中断")
            : Loc.T("◄  BACK TO TITLE", "◄  タイトルへ戻る");
        if (DrawModernButton(new Rect(hx + halfW + 16f, btnY, halfW, 44), backLabel, "[ESC / B]", new Color(0.18f, 0.62f, 0.95f), false, !canRestart || resultsFocus == 1))
        {
            if (Online) LeaveOnline();
            else ReturnToTitle();
        }
    }
}
