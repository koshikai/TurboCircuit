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
    // 起動オプション -demo：自動スタートし、プレイヤーも CPU が運転する（動作確認用）
    public bool Demo { get; private set; }
    bool screenshotRequested, screenshotTaken, titleShotRequested;
    string customShotName;
    float shotDelay = 4.2f;

    Track track;
    readonly List<ItemBox> boxes = new List<ItemBox>();
    readonly List<Banana> bananas = new List<Banana>();
    readonly List<Missile> missiles = new List<Missile>();
    Font font;

    float stateTime, raceTime, playerLapStart, lastLapTime, bestLapTime, shake, titleDist, camYaw, finishAt;
    int lastCountdownBeep;
    string bannerText; Color bannerColor; float bannerTime = 99f;

    Camera cam;
    Texture2D minimap;
    Texture2D vignetteTex;
    Texture2D iconTurbo, iconBanana, iconMissile, iconShield;
    Texture2D titleLogoTex;
    Texture2D[] trackBadgeTex = new Texture2D[3];
    Texture2D cardPanelTex;
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

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 62f;
        cam.farClipPlane = 1500f;
        cam.nearClipPlane = 0.2f;
        camGo.AddComponent<AudioListener>();

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
            k.Init(this, track, kName, kCol, isPlayer, idx, lat, 0.95f);
            Karts.Add(k);
            if (isPlayer) Player = k;
        }

        LoadCourse(cIdx);
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
        int next = (SelectedCourse + delta + Track.Courses.Length) % Track.Courses.Length;
        if (next != SelectedCourse)
        {
            if (Audio != null) Audio.Beep();
            LoadCourse(next);
        }
    }

    void SwitchKart(int delta)
    {
        int next = (SelectedKart + delta + KartCharacters.Length) % KartCharacters.Length;
        if (next != SelectedKart)
        {
            SelectedKart = next;
            if (Audio != null) Audio.Beep();
            if (Player != null)
            {
                var def = KartCharacters[SelectedKart];
                Player.Name = "YOU (" + def.name + ")";
                Player.Color = def.color;
                Player.RebuildModel();
                Player.Hop();
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
            int j = Random.Range(0, skills.Count);
            k.SetSkill(skills[j]);
            skills.RemoveAt(j);
        }

        raceTime = 0;
        playerLapStart = 0;
        lastLapTime = 0;
        bestLapTime = 0;
        lastCountdownBeep = -1;
        camYaw = Player.Heading;
        Audio.SetMusicTempo(1f);
        Audio.SetMusicVolume(0.3f);
        bannerTime = 99f;
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
        if (screenshotRequested && state == State.Racing && stateTime > shotDelay && !screenshotTaken)
        {
            screenshotTaken = true;
            string fname = string.IsNullOrEmpty(customShotName) ? $"screenshot_course{SelectedCourse}.png" : customShotName;
            ScreenCapture.CaptureScreenshot(fname);
            Invoke(nameof(QuitAfterScreenshot), 0.4f);
        }
        if ((screenshotRequested || titleShotRequested) && (stateTime + raceTime) > 30f) Application.Quit();

        if (Input.GetKeyDown(KeyCode.Escape) && state != State.Title)
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
        bannerTime += dt;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton7);
        switch (state)
        {
            case State.Title:
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                    SwitchCourse(-1);
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                    SwitchCourse(1);
                else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                    SwitchKart(-1);
                else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                    SwitchKart(1);

                if (!titleShotRequested && (enter || Input.GetKeyDown(KeyCode.Space) || (screenshotRequested && stateTime > 0.6f) || (Demo && stateTime > 2.5f))) StartCountdown();
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
                if (Time.time > finishAt + 2.5f && enter) StartCountdown();
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

        Audio.SetEngine(Player.Speed / Kart.MaxSpeed, state != State.Title, Player.Drifting && canDrive);
        shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
    }

    void StartCountdown()
    {
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
            if (k == Player)
            {
                Audio.Finish();
                Banner("FINISH!", Color.white);
                state = State.Results;
                finishAt = Time.time;
                Audio.SetMusicVolume(0.15f);
                RecordLap();
            }
            return;
        }
        if (k != Player || k.Lap < 2) return;

        RecordLap();
        if (k.Lap == totalLaps)
        {
            Banner("FINAL LAP!", new Color(1f, 0.3f, 0.3f));
            Audio.FinalLap();
            Audio.SetMusicTempo(1.1f);
        }
        else
        {
            Banner("LAP " + k.Lap + "/" + totalLaps, Color.white);
            Audio.Lap();
        }
    }

    void RecordLap()
    {
        lastLapTime = raceTime - playerLapStart;
        playerLapStart = raceTime;
        if (bestLapTime <= 0 || lastLapTime < bestLapTime) bestLapTime = lastLapTime;
    }

    public void Banner(string text, Color color)
    {
        bannerText = text;
        bannerColor = color;
        bannerTime = 0;
    }

    public void Shake(float s) => shake = Mathf.Max(shake, s);

    void UpdatePlaces()
    {
        var order = Karts.OrderBy(k => k.Finished ? 0 : 1)
                         .ThenBy(k => k.Finished ? k.FinishTime : -k.RaceDistance)
                         .ToList();
        for (int i = 0; i < order.Count; i++) order[i].Place = i + 1;
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
            a.transform.position += n * (min - m) * 0.5f;
            b.transform.position -= n * (min - m) * 0.5f;

            if (a.Shielded && !b.Shielded) { b.Spin(); continue; }
            if (b.Shielded && !a.Shielded) { a.Spin(); continue; }

            // 押し合い：横方向に少し弾く
            a.VelDir = (a.VelDir + n * 0.25f).normalized;
            b.VelDir = (b.VelDir - n * 0.25f).normalized;
            a.Speed *= 0.985f;
            b.Speed *= 0.985f;
            if ((a == Player || b == Player) && Random.value < 0.15f) Audio.Bump();
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
                if (k.GiveItem(RollItem(k)) && k == Player) Audio.Pickup();
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
                if (k == b.Owner && !b.Armed) continue;
                var d = k.transform.position - b.transform.position;
                d.y = 0;
                if (d.magnitude > 1.5f) continue;
                if (k.Shielded || k.Spin())
                {
                    Fx.Burst(b.transform.position, new Color(1f, 0.95f, 0.3f), 12, 5f, 0.4f);
                    bananas.RemoveAt(i);
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
        var b = new GameObject("Banana").AddComponent<Banana>();
        b.Init(pos, owner, bananaMaterial);
        bananas.Add(b);
    }

    public void SpawnMissile(Kart owner)
    {
        var target = Karts.FirstOrDefault(k => k.Place == owner.Place - 1);
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

    // ───────────────────────── Camera ─────────────────────────

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (paused) return;

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

        var kp = Player.transform.position;
        if (state == State.Results && Time.time > finishAt + 1.5f) camYaw += 25f * dt;
        else camYaw = Mathf.LerpAngle(camYaw, Player.Heading, 1f - Mathf.Exp(-5f * dt));

        float yaw = camYaw;
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
        float pitchAng = Player.transform.eulerAngles.x;
        if (pitchAng > 180f) pitchAng -= 360f;
        float pitchOffset = Mathf.Clamp(pitchAng * -0.06f, -2.5f, 2.5f);
        cam.transform.LookAt(kp + r * new Vector3(0, 1.1f + pitchOffset, 3.2f));
        if (shake > 0) cam.transform.position += Random.insideUnitSphere * shake * shake * 0.5f;

        float fov = 62f + Mathf.Clamp01(Player.Speed / Kart.MaxSpeed) * 4f + (Player.Boosting ? 10f : 0f);
        if (Player != null && Player.IsAirborne) fov += 12f;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * dt));
    }

    // ───────────────────────── UI ─────────────────────────

    GUIStyle sBig, sMid, sSmall;

    void OnGUI()
    {
        if (sBig == null)
        {
            sBig = new GUIStyle(GUI.skin.label) { fontSize = 90, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            sBig.normal.textColor = Color.white;
            sMid = new GUIStyle(sBig) { fontSize = 38 };
            sSmall = new GUIStyle(sBig) { fontSize = 22, fontStyle = FontStyle.Bold };
        }
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = 720f;

        if (state != State.Title)
        {
            DrawVignette(w, h);
            if (RaceRunning) DrawSpeedLines(w, h);
        }

        switch (state)
        {
            case State.Title: DrawTitle(w, h); break;
            case State.Countdown: DrawHud(w, h); DrawCountdown(w, h); break;
            case State.Racing: DrawHud(w, h); break;
            case State.Results: DrawHud(w, h); if (Time.time > finishAt + 2.5f) DrawResults(w, h); break;
        }

        if (bannerTime < 1.6f)
        {
            float a = Mathf.Clamp01((1.6f - bannerTime) / 0.3f);
            float pop = 1f + Mathf.Max(0, 0.3f - bannerTime) * 1.5f;
            var st = new GUIStyle(sBig) { fontSize = (int)(72 * pop) };
            var c = bannerColor; c.a = a;
            Outlined(new Rect(0, h * 0.28f, w, 120), bannerText, st, c);
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

    void DrawTitle(float w, float h)
    {
        float t = Time.time;

        // 1. トップ：TURBO CIRCUIT アーケードロゴ
        if (titleLogoTex != null)
        {
            float logoW = 360f;
            float logoH = logoW * (titleLogoTex.height / (float)titleLogoTex.width);
            float logoY = 8f + Mathf.Sin(t * 2.2f) * 2f;
            GUI.DrawTexture(new Rect((w - logoW) * 0.5f, logoY, logoW, logoH), titleLogoTex, ScaleMode.ScaleToFit);
        }
        else
        {
            var title = new GUIStyle(sBig) { fontSize = 76 };
            Outlined(new Rect(0, 16, w, 80), "TURBO CIRCUIT", title, Color.HSVToRGB(Mathf.Repeat(t * 0.1f, 1f), 0.6f, 1f), 5);
        }

        float cardY = 175f;
        float cardH = 410f;
        float cardW = 330f;

        // 2. 左カード：コースセレクター（TRACK SELECTION）
        float leftX = 35f;
        DrawCard(new Rect(leftX, cardY, cardW, cardH));
        DrawHeaderRibbon(new Rect(leftX + 10, cardY + 10, cardW - 20, 30), "◄ TRACK SELECT [A][D] ►", new Color(0.15f, 0.75f, 1f));

        var curDef = Track.Courses[SelectedCourse];
        if (trackBadgeTex[SelectedCourse] != null)
        {
            float bSize = 138f;
            GUI.DrawTexture(new Rect(leftX + (cardW - bSize) * 0.5f, cardY + 48, bSize, bSize), trackBadgeTex[SelectedCourse], ScaleMode.ScaleToFit);
        }

        var courseTitleStyle = new GUIStyle(sSmall) { fontSize = 21, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
        Outlined(new Rect(leftX + 8, cardY + 194, cardW - 16, 28), $"< {curDef.Name.ToUpper()} >", courseTitleStyle, new Color(1f, 0.9f, 0.2f), 2);

        string diffStr = SelectedCourse == 0 ? "★☆☆  NOVICE" : SelectedCourse == 1 ? "★★☆  ADVANCED" : "★★★  EXPERT";
        Color diffCol = SelectedCourse == 0 ? new Color(0.35f, 1f, 0.45f) : SelectedCourse == 1 ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.35f, 0.35f);
        var subStyle = new GUIStyle(sSmall) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        Outlined(new Rect(leftX + 10, cardY + 225, cardW - 20, 20), diffStr, subStyle, diffCol, 1);

        var descStyle = new GUIStyle(sSmall) { fontSize = 13, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Normal, wordWrap = true };
        GUI.color = new Color(0.9f, 0.95f, 1f, 0.92f);
        GUI.Label(new Rect(leftX + 16, cardY + 252, cardW - 32, 85), curDef.Description, descStyle);
        GUI.color = Color.white;

        DrawBadgeTag(new Rect(leftX + (cardW - 200) * 0.5f, cardY + 360, 200, 26), $"{totalLaps} LAPS   |   8 KARTS GP", new Color(0.12f, 0.35f, 0.65f, 0.85f));


        // 3. 右カード：ドライバー＆マシンセレクター（DRIVER & MACHINE）
        float rightX = w - cardW - 35f;
        DrawCard(new Rect(rightX, cardY, cardW, cardH));
        DrawHeaderRibbon(new Rect(rightX + 10, cardY + 10, cardW - 20, 30), "◄ DRIVER & KART [W][S] ►", new Color(1f, 0.65f, 0.15f));

        var curChar = KartCharacters[SelectedKart];
        var charTitleStyle = new GUIStyle(sSmall) { fontSize = 23, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
        Outlined(new Rect(rightX + 8, cardY + 48, cardW - 16, 30), $"< {curChar.name} >", charTitleStyle, curChar.color, 2);

        var driverNickStyle = new GUIStyle(sSmall) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        Outlined(new Rect(rightX + 10, cardY + 80, cardW - 20, 20), curChar.driver, driverNickStyle, Color.white, 1);

        DrawBadgeTag(new Rect(rightX + 20, cardY + 108, cardW - 40, 24), curChar.trait, new Color(curChar.color.r * 0.32f, curChar.color.g * 0.32f, curChar.color.b * 0.32f, 0.9f));

        // 4項目ステータスゲージ
        float statStartY = cardY + 148f;
        DrawStatGauge(rightX + 22, statStartY + 0, cardW - 44, "SPEED", curChar.speed, 8, new Color(0.2f, 0.85f, 1f));
        DrawStatGauge(rightX + 22, statStartY + 42, cardW - 44, "ACCEL", curChar.accel, 8, new Color(1f, 0.85f, 0.2f));
        DrawStatGauge(rightX + 22, statStartY + 84, cardW - 44, "STEER", curChar.handling, 8, new Color(0.35f, 1f, 0.5f));
        DrawStatGauge(rightX + 22, statStartY + 126, cardW - 44, "WEIGHT", curChar.weight, 8, new Color(1f, 0.45f, 0.35f));

        var switchGuide = new GUIStyle(sSmall) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
        GUI.color = new Color(0.85f, 0.9f, 1f, 0.7f);
        GUI.Label(new Rect(rightX + 10, cardY + 365, cardW - 20, 20), "Press [W][S] to switch machine", switchGuide);
        GUI.color = Color.white;


        // 4. 画面中央下部：PRESS ENTER TO RACE（ネオンパルス）
        float pulse = (Mathf.Sin(t * 6f) + 1f) * 0.5f;
        float btnW = 430f + pulse * 10f;
        float btnH = 46f + pulse * 4f;
        float btnX = (w - btnW) * 0.5f;
        float btnY = 598f - pulse * 2f;

        GUI.color = new Color(0.04f, 0.10f, 0.24f, 0.9f);
        GUI.DrawTexture(new Rect(btnX, btnY, btnW, btnH), Texture2D.whiteTexture);
        DrawFrame(new Rect(btnX, btnY, btnW, btnH), 3, Color.Lerp(new Color(1f, 0.82f, 0.15f), new Color(0.2f, 0.9f, 1f), pulse));
        GUI.color = Color.white;

        var startStyle = new GUIStyle(sMid) { fontSize = (int)(24 + pulse * 2), fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
        Color startCol = Color.Lerp(new Color(1f, 0.88f, 0.25f), Color.white, pulse * 0.65f);
        Outlined(new Rect(btnX, btnY + 2, btnW, btnH - 4), "►►  PRESS ENTER TO RACE!  ◄◄", startStyle, startCol, 3);


        // 5. 画面最下部：コントロールガイドバー
        GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);
        GUI.DrawTexture(new Rect(0, h - 34, w, 34), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, h - 34, w, 34), 1, new Color(0.25f, 0.4f, 0.6f, 0.5f));

        var barStyle = new GUIStyle(sSmall) { fontSize = 13, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
        string guideText = "[W][S] Driver   •   [A][D] Track   •   [SPACE] Drift / Hop   •   [E] Item   •   [ESC] Pause";
        Outlined(new Rect(0, h - 32, w, 28), guideText, barStyle, new Color(0.85f, 0.92f, 1f), 1);
    }

    void DrawCard(Rect r)
    {
        if (cardPanelTex != null)
        {
            GUI.DrawTexture(r, cardPanelTex, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.color = new Color(0.04f, 0.08f, 0.18f, 0.88f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFrame(r, 2, new Color(0.2f, 0.7f, 1f, 0.85f));
        }
    }

    void DrawHeaderRibbon(Rect r, string text, Color accent)
    {
        GUI.color = new Color(0.08f, 0.14f, 0.28f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = accent;
        GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var st = new GUIStyle(sSmall) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        Outlined(r, text, st, Color.white, 1);
    }

    void DrawBadgeTag(Rect r, string text, Color bg)
    {
        GUI.color = bg;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(r, 1, new Color(1f, 1f, 1f, 0.35f));

        var st = new GUIStyle(sSmall) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        Outlined(r, text, st, Color.white, 1);
    }

    void DrawStatGauge(float x, float y, float w, string label, int value, int maxVal, Color barColor)
    {
        var lblStyle = new GUIStyle(sSmall) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        Outlined(new Rect(x, y, 70, 16), label, lblStyle, Color.white, 1);

        float barX = x + 72;
        float barW = w - 72;
        float barH = 13;
        float barY = y + 1;

        GUI.color = new Color(0.08f, 0.12f, 0.2f, 0.85f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

        int segments = maxVal;
        float gap = 2.5f;
        float segW = (barW - (segments - 1) * gap) / segments;
        for (int i = 0; i < segments; i++)
        {
            float sx = barX + i * (segW + gap);
            if (i < value)
            {
                GUI.color = barColor;
                GUI.DrawTexture(new Rect(sx, barY, segW, barH), Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = new Color(0.2f, 0.25f, 0.35f, 0.4f);
                GUI.DrawTexture(new Rect(sx, barY, segW, barH), Texture2D.whiteTexture);
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
        var st = new GUIStyle(sBig) { fontSize = (int)(150 * (1.3f - frac * 0.3f)) };
        var c = n > 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f);
        c.a = 1f - frac * 0.3f;
        Outlined(new Rect(0, h * 0.25f, w, 200), text, st, c, 5);
    }

    void DrawHud(float w, float h)
    {
        // アイテム枠
        var slot = new Rect(24, 20, 114, 114);
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.8f);
        GUI.DrawTexture(slot, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(slot, 3, new Color(1f, 0.85f, 0.25f));

        ItemType shown = Player.Item;
        if (Player.RouletteTimer > 0) shown = (ItemType)(1 + (int)(Time.time * 16f) % 4);
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
                var ls = new GUIStyle(sSmall) { fontSize = 20 };
                Outlined(new Rect(slot.x, slot.y + 12, slot.width, 58), label, ls, Color.white, 2);
            }

            if (Player.RouletteTimer <= 0)
                Outlined(new Rect(slot.x, slot.yMax - 22, slot.width, 20), "[E]", new GUIStyle(sSmall) { fontSize = 15 }, new Color(1f, 1f, 0.3f), 1);
        }

        // 周回とタイム
        var right = new GUIStyle(sMid) { alignment = TextAnchor.UpperRight };
        int lap = Mathf.Clamp(Player.MaxLap, 1, totalLaps);
        Outlined(new Rect(w - 324, 18, 300, 50), "LAP " + lap + "/" + totalLaps, right, Color.white);
        var rs = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };
        Outlined(new Rect(w - 324, 66, 300, 30), FormatTime(Player.Finished ? Player.FinishTime : raceTime), rs, Color.white);
        if (bestLapTime > 0) Outlined(new Rect(w - 324, 94, 300, 30), "BEST LAP " + FormatTime(bestLapTime), new GUIStyle(rs) { fontSize = 16 }, new Color(1f, 0.9f, 0.5f));

        // 順位
        string place = Ordinal(Player.Place);
        var ps = new GUIStyle(sBig) { fontSize = 120, alignment = TextAnchor.LowerRight };
        Color pc = Player.Place == 1 ? new Color(1f, 0.85f, 0.15f) : Player.Place <= 3 ? new Color(0.85f, 0.9f, 1f) : Color.white;
        Outlined(new Rect(w - 324, h - 170, 300, 150), place, ps, pc, 5);

        // スピード
        var ss = new GUIStyle(sMid) { alignment = TextAnchor.LowerCenter };
        Outlined(new Rect(w / 2 - 150, h - 70, 300, 50), Mathf.RoundToInt(Mathf.Abs(Player.Speed) * 3.6f) + " km/h", ss, Color.white);

        // ミニマップ
        var mm = new Rect(20, h - 250, 230, 230);
        GUI.color = new Color(0, 0, 0, 0.3f);
        GUI.DrawTexture(mm, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(mm, minimap);
        foreach (var k in Karts.OrderBy(k => k == Player ? 1 : 0))
        {
            var uv = toMap(k.transform.position);
            var p = new Vector2(mm.x + uv.x * mm.width, mm.y + (1f - uv.y) * mm.height);
            float s = k == Player ? 14f : 10f;
            GUI.color = k == Player ? Color.white : Color.black;
            GUI.DrawTexture(new Rect(p.x - s / 2 - 2, p.y - s / 2 - 2, s + 4, s + 4), Texture2D.whiteTexture);
            GUI.color = k.Color;
            GUI.DrawTexture(new Rect(p.x - s / 2, p.y - s / 2, s, s), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // 逆走
        if (RaceRunning && !Player.Finished && Vector3.Dot(Player.Forward, track.Dirs[Player.Index]) < -0.3f && Player.Speed > 3f && Mathf.Repeat(Time.time, 0.6f) < 0.4f)
            Outlined(new Rect(0, h * 0.42f, w, 80), "WRONG WAY!", new GUIStyle(sBig) { fontSize = 60 }, new Color(1f, 0.3f, 0.3f));
    }

    void DrawResults(float w, float h)
    {
        var panel = new Rect(w / 2 - 280, 110, 560, 470);
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(panel, 3, new Color(1f, 0.85f, 0.2f));
        Outlined(new Rect(panel.x, panel.y + 10, panel.width, 60), "RESULTS", sMid, new Color(1f, 0.85f, 0.2f));

        var order = Karts.OrderBy(k => k.Place).ToList();
        var left = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleLeft };
        var rightS = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight };
        for (int i = 0; i < order.Count; i++)
        {
            var k = order[i];
            var row = new Rect(panel.x + 30, panel.y + 80 + i * 44, panel.width - 60, 40);
            if (k == Player)
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
            Outlined(new Rect(0, panel.yMax + 20, w, 50), "PRESS ENTER TO RACE AGAIN", new GUIStyle(sMid) { fontSize = 30 }, Color.white);
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

    void DrawVignette(float w, float h)
    {
        if (vignetteTex == null) return;
        float speed01 = Player != null ? Mathf.Clamp01(Player.Speed / Kart.MaxSpeed) : 0f;
        float boostBonus = (Player != null && Player.Boosting) ? 0.35f : 0f;
        float alpha = Mathf.Clamp01(0.18f + speed01 * 0.25f + boostBonus);
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(0, 0, w, h), vignetteTex, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    void DrawSpeedLines(float w, float h)
    {
        if (Player == null) return;
        float speedRatio = Mathf.Clamp01(Player.Speed / Kart.MaxSpeed);
        bool boost = Player.Boosting;
        if (speedRatio < 0.65f && !boost) return;

        float intensity = boost ? 1f : (speedRatio - 0.65f) / 0.35f;
        int lineCount = (int)(intensity * 18);
        var cx = w * 0.5f;
        var cy = h * 0.5f;

        var rng = new System.Random((int)(Time.time * 60f));
        for (int i = 0; i < lineCount; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rOuter = Mathf.Max(w, h) * 0.65f;
            float rInner = rOuter - (60f + (float)rng.NextDouble() * 140f * intensity);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

            Vector2 p1 = new Vector2(cx + cos * rOuter, cy + sin * rOuter);
            Vector2 p2 = new Vector2(cx + cos * rInner, cy + sin * rInner);

            Color lineColor = boost
                ? (rng.Next(2) == 0 ? new Color(1f, 0.7f, 0.2f, 0.75f) : new Color(0.4f, 0.85f, 1f, 0.75f))
                : new Color(1f, 1f, 1f, 0.45f * intensity);

            DrawSpeedStroke(p1, p2, 2.5f + (float)rng.NextDouble() * 2f, lineColor);
        }
    }

    void DrawSpeedStroke(Vector2 a, Vector2 b, float thickness, Color col)
    {
        var prev = GUI.color;
        GUI.color = col;
        var d = b - a;
        float len = d.magnitude;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
        GUI.matrix = m;
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
        titleLogoTex = LoadTexture("UI/title_logo", uiDir + "title_logo.png");
        for (int i = 0; i < 3; i++)
            trackBadgeTex[i] = LoadTexture($"UI/badge_track_{i}", uiDir + $"badge_track_{i}.png");

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
