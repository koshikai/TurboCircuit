using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// AIエージェント向け高速スクリーンショット＆3Dモデル4面図検証システム
// 起動引数:
//   -shot-ui <target1,target2,...>
//       例: -shot-ui title,settings,results,lobby4p
//   -shot-spot <courseIdx> <progress1,progress2,...> [yawOffset] [nohud]
//       例: -shot-spot 4 0.106,0.29,0.555,0.82 0 nohud
//   -shot-model <type>:<arg>
//       例: -shot-model kart:0:2  / -shot-model kart:all
//           -shot-model animal:cow / -shot-model animal:all
//           -shot-model fan:0      / -shot-model fan:all
//           -shot-model item:all
public partial class RaceManager
{
    bool suppressVerifyCamera;
    bool suppressVerifyGui;

    string shotUiArg;
    int shotSpotCourse = -1;
    string shotSpotProgressArg;
    float shotSpotYawOffset;
    bool shotSpotNoHud;
    string shotModelArg;

    void ParseVerifyArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-shot-ui" && i + 1 < args.Length)
            {
                shotUiArg = args[i + 1];
            }
            else if (args[i] == "-shot-spot" && i + 2 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int cIdx))
                    shotSpotCourse = cIdx;
                shotSpotProgressArg = args[i + 2];
                if (i + 3 < args.Length && !args[i + 3].StartsWith("-"))
                {
                    if (float.TryParse(args[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out float yaw))
                        shotSpotYawOffset = yaw;
                    else if (args[i + 3].Equals("nohud", System.StringComparison.OrdinalIgnoreCase))
                        shotSpotNoHud = true;
                }
                if (i + 4 < args.Length && args[i + 4].Equals("nohud", System.StringComparison.OrdinalIgnoreCase))
                {
                    shotSpotNoHud = true;
                }
            }
            else if (args[i] == "-shot-model" && i + 1 < args.Length)
            {
                shotModelArg = args[i + 1];
            }
        }
    }

    bool TryStartVerifyCoroutine()
    {
        if (!string.IsNullOrEmpty(shotModelArg))
        {
            StartCoroutine(RunShotModelCoroutine(shotModelArg));
            return true;
        }
        if (shotSpotCourse >= 0 && !string.IsNullOrEmpty(shotSpotProgressArg))
        {
            StartCoroutine(RunShotSpotCoroutine(shotSpotCourse, shotSpotProgressArg, shotSpotYawOffset, shotSpotNoHud));
            return true;
        }
        if (!string.IsNullOrEmpty(shotUiArg))
        {
            StartCoroutine(RunShotUiCoroutine(shotUiArg));
            return true;
        }
        return false;
    }

    static string VerifyOutputDir
    {
        get
        {
            string dir = Application.dataPath;
            return dir;
        }
    }

    static void SaveVerifyPng(Texture2D tex, string filename)
    {
        byte[] bytes = tex.EncodeToPNG();
        string outPath = Path.Combine(VerifyOutputDir, filename);
        File.WriteAllBytes(outPath, bytes);
        Debug.Log($"[VerifyShot] Saved: {outPath} ({tex.width}x{tex.height})");
    }

    // 複数枚のTexture2Dを2x2コンタクトシート（1枚絵）に合成して保存する
    static void SaveContactSheet(List<Texture2D> shots, List<string> labels, string filename, int cellW = 640, int cellH = 360)
    {
        if (shots == null || shots.Count == 0) return;
        int cols = 2;
        int rows = Mathf.CeilToInt(shots.Count / 2f);
        int sheetW = cols * cellW;
        int sheetH = rows * cellH;

        var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGB24, false);
        Color bg = new Color(0.12f, 0.14f, 0.18f);
        Color[] fill = new Color[sheetW * sheetH];
        for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        sheet.SetPixels(fill);

        for (int idx = 0; idx < shots.Count; idx++)
        {
            var src = shots[idx];
            if (src == null) continue;
            int col = idx % cols;
            int row = idx / cols;
            int ox = col * cellW;
            // UnityのTexture2D座標系は左下が(0,0)なので上の行ほどyが大きい
            int oy = (rows - 1 - row) * cellH;

            for (int y = 0; y < cellH; y++)
            {
                float v = (y + 0.5f) / cellH;
                for (int x = 0; x < cellW; x++)
                {
                    float u = (x + 0.5f) / cellW;
                    // 枠線（2px）
                    if (x < 2 || x >= cellW - 2 || y < 2 || y >= cellH - 2)
                    {
                        sheet.SetPixel(ox + x, oy + y, new Color(0.25f, 0.3f, 0.38f));
                    }
                    else
                    {
                        sheet.SetPixel(ox + x, oy + y, src.GetPixelBilinear(u, v));
                    }
                }
            }
        }
        sheet.Apply();
        SaveVerifyPng(sheet, filename);
        Destroy(sheet);
    }

    // ───────────────────────── 1. -shot-ui (UIピンポイント高速撮影) ─────────────────────────

    IEnumerator RunShotUiCoroutine(string targetsCsv)
    {
        // 初回初期化・フォント・アセット安定のため少し待つ
        yield return new WaitForSeconds(0.35f);

        string[] targets = targetsCsv.Split(',');
        var capturedTexs = new List<Texture2D>();
        var capturedNames = new List<string>();

        foreach (var raw in targets)
        {
            string t = raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(t)) continue;

            // 状態リセット
            showSettings = false;
            paused = false;
            netMenu = false;
            if (updater != null) updater.ShowModal = false;
            if (Net != null && Net.Busy) Net.Disconnect();
            SetTwoPlayer(false);
            IsTimeAttack = false;

            switch (t)
            {
                case "title":
                    state = State.Title;
                    ConfigureHuman(0);
                    break;
                case "title2p":
                    state = State.Title;
                    SelectedDriver2 = 7;
                    SelectedKart2 = 4;
                    SetTwoPlayer(true);
                    break;
                case "settings":
                case "settings_general":
                    state = State.Title;
                    showSettings = true;
                    settingsTab = 0;
                    break;
                case "keys":
                case "settings_keys":
                    state = State.Title;
                    showSettings = true;
                    settingsTab = 1;
                    break;
                case "updates":
                case "settings_updates":
                    state = State.Title;
                    showSettings = true;
                    settingsTab = 2;
                    break;
                case "updatemodal":
                case "update_modal":
                    state = State.Title;
                    if (updater != null) updater.SimulateUpdateAvailableForTesting();
                    break;
                case "netmenu":
                    state = State.Title;
                    netMenu = true;
                    break;
                case "lobby":
                case "lobby4p":
                    state = State.Title;
                    Net.HostDirect();
                    Net.JoinCode = "WFHPQQ";
                    playerKarts.Clear();
                    playerNames.Clear();
                    playerKarts[5] = PackDriverKart(SelectedDriver, SelectedKart); playerNames[5] = "HostPlayer";
                    playerKarts[0] = PackDriverKart(1, 1); playerNames[0] = "RivalOne";
                    playerKarts[1] = PackDriverKart(6, 3); playerNames[1] = "Speedy";
                    playerKarts[2] = PackDriverKart(8, 7); playerNames[2] = "KartMaster";
                    lobbySlots.Clear();
                    foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
                    ApplyLobbyKarts();
                    break;
                case "lobby8p":
                    state = State.Title;
                    Net.HostDirect();
                    Net.JoinCode = "WFHPQQ";
                    playerKarts.Clear();
                    playerNames.Clear();
                    playerKarts[5] = PackDriverKart(SelectedDriver, SelectedKart); playerNames[5] = "HostPlayer";
                    playerKarts[0] = PackDriverKart(1, 1); playerNames[0] = "RivalOne";
                    playerKarts[1] = PackDriverKart(6, 3); playerNames[1] = "Speedy";
                    playerKarts[2] = PackDriverKart(8, 7); playerNames[2] = "KartMaster";
                    playerKarts[3] = PackDriverKart(4, 5); playerNames[3] = "Racer4";
                    playerKarts[4] = PackDriverKart(9, 6); playerNames[4] = "Racer5";
                    playerKarts[6] = PackDriverKart(2, 2); playerNames[6] = "Racer6";
                    playerKarts[7] = PackDriverKart(7, 4); playerNames[7] = "Racer7";
                    lobbySlots.Clear();
                    foreach (var s in HumanSlots) if (playerKarts.ContainsKey(s)) lobbySlots.Add(s);
                    ApplyLobbyKarts();
                    break;
                case "countdown":
                    StartRaceFromTitle();
                    state = State.Countdown;
                    stateTime = 1.5f;
                    break;
                case "racing":
                case "hud":
                case "hud1p":
                    StartRaceFromTitle();
                    state = State.Racing;
                    stateTime = 5f;
                    raceTime = 14.2f;
                    if (Player != null) Player.SetPreviewSpeedAndBoost(29.5f, 1.5f, ItemType.Missile);
                    break;
                case "hud2p":
                case "racing2p":
                    SetTwoPlayer(true);
                    ResetRace();
                    state = State.Racing;
                    stateTime = 5f;
                    raceTime = 15.2f;
                    if (Player != null) Player.SetPreviewSpeedAndBoost(27.2f, 0f, ItemType.Turbo);
                    if (Player2 != null) Player2.SetPreviewSpeedAndBoost(29.0f, 1.2f, ItemType.Banana);
                    break;
                case "paused":
                    StartRaceFromTitle();
                    state = State.Racing;
                    stateTime = 5f;
                    raceTime = 12.0f;
                    paused = true;
                    break;
                case "results":
                    StartRaceFromTitle();
                    bestLap[0] = 24.18f;
                    foreach (var k in Karts)
                    {
                        k.Finished = true;
                        k.Place = Karts.IndexOf(k) + 1;
                        k.FinishTime = 72.5f + k.Place * 1.5f;
                    }
                    placeOrder.Clear();
                    placeOrder.AddRange(Karts);
                    placeOrder.Sort(ComparePlaces);
                    state = State.Results;
                    finishAt = Time.time - 5f;
                    break;
            }

            // カメラとUIを2フレーム更新させてからバックバッファをキャプチャ
            yield return new WaitForSeconds(0.18f);
            yield return new WaitForEndOfFrame();

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            SaveVerifyPng(tex, $"verify_ui_{t}.png");
            capturedTexs.Add(tex);
            capturedNames.Add(t);
        }

        if (capturedTexs.Count > 1)
        {
            SaveContactSheet(capturedTexs, capturedNames, "verify_ui_sheet.png");
        }
        foreach (var tex in capturedTexs) Destroy(tex);

        yield return null;
        Application.Quit();
    }

    // ───────────────────────── 2. -shot-spot (コース任意地点ピンポイント撮影) ─────────────────────────

    IEnumerator RunShotSpotCoroutine(int courseIdx, string progressCsv, float yawOffset, bool noHud)
    {
        yield return new WaitForSeconds(0.3f);

        courseIdx = Mathf.Clamp(courseIdx, 0, Track.Courses.Length - 1);
        if (SelectedCourse != courseIdx)
            LoadCourse(courseIdx);

        StartRaceFromTitle();
        state = State.Racing;
        stateTime = 5f;
        raceTime = 10f;
        suppressVerifyGui = noHud;
        suppressVerifyCamera = true;

        // CPUカートが映り込んで邪魔にならないよう退避させる（プレイヤーのみ配置）
        foreach (var k in Karts)
        {
            if (k != Player) k.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.2f);

        string[] parts = progressCsv.Split(',');
        var capturedTexs = new List<Texture2D>();
        var capturedNames = new List<string>();

        foreach (var raw in parts)
        {
            if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float p01))
                continue;

            p01 = Mathf.Repeat(p01, 1f);
            int idx = Mathf.Clamp(Mathf.RoundToInt(p01 * track.Count), 0, track.Count - 1);

            Player.transform.position = track.PointAt(idx, 0f) + Vector3.up * 0.4f;
            Player.Index = idx;
            Player.Progress = track.Dist[idx];
            float trackHeading = Mathf.Atan2(track.Dirs[idx].x, track.Dirs[idx].z) * Mathf.Rad2Deg;
            float camHeading = trackHeading + yawOffset;
            Player.Heading = trackHeading;
            Player.Speed = 0f;
            Player.SetPreviewSpeedAndBoost(26.0f, 0f);

            var r = Quaternion.Euler(0f, camHeading, 0f);
            cam.fieldOfView = 60f;
            cam.transform.position = Player.transform.position + r * new Vector3(0f, 2.7f, -6.8f);
            cam.transform.LookAt(Player.transform.position + r * new Vector3(0f, 1.1f, 3.2f));

            yield return null;
            yield return new WaitForEndOfFrame();

            string tag = $"c{courseIdx}_p{Mathf.RoundToInt(p01 * 1000f):D3}";
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            SaveVerifyPng(tex, $"verify_spot_{tag}.png");
            capturedTexs.Add(tex);
            capturedNames.Add(tag);
        }

        if (capturedTexs.Count > 1)
        {
            SaveContactSheet(capturedTexs, capturedNames, $"verify_spot_c{courseIdx}_sheet.png");
        }
        foreach (var tex in capturedTexs) Destroy(tex);

        yield return null;
        Application.Quit();
    }

    // ───────────────────────── 3. -shot-model (3Dモデル4面図スタジオ撮影) ─────────────────────────

    IEnumerator RunShotModelCoroutine(string spec)
    {
        yield return new WaitForSeconds(0.25f);
        suppressVerifyCamera = true;
        suppressVerifyGui = true;

        // 3Dスタジオ構築（上空 Y=1000m に隔離配置）
        var studio = new GameObject("VerifyModelStudio");
        Vector3 center = new Vector3(0f, 1000f, 0f);
        studio.transform.position = center;

        // 既存の太陽光を弱め、スタジオ専用3点照明を配置
        if (Sun != null) Sun.enabled = false;
        bool prevFog = RenderSettings.fog;
        RenderSettings.fog = false;

        var keyLight = new GameObject("StudioKey").AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.transform.SetParent(studio.transform, false);
        keyLight.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        keyLight.intensity = 1.35f;
        keyLight.color = new Color(1f, 0.98f, 0.94f);

        var fillLight = new GameObject("StudioFill").AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.transform.SetParent(studio.transform, false);
        fillLight.transform.rotation = Quaternion.Euler(25f, 135f, 0f);
        fillLight.intensity = 0.65f;
        fillLight.color = new Color(0.78f, 0.88f, 1f);

        var rimLight = new GameObject("StudioRim").AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.transform.SetParent(studio.transform, false);
        rimLight.transform.rotation = Quaternion.Euler(-15f, 200f, 0f);
        rimLight.intensity = 0.4f;
        rimLight.color = new Color(0.95f, 0.95f, 1f);

        // 1m基準グリッド床の生成（6m x 6m、0.5mサブグリッド、X軸=赤、Z軸=青）
        var gridStage = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(gridStage.GetComponent<Collider>());
        gridStage.name = "ReferenceGridFloor";
        gridStage.transform.SetParent(studio.transform, false);
        gridStage.transform.localPosition = new Vector3(0f, 0f, 0f);
        gridStage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        gridStage.transform.localScale = new Vector3(6f, 6f, 1f);
        var gridMat = new Material(wallMaterial);
        var gridTex = BuildReferenceGridTexture(512, 6f);
        gridMat.mainTexture = gridTex;
        gridMat.SetTexture("_BaseMap", gridTex);
        gridMat.SetTexture("_MainTex", gridTex);
        gridMat.color = Color.white;
        gridMat.SetColor("_BaseColor", Color.white);
        gridMat.SetFloat("_Smoothness", 0.1f);
        gridStage.GetComponent<Renderer>().sharedMaterial = gridMat;

        // 高さ基準ルーラーポール（0.5mごとに目盛りリング付きの2.0mポールを左奥に配置）
        var rulerRoot = BuildHeightRuler(studio.transform, new Vector3(-1.35f, 0f, -1.1f));

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.21f, 0.27f);
        cam.fieldOfView = 40f;

        // 全CPU・プレイヤーカーを一旦非表示
        foreach (var k in Karts) k.gameObject.SetActive(false);

        string[] parts = spec.Split(':');
        string category = parts[0].ToLowerInvariant();
        string arg1 = parts.Length > 1 ? parts[1] : "0";
        string arg2 = parts.Length > 2 ? parts[2] : "0";

        var reportLines = new List<string>();

        if (category == "kart")
        {
            if (arg1.Equals("all", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int m = 0; m < KartMachines.Length; m++)
                {
                    RenderKartTurnaround(center, 0, m, $"verify_model_kart_m{m}_{KartMachines[m].code}.png", reportLines);
                    yield return null;
                }
            }
            else
            {
                int d = int.TryParse(arg1, out int pd) ? Mathf.Clamp(pd, 0, KartCharacters.Length - 1) : 0;
                int m = int.TryParse(arg2, out int pm) ? Mathf.Clamp(pm, 0, KartMachines.Length - 1) : 0;
                RenderKartTurnaround(center, d, m, $"verify_model_kart_d{d}_m{m}.png", reportLines);
                yield return null;
            }
        }
        else if (category == "driver")
        {
            if (arg1.Equals("all", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int d = 0; d < KartCharacters.Length; d++)
                {
                    RenderKartTurnaround(center, d, 0, $"verify_model_driver_{d}_{KartCharacters[d].name}.png", reportLines);
                    yield return null;
                }
            }
            else
            {
                int d = int.TryParse(arg1, out int pd) ? Mathf.Clamp(pd, 0, KartCharacters.Length - 1) : 0;
                RenderKartTurnaround(center, d, 0, $"verify_model_driver_{d}_{KartCharacters[d].name}.png", reportLines);
                yield return null;
            }
        }
        else if (category == "animal")
        {
            string[] allAnimals = { "bunny", "penguin", "polar", "deer", "fox", "lion", "cow", "cat", "dog", "crab", "chick", "parrot", "panda" };
            string[] list = arg1.Equals("all", System.StringComparison.OrdinalIgnoreCase) ? allAnimals : new[] { arg1.ToLowerInvariant() };

            string animalTexPath = Application.dataPath + "/Resources/Animals/Textures/colormap.png";
            var colormapTex = LoadTexture("Animals/Textures/colormap", animalTexPath);
            var animalMat = new Material(wallMaterial);
            if (colormapTex != null)
            {
                animalMat.mainTexture = colormapTex;
                animalMat.SetTexture("_BaseMap", colormapTex);
                animalMat.SetTexture("_MainTex", colormapTex);
            }
            animalMat.color = Color.white;
            animalMat.SetColor("_BaseColor", Color.white);
            animalMat.SetFloat("_Smoothness", 0.15f);

            foreach (var aName in list)
            {
                var prefab = Resources.Load<GameObject>($"Animals/animal-{aName}");
                if (prefab == null)
                {
                    Debug.LogWarning($"[VerifyShot] Animal prefab not found: Animals/animal-{aName}");
                    continue;
                }
                var go = Instantiate(prefab, studio.transform);
                go.transform.position = center;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 1.5f;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = animalMat;

                RenderObjectTurnaround(go, center, $"verify_model_animal_{aName}.png", $"animal:{aName}", reportLines);
                DestroyImmediate(go);
            }
        }
        else if (category == "fan")
        {
            var spectatorPalette = SpectatorPalette.Create(this, new List<Material>());
            string[] hatNames = { "cap", "visor", "knit", "bear_ears", "headphones", "sunglasses" };
            int start = 0, end = 5;
            if (!arg1.Equals("all", System.StringComparison.OrdinalIgnoreCase) && int.TryParse(arg1, out int hIdx))
            {
                start = end = Mathf.Clamp(hIdx, 0, 5);
            }
            for (int h = start; h <= end; h++)
            {
                var fanObj = SpectatorFan.CreateFan(studio.transform, center, Quaternion.identity, new System.Random(h * 888 + 19), this, spectatorPalette);
                RenderObjectTurnaround(fanObj, center, $"verify_model_fan_{h}_{hatNames[h]}.png", $"fan:{hatNames[h]}", reportLines);
                DestroyImmediate(fanObj);
            }
        }
        else if (category == "item")
        {
            // Banana
            var bananaGo = new GameObject("VerifyBanana");
            bananaGo.transform.SetParent(studio.transform, false);
            var banana = bananaGo.AddComponent<Banana>();
            banana.Init(center, null, bananaMaterial);
            bananaGo.transform.rotation = Quaternion.identity;
            RenderObjectTurnaround(bananaGo, center, "verify_model_item_banana.png", "item:banana", reportLines);
            DestroyImmediate(bananaGo);

            // Missile
            var missileGo = new GameObject("VerifyMissile");
            missileGo.transform.SetParent(studio.transform, false);
            var missile = missileGo.AddComponent<Missile>();
            missile.Init(this, track, Player, null, missileMaterial);
            missileGo.transform.position = center + Vector3.up * 0.4f;
            missileGo.transform.rotation = Quaternion.identity;
            RenderObjectTurnaround(missileGo, center, "verify_model_item_missile.png", "item:missile", reportLines);
            DestroyImmediate(missileGo);

            // ItemBox
            var boxGo = new GameObject("VerifyItemBox");
            boxGo.transform.SetParent(studio.transform, false);
            var box = boxGo.AddComponent<ItemBox>();
            box.Init(center - Vector3.up * 0.45f, itemBoxMaterial, font);
            boxGo.transform.rotation = Quaternion.identity;
            RenderObjectTurnaround(boxGo, center, "verify_model_item_box.png", "item:box", reportLines);
            DestroyImmediate(boxGo);
        }

        if (reportLines.Count > 0)
        {
            string reportPath = Path.Combine(VerifyOutputDir, "verify_model_bounds.txt");
            File.WriteAllLines(reportPath, reportLines);
            Debug.Log($"[VerifyShot] Model bounds report saved: {reportPath}");
        }

        RenderSettings.fog = prevFog;
        Destroy(studio);
        yield return null;
        Application.Quit();
    }

    void RenderKartTurnaround(Vector3 studioCenter, int driverIdx, int machineIdx, string filename, List<string> reportLines)
    {
        SelectedDriver = driverIdx;
        SelectedKart = machineIdx;
        Player.gameObject.SetActive(true);
        ConfigureHuman(0);
        Player.transform.position = studioCenter;
        Player.transform.rotation = Quaternion.identity;
        Player.Heading = 0f;
        Player.Speed = 0f;

        string label = $"kart:d{driverIdx}({KartCharacters[driverIdx].name})_m{machineIdx}({KartMachines[machineIdx].name})";
        RenderObjectTurnaround(Player.gameObject, studioCenter, filename, label, reportLines);
        Player.gameObject.SetActive(false);
    }

    // 対象GameObjectを4方向（1:斜め前, 2:正面, 3:左側面, 4:斜め後ろ）からRenderTextureに描画し、1枚の2x2シートに合成
    void RenderObjectTurnaround(GameObject targetGo, Vector3 studioCenter, string filename, string label, List<string> reportLines)
    {
        Bounds b = CalculateWorldBounds(targetGo, studioCenter);
        Vector3 size = b.size;
        float bottomY = b.min.y - studioCenter.y;
        float topY = b.max.y - studioCenter.y;
        string metricLine = $"{label} | Size(WxHxD): {size.x:F2}m x {size.y:F2}m x {size.z:F2}m | BottomY: {bottomY:+0.00;-0.00;0.00}m | TopY: {topY:F2}m";
        reportLines.Add(metricLine);
        Debug.Log("[VerifyBounds] " + metricLine);

        Vector3 lookAt = new Vector3(studioCenter.x, studioCenter.y + Mathf.Clamp(size.y * 0.48f, 0.4f, 1.3f), studioCenter.z);
        float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float dist = Mathf.Clamp(maxDim * 1.85f, 2.8f, 6.0f);

        // 4アングル定義: (Yaw, Pitch)
        // [0] 左上: 斜め前 (3/4 Front-Right)
        // [1] 右上: 正面 (Front)
        // [2] 左下: 左側面 (Left Side)
        // [3] 右下: 斜め後ろ (3/4 Rear-Left / Player Chase View)
        Vector2[] angles =
        {
            new Vector2(35f, 18f),
            new Vector2(0f, 10f),
            new Vector2(-90f, 10f),
            new Vector2(215f, 20f)
        };

        int cellRes = 512;
        var rt = new RenderTexture(cellRes, cellRes, 24);
        var prevRt = cam.targetTexture;
        var prevRect = cam.rect;
        cam.rect = new Rect(0f, 0f, 1f, 1f);
        cam.targetTexture = rt;

        var shots = new List<Texture2D>(4);
        for (int i = 0; i < 4; i++)
        {
            float yaw = angles[i].x;
            float pitch = angles[i].y;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            // Z+ が正面なので、(0, 0, 1) 方向からオブジェクトを見るために rot * Vector3.forward * dist を足す
            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad) * dist, Mathf.Cos(pitch * Mathf.Deg2Rad) * dist);
            cam.transform.position = lookAt + offset;
            cam.transform.LookAt(lookAt);
            cam.Render();

            RenderTexture.active = rt;
            var cellTex = new Texture2D(cellRes, cellRes, TextureFormat.RGB24, false);
            cellTex.ReadPixels(new Rect(0, 0, cellRes, cellRes), 0, 0);
            cellTex.Apply();
            RenderTexture.active = null;
            shots.Add(cellTex);
        }

        cam.targetTexture = prevRt;
        cam.rect = prevRect;
        rt.Release();
        Destroy(rt);

        SaveContactSheet(shots, null, filename, cellRes, cellRes);
        foreach (var s in shots) Destroy(s);
    }

    static Bounds CalculateWorldBounds(GameObject go, Vector3 fallbackCenter)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        Bounds b = new Bounds(fallbackCenter, Vector3.zero);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            string n = r.gameObject.name;
            if (n == "BlobShadow" || n.StartsWith("BoostFlame") || n.Contains("Skid"))
                continue;
            // スタジオ中心から極端に離れた残留メッシュを除外
            if (Vector3.Distance(r.bounds.center, fallbackCenter) > 15f)
                continue;
            if (!hasBounds)
            {
                b = r.bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        return b;
    }

    // 1m間隔のメイングリッド・0.5mサブグリッド・X軸(赤)/Z軸(青)を描画した基準床テクスチャ
    static Texture2D BuildReferenceGridTexture(int size, float worldSizeMeters)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float halfMeters = worldSizeMeters * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float wz = ((y + 0.5f) / size - 0.5f) * worldSizeMeters;
            for (int x = 0; x < size; x++)
            {
                float wx = ((x + 0.5f) / size - 0.5f) * worldSizeMeters;

                // 1mごとのチェッカータイル
                int ix = Mathf.FloorToInt(wx + halfMeters);
                int iz = Mathf.FloorToInt(wz + halfMeters);
                bool even = ((ix + iz) & 1) == 0;
                Color col = even ? new Color(0.28f, 0.31f, 0.36f) : new Color(0.23f, 0.26f, 0.31f);

                // 0.5mサブグリッド線
                float gx05 = Mathf.Abs(wx * 2f - Mathf.Round(wx * 2f));
                float gz05 = Mathf.Abs(wz * 2f - Mathf.Round(wz * 2f));
                if (gx05 < 0.025f || gz05 < 0.025f)
                    col = Color.Lerp(col, new Color(0.42f, 0.47f, 0.55f), 0.55f);

                // 1.0mメイングリッド線
                float gx1 = Mathf.Abs(wx - Mathf.Round(wx));
                float gz1 = Mathf.Abs(wz - Mathf.Round(wz));
                if (gx1 < 0.02f || gz1 < 0.02f)
                    col = new Color(0.62f, 0.68f, 0.78f);

                // 原点X軸(赤)・Z軸(青)
                if (Mathf.Abs(wz) < 0.025f) col = new Color(0.95f, 0.28f, 0.28f);
                if (Mathf.Abs(wx) < 0.025f) col = new Color(0.28f, 0.58f, 0.98f);

                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    // 0.5m, 1.0m, 1.5m, 2.0m の高さ目盛りポールを作成
    Transform BuildHeightRuler(Transform parent, Vector3 localPos)
    {
        var root = new GameObject("HeightRuler").transform;
        root.SetParent(parent, false);
        root.localPosition = localPos;

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(root, false);
        pole.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        pole.transform.localScale = new Vector3(0.04f, 1.0f, 0.04f);
        pole.GetComponent<Renderer>().sharedMaterial = chromeMaterial;

        for (int i = 1; i <= 4; i++)
        {
            float h = i * 0.5f;
            var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tick.GetComponent<Collider>());
            tick.transform.SetParent(root, false);
            tick.transform.localPosition = new Vector3(0.08f, h, 0f);
            bool major = (i % 2 == 0);
            tick.transform.localScale = new Vector3(major ? 0.22f : 0.14f, 0.025f, 0.04f);
            tick.GetComponent<Renderer>().sharedMaterial = major ? boostPadMaterial : skinMaterial;
        }
        return root;
    }
}
