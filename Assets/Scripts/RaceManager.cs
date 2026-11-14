using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// レース全体の進行・順位・アイテム・カメラ・UI
public partial class RaceManager : MonoBehaviour
{
    [Header("Materials (BuildTool が自動で設定)")]
    public Material roadMaterial;
    public Material curbMaterial;
    public Material wallMaterial;
    public Material grassMaterial;
    public Material chromeMaterial;
    public Material standMaterial;
    public Material kartPaintMaterial;
    public Material tireMaterial;
    public Material visorMaterial;
    public Material skinMaterial;
    public Material trunkMaterial;
    public Material leavesMaterial;
    public Material mountainMaterial;
    public Material snowMaterial;
    public Material boostPadMaterial;
    public Material itemBoxMaterial;
    public Material bananaMaterial;
    public Material missileMaterial;
    public Material glowMaterial;
    public Material skidmarkMaterial;
    public Material headlightMaterial;
    public Material taillightMaterial;
    public Material bannerMaterial;

    [Header("Race")]
    public int totalLaps = 3;
    public int playerGridSlot = 5;

    enum State { Title, Intro, Countdown, Racing, Results }
    State state = State.Title;
    bool paused;

    public Kart Player { get; private set; }
    public List<Kart> Karts { get; } = new List<Kart>();
    public RaceAudio Audio { get; private set; }
    public Light Sun { get; private set; }
    public int SelectedCourse { get; private set; }
    public bool RaceRunning => state == State.Racing || state == State.Results;
    public bool InIntro => state == State.Intro;
    public bool InCountdown => state == State.Countdown;
    public float RaceTime => raceTime;
    // 起動オプション -demo：自動スタートし、プレイヤーも CPU が運転する（動作確認用）
    public bool Demo { get; private set; }
    bool screenshotRequested, screenshotTaken, titleShotRequested, testBoostRequested, autoShotRequested, previewSpectatorsRequested;
    string customShotName;
    float shotDelay = 4.2f;

    Track track;
    readonly List<ItemBox> boxes = new List<ItemBox>();
    readonly List<Banana> bananas = new List<Banana>();
    readonly List<Missile> missiles = new List<Missile>();
    readonly List<Bomb> bombs = new List<Bomb>();
    public List<Banana> Bananas => bananas;
    public List<Bomb> Bombs => bombs;
    public bool IsItemBoxActive(int spotIndex) => spotIndex >= 0 && spotIndex < boxes.Count && boxes[spotIndex] != null && boxes[spotIndex].Active;
    Font font;
    Font fontMain, fontNum, fontMono, fontSys;
    Font fontEnMain, fontJpPop, fontJpRound;

    float stateTime, raceTime, shake, finishAt;
    int lastCountdownBeep;

    // ローカル対戦（2P 画面分割）。インデックス 0 = P1、1 = P2
    public bool TwoPlayer { get; private set; }
    public Kart Player2 { get; private set; }
    public int SelectedDriver { get; private set; } = 0;
    public int SelectedDriver2 { get; private set; } = 1;
    public int SelectedKart { get; private set; } = 0;
    public int SelectedKart2 { get; private set; } = 1;
    int P2Slot => playerGridSlot - 1;
    readonly float[] lapStart = new float[2], bestLap = new float[2], camYaws = new float[2];
    readonly string[] bannerTexts = new string[2];
    readonly Color[] bannerColors = new Color[2];
    readonly float[] bannerTimes = { 99f, 99f };

    Camera cam, cam2;

    // オンライン対戦（Unity Relay / IP 直接接続）
    public NetSession Net { get; private set; }
    public bool Online => Net != null && (Net.Connected || (Net.IsHost && Net.State == NetSession.Phase.Waiting));
    public static readonly int[] HumanSlots = { 5, 4, 3, 2, 1, 0, 6, 7 };
    int mySlot = 5;
    readonly Dictionary<int, int> playerKarts = new Dictionary<int, int>(); // slot -> packed (driverIdx | (kartIdx << 4))
    readonly Dictionary<Unity.Networking.Transport.NetworkConnection, int> clientSlots = new Dictionary<Unity.Networking.Transport.NetworkConnection, int>();
    bool LobbyActive => state == State.Title && Net != null && Net.Busy && playerKarts.Count > 0;
    public int OnlinePlayerCount => Net != null && Net.Busy ? Mathf.Max(1, playerKarts.Count) : 1;
    float firstFinishTime = -1f;
    // レコード管理 & 設定 & 観戦 & 後方視点
    bool newRecordTime, newRecordLap;
    readonly bool[] isLookingBehind = new bool[2];
    int spectateIndex = -1;
    bool showSettings;
    int settingsTab = 0; // 0 = General, 1 = Key & Pad Config, 2 = Updates
    string waitingKeyAction = null;
    string waitingPadAction = null;
    int waitingKeySkipFrame = -1;
    int waitingPadSkipFrame = -1;
    int titleFocus = 1; // 0=Start, 1=Drv1, 2=Krt1, 3=Drv2, 4=Krt2, 5=Course, 6=Mode, 7=1P/2P, 8=Online, 9=Options, 10=Updates, 11=Language, 12=Quit, 13=Cup, 14=Records, 15=SpeedClass
    bool showQuitConfirm;
    int quitModalFocus = 1; // 0=Quit, 1=Cancel
    bool showRecords;
    int recordsFocus = 0;
    int pauseFocus = 0;
    int settingsFocus = 0;
    int settingsCol = 0; // 0=Keyboard, 1=Controller
    int netFocus = 0;
    int lobbyFocus = 0;
    int resultsFocus = 0;
    int updateModalFocus = 0;
    float navRepeatTimer = 0f;
    Vector2Int lastNavDir = Vector2Int.zero;
    string playerName = "Player";
    readonly Dictionary<int, string> playerNames = new Dictionary<int, string>();
    AppUpdater updater;

    // ゲームモード（シングル / カップ戦 / タイムアタック）& スピードクラス
    public enum GameMode { SingleRace = 0, Cup = 1, TimeAttack = 2 }
    public GameMode CurrentMode { get; private set; } = GameMode.SingleRace;
    public bool IsTimeAttack
    {
        get => CurrentMode == GameMode.TimeAttack;
        private set => CurrentMode = value ? GameMode.TimeAttack : (CurrentMode == GameMode.TimeAttack ? GameMode.SingleRace : CurrentMode);
    }
    public bool IsCupMode => CurrentMode == GameMode.Cup;

    public SpeedClass CurrentSpeedClass { get; private set; } = SpeedClass.Cc100;
    public SpeedClass EffectiveSpeedClass => (Online || IsTimeAttack) ? SpeedClass.Cc100 : CurrentSpeedClass;
    public float SpeedClassMultiplier => GetSpeedMultiplier(EffectiveSpeedClass);

    bool cupActive;
    int cupIndex = 0;
    int cupStage = 0;
    readonly int[] cupPoints = new int[8];
    bool cupPointsAwarded;
    bool cupNewTrophy;
    int cupFinalRank;

    // タイムアタック & ゴーストカー
    GhostReplay ghostReplay;
    readonly List<GhostFrame> currentRunGhost = new List<GhostFrame>();
    float nextGhostSampleTime;

    public void CycleGameMode(int delta = 1)
    {
        int next = ((int)CurrentMode + delta + 3) % 3;
        if (TwoPlayer && (GameMode)next == GameMode.TimeAttack)
            next = (next + (delta >= 0 ? 1 : -1) + 3) % 3;
        CurrentMode = (GameMode)next;
        if (CurrentMode == GameMode.TimeAttack && TwoPlayer) SetTwoPlayer(false);
        if (CurrentMode == GameMode.Cup)
        {
            int firstCourse = DefaultCups[cupIndex].courses[0];
            if (SelectedCourse != firstCourse) LoadCourse(firstCourse);
        }
        if ((titleFocus == 13 && !IsCupMode) || (titleFocus == 15 && IsTimeAttack))
            titleFocus = 6;
        if (Audio != null) Audio.Select();
    }

    public void SwitchCup(int delta)
    {
        cupIndex = (cupIndex + delta + DefaultCups.Length) % DefaultCups.Length;
        LoadCourse(DefaultCups[cupIndex].courses[0]);
        if (Audio != null) Audio.Tick();
    }

    public void CycleSpeedClass(int delta = 1)
    {
        int next = ((int)CurrentSpeedClass + delta + 3) % 3;
        CurrentSpeedClass = (SpeedClass)next;
        PlayerPrefs.SetInt("tc_cc", (int)CurrentSpeedClass);
        PlayerPrefs.Save();
        if (Audio != null) Audio.Select();
    }

    // 入力欄が空でも表示・送信には必ず有効な名前を使う
    string DisplayName => string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
    bool IsEditingTextField => GUIUtility.keyboardControl != 0 && ((showSettings && settingsTab == 0) || (state == State.Title && netMenu && !LobbyActive && (Net == null || !Net.Busy)));
    // オンラインのクライアントでは自カートが playerGridSlot ではないため、常に Player から求める
    int MyKartIndex => Karts.IndexOf(Player);
    bool Spectating => !TwoPlayer && spectateIndex >= 0 && spectateIndex < Karts.Count && spectateIndex != MyKartIndex;
    // カメラ・HUD が追う対象（観戦中は観戦相手）
    Kart ViewKart => Spectating ? Karts[spectateIndex] : Player;

    void SetPlayerName(string n)
    {
        if (n == playerName) return;
        playerName = n;
        PlayerPrefs.SetString("tc_player_name", playerName);
        if (Player != null && !TwoPlayer)
            Player.Name = $"{DisplayName} ({Loc.DriverName(SelectedDriver, KartCharacters[SelectedDriver].name)})";
    }

    public void ToggleLanguage()
    {
        Loc.Toggle();
        ApplyLanguageFonts();
        RefreshKartNames();
        if (Audio != null) Audio.Select();
    }

    public void RefreshKartNames()
    {
        if (Online)
        {
            ApplyLobbyKarts();
            return;
        }
        for (int i = 0; i < Karts.Count; i++)
        {
            var k = Karts[i];
            if (k == null) continue;
            if (i == playerGridSlot)
            {
                int drv = Mathf.Clamp(SelectedDriver, 0, KartCharacters.Length - 1);
                k.Name = TwoPlayer ? $"P1 ({Loc.DriverName(drv, KartCharacters[drv].name)})" : $"{DisplayName} ({Loc.DriverName(drv, KartCharacters[drv].name)})";
            }
            else if (TwoPlayer && i == P2Slot)
            {
                int drv = Mathf.Clamp(SelectedDriver2, 0, KartCharacters.Length - 1);
                k.Name = $"P2 ({Loc.DriverName(drv, KartCharacters[drv].name)})";
            }
            else
            {
                int ai = i < playerGridSlot ? i : i - 1;
                int aiDrv = Mathf.Clamp(k.ModelVariant, 0, KartCharacters.Length - 1);
                k.Name = $"{Loc.AiName(ai, AiNames[ai])} ({Loc.DriverName(aiDrv, KartCharacters[aiDrv].name)})";
            }
        }
    }

    void CloseSettings()
    {
        showSettings = false;
        waitingKeyAction = null;
        waitingPadAction = null;
        GUIUtility.keyboardControl = 0;
        PlayerPrefs.Save();
    }

    public static float GetBestTime(int course) => PlayerPrefs.GetFloat($"tc_rec_time_{course}", 0f);
    public static float GetBestLap(int course) => PlayerPrefs.GetFloat($"tc_rec_lap_{course}", 0f);
    public static void SetBestTime(int course, float t) { PlayerPrefs.SetFloat($"tc_rec_time_{course}", t); PlayerPrefs.Save(); }
    public static void SetBestLap(int course, float t) { PlayerPrefs.SetFloat($"tc_rec_lap_{course}", t); PlayerPrefs.Save(); }
    public static int GetCupBestRank(string cupId) => PlayerPrefs.GetInt($"tc_cup_best_{cupId}", 0);
    public static void SetCupBestRank(string cupId, int rank)
    {
        int prev = GetCupBestRank(cupId);
        if (rank > 0 && (prev <= 0 || rank < prev))
        {
            PlayerPrefs.SetInt($"tc_cup_best_{cupId}", rank);
            PlayerPrefs.Save();
        }
    }

    bool netMenu;
    string joinCodeInput = "", ipInput = "127.0.0.1";
    int nextBananaId;
    int nextBombId;
    public static readonly List<ItemDef> ItemDefs = new List<ItemDef>();
    public static ItemDef GetItemDef(ItemType type) => ItemDefs.Find(d => d.Type == type);
    float screenFlashTimer;
    float thunderCooldown;
    public float ScreenFlashTimer => screenFlashTimer;
    GUIStyle sButton, sField, sNum, sMono, sSys;
    readonly List<(int id, NetKartState s)> sendBuf = new List<(int, NetKartState)>();
    Texture2D minimap;
    Texture2D vignetteTex;
    Texture2D speedLineTex;
    Texture2D iconTurbo, iconBanana, iconMissile, iconStar, iconShield, iconBomb, iconThunder;
    Texture2D titleLogoTex;
    Texture2D[] trackBadgeTex = new Texture2D[8];
    Texture2D cardPanelTex;
    Texture2D cardPopTex;
    Texture2D btnRaceTex;
    Texture2D ribbonTrackTex;
    Texture2D ribbonDriverTex;
    Texture2D modalBgTex;
    Texture2D itemSlotTex;
    Texture2D minimapGlassTex;
    Texture2D rankPlateTex, rankPlateGold, rankPlateSilver, rankPlateBronze;
    Texture2D circleDotTex, speedoPlateTex;
    System.Func<Vector3, Vector2> toMap;

    void Awake()
    {
        var args = System.Environment.GetCommandLineArgs();
        Loc.Init(args);
        Demo = args.Contains("-demo") || args.Contains("-screenshot");
        screenshotRequested = args.Contains("-screenshot");
        titleShotRequested = args.Contains("-titleshot");
        testBoostRequested = args.Contains("-testboost");
        autoShotRequested = args.Contains("-autoshot");
        previewSpectatorsRequested = args.Contains("-preview-spectators");
        ParseVerifyArgs(args);
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-shotdelay" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d))
                shotDelay = d;
            if (args[i] == "-shotname")
                customShotName = args[i + 1];
        }
        Application.runInBackground = true; // オンライン対戦中に別ウィンドウへ切り替えても接続が切れないように
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureMaterials();
        LoadUIAssets();
        Audio = gameObject.AddComponent<RaceAudio>();
        ghostReplay = gameObject.AddComponent<GhostReplay>();
        updater = gameObject.AddComponent<AppUpdater>();
        Fx.Init(glowMaterial);
        vignetteTex = TextureGen.Vignette();
        speedLineTex = TextureGen.SpeedLine();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 62f;
        cam.farClipPlane = 1500f;
        cam.nearClipPlane = 0.2f;
        camGo.AddComponent<AudioListener>();

        // 2P 対戦用の右画面カメラ（対戦中のみ有効）
        cam2 = new GameObject("Camera P2").AddComponent<Camera>();
        cam2.fieldOfView = 62f;
        cam2.farClipPlane = 1500f;
        cam2.nearClipPlane = 0.2f;
        cam2.rect = new Rect(0.5f, 0f, 0.5f, 1f);
        cam2.depth = 1;
        cam2.enabled = false;

        SetupPostProcessing();
        playerName = PlayerPrefs.GetString("tc_player_name", "Player");
        CurrentSpeedClass = (SpeedClass)Mathf.Clamp(PlayerPrefs.GetInt("tc_cc", (int)SpeedClass.Cc100), 0, 2);
        if (PlayerPrefs.HasKey("tc_res_w") && PlayerPrefs.HasKey("tc_res_h"))
        {
            int rw = PlayerPrefs.GetInt("tc_res_w");
            int rh = PlayerPrefs.GetInt("tc_res_h");
            bool fs = PlayerPrefs.GetInt("tc_fullscreen", 1) == 1;
            Screen.SetResolution(rw, rh, fs);
        }
        else if (PlayerPrefs.HasKey("tc_fullscreen"))
        {
            Screen.fullScreen = PlayerPrefs.GetInt("tc_fullscreen") == 1;
        }
        else
        {
            Screen.fullScreen = true;
        }

        Sun = new GameObject("Sun").AddComponent<Light>();
        Sun.type = LightType.Directional;
        Sun.intensity = 1.25f;
        Sun.color = new Color(1f, 0.96f, 0.88f);
        Sun.shadows = LightShadows.Soft;
        Sun.shadowStrength = 0.78f;
        Sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        RenderSettings.sun = Sun;
        QualitySettings.shadowDistance = 350f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;

        // コマンドライン引数 -course, -driver, -kart の解析
        int cIdx = 0;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-course" && int.TryParse(args[i + 1], out int parsed))
                cIdx = parsed;
            if (args[i] == "-driver" && int.TryParse(args[i + 1], out int parsedDriver))
                SelectedDriver = Mathf.Clamp(parsedDriver, 0, KartCharacters.Length - 1);
            if (args[i] == "-kart" && int.TryParse(args[i + 1], out int parsedKart))
                SelectedKart = Mathf.Clamp(parsedKart, 0, KartMachines.Length - 1);
        }

        track = new GameObject("Track").AddComponent<Track>();
        track.BuildPath(cIdx);

        // カート初期生成
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject(i == playerGridSlot ? "Player" : "CPU");
            var k = go.AddComponent<Kart>();
            bool isPlayer = i == playerGridSlot;
            int ai = i < playerGridSlot ? i : i - 1;
            GridSlot(i, out int idx, out float lat);
            int dIdx = isPlayer ? SelectedDriver : ((ai + 3) % KartCharacters.Length);
            int mIdx = isPlayer ? SelectedKart : ((ai + 1) % KartMachines.Length);
            k.ModelVariant = dIdx;
            k.KartVariant = mIdx;
            k.DriverColor = KartCharacters[dIdx].color;
            string kName = isPlayer
                ? $"{DisplayName} ({Loc.DriverName(dIdx, KartCharacters[dIdx].name)})"
                : $"{Loc.AiName(ai, AiNames[ai])} ({Loc.DriverName(dIdx, KartCharacters[dIdx].name)})";
            Color kCol = isPlayer ? KartMachines[mIdx].color : AiColors[ai];
            float skill = isPlayer ? 1f : Mathf.Lerp(1.02f, 0.94f, (float)i / 7f);
            k.Init(this, track, kName, kCol, isPlayer, idx, lat, skill);
            GetCombinedStats(dIdx, mIdx, out int spd, out int acc, out int hnd, out int wgt);
            k.ApplyStats(spd, acc, hnd, wgt);
            Karts.Add(k);
            placeOrder.Add(k);
            if (isPlayer) { Player = k; ApplyKartStats(); }
        }
        Player2 = Karts[P2Slot]; // 普段は CPU。2P モード時のみ人間が操作する
        if (args.Contains("-twoplayer")) SetTwoPlayer(true);

        Net = gameObject.AddComponent<NetSession>();
        Net.OnConnected = OnNetClientConnected;
        Net.OnDisconnected = OnNetDisconnected;
        Net.OnClientConnected = OnNetHostClientConnected;
        Net.OnClientDisconnected = OnNetHostClientDisconnected;
        Net.OnClientHello = OnNetClientHello;
        Net.OnWelcome = OnNetWelcome;
        Net.OnLobbySync = OnNetLobbySync;
        Net.OnCourse = c => { if (state == State.Title && c != SelectedCourse) LoadCourse(c); };
        Net.OnStart = c => { if (c != SelectedCourse) LoadCourse(c); StartCountdown(); };
        Net.OnState = (id, s) =>
        {
            if (id >= 0 && id < Karts.Count && Karts[id].IsRemote)
            {
                if (state == State.Countdown) s.Flags &= unchecked((byte)~NetKartState.FDone);
                Karts[id].ApplyNetState(s);
            }
        };
        Net.OnBanana = (id, owner, pos) => { if (owner >= 0 && owner < Karts.Count) SpawnBananaInternal(id, pos, Karts[owner]); };
        Net.OnBananaGone = RemoveBananaById;
        Net.OnMissile = (owner, target) => { if (owner >= 0 && owner < Karts.Count) SpawnMissileInternal(Karts[owner], (target >= 0 && target < Karts.Count) ? Karts[target] : null); };
        Net.OnBomb = (id, owner, pos, vel) => { if (owner >= 0 && owner < Karts.Count) SpawnBombInternal(id, pos, vel, Karts[owner]); };
        Net.OnBombExplode = ExplodeBombById;
        Net.OnThunder = (userSlot) =>
        {
            screenFlashTimer = 0.35f;
            Audio.Explode(1.2f);
            if (userSlot >= 0 && userSlot < Karts.Count)
            {
                var tDef = GetItemDef(ItemType.Thunder);
                string uName = ShortKartName(Karts[userSlot]);
                AddBattleLog(
                    $"{uName} -> ALL [{(tDef != null ? tDef.NameEn : "THUNDER")}]",
                    $"{uName} → 全員 [{(tDef != null ? tDef.NameJp : "サンダー")}]",
                    tDef != null ? tDef.AccentColor : Color.yellow);
            }
            foreach (var k in Karts)
            {
                if (k == null || !k.gameObject.activeSelf) continue;
                if (userSlot >= 0 && userSlot < Karts.Count && k == Karts[userSlot]) continue;
                k.Shock(6.5f);
            }
        };
        // 動作確認用：-nethost / -netjoin <ip> で起動直後に直接接続する
        if (args.Contains("-netmenu")) netMenu = true;
        if (args.Contains("-nethost")) Net.HostDirect();
        if (args.Contains("-relayhost")) Net.HostRelay();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-relayjoin") Net.JoinRelay(args[i + 1]);
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-netjoin") Net.JoinDirect(args[i + 1]);

        LoadCourse(cIdx);
    }

    void Start()
    {
        if (TryStartVerifyCoroutine()) return;
        if (previewSpectatorsRequested) StartCoroutine(CaptureSpectatorPreviews());
        else if (autoShotRequested) StartCoroutine(AutoCaptureScreens());
    }

    void ApplyKartStats()
    {
        GetCombinedStats(SelectedDriver, SelectedKart, out int spd, out int acc, out int hdl, out int wgt);
        Player.ApplyStats(spd, acc, hdl, wgt);
    }

    Kart HumanKart(int p) => p == 0 ? Player : Player2;

    void SetTwoPlayer(bool on)
    {
        if (TwoPlayer == on) return;
        TwoPlayer = on;
        if (!on && (titleFocus == 3 || titleFocus == 4)) titleFocus = 1;
        if (Audio != null) Audio.Select();
        ConfigureHuman(0);
        ConfigureHuman(1);
    }

    // P1 / P2 のカートを人間操作（または P2 のみ CPU）に設定し、見た目と性能を選択キャラ＋カートに合わせる
    void ConfigureHuman(int p)
    {
        var k = HumanKart(p);
        if (p == 0 || TwoPlayer)
        {
            int drv = p == 0 ? SelectedDriver : SelectedDriver2;
            int krt = p == 0 ? SelectedKart : SelectedKart2;
            drv = Mathf.Clamp(drv, 0, KartCharacters.Length - 1);
            krt = Mathf.Clamp(krt, 0, KartMachines.Length - 1);
            var d = KartCharacters[drv];
            var m = KartMachines[krt];
            k.IsPlayer = true;
            k.PlayerIndex = p;
            k.ModelVariant = drv;
            k.KartVariant = krt;
            k.Name = TwoPlayer ? $"P{p + 1} ({Loc.DriverName(drv, d.name)})" : $"{DisplayName} ({Loc.DriverName(drv, d.name)})";
            k.DriverColor = d.color;
            k.Color = m.color;
            GetCombinedStats(drv, krt, out int spd, out int acc, out int hdl, out int wgt);
            k.ApplyStats(spd, acc, hdl, wgt);
        }
        else
        {
            int ai = P2Slot; // P2Slot < playerGridSlot なので CPU 名簿のインデックスと一致
            k.IsPlayer = false;
            k.PlayerIndex = 0;
            int aiDrv = (ai + 3) % KartCharacters.Length;
            int aiKrt = (ai + 1) % KartMachines.Length;
            k.ModelVariant = aiDrv;
            k.KartVariant = aiKrt;
            k.Name = $"{Loc.AiName(ai, AiNames[ai])} ({Loc.DriverName(aiDrv, KartCharacters[aiDrv].name)})";
            k.DriverColor = KartCharacters[aiDrv].color;
            k.Color = AiColors[ai];
            GetCombinedStats(aiDrv, aiKrt, out int spd, out int acc, out int hdl, out int wgt);
            k.ApplyStats(spd, acc, hdl, wgt);
        }
        k.RebuildModel();
        k.Hop();
    }

    bool AllHumansFinished()
    {
        if (Online)
        {
            if (!Player.Finished) return false;
            foreach (var slot in playerKarts.Keys)
            {
                if (slot >= 0 && slot < Karts.Count && !Karts[slot].Finished) return false;
            }
            return true;
        }
        return Player.Finished && (!TwoPlayer || Player2.Finished);
    }

    void ReturnToTitle()
    {
        paused = false;
        cupActive = false;
        cupPointsAwarded = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        state = State.Title;
        stateTime = 0;
        GamepadHaptics.StopAll();
        if (ghostReplay != null) ghostReplay.ClearPlayback();
        RestoreRoles();
        ShowAllKarts();
        LoadCourse(SelectedCourse);
        if (Audio != null) Audio.RestartMusic(SelectedCourse);
    }

    public void LoadCourse(int index)
    {
        SelectedCourse = Mathf.Clamp(index, 0, Track.Courses.Length - 1);
        foreach (var b in boxes) if (b) Destroy(b.gameObject);
        boxes.Clear();
        foreach (var b in bananas) if (b) Destroy(b.gameObject);
        bananas.Clear();
        foreach (var m in missiles) if (m) Destroy(m.gameObject);
        missiles.Clear();
        foreach (var b in bombs) if (b) Destroy(b.gameObject);
        bombs.Clear();

        Audio.SetCourseMusic(SelectedCourse);
        track.BuildPath(SelectedCourse);
        track.BuildVisuals(this, SelectedCourse);
        track.ApplyEnvironment(this, SelectedCourse);
        if (minimap != null) Destroy(minimap);
        minimap = track.MakeMinimap(256, out toMap);

        foreach (var spot in track.ItemBoxSpots)
        {
            var box = new GameObject("ItemBox").AddComponent<ItemBox>();
            box.Init(track.PointAt(spot.index, spot.lateral), itemBoxMaterial, font);
            boxes.Add(box);
        }

        for (int i = 0; i < Karts.Count; i++)
        {
            GridSlot(i, out int idx, out float lat);
            Karts[i].ResetTo(idx, lat);
        }

        if (cam != null) cam.transform.position = track.PointAt(0, 0) + Vector3.up * 20f;
    }

    void SwitchCourse(int delta)
    {
        if (Net.Busy && !Net.IsHost) return; // コースはホストが決める
        int next = (SelectedCourse + delta + Track.Courses.Length) % Track.Courses.Length;
        if (next != SelectedCourse)
        {
            if (Audio != null) Audio.Select();
            LoadCourse(next);
            if (Online) Net.SendCourse(next);
        }
    }

    public void SwitchDriver(int p, int delta)
    {
        int cur = p == 0 ? SelectedDriver : SelectedDriver2;
        int next = (cur + delta + KartCharacters.Length) % KartCharacters.Length;
        if (next == cur) return;
        if (p == 0) SelectedDriver = next; else SelectedDriver2 = next;
        if (Audio != null) Audio.Select();
        if (HumanKart(p) != null) ConfigureHuman(p);
        SyncOnlineSelectionIfNeeded(p);
    }

    public void SwitchKart(int p, int delta)
    {
        int cur = p == 0 ? SelectedKart : SelectedKart2;
        int next = (cur + delta + KartMachines.Length) % KartMachines.Length;
        if (next == cur) return;
        if (p == 0) SelectedKart = next; else SelectedKart2 = next;
        if (Audio != null) Audio.Select();
        if (HumanKart(p) != null) ConfigureHuman(p);
        SyncOnlineSelectionIfNeeded(p);
    }

    void SyncOnlineSelectionIfNeeded(int p)
    {
        if (p == 0 && Online)
        {
            int packed = PackDriverKart(SelectedDriver, SelectedKart);
            if (Net.IsHost)
            {
                playerKarts[mySlot] = packed;
                SyncLobby();
            }
            else
            {
                Net.SendHello(packed, DisplayName);
            }
        }
    }

    void GridSlot(int slot, out int index, out float lateral)
    {
        int row = slot / 2;
        float back = 7f + row * 7f + (slot % 2) * 3.5f;
        index = track.Wrap(-Mathf.CeilToInt(back / 2f));
        lateral = slot % 2 == 0 ? -4f : 4f;
    }

    void ResetRace(bool restartMusic = true)
    {
        foreach (var b in bananas) if (b) Destroy(b.gameObject);
        bananas.Clear();
        foreach (var m in missiles) if (m) Destroy(m.gameObject);
        missiles.Clear();
        foreach (var b in bombs) if (b) Destroy(b.gameObject);
        bombs.Clear();
        battleLog.Clear();

        for (int i = 0; i < Karts.Count; i++)
        {
            GridSlot(i, out int idx, out float lat);
            Karts[i].ResetTo(idx, lat);
        }
        // CPU の強さは毎レースばらつかせる
        var skills = new List<float> { 0.985f, 0.97f, 0.96f, 0.95f, 0.94f, 0.93f, 0.91f };
        foreach (var k in Karts.Where(k => !k.IsPlayer))
        {
            if (skills.Count == 0) { k.SetSkill(0.95f); continue; }
            int j = Random.Range(0, skills.Count);
            k.SetSkill(skills[j]);
            skills.RemoveAt(j);
        }

        raceTime = 0;
        for (int p = 0; p < 2; p++)
        {
            lapStart[p] = 0;
            bestLap[p] = 0;
            camYaws[p] = HumanKart(p).Heading;
            bannerTimes[p] = 99f;
        }
        lastCountdownBeep = -1;
        if (restartMusic && Audio != null) Audio.RestartMusic(SelectedCourse);
    }

    // ───────────────────────── Loop ─────────────────────────

    void Update()
    {
        HandleCaptureRequests();

        var nav = new MenuNav();
        PollMenuNav(out nav.moveX, out nav.moveY, out nav.confirm, out nav.cancel, out nav.tabPrev, out nav.tabNext);
        nav.padStart = InputSettings.GetSinglePadDown(PadBind.Start, 0) || InputSettings.GetSinglePadDown(PadBind.Start, 1);
        nav.padBack = InputSettings.GetSinglePadDown(PadBind.Back, 0) || InputSettings.GetSinglePadDown(PadBind.Back, 1);
        nav.padX = InputSettings.GetSinglePadDown(PadBind.X, 0) || InputSettings.GetSinglePadDown(PadBind.X, 1);
        nav.padY = InputSettings.GetSinglePadDown(PadBind.Y, 0) || InputSettings.GetSinglePadDown(PadBind.Y, 1);

        GamepadHaptics.Update(Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        if (showQuitConfirm) { UpdateQuitModalInput(nav); return; }
        if (updater != null && updater.ShowModal) { UpdateModalInput(nav); return; }
        if (showSettings) { UpdateSettingsInput(nav); return; }
        if (showRecords) { UpdateRecordsInput(nav); return; }
        if (UpdateOverlayInput(nav)) return;
        if (paused) { UpdatePausedInput(nav); return; }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        stateTime += dt;
        bannerTimes[0] += dt;
        bannerTimes[1] += dt;
        if (screenFlashTimer > 0) screenFlashTimer -= dt;
        if (thunderCooldown > 0) thunderCooldown -= dt;

        switch (state)
        {
            case State.Title: UpdateTitleInput(nav); break;
            case State.Intro: UpdateIntro(nav, dt); break;
            case State.Countdown: UpdateCountdown(); break;
            case State.Racing: UpdateRacing(dt); break;
            case State.Results: UpdateResults(nav, dt); break;
        }

        bool canDrive = SimulateKarts(dt);
        SendNetStates();
        UpdateEngineAudio(dt, canDrive);
    }

    void UpdateCountdown()
    {
        int beep = Mathf.FloorToInt(stateTime - 1f);
        if (beep != lastCountdownBeep && beep >= 0 && beep <= 3)
        {
            lastCountdownBeep = beep;
            if (track != null) track.UpdateStartSignals(beep);
            if (beep < 3) Audio.Beep(beep);
            else
            {
                Audio.Go();
                if (Audio != null) Audio.SetMusicVolume(0.30f);
                state = State.Racing;
                stateTime = 0;
                foreach (var k in Karts) k.OnGo();
            }
        }
    }

    void UpdateRacing(float dt)
    {
        raceTime += dt;
        for (int i = battleLog.Count - 1; i >= 0; i--)
        {
            if (Time.time >= battleLog[i].ExpireAt) battleLog.RemoveAt(i);
        }
        if (Audio != null)
        {
            float minWarnDist = 999f;
            if (Player != null && !Player.Finished && IsMissileApproaching(Player, out float d1))
                minWarnDist = Mathf.Min(minWarnDist, d1);
            if (TwoPlayer && Player2 != null && !Player2.Finished && IsMissileApproaching(Player2, out float d2))
                minWarnDist = Mathf.Min(minWarnDist, d2);
            if (minWarnDist < 45f)
                Audio.MissileWarning(minWarnDist);
        }
        if (stateTime > 2.5f && stateTime - dt <= 2.5f && track != null)
        {
            track.UpdateStartSignals(-1); // GO演出終了後に消灯
        }
        if (IsTimeAttack && ghostReplay != null)
        {
            ghostReplay.TickPlayback(raceTime);
            if (raceTime >= nextGhostSampleTime && Player != null && !Player.Finished)
            {
                currentRunGhost.Add(new GhostFrame { t = raceTime, pos = Player.transform.position, heading = Player.Heading, dist = Player.RaceDistance });
                nextGhostSampleTime = raceTime + 0.05f;
            }
        }
        if (Online)
        {
            if (firstFinishTime < 0f)
            {
                if (Player.Finished || playerKarts.Keys.Any(s => s >= 0 && s < Karts.Count && Karts[s].Finished))
                    firstFinishTime = Time.time;
            }
            else if (Time.time - firstFinishTime > 25f)
            {
                state = State.Results;
                resultsFocus = 0;
                finishAt = Time.time;
                AwardCupPointsIfNeeded();
                GamepadHaptics.StopAll();
                if (Audio != null)
                {
                    Audio.SetMusicVolume(0.15f);
                    Audio.SetMusicTempo(1f);
                }
            }
        }
        if (AllHumansFinished())
        {
            state = State.Results;
            resultsFocus = 0;
            finishAt = Time.time;
            AwardCupPointsIfNeeded();
            GamepadHaptics.StopAll();
            if (Audio != null)
            {
                Audio.SetMusicVolume(0.15f);
                Audio.SetMusicTempo(1f);
            }
        }
    }

    void UpdateResults(in MenuNav nav, float dt)
    {
        raceTime += dt;
        bool canRestart = !Online || Net.IsHost;
        if (!canRestart) resultsFocus = 1;
        if (Time.time > finishAt + 2.5f)
        {
            if (canRestart && (nav.moveX != 0 || nav.moveY != 0))
            {
                resultsFocus = 1 - resultsFocus;
                if (Audio != null) Audio.Tick();
            }
            if (nav.confirm || (canRestart && nav.padStart))
            {
                if (canRestart && (resultsFocus == 0 || nav.padStart))
                {
                    if (cupActive && cupStage + 1 < DefaultCups[cupIndex].courses.Length)
                        AdvanceCupStage();
                    else
                        StartRaceFromTitle();
                }
                else
                {
                    if (Online) LeaveOnline();
                    else ReturnToTitle();
                }
            }
            else if (nav.cancel || GameInput.GetKeyDown(KeyCode.T) || GameInput.GetKeyDown(KeyCode.Backspace) || nav.padY)
            {
                if (Online) LeaveOnline();
                else ReturnToTitle();
            }
        }
    }

    // 全カートの更新・衝突・アイテム判定・順位。カートを走行可能として更新したかを返す
    bool SimulateKarts(float dt)
    {
        bool canDrive = RaceRunning;
        foreach (var k in Karts) k.Tick(dt, canDrive);

        if (canDrive)
        {
            KartCollisions();
            CheckItemBoxes();
            CheckBananas();
        }
        UpdatePlaces();
        return canDrive;
    }

    void SendNetStates()
    {
        if (Online && state != State.Title && Net.SendDue)
        {
            sendBuf.Clear();
            for (int i = 0; i < Karts.Count; i++)
                if (!Karts[i].IsRemote && (Net.IsHost || Karts[i] == Player)) sendBuf.Add((i, Karts[i].MakeNetState()));
            Net.SendStates(sendBuf);
        }
    }

    void UpdateEngineAudio(float dt, bool canDrive)
    {
        float engSpeed = Mathf.Abs(Player.Speed);
        float engThrottle = Mathf.Clamp01(Player.CurrentThrottle);
        bool anyDrift = Player.Drifting;
        int maxDriftLv = Player.DriftLevel;
        if (TwoPlayer && Player2 != null)
        {
            engSpeed = Mathf.Max(engSpeed, Mathf.Abs(Player2.Speed));
            engThrottle = Mathf.Max(engThrottle, Mathf.Clamp01(Player2.CurrentThrottle));
            anyDrift = anyDrift || Player2.Drifting;
            maxDriftLv = Mathf.Max(maxDriftLv, Player2.DriftLevel);
        }
        float refMax = Player != null ? Player.EffectiveMaxSpeed : Kart.MaxSpeed;
        Audio.SetEngine(engSpeed / refMax, engThrottle, state != State.Title, anyDrift && canDrive, maxDriftLv);
        shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
    }

    // オンライン中はホストだけがレースを開始でき、クライアントには開始の合図が届く
    void StartRaceFromTitle()
    {
        if (Net.Busy && !Online) return;
        if (Online)
        {
            if (!Net.IsHost) return;
            cupActive = false;
            Net.SendStart(SelectedCourse);
            StartCountdown(); // オンライン対戦時は同期ズレを防ぐため直接カウントダウンへ
            return;
        }
        if (CurrentMode == GameMode.Cup)
        {
            cupActive = true;
            cupStage = 0;
            System.Array.Clear(cupPoints, 0, cupPoints.Length);
            cupPointsAwarded = false;
            cupNewTrophy = false;
            cupFinalRank = 0;
            int firstCourse = DefaultCups[cupIndex].courses[0];
            if (SelectedCourse != firstCourse) LoadCourse(firstCourse);
        }
        else
        {
            cupActive = false;
            cupPointsAwarded = false;
        }
        StartCourseIntro();
    }

    void AwardCupPointsIfNeeded()
    {
        if (!cupActive || cupPointsAwarded) return;
        cupPointsAwarded = true;
        for (int i = 0; i < Karts.Count && i < cupPoints.Length; i++)
        {
            cupPoints[i] += GetCupPointsForPlace(Karts[i].Place);
        }
        var cup = DefaultCups[cupIndex];
        if (cupStage + 1 >= cup.courses.Length)
        {
            var indices = new List<int>(Karts.Count);
            for (int i = 0; i < Karts.Count; i++)
            {
                if (Karts[i] != null && Karts[i].gameObject.activeSelf) indices.Add(i);
            }
            indices.Sort((a, b) =>
            {
                int cmp = cupPoints[b].CompareTo(cupPoints[a]);
                return cmp != 0 ? cmp : Karts[a].Place.CompareTo(Karts[b].Place);
            });
            int r1 = indices.IndexOf(playerGridSlot) + 1;
            int r2 = TwoPlayer ? indices.IndexOf(P2Slot) + 1 : 0;
            cupFinalRank = (TwoPlayer && r2 > 0 && (r1 <= 0 || r2 < r1)) ? r2 : r1;
            if (!Demo && cupFinalRank > 0)
            {
                int prev = GetCupBestRank(cup.id);
                if (prev <= 0 || cupFinalRank < prev)
                {
                    cupNewTrophy = true;
                    SetCupBestRank(cup.id, cupFinalRank);
                }
            }
        }
    }

    void AdvanceCupStage()
    {
        if (!cupActive) return;
        var cup = DefaultCups[cupIndex];
        if (cupStage + 1 >= cup.courses.Length) return;
        cupStage++;
        cupPointsAwarded = false;
        LoadCourse(cup.courses[cupStage]);
        StartCourseIntro();
    }

    void StartCourseIntro()
    {
        netMenu = false;
        if (!Online) RestoreRoles();
        if (IsTimeAttack && !Online)
        {
            currentRunGhost.Clear();
            nextGhostSampleTime = 0f;
            if (ghostReplay != null) ghostReplay.InitPlayback(SelectedCourse, glowMaterial, track);
            for (int i = 0; i < Karts.Count; i++)
            {
                if (Karts[i] != Player) Karts[i].gameObject.SetActive(false);
            }
            if (Player != null) Player.Item = ItemType.Turbo; // タイムアタック開幕ターボ支給
        }
        else
        {
            if (ghostReplay != null) ghostReplay.ClearPlayback();
            ShowAllKarts();
        }
        ResetRace(restartMusic: true);
        if (Audio != null) Audio.SetMusicVolume(0.22f); // コース紹介中は上品で落ち着いた音量
        if (track != null) track.UpdateStartSignals(-1);
        firstFinishTime = -1f;
        newRecordTime = false;
        newRecordLap = false;
        spectateIndex = -1;
        state = State.Intro;
        stateTime = 0f;
        InitCourseIntro();
    }

    void StartCountdown(bool preserveMusic = false)
    {
        // 接続メニューが残ると Update が早期 return し、カウントダウンが進まない。
        netMenu = false;
        if (!Online) RestoreRoles();
        if (IsTimeAttack && !Online)
        {
            currentRunGhost.Clear();
            nextGhostSampleTime = 0f;
            if (ghostReplay != null) ghostReplay.InitPlayback(SelectedCourse, glowMaterial, track);
            for (int i = 0; i < Karts.Count; i++)
            {
                if (Karts[i] != Player) Karts[i].gameObject.SetActive(false);
            }
            if (Player != null) Player.Item = ItemType.Turbo; // タイムアタック開幕ターボ支給
        }
        else
        {
            if (ghostReplay != null) ghostReplay.ClearPlayback();
            ShowAllKarts();
        }
        ResetRace(restartMusic: !preserveMusic);
        if (Audio != null) Audio.SetMusicVolume(0.27f); // カウントダウン音量へステップアップ
        if (track != null) track.UpdateStartSignals(-1);
        firstFinishTime = -1f;
        newRecordTime = false;
        newRecordLap = false;
        spectateIndex = -1;
        state = State.Countdown;
        stateTime = 0;
    }

    public void OnLapCompleted(Kart k)
    {
        if (k.Lap > totalLaps && !k.Finished)
        {
            k.Finished = true;
            k.FinishTime = raceTime;
            if (k.IsPlayer)
            {
                Audio.Finish();
                Audio.SetMusicTempo(1f);
                Banner(Loc.T("FINISH!", "ゴール！"), Color.white, k.PlayerIndex);
                RecordLap(k.PlayerIndex);

                if (!Demo)
                {
                    float prevBest = GetBestTime(SelectedCourse);
                    if (prevBest <= 0 || k.FinishTime < prevBest)
                    {
                        SetBestTime(SelectedCourse, k.FinishTime);
                        newRecordTime = true;
                        Banner(Loc.T("NEW COURSE RECORD!", "コース新記録！"), new Color(1f, 0.85f, 0.2f), k.PlayerIndex);
                    }
                    if (k.PlayerIndex == 0 && IsTimeAttack && currentRunGhost.Count > 1)
                    {
                        currentRunGhost.Add(new GhostFrame { t = k.FinishTime, pos = k.transform.position, heading = k.Heading, dist = k.RaceDistance });
                        var oldGhost = GhostReplay.LoadGhost(SelectedCourse);
                        if (oldGhost == null || oldGhost.frames == null || oldGhost.frames.Count < 2 || k.FinishTime < oldGhost.finishTime)
                        {
                            var gd = new GhostData { finishTime = k.FinishTime, frames = new List<GhostFrame>(currentRunGhost) };
                            GhostReplay.SaveGhost(SelectedCourse, gd);
                        }
                    }
                }
            }
            if (state != State.Results && AllHumansFinished())
            {
                state = State.Results;
                resultsFocus = 0;
                finishAt = Time.time;
                AwardCupPointsIfNeeded();
                GamepadHaptics.StopAll();
                if (Audio != null)
                {
                    Audio.SetMusicVolume(0.15f);
                    Audio.SetMusicTempo(1f);
                }
            }
            return;
        }
        if (!k.IsPlayer || k.Lap < 2) return;

        RecordLap(k.PlayerIndex);
        if (k.Lap == totalLaps)
        {
            Banner(Loc.T("FINAL LAP!", "ファイナルラップ！"), new Color(1f, 0.3f, 0.3f), k.PlayerIndex);
            Audio.FinalLap();
            Audio.SetMusicTempo(1.1f);
        }
        else
        {
            Banner(Loc.T("LAP " + k.Lap + "/" + totalLaps, $"ラップ {k.Lap}/{totalLaps}"), Color.white, k.PlayerIndex);
            Audio.Lap();
        }
    }

    void RecordLap(int p)
    {
        float lap = raceTime - lapStart[p];
        lapStart[p] = raceTime;
        if (bestLap[p] <= 0 || lap < bestLap[p])
        {
            bestLap[p] = lap;
            if (!Demo)
            {
                float prevBestLap = GetBestLap(SelectedCourse);
                if (prevBestLap <= 0 || lap < prevBestLap)
                {
                    SetBestLap(SelectedCourse, lap);
                    newRecordLap = true;
                    Banner(Loc.T("NEW LAP RECORD!", "ベストラップ更新！"), new Color(1f, 0.85f, 0.2f), p);
                }
            }
        }
    }

    public void Banner(string text, Color color, int player = 0)
    {
        bannerTexts[player] = text;
        bannerColors[player] = color;
        bannerTimes[player] = 0;
    }

    public void Shake(float s) => shake = Mathf.Max(shake, s);

    void OnDestroy()
    {
        GamepadHaptics.StopAll();
        Fx.Shutdown();
        if (minimap != null) Destroy(minimap);
        TextureGen.ClearCache();
    }

    void OnApplicationQuit()
    {
        GamepadHaptics.StopAll();
    }
}
