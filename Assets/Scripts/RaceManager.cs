using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// レース全体の進行・順位・アイテム・カメラ・UI
public class RaceManager : MonoBehaviour
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
    bool screenshotRequested, screenshotTaken, titleShotRequested, testBoostRequested;
    string customShotName;
    float shotDelay = 4.2f;

    Track track;
    readonly List<ItemBox> boxes = new List<ItemBox>();
    readonly List<Banana> bananas = new List<Banana>();
    readonly List<Missile> missiles = new List<Missile>();
    Font font;

    float stateTime, raceTime, shake, titleDist, finishAt;
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
    public bool Online => Net != null && Net.Connected;
    Kart rival;                 // オンライン相手のカート
    int rivalKartIdx;
    bool netMenu;
    string joinCodeInput = "", ipInput = "127.0.0.1";
    int nextBananaId;
    GUIStyle sButton, sField;
    readonly List<(int id, NetKartState s)> sendBuf = new List<(int, NetKartState)>();
    Texture2D minimap;
    Texture2D vignetteTex;
    Texture2D speedLineTex;
    Texture2D iconTurbo, iconBanana, iconMissile, iconShield;
    Texture2D titleLogoTex;
    Texture2D[] trackBadgeTex = new Texture2D[4];
    Texture2D cardPanelTex;
    Texture2D cardPopTex;
    Texture2D btnRaceTex;
    Texture2D ribbonTrackTex;
    Texture2D ribbonDriverTex;
    System.Func<Vector3, Vector2> toMap;

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
    public static readonly KartCharacterDef[] KartCharacters =
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

    public int SelectedKart { get; private set; } = 0;

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
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-shotdelay" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d))
                shotDelay = d;
            if (args[i] == "-shotname")
                customShotName = args[i + 1];
        }
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureMaterials();
        LoadUIAssets();
        Audio = gameObject.AddComponent<RaceAudio>();
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
        QualitySettings.shadowResolution = ShadowResolution.High;

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
        Net.OnConnected = EnterOnline;
        Net.OnDisconnected = OnNetDisconnected;
        Net.OnRivalKart = OnRivalKart;
        Net.OnCourse = c => { if (state == State.Title && c != SelectedCourse) LoadCourse(c); };
        Net.OnStart = c => { if (c != SelectedCourse) LoadCourse(c); StartCountdown(); };
        Net.OnState = (id, s) => { if (id >= 0 && id < Karts.Count && Karts[id].IsRemote) Karts[id].ApplyNetState(s); };
        Net.OnBanana = (id, owner, pos) => { if (owner < Karts.Count) SpawnBananaInternal(id, pos, Karts[owner]); };
        Net.OnBananaGone = RemoveBananaById;
        Net.OnMissile = (owner, target) => { if (owner < Karts.Count) SpawnMissileInternal(Karts[owner], target < Karts.Count ? Karts[target] : null); };
        // 動作確認用：-nethost / -netjoin <ip> で起動直後に直接接続する
        if (args.Contains("-netmenu")) netMenu = true;
        if (args.Contains("-nethost")) Net.HostDirect();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-netjoin") Net.JoinDirect(args[i + 1]);

        LoadCourse(cIdx);
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
        if (Audio != null) Audio.Beep();
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
            k.Name = TwoPlayer ? $"P{p + 1} ({d.name})" : "YOU (" + d.name + ")";
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

    bool AllHumansFinished() => Player.Finished && (!TwoPlayer || Player2.Finished) && (!Online || rival.Finished);

    // ───────────────────────── オンライン対戦 ─────────────────────────

    // 接続成立：ホスト = 手前スロット(P1)、クライアント = 奥のスロット(P2)。CPU はホストだけが計算する
    void EnterOnline()
    {
        netMenu = false;
        if (TwoPlayer) SetTwoPlayer(false);
        var mine = Net.IsHost ? Karts[playerGridSlot] : Karts[P2Slot];
        rival = Net.IsHost ? Karts[P2Slot] : Karts[playerGridSlot];
        Player = mine;
        mine.IsRemote = false;
        ConfigureHuman(0);

        rival.IsPlayer = false;
        rival.IsRemote = true;
        ApplyRivalLook();
        if (!Net.IsHost)
            foreach (var k in Karts) if (k != mine && k != rival) k.IsRemote = true;

        Net.SendHello(SelectedKart);
        if (Net.IsHost) Net.SendCourse(SelectedCourse);
        if (Audio != null) Audio.Beep();
    }

    void ApplyRivalLook()
    {
        var d = KartCharacters[rivalKartIdx];
        rival.ModelVariant = rivalKartIdx;
        rival.Name = "RIVAL (" + d.name + ")";
        rival.Color = d.color;
        rival.RebuildModel();
    }

    void OnRivalKart(int sel)
    {
        rivalKartIdx = Mathf.Clamp(sel, 0, KartCharacters.Length - 1);
        if (Online && rival != null) ApplyRivalLook();
    }

    void MakeAI(Kart k, int slot)
    {
        int ai = slot < playerGridSlot ? slot : slot - 1;
        bool named = slot != playerGridSlot;
        k.IsPlayer = false;
        k.IsRemote = false;
        k.PlayerIndex = 0;
        k.ModelVariant = -1;
        k.Name = named ? AiNames[ai] : "Guest";
        k.Color = named ? AiColors[ai] : new Color(0.7f, 0.75f, 0.85f);
        k.ApplyStats(5, 5, 5, 5);
        k.RebuildModel();
    }

    void OnNetDisconnected()
    {
        foreach (var k in Karts) k.IsRemote = false;
        if (rival != null) MakeAI(rival, Karts.IndexOf(rival));
        rival = null;
        if (state == State.Title) RestoreRoles();
        else Banner("OPPONENT LEFT", new Color(1f, 0.4f, 0.3f), 0);
    }

    // クライアントとして入れ替わっていた P1 / P2 スロットを元に戻す
    void RestoreRoles()
    {
        var home = Karts[playerGridSlot];
        if (Player == home) return;
        MakeAI(Player, Karts.IndexOf(Player));
        Player = home;
        home.IsRemote = false;
        ConfigureHuman(0);
    }

    void LeaveOnline()
    {
        netMenu = false;
        Net.Disconnect();
        if (state != State.Title) ReturnToTitle();
        else RestoreRoles();
    }

    void ReturnToTitle()
    {
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        state = State.Title;
        stateTime = 0;
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

        titleDist = 0;
        if (cam != null) cam.transform.position = track.PointAt(0, 0) + Vector3.up * 20f;
    }

    void SwitchCourse(int delta)
    {
        if (Net.Busy && !Net.IsHost) return; // コースはホストが決める
        int next = (SelectedCourse + delta + Track.Courses.Length) % Track.Courses.Length;
        if (next != SelectedCourse)
        {
            if (Audio != null) Audio.Beep();
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
        if (Audio != null) Audio.Beep();
        if (HumanKart(p) != null) ConfigureHuman(p);
        if (p == 0 && Online) Net.SendHello(SelectedKart);
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
        Audio.SetMusicTempo(1f);
        Audio.SetMusicVolume(0.3f);
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

        if (Input.GetKeyDown(KeyCode.Escape) && (netMenu || Net.Busy))
        {
            LeaveOnline(); // オンライン中は一時停止できない。ESC で切断してタイトルへ戻る
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && state != State.Title)
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }
        if (paused)
        {
            if (Input.GetKeyDown(KeyCode.Q)) Application.Quit();
            if (Input.GetKeyDown(KeyCode.R)) { paused = false; Time.timeScale = 1f; AudioListener.pause = false; StartCountdown(); }
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        stateTime += dt;
        bannerTimes[0] += dt;
        bannerTimes[1] += dt;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton7);
        switch (state)
        {
            case State.Title:
                if (netMenu) break; // オンライン画面の入力中はタイトルのショートカットを無効にする
                if (Input.GetKeyDown(KeyCode.O) && !Net.Busy)
                    { netMenu = true; break; }
                if (Input.GetKeyDown(KeyCode.Tab) && !Net.Busy)
                    SetTwoPlayer(!TwoPlayer);
                else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    SwitchCourse(-1);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    SwitchCourse(1);
                else if (Input.GetKeyDown(KeyCode.W) || (!TwoPlayer && Input.GetKeyDown(KeyCode.UpArrow)))
                    SwitchKart(0, -1);
                else if (Input.GetKeyDown(KeyCode.S) || (!TwoPlayer && Input.GetKeyDown(KeyCode.DownArrow)))
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
                break;
            case State.Results:
                raceTime += dt;
                if (Time.time > finishAt + 2.5f && enter) StartRaceFromTitle();
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

        Audio.SetEngine(Player.Speed / Kart.MaxSpeed, state != State.Title, Player.Drifting && canDrive);
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
        ResetRace();
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
                if (AllHumansFinished())
                {
                    state = State.Results;
                    finishAt = Time.time;
                    Audio.SetMusicVolume(0.15f);
                }
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
        if (bestLap[p] <= 0 || lap < bestLap[p]) bestLap[p] = lap;
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

    // ───────────────────────── Camera ─────────────────────────

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (paused) return;

        // 対戦中は左右 2 分割（P1 = 左、P2 = 右）
        bool split = TwoPlayer && state != State.Title;
        cam.rect = split ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
        if (cam2.enabled != split) cam2.enabled = split;

        if (state == State.Title)
        {
            if (Player != null && track != null && track.Count > 0)
            {
                var trackPt = track.PointAt(0, 0);
                var trackDir = track.Dirs[0];
                var trackRight = track.Rights[0];

                Player.transform.position = trackPt;
                float baseHeading = Quaternion.LookRotation(trackDir).eulerAngles.y;
                Player.Heading = baseHeading - 25f + Mathf.Sin(Time.time * 1.3f) * 10f;

                // ショールームカメラ：カートの前方やや右寄りローアングルからカート正面を見上げる
                Vector3 desiredCamPos = trackPt + trackDir * 4.4f + trackRight * 1.7f + Vector3.up * 1.05f;
                Vector3 lookTarget = trackPt + Vector3.up * 0.6f;

                cam.transform.position = Vector3.Lerp(cam.transform.position, desiredCamPos, 1f - Mathf.Exp(-5f * dt));
                var targetRot = Quaternion.LookRotation(lookTarget - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRot, 1f - Mathf.Exp(-5f * dt));
                cam.fieldOfView = 48f;
            }
            return;
        }

        UpdateChaseCamera(cam, Player, 0, dt);
        if (split) UpdateChaseCamera(cam2, Player2, 1, dt);
    }

    void UpdateChaseCamera(Camera c, Kart pl, int p, float dt)
    {
        var kp = pl.transform.position;
        if (state == State.Results && Time.time > finishAt + 1.5f) camYaws[p] += 25f * dt;
        else camYaws[p] = Mathf.LerpAngle(camYaws[p], pl.Heading, 1f - Mathf.Exp(-5f * dt));

        float yaw = camYaws[p];
        float dist = 6.8f, height = 2.7f;
        if (state == State.Results) { dist = 9f; height = 5f; }
        if (state == State.Countdown)
        {
            // カウントダウン中はカートの前から後ろへ回り込む
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(stateTime / 2f));
            yaw += 180f * (1f - t);
            dist = Mathf.Lerp(5f, 6.8f, t);
        }
        var r = Quaternion.Euler(0, yaw, 0);
        var target = kp + r * new Vector3(0, height, -dist);
        bool snap = state == State.Countdown && stateTime < 0.05f;
        c.transform.position = snap ? target : Vector3.Lerp(c.transform.position, target, 1f - Mathf.Exp(-12f * dt));
        float pitchAng = pl.transform.eulerAngles.x;
        if (pitchAng > 180f) pitchAng -= 360f;
        float pitchOffset = Mathf.Clamp(pitchAng * -0.06f, -2.5f, 2.5f);
        c.transform.LookAt(kp + r * new Vector3(0, 1.1f + pitchOffset, 3.2f));
        if (shake > 0) c.transform.position += Random.insideUnitSphere * shake * shake * 0.5f;

        // 分割画面は横幅が狭いので視野角を広めに取る
        float fov = (TwoPlayer ? 70f : 62f) + Mathf.Clamp01(pl.Speed / Kart.MaxSpeed) * 4f + (pl.Boosting ? 10f : 0f);
        if (pl.IsAirborne) fov += 12f;
        c.fieldOfView = Mathf.Lerp(c.fieldOfView, fov, 1f - Mathf.Exp(-6f * dt));
    }

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
        int bid = b == sBig ? 0 : b == sMid ? 1 : 2;
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
            sBig = new GUIStyle(GUI.skin.label) { fontSize = 90, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            sBig.normal.textColor = Color.white;
            sMid = new GUIStyle(sBig) { fontSize = 38 };
            sSmall = new GUIStyle(sBig) { fontSize = 22, fontStyle = FontStyle.Bold };
            sButton = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            sField = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = 720f;

        if (state != State.Title && !TwoPlayer)
        {
            DrawVignette(Player, w, h);
            if (RaceRunning) DrawSpeedLines(w, h);
        }

        switch (state)
        {
            case State.Title: DrawTitle(w, h); if (netMenu) DrawNetMenu(w, h); break;
            case State.Countdown: DrawHuds(w, h, true); break;
            case State.Racing: DrawHuds(w, h, false); break;
            case State.Results: DrawHuds(w, h, false); if (Time.time > finishAt + 2.5f) DrawResults(w, h); break;
        }

        if (paused)
        {
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Outlined(new Rect(0, h * 0.35f, w, 100), "PAUSED", sBig, Color.white);
            Outlined(new Rect(0, h * 0.52f, w, 40), "ESC : Resume     R : Restart     Q : Quit", sSmall, Color.white);
        }
    }

    void DrawNetMenu(float w, float h)
    {
        var r = new Rect(w / 2 - 290, 110, 580, 470);
        GUI.color = new Color(0.04f, 0.07f, 0.18f, 0.97f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(r, 3, new Color(1f, 0.85f, 0.25f));
        Outlined(new Rect(r.x, r.y + 8, r.width, 50), "ONLINE BATTLE", St(sMid, 34), new Color(1f, 0.85f, 0.25f), 2);
        float x = r.x + 30, cw = r.width - 60, y = r.y + 70;
        var info = St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.85f, 0.9f, 1f));

        if (Net.Busy)
        {
            GUI.Label(new Rect(x, y, cw, 30), Net.IsHost ? "HOSTING" : "JOINING", St(sSmall, 20, TextAnchor.MiddleCenter, FontStyle.Bold, false, Color.white));
            if (!string.IsNullOrEmpty(Net.JoinCode))
            {
                GUI.Label(new Rect(x, y + 50, cw, 24), "Tell your friend this ROOM CODE:", info);
                Outlined(new Rect(x, y + 80, cw, 80), Net.JoinCode, St(sBig, 64), new Color(0.4f, 1f, 0.6f), 3);
            }
            GUI.Label(new Rect(x, y + 190, cw, 60), Net.Message, info);
            if (GUI.Button(new Rect(x + cw / 2 - 100, r.yMax - 70, 200, 42), "CANCEL", sButton)) Net.Disconnect();
            return;
        }

        // インターネット経由（Unity Relay）
        GUI.Label(new Rect(x, y, cw, 26), "INTERNET  (Unity Relay)", St(sSmall, 18, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.5f, 0.85f, 1f)));
        if (GUI.Button(new Rect(x, y + 32, 250, 40), "HOST ROOM", sButton)) Net.HostRelay();
        joinCodeInput = GUI.TextField(new Rect(x + 270, y + 32, 120, 40), joinCodeInput, 8, sField).ToUpperInvariant();
        if (GUI.Button(new Rect(x + 400, y + 32, cw - 400, 40), "JOIN", sButton)) Net.JoinRelay(joinCodeInput);

        // IP 直接接続（LAN / VPN）
        y += 110;
        GUI.Label(new Rect(x, y, cw, 26), "DIRECT IP  (LAN / VPN / port " + NetSession.DefaultPort + " open)", St(sSmall, 18, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.5f, 0.85f, 1f)));
        if (GUI.Button(new Rect(x, y + 32, 250, 40), "HOST (LISTEN)", sButton)) Net.HostDirect();
        ipInput = GUI.TextField(new Rect(x + 270, y + 32, 150, 40), ipInput, 40, sField);
        if (GUI.Button(new Rect(x + 430, y + 32, cw - 430, 40), "JOIN", sButton)) Net.JoinDirect(ipInput);

        if (!string.IsNullOrEmpty(Net.Message))
            GUI.Label(new Rect(x, y + 90, cw, 50), Net.Message, St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(1f, 0.5f, 0.4f)));
        GUI.Label(new Rect(x, r.yMax - 120, cw, 48), "Host picks the course and starts the race. 1-vs-1 online, CPU karts fill the grid.", info);
        if (GUI.Button(new Rect(x + cw / 2 - 100, r.yMax - 62, 200, 40), "CLOSE  [ESC]", sButton)) netMenu = false;
    }

    // 1P は全画面、2P は左右それぞれのビューにクリップして HUD・バナー・ビネットを描く
    void DrawHuds(float w, float h, bool countdown)
    {
        if (!TwoPlayer)
        {
            DrawHud(Player, w, h, false, 0);
            if (countdown) DrawCountdown(w, h);
            DrawBanner(0, w, h);
            return;
        }

        float hw = w * 0.5f;
        for (int p = 0; p < 2; p++)
        {
            GUI.BeginClip(new Rect(p * hw, 0, hw, h));
            DrawVignette(HumanKart(p), hw, h);
            DrawHud(HumanKart(p), hw, h, true, p);
            if (countdown) DrawCountdown(hw, h);
            DrawBanner(p, hw, h);
            GUI.EndClip();
        }
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(hw - 2f, 0, 4f, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawBanner(int p, float w, float h)
    {
        float t = bannerTimes[p];
        if (t >= 1.6f) return;
        float a = Mathf.Clamp01((1.6f - t) / 0.3f);
        float pop = 1f + Mathf.Max(0, 0.3f - t) * 1.5f;
        var st = St(sBig, (int)((TwoPlayer ? 54 : 72) * pop));
        var c = bannerColors[p]; c.a = a;
        Outlined(new Rect(0, h * 0.28f, w, 120), bannerTexts[p], st, c);
    }

    void DrawTitle(float w, float h)
    {
        float t = Time.time;

        // 1. トップ：TURBO CIRCUIT ポップロゴ
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
            float bSize = 138f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 48, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        var courseTitleStyle = St(sSmall, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, new Color(0.12f, 0.18f, 0.35f));
        GUI.Label(new Rect(leftX + 8, cardY + 192, cardW - 16, 28), $"< {curDef.Name.ToUpper()} >", courseTitleStyle);

        string diffStr = SelectedCourse == 0 ? "★☆☆  NOVICE" : SelectedCourse == 1 ? "★★☆  ADVANCED" : SelectedCourse == 2 ? "★★★  EXPERT" : "★★☆  URBAN";
        Color diffBg = SelectedCourse == 0 ? new Color(0.2f, 0.78f, 0.42f) : SelectedCourse == 1 ? new Color(1f, 0.65f, 0.15f) : SelectedCourse == 2 ? new Color(1f, 0.28f, 0.38f) : new Color(0.62f, 0.3f, 0.95f);
        DrawPopPill(new Rect(leftX + (cardW - 140) * 0.5f, cardY + 224, 140, 22), diffStr, diffBg);

        var descStyle = St(sSmall, 13, TextAnchor.UpperCenter, FontStyle.Normal, true, new Color(0.26f, 0.30f, 0.42f));
        GUI.Label(new Rect(leftX + 18, cardY + 254, cardW - 36, 85), curDef.Description, descStyle);

        DrawPopPill(new Rect(leftX + (cardW - 200) * 0.5f, cardY + 360, 200, 26), $"{totalLaps} LAPS   |   8 KARTS GP", new Color(0.18f, 0.52f, 0.88f));


        // 3. 右カード：ドライバー＆マシンセレクター（DRIVER & MACHINE）
        float rightX = w - cardW - 35f;
        DrawPopCard(new Rect(rightX, cardY, cardW, cardH));
        DrawPopRibbon(new Rect(rightX + 15, cardY + 12, cardW - 30, 32), ribbonDriverTex, TwoPlayer ? "◄ P1 DRIVER [W][S] ►" : "◄ DRIVER & KART [W][S] ►");

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
        GUI.Label(new Rect(rightX + 10, cardY + 365, cardW - 20, 20), TwoPlayer ? "P1: [W][S]   P2: [UP][DOWN]" : "Press [W][S] to switch machine", switchGuide);

        // 2P：P2 のマシン選択
        if (TwoPlayer)
        {
            var c2 = KartCharacters[SelectedKart2];
            Outlined(new Rect(rightX + 8, cardY + 296, cardW - 16, 28), $"P2  < {c2.name} >", St(sSmall, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), c2.color, 2);
            DrawPopPill(new Rect(rightX + 22, cardY + 328, cardW - 44, 24), c2.trait, c2.color * 0.9f);
        }

        // プレイ人数の切り替え
        var modeBg = TwoPlayer ? new Color(0.95f, 0.3f, 0.5f) : new Color(0.18f, 0.52f, 0.88f);
        if (Online)
            DrawPopPill(new Rect((w - 340) * 0.5f, 556f, 340, 28), Net.IsHost ? "ONLINE  VS  RIVAL  (YOU = HOST)" : "ONLINE  VS  RIVAL  (HOST DECIDES START)", new Color(0.18f, 0.7f, 0.4f));
        else
            DrawPopPill(new Rect((w - 300) * 0.5f, 556f, 300, 28), TwoPlayer ? "[TAB]  2 PLAYERS  (SPLIT SCREEN)" : "[TAB]  1 PLAYER", modeBg);


        // 4. 画面中央下部：PRESS ENTER TO RACE（ぷっくり立体キャンディボタン）
        float pulse = (Mathf.Sin(t * 6f) + 1f) * 0.5f;
        float btnW = 440f + pulse * 10f;
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
            ? "P1: [WASD] [SPACE] drift [E] item   •   P2: [ARROWS] [R-SHIFT] drift [R-CTRL] item   •   [TAB] 1P/2P   •   [ESC] Pause"
            : "[W][S] Driver   •   [A][D] Track   •   [SPACE] Drift / Hop   •   [E] Item   •   [TAB] 2P Battle   •   [O] Online   •   [ESC] Pause";
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
        GUI.Label(new Rect(x, y, 70, 16), label, lblStyle);

        float barX = x + 72;
        float barW = w - 72;
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
    }

    void DrawCountdown(float w, float h)
    {
        float t = stateTime - 1f;
        if (t < 0) return;
        int n = 3 - Mathf.FloorToInt(t);
        float frac = t - Mathf.Floor(t);
        string text = n > 0 ? n.ToString() : "GO!";
        var st = St(sBig, (int)(150 * (1.3f - frac * 0.3f)));
        var c = n > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f);
        c.a = 1f - frac * 0.3f;
        Outlined(new Rect(0, h * 0.25f, w, 200), text, st, c, 5);
    }

    void DrawHud(Kart pl, float w, float h, bool compact, int pi)
    {
        // アイテム枠
        var slot = new Rect(24, 20, 114, 114);
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.8f);
        GUI.DrawTexture(slot, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(slot, 3, new Color(1f, 0.85f, 0.25f));

        ItemType shown = pl.Item;
        if (pl.RouletteTimer > 0) shown = (ItemType)(1 + (int)(Time.time * 16f) % 4);
        if (shown != ItemType.None)
        {
            Texture2D icon = null;
            Color ic; string label;
            switch (shown)
            {
                case ItemType.Turbo: icon = iconTurbo; ic = new Color(1f, 0.55f, 0.1f); label = "TURBO"; break;
                case ItemType.Banana: icon = iconBanana; ic = new Color(1f, 0.9f, 0.2f); label = "BANANA"; break;
                case ItemType.Missile: icon = iconMissile; ic = new Color(1f, 0.2f, 0.2f); label = "MISSILE"; break;
                default: icon = iconShield; ic = Color.HSVToRGB(Mathf.Repeat(Time.time, 1f), 0.6f, 1f); label = "SHIELD"; break;
            }

            if (icon != null)
            {
                // 生成した3Dアイコン画像の描画
                GUI.DrawTexture(new Rect(slot.x + 8, slot.y + 8, slot.width - 16, slot.height - 16), icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // フォールバック
                GUI.color = ic;
                GUI.DrawTexture(new Rect(slot.x + 12, slot.y + 12, slot.width - 24, 58), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var ls = St(sSmall, 20);
                Outlined(new Rect(slot.x, slot.y + 12, slot.width, 58), label, ls, Color.white, 2);
            }

            if (pl.RouletteTimer <= 0)
                Outlined(new Rect(slot.x, slot.yMax - 22, slot.width, 20), "[E]", St(sSmall, 15), new Color(1f, 1f, 0.3f), 1);
        }

        // 周回とタイム
        var right = St(sMid, 0, TextAnchor.UpperRight);
        int lap = Mathf.Clamp(pl.MaxLap, 1, totalLaps);
        Outlined(new Rect(w - 324, 18, 300, 50), "LAP " + lap + "/" + totalLaps, right, Color.white);
        var rs = St(sSmall, 0, TextAnchor.UpperRight);
        Outlined(new Rect(w - 324, 66, 300, 30), FormatTime(pl.Finished ? pl.FinishTime : raceTime), rs, Color.white);
        if (bestLap[pi] > 0) Outlined(new Rect(w - 324, 94, 300, 30), "BEST LAP " + FormatTime(bestLap[pi]), St(sSmall, 16, TextAnchor.UpperRight), new Color(1f, 0.9f, 0.5f));

        // 順位
        string place = Ordinal(pl.Place);
        var ps = St(sBig, 120, TextAnchor.LowerRight);
        Color pc = pl.Place == 1 ? new Color(1f, 0.85f, 0.15f) : pl.Place <= 3 ? new Color(0.85f, 0.9f, 1f) : Color.white;
        Outlined(new Rect(w - 324, h - 170, 300, 150), place, ps, pc, 5);

        // スピード
        var ss = compact ? St(sMid, 30, TextAnchor.LowerLeft) : St(sMid, 0, TextAnchor.LowerCenter);
        Outlined(compact ? new Rect(176, h - 70, 230, 50) : new Rect(w / 2 - 150, h - 70, 300, 50), Mathf.RoundToInt(Mathf.Abs(pl.Speed) * 3.6f) + " km/h", ss, Color.white);

        // ミニマップ
        var mm = compact ? new Rect(10, h - 170, 156, 156) : new Rect(20, h - 250, 230, 230);
        GUI.color = new Color(0, 0, 0, 0.3f);
        GUI.DrawTexture(mm, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(mm, minimap);
        int playerIdx = Karts.IndexOf(pl);
        for (int n = 1; n <= Karts.Count; n++) // プレイヤーを最後（最前面）に描く
        {
            var k = Karts[(playerIdx + n) % Karts.Count];
            var uv = toMap(k.transform.position);
            var p = new Vector2(mm.x + uv.x * mm.width, mm.y + (1f - uv.y) * mm.height);
            float s = k == pl ? 14f : k.IsPlayer ? 12f : 10f;
            GUI.color = k == pl ? Color.white : k.IsPlayer ? new Color(1f, 0.85f, 0.2f) : Color.black;
            GUI.DrawTexture(new Rect(p.x - s / 2 - 2, p.y - s / 2 - 2, s + 4, s + 4), Texture2D.whiteTexture);
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // 逆走
        if (RaceRunning && !pl.Finished && Vector3.Dot(pl.Forward, track.Dirs[pl.Index]) < -0.3f && pl.Speed > 3f && Mathf.Repeat(Time.time, 0.6f) < 0.4f)
            Outlined(new Rect(0, h * 0.42f, w, 80), "WRONG WAY!", St(sBig, compact ? 44 : 60), new Color(1f, 0.3f, 0.3f));
    }

    void DrawResults(float w, float h)
    {
        var panel = new Rect(w / 2 - 280, 110, 560, 470);
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(panel, 3, new Color(1f, 0.85f, 0.2f));
        string head = "RESULTS";
        if (TwoPlayer) head = Player.Place < Player2.Place ? "P1 WINS!" : "P2 WINS!";
        else if (Online && rival != null) head = Player.Place < rival.Place ? "YOU WIN!" : "YOU LOSE...";
        Outlined(new Rect(panel.x, panel.y + 10, panel.width, 60), head, sMid, new Color(1f, 0.85f, 0.2f));

        var order = placeOrder;
        var left = St(sSmall, 0, TextAnchor.MiddleLeft);
        var rightS = St(sSmall, 0, TextAnchor.MiddleRight);
        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            var row = new Rect(panel.x + 30, panel.y + 80 + i * 44, panel.width - 60, 40);
            if (k.IsPlayer || (Online && k == rival))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.18f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(row.x + 70, row.y + 10, 20, 20), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Outlined(new Rect(row.x + 8, row.y, 60, 40), Ordinal(k.Place), left, i == 0 ? new Color(1f, 0.85f, 0.2f) : Color.white, 1);
            Outlined(new Rect(row.x + 104, row.y, 200, 40), k.Name, left, Color.white, 1);
            Outlined(new Rect(row.x, row.y, row.width - 10, 40), k.Finished ? FormatTime(k.FinishTime) : "--:--.--", rightS, Color.white, 1);
        }
        if (Mathf.Repeat(Time.time, 1.1f) < 0.75f)
            Outlined(new Rect(0, panel.yMax + 20, w, 50), "PRESS ENTER TO RACE AGAIN", St(sMid, 30), Color.white);
    }

    static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");

    static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return string.Format("{0}:{1:00.00}", m, s);
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

    void EnsureMaterials()
    {
        if (skidmarkMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            skidmarkMaterial = new Material(shader) { mainTexture = TextureGen.TireMark() };
        }
        if (headlightMaterial == null)
        {
            var shader = Shader.Find("Standard");
            headlightMaterial = new Material(shader);
            headlightMaterial.SetColor("_Color", new Color(1f, 1f, 0.95f));
            headlightMaterial.EnableKeyword("_EMISSION");
            headlightMaterial.SetColor("_EmissionColor", new Color(1.4f, 1.4f, 1.1f));
        }
        if (taillightMaterial == null)
        {
            var shader = Shader.Find("Standard");
            taillightMaterial = new Material(shader);
            taillightMaterial.SetColor("_Color", new Color(0.9f, 0.1f, 0.1f));
            taillightMaterial.EnableKeyword("_EMISSION");
            taillightMaterial.SetColor("_EmissionColor", new Color(0.8f, 0.05f, 0.05f));
        }
        if (bannerMaterial == null)
        {
            var shader = Shader.Find("Standard");
            bannerMaterial = new Material(shader);
            bannerMaterial.SetColor("_Color", Color.white);
            bannerMaterial.SetFloat("_Glossiness", 0.4f);
        }
    }

    void DrawVignette(Kart pl, float w, float h)
    {
        if (vignetteTex == null) return;
        float speed01 = pl != null ? Mathf.Clamp01(pl.Speed / Kart.MaxSpeed) : 0f;
        bool boost = pl != null && pl.Boosting;
        float boostBonus = boost ? 0.35f : 0f;
        float alpha = Mathf.Clamp01(0.18f + speed01 * 0.25f + boostBonus);

        // ブースト時は周辺にサイバーブルーのエネルギーグローを薄く付加
        if (boost)
        {
            GUI.color = new Color(0.2f, 0.75f, 1f, alpha * 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), vignetteTex, ScaleMode.StretchToFill);
        }

        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(0, 0, w, h), vignetteTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    void DrawSpeedLines(float w, float h)
    {
        if (Player == null) return;
        float speedRatio = Mathf.Clamp01(Player.Speed / Kart.MaxSpeed);
        bool boost = Player.Boosting;
        if (speedRatio < 0.38f && !boost) return;

        // 速度比率 0.38〜1.0 を 0〜1 に正規化。ブースト時は 1.4
        float speedIntensity = Mathf.Clamp01((speedRatio - 0.38f) / 0.62f);
        float intensity = boost ? 1.4f : speedIntensity;
        if (intensity <= 0.01f) return;

        float scale = Screen.height / 720f;
        Matrix4x4 baseMatrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        // 消失点（Focus Point）: 画面中央・道路奥に安定配置
        float steerTilt = Player != null ? Mathf.Clamp(Player.Lateral * 2f, -22f, 22f) : 0f;
        Vector2 focus = new Vector2(w * 0.5f + steerTilt, h * 0.46f);

        float time = Time.time;
        var strokeTex = speedLineTex != null ? speedLineTex : Texture2D.whiteTexture;

        // ─────────────────────────────────────────────
        // レイヤー1: 流れる高速ウィンドストリーム（滑らかに外から内へ疾走する空気の筋）
        // ─────────────────────────────────────────────
        int streamCount = (int)Mathf.Lerp(18f, boost ? 42f : 32f, intensity);
        float streamSpeed = 3.5f + speedRatio * 3.8f + (boost ? 5.2f : 0f);

        for (int i = 0; i < streamCount; i++)
        {
            float baseAngle = (i / (float)streamCount) * Mathf.PI * 2f;
            float seed = i * 137.5f;
            float angle = baseAngle + Mathf.Sin(seed) * 0.12f;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // 画面境界までの距離
            float tx = cos > 0.0001f ? (w - focus.x) / cos : (cos < -0.0001f ? -focus.x / cos : 99999f);
            float ty = sin > 0.0001f ? (h - focus.y) / sin : (sin < -0.0001f ? -focus.y / sin : 99999f);
            float rEdge = Mathf.Min(tx, ty);
            if (rEdge < 30f) continue;

            // 視界保護セーフゾーン（楕円：横170px, 縦110px）
            float rSafe = Mathf.Sqrt(Mathf.Pow(cos * 170f, 2f) + Mathf.Pow(sin * 110f, 2f));

            // 外側から中心へ流れるフェーズ
            float phaseOffset = Mathf.Repeat(seed * 0.317f, 1f);
            float flow = Mathf.Repeat(time * streamSpeed * (0.85f + (i % 4) * 0.1f) + phaseOffset, 1f);

            // 画面外枠からセーフゾーン手前まで流れる
            float maxLineLen = Mathf.Lerp(150f, 320f, intensity) * (0.75f + (i % 3) * 0.25f);
            float currentR = Mathf.Lerp(rEdge + maxLineLen * 0.3f, rSafe + 30f, flow);
            float headR = currentR - maxLineLen;

            float alphaMod = 1f;
            if (currentR > rEdge) alphaMod *= Mathf.Clamp01((rEdge + maxLineLen * 0.3f - currentR) / (maxLineLen * 0.3f));
            if (headR < rSafe) alphaMod *= Mathf.Clamp01((headR - rSafe * 0.6f) / (rSafe * 0.4f));
            if (alphaMod <= 0.01f) continue;

            Vector2 p1 = new Vector2(focus.x + cos * currentR, focus.y + sin * currentR);
            Vector2 p2 = new Vector2(focus.x + cos * Mathf.Max(headR, rSafe * 0.5f), focus.y + sin * Mathf.Max(headR, rSafe * 0.5f));

            float thick = Mathf.Lerp(2.5f, boost ? 5.2f : 3.8f, intensity);

            Color col;
            if (boost)
            {
                col = (i % 3 == 0)
                    ? new Color(1f, 0.80f, 0.2f, 0.90f * intensity * alphaMod)
                    : (i % 3 == 1)
                        ? new Color(0.2f, 0.92f, 1f, 0.90f * intensity * alphaMod)
                        : new Color(1f, 1f, 1f, 0.95f * intensity * alphaMod);
            }
            else
            {
                // 明るい背景でもクッキリ見えるよう、鮮やかなシアンと白をブレンド
                col = (i % 2 == 0)
                    ? new Color(0.35f, 0.88f, 1f, 0.85f * intensity * alphaMod)
                    : new Color(1f, 1f, 1f, 0.80f * intensity * alphaMod);
            }

            DrawSpeedStroke(p1, p2, thick, col, strokeTex, baseMatrix);
        }

        // ─────────────────────────────────────────────
        // レイヤー2: 迫力の集中線（ダイナミック・インパクトスパイク）
        // ─────────────────────────────────────────────
        int spikeCount = (int)Mathf.Lerp(28f, boost ? 80f : 56f, intensity);
        int timeSlot = (int)(time * 22f); // 22Hzでリズミカルに切り替わる

        for (int i = 0; i < spikeCount; i++)
        {
            int hash = (i * 265443576 + timeSlot * 8235729) & 0x7FFFFFFF;
            float r0 = (hash % 1000) / 1000f;
            float r1 = ((hash / 1000) % 1000) / 1000f;
            float r2 = ((hash / 1000000) % 1000) / 1000f;

            // 角度の分散（360度全体に均一に分散）
            float baseAngle = (i / (float)spikeCount) * Mathf.PI * 2f;
            float angle = baseAngle + (r0 - 0.5f) * (Mathf.PI * 2f / spikeCount) * 1.15f;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float tx = cos > 0.0001f ? (w - focus.x) / cos : (cos < -0.0001f ? -focus.x / cos : 99999f);
            float ty = sin > 0.0001f ? (h - focus.y) / sin : (sin < -0.0001f ? -focus.y / sin : 99999f);
            float rEdge = Mathf.Min(tx, ty);
            if (rEdge < 30f) continue;

            // 視界保護セーフゾーン
            float rSafe = Mathf.Sqrt(Mathf.Pow(cos * 160f, 2f) + Mathf.Pow(sin * 100f, 2f));

            // 外枠から中心方向へ伸びる長さ（3層のバリエーション）
            float rOuter = rEdge + 12f;
            float tier = (hash >> 5) % 3;
            float reach = tier == 0
                ? Mathf.Lerp(0.28f, 0.48f, intensity) * (0.8f + r1 * 0.4f)
                : tier == 1
                    ? Mathf.Lerp(0.50f, 0.75f, intensity) * (0.85f + r1 * 0.3f)
                    : Mathf.Lerp(0.75f, boost ? 0.96f : 0.90f, intensity);

            float rInner = Mathf.Max(rSafe, rOuter - (rOuter - rSafe) * reach);

            Vector2 p1 = new Vector2(focus.x + cos * rOuter, focus.y + sin * rOuter);
            Vector2 p2 = new Vector2(focus.x + cos * rInner, focus.y + sin * rInner);

            float thick = Mathf.Lerp(3.2f, boost ? 8.5f : 6.0f, intensity) * (0.65f + r2 * 0.7f);

            Color col;
            if (boost)
            {
                int kind = (hash >> 3) % 4;
                switch (kind)
                {
                    case 0: col = new Color(1f, 0.70f, 0.15f, 0.95f * intensity); break; // 炎ゴールド
                    case 1: col = new Color(0.20f, 0.95f, 1f, 0.95f * intensity); break;  // ネオンシアン
                    case 2: col = new Color(1f, 0.98f, 0.55f, 0.98f * intensity); break; // 高輝度イエロー
                    default: col = new Color(1f, 1f, 1f, 1.0f); break;                   // 白熱コア
                }
            }
            else
            {
                float a = Mathf.Clamp01(0.75f * intensity + r2 * 0.25f);
                col = (i % 3 == 0)
                    ? new Color(0.3f, 0.9f, 1f, a)
                    : (i % 3 == 1)
                        ? new Color(0.85f, 0.96f, 1f, a)
                        : new Color(1f, 1f, 1f, a);
            }

            DrawSpeedStroke(p1, p2, thick, col, strokeTex, baseMatrix);
        }

        GUI.matrix = baseMatrix;
    }

    void DrawSpeedStroke(Vector2 a, Vector2 b, float thickness, Color col, Texture2D tex, Matrix4x4 baseMatrix)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 1f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var prev = GUI.color;
        GUI.color = col;
        GUI.matrix = baseMatrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0, 0, ang), Vector3.one);
        GUI.DrawTexture(new Rect(0, -thickness * 0.5f, len, thickness), tex);
        GUI.color = prev;
    }

    void LoadUIAssets()
    {
        string iconDir = Application.dataPath + "/Resources/Icons/";
        iconTurbo = LoadTexture("Icons/item_turbo", iconDir + "item_turbo.jpg");
        iconBanana = LoadTexture("Icons/item_banana", iconDir + "item_banana.jpg");
        iconMissile = LoadTexture("Icons/item_missile", iconDir + "item_missile.jpg");
        iconShield = LoadTexture("Icons/item_shield", iconDir + "item_shield.jpg");

        string uiDir = Application.dataPath + "/Resources/UI/";
        titleLogoTex = LoadTexture("UI/title_logo_pop", uiDir + "title_logo_pop.png") ?? LoadTexture("UI/title_logo", uiDir + "title_logo.png");
        for (int i = 0; i < trackBadgeTex.Length; i++)
            trackBadgeTex[i] = LoadTexture($"UI/badge_track_{i}_pop", uiDir + $"badge_track_{i}_pop.png") ?? LoadTexture($"UI/badge_track_{i}", uiDir + $"badge_track_{i}.png");
        if (trackBadgeTex[3] == null) trackBadgeTex[3] = TextureGen.CityBadge();

        cardPopTex = LoadTexture("UI/ui_card_pop", uiDir + "ui_card_pop.png");
        btnRaceTex = LoadTexture("UI/ui_btn_race", uiDir + "ui_btn_race.png");
        ribbonTrackTex = LoadTexture("UI/ui_ribbon_track", uiDir + "ui_ribbon_track.png");
        ribbonDriverTex = LoadTexture("UI/ui_ribbon_driver", uiDir + "ui_ribbon_driver.png");
        cardPanelTex = TextureGen.CardPanel(340, 400, new Color(0.04f, 0.08f, 0.18f, 0.88f), new Color(0.02f, 0.04f, 0.10f, 0.94f), new Color(0.2f, 0.7f, 1f, 0.85f), 2);
    }

    public static Texture2D LoadTexture(string resPath, string diskPath)
    {
        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null) return tex;
        if (System.IO.File.Exists(diskPath))
        {
            var bytes = System.IO.File.ReadAllBytes(diskPath);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (t.LoadImage(bytes)) return t;
        }
        return null;
    }

    void QuitAfterScreenshot() => Application.Quit();
}
