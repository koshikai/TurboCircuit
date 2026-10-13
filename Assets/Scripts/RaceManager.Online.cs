using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// オンライン対戦：接続イベント、ロビー同期、スロット割り当て、ロビー画面と接続メニュー
public partial class RaceManager
{
    // ───────────────────────── オンライン対戦 ─────────────────────────

    void OnNetHostClientConnected(Unity.Networking.Transport.NetworkConnection conn)
    {
        if (TwoPlayer) SetTwoPlayer(false);
        if (IsTimeAttack) IsTimeAttack = false;
        playerKarts[5] = PackDriverKart(SelectedDriver, SelectedKart);
        // 未割り当ての人間スロットを探す
        int nextSlot = HumanSlots.FirstOrDefault(s => !playerKarts.ContainsKey(s));
        if (nextSlot == 0 && playerKarts.ContainsKey(0)) nextSlot = 6;
        clientSlots[conn] = nextSlot;
        playerKarts[nextSlot] = 0; // 初期選択
        Net.SendWelcome(conn, nextSlot, SelectedCourse);
        if (state == State.Title)
        {
            SyncLobby();
        }
        else
        {
            if (nextSlot >= 0 && nextSlot < Karts.Count)
            {
                Karts[nextSlot].IsPlayer = false;
                Karts[nextSlot].IsRemote = true;
            }
            Net.SendStart(SelectedCourse);
        }
        if (Audio != null) Audio.Select();
    }

    void OnNetHostClientDisconnected(Unity.Networking.Transport.NetworkConnection conn)
    {
        if (clientSlots.TryGetValue(conn, out int slot))
        {
            clientSlots.Remove(conn);
            playerKarts.Remove(slot);
            playerNames.Remove(slot);
            MakeAI(Karts[slot], slot);
            if (state == State.Title) SyncLobby();
            Banner(Loc.T("PLAYER LEFT", "プレイヤー退出"), new Color(1f, 0.4f, 0.3f), 0);
        }
    }

    void OnNetClientHello(Unity.Networking.Transport.NetworkConnection conn, int kartChar, string pName)
    {
        if (clientSlots.TryGetValue(conn, out int slot))
        {
            UnpackDriverKart(kartChar, out int drv, out int krt);
            playerKarts[slot] = PackDriverKart(drv, krt);
            playerNames[slot] = string.IsNullOrEmpty(pName) ? $"P{slot + 1}" : pName;
            if (state == State.Title) SyncLobby();
        }
    }

    void OnNetClientConnected()
    {
        if (TwoPlayer) SetTwoPlayer(false);
        if (IsTimeAttack) IsTimeAttack = false;
        int packed = PackDriverKart(SelectedDriver, SelectedKart);
        if (Net.IsHost)
        {
            playerKarts[5] = packed;
            playerNames[5] = DisplayName;
            SyncLobby();
        }
        else
        {
            Net.SendHello(packed, DisplayName);
        }
        if (Audio != null) Audio.Select();
    }

    void OnNetWelcome(int assignedSlot, int course)
    {
        if (TwoPlayer) SetTwoPlayer(false);
        if (IsTimeAttack) IsTimeAttack = false;
        mySlot = assignedSlot;
        Player = Karts[mySlot];
        Player.IsRemote = false;
        Player.IsPlayer = true;
        ConfigureHuman(0);
        for (int i = 0; i < Karts.Count; i++)
        {
            if (i != mySlot) Karts[i].IsRemote = true;
        }
        if (SelectedCourse != course) LoadCourse(course);
        Net.SendHello(PackDriverKart(SelectedDriver, SelectedKart), DisplayName);
        if (Audio != null) Audio.Select();
    }

    void OnNetLobbySync(List<(int slot, int kartChar, string name)> list)
    {
        playerKarts.Clear();
        playerNames.Clear();
        foreach (var (slot, kartChar, n) in list)
        {
            playerKarts[slot] = kartChar;
            playerNames[slot] = n;
        }
        ApplyLobbyKarts();
    }

    void SyncLobby()
    {
        playerKarts[5] = PackDriverKart(SelectedDriver, SelectedKart);
        playerNames[5] = DisplayName;
        var list = playerKarts.Select(kv => (kv.Key, kv.Value, playerNames.TryGetValue(kv.Key, out var n) ? n : (kv.Key == 5 ? DisplayName : $"P{kv.Key + 1}"))).ToList();
        Net.SendLobbySync(list);
        ApplyLobbyKarts();
    }

    void ApplyLobbyKarts()
    {
        var synced = new HashSet<int>(playerKarts.Keys);
        foreach (var kv in playerKarts)
        {
            int slot = kv.Key;
            UnpackDriverKart(kv.Value, out int drvIdx, out int krtIdx);
            var d = KartCharacters[drvIdx];
            var m = KartMachines[krtIdx];
            var k = Karts[slot];
            k.ModelVariant = drvIdx;
            k.KartVariant = krtIdx;
            k.DriverColor = d.color;
            k.Color = m.color;
            GetCombinedStats(drvIdx, krtIdx, out int spd, out int acc, out int hdl, out int wgt);
            k.ApplyStats(spd, acc, hdl, wgt);

            int order = System.Array.IndexOf(HumanSlots, slot);
            string customName = playerNames.TryGetValue(slot, out var pn) && !string.IsNullOrEmpty(pn) ? pn : "";
            string drvName = Loc.DriverName(drvIdx, d.name);
            string title;
            if (slot == mySlot)
            {
                title = string.IsNullOrEmpty(customName) ? $"{DisplayName} ({drvName})" : $"{customName} ({drvName})";
            }
            else
            {
                string fallback = (slot == 5 ? "HOST" : $"P{order + 1}");
                title = string.IsNullOrEmpty(customName) ? $"{fallback} ({drvName})" : $"{customName} ({drvName})";
            }
            k.Name = title;
            k.RebuildModel();

            if (slot == mySlot)
            {
                k.IsPlayer = true;
                k.IsRemote = false;
            }
            else
            {
                k.IsPlayer = false;
                k.IsRemote = true;
            }
        }

        for (int i = 0; i < Karts.Count; i++)
        {
            if (!synced.Contains(i))
            {
                MakeAI(Karts[i], i);
                Karts[i].IsRemote = !Net.IsHost; // ホストが AI 挙動をシミュレート、クライアントは同期受信
            }
        }
    }

    void MakeAI(Kart k, int slot)
    {
        int ai = slot < playerGridSlot ? slot : slot - 1;
        bool named = slot != playerGridSlot;
        int aiDrv = (ai + 3) % KartCharacters.Length;
        int aiKrt = (ai + 1) % KartMachines.Length;
        k.IsPlayer = false;
        k.IsRemote = false;
        k.PlayerIndex = 0;
        k.ModelVariant = aiDrv;
        k.KartVariant = aiKrt;
        string aiName = Loc.AiName(ai, AiNames[ai]);
        string drvName = Loc.DriverName(aiDrv, KartCharacters[aiDrv].name);
        k.Name = named ? $"{aiName} ({drvName})" : Loc.T("Guest", "ゲスト");
        k.DriverColor = KartCharacters[aiDrv].color;
        k.Color = named ? AiColors[ai] : new Color(0.7f, 0.75f, 0.85f);
        GetCombinedStats(aiDrv, aiKrt, out int spd, out int acc, out int hdl, out int wgt);
        k.ApplyStats(spd, acc, hdl, wgt);
        k.RebuildModel();
    }

    void OnNetDisconnected()
    {
        playerKarts.Clear();
        playerNames.Clear();
        clientSlots.Clear();
        mySlot = playerGridSlot;
        foreach (var k in Karts) k.IsRemote = false;
        RestoreRoles();
        if (state == State.Title)
        {
            // タイトル画面のまま
        }
        else
        {
            ReturnToTitle();
            Banner(Loc.T("HOST DISCONNECTED", "ホストとの接続が切断されました"), new Color(1f, 0.4f, 0.3f), 0);
        }
    }

    // スロット配置をシングルプレイ状態に元に戻す
    void RestoreRoles()
    {
        playerKarts.Clear();
        playerNames.Clear();
        clientSlots.Clear();
        mySlot = playerGridSlot;
        ShowAllKarts();
        for (int i = 0; i < Karts.Count; i++)
        {
            if (i == playerGridSlot)
            {
                Player = Karts[i];
                Player.IsRemote = false;
                ConfigureHuman(0);
            }
            else if (TwoPlayer && i == P2Slot)
            {
                Karts[i].IsRemote = false;
                ConfigureHuman(1);
            }
            else
            {
                MakeAI(Karts[i], i);
            }
        }
    }

    void LeaveOnline()
    {
        netMenu = false;
        GUIUtility.keyboardControl = 0;
        GamepadHaptics.StopAll();
        Net.Disconnect();
        RestoreRoles();
        if (state != State.Title) ReturnToTitle();
    }

    // ───────────────────────── Online lobby stage ─────────────────────────

    // 部屋に入っている人間のカートだけをスタート地点に整列させ、カメラで全員を映す
    readonly List<int> lobbySlots = new List<int>();

    void ShowAllKarts()
    {
        foreach (var k in Karts)
            if (!k.gameObject.activeSelf) k.gameObject.SetActive(true);
    }

    void UpdateLobbyStage(float dt)
    {
        lobbySlots.Clear();
        foreach (var s in HumanSlots)
            if (playerKarts.ContainsKey(s)) lobbySlots.Add(s); // ホストが先頭

        for (int i = 0; i < Karts.Count; i++)
        {
            bool show = playerKarts.ContainsKey(i);
            if (Karts[i].gameObject.activeSelf != show) Karts[i].gameObject.SetActive(show);
        }

        int n = lobbySlots.Count;
        int front = n <= 4 ? n : (n + 1) / 2;
        int back = n - front;
        const float spacing = 3.4f, rowGap = 5.2f;

        var origin = track.PointAt(0, 0);
        var dir = track.Dirs[0];
        var right = track.Rights[0];
        float baseHeading = Quaternion.LookRotation(dir).eulerAngles.y;

        for (int i = 0; i < n; i++)
        {
            bool isFront = i < front;
            int idx = isFront ? i : i - front;
            int count = isFront ? front : back;
            float x = (idx - (count - 1) * 0.5f) * spacing;
            float z = isFront ? 0f : rowGap;
            var k = Karts[lobbySlots[i]];
            k.transform.position = origin + right * x - dir * z;
            k.Heading = baseHeading - 18f + Mathf.Sin(Time.time * 1.3f + i * 0.9f) * 7f;
        }

        int rows = back > 0 ? 2 : 1;
        float width = spacing * (front - 1) + 3.5f;
        float dist = Mathf.Max(8.6f, width * 0.84f) + (rows - 1) * 2.4f;
        var center = origin - dir * ((rows - 1) * rowGap * 0.5f);
        var camPos = center + dir * dist + right * 0.1f + Vector3.up * (2.5f + (rows - 1) * 1.9f);
        var look = center + Vector3.up * (rows == 1 ? 0.65f : 0.85f);
        float k2 = 1f - Mathf.Exp(-5f * dt);
        cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, k2);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), k2);
        cam.fieldOfView = 48f;
    }

    float copyNotifyTimer;

    void DrawNetMenu(float w, float h)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var r = new Rect(w / 2 - 290, 75, 580, 560);
        DrawPopCard(r);
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 38), ribbonDriverTex, Loc.T("★ ONLINE MULTIPLAYER ★", "★ オンライン対戦（最大8人） ★"));

        float x = r.x + 35, cw = r.width - 70, y = r.y + 64;
        var info = St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.35f, 0.42f, 0.55f));

        if (Net.Busy)
        {
            // ロビー（参加者一覧）に入る前の接続待ち。入室後は DrawLobby に切り替わる
            string msg = string.IsNullOrEmpty(Net.Message) ? Loc.T("Connecting...", "接続中...") : Net.Message;
            GUI.Label(new Rect(x, y + 60, cw, 60), msg, St(sSmall, 22, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(0.15f, 0.22f, 0.38f)));
            if (DrawModernButton(new Rect(x + cw / 2 - 100, r.yMax - 56, 200, 38), Loc.T("CANCEL", "キャンセル"), "[ESC / B]", new Color(1f, 0.45f, 0.35f), false, true)) LeaveOnline();
            return;
        }

        // プレイヤー名入力
        GUI.Label(new Rect(x, y, 140, 32), Loc.T("YOUR NAME:", "プレイヤー名:"), St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.15f, 0.22f, 0.38f)));
        var nameRect = new Rect(x + 140, y, 220, 32);
        var codeRect = new Rect(x + 255, y + 48 + 30, 130, 42);
        var ipRect = new Rect(x + 255, y + 48 + 105 + 30, 150, 42);
        if (Event.current != null && Event.current.type == EventType.MouseDown
            && !nameRect.Contains(Event.current.mousePosition)
            && !codeRect.Contains(Event.current.mousePosition)
            && !ipRect.Contains(Event.current.mousePosition))
        {
            GUIUtility.keyboardControl = 0;
        }
        string enteredName = GUI.TextField(nameRect, playerName, 12, sField);
        SetPlayerName(enteredName);
        y += 48;

        // インターネット経由（Unity Relay）
        GUI.Label(new Rect(x, y, cw, 26), Loc.T("INTERNET  (Unity Relay - Up to 8 Players)", "インターネット対戦  (Unity Relay・ルームコード)"), St(sSmall, 17, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.45f, 0.85f)));
        if (DrawModernButton(new Rect(x, y + 30, 240, 42), Loc.T("HOST ROOM", "部屋を作る"), "", new Color(1f, 0.62f, 0.1f), true, netFocus == 0)) Net.HostRelay();
        joinCodeInput = GUI.TextField(codeRect, joinCodeInput, 8, sField).ToUpperInvariant();
        if (DrawModernButton(new Rect(x + 395, y + 30, cw - 395, 42), Loc.T("JOIN", "参加"), "", new Color(0.18f, 0.65f, 0.95f), false, netFocus == 1)) Net.JoinRelay(joinCodeInput);

        // IP 直接接続（LAN / VPN）
        y += 105;
        GUI.Label(new Rect(x, y, cw, 26), Loc.T("DIRECT IP  (LAN / VPN / port " + NetSession.DefaultPort + " open)", $"IP直接接続  (LAN / VPN / ポート {NetSession.DefaultPort})"), St(sSmall, 17, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.45f, 0.85f)));
        if (DrawModernButton(new Rect(x, y + 30, 240, 42), Loc.T("HOST (LISTEN)", "ホスト待機"), "", new Color(0.65f, 0.35f, 0.92f), false, netFocus == 2)) Net.HostDirect();
        ipInput = GUI.TextField(ipRect, ipInput, 40, sField);
        if (DrawModernButton(new Rect(x + 415, y + 30, cw - 415, 42), Loc.T("JOIN", "参加"), "", new Color(0.18f, 0.65f, 0.95f), false, netFocus == 3)) Net.JoinDirect(ipInput);

        if (!string.IsNullOrEmpty(Net.Message))
            GUI.Label(new Rect(x, y + 84, cw, 40), Net.Message, St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(0.95f, 0.3f, 0.25f)));
        GUI.Label(new Rect(x, r.yMax - 98, cw, 40), Loc.T("Host picks the course and starts the race. Up to 8 players, CPU karts fill empty slots.", "ホストがコースを選択してレースを開始します。最大8人まで参加でき、空席はCPUが補充します。"), info);
        if (DrawModernButton(new Rect(x + cw / 2 - 100, r.yMax - 50, 200, 38), Loc.T("CLOSE", "閉じる"), "[ESC / B]", new Color(0.2f, 0.85f, 0.45f), false, netFocus == 4)) netMenu = false;
    }

    // オンラインロビー：3D ステージ上に全員のカートを並べ、頭上に名前タグ、下に操作パネルを表示する
    void DrawLobby(float w, float h)
    {
        if (copyNotifyTimer > 0) copyNotifyTimer -= Time.deltaTime;
        float scale = Screen.height / 720f;
        var gold = new Color(1f, 0.85f, 0.25f);

        // 上部バー：タイトル・ルームコード・コース
        GUI.color = new Color(0.04f, 0.07f, 0.18f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, w, 84), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, 0, w, 84), 1, new Color(1f, 0.85f, 0.25f, 0.5f));
        Outlined(new Rect(24, 8, 360, 40), Loc.T("ONLINE LOBBY", "オンラインロビー"), St(sMid, 30, TextAnchor.MiddleLeft), gold, 2);
        GUI.Label(new Rect(28, 50, 360, 26), Loc.T($"PLAYERS  {playerKarts.Count} / 8   (CPU fills empty slots)", $"参加人数  {playerKarts.Count} / 8人   (空席はCPUが走行)"), St(sSmall, 14, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.85f, 0.9f, 1f)));

        if (!string.IsNullOrEmpty(Net.JoinCode))
        {
            GUI.Label(new Rect(w / 2 - 160, 4, 320, 18), Loc.T("ROOM CODE (CLICK TO COPY)", "ルームコード（コピーして共有）"), St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.7f, 0.85f, 1f)));
            Outlined(new Rect(w / 2 - 160, 20, 200, 56), Net.JoinCode, St(sNum, 46), new Color(0.4f, 1f, 0.6f), 3);
            string copyBtnText = copyNotifyTimer > 0 ? Loc.T("★ COPIED!", "★ コピー済!") : Loc.T("► COPY", "► コピー");
            Color copyBtnCol = copyNotifyTimer > 0 ? new Color(0.2f, 0.82f, 0.45f) : new Color(0.18f, 0.62f, 0.95f);
            if (DrawModernButton(new Rect(w / 2 + 50, 26, 114, 38), copyBtnText, "", copyBtnCol, false, !Net.IsHost && lobbyFocus == 2))
            {
                GUIUtility.systemCopyBuffer = Net.JoinCode;
                copyNotifyTimer = 2.0f;
            }
        }
        else
        {
            Outlined(new Rect(w / 2 - 150, 20, 300, 50),
                Net.IsHost ? Loc.T("DIRECT  (port " + NetSession.DefaultPort + ")", $"IP直接接続 (ポート {NetSession.DefaultPort})") : Loc.T("DIRECT CONNECTION", "IP直接接続"),
                St(sMid, 22), new Color(0.4f, 1f, 0.6f), 2);
        }

        var curDef = Track.Courses[SelectedCourse];
        string cName = Loc.CourseName(SelectedCourse, curDef.Name);
        GUI.Label(new Rect(w - 384, 8, 360, 36), $"◄  {cName}  ►", St(sMid, Loc.IsJa ? 21 : 24, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, false, Color.white));
        string diffBadge = Loc.CourseShortDifficulty(SelectedCourse);
        string courseInfo = Net.IsHost
            ? Loc.T($"[A/D / LB/RB] CHANGE  •  {diffBadge}  •  {totalLaps} LAPS", $"[A/D / LB/RB] コース変更  •  {diffBadge}  •  {totalLaps}周")
            : Loc.T($"{diffBadge}  •  {totalLaps} LAPS", $"{diffBadge}  •  {totalLaps}周");
        GUI.Label(new Rect(w - 384, 48, 360, 26), courseInfo, St(sSmall, 13, TextAnchor.MiddleRight, FontStyle.Bold, false, new Color(0.7f, 0.9f, 1f)));

        float py = h - 190;

        // 各カート頭上の名前タグ（奥から手前へ描画し、手前のタグが手前に重なるようにする）
        var sortedSlots = new List<int>(lobbySlots);
        sortedSlots.Sort((a, b) =>
        {
            float da = Vector3.Dot(Karts[a].transform.position - cam.transform.position, cam.transform.forward);
            float db = Vector3.Dot(Karts[b].transform.position - cam.transform.position, cam.transform.forward);
            return db.CompareTo(da);
        });

        for (int i = 0; i < sortedSlots.Count; i++)
        {
            int slot = sortedSlots[i];
            int orderIndex = lobbySlots.IndexOf(slot);
            UnpackDriverKart(playerKarts[slot], out int drvIdx, out int krtIdx);
            var d = KartCharacters[drvIdx];
            var m = KartMachines[krtIdx];
            var sp = cam.WorldToScreenPoint(Karts[slot].transform.position + Vector3.up * 1.75f);
            if (sp.z <= 0f) continue;
            float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;

            // 上部バー（ROOM CODE等）・左右画面端・下部操作パネルへの侵入を防止する安全ガード
            float tw = 186f;
            gx = Mathf.Clamp(gx, tw * 0.5f + 12f, w - tw * 0.5f - 12f);
            gy = Mathf.Clamp(gy, 136f, py - 30f);

            bool me = slot == mySlot;
            string custom = playerNames.TryGetValue(slot, out var pn) && !string.IsNullOrEmpty(pn) ? pn : (me ? DisplayName : (slot == 5 ? "HOST" : $"P{orderIndex + 1}"));
            string role = me
                ? (slot == 5 ? Loc.T("YOU · HOST", "自分 · ホスト") : Loc.T("YOU", "自分"))
                : (slot == 5 ? Loc.T("HOST", "ホスト") : $"P{orderIndex + 1}");
            var bg = me ? new Color(0.15f, 0.7f, 0.38f) : new Color(0.16f, 0.2f, 0.4f);
            DrawPopPill(new Rect(gx - tw / 2, gy - 44, tw, 20), $"{role}: {custom}", bg);
            DrawPopPill(new Rect(gx - tw / 2, gy - 22, tw, 22), $"{Loc.DriverName(drvIdx, d.name)} • {Loc.KartName(krtIdx, m.name)}", m.color * 0.85f);
        }

        // 下部パネル
        GUI.color = new Color(0.04f, 0.07f, 0.18f, 0.88f);
        GUI.DrawTexture(new Rect(0, py, w, 190), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, py, w, 190), 1, new Color(1f, 0.85f, 0.25f, 0.5f));

        // 左：自分のドライバー＆マシン選択
        int myDrvIdx = Mathf.Clamp(SelectedDriver, 0, KartCharacters.Length - 1);
        int myKrtIdx = Mathf.Clamp(SelectedKart, 0, KartMachines.Length - 1);
        var myDrv = KartCharacters[myDrvIdx];
        var myKrt = KartMachines[myKrtIdx];
        GetCombinedStats(SelectedDriver, SelectedKart, out int spd, out int acc, out int hdl, out int wgt);
        var card = new Rect(24, py + 12, 380, 166);
        DrawPopCard(card);
        if (lobbyFocus == 0)
            DrawFocusHighlight(new Rect(card.x + 10, card.y + 6, card.width - 20, 30), 7f);
        if (DrawModernButton(new Rect(card.x + 12, card.y + 8, 30, 26), "◄", "", myDrv.color)) SwitchDriver(0, -1);
        Outlined(new Rect(card.x + 44, card.y + 7, card.width - 88, 28),
            Loc.T($"DRIVER: {myDrv.name}", $"ドライバー: {Loc.DriverName(myDrvIdx, myDrv.name)}"),
            St(sMid, 16, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), myDrv.color, 1.5f);
        if (DrawModernButton(new Rect(card.xMax - 42, card.y + 8, 30, 26), "►", "", myDrv.color)) SwitchDriver(0, 1);

        if (lobbyFocus == 1)
            DrawFocusHighlight(new Rect(card.x + 10, card.y + 36, card.width - 20, 30), 7f);
        if (DrawModernButton(new Rect(card.x + 12, card.y + 38, 30, 26), "◄", "", myKrt.color)) SwitchKart(0, -1);
        Outlined(new Rect(card.x + 44, card.y + 37, card.width - 88, 28),
            Loc.T($"KART: {myKrt.name}", $"マシン: {Loc.KartName(myKrtIdx, myKrt.name)}"),
            St(sMid, 16, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), myKrt.color, 1.5f);
        if (DrawModernButton(new Rect(card.xMax - 42, card.y + 38, 30, 26), "►", "", myKrt.color)) SwitchKart(0, 1);

        float gy0 = card.y + 70;
        DrawToonStatGauge(card.x + 18, gy0 + 0, card.width - 36, Loc.T("SPEED",  "最高速"), spd, 8, new Color(0.08f, 0.72f, 0.98f));
        DrawToonStatGauge(card.x + 18, gy0 + 19, card.width - 36, Loc.T("ACCEL",  "加速力"), acc, 8, new Color(1f, 0.72f, 0.05f));
        DrawToonStatGauge(card.x + 18, gy0 + 38, card.width - 36, Loc.T("STEER",  "旋回性"), hdl, 8, new Color(0.25f, 0.85f, 0.35f));
        DrawToonStatGauge(card.x + 18, gy0 + 57, card.width - 36, Loc.T("WEIGHT", "重さ"),   wgt, 8, new Color(0.95f, 0.42f, 0.25f));
        GUI.Label(new Rect(card.x + 10, card.y + 146, card.width - 20, 18), Loc.T("[W/S / D-Pad] Driver   •   [Q/E / D-Pad] Kart", "[W/S / 十字キー] ドライバー   •   [Q/E / 十字キー] マシン"), St(sSmall, 11, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.45f, 0.5f, 0.65f)));

        // 中央：スタート
        float cx = w / 2 + 16f;
        if (Net.IsHost)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            float btnScale = 1f + pulse * 0.04f;
            var br = new Rect(Mathf.Round(cx - 190f), Mathf.Round(py + 40f), 380f, 64f);
            Matrix4x4 prevMat = GUI.matrix;
            Vector3 btnPivot = new Vector3(br.center.x, br.center.y, 0f);
            GUI.matrix = prevMat
                * Matrix4x4.Translate(btnPivot)
                * Matrix4x4.Scale(new Vector3(btnScale, btnScale, 1f))
                * Matrix4x4.Translate(-btnPivot);

            var raceSh = TextureGen.RoundedShadow(380, 64, 32f);
            GUI.DrawTexture(new Rect(br.x, br.y + 4f, br.width, br.height), raceSh);
            var raceBtn = TextureGen.RaceStartButton(380, 64);
            GUI.DrawTexture(br, raceBtn);
            if (lobbyFocus == 2)
                DrawFocusHighlight(br, 32f);
            Outlined(br, Loc.T("►► START RACE ◄◄", "►► レース開始！ ◄◄"), St(sMid, 26, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), Color.white, 2.5f);
            bool startClicked = GUI.Button(br, GUIContent.none, GUIStyle.none);
            GUI.matrix = prevMat;
            if (startClicked) StartRaceFromTitle();
            GUI.Label(new Rect(cx - 190, py + 118, 380, 24), Loc.T("[ENTER / A / START] to start for everyone", "[ENTER / A / START] で全員のレースを開始"), St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.7f, 0.85f, 1f)));
        }
        else
        {
            string dots = new string('.', 1 + (int)(Time.time * 2f) % 3);
            Outlined(new Rect(cx - 220, py + 44, 440, 50), Loc.T("Waiting for host" + dots, "ホストの開始を待っています" + dots), St(sMid, 24), Color.white, 2);
        }

        // 右：退出
        if (DrawModernButton(new Rect(w - 270, py + 122, 246, 44), Loc.T("◄ LEAVE ROOM", "◄ ルームを退出"), "[ESC / B]", new Color(0.92f, 0.32f, 0.32f), false, lobbyFocus == 3)) LeaveOnline();
    }
}
