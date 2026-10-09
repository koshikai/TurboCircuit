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

    enum State { Title, Countdown, Racing, Results }
    State state = State.Title;
    bool paused;

    public Kart Player { get; private set; }
    public List<Kart> Karts { get; } = new List<Kart>();
    public RaceAudio Audio { get; private set; }
    public Light Sun { get; private set; }
    public int SelectedCourse { get; private set; }
    public bool RaceRunning => state == State.Racing || state == State.Results;
    public float RaceTime => raceTime;
    // 起動オプション -demo：自動スタートし、プレイヤーも CPU が運転する（動作確認用）
    public bool Demo { get; private set; }
    bool screenshotRequested, screenshotTaken, titleShotRequested, testBoostRequested, autoShotRequested;
    string customShotName;
    float shotDelay = 4.2f;

    Track track;
    readonly List<ItemBox> boxes = new List<ItemBox>();
    readonly List<Banana> bananas = new List<Banana>();
    readonly List<Missile> missiles = new List<Missile>();
    Font font;
    Font fontMain, fontNum, fontMono;

    float stateTime, raceTime, shake, finishAt;
    int lastCountdownBeep;

    // ローカル対戦（2P 画面分割）。インデックス 0 = P1、1 = P2
    public bool TwoPlayer { get; private set; }
    public Kart Player2 { get; private set; }
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
    readonly Dictionary<int, int> playerKarts = new Dictionary<int, int>(); // slot -> kartCharIdx
    readonly Dictionary<Unity.Networking.Transport.NetworkConnection, int> clientSlots = new Dictionary<Unity.Networking.Transport.NetworkConnection, int>();
    bool LobbyActive => state == State.Title && Net != null && Net.Busy && playerKarts.Count > 0;
    public int OnlinePlayerCount => Net != null && Net.Busy ? Mathf.Max(1, playerKarts.Count) : 1;
    float firstFinishTime = -1f;
    // レコード管理 & 設定 & 観戦 & 後方視点
    bool newRecordTime, newRecordLap;
    bool isLookingBehind;
    int spectateIndex = -1;
    bool showSettings;
    int settingsTab = 0; // 0 = General, 1 = Key Config
    string waitingKeyAction = null;
    string playerName = "Player";
    readonly Dictionary<int, string> playerNames = new Dictionary<int, string>();

    // タイムアタック & ゴーストカー
    public bool IsTimeAttack { get; private set; }
    GhostReplay ghostReplay;
    readonly List<GhostFrame> currentRunGhost = new List<GhostFrame>();
    float nextGhostSampleTime;

    // 入力欄が空でも表示・送信には必ず有効な名前を使う
    string DisplayName => string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
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
        if (Player != null && !TwoPlayer) Player.Name = $"{DisplayName} ({KartCharacters[SelectedKart].name})";
    }

    void CloseSettings()
    {
        showSettings = false;
        waitingKeyAction = null;
        PlayerPrefs.Save();
    }

    public static float GetBestTime(int course) => PlayerPrefs.GetFloat($"tc_rec_time_{course}", 0f);
    public static float GetBestLap(int course) => PlayerPrefs.GetFloat($"tc_rec_lap_{course}", 0f);
    public static void SetBestTime(int course, float t) { PlayerPrefs.SetFloat($"tc_rec_time_{course}", t); PlayerPrefs.Save(); }
    public static void SetBestLap(int course, float t) { PlayerPrefs.SetFloat($"tc_rec_lap_{course}", t); PlayerPrefs.Save(); }

    bool netMenu;
    string joinCodeInput = "", ipInput = "127.0.0.1";
    int nextBananaId;
    GUIStyle sButton, sField, sNum, sMono;
    readonly List<(int id, NetKartState s)> sendBuf = new List<(int, NetKartState)>();
    Texture2D minimap;
    Texture2D vignetteTex;
    Texture2D speedLineTex;
    Texture2D iconTurbo, iconBanana, iconMissile, iconShield;
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
    System.Func<Vector3, Vector2> toMap;

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
            speed = 8, accel = 4, handling = 4, weight = 6 
        },
        new KartCharacterDef { 
            name = "OOPI", driver = "Racer Swift", trait = "Sharp Handling & Turn", color = new Color(1.0f, 0.62f, 0.12f),
            speed = 4, accel = 6, handling = 8, weight = 3 
        },
        new KartCharacterDef { 
            name = "OOZI", driver = "Cool Titan", trait = "Heavy Weight & High Grip", color = new Color(0.22f, 0.88f, 0.38f),
            speed = 6, accel = 3, handling = 4, weight = 8 
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
                if (loaded != null && loaded.Length > 0)
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

    public int SelectedKart { get; private set; } = 0;
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
        Demo = args.Contains("-demo") || args.Contains("-screenshot");
        screenshotRequested = args.Contains("-screenshot");
        titleShotRequested = args.Contains("-titleshot");
        testBoostRequested = args.Contains("-testboost");
        autoShotRequested = args.Contains("-autoshot");
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
        cam2.enabled = false;

        SetupPostProcessing();
        playerName = PlayerPrefs.GetString("tc_player_name", "Player");
        if (PlayerPrefs.HasKey("tc_fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt("tc_fullscreen") == 1;

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

        // コマンドライン引数 -course の解析
        int cIdx = 0;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-course" && int.TryParse(args[i + 1], out int parsed))
                cIdx = parsed;
            if (args[i] == "-kart" && int.TryParse(args[i + 1], out int parsedKart))
                SelectedKart = Mathf.Clamp(parsedKart, 0, KartCharacters.Length - 1);
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
            string kName = isPlayer ? ("YOU (" + KartCharacters[SelectedKart].name + ")") : AiNames[ai];
            Color kCol = isPlayer ? KartCharacters[SelectedKart].color : AiColors[ai];
            float skill = isPlayer ? 1f : Mathf.Lerp(1.02f, 0.94f, (float)i / 7f);
            k.Init(this, track, kName, kCol, isPlayer, idx, lat, skill);
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
        Net.OnState = (id, s) => { if (id >= 0 && id < Karts.Count && Karts[id].IsRemote) Karts[id].ApplyNetState(s); };
        Net.OnBanana = (id, owner, pos) => { if (owner >= 0 && owner < Karts.Count) SpawnBananaInternal(id, pos, Karts[owner]); };
        Net.OnBananaGone = RemoveBananaById;
        Net.OnMissile = (owner, target) => { if (owner >= 0 && owner < Karts.Count) SpawnMissileInternal(Karts[owner], (target >= 0 && target < Karts.Count) ? Karts[target] : null); };
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
        if (autoShotRequested) StartCoroutine(AutoCaptureScreens());
    }

    System.Collections.IEnumerator AutoCaptureScreens()
    {
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot("shot_01_title.png");
        yield return new WaitForSeconds(0.4f);

        showSettings = true;
        settingsTab = 0;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_02_settings_general.png");
        yield return new WaitForSeconds(0.4f);

        settingsTab = 1;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_03_settings_keys.png");
        yield return new WaitForSeconds(0.4f);
        CloseSettings();
        yield return new WaitForSeconds(0.4f);

        StartRaceFromTitle();
        yield return new WaitForSeconds(1.8f);
        ScreenCapture.CaptureScreenshot("shot_04_countdown.png");

        yield return new WaitForSeconds(2.8f);
        ScreenCapture.CaptureScreenshot("shot_05_racing_1p.png");

        paused = true;
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot("shot_06_paused.png");
        yield return new WaitForSeconds(0.4f);
        paused = false;

        // 7. リザルト画面
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
        ScreenCapture.CaptureScreenshot("shot_09_racing_timeattack.png");
        yield return new WaitForSeconds(0.4f);

        // 10. オンライン待機画面 (LOBBY - ROOM CODE 付き 4人)
        state = State.Title;
        SetTwoPlayer(false);
        IsTimeAttack = false;
        Net.HostDirect();
        Net.JoinCode = "WFHPQQ";
        playerKarts[5] = 0; playerNames[5] = "HostPlayer";
        playerKarts[0] = 1; playerNames[0] = "RivalOne";
        playerKarts[1] = 2; playerNames[1] = "Speedy";
        playerKarts[2] = 3; playerNames[2] = "KartMaster";
        lobbySlots.Clear();
        foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
        yield return new WaitForSeconds(0.8f);
        ScreenCapture.CaptureScreenshot("shot_10_lobby_4p.png");
        yield return new WaitForSeconds(0.4f);

        // 11. オンライン待機画面 (LOBBY - ROOM CODE 付き 8人満員)
        playerKarts[3] = 4; playerNames[3] = "Racer4";
        playerKarts[4] = 0; playerNames[4] = "Racer5";
        playerKarts[6] = 1; playerNames[6] = "Racer6";
        playerKarts[7] = 2; playerNames[7] = "Racer7";
        lobbySlots.Clear();
        foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
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
            ScreenCapture.CaptureScreenshot("shot_14_campus_ginkgo.png");
            yield return new WaitForSeconds(0.3f);

            // 15. 北大キャンパス 第2農場モデルバーン＆放牧ホルスタイン牛＆木柵
            TeleportPlayer((int)(0.555f * track.Count));
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot("shot_15_campus_barn.png");
            yield return new WaitForSeconds(0.3f);

            // 16. 北大キャンパス ポプラ並木通り（直立ポプラ列柱）
            TeleportPlayer((int)(0.82f * track.Count));
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot("shot_16_campus_poplar.png");
            yield return new WaitForSeconds(0.3f);

            // 17. 北大キャンパス 大野池＆総合博物館（睡蓮・木橋・自然石）
            TeleportPlayer((int)(0.22f * track.Count));
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot("shot_17_campus_pond.png");
            yield return new WaitForSeconds(0.3f);

            // 18. 北大キャンパス クラーク博士胸像＆花壇＆中央ローン
            TeleportPlayer((int)(0.106f * track.Count), 1.8f, -28f);
            yield return new WaitForSeconds(0.6f);
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
            ScreenCapture.CaptureScreenshot("shot_19_circuit_race.png");
            yield return new WaitForSeconds(0.3f);

            // 20. Course 1: SUNSET DUNES（ヤシの木・砂漠枯れ木・砂漠自然岩）
            LoadCourse(1);
            yield return new WaitForSeconds(0.6f);
            StartRaceFromTitle();
            state = State.Racing;
            stateTime = 5f;
            raceTime = 10f;
            yield return new WaitForSeconds(0.8f);
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
            ScreenCapture.CaptureScreenshot("shot_22_neon_race.png");
        }

        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    void ApplyKartStats()
    {
        var d = KartCharacters[SelectedKart];
        Player.ApplyStats(d.speed, d.accel, d.handling, d.weight);
    }

    Kart HumanKart(int p) => p == 0 ? Player : Player2;

    void SetTwoPlayer(bool on)
    {
        if (TwoPlayer == on) return;
        TwoPlayer = on;
        if (Audio != null) Audio.Select();
        ConfigureHuman(0);
        ConfigureHuman(1);
    }

    // P1 / P2 のカートを人間操作（または P2 のみ CPU）に設定し、見た目と性能を選択キャラに合わせる
    void ConfigureHuman(int p)
    {
        var k = HumanKart(p);
        if (p == 0 || TwoPlayer)
        {
            int sel = p == 0 ? SelectedKart : SelectedKart2;
            var d = KartCharacters[sel];
            k.IsPlayer = true;
            k.PlayerIndex = p;
            k.ModelVariant = sel;
            k.Name = TwoPlayer ? $"P{p + 1} ({d.name})" : $"{DisplayName} ({d.name})";
            k.Color = d.color;
            k.ApplyStats(d.speed, d.accel, d.handling, d.weight);
        }
        else
        {
            int ai = P2Slot; // P2Slot < playerGridSlot なので CPU 名簿のインデックスと一致
            k.IsPlayer = false;
            k.PlayerIndex = 0;
            k.ModelVariant = -1;
            k.Name = AiNames[ai];
            k.Color = AiColors[ai];
            k.ApplyStats(5, 5, 5, 5);
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
        RestoreRoles();
        LoadCourse(SelectedCourse);
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

    public void LoadCourse(int index)
    {
        SelectedCourse = Mathf.Clamp(index, 0, Track.Courses.Length - 1);
        foreach (var b in boxes) if (b) Destroy(b.gameObject);
        boxes.Clear();
        foreach (var b in bananas) if (b) Destroy(b.gameObject);
        bananas.Clear();
        foreach (var m in missiles) if (m) Destroy(m.gameObject);
        missiles.Clear();

        Audio.SetCourseMusic(SelectedCourse);
        track.BuildPath(SelectedCourse);
        track.BuildVisuals(this, SelectedCourse);
        track.ApplyEnvironment(this, SelectedCourse);
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

    void SwitchKart(int p, int delta)
    {
        int cur = p == 0 ? SelectedKart : SelectedKart2;
        int next = (cur + delta + KartCharacters.Length) % KartCharacters.Length;
        if (next == cur) return;
        if (p == 0) SelectedKart = next; else SelectedKart2 = next;
        if (Audio != null) Audio.Select();
        if (HumanKart(p) != null) ConfigureHuman(p);
        if (p == 0 && Online)
        {
            if (Net.IsHost)
            {
                playerKarts[mySlot] = SelectedKart;
                SyncLobby();
            }
            else
            {
                Net.SendHello(SelectedKart, DisplayName);
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

    void Update()
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

        if (showSettings)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && netMenu && !LobbyActive)
        {
            netMenu = false;
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && Net.Busy && state == State.Title)
        {
            LeaveOnline();
        }
        else if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7)) && state != State.Title)
        {
            if (Online)
            {
                LeaveOnline();
            }
            else
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
                AudioListener.pause = paused;
            }
        }
        if (paused)
        {
            if (Input.GetKeyDown(KeyCode.O)) { showSettings = !showSettings; return; }
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton6)) Application.Quit();
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton2)) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; StartCountdown(); }
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.JoystickButton3)) { ReturnToTitle(); return; }
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        GamepadHaptics.Update(dt);
        stateTime += dt;
        bannerTimes[0] += dt;
        bannerTimes[1] += dt;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) 
                  || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton0);
        switch (state)
        {
            case State.Title:
                if (netMenu && !LobbyActive) break; // 接続画面の入力中はタイトルのショートカットを無効にする
                float vAxis = Input.GetAxisRaw("Vertical");
                stickNavTimer -= dt;
                bool stickUp = false;
                bool stickDown = false;
                if (Mathf.Abs(vAxis) > 0.6f)
                {
                    if (stickNavTimer <= 0f)
                    {
                        if (vAxis > 0.6f) stickUp = true;
                        else if (vAxis < -0.6f) stickDown = true;
                        stickNavTimer = 0.25f;
                    }
                }
                else if (Mathf.Abs(vAxis) < 0.2f)
                {
                    stickNavTimer = 0f;
                }

                if (Input.GetKeyDown(KeyCode.P) && !Net.Busy)
                    { showSettings = !showSettings; break; }
                if ((Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.JoystickButton2)) && !Net.Busy)
                    { netMenu = true; break; }
                if (Input.GetKeyDown(KeyCode.M) && !Net.Busy && !TwoPlayer)
                {
                    IsTimeAttack = !IsTimeAttack;
                    if (Audio != null) Audio.Select();
                    break;
                }
                if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton3)) && !Net.Busy)
                {
                    if (IsTimeAttack) IsTimeAttack = false;
                    SetTwoPlayer(!TwoPlayer);
                }
                else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton4))
                    SwitchCourse(-1);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton5))
                    SwitchCourse(1);
                else if (Input.GetKeyDown(KeyCode.W) || stickUp || (!TwoPlayer && Input.GetKeyDown(KeyCode.UpArrow)))
                    SwitchKart(0, -1);
                else if (Input.GetKeyDown(KeyCode.S) || stickDown || (!TwoPlayer && Input.GetKeyDown(KeyCode.DownArrow)))
                    SwitchKart(0, 1);
                else if (TwoPlayer && Input.GetKeyDown(KeyCode.UpArrow))
                    SwitchKart(1, -1);
                else if (TwoPlayer && Input.GetKeyDown(KeyCode.DownArrow))
                    SwitchKart(1, 1);

                if (!titleShotRequested && (enter || Input.GetKeyDown(KeyCode.Space) || (screenshotRequested && stateTime > 0.6f) || (Demo && stateTime > 2.5f)))
                    StartRaceFromTitle();
                break;
            case State.Countdown:
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
                break;
            case State.Racing:
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
                        finishAt = Time.time;
                        Audio.SetMusicVolume(0.15f);
                    }
                }
                if (AllHumansFinished())
                {
                    state = State.Results;
                    finishAt = Time.time;
                    Audio.SetMusicVolume(0.15f);
                }
                break;
            case State.Results:
                raceTime += dt;
                if (Time.time > finishAt + 2.5f)
                {
                    if (enter || Input.GetKeyDown(KeyCode.JoystickButton0)) StartRaceFromTitle();
                    else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton3))
                    {
                        if (Online) LeaveOnline();
                        else ReturnToTitle();
                    }
                }
                break;
        }

        bool canDrive = RaceRunning;
        foreach (var k in Karts) k.Tick(dt, canDrive);

        if (canDrive)
        {
            KartCollisions();
            CheckItemBoxes();
            CheckBananas();
        }
        UpdatePlaces();

        if (Online && state != State.Title && Net.SendDue)
        {
            sendBuf.Clear();
            for (int i = 0; i < Karts.Count; i++)
                if (!Karts[i].IsRemote && (Net.IsHost || Karts[i] == Player)) sendBuf.Add((i, Karts[i].MakeNetState()));
            Net.SendStates(sendBuf);
        }

        float engSpeed = Mathf.Abs(Player.Speed);
        bool anyDrift = Player.Drifting;
        if (TwoPlayer && Player2 != null)
        {
            engSpeed = Mathf.Max(engSpeed, Mathf.Abs(Player2.Speed));
            anyDrift = anyDrift || Player2.Drifting;
        }
        Audio.SetEngine(engSpeed / Kart.MaxSpeed, state != State.Title, anyDrift && canDrive);
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
        StartCountdown();
    }

    void StartCountdown()
    {
        if (!Online) RestoreRoles();
        if (IsTimeAttack)
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
                Banner("FINISH!", Color.white, k.PlayerIndex);
                RecordLap(k.PlayerIndex);

                if (k.PlayerIndex == 0 && !Demo)
                {
                    float prevBest = GetBestTime(SelectedCourse);
                    if (prevBest <= 0 || k.FinishTime < prevBest)
                    {
                        SetBestTime(SelectedCourse, k.FinishTime);
                        newRecordTime = true;
                        Banner("NEW COURSE RECORD!", new Color(1f, 0.85f, 0.2f), k.PlayerIndex);

                        if (IsTimeAttack && currentRunGhost.Count > 1)
                        {
                            var gd = new GhostData { finishTime = k.FinishTime, frames = new List<GhostFrame>(currentRunGhost) };
                            GhostReplay.SaveGhost(SelectedCourse, gd);
                        }
                    }
                }
            }
            if (AllHumansFinished())
            {
                state = State.Results;
                finishAt = Time.time;
                Audio.SetMusicVolume(0.15f);
            }
            return;
        }
        if (!k.IsPlayer || k.Lap < 2) return;

        RecordLap(k.PlayerIndex);
        if (k.Lap == totalLaps)
        {
            Banner("FINAL LAP!", new Color(1f, 0.3f, 0.3f), k.PlayerIndex);
            Audio.FinalLap();
            Audio.SetMusicTempo(1.1f);
        }
        else
        {
            Banner("LAP " + k.Lap + "/" + totalLaps, Color.white, k.PlayerIndex);
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
            if (p == 0 && !Demo)
            {
                float prevBestLap = GetBestLap(SelectedCourse);
                if (prevBestLap <= 0 || lap < prevBestLap)
                {
                    SetBestLap(SelectedCourse, lap);
                    newRecordLap = true;
                    Banner("NEW LAP RECORD!", new Color(1f, 0.85f, 0.2f), p);
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
        placeOrder.Sort(ComparePlaces);
        for (int i = 0; i < placeOrder.Count; i++) placeOrder[i].Place = i + 1;
    }

    void KartCollisions()
    {
        for (int i = 0; i < Karts.Count; i++)
        for (int j = i + 1; j < Karts.Count; j++)
        {
            var a = Karts[i]; var b = Karts[j];
            if (Mathf.Abs(a.transform.position.y - b.transform.position.y) > 2.0f) continue;
            var d = a.transform.position - b.transform.position;
            d.y = 0;
            float m = d.magnitude, min = Kart.Radius * 2f;
            if (m >= min || m < 0.001f) continue;
            var n = d / m;
            // 重いカートほど押し返されにくい
            float wa = b.Mass / (a.Mass + b.Mass), wb = 1f - wa;
            a.transform.position += n * (min - m) * wa;
            b.transform.position -= n * (min - m) * wb;

            if (a.Shielded && !b.Shielded) { b.Spin(); continue; }
            if (b.Shielded && !a.Shielded) { a.Spin(); continue; }

            // 押し合い：横方向に少し弾く
            a.VelDir = (a.VelDir + n * 0.5f * wa).normalized;
            b.VelDir = (b.VelDir - n * 0.5f * wb).normalized;
            a.Speed *= 0.985f;
            b.Speed *= 0.985f;
            if ((a.IsPlayer || b.IsPlayer) && Random.value < 0.15f) Audio.Bump();
        }
    }

    void CheckItemBoxes()
    {
        foreach (var box in boxes)
        {
            if (!box.Active) continue;
            foreach (var k in Karts)
            {
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
                if (k.IsRemote) continue; // 当たり判定は持ち主側で行う
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
        float p = (k.Place - 1) / (float)(Karts.Count - 1);
        float wBanana = Mathf.Lerp(0.55f, 0.05f, p);
        float wTurbo = Mathf.Lerp(0.25f, 0.4f, p);
        float wMissile = Mathf.Lerp(0.2f, 0.35f, p);
        float wShield = Mathf.Lerp(0.0f, 0.2f, p);
        float r = Random.value * (wBanana + wTurbo + wMissile + wShield);
        if ((r -= wBanana) < 0) return ItemType.Banana;
        if ((r -= wTurbo) < 0) return ItemType.Turbo;
        if ((r -= wMissile) < 0) return ItemType.Missile;
        return ItemType.Shield;
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
        var target = Karts.FirstOrDefault(k => k.Place == owner.Place - 1);
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

    public bool MissileHitsBanana(Vector3 pos)
    {
        for (int i = 0; i < bananas.Count; i++)
        {
            if ((bananas[i].transform.position + Vector3.up * 0.5f - pos).magnitude < 1.4f)
            {
                Destroy(bananas[i].gameObject);
                bananas.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool KartBehindWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k) continue;
            float d = k.RaceDistance - o.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }

    public bool KartAheadWithin(Kart k, float dist)
    {
        foreach (var o in Karts)
        {
            if (o == k) continue;
            float d = o.RaceDistance - k.RaceDistance;
            if (d > 0 && d < dist) return true;
        }
        return false;
    }

    void QuitAfterScreenshot() => Application.Quit();

    void OnDestroy()
    {
        GamepadHaptics.StopAll();
    }

    void OnApplicationQuit()
    {
        GamepadHaptics.StopAll();
    }
}
