using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public struct KartCharacterDef
{
    public string name;
    public string driver;
    public string trait;
    public Color color;
    public int speed;     // 1〜8
    public int accel;     // 1〜8
    public int handling;  // 1〜8
    public int weight;    // 1〜8
}

[System.Serializable]
public struct KartMachineDef
{
    public string name;
    public string code;
    public string trait;
    public Color color;
    public int speed;     // 1〜8
    public int accel;     // 1〜8
    public int handling;  // 1〜8
    public int weight;    // 1〜8
}

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
    int titleFocus = 1; // 0=Start, 1=Drv1, 2=Krt1, 3=Drv2, 4=Krt2, 5=Course, 6=Mode, 7=1P/2P, 8=Online, 9=Options, 10=Updates, 11=Language, 12=Quit
    bool showQuitConfirm;
    int quitModalFocus = 1; // 0=Quit, 1=Cancel
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

    // タイムアタック & ゴーストカー
    public bool IsTimeAttack { get; private set; }
    GhostReplay ghostReplay;
    readonly List<GhostFrame> currentRunGhost = new List<GhostFrame>();
    float nextGhostSampleTime;

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

    // ───────────────────────── 全10種類のドライバー（キャラクター）定義 ─────────────────────────
    public static readonly KartCharacterDef[] DefaultKartCharacters =
    {
        new KartCharacterDef {
            name = "OOBI", driver = "Alien Ace", trait = "Balanced / All-Rounder", color = new Color(0.95f, 0.25f, 0.25f),
            speed = 5, accel = 5, handling = 5, weight = 5
        },
        new KartCharacterDef {
            name = "OODI", driver = "Pink Dash", trait = "High Drift & Turbo Boost", color = new Color(0.98f, 0.38f, 0.78f),
            speed = 4, accel = 7, handling = 6, weight = 3
        },
        new KartCharacterDef {
            name = "OOLI", driver = "Aero Sonic", trait = "Maximum Top Speed", color = new Color(0.18f, 0.65f, 1.0f),
            speed = 7, accel = 4, handling = 4, weight = 6
        },
        new KartCharacterDef {
            name = "OOPI", driver = "Racer Swift", trait = "Sharp Handling & Turn", color = new Color(1.0f, 0.62f, 0.12f),
            speed = 4, accel = 6, handling = 7, weight = 3
        },
        new KartCharacterDef {
            name = "OOZI", driver = "Cool Titan", trait = "Heavy Weight & High Grip", color = new Color(0.22f, 0.88f, 0.38f),
            speed = 6, accel = 3, handling = 4, weight = 7
        },
        new KartCharacterDef {
            name = "PANDA", driver = "Bamboo Boss", trait = "Heavyweight Brawler", color = new Color(0.18f, 0.80f, 0.62f),
            speed = 6, accel = 4, handling = 4, weight = 7
        },
        new KartCharacterDef {
            name = "PENGUIN", driver = "Ice Slider", trait = "Smooth Ice Drifter", color = new Color(0.22f, 0.78f, 0.98f),
            speed = 4, accel = 6, handling = 7, weight = 4
        },
        new KartCharacterDef {
            name = "FOX", driver = "Nitro Kitsune", trait = "Agile Speedster", color = new Color(1.0f, 0.48f, 0.14f),
            speed = 6, accel = 6, handling = 6, weight = 3
        },
        new KartCharacterDef {
            name = "BUNNY", driver = "Hopper Star", trait = "Featherweight Dash", color = new Color(0.74f, 0.42f, 0.98f),
            speed = 3, accel = 7, handling = 7, weight = 3
        },
        new KartCharacterDef {
            name = "LION", driver = "Golden King", trait = "Royal Top Speed", color = new Color(1.0f, 0.76f, 0.12f),
            speed = 7, accel = 4, handling = 4, weight = 7
        },
    };

    // ───────────────────────── 全8種類のカートマシン（車体）定義 ─────────────────────────
    public static readonly KartMachineDef[] DefaultKartMachines =
    {
        new KartMachineDef {
            name = "STANDARD", code = "STD-01", trait = "Balanced All-Rounder", color = new Color(0.95f, 0.25f, 0.25f),
            speed = 5, accel = 5, handling = 5, weight = 5
        },
        new KartMachineDef {
            name = "PIPE BUGGY", code = "BUG-02", trait = "Light & Quick Accel", color = new Color(1.0f, 0.55f, 0.12f),
            speed = 4, accel = 7, handling = 6, weight = 3
        },
        new KartMachineDef {
            name = "MACH FORMULA", code = "GP-03", trait = "High-Speed Aero Wing", color = new Color(0.15f, 0.55f, 1.0f),
            speed = 7, accel = 4, handling = 5, weight = 5
        },
        new KartMachineDef {
            name = "MONSTER", code = "MON-04", trait = "Heavy Off-Road Tire", color = new Color(0.20f, 0.82f, 0.35f),
            speed = 6, accel = 3, handling = 3, weight = 7
        },
        new KartMachineDef {
            name = "CYBER GT", code = "CYB-05", trait = "Neon Drift Machine", color = new Color(0.88f, 0.22f, 0.95f),
            speed = 6, accel = 6, handling = 6, weight = 4
        },
        new KartMachineDef {
            name = "ROADSTER", code = "RET-06", trait = "Classic Smooth Turn", color = new Color(0.96f, 0.76f, 0.18f),
            speed = 5, accel = 6, handling = 7, weight = 4
        },
        new KartMachineDef {
            name = "MUSCLE V8", code = "MUS-07", trait = "Supercharged Power", color = new Color(0.48f, 0.25f, 0.95f),
            speed = 7, accel = 4, handling = 3, weight = 7
        },
        new KartMachineDef {
            name = "JET POD", code = "JET-08", trait = "Twin-Turbine Rocket", color = new Color(0.12f, 0.85f, 0.95f),
            speed = 7, accel = 6, handling = 4, weight = 4
        },
    };

    static KartCharacterDef[] runtimeKartCharacters;
    public static KartCharacterDef[] KartCharacters
    {
        get
        {
            if (runtimeKartCharacters == null)
            {
                var loaded = Resources.LoadAll<KartData>("Data/Karts");
                if (loaded != null && loaded.Length >= DefaultKartCharacters.Length)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runtimeKartCharacters = loaded.Select(k => k.ToDef()).ToArray();
                }
                else
                {
                    runtimeKartCharacters = DefaultKartCharacters;
                }
            }
            return runtimeKartCharacters;
        }
    }

    static KartMachineDef[] runtimeKartMachines;
    public static KartMachineDef[] KartMachines
    {
        get
        {
            if (runtimeKartMachines == null)
            {
                var loaded = Resources.LoadAll<KartMachineData>("Data/Machines");
                if (loaded != null && loaded.Length >= DefaultKartMachines.Length)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runtimeKartMachines = loaded.Select(m => m.ToDef()).ToArray();
                }
                else
                {
                    runtimeKartMachines = DefaultKartMachines;
                }
            }
            return runtimeKartMachines;
        }
    }

    public static void GetCombinedStats(int driverIdx, int kartIdx, out int speed, out int accel, out int handling, out int weight)
    {
        var d = KartCharacters[Mathf.Clamp(driverIdx, 0, KartCharacters.Length - 1)];
        var m = KartMachines[Mathf.Clamp(kartIdx, 0, KartMachines.Length - 1)];
        speed    = Mathf.Clamp(d.speed + m.speed - 5, 1, 8);
        accel    = Mathf.Clamp(d.accel + m.accel - 5, 1, 8);
        handling = Mathf.Clamp(d.handling + m.handling - 5, 1, 8);
        weight   = Mathf.Clamp(d.weight + m.weight - 5, 1, 8);
    }

    public static int PackDriverKart(int driverIdx, int kartIdx)
    {
        int d = Mathf.Clamp(driverIdx, 0, 15) & 0x0F;
        int k = Mathf.Clamp(kartIdx, 0, 15) & 0x0F;
        return d | (k << 4);
    }

    public static void UnpackDriverKart(int packed, out int driverIdx, out int kartIdx)
    {
        driverIdx = Mathf.Clamp(packed & 0x0F, 0, KartCharacters.Length - 1);
        kartIdx = Mathf.Clamp((packed >> 4) & 0x0F, 0, KartMachines.Length - 1);
    }

    float stickNavTimer = 0f;

    static readonly string[] AiNames = { "Blaze", "Nova", "Rex", "Kiki", "Bolt", "Mochi", "Taro" };
    static readonly Color[] AiColors =
    {
        new Color(0.15f, 0.4f, 1f), new Color(0.15f, 0.8f, 0.25f), new Color(1f, 0.85f, 0.1f), new Color(0.6f, 0.25f, 0.95f),
        new Color(1f, 0.5f, 0.05f), new Color(1f, 0.45f, 0.75f), new Color(0.1f, 0.85f, 0.9f),
    };

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
        if (PlayerPrefs.HasKey("tc_res_w") && PlayerPrefs.HasKey("tc_res_h"))
        {
            int rw = PlayerPrefs.GetInt("tc_res_w");
            int rh = PlayerPrefs.GetInt("tc_res_h");
            bool fs = PlayerPrefs.GetInt("tc_fullscreen", 0) == 1;
            Screen.SetResolution(rw, rh, fs);
        }
        else if (PlayerPrefs.HasKey("tc_fullscreen"))
        {
            Screen.fullScreen = PlayerPrefs.GetInt("tc_fullscreen") == 1;
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

    System.Collections.IEnumerator CaptureSpectatorPreviews()
    {
        yield return new WaitForSeconds(0.6f);
        state = State.Countdown; // UIオーバーレイを抑制
        // スタジオ風カメラ配置
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.22f, 0.26f, 0.32f);

        // プレビュー対象の親
        var studio = new GameObject("StudioRoot");
        studio.transform.position = new Vector3(0, 1000f, 0); // コースから離れた上空スタジオ
        Vector3 center = studio.transform.position;

        // スタジオ専用ライト
        var studioKey = new GameObject("StudioKeyLight").AddComponent<Light>();
        studioKey.type = LightType.Directional;
        studioKey.transform.SetParent(studio.transform, false);
        studioKey.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
        studioKey.intensity = 1.35f;
        studioKey.color = new Color(1f, 0.98f, 0.95f);

        var studioFill = new GameObject("StudioFillLight").AddComponent<Light>();
        studioFill.type = LightType.Directional;
        studioFill.transform.SetParent(studio.transform, false);
        studioFill.transform.rotation = Quaternion.Euler(-20f, 145f, 0f);
        studioFill.intensity = 0.55f;
        studioFill.color = new Color(0.75f, 0.88f, 1f);

        // 床ディスク（スタジオの台座）
        var stage = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stage.transform.SetParent(studio.transform, false);
        stage.transform.localPosition = new Vector3(0, -0.05f, 0);
        stage.transform.localScale = new Vector3(4.5f, 0.05f, 4.5f);
        var stageRend = stage.GetComponent<Renderer>();
        stageRend.sharedMaterial = wallMaterial;

        // 1. 人間サポーターキャラクター（6種類の帽子・応援スタイル）
        var spectatorPalette = SpectatorPalette.Create(this, new List<Material>());
        string[] hatNames = new[] { "cap", "visor", "knit", "bear_ears", "headphones", "sunglasses" };

        for (int h = 0; h < 6; h++)
        {
            var fanObj = SpectatorFan.CreateFan(
                studio.transform,
                center,
                Quaternion.identity,
                new System.Random(h * 888 + 19),
                this,
                spectatorPalette
            );

            // 前面全身ショット
            cam.transform.position = center + new Vector3(1.6f, 2.3f, 3.2f);
            cam.transform.LookAt(center + new Vector3(0, 1.4f, 0));
            yield return new WaitForSeconds(0.25f);
            ScreenCapture.CaptureScreenshot($"preview_fan_{h}_{hatNames[h]}_front.png");
            yield return new WaitForSeconds(0.15f);

            // バストアップ・クローズアップ
            cam.transform.position = center + new Vector3(0.5f, 1.9f, 2.0f);
            cam.transform.LookAt(center + new Vector3(0, 1.6f, 0));
            yield return new WaitForSeconds(0.2f);
            ScreenCapture.CaptureScreenshot($"preview_fan_{h}_{hatNames[h]}_closeup.png");
            yield return new WaitForSeconds(0.15f);

            Destroy(fanObj);
        }

        // 2. 動物観客たち（全12種）
        string animalTexPath = Application.dataPath + "/Resources/Animals/Textures/colormap.png";
        var colormapTex = RaceManager.LoadTexture("Animals/Textures/colormap", animalTexPath);
        var animalMat = new Material(wallMaterial);
        if (colormapTex != null) animalMat.mainTexture = colormapTex;
        animalMat.color = Color.white;
        animalMat.SetColor("_BaseColor", Color.white);
        animalMat.SetFloat("_Smoothness", 0.15f);

        string[] animalNames = new[]
        {
            "bunny", "penguin", "polar", "deer", "fox", "lion", "cow", "cat", "dog", "crab", "chick", "parrot"
        };

        foreach (var name in animalNames)
        {
            var prefab = Resources.Load<GameObject>($"Animals/animal-{name}");
            if (prefab == null) continue;

            var animalGo = Instantiate(prefab, studio.transform);
            animalGo.transform.position = center + Vector3.up * 0.1f;
            animalGo.transform.rotation = Quaternion.Euler(0, 35f, 0); // 正面斜め向き
            animalGo.transform.localScale = Vector3.one * 1.5f;

            foreach (var r in animalGo.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = animalMat;
            }

            cam.transform.position = center + new Vector3(2.5f, 2.2f, 3.2f);
            cam.transform.LookAt(center + new Vector3(0, 0.8f, 0));
            yield return new WaitForSeconds(0.2f);
            ScreenCapture.CaptureScreenshot($"preview_animal_{name}.png");
            yield return new WaitForSeconds(0.15f);

            Destroy(animalGo);
        }

        Destroy(studio);
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    System.Collections.IEnumerator AutoCaptureScreens()
    {
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot("shot_01_title.png");
        yield return new WaitForSeconds(0.4f);

        // カスタムキャラ＆カート組み合わせのタイトル画面確認 (例: PANDA + MACH FORMULA)
        SelectedDriver = 5;
        SelectedKart = 2;
        ConfigureHuman(0);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot("shot_01b_title_custom.png");
        yield return new WaitForSeconds(0.3f);

        // 2P分割時のタイトル画面確認
        SelectedDriver2 = 7; // FOX
        SelectedKart2 = 4;   // CYBER GT
        SetTwoPlayer(true);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot("shot_01c_title_2p.png");
        yield return new WaitForSeconds(0.3f);
        SetTwoPlayer(false);

        showSettings = true;
        settingsTab = 0;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_02_settings_general.png");
        yield return new WaitForSeconds(0.4f);

        settingsTab = 1;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_03_settings_keys.png");
        yield return new WaitForSeconds(0.4f);

        settingsTab = 2;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_03b_settings_updates.png");
        yield return new WaitForSeconds(0.4f);
        CloseSettings();
        yield return new WaitForSeconds(0.4f);

        if (updater != null) updater.SimulateUpdateAvailableForTesting();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot("shot_03c_update_modal.png");
        yield return new WaitForSeconds(0.4f);
        if (updater != null) updater.ShowModal = false;
        yield return new WaitForSeconds(0.4f);

        StartRaceFromTitle();
        yield return new WaitForSeconds(1.8f);
        ScreenCapture.CaptureScreenshot("shot_04_countdown.png");

        yield return new WaitForSeconds(2.8f);
        if (Player != null) Player.SetPreviewSpeedAndBoost(29.5f, 1.5f, ItemType.Missile);
        yield return new WaitForSeconds(0.10f);
        ScreenCapture.CaptureScreenshot("shot_05_racing_1p.png");
        yield return new WaitForSeconds(0.25f);

        if (Player != null) Player.SetPreviewDrift(1, 1.15f, 27.8f, ItemType.Missile);
        yield return new WaitForSeconds(0.10f);
        ScreenCapture.CaptureScreenshot("shot_05b_drift_blue.png");
        yield return new WaitForSeconds(0.25f);

        if (Player != null) Player.SetPreviewDrift(-1, 2.85f, 28.5f, ItemType.Turbo);
        yield return new WaitForSeconds(0.10f);
        ScreenCapture.CaptureScreenshot("shot_05c_drift_purple.png");
        yield return new WaitForSeconds(0.25f);

        paused = true;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_06_paused.png");
        yield return new WaitForSeconds(0.4f);
        paused = false;

        // 7. リザルト画面
        bestLap[0] = 24.18f;
        foreach (var k in Karts) { k.Finished = true; k.Place = Karts.IndexOf(k) + 1; k.FinishTime = 72.5f + k.Place * 1.5f; }
        placeOrder.Clear();
        placeOrder.AddRange(Karts);
        placeOrder.Sort(ComparePlaces);
        state = State.Results;
        finishAt = Time.time - 5f;
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot("shot_07_results.png");
        yield return new WaitForSeconds(0.4f);

        // 8. 2P レース中 HUD
        SetTwoPlayer(true);
        ResetRace();
        state = State.Racing;
        stateTime = 5f;
        raceTime = 15.2f;
        yield return new WaitForSeconds(0.6f);
        if (Player != null) Player.SetPreviewSpeedAndBoost(27.2f, 0f, ItemType.Turbo);
        if (Player2 != null) Player2.SetPreviewSpeedAndBoost(29.0f, 1.2f, ItemType.Banana);
        ScreenCapture.CaptureScreenshot("shot_08_racing_2p.png");
        yield return new WaitForSeconds(0.4f);

        // 9. タイムアタック レース中 HUD
        SetTwoPlayer(false);
        IsTimeAttack = true;
        ResetRace();
        state = State.Racing;
        stateTime = 5f;
        raceTime = 28.6f;
        yield return new WaitForSeconds(0.6f);
        if (Player != null) Player.SetPreviewSpeedAndBoost(30.5f, 1.4f, ItemType.Turbo);
        ScreenCapture.CaptureScreenshot("shot_09_racing_timeattack.png");
        yield return new WaitForSeconds(0.4f);

        // 10. オンライン待機画面 (LOBBY - ROOM CODE 付き 4人)
        state = State.Title;
        SetTwoPlayer(false);
        IsTimeAttack = false;
        Net.HostDirect();
        Net.JoinCode = "WFHPQQ";
        playerKarts[5] = PackDriverKart(SelectedDriver, SelectedKart); playerNames[5] = "HostPlayer";
        playerKarts[0] = PackDriverKart(1, 1); playerNames[0] = "RivalOne";
        playerKarts[1] = PackDriverKart(6, 3); playerNames[1] = "Speedy";
        playerKarts[2] = PackDriverKart(8, 7); playerNames[2] = "KartMaster";
        lobbySlots.Clear();
        foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
        ApplyLobbyKarts();
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot("shot_10_lobby_4p.png");
        yield return new WaitForSeconds(0.4f);

        // 11. オンライン待機画面 (LOBBY - ROOM CODE 付き 8人満員)
        playerKarts[3] = PackDriverKart(4, 5); playerNames[3] = "Racer4";
        playerKarts[4] = PackDriverKart(9, 6); playerNames[4] = "Racer5";
        playerKarts[6] = PackDriverKart(2, 2); playerNames[6] = "Racer6";
        playerKarts[7] = PackDriverKart(7, 4); playerNames[7] = "Racer7";
        lobbySlots.Clear();
        foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
        ApplyLobbyKarts();
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot("shot_11_lobby_8p.png");
        yield return new WaitForSeconds(0.4f);

        // 12. 北大キャンパス コース表示（タイトル画面）
        Net.Disconnect();
        state = State.Title;
        LoadCourse(4);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot("shot_12_campus_title.png");
        yield return new WaitForSeconds(0.4f);

        // 13. 北大キャンパス レース中（スタート直後・メインストリート）
        StartRaceFromTitle();
        state = State.Racing;
        stateTime = 5f;
        raceTime = 12.4f;
        yield return new WaitForSeconds(0.8f);
        if (Player != null) Player.SetPreviewSpeedAndBoost(26.5f, 0f, ItemType.Star);
        ScreenCapture.CaptureScreenshot("shot_13_campus_race.png");
        yield return new WaitForSeconds(0.3f);

        void TeleportPlayer(int idx, float lat = 0f, float extraYaw = 0f)
        {
            if (track == null || track.Count == 0) return;
            int i = Mathf.Clamp(idx, 0, track.Count - 1);
            Player.transform.position = track.PointAt(i, lat) + Vector3.up * 0.4f;
            Player.Index = i;
            Player.Progress = track.Dist[i];
            float heading = Mathf.Atan2(track.Dirs[i].x, track.Dirs[i].z) * Mathf.Rad2Deg + extraYaw;
            Player.Heading = heading;
            Player.Speed = 0f;
            camYaws[0] = heading;
            var r = Quaternion.Euler(0, heading, 0);
            cam.transform.position = Player.transform.position + r * new Vector3(0, 2.7f, -6.8f);
            cam.transform.LookAt(Player.transform.position + r * new Vector3(0, 1.1f, 3.2f));
        }

        // 14. 北大キャンパス 北13条イチョウ並木（黄金のトンネル＆落ち葉絨毯）
        if (track != null && track.Count > 0)
        {
            TeleportPlayer((int)(0.29f * track.Count));
            yield return new WaitForSeconds(0.6f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(25.5f, 0f);
            ScreenCapture.CaptureScreenshot("shot_14_campus_ginkgo.png");
            yield return new WaitForSeconds(0.3f);

            // 15. 北大キャンパス 第2農場モデルバーン＆放牧ホルスタイン牛＆木柵
            TeleportPlayer((int)(0.555f * track.Count));
            yield return new WaitForSeconds(0.6f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(24.0f, 0f);
            ScreenCapture.CaptureScreenshot("shot_15_campus_barn.png");
            yield return new WaitForSeconds(0.3f);

            // 16. 北大キャンパス ポプラ並木通り（直立ポプラ列柱）
            TeleportPlayer((int)(0.82f * track.Count));
            yield return new WaitForSeconds(0.6f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(28.5f, 1.2f);
            ScreenCapture.CaptureScreenshot("shot_16_campus_poplar.png");
            yield return new WaitForSeconds(0.3f);

            // 17. 北大キャンパス 大野池＆総合博物館（睡蓮・木橋・自然石）
            TeleportPlayer((int)(0.22f * track.Count));
            yield return new WaitForSeconds(0.6f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(23.5f, 0f);
            ScreenCapture.CaptureScreenshot("shot_17_campus_pond.png");
            yield return new WaitForSeconds(0.3f);

            // 18. 北大キャンパス クラーク博士胸像＆花壇＆中央ローン
            TeleportPlayer((int)(0.106f * track.Count), 1.8f, -28f);
            yield return new WaitForSeconds(0.6f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(22.0f, 0f);
            ScreenCapture.CaptureScreenshot("shot_18_campus_clark.png");
            yield return new WaitForSeconds(0.3f);

            // 19. Course 0: TURBO CIRCUIT（スタイライズド大樹・白樺・花壇・サーキット）
            LoadCourse(0);
            yield return new WaitForSeconds(0.6f);
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 10f;
            yield return new WaitForSeconds(0.8f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(28.0f, 1.2f, ItemType.Turbo);
            ScreenCapture.CaptureScreenshot("shot_19_circuit_race.png");
            yield return new WaitForSeconds(0.3f);

            // スポンサー看板・オーバーヘッドゲートの検証撮影
            TeleportPlayer(16, 1.2f, 22f);
            if (Player != null) Player.Speed = 0f;
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot("shot_19b_sponsor_board.png");
            yield return new WaitForSeconds(0.3f);

            TeleportPlayer(track.Count * 33 / 100 - 9, 0f, 0f);
            if (Player != null) Player.Speed = 0f;
            yield return new WaitForSeconds(0.4f);
            ScreenCapture.CaptureScreenshot("shot_19c_bridge_banner.png");
            yield return new WaitForSeconds(0.3f);

            // 20. Course 1: SUNSET DUNES（ヤシの木・砂漠枯れ木・砂漠自然岩）
            LoadCourse(1);
            yield return new WaitForSeconds(0.6f);
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 10f;
            yield return new WaitForSeconds(0.8f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(26.8f, 0f, ItemType.Banana);
            ScreenCapture.CaptureScreenshot("shot_20_dunes_race.png");
            yield return new WaitForSeconds(0.3f);

            // 21. Course 2: FROST PEAK（スノーパイン針葉樹林・雪山・氷晶）
            LoadCourse(2);
            yield return new WaitForSeconds(0.6f);
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 10f;
            yield return new WaitForSeconds(0.8f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(27.5f, 0f, ItemType.Star);
            ScreenCapture.CaptureScreenshot("shot_21_frost_race.png");
            yield return new WaitForSeconds(0.3f);

            // 22. Course 3: NEON METROPOLIS（ネオン街路灯・サイバーツリー・発光看板・摩天楼）
            LoadCourse(3);
            yield return new WaitForSeconds(0.6f);
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 10f;
            yield return new WaitForSeconds(0.8f);
            if (Player != null) Player.SetPreviewSpeedAndBoost(30.2f, 1.5f, ItemType.Missile);
            ScreenCapture.CaptureScreenshot("shot_22_neon_race.png");
            yield return new WaitForSeconds(0.3f);

            // 23. Course 5: SKYLINE OVERPASS タイトル画面（コースバッジ・情報）
            LoadCourse(5);
            state = State.Title;
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot("shot_23_overpass_title.png");
            yield return new WaitForSeconds(0.3f);

            // 24. Course 5: 下層アンダーパス走行（頭上15mを高架橋が横断する迫力の立体交差アングル）
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 12f;
            if (track != null && track.Count > 0)
            {
                TeleportPlayer((int)(0.365f * track.Count)); // アンダーパス交差直前
                yield return new WaitForSeconds(0.6f);
                if (Player != null) Player.SetPreviewSpeedAndBoost(28.5f, 0f, ItemType.Turbo);
                ScreenCapture.CaptureScreenshot("shot_24_overpass_underpass.png");
                yield return new WaitForSeconds(0.3f);

                // 25. Course 5: 上層斜張橋走行（主塔・ステイケーブル・下界を見下ろす立体交差ピーク）
                TeleportPlayer((int)(0.785f * track.Count)); // 高架橋交差直前
                yield return new WaitForSeconds(0.6f);
                if (Player != null) Player.SetPreviewSpeedAndBoost(31.0f, 1.4f, ItemType.Missile);
                ScreenCapture.CaptureScreenshot("shot_25_overpass_bridge.png");
                yield return new WaitForSeconds(0.3f);
            }
        }

        yield return new WaitForSeconds(0.5f);
        Application.Quit();
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
        Time.timeScale = 1f;
        AudioListener.pause = false;
        state = State.Title;
        stateTime = 0;
        GamepadHaptics.StopAll();
        if (ghostReplay != null) ghostReplay.ClearPlayback();
        RestoreRoles();
        ShowAllKarts();
        LoadCourse(SelectedCourse);
        Audio.SetMusicTempo(1f);
        Audio.SetMusicVolume(0.3f);
    }

    void RemoveBananaById(int id)
    {
        for (int i = bananas.Count - 1; i >= 0; i--)
            if (bananas[i] != null && bananas[i].NetId == id)
            {
                Destroy(bananas[i].gameObject);
                bananas.RemoveAt(i);
            }
    }

    void ExplodeBombById(int id)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
            if (bombs[i] != null && bombs[i].NetId == id)
            {
                bombs[i].Explode();
            }
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

    void ResetRace()
    {
        foreach (var b in bananas) if (b) Destroy(b.gameObject);
        bananas.Clear();
        foreach (var m in missiles) if (m) Destroy(m.gameObject);
        missiles.Clear();
        foreach (var b in bombs) if (b) Destroy(b.gameObject);
        bombs.Clear();

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
        Audio.RestartMusic(SelectedCourse);
    }

    // ───────────────────────── Loop ─────────────────────────

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
        if (editingText && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape)))
        {
            GUIUtility.keyboardControl = 0;
        }

        Vector2 padVec = InputSettings.GetMenuPadVector();
        if (!editingText && padVec == Vector2.zero)
        {
            float ax = Input.GetAxisRaw("Horizontal");
            float ay = Input.GetAxisRaw("Vertical");
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
               || (!editingText && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)));
        cancel = InputSettings.GetSinglePadDown(PadBind.B, 0) || InputSettings.GetSinglePadDown(PadBind.B, 1)
              || Input.GetKeyDown(KeyCode.Escape);
        tabPrev = InputSettings.GetSinglePadDown(PadBind.LB, 0) || InputSettings.GetSinglePadDown(PadBind.LB, 1);
        tabNext = InputSettings.GetSinglePadDown(PadBind.RB, 0) || InputSettings.GetSinglePadDown(PadBind.RB, 1);
    }

    static readonly int[] TitleFocusOrder1P = { 1, 2, 7, 8, 0, 6, 5, 9, 10, 11, 12 };
    static readonly int[] TitleFocusOrder2P = { 1, 2, 3, 4, 7, 8, 0, 6, 5, 9, 10, 11, 12 };

    // メニュー操作の入力スナップショット（Update の先頭で1フレームに1回だけ取得する）
    struct MenuNav
    {
        public int moveX, moveY;
        public bool confirm, cancel, tabPrev, tabNext;
        public bool padStart, padBack, padX, padY;
    }

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

    // -autoshot / -titleshot / F12 などのスクリーンショット要求とテスト用ブースト
    void HandleCaptureRequests()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            ScreenCapture.CaptureScreenshot("screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        }
        if (titleShotRequested && state == State.Title && stateTime > shotDelay && !screenshotTaken)
        {
            screenshotTaken = true;
            string fname = string.IsNullOrEmpty(customShotName) ? "screenshot_title.png" : customShotName;
            ScreenCapture.CaptureScreenshot(fname);
            Invoke(nameof(QuitAfterScreenshot), 0.4f);
        }
        if (testBoostRequested && state == State.Racing && Player != null && !Player.Boosting)
            Player.Boost(5f);

        if (screenshotRequested && state == State.Racing && stateTime > shotDelay && !screenshotTaken)
        {
            screenshotTaken = true;
            string fname = string.IsNullOrEmpty(customShotName) ? $"screenshot_course{SelectedCourse}.png" : customShotName;
            ScreenCapture.CaptureScreenshot(fname);
            Invoke(nameof(QuitAfterScreenshot), 0.4f);
        }
        if ((screenshotRequested || titleShotRequested) && (stateTime + raceTime) > 60f) Application.Quit();
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
        if (Audio != null) Audio.Select();
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
            if (Audio != null) Audio.Select();
            return;
        }
        if (nav.confirm)
        {
            if (updater.State == AppUpdater.UpdateState.Downloading) updater.CancelDownload();
            else if (updateModalFocus == 1) updater.ShowModal = false;
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
                    if (Audio != null) Audio.Tick();
                }
            }
            return;
        }
        if (!string.IsNullOrEmpty(waitingKeyAction))
        {
            if (InputSettings.GetSinglePadDown(PadBind.B, 0) || nav.padStart)
            {
                waitingKeyAction = null;
                if (Audio != null) Audio.Tick();
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
            if (Audio != null) Audio.Select();
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
            else if (settingsFocus == 6 && nav.confirm) { CloseSettings(); if (Audio != null) Audio.Select(); }
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
                    if (Audio != null) Audio.Select();
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
                    if (Audio != null) Audio.Select();
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
        else if ((Input.GetKeyDown(KeyCode.Escape) || nav.padStart) && (state == State.Countdown || state == State.Racing))
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
        if (Input.GetKeyDown(KeyCode.O)) { showSettings = !showSettings; settingsFocus = 0; return; }
        if (Input.GetKeyDown(KeyCode.Q) || nav.padBack) Application.Quit();
        if (Input.GetKeyDown(KeyCode.R) || nav.padX) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; StartCountdown(); return; }
        if (Input.GetKeyDown(KeyCode.T) || nav.padY) { ReturnToTitle(); return; }
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
            if (nav.tabPrev || (!editTxt && Input.GetKeyDown(KeyCode.A))) SwitchCourse(-1);
            else if (nav.tabNext || (!editTxt && Input.GetKeyDown(KeyCode.D))) SwitchCourse(1);
            if (nav.moveY != 0) { lobbyFocus = (lobbyFocus - nav.moveY + 4) % 4; if (Audio != null) Audio.Tick(); }
            if (nav.moveX != 0)
            {
                if (lobbyFocus == 0) SwitchDriver(0, nav.moveX);
                else if (lobbyFocus == 1) SwitchKart(0, nav.moveX);
                else { lobbyFocus = lobbyFocus == 2 ? 3 : 2; if (Audio != null) Audio.Tick(); }
            }
            if (!editTxt && Input.GetKeyDown(KeyCode.W)) SwitchDriver(0, -1);
            else if (!editTxt && Input.GetKeyDown(KeyCode.S)) SwitchDriver(0, 1);
            else if (!editTxt && Input.GetKeyDown(KeyCode.Q)) SwitchKart(0, -1);
            else if (!editTxt && Input.GetKeyDown(KeyCode.E)) SwitchKart(0, 1);

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

        if (!editTxt && !Net.Busy && (Input.GetKeyDown(KeyCode.Escape) || nav.cancel || nav.padBack))
            { ShowQuitModal(); return; }
        if (!editTxt && Input.GetKeyDown(KeyCode.P) && !Net.Busy)
            { showSettings = !showSettings; settingsFocus = 0; if (Audio != null) Audio.Select(); return; }
        if (!editTxt && Input.GetKeyDown(KeyCode.L) && !Net.Busy)
            { ToggleLanguage(); return; }
        if (!editTxt && Input.GetKeyDown(KeyCode.U) && !Net.Busy)
            { if (updater != null) { updater.ShowModal = !updater.ShowModal; updateModalFocus = 0; if (Audio != null) Audio.Select(); } return; }
        if (((!editTxt && Input.GetKeyDown(KeyCode.O)) || nav.padX) && !Net.Busy)
            { if (IsTimeAttack) IsTimeAttack = false; netMenu = true; netFocus = 0; if (Audio != null) Audio.Select(); return; }
        if (!editTxt && Input.GetKeyDown(KeyCode.M) && !Net.Busy && !TwoPlayer)
        {
            IsTimeAttack = !IsTimeAttack;
            if (Audio != null) Audio.Select();
            return;
        }
        if (((!editTxt && Input.GetKeyDown(KeyCode.Tab)) || nav.padY) && !Net.Busy)
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
            int nextIdx = (curIdx - nav.moveY + order.Length) % order.Length;
            titleFocus = order[nextIdx];
            if (Audio != null) Audio.Tick();
        }

        if (nav.tabPrev || (!editTxt && Input.GetKeyDown(KeyCode.A)))
            SwitchCourse(-1);
        else if (nav.tabNext || (!editTxt && Input.GetKeyDown(KeyCode.D)))
            SwitchCourse(1);
        else if (nav.moveX != 0)
        {
            if (titleFocus == 1) SwitchDriver(0, nav.moveX);
            else if (titleFocus == 2) SwitchKart(0, nav.moveX);
            else if (titleFocus == 3 && TwoPlayer) SwitchDriver(1, nav.moveX);
            else if (titleFocus == 4 && TwoPlayer) SwitchKart(1, nav.moveX);
            else if (titleFocus == 5 || titleFocus == 0) SwitchCourse(nav.moveX);
            else if (titleFocus == 6) { if (TwoPlayer) SetTwoPlayer(false); IsTimeAttack = !IsTimeAttack; if (Audio != null) Audio.Select(); }
            else if (titleFocus == 7 || titleFocus == 8) { titleFocus = titleFocus == 7 ? 8 : 7; if (Audio != null) Audio.Tick(); }
            else if (titleFocus == 9 || titleFocus == 10 || titleFocus == 11 || titleFocus == 12)
            {
                int[] topBar = { 11, 10, 9, 12 };
                int ti = System.Array.IndexOf(topBar, titleFocus);
                titleFocus = topBar[(ti + (nav.moveX > 0 ? 1 : topBar.Length - 1)) % topBar.Length];
                if (Audio != null) Audio.Tick();
            }
        }
        else if (!editTxt && Input.GetKeyDown(KeyCode.W))
            SwitchDriver(0, -1);
        else if (!editTxt && Input.GetKeyDown(KeyCode.S))
            SwitchDriver(0, 1);
        else if (!editTxt && Input.GetKeyDown(KeyCode.Q))
            SwitchKart(0, -1);
        else if (!editTxt && Input.GetKeyDown(KeyCode.E))
            SwitchKart(0, 1);
        else if (!editTxt && TwoPlayer && Input.GetKeyDown(KeyCode.UpArrow))
            SwitchDriver(1, -1);
        else if (!editTxt && TwoPlayer && Input.GetKeyDown(KeyCode.DownArrow))
            SwitchDriver(1, 1);
        else if (!editTxt && TwoPlayer && Input.GetKeyDown(KeyCode.LeftArrow))
            SwitchKart(1, -1);
        else if (!editTxt && TwoPlayer && Input.GetKeyDown(KeyCode.RightArrow))
            SwitchKart(1, 1);

        if (!titleShotRequested)
        {
            if (nav.padStart || (!editTxt && Input.GetKeyDown(KeyCode.Space)) || (screenshotRequested && stateTime > 0.6f) || (Demo && stateTime > 2.5f))
            {
                StartRaceFromTitle();
            }
            else if (nav.confirm)
            {
                if (titleFocus == 6) { if (TwoPlayer) SetTwoPlayer(false); IsTimeAttack = !IsTimeAttack; if (Audio != null) Audio.Select(); }
                else if (titleFocus == 7) { if (IsTimeAttack) IsTimeAttack = false; SetTwoPlayer(!TwoPlayer); }
                else if (titleFocus == 8 && !Net.Busy) { if (IsTimeAttack) IsTimeAttack = false; netMenu = true; netFocus = 0; if (Audio != null) Audio.Select(); }
                else if (titleFocus == 9 && !Net.Busy) { showSettings = true; settingsFocus = 0; if (Audio != null) Audio.Select(); }
                else if (titleFocus == 10 && !Net.Busy) { if (updater != null) { updater.ShowModal = true; updateModalFocus = 0; if (Audio != null) Audio.Select(); } }
                else if (titleFocus == 11 && !Net.Busy) { ToggleLanguage(); }
                else if (titleFocus == 12 && !Net.Busy) { ShowQuitModal(); }
                else StartRaceFromTitle();
            }
        }
    }

    void UpdateCountdown()
    {
        int beep = Mathf.FloorToInt(stateTime - 1f);
        if (beep != lastCountdownBeep && beep >= 0 && beep <= 3)
        {
            lastCountdownBeep = beep;
            if (track != null) track.UpdateStartSignals(beep);
            if (beep < 3) Audio.Beep();
            else
            {
                Audio.Go();
                state = State.Racing;
                stateTime = 0;
                foreach (var k in Karts) k.OnGo();
            }
        }
    }

    void UpdateRacing(float dt)
    {
        raceTime += dt;
        if (stateTime > 2.5f && stateTime - dt <= 2.5f && track != null)
        {
            track.UpdateStartSignals(-1); // GO演出終了後に消灯
        }
        if (IsTimeAttack && ghostReplay != null)
        {
            ghostReplay.TickPlayback(raceTime);
            if (raceTime >= nextGhostSampleTime && Player != null && !Player.Finished)
            {
                currentRunGhost.Add(new GhostFrame { t = raceTime, pos = Player.transform.position, heading = Player.Heading });
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
                GamepadHaptics.StopAll();
                Audio.SetMusicVolume(0.15f);
            }
        }
        if (AllHumansFinished())
        {
            state = State.Results;
            resultsFocus = 0;
            finishAt = Time.time;
            GamepadHaptics.StopAll();
            Audio.SetMusicVolume(0.15f);
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
                if (canRestart && (resultsFocus == 0 || nav.padStart)) StartRaceFromTitle();
                else
                {
                    if (Online) LeaveOnline();
                    else ReturnToTitle();
                }
            }
            else if (nav.cancel || Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Backspace) || nav.padY)
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
        Audio.SetEngine(engSpeed / Kart.MaxSpeed, engThrottle, state != State.Title, anyDrift && canDrive, maxDriftLv);
        shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
    }

    // オンライン中はホストだけがレースを開始でき、クライアントには開始の合図が届く
    void StartRaceFromTitle()
    {
        if (Net.Busy && !Online) return;
        if (Online)
        {
            if (!Net.IsHost) return;
            Net.SendStart(SelectedCourse);
        }
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
        ResetRace();
        if (track != null) track.UpdateStartSignals(-1);
        firstFinishTime = -1f;
        newRecordTime = false;
        newRecordLap = false;
        spectateIndex = -1;
        state = State.Intro;
        stateTime = 0f;
        InitCourseIntro();
    }

    void StartCountdown()
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
        ResetRace();
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
                        currentRunGhost.Add(new GhostFrame { t = k.FinishTime, pos = k.transform.position, heading = k.Heading });
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
                GamepadHaptics.StopAll();
                Audio.SetMusicVolume(0.15f);
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

    void UpdatePlaces()
    {
        placeOrder.Sort((a, b) =>
        {
            bool ha = a.gameObject.activeSelf, hb = b.gameObject.activeSelf;
            if (ha != hb) return ha ? -1 : 1; // 非表示（タイムアタック中のCPU）は最下位
            return ComparePlaces(a, b);
        });
        for (int i = 0; i < placeOrder.Count; i++) placeOrder[i].Place = i + 1;
    }

    void KartCollisions()
    {
        for (int i = 0; i < Karts.Count; i++)
        for (int j = i + 1; j < Karts.Count; j++)
        {
            var a = Karts[i]; var b = Karts[j];
            if (!a.gameObject.activeSelf || !b.gameObject.activeSelf) continue;
            if (Mathf.Abs(a.transform.position.y - b.transform.position.y) > 2.0f) continue;
            var d = a.transform.position - b.transform.position;
            d.y = 0;
            float m = d.magnitude, min = Kart.Radius * 2f;
            if (m >= min || m < 0.001f) continue;
            var n = d / m;
            // 重いカートほど押し返されにくい（ミニ化中は軽くなり弾かれやすい）
            float massA = a.Mass * (a.Shocked ? 0.35f : 1.0f);
            float massB = b.Mass * (b.Shocked ? 0.35f : 1.0f);
            float wa = massB / (massA + massB), wb = 1f - wa;
            a.transform.position += n * (min - m) * wa;
            b.transform.position -= n * (min - m) * wb;

            if (a.Shielded && !b.Shielded) { b.Spin(); continue; }
            if (b.Shielded && !a.Shielded) { a.Spin(); continue; }
            if (!a.Shocked && b.Shocked) { b.Spin(); continue; }
            if (!b.Shocked && a.Shocked) { a.Spin(); continue; }

            // 相対接近速度を計算し、擦れ続けではなく実際に衝突した瞬間のみ強さに応じたバンプ音を鳴らす
            float relVel = Vector3.Dot(b.VelDir * b.Speed - a.VelDir * a.Speed, n);
            // 押し合い：横方向に少し弾く
            a.VelDir = (a.VelDir + n * 0.5f * wa).normalized;
            b.VelDir = (b.VelDir - n * 0.5f * wb).normalized;
            float damp = Mathf.Pow(0.985f, Time.deltaTime * 60f); // フレームレート非依存
            a.Speed *= damp;
            b.Speed *= damp;
            if ((a.IsPlayer || b.IsPlayer) && relVel > 2.8f)
                Audio.Bump(Mathf.Clamp01(relVel / 18f));
        }
    }

    void CheckItemBoxes()
    {
        foreach (var box in boxes)
        {
            if (!box.Active) continue;
            foreach (var k in Karts)
            {
                if (!k.gameObject.activeSelf) continue;
                if (Mathf.Abs(k.transform.position.y - box.transform.position.y) > 2.2f) continue;
                var d = k.transform.position - box.transform.position;
                d.y = 0;
                if (d.magnitude > 2.2f) continue;
                box.Break();
                if (k.IsRemote) break; // 相手側のカートのアイテムは相手が決める
                if (k.GiveItem(RollItem(k)) && k.IsPlayer) Audio.Pickup();
                break;
            }
        }
    }

    void CheckBananas()
    {
        for (int i = bananas.Count - 1; i >= 0; i--)
        {
            var b = bananas[i];
            foreach (var k in Karts)
            {
                if (k.IsRemote || !k.gameObject.activeSelf) continue; // 当たり判定は持ち主側で行う
                if (k == b.Owner && !b.Armed) continue;
                if (Mathf.Abs(k.transform.position.y - b.transform.position.y) > 2.0f) continue;
                var d = k.transform.position - b.transform.position;
                d.y = 0;
                if (d.magnitude > 1.5f) continue;
                if (k.Shielded || k.Spin())
                {
                    Fx.Burst(b.transform.position, new Color(1f, 0.95f, 0.3f), 12, 5f, 0.4f);
                    bananas.RemoveAt(i);
                    if (Online) Net.SendBananaGone(b.NetId);
                    Destroy(b.gameObject);
                    break;
                }
            }
        }
    }

    public bool IsMissileApproaching(Kart k, out float distance)
    {
        distance = 999f;
        if (k == null || missiles.Count == 0) return false;
        foreach (var m in missiles)
        {
            if (m != null && m.Target == k)
            {
                float d = Vector3.Distance(m.transform.position, k.transform.position);
                if (d < 45f && d < distance)
                {
                    distance = d;
                }
            }
        }
        return distance < 45f;
    }

    ItemType RollItem(Kart k)
    {
        if (ItemDefs.Count == 0) return ItemType.Banana;
        int activeCount = Karts.Count(x => x.gameObject.activeSelf);
        float p = activeCount > 1 ? Mathf.Clamp01((k.Place - 1) / (float)(activeCount - 1)) : 0f;

        float totalWeight = 0f;
        for (int i = 0; i < ItemDefs.Count; i++)
        {
            totalWeight += ItemDefs[i].WeightFunc != null ? ItemDefs[i].WeightFunc(p) : 0.1f;
        }

        if (totalWeight <= 0f) return ItemType.Banana;

        float r = Random.value * totalWeight;
        for (int i = 0; i < ItemDefs.Count; i++)
        {
            float w = ItemDefs[i].WeightFunc != null ? ItemDefs[i].WeightFunc(p) : 0.1f;
            if ((r -= w) < 0f) return ItemDefs[i].Type;
        }

        return ItemDefs[ItemDefs.Count - 1].Type;
    }

    public void UseThunder(Kart user)
    {
        thunderCooldown = 28f;
        screenFlashTimer = 0.35f;
        Audio.Explode(1.2f);

        foreach (var k in Karts)
        {
            if (k == null || !k.gameObject.activeSelf || k == user) continue;
            k.Shock(6.5f);
        }

        if (Online && user != null && !user.IsRemote)
        {
            Net.SendThunder(Karts.IndexOf(user));
        }
    }

    public void SpawnBanana(Vector3 pos, Kart owner)
    {
        int ownerId = Karts.IndexOf(owner);
        int id = ownerId * 100000 + (nextBananaId++ % 100000);
        SpawnBananaInternal(id, pos, owner);
        if (Online && !owner.IsRemote) Net.SendBanana(id, ownerId, pos);
    }

    void SpawnBananaInternal(int id, Vector3 pos, Kart owner)
    {
        var b = new GameObject("Banana").AddComponent<Banana>();
        b.Init(pos, owner, bananaMaterial);
        b.NetId = id;
        bananas.Add(b);
    }

    public void SpawnMissile(Kart owner)
    {
        var target = Karts.FirstOrDefault(k => k.gameObject.activeSelf && k.Place == owner.Place - 1);
        SpawnMissileInternal(owner, target);
        if (Online && !owner.IsRemote) Net.SendMissile(Karts.IndexOf(owner), target == null ? -1 : Karts.IndexOf(target));
    }

    void SpawnMissileInternal(Kart owner, Kart target)
    {
        var m = new GameObject("Missile").AddComponent<Missile>();
        m.Init(this, track, owner, target, missileMaterial);
        missiles.Add(m);
    }

    public void RemoveMissile(Missile m) => missiles.Remove(m);

    public void SpawnBomb(Vector3 pos, Vector3 vel, Kart owner)
    {
        int ownerId = Karts.IndexOf(owner);
        int id = ownerId * 100000 + (nextBombId++ % 100000);
        SpawnBombInternal(id, pos, vel, owner);
        if (Online && !owner.IsRemote) Net.SendBomb(id, ownerId, pos, vel);
    }

    void SpawnBombInternal(int id, Vector3 pos, Vector3 vel, Kart owner)
    {
        var b = new GameObject("Bomb").AddComponent<Bomb>();
        b.Init(this, track, owner, pos, vel, kartPaintMaterial);
        b.NetId = id;
        bombs.Add(b);
    }

    public void RemoveBomb(Bomb b) => bombs.Remove(b);

    public void TriggerBombChain(Bomb source, Vector3 pos, float radius)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var b = bombs[i];
            if (b == null || b == source) continue;
            if (Vector3.Distance(b.transform.position, pos) < radius)
            {
                b.Explode();
            }
        }
    }

    public bool MissileHitsBanana(Vector3 pos)
    {
        for (int i = 0; i < bananas.Count; i++)
        {
            if (bananas[i] == null) continue;
            if ((bananas[i].transform.position + Vector3.up * 0.5f - pos).magnitude < 1.4f)
            {
                var b = bananas[i];
                Fx.Burst(b.transform.position, new Color(1f, 0.95f, 0.3f), 12, 5f, 0.4f);
                bananas.RemoveAt(i);
                if (Online) Net.SendBananaGone(b.NetId);
                Destroy(b.gameObject);
                return true;
            }
        }
        return false;
    }

    public bool KartBehindWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k || !o.gameObject.activeSelf) continue;
            float d = k.RaceDistance - o.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }

    public bool KartAheadWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k || !o.gameObject.activeSelf) continue;
            float d = o.RaceDistance - k.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }

    void QuitAfterScreenshot() => Application.Quit();

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
