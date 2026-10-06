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
        const float spacing = 3.4f, rowGap = 4.5f;

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
        float dist = Mathf.Max(6f, width * 0.78f) + (rows - 1) * 2.5f;
        var center = origin - dir * ((rows - 1) * rowGap * 0.5f);
        var camPos = center + dir * dist + right * 1.2f + Vector3.up * (1.6f + rows * 0.9f);
        var look = center + Vector3.up * -0.6f;
        float k2 = 1f - Mathf.Exp(-5f * dt);
        cam.transform.position = Vector3.Lerp(cam.transform.position, camPos, k2);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), k2);
        cam.fieldOfView = 48f;
    }

    void DrawNetMenu(float w, float h)
    {
        var r = new Rect(w / 2 - 290, 80, 580, 550);
        GUI.color = new Color(0.04f, 0.07f, 0.18f, 0.97f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(r, 3, new Color(1f, 0.85f, 0.25f));
        Outlined(new Rect(r.x, r.y + 8, r.width, 50), "ONLINE MULTIPLAYER", St(sMid, 34), new Color(1f, 0.85f, 0.25f), 2);
        float x = r.x + 30, cw = r.width - 60, y = r.y + 60;
        var info = St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Normal, true, new Color(0.85f, 0.9f, 1f));

        if (Net.Busy)
        {
            // ロビー（参加者一覧）に入る前の接続待ち。入室後は DrawLobby に切り替わる
            string msg = string.IsNullOrEmpty(Net.Message) ? "Connecting..." : Net.Message;
            GUI.Label(new Rect(x, y + 60, cw, 60), msg, St(sSmall, 22, TextAnchor.MiddleCenter, FontStyle.Bold, true, Color.white));
            if (GUI.Button(new Rect(x + cw / 2 - 100, r.yMax - 56, 200, 38), "CANCEL  [ESC]", sButton)) LeaveOnline();
            return;
        }

        // プレイヤー名入力
        GUI.Label(new Rect(x, y, 130, 32), "YOUR NAME:", St(sSmall, 16, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(1f, 0.9f, 0.5f)));
        string enteredName = GUI.TextField(new Rect(x + 130, y, 220, 32), playerName, 12, sField);
        SetPlayerName(enteredName);
        y += 44;

        // インターネット経由（Unity Relay）
        GUI.Label(new Rect(x, y, cw, 26), "INTERNET  (Unity Relay - Up to 8 Players)", St(sSmall, 18, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.5f, 0.85f, 1f)));
        if (GUI.Button(new Rect(x, y + 32, 250, 40), "HOST ROOM", sButton)) Net.HostRelay();
        joinCodeInput = GUI.TextField(new Rect(x + 270, y + 32, 120, 40), joinCodeInput, 8, sField).ToUpperInvariant();
        if (GUI.Button(new Rect(x + 400, y + 32, cw - 400, 40), "JOIN", sButton)) Net.JoinRelay(joinCodeInput);

        // IP 直接接続（LAN / VPN）
        y += 105;
        GUI.Label(new Rect(x, y, cw, 26), "DIRECT IP  (LAN / VPN / port " + NetSession.DefaultPort + " open)", St(sSmall, 18, TextAnchor.MiddleLeft, FontStyle.Bold, false, new Color(0.5f, 0.85f, 1f)));
        if (GUI.Button(new Rect(x, y + 32, 250, 40), "HOST (LISTEN)", sButton)) Net.HostDirect();
        ipInput = GUI.TextField(new Rect(x + 270, y + 32, 150, 40), ipInput, 40, sField);
        if (GUI.Button(new Rect(x + 430, y + 32, cw - 430, 40), "JOIN", sButton)) Net.JoinDirect(ipInput);

        if (!string.IsNullOrEmpty(Net.Message))
            GUI.Label(new Rect(x, y + 84, cw, 40), Net.Message, St(sSmall, 15, TextAnchor.MiddleCenter, FontStyle.Bold, true, new Color(1f, 0.5f, 0.4f)));
        GUI.Label(new Rect(x, r.yMax - 98, cw, 40), "Host picks the course and starts the race. Up to 8 players, CPU karts fill empty slots.", info);
        if (GUI.Button(new Rect(x + cw / 2 - 100, r.yMax - 50, 200, 38), "CLOSE  [ESC]", sButton)) netMenu = false;
    }

    // オンラインロビー：3D ステージ上に全員のカートを並べ、頭上に名前タグ、下に操作パネルを表示する
    void DrawLobby(float w, float h)
    {
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
            GUI.Label(new Rect(w / 2 - 150, 4, 300, 20), "ROOM CODE", St(sSmall, 13, TextAnchor.MiddleCenter, FontStyle.Bold, false, new Color(0.7f, 0.85f, 1f)));
            Outlined(new Rect(w / 2 - 150, 20, 300, 60), Net.JoinCode, St(sBig, 52), new Color(0.4f, 1f, 0.6f), 3);
        }
        else
        {
            Outlined(new Rect(w / 2 - 150, 20, 300, 50), Net.IsHost ? "DIRECT  (port " + NetSession.DefaultPort + ")" : "DIRECT CONNECTION", St(sMid, 22), new Color(0.4f, 1f, 0.6f), 2);
        }

        var curDef = Track.Courses[SelectedCourse];
        GUI.Label(new Rect(w - 384, 8, 360, 40), $"◄  {curDef.Name.ToUpper()}  ►", St(sSmall, 24, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, false, Color.white));
        GUI.Label(new Rect(w - 384, 50, 360, 26), Net.IsHost ? "[A][D]  you pick the course" : "course is picked by the host", St(sSmall, 14, TextAnchor.MiddleRight, FontStyle.Normal, false, new Color(0.7f, 0.85f, 1f)));

        // 各カート頭上の名前タグ
        for (int i = 0; i < lobbySlots.Count; i++)
        {
            int slot = lobbySlots[i];
            var d = KartCharacters[Mathf.Clamp(playerKarts[slot], 0, KartCharacters.Length - 1)];
            var sp = cam.WorldToScreenPoint(Karts[slot].transform.position + Vector3.up * 1.9f);
            if (sp.z <= 0f) continue;
            float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;
            bool me = slot == mySlot;
            string custom = playerNames.TryGetValue(slot, out var pn) && !string.IsNullOrEmpty(pn) ? pn : (me ? DisplayName : (slot == 5 ? "HOST" : $"P{i + 1}"));
            string role = me ? (slot == 5 ? "YOU · HOST" : "YOU") : (slot == 5 ? "HOST" : $"P{i + 1}");
            float tw = 160f;
            var bg = me ? new Color(0.15f, 0.7f, 0.38f) : new Color(0.16f, 0.2f, 0.4f);
            DrawPopPill(new Rect(gx - tw / 2, gy - 44, tw, 20), $"{role}: {custom}", bg);
            DrawPopPill(new Rect(gx - tw / 2, gy - 22, tw, 22), d.name, d.color * 0.85f);
        }

        // 下部パネル
        float py = h - 190;
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
