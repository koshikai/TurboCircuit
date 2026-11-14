using UnityEngine;

public partial class RaceManager
{
    float stickNavTimer = 0f;

    void PollMenuNav(out int moveX, out int moveY, out bool confirm, out bool cancel, out bool tabPrev, out bool tabNext)
    {
        InputSettings.Init();
        GamepadHaptics.PollPads();
        moveX = 0;
        moveY = 0;

        if (!((showSettings && settingsTab == 0) || (state == State.Title && netMenu && !LobbyActive && (Net == null || !Net.Busy))) && GUIUtility.keyboardControl != 0)
        {
            GUIUtility.keyboardControl = 0;
        }
        bool editingText = IsEditingTextField;
        if (editingText && (GameInput.GetKeyDown(KeyCode.Return) || GameInput.GetKeyDown(KeyCode.KeypadEnter) || GameInput.GetKeyDown(KeyCode.Escape)))
        {
            GUIUtility.keyboardControl = 0;
        }

        Vector2 padVec = InputSettings.GetMenuPadVector();
        if (!editingText && padVec == Vector2.zero)
        {
            float ax = GameInput.GetAxisRaw("Horizontal");
            float ay = GameInput.GetAxisRaw("Vertical");
            if (Mathf.Abs(ax) > 0.55f || Mathf.Abs(ay) > 0.55f)
                padVec = new Vector2(ax, ay);
        }

        Vector2Int dir = new Vector2Int(
            padVec.x > 0.45f ? 1 : padVec.x < -0.45f ? -1 : 0,
            padVec.y > 0.45f ? 1 : padVec.y < -0.45f ? -1 : 0
        );

        if (dir == Vector2Int.zero)
        {
            lastNavDir = Vector2Int.zero;
            navRepeatTimer = 0f;
        }
        else if (dir != lastNavDir)
        {
            moveX = dir.x;
            moveY = dir.y;
            lastNavDir = dir;
            navRepeatTimer = 0.25f;
        }
        else
        {
            navRepeatTimer -= Time.unscaledDeltaTime;
            if (navRepeatTimer <= 0f)
            {
                moveX = dir.x;
                moveY = dir.y;
                navRepeatTimer = 0.13f;
            }
        }

        confirm = InputSettings.GetSinglePadDown(PadBind.A, 0) || InputSettings.GetSinglePadDown(PadBind.A, 1)
               || (!editingText && (GameInput.GetKeyDown(KeyCode.Return) || GameInput.GetKeyDown(KeyCode.KeypadEnter)));
        cancel = InputSettings.GetSinglePadDown(PadBind.B, 0) || InputSettings.GetSinglePadDown(PadBind.B, 1)
              || GameInput.GetKeyDown(KeyCode.Escape);
        tabPrev = InputSettings.GetSinglePadDown(PadBind.LB, 0) || InputSettings.GetSinglePadDown(PadBind.LB, 1);
        tabNext = InputSettings.GetSinglePadDown(PadBind.RB, 0) || InputSettings.GetSinglePadDown(PadBind.RB, 1);
    }

    static readonly int[] TitleFocusOrder1P = { 1, 2, 7, 8, 14, 0, 6, 15, 13, 5, 9, 10, 11, 12 };
    static readonly int[] TitleFocusOrder2P = { 1, 2, 3, 4, 7, 8, 14, 0, 6, 15, 13, 5, 9, 10, 11, 12 };

    // メニュー操作の入力スナップショット（Update の先頭で1フレームに1回だけ取得する）
    struct MenuNav
    {
        public int moveX, moveY;
        public bool confirm, cancel, tabPrev, tabNext;
        public bool padStart, padBack, padX, padY;
    }

    void OpenRecordsModal()
    {
        showRecords = true;
        recordsFocus = Track.Courses.Length;
        if (Audio != null) Audio.Select();
    }

    void CloseRecordsModal()
    {
        showRecords = false;
        if (Audio != null) Audio.Cancel();
    }

    void UpdateRecordsInput(in MenuNav nav)
    {
        if (nav.moveY != 0)
        {
            int count = Track.Courses.Length + 1;
            recordsFocus = (recordsFocus - nav.moveY + count) % count;
            if (Audio != null) Audio.Tick();
        }
        if (nav.cancel || GameInput.GetKeyDown(KeyCode.R))
        {
            CloseRecordsModal();
            return;
        }
        if (nav.confirm)
        {
            if (recordsFocus >= 0 && recordsFocus < Track.Courses.Length)
            {
                LoadCourse(recordsFocus);
                CloseRecordsModal();
            }
            else
            {
                CloseRecordsModal();
            }
        }
    }

    // ゲーム終了確認モーダル表示・非表示
    void ShowQuitModal()
    {
        showQuitConfirm = true;
        quitModalFocus = 1; // 既定は「戻る (CANCEL)」
        if (Audio != null) Audio.Select();
    }

    void CloseQuitModal()
    {
        showQuitConfirm = false;
        if (Audio != null) Audio.Cancel();
    }

    // ゲーム終了確認モーダル表示中の入力
    void UpdateQuitModalInput(in MenuNav nav)
    {
        if (nav.moveX != 0)
        {
            quitModalFocus = 1 - quitModalFocus;
            if (Audio != null) Audio.Tick();
        }
        if (nav.cancel)
        {
            CloseQuitModal();
            return;
        }
        if (nav.confirm)
        {
            if (quitModalFocus == 0)
            {
                Application.Quit();
            }
            else
            {
                CloseQuitModal();
            }
            return;
        }
    }

    // アップデート確認モーダル表示中の入力
    void UpdateModalInput(in MenuNav nav)
    {
        if (nav.moveX != 0 || nav.moveY != 0)
        {
            updateModalFocus = 1 - updateModalFocus;
            if (Audio != null) Audio.Tick();
        }
        if (nav.cancel)
        {
            if (updater.State == AppUpdater.UpdateState.Downloading) updater.CancelDownload();
            else updater.ShowModal = false;
            if (Audio != null) Audio.Cancel();
            return;
        }
        if (nav.confirm)
        {
            if (updater.State == AppUpdater.UpdateState.Downloading) { updater.CancelDownload(); if (Audio != null) Audio.Cancel(); return; }
            else if (updateModalFocus == 1) { updater.ShowModal = false; if (Audio != null) Audio.Cancel(); return; }
            else if (updater.State == AppUpdater.UpdateState.UpdateAvailable) updater.StartDownload();
            else if (updater.State == AppUpdater.UpdateState.Downloaded) updater.ApplyUpdateAndRestart();
            else if (updater.State == AppUpdater.UpdateState.UpToDate || updater.State == AppUpdater.UpdateState.Error) updater.CheckForUpdates(openModalIfNew: true);
            if (Audio != null) Audio.Select();
            return;
        }
        return;
    }

    // 設定画面（音量/画面/キーコンフィグ/アップデート）の入力
    void UpdateSettingsInput(in MenuNav nav)
    {
        if (!string.IsNullOrEmpty(waitingPadAction))
        {
            if (Time.frameCount > waitingPadSkipFrame)
            {
                if (InputSettings.TryDetectPadBindDown(out var detectedPad, out bool cancelPad))
                {
                    InputSettings.SetPadBinding(waitingPadAction, detectedPad);
                    waitingPadAction = null;
                    if (Audio != null) Audio.Select();
                }
                else if (cancelPad)
                {
                    waitingPadAction = null;
                    if (Audio != null) Audio.Cancel();
                }
            }
            return;
        }
        if (!string.IsNullOrEmpty(waitingKeyAction))
        {
            if (InputSettings.GetSinglePadDown(PadBind.B, 0) || nav.padStart)
            {
                waitingKeyAction = null;
                if (Audio != null) Audio.Cancel();
            }
            return;
        }

        if (nav.tabPrev || nav.tabNext)
        {
            settingsTab = (settingsTab + (nav.tabNext ? 1 : -1) + 3) % 3;
            settingsFocus = 0;
            GUIUtility.keyboardControl = 0;
            if (Audio != null) Audio.Select();
            return;
        }
        if (nav.cancel)
        {
            CloseSettings();
            if (Audio != null) Audio.Cancel();
            return;
        }

        if (settingsTab == 0)
        {
            if (nav.moveY != 0) { settingsFocus = (settingsFocus - nav.moveY + 7) % 7; GUIUtility.keyboardControl = 0; if (Audio != null) Audio.Tick(); }
            if (settingsFocus == 0 && nav.moveX != 0) { Audio.SetMasterBgm(Mathf.Clamp01(RaceAudio.MasterBgmVolume + nav.moveX * 0.05f)); if (Audio != null) Audio.Tick(); }
            else if (settingsFocus == 1 && nav.moveX != 0) { Audio.SetMasterSfx(Mathf.Clamp01(RaceAudio.MasterSfxVolume + nav.moveX * 0.05f)); if (Audio != null) Audio.Tick(); }
            else if (settingsFocus == 2 && nav.moveX != 0) { InputSettings.StickDeadzone = Mathf.Clamp(InputSettings.StickDeadzone + nav.moveX * 0.02f, 0.05f, 0.35f); InputSettings.Save(); if (Audio != null) Audio.Tick(); }
            else if (settingsFocus == 3 && (nav.confirm || nav.moveX != 0)) { Screen.fullScreen = !Screen.fullScreen; PlayerPrefs.SetInt("tc_fullscreen", Screen.fullScreen ? 1 : 0); if (Audio != null) Audio.Select(); }
            else if (settingsFocus == 4 && (nav.confirm || nav.moveX != 0)) { CycleResolution(nav.moveX < 0 ? -1 : 1); }
            else if (settingsFocus == 5 && (nav.confirm || nav.moveX != 0)) { ToggleLanguage(); }
            else if (settingsFocus == 6 && nav.confirm) { CloseSettings(); if (Audio != null) Audio.Cancel(); }
        }
        else if (settingsTab == 1)
        {
            if (nav.moveY != 0)
            {
                settingsFocus = (settingsFocus - nav.moveY + 9) % 9;
                if (settingsFocus == 2 || settingsFocus == 3) settingsCol = 0;
                if (Audio != null) Audio.Tick();
            }
            if (nav.moveX != 0 && settingsFocus <= 6 && settingsFocus != 2 && settingsFocus != 3)
            {
                settingsCol = 1 - settingsCol;
                if (Audio != null) Audio.Tick();
            }
            if (nav.confirm)
            {
                if (settingsFocus <= 6)
                {
                    string[] ids = { "accel", "brake", "left", "right", "drift", "item", "rear" };
                    string actId = ids[settingsFocus];
                    if (settingsCol == 0 || actId == "left" || actId == "right")
                    {
                        waitingKeyAction = actId;
                        waitingPadAction = null;
                        waitingKeySkipFrame = Time.frameCount;
                    }
                    else
                    {
                        waitingPadAction = actId;
                        waitingKeyAction = null;
                        waitingPadSkipFrame = Time.frameCount;
                    }
                    if (Audio != null) Audio.Select();
                }
                else if (settingsFocus == 7)
                {
                    InputSettings.ResetDefaults();
                    waitingKeyAction = null;
                    waitingPadAction = null;
                    if (Audio != null) Audio.Select();
                }
                else if (settingsFocus == 8)
                {
                    CloseSettings();
                    if (Audio != null) Audio.Cancel();
                }
            }
        }
        else if (settingsTab == 2)
        {
            if (nav.moveY != 0) { settingsFocus = (settingsFocus - nav.moveY + 4) % 4; if (Audio != null) Audio.Tick(); }
            if (nav.confirm)
            {
                if (settingsFocus == 0)
                {
                    bool isChecking = updater != null && (updater.State == AppUpdater.UpdateState.Checking || updater.State == AppUpdater.UpdateState.Downloading);
                    if (!isChecking && updater != null) updater.CheckForUpdates(openModalIfNew: true);
                    if (Audio != null) Audio.Select();
                }
                else if (settingsFocus == 1)
                {
                    if (updater != null && (updater.State == AppUpdater.UpdateState.UpdateAvailable || updater.State == AppUpdater.UpdateState.Downloaded))
                    {
                        updater.ShowModal = true;
                        updateModalFocus = 0;
                    }
                    else
                    {
                        Application.OpenURL(AppUpdater.ReleasesPageUrl);
                    }
                    if (Audio != null) Audio.Select();
                }
                else if (settingsFocus == 2)
                {
                    if (updater != null) updater.SimulateUpdateAvailableForTesting();
                    if (Audio != null) Audio.Select();
                }
                else if (settingsFocus == 3)
                {
                    CloseSettings();
                    if (Audio != null) Audio.Cancel();
                }
            }
        }
        return;
    }

    // オンラインメニュー・接続中キャンセル・ポーズ切替。入力を消費したら true
    bool UpdateOverlayInput(in MenuNav nav)
    {
        if (state == State.Title && netMenu && !LobbyActive)
        {
            if (Net.Busy)
            {
                if (nav.cancel || nav.confirm) LeaveOnline();
                return true;
            }
            if (nav.cancel)
            {
                netMenu = false;
                GUIUtility.keyboardControl = 0;
                if (Audio != null) Audio.Select();
                return true;
            }
            if (nav.moveY != 0)
            {
                netFocus = (netFocus - nav.moveY + 5) % 5;
                GUIUtility.keyboardControl = 0;
                if (Audio != null) Audio.Tick();
            }
            if (nav.confirm)
            {
                if (netFocus == 0) Net.HostRelay();
                else if (netFocus == 1) Net.JoinRelay(joinCodeInput);
                else if (netFocus == 2) Net.HostDirect();
                else if (netFocus == 3) Net.JoinDirect(ipInput);
                else if (netFocus == 4) { netMenu = false; GUIUtility.keyboardControl = 0; }
                if (Audio != null) Audio.Select();
            }
            return true;
        }
        else if (nav.cancel && Net.Busy && state == State.Title)
        {
            LeaveOnline();
            return true;
        }
        else if ((GameInput.GetKeyDown(KeyCode.Escape) || nav.padStart) && (state == State.Countdown || state == State.Racing))
        {
            if (Online)
            {
                LeaveOnline();
            }
            else
            {
                paused = !paused;
                if (paused) pauseFocus = 0;
                Time.timeScale = paused ? 0f : 1f;
                AudioListener.pause = paused;
                if (paused) GamepadHaptics.StopAll();
                if (Audio != null) Audio.Select();
            }
            return true;
        }
        return false;
    }

    // ポーズ中の入力
    void UpdatePausedInput(in MenuNav nav)
    {
        if (nav.moveY != 0) { pauseFocus = (pauseFocus - nav.moveY + 5) % 5; if (Audio != null) Audio.Tick(); }
        if (GameInput.GetKeyDown(KeyCode.O)) { showSettings = !showSettings; settingsFocus = 0; return; }
        if (GameInput.GetKeyDown(KeyCode.Q) || nav.padBack) Application.Quit();
        if (GameInput.GetKeyDown(KeyCode.R) || nav.padX) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; StartCountdown(); return; }
        if (GameInput.GetKeyDown(KeyCode.T) || nav.padY) { ReturnToTitle(); return; }
        if (InputSettings.GetSinglePadDown(PadBind.B, 0) || InputSettings.GetSinglePadDown(PadBind.B, 1))
        {
            paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (Audio != null) Audio.Select();
            return;
        }
        if (nav.confirm)
        {
            if (pauseFocus == 0) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; if (Audio != null) Audio.Select(); }
            else if (pauseFocus == 1) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; StartCountdown(); }
            else if (pauseFocus == 2) { showSettings = true; settingsFocus = 0; if (Audio != null) Audio.Select(); }
            else if (pauseFocus == 3) { ReturnToTitle(); }
            else if (pauseFocus == 4) { Application.Quit(); }
        }
        return;
    }

    // タイトル / ロビー画面の入力
    void UpdateTitleInput(in MenuNav nav)
    {
        bool editTxt = IsEditingTextField;
        if (LobbyActive)
        {
            if (nav.tabPrev || (!editTxt && GameInput.GetKeyDown(KeyCode.A))) SwitchCourse(-1);
            else if (nav.tabNext || (!editTxt && GameInput.GetKeyDown(KeyCode.D))) SwitchCourse(1);
            if (nav.moveY != 0) { lobbyFocus = (lobbyFocus - nav.moveY + 4) % 4; if (Audio != null) Audio.Tick(); }
            if (nav.moveX != 0)
            {
                if (lobbyFocus == 0) SwitchDriver(0, nav.moveX);
                else if (lobbyFocus == 1) SwitchKart(0, nav.moveX);
                else { lobbyFocus = lobbyFocus == 2 ? 3 : 2; if (Audio != null) Audio.Tick(); }
            }
            if (!editTxt && GameInput.GetKeyDown(KeyCode.W)) SwitchDriver(0, -1);
            else if (!editTxt && GameInput.GetKeyDown(KeyCode.S)) SwitchDriver(0, 1);
            else if (!editTxt && GameInput.GetKeyDown(KeyCode.Q)) SwitchKart(0, -1);
            else if (!editTxt && GameInput.GetKeyDown(KeyCode.E)) SwitchKart(0, 1);

            if (nav.padStart) StartRaceFromTitle();
            else if (nav.confirm)
            {
                if (lobbyFocus == 3) LeaveOnline();
                else if (lobbyFocus == 2 && !string.IsNullOrEmpty(Net.JoinCode) && !Net.IsHost)
                {
                    GUIUtility.systemCopyBuffer = Net.JoinCode;
                    copyNotifyTimer = 2.0f;
                    if (Audio != null) Audio.Select();
                }
                else if (Net.IsHost) StartRaceFromTitle();
            }
            return;
        }

        if (!editTxt && !Net.Busy && (GameInput.GetKeyDown(KeyCode.Escape) || nav.cancel || nav.padBack))
            { ShowQuitModal(); return; }
        if (!editTxt && GameInput.GetKeyDown(KeyCode.P) && !Net.Busy)
            { showSettings = !showSettings; settingsFocus = 0; if (Audio != null) Audio.Select(); return; }
        if (!editTxt && GameInput.GetKeyDown(KeyCode.L) && !Net.Busy)
            { ToggleLanguage(); return; }
        if (!editTxt && GameInput.GetKeyDown(KeyCode.U) && !Net.Busy)
            { if (updater != null) { updater.ShowModal = !updater.ShowModal; updateModalFocus = 0; if (Audio != null) Audio.Select(); } return; }
        if (((!editTxt && GameInput.GetKeyDown(KeyCode.O)) || nav.padX) && !Net.Busy)
            { if (IsTimeAttack) IsTimeAttack = false; netMenu = true; netFocus = 0; if (Audio != null) Audio.Select(); return; }
        if (!editTxt && GameInput.GetKeyDown(KeyCode.M) && !Net.Busy)
        {
            CycleGameMode(1);
            return;
        }
        if (!editTxt && !IsTimeAttack && GameInput.GetKeyDown(KeyCode.C) && !Net.Busy)
        {
            CycleSpeedClass(1);
            return;
        }
        if (!editTxt && GameInput.GetKeyDown(KeyCode.R) && !Net.Busy)
        {
            OpenRecordsModal();
            return;
        }
        if (((!editTxt && GameInput.GetKeyDown(KeyCode.Tab)) || nav.padY) && !Net.Busy)
        {
            if (IsTimeAttack) IsTimeAttack = false;
            SetTwoPlayer(!TwoPlayer);
            return;
        }

        // コントローラーのD-Pad / 左スティックによるタイトルメニュー選択移動
        if (nav.moveY != 0)
        {
            var order = TwoPlayer ? TitleFocusOrder2P : TitleFocusOrder1P;
            int curIdx = System.Array.IndexOf(order, titleFocus);
            if (curIdx < 0) curIdx = 0;
            for (int step = 0; step < order.Length; step++)
            {
                curIdx = (curIdx - nav.moveY + order.Length) % order.Length;
                int cand = order[curIdx];
                if (cand == 13 && !IsCupMode) continue;
                if (cand == 15 && IsTimeAttack) continue;
                titleFocus = cand;
                break;
            }
            if (Audio != null) Audio.Tick();
        }

        if (nav.tabPrev || (!editTxt && GameInput.GetKeyDown(KeyCode.A)))
            SwitchCourse(-1);
        else if (nav.tabNext || (!editTxt && GameInput.GetKeyDown(KeyCode.D)))
            SwitchCourse(1);
        else if (nav.moveX != 0)
        {
            if (titleFocus == 1) SwitchDriver(0, nav.moveX);
            else if (titleFocus == 2) SwitchKart(0, nav.moveX);
            else if (titleFocus == 3 && TwoPlayer) SwitchDriver(1, nav.moveX);
            else if (titleFocus == 4 && TwoPlayer) SwitchKart(1, nav.moveX);
            else if (titleFocus == 5 || titleFocus == 0) SwitchCourse(nav.moveX);
            else if (titleFocus == 6) CycleGameMode(nav.moveX < 0 ? -1 : 1);
            else if (titleFocus == 13 && IsCupMode) SwitchCup(nav.moveX);
            else if (titleFocus == 15 && !IsTimeAttack) CycleSpeedClass(nav.moveX);
            else if (titleFocus == 7 || titleFocus == 8 || titleFocus == 14)
            {
                int[] midRow = { 7, 8, 14 };
                int mi = System.Array.IndexOf(midRow, titleFocus);
                titleFocus = midRow[(mi + (nav.moveX > 0 ? 1 : midRow.Length - 1)) % midRow.Length];
                if (Audio != null) Audio.Tick();
            }
            else if (titleFocus == 9 || titleFocus == 10 || titleFocus == 11 || titleFocus == 12)
            {
                int[] topBar = { 11, 10, 9, 12 };
                int ti = System.Array.IndexOf(topBar, titleFocus);
                titleFocus = topBar[(ti + (nav.moveX > 0 ? 1 : topBar.Length - 1)) % topBar.Length];
                if (Audio != null) Audio.Tick();
            }
        }
        else if (!editTxt && GameInput.GetKeyDown(KeyCode.W))
            SwitchDriver(0, -1);
        else if (!editTxt && GameInput.GetKeyDown(KeyCode.S))
            SwitchDriver(0, 1);
        else if (!editTxt && GameInput.GetKeyDown(KeyCode.Q))
            SwitchKart(0, -1);
        else if (!editTxt && GameInput.GetKeyDown(KeyCode.E))
            SwitchKart(0, 1);
        else if (!editTxt && TwoPlayer && GameInput.GetKeyDown(KeyCode.UpArrow))
            SwitchDriver(1, -1);
        else if (!editTxt && TwoPlayer && GameInput.GetKeyDown(KeyCode.DownArrow))
            SwitchDriver(1, 1);
        else if (!editTxt && TwoPlayer && GameInput.GetKeyDown(KeyCode.LeftArrow))
            SwitchKart(1, -1);
        else if (!editTxt && TwoPlayer && GameInput.GetKeyDown(KeyCode.RightArrow))
            SwitchKart(1, 1);

        if (!titleShotRequested)
        {
            if (nav.padStart || (!editTxt && GameInput.GetKeyDown(KeyCode.Space)) || (screenshotRequested && stateTime > 0.6f) || (Demo && stateTime > 2.5f))
            {
                StartRaceFromTitle();
            }
            else if (nav.confirm)
            {
                if (titleFocus == 6) CycleGameMode(1);
                else if (titleFocus == 13 && IsCupMode) SwitchCup(1);
                else if (titleFocus == 14 && !Net.Busy) OpenRecordsModal();
                else if (titleFocus == 15 && !IsTimeAttack) CycleSpeedClass(1);
                else if (titleFocus == 7) { if (IsTimeAttack) IsTimeAttack = false; SetTwoPlayer(!TwoPlayer); }
                else if (titleFocus == 8 && !Net.Busy) { if (IsTimeAttack) IsTimeAttack = false; cupActive = false; netMenu = true; netFocus = 0; if (Audio != null) Audio.Select(); }
                else if (titleFocus == 9 && !Net.Busy) { showSettings = true; settingsFocus = 0; if (Audio != null) Audio.Select(); }
                else if (titleFocus == 10 && !Net.Busy) { if (updater != null) { updater.ShowModal = true; updateModalFocus = 0; if (Audio != null) Audio.Select(); } }
                else if (titleFocus == 11 && !Net.Busy) { ToggleLanguage(); }
                else if (titleFocus == 12 && !Net.Busy) { ShowQuitModal(); }
                else StartRaceFromTitle();
            }
        }
    }
}
