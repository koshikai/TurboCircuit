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
        playerKarts[5] = SelectedKart;
        // 未割り当ての人間スロットを探す
        int nextSlot = HumanSlots.FirstOrDefault(s => !playerKarts.ContainsKey(s));
        if (nextSlot == 0 && playerKarts.ContainsKey(0)) nextSlot = 6;
        clientSlots[conn] = nextSlot;
        playerKarts[nextSlot] = 0; // 初期選択
        Net.SendWelcome(conn, nextSlot, SelectedCourse);
        SyncLobby();
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
            SyncLobby();
            Banner("PLAYER LEFT", new Color(1f, 0.4f, 0.3f), 0);
        }
    }

    void OnNetClientHello(Unity.Networking.Transport.NetworkConnection conn, int kartChar, string pName)
    {
        if (clientSlots.TryGetValue(conn, out int slot))
        {
            playerKarts[slot] = Mathf.Clamp(kartChar, 0, KartCharacters.Length - 1);
            playerNames[slot] = string.IsNullOrEmpty(pName) ? $"P{slot + 1}" : pName;
            SyncLobby();
        }
    }

    void OnNetClientConnected()
    {
        if (TwoPlayer) SetTwoPlayer(false);
        if (Net.IsHost)
        {
            playerKarts[5] = SelectedKart;
            playerNames[5] = DisplayName;
            SyncLobby();
        }
        else
        {
            Net.SendHello(SelectedKart, DisplayName);
        }
        if (Audio != null) Audio.Select();
    }

    void OnNetWelcome(int assignedSlot, int course)
    {
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
        Net.SendHello(SelectedKart, DisplayName);
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
        playerKarts[5] = SelectedKart;
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
            int kIdx = Mathf.Clamp(kv.Value, 0, KartCharacters.Length - 1);
            var d = KartCharacters[kIdx];
            var k = Karts[slot];
            k.ModelVariant = kIdx;
            k.Color = d.color;
            k.ApplyStats(d.speed, d.accel, d.handling, d.weight);

            int order = System.Array.IndexOf(HumanSlots, slot);
            string customName = playerNames.TryGetValue(slot, out var pn) && !string.IsNullOrEmpty(pn) ? pn : "";
            string title;
            if (slot == mySlot)
            {
                title = string.IsNullOrEmpty(customName) ? $"{DisplayName} ({d.name})" : $"{customName} ({d.name})";
            }
            else
            {
                string fallback = (slot == 5 ? "HOST" : $"P{order + 1}");
                title = string.IsNullOrEmpty(customName) ? $"{fallback} ({d.name})" : $"{customName} ({d.name})";
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
        playerKarts.Clear();
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
            Banner("HOST DISCONNECTED", new Color(1f, 0.4f, 0.3f), 0);
        }
    }

    // スロット配置をシングルプレイ状態に元に戻す
    void RestoreRoles()
    {
        playerKarts.Clear();
        clientSlots.Clear();
        mySlot = playerGridSlot;
        for (int i = 0; i < Karts.Count; i++)
        {
            if (i == playerGridSlot)
            {
                Player = Karts[i];
                Player.IsRemote = false;
                ConfigureHuman(0);
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
        float dist = Mathf.Max(7.2f, width * 0.72f) + (rows - 1) * 3.0f;
        var center = origin - dir * ((rows - 1) * rowGap * 0.5f);
        var camPos = center + dir * dist + right * 0.8f + Vector3.up * (2.4f + (rows - 1) * 2.0f);
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
        DrawPopRibbon(new Rect(r.x + 20, r.y + 12, r.width - 40, 38), ribbonDriverTex, "★ ONLINE MULTIPLAYER ★");

        float x = r.x + 35, cw = r.width - 70, y = r.y + 64;
        var info = St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.35f, 0.42f, 0.55f));

        if (Net.Busy)
        {
            // ロビー（参加者一覧）に入る前の接続待ち。入室後は DrawLobby に切り替わる
            string msg = string.IsNullOrEmpty(Net.Message) ? "Connecting..." : Net.Message;
            GUI.Label(new Rect(x, y + 60, cw, 60), msg, St(sSmall, 22, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(0.15f, 0.22f, 0.38f)));
            if (DrawModernButton(new Rect(x + cw / 2 - 100, r.yMax - 56, 200, 38), "CANCEL", "[ESC]", new Color(1f, 0.45f, 0.35f))) LeaveOnline();
            return;
        }

        // プレイヤー名入力
        GUI.Label(new Rect(x, y, 140, 32), "YOUR NAME:", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.15f, 0.22f, 0.38f)));
        string enteredName = GUI.TextField(new Rect(x + 140, y, 220, 32), playerName, 12, sField);
        SetPlayerName(enteredName);
        y += 48;

        // インターネット経由（Unity Relay）
        GUI.Label(new Rect(x, y, cw, 26), "INTERNET  (Unity Relay - Up to 8 Players)", St(sSmall, 17, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.45f, 0.85f)));
        if (DrawModernButton(new Rect(x, y + 30, 240, 42), "HOST ROOM", "", new Color(1f, 0.62f, 0.1f), true)) Net.HostRelay();
        joinCodeInput = GUI.TextField(new Rect(x + 255, y + 30, 130, 42), joinCodeInput, 8, sField).ToUpperInvariant();
        if (DrawModernButton(new Rect(x + 395, y + 30, cw - 395, 42), "JOIN", "", new Color(0.18f, 0.65f, 0.95f))) Net.JoinRelay(joinCodeInput);

        // IP 直接接続（LAN / VPN）
        y += 105;
        GUI.Label(new Rect(x, y, cw, 26), "DIRECT IP  (LAN / VPN / port " + NetSession.DefaultPort + " open)", St(sSmall, 17, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.12f, 0.45f, 0.85f)));
        if (DrawModernButton(new Rect(x, y + 30, 240, 42), "HOST (LISTEN)", "", new Color(0.65f, 0.35f, 0.92f))) Net.HostDirect();
        ipInput = GUI.TextField(new Rect(x + 255, y + 30, 150, 42), ipInput, 40, sField);
        if (DrawModernButton(new Rect(x + 415, y + 30, cw - 415, 42), "JOIN", "", new Color(0.18f, 0.65f, 0.95f))) Net.JoinDirect(ipInput);

        if (!string.IsNullOrEmpty(Net.Message))
            GUI.Label(new Rect(x, y + 84, cw, 40), Net.Message, St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(0.95f, 0.3f, 0.25f)));
        GUI.Label(new Rect(x, r.yMax - 98, cw, 40), "Host picks the course and starts the race. Up to 8 players, CPU karts fill empty slots.", info);
        if (DrawModernButton(new Rect(x + cw / 2 - 100, r.yMax - 50, 200, 38), "CLOSE", "[ESC]", new Color(0.2f, 0.85f, 0.45f))) netMenu = false;
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
        Outlined(new Rect(24, 8, 360, 40), "ONLINE LOBBY", St(sMid, 32, TextAnchor.MiddleLeft), gold, 2);
        GUI.Label(new Rect(28, 50, 360, 26), $"PLAYERS  {playerKarts.Count} / 8   (CPU fills empty slots)", St(sSmall, 15, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.85f, 0.9f, 1f)));

        if (!string.IsNullOrEmpty(Net.JoinCode))
        {
            GUI.Label(new Rect(w / 2 - 160, 4, 320, 18), "ROOM CODE (CLICK TO COPY)", St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.7f, 0.85f, 1f)));
            Outlined(new Rect(w / 2 - 160, 20, 200, 56), Net.JoinCode, St(sBig, 48), new Color(0.4f, 1f, 0.6f), 3);
            string copyBtnText = copyNotifyTimer > 0 ? "✓ COPIED!" : "📋 COPY";
            if (GUI.Button(new Rect(w / 2 + 50, 28, 100, 38), copyBtnText, sButton))
            {
                GUIUtility.systemCopyBuffer = Net.JoinCode;
                copyNotifyTimer = 2.0f;
            }
        }
        else
        {
            Outlined(new Rect(w / 2 - 150, 20, 300, 50), Net.IsHost ? "DIRECT  (port " + NetSession.DefaultPort + ")" : "DIRECT CONNECTION", St(sMid, 22), new Color(0.4f, 1f, 0.6f), 2);
        }

        var curDef = Track.Courses[SelectedCourse];
        GUI.Label(new Rect(w - 384, 8, 360, 36), $"◄  {curDef.Name.ToUpper()}  ►", St(sSmall, 24, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, false, Color.white));
        string diffBadge = SelectedCourse == 0 ? "★☆☆ NOVICE" : SelectedCourse == 1 ? "★★☆ ADVANCED" : SelectedCourse == 2 ? "★★★ EXPERT" : "★★☆ URBAN";
        string courseInfo = Net.IsHost ? $"[A][D] CHANGE  •  {diffBadge}  •  {totalLaps} LAPS" : $"{diffBadge}  •  {totalLaps} LAPS";
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
            var d = KartCharacters[Mathf.Clamp(playerKarts[slot], 0, KartCharacters.Length - 1)];
            var sp = cam.WorldToScreenPoint(Karts[slot].transform.position + Vector3.up * 1.85f);
            if (sp.z <= 0f) continue;
            float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;

            // 上部バー（ROOM CODE等）や下部操作パネルへの侵入を防止する安全ガード
            gy = Mathf.Clamp(gy, 136f, py - 30f);

            bool me = slot == mySlot;
            string custom = playerNames.TryGetValue(slot, out var pn) && !string.IsNullOrEmpty(pn) ? pn : (me ? DisplayName : (slot == 5 ? "HOST" : $"P{orderIndex + 1}"));
            string role = me ? (slot == 5 ? "YOU · HOST" : "YOU") : (slot == 5 ? "HOST" : $"P{orderIndex + 1}");
            float tw = 160f;
            var bg = me ? new Color(0.15f, 0.7f, 0.38f) : new Color(0.16f, 0.2f, 0.4f);
            DrawPopPill(new Rect(gx - tw / 2, gy - 44, tw, 20), $"{role}: {custom}", bg);
            DrawPopPill(new Rect(gx - tw / 2, gy - 22, tw, 22), d.name, d.color * 0.85f);
        }

        // 下部パネル
        GUI.color = new Color(0.04f, 0.07f, 0.18f, 0.88f);
        GUI.DrawTexture(new Rect(0, py, w, 190), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(new Rect(0, py, w, 190), 1, new Color(1f, 0.85f, 0.25f, 0.5f));

        // 左：自分のドライバー
        var me2 = KartCharacters[SelectedKart];
        var card = new Rect(24, py + 14, 340, 164);
        DrawPopCard(card);
        Outlined(new Rect(card.x + 8, card.y + 6, card.width - 16, 28), $"< {me2.name} >", St(sSmall, 21, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), me2.color, 2);
        DrawPopPill(new Rect(card.x + 20, card.y + 38, card.width - 40, 20), me2.trait, me2.color * 0.9f);
        float gy0 = card.y + 66;
        DrawToonStatGauge(card.x + 20, gy0 + 0, card.width - 40, "SPEED", me2.speed, 8, new Color(0.08f, 0.72f, 0.98f));
        DrawToonStatGauge(card.x + 20, gy0 + 22, card.width - 40, "ACCEL", me2.accel, 8, new Color(1f, 0.72f, 0.05f));
        DrawToonStatGauge(card.x + 20, gy0 + 44, card.width - 40, "STEER", me2.handling, 8, new Color(0.25f, 0.85f, 0.35f));
        GUI.Label(new Rect(card.x + 10, card.y + 138, card.width - 20, 20), "[W][S]  change driver", St(sSmall, 12, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.45f, 0.5f, 0.65f)));

        // 中央：スタート
        float cx = w / 2;
        if (Net.IsHost)
        {
            float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            var br = new Rect(cx - 190, py + 40, 380, 64 + pulse * 4f);
            if (btnRaceTex != null) GUI.DrawTexture(br, btnRaceTex, ScaleMode.StretchToFill);
            else { GUI.color = new Color(1f, 0.7f, 0.1f); GUI.DrawTexture(br, Texture2D.whiteTexture); GUI.color = Color.white; DrawFrame(br, 3, new Color(0.1f, 0.15f, 0.3f)); }
            Outlined(br, "►► START RACE ◄◄", St(sMid, 26, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic), Color.white, 2.5f);
            if (GUI.Button(br, GUIContent.none, GUIStyle.none)) StartRaceFromTitle();
            GUI.Label(new Rect(cx - 190, py + 118, 380, 24), "[ENTER] to start for everyone", St(sSmall, 14, TextAnchor.MiddleCenter, FontStyle.Normal, false, new Color(0.7f, 0.85f, 1f)));
        }
        else
        {
            string dots = new string('.', 1 + (int)(Time.time * 2f) % 3);
            Outlined(new Rect(cx - 220, py + 44, 440, 50), "Waiting for host" + dots, St(sMid, 26), Color.white, 2);
        }

        // 右：退出
        if (GUI.Button(new Rect(w - 224, py + 126, 200, 44), "LEAVE ROOM  [ESC]", sButton)) LeaveOnline();
    }
}
