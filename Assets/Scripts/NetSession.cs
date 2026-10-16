using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

// 1 台のカートの同期用スナップショット（持ち主が送り、相手側ではゴーストとして再現する）
public struct NetKartState
{
    public const byte FDrift = 1, FBoost = 2, FShield = 4, FStar = 4, FSpin = 8, FAir = 16, FBrake = 32, FDone = 64, FTrick = 128;

    public Vector3 Pos;
    public float Heading, Speed, Steer, Progress, FinishTime;
    public byte Flags;
    public int DriftLevel, DriftDir, Lap, MaxLap;
}

// 最大 8 人対戦用のネットワーク層。Unity Transport の上に最小限のメッセージを載せる。
// 接続方法は Unity Relay（ルームコード）と、IP 直接接続（LAN / VPN / ポート開放済み）の 2 種類。
// ホスト = 部屋の管理者（コース決定・スタート・AI カート・アイテム中継を担当）。
public class NetSession : MonoBehaviour
{
    public enum Phase { Offline, Starting, Waiting, Connecting, Connected }

    public enum Msg : byte
    {
        Hello = 1,          // クライアント -> ホスト: 選択カートキャラ番号
        Course = 2,         // ホスト -> 全員: 選択コース番号
        Start = 3,          // ホスト -> 全員: レース開始（コース番号）
        State = 4,          // ホスト <-> クライアント: カート状態スナップショット
        BananaSpawn = 5,    // 設置 -> 全員中継
        BananaGone = 6,     // 消滅 -> 全員中継
        MissileSpawn = 7,   // 発射 -> 全員中継
        Welcome = 8,        // ホスト -> 新規クライアント: [割り当てスロット, コース番号]
        LobbySync = 9,      // ホスト -> 全員: 参加プレイヤー一覧 [count, {slot, kartChar}...]
        BombSpawn = 10,     // 投擲 -> 全員中継
        BombExplode = 11,   // 爆発 -> 全員中継
        ThunderUse = 12     // サンダー発動 -> 全員中継 [userSlot]
    }

    public const ushort DefaultPort = 7777;
    public const int MaxClients = 7; // 最大 7 人のクライアント（ホスト含めて計 8 人）
    const float PingSeconds = 0.05f;

    public Phase State { get; private set; }
    public bool IsHost { get; private set; }
    public string JoinCode { get; set; } = "";
    public string Message { get; private set; } = "";
    public bool Connected => IsHost ? (hostConnections.Count > 0) : (State == Phase.Connected);
    public bool Busy => State != Phase.Offline;
    public bool SendDue => (Connected || (IsHost && State == Phase.Connected)) && Time.unscaledTime >= nextSend;
    public int ConnectedCount => IsHost ? (hostConnections.Count + 1) : (Connected ? syncedPlayerCount : 1);

    public Action OnConnected, OnDisconnected;
    public Action<NetworkConnection, int, string> OnClientHello;
    public Action<NetworkConnection> OnClientConnected, OnClientDisconnected;
    public Action<int, int> OnWelcome; // (assignedSlot, course)
    public Action<List<(int slot, int kartChar, string name)>> OnLobbySync;
    public Action<int> OnCourse, OnStart, OnBananaGone, OnBombExplode, OnThunder;
    public Action<int, NetKartState> OnState;
    public Action<int, int, Vector3> OnBanana;
    public Action<int, int> OnMissile;
    public Action<int, int, Vector3, Vector3> OnBomb;

    NetworkDriver driver;
    NetworkConnection clientConn;
    readonly List<NetworkConnection> hostConnections = new List<NetworkConnection>();
    NetworkPipeline reliable;
    float nextSend;
    int syncedPlayerCount = 1;
    int sessionGen;

    // ───────────────────────── 接続 ─────────────────────────

    public async void HostRelay()
    {
        if (Busy) return;
        Begin(true, Loc.T("Contacting Relay...", "Relay サーバーに接続中..."));
        int gen = sessionGen;
        try
        {
            await EnsureServices();
            if (State != Phase.Starting || sessionGen != gen) return;
            var alloc = await RelayService.Instance.CreateAllocationAsync(MaxClients);
            if (State != Phase.Starting || sessionGen != gen) return;
            var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls") ?? alloc.ServerEndpoints.First();
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.ConnectionData, alloc.Key, ep.Secure);
            string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            if (State != Phase.Starting || sessionGen != gen) return; // 途中でキャンセルされた
            var settings = Settings();
            settings.WithRelayParameters(ref data);
            CreateDriver(settings);
            driver.Bind(NetworkEndpoint.AnyIpv4);
            driver.Listen();
            JoinCode = code;
            Debug.Log("[Net] Room code: " + code);
            State = Phase.Waiting;
            Message = Loc.T("Waiting for players...", "プレイヤーの参加を待機中...");
            OnConnected?.Invoke();
        }
        catch (Exception e) { if (sessionGen == gen) Fail(Loc.T("Relay error: ", "Relay エラー: ") + e.Message); }
    }

    public async void JoinRelay(string code)
    {
        if (Busy) return;
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) { Message = Loc.T("Enter the room code.", "ルームコードを入力してください。"); return; }
        Begin(false, Loc.T("Contacting Relay...", "Relay サーバーに接続中..."));
        int gen = sessionGen;
        try
        {
            await EnsureServices();
            if (State != Phase.Starting || sessionGen != gen) return;
            Debug.Log("[Net] Joining room " + code);
            var alloc = await RelayService.Instance.JoinAllocationAsync(code);
            if (State != Phase.Starting || sessionGen != gen) return;
            var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls") ?? alloc.ServerEndpoints.First();
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.HostConnectionData, alloc.Key, ep.Secure);
            var settings = Settings();
            settings.WithRelayParameters(ref data);
            CreateDriver(settings);
            clientConn = driver.Connect(data.Endpoint);
            State = Phase.Connecting;
            Message = Loc.T("Connecting...", "接続中...");
            Debug.Log("[Net] Connecting via Relay");
        }
        catch (Exception e) { if (sessionGen == gen) Fail(Loc.T("Relay error: ", "Relay エラー: ") + e.Message); }
    }

    public void HostDirect(ushort port = DefaultPort)
    {
        if (Busy) return;
        Begin(true, "");
        CreateDriver(Settings());
        if (driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0) { Fail(Loc.T("Could not open port ", "ポートを開けませんでした: ") + port); return; }
        driver.Listen();
        JoinCode = "";
        State = Phase.Waiting;
        Message = Loc.T("Waiting for players on port " + port + "...", $"ポート {port} で参加者を待機中...");
        OnConnected?.Invoke();
    }

    public void JoinDirect(string address)
    {
        if (Busy) return;
        address = (address ?? "").Trim();
        ushort port = DefaultPort;
        int colon = address.LastIndexOf(':');
        if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort p)) { port = p; address = address.Substring(0, colon); }
        if (!NetworkEndpoint.TryParse(address, port, out var endpoint)) { Message = Loc.T("Invalid address.", "無効なIPアドレスです。"); return; }
        Begin(false, Loc.T("Connecting...", "接続中..."));
        CreateDriver(Settings());
        clientConn = driver.Connect(endpoint);
        State = Phase.Connecting;
    }

    public void Disconnect()
    {
        bool wasConnected = Connected;
        Teardown();
        Message = "";
        if (wasConnected) OnDisconnected?.Invoke();
    }

    void Begin(bool host, string message)
    {
        sessionGen++;
        IsHost = host;
        State = Phase.Starting;
        Message = message;
        JoinCode = "";
        syncedPlayerCount = 1;
    }

    void Fail(string message)
    {
        Debug.LogWarning("[Net] " + message);
        Teardown();
        Message = message;
    }

    static async System.Threading.Tasks.Task EnsureServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    static NetworkSettings Settings()
    {
        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 12, disconnectTimeoutMS: 6000, heartbeatTimeoutMS: 500);
        return settings;
    }

    void CreateDriver(NetworkSettings settings)
    {
        if (driver.IsCreated)
        {
            driver.ScheduleUpdate().Complete();
            driver.Dispose();
        }
        driver = NetworkDriver.Create(settings);
        reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
    }

    void Teardown()
    {
        sessionGen++;
        if (driver.IsCreated)
        {
            for (int i = 0; i < hostConnections.Count; i++)
            {
                if (hostConnections[i].IsCreated) driver.Disconnect(hostConnections[i]);
            }
            if (clientConn.IsCreated) driver.Disconnect(clientConn);
            driver.ScheduleUpdate().Complete();
            driver.Dispose();
        }
        hostConnections.Clear();
        clientConn = default;
        State = Phase.Offline;
        JoinCode = "";
        syncedPlayerCount = 1;
    }

    void OnDestroy()
    {
        Teardown();
    }

    // ───────────────────────── 受信 ─────────────────────────

    void Update()
    {
        if (!driver.IsCreated) return;
        driver.ScheduleUpdate().Complete();

        if (IsHost)
        {
            if (State == Phase.Waiting || State == Phase.Connected)
            {
                NetworkConnection accepted;
                while (driver.IsCreated && (accepted = driver.Accept()) != default && accepted.IsCreated)
                {
                    if (hostConnections.Count >= MaxClients)
                    {
                        Debug.LogWarning("[Net] Room full, rejecting client");
                        driver.Disconnect(accepted);
                        continue;
                    }
                    Debug.Log($"[Net] Accepted a client (#{hostConnections.Count + 1})");
                    hostConnections.Add(accepted);
                    State = Phase.Connected;
                    Message = Loc.T($"Connected ({hostConnections.Count + 1} players)", $"接続中 ({hostConnections.Count + 1}人参加)");
                    OnClientConnected?.Invoke(accepted);
                }
            }

            for (int i = hostConnections.Count - 1; i >= 0; i--)
            {
                var conn = hostConnections[i];
                if (!conn.IsCreated) { hostConnections.RemoveAt(i); continue; }
                NetworkEvent.Type cmd;
                while (driver.IsCreated && conn.IsCreated && (cmd = driver.PopEventForConnection(conn, out var stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        Handle(conn, ref stream);
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log($"[Net] Client disconnected ({hostConnections.Count - 1} remaining)");
                        hostConnections.RemoveAt(i);
                        OnClientDisconnected?.Invoke(conn);
                        if (hostConnections.Count == 0)
                        {
                            State = Phase.Waiting;
                            Message = Loc.T("Waiting for players...", "プレイヤーの参加を待機中...");
                        }
                        else
                        {
                            Message = Loc.T($"Connected ({hostConnections.Count + 1} players)", $"接続中 ({hostConnections.Count + 1}人参加)");
                        }
                        break;
                    }
                }
            }
        }
        else
        {
            if (!clientConn.IsCreated) return;
            NetworkEvent.Type cmd;
            while (driver.IsCreated && clientConn.IsCreated && (cmd = driver.PopEventForConnection(clientConn, out var stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("[Net] Connected to host");
                    State = Phase.Connected;
                    Message = Loc.T("Connected to host", "ホストに接続しました");
                    OnConnected?.Invoke();
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    Handle(clientConn, ref stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("[Net] Disconnected from host (was connected: " + Connected + ")");
                    bool wasConnected = Connected;
                    Teardown();
                    Message = wasConnected ? Loc.T("Disconnected from host.", "ホストから切断されました。") : Loc.T("Could not connect.", "接続できませんでした。");
                    if (wasConnected) OnDisconnected?.Invoke();
                    return;
                }
            }
        }
    }

    void Handle(NetworkConnection sourceConn, ref DataStreamReader r)
    {
        if (r.Length - r.GetBytesRead() < 1) return;
        var type = (Msg)r.ReadByte();
        int remain = r.Length - r.GetBytesRead();
        int need;
        switch (type)
        {
            case Msg.Hello: case Msg.LobbySync: case Msg.Course: case Msg.Start: case Msg.State: case Msg.ThunderUse: need = 1; break;
            case Msg.Welcome: case Msg.MissileSpawn: need = 2; break;
            case Msg.BananaGone: case Msg.BombExplode: need = 4; break;
            case Msg.BananaSpawn: need = 17; break;
            case Msg.BombSpawn: need = 29; break;
            default: return;
        }
        if (remain < need) return; // 壊れた/途中で切れたパケットは破棄
        switch (type)
        {
            case Msg.Hello:
                int kartChar = r.ReadByte();
                string pName = ReadString(ref r);
                if (IsHost) OnClientHello?.Invoke(sourceConn, kartChar, pName);
                break;
            case Msg.Welcome:
                int mySlot = r.ReadByte();
                int course = r.ReadByte();
                OnWelcome?.Invoke(mySlot, course);
                break;
            case Msg.LobbySync:
                int pCount = r.ReadByte();
                syncedPlayerCount = pCount;
                var pList = new List<(int slot, int kartChar, string name)>(pCount);
                for (int i = 0; i < pCount; i++)
                {
                    if (r.Length - r.GetBytesRead() < 3) break; // slot + kart + name長
                    int slot = r.ReadByte();
                    int kIdx = r.ReadByte();
                    string pNick = ReadString(ref r);
                    pList.Add((slot, kIdx, pNick));
                }
                OnLobbySync?.Invoke(pList);
                break;
            case Msg.Course:
                OnCourse?.Invoke(r.ReadByte());
                break;
            case Msg.Start:
                OnStart?.Invoke(r.ReadByte());
                break;
            case Msg.State:
                int n = r.ReadByte();
                var states = new List<(int id, NetKartState s)>(n);
                for (int i = 0; i < n; i++)
                {
                    if (r.Length - r.GetBytesRead() < 37) break; // 1(id) + 36(state)
                    int id = r.ReadByte();
                    var s = ReadState(ref r);
                    states.Add((id, s));
                    OnState?.Invoke(id, s);
                }
                if (IsHost && states.Count > 0)
                {
                    // ホスト経由で他の全クライアントへ即座に中継
                    BroadcastExcept(sourceConn, false, Msg.State, (ref DataStreamWriter w) => WriteStates(ref w, states));
                }
                break;
            case Msg.BananaSpawn:
                int bid = r.ReadInt(), owner = r.ReadByte();
                var bpos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                OnBanana?.Invoke(bid, owner, bpos);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.BananaSpawn, (ref DataStreamWriter w) =>
                    {
                        w.WriteInt(bid);
                        w.WriteByte((byte)owner);
                        w.WriteFloat(bpos.x); w.WriteFloat(bpos.y); w.WriteFloat(bpos.z);
                    });
                }
                break;
            case Msg.BananaGone:
                int bgId = r.ReadInt();
                OnBananaGone?.Invoke(bgId);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.BananaGone, (ref DataStreamWriter w) => w.WriteInt(bgId));
                }
                break;
            case Msg.MissileSpawn:
                int mOwner = r.ReadByte(), mTarget = r.ReadByte();
                OnMissile?.Invoke(mOwner, mTarget);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.MissileSpawn, (ref DataStreamWriter w) =>
                    {
                        w.WriteByte((byte)mOwner);
                        w.WriteByte((byte)mTarget);
                    });
                }
                break;
            case Msg.BombSpawn:
                int bmbId = r.ReadInt(), bmbOwner = r.ReadByte();
                var bmbPos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                var bmbVel = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                OnBomb?.Invoke(bmbId, bmbOwner, bmbPos, bmbVel);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.BombSpawn, (ref DataStreamWriter w) =>
                    {
                        w.WriteInt(bmbId);
                        w.WriteByte((byte)bmbOwner);
                        w.WriteFloat(bmbPos.x); w.WriteFloat(bmbPos.y); w.WriteFloat(bmbPos.z);
                        w.WriteFloat(bmbVel.x); w.WriteFloat(bmbVel.y); w.WriteFloat(bmbVel.z);
                    });
                }
                break;
            case Msg.BombExplode:
                int beId = r.ReadInt();
                OnBombExplode?.Invoke(beId);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.BombExplode, (ref DataStreamWriter w) => w.WriteInt(beId));
                }
                break;
            case Msg.ThunderUse:
                int tuSlot = r.ReadByte();
                OnThunder?.Invoke(tuSlot);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.ThunderUse, (ref DataStreamWriter w) => w.WriteByte((byte)tuSlot));
                }
                break;
        }
    }

    static NetKartState ReadState(ref DataStreamReader r)
    {
        return new NetKartState
        {
            Pos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat()),
            Heading = r.ReadFloat(),
            Speed = r.ReadFloat(),
            Steer = r.ReadByte() / 127.5f - 1f,
            Progress = r.ReadFloat(),
            FinishTime = r.ReadFloat(),
            Flags = r.ReadByte(),
            DriftLevel = r.ReadByte(),
            DriftDir = r.ReadByte() - 1,
            Lap = r.ReadShort(),
            MaxLap = r.ReadShort(),
        };
    }

    static void WriteStates(ref DataStreamWriter w, List<(int id, NetKartState s)> states)
    {
        w.WriteByte((byte)states.Count);
        foreach (var (id, s) in states)
        {
            w.WriteByte((byte)id);
            w.WriteFloat(s.Pos.x); w.WriteFloat(s.Pos.y); w.WriteFloat(s.Pos.z);
            w.WriteFloat(s.Heading);
            w.WriteFloat(s.Speed);
            w.WriteByte((byte)Mathf.RoundToInt((Mathf.Clamp(s.Steer, -1f, 1f) + 1f) * 127.5f));
            w.WriteFloat(s.Progress);
            w.WriteFloat(s.FinishTime);
            w.WriteByte(s.Flags);
            w.WriteByte((byte)s.DriftLevel);
            w.WriteByte((byte)(s.DriftDir + 1));
            w.WriteShort((short)s.Lap);
            w.WriteShort((short)s.MaxLap);
        }
    }

    // ───────────────────────── 送信 ─────────────────────────

    // DataStreamWriter は書き込み位置を構造体内に持つため、必ず ref で受け渡す（値渡しだと EndSend 時に書いた内容が失われる）
    delegate void WriteFn(ref DataStreamWriter w);

    bool Open(NetworkConnection c, bool isReliable, Msg type, out DataStreamWriter w)
    {
        w = default;
        if (!driver.IsCreated || !c.IsCreated) return false;
        int r = isReliable ? driver.BeginSend(reliable, c, out w) : driver.BeginSend(c, out w);
        if (r != 0) return false;
        w.WriteByte((byte)type);
        return true;
    }

    void Broadcast(bool isReliable, Msg type, WriteFn write)
    {
        for (int i = 0; i < hostConnections.Count; i++)
        {
            var c = hostConnections[i];
            if (!c.IsCreated) continue;
            if (Open(c, isReliable, type, out var w))
            {
                write?.Invoke(ref w);
                driver.EndSend(w);
            }
        }
    }

    void BroadcastExcept(NetworkConnection exclude, bool isReliable, Msg type, WriteFn write)
    {
        for (int i = 0; i < hostConnections.Count; i++)
        {
            var c = hostConnections[i];
            if (!c.IsCreated || c == exclude) continue;
            if (Open(c, isReliable, type, out var w))
            {
                write?.Invoke(ref w);
                driver.EndSend(w);
            }
        }
    }

    public void SendHello(int kart, string playerName)
    {
        if (IsHost) return;
        if (Open(clientConn, true, Msg.Hello, out var w))
        {
            w.WriteByte((byte)kart);
            WriteString(ref w, playerName);
            driver.EndSend(w);
        }
    }

    public void SendWelcome(NetworkConnection c, int assignedSlot, int course)
    {
        if (Open(c, true, Msg.Welcome, out var w))
        {
            w.WriteByte((byte)assignedSlot);
            w.WriteByte((byte)course);
            driver.EndSend(w);
        }
    }

    public void SendLobbySync(List<(int slot, int kartChar, string name)> players)
    {
        syncedPlayerCount = players.Count;
        Broadcast(true, Msg.LobbySync, (ref DataStreamWriter w) =>
        {
            w.WriteByte((byte)players.Count);
            foreach (var (slot, kartChar, name) in players)
            {
                w.WriteByte((byte)slot);
                w.WriteByte((byte)kartChar);
                WriteString(ref w, name);
            }
        });
    }

    public static void WriteString(ref DataStreamWriter w, string s)
    {
        if (string.IsNullOrEmpty(s)) { w.WriteByte(0); return; }
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
        int len = Mathf.Min(bytes.Length, 32);
        w.WriteByte((byte)len);
        for (int i = 0; i < len; i++) w.WriteByte(bytes[i]);
    }

    public static string ReadString(ref DataStreamReader r)
    {
        if (r.GetBytesRead() >= r.Length) return "";
        int len = r.ReadByte();
        int rem = r.Length - r.GetBytesRead();
        if (len <= 0 || len > rem) return "";
        byte[] bytes = new byte[len];
        for (int i = 0; i < len; i++) bytes[i] = r.ReadByte();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void SendCourse(int course)
    {
        if (!IsHost) return;
        Broadcast(true, Msg.Course, (ref DataStreamWriter w) => w.WriteByte((byte)course));
    }

    public void SendStart(int course)
    {
        if (!IsHost) return;
        Broadcast(true, Msg.Start, (ref DataStreamWriter w) => w.WriteByte((byte)course));
    }

    public void SendBananaGone(int id)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.BananaGone, (ref DataStreamWriter w) => w.WriteInt(id));
        }
        else
        {
            if (!Open(clientConn, true, Msg.BananaGone, out var w)) return;
            w.WriteInt(id);
            driver.EndSend(w);
        }
    }

    public void SendMissile(int owner, int target)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.MissileSpawn, (ref DataStreamWriter w) =>
            {
                w.WriteByte((byte)owner);
                w.WriteByte((byte)(target < 0 ? 255 : target));
            });
        }
        else
        {
            if (!Open(clientConn, true, Msg.MissileSpawn, out var w)) return;
            w.WriteByte((byte)owner);
            w.WriteByte((byte)(target < 0 ? 255 : target));
            driver.EndSend(w);
        }
    }

    public void SendBanana(int id, int owner, Vector3 pos)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.BananaSpawn, (ref DataStreamWriter w) =>
            {
                w.WriteInt(id);
                w.WriteByte((byte)owner);
                w.WriteFloat(pos.x); w.WriteFloat(pos.y); w.WriteFloat(pos.z);
            });
        }
        else
        {
            if (!Open(clientConn, true, Msg.BananaSpawn, out var w)) return;
            w.WriteInt(id);
            w.WriteByte((byte)owner);
            w.WriteFloat(pos.x); w.WriteFloat(pos.y); w.WriteFloat(pos.z);
            driver.EndSend(w);
        }
    }

    public void SendBomb(int id, int owner, Vector3 pos, Vector3 vel)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.BombSpawn, (ref DataStreamWriter w) =>
            {
                w.WriteInt(id);
                w.WriteByte((byte)owner);
                w.WriteFloat(pos.x); w.WriteFloat(pos.y); w.WriteFloat(pos.z);
                w.WriteFloat(vel.x); w.WriteFloat(vel.y); w.WriteFloat(vel.z);
            });
        }
        else
        {
            if (!Open(clientConn, true, Msg.BombSpawn, out var w)) return;
            w.WriteInt(id);
            w.WriteByte((byte)owner);
            w.WriteFloat(pos.x); w.WriteFloat(pos.y); w.WriteFloat(pos.z);
            w.WriteFloat(vel.x); w.WriteFloat(vel.y); w.WriteFloat(vel.z);
            driver.EndSend(w);
        }
    }

    public void SendBombExplode(int id)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.BombExplode, (ref DataStreamWriter w) => w.WriteInt(id));
        }
        else
        {
            if (!Open(clientConn, true, Msg.BombExplode, out var w)) return;
            w.WriteInt(id);
            driver.EndSend(w);
        }
    }

    public void SendThunder(int userSlot)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.ThunderUse, (ref DataStreamWriter w) => w.WriteByte((byte)userSlot));
        }
        else
        {
            if (!Open(clientConn, true, Msg.ThunderUse, out var w)) return;
            w.WriteByte((byte)userSlot);
            driver.EndSend(w);
        }
    }

    // 自分が操作・計算しているカートの状態をまとめて送る（約 20Hz）
    public void SendStates(List<(int id, NetKartState s)> states)
    {
        nextSend = Time.unscaledTime + PingSeconds;
        if (states.Count == 0) return;
        if (IsHost)
        {
            Broadcast(false, Msg.State, (ref DataStreamWriter w) => WriteStates(ref w, states));
        }
        else
        {
            if (Open(clientConn, false, Msg.State, out var w))
            {
                WriteStates(ref w, states);
                driver.EndSend(w);
            }
        }
    }
}
