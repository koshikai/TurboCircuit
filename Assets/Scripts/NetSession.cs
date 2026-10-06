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
    public const byte FDrift = 1, FBoost = 2, FShield = 4, FSpin = 8, FAir = 16, FBrake = 32, FDone = 64;

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
        LobbySync = 9       // ホスト -> 全員: 参加プレイヤー一覧 [count, {slot, kartChar}...]
    }

    public const ushort DefaultPort = 7777;
    public const int MaxClients = 7; // 最大 7 人のクライアント（ホスト含めて計 8 人）
    const float PingSeconds = 0.05f;

    public Phase State { get; private set; }
    public bool IsHost { get; private set; }
    public string JoinCode { get; private set; } = "";
    public string Message { get; private set; } = "";
    public bool Connected => IsHost ? (hostConnections.Count > 0) : (State == Phase.Connected);
    public bool Busy => State != Phase.Offline;
    public bool SendDue => (Connected || (IsHost && State == Phase.Connected)) && Time.unscaledTime >= nextSend;
    public int ConnectedCount => IsHost ? (hostConnections.Count + 1) : (Connected ? syncedPlayerCount : 1);

    public Action OnConnected, OnDisconnected;
    public Action<NetworkConnection, int> OnClientHello;
    public Action<NetworkConnection> OnClientConnected, OnClientDisconnected;
    public Action<int, int> OnWelcome; // (assignedSlot, course)
    public Action<List<(int slot, int kartChar)>> OnLobbySync;
    public Action<int> OnCourse, OnStart, OnBananaGone;
    public Action<int, NetKartState> OnState;
    public Action<int, int, Vector3> OnBanana;
    public Action<int, int> OnMissile;

    NetworkDriver driver;
    NetworkConnection clientConn;
    readonly List<NetworkConnection> hostConnections = new List<NetworkConnection>();
    NetworkPipeline reliable;
    float nextSend;
    int syncedPlayerCount = 1;

    // ───────────────────────── 接続 ─────────────────────────

    public async void HostRelay()
    {
        if (Busy) return;
        Begin(true, "Contacting Relay...");
        try
        {
            await EnsureServices();
            var alloc = await RelayService.Instance.CreateAllocationAsync(MaxClients);
            var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls") ?? alloc.ServerEndpoints.First();
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.ConnectionData, alloc.Key, ep.Secure);
            string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            if (State != Phase.Starting) return; // 途中でキャンセルされた
            var settings = Settings();
            settings.WithRelayParameters(ref data);
            CreateDriver(settings);
            driver.Bind(NetworkEndpoint.AnyIpv4);
            driver.Listen();
            JoinCode = code;
            Debug.Log("[Net] Room code: " + code);
            State = Phase.Waiting;
            Message = "Waiting for players...";
            OnConnected?.Invoke();
        }
        catch (Exception e) { Fail("Relay error: " + e.Message); }
    }

    public async void JoinRelay(string code)
    {
        if (Busy) return;
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) { Message = "Enter the room code."; return; }
        Begin(false, "Contacting Relay...");
        try
        {
            await EnsureServices();
            Debug.Log("[Net] Joining room " + code);
            var alloc = await RelayService.Instance.JoinAllocationAsync(code);
            var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls") ?? alloc.ServerEndpoints.First();
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.HostConnectionData, alloc.Key, ep.Secure);
            if (State != Phase.Starting) return;
            var settings = Settings();
            settings.WithRelayParameters(ref data);
            CreateDriver(settings);
            clientConn = driver.Connect(data.Endpoint);
            State = Phase.Connecting;
            Message = "Connecting...";
            Debug.Log("[Net] Connecting via Relay");
        }
        catch (Exception e) { Fail("Relay error: " + e.Message); }
    }

    public void HostDirect(ushort port = DefaultPort)
    {
        if (Busy) return;
        Begin(true, "");
        CreateDriver(Settings());
        if (driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0) { Fail("Could not open port " + port); return; }
        driver.Listen();
        JoinCode = "";
        State = Phase.Waiting;
        Message = "Waiting for players on port " + port + "...";
        OnConnected?.Invoke();
    }

    public void JoinDirect(string address)
    {
        if (Busy) return;
        address = (address ?? "").Trim();
        ushort port = DefaultPort;
        int colon = address.LastIndexOf(':');
        if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort p)) { port = p; address = address.Substring(0, colon); }
        if (!NetworkEndpoint.TryParse(address, port, out var endpoint)) { Message = "Invalid address."; return; }
        Begin(false, "Connecting...");
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
        driver = NetworkDriver.Create(settings);
        reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
    }

    void Teardown()
    {
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
                    Debug.Log($"[Net] Accepted a client (#{hostConnections.Count + 1})");
                    hostConnections.Add(accepted);
                    State = Phase.Connected;
                    Message = $"Connected ({hostConnections.Count + 1} players)";
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
                            Message = "Waiting for players...";
                        }
                        else
                        {
                            Message = $"Connected ({hostConnections.Count + 1} players)";
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
                    Message = "Connected to host";
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
                    Message = wasConnected ? "Disconnected from host." : "Could not connect.";
                    if (wasConnected) OnDisconnected?.Invoke();
                    return;
                }
            }
        }
    }

    void Handle(NetworkConnection sourceConn, ref DataStreamReader r)
    {
        var type = (Msg)r.ReadByte();
        switch (type)
        {
            case Msg.Hello:
                int kartChar = r.ReadByte();
                if (IsHost) OnClientHello?.Invoke(sourceConn, kartChar);
                break;
            case Msg.Welcome:
                int mySlot = r.ReadByte();
                int course = r.ReadByte();
                OnWelcome?.Invoke(mySlot, course);
                break;
            case Msg.LobbySync:
                int pCount = r.ReadByte();
                syncedPlayerCount = pCount;
                var pList = new List<(int slot, int kartChar)>(pCount);
                for (int i = 0; i < pCount; i++)
                {
                    int slot = r.ReadByte();
                    int kIdx = r.ReadByte();
                    pList.Add((slot, kIdx));
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
                    int id = r.ReadByte();
                    var s = ReadState(ref r);
                    states.Add((id, s));
                    OnState?.Invoke(id, s);
                }
                if (IsHost && states.Count > 0)
                {
                    // ホスト経由で他の全クライアントへ即座に中継
                    BroadcastExcept(sourceConn, false, Msg.State, w => WriteStates(w, states));
                }
                break;
            case Msg.BananaSpawn:
                int bid = r.ReadInt(), owner = r.ReadByte();
                var bpos = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                OnBanana?.Invoke(bid, owner, bpos);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.BananaSpawn, w =>
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
                    BroadcastExcept(sourceConn, true, Msg.BananaGone, w => w.WriteInt(bgId));
                }
                break;
            case Msg.MissileSpawn:
                int mOwner = r.ReadByte(), mTarget = r.ReadByte();
                OnMissile?.Invoke(mOwner, mTarget);
                if (IsHost)
                {
                    BroadcastExcept(sourceConn, true, Msg.MissileSpawn, w =>
                    {
                        w.WriteByte((byte)mOwner);
                        w.WriteByte((byte)mTarget);
                    });
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

    static void WriteStates(DataStreamWriter w, List<(int id, NetKartState s)> states)
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

    bool Open(NetworkConnection c, bool isReliable, Msg type, out DataStreamWriter w)
    {
        w = default;
        if (!driver.IsCreated || !c.IsCreated) return false;
        int r = isReliable ? driver.BeginSend(reliable, c, out w) : driver.BeginSend(c, out w);
        if (r != 0) return false;
        w.WriteByte((byte)type);
        return true;
    }

    void Broadcast(bool isReliable, Msg type, Action<DataStreamWriter> write)
    {
        for (int i = 0; i < hostConnections.Count; i++)
        {
            var c = hostConnections[i];
            if (!c.IsCreated) continue;
            if (Open(c, isReliable, type, out var w))
            {
                write?.Invoke(w);
                driver.EndSend(w);
            }
        }
    }

    void BroadcastExcept(NetworkConnection exclude, bool isReliable, Msg type, Action<DataStreamWriter> write)
    {
        for (int i = 0; i < hostConnections.Count; i++)
        {
            var c = hostConnections[i];
            if (!c.IsCreated || c == exclude) continue;
            if (Open(c, isReliable, type, out var w))
            {
                write?.Invoke(w);
                driver.EndSend(w);
            }
        }
    }

    public void SendHello(int kart)
    {
        if (IsHost) return;
        if (Open(clientConn, true, Msg.Hello, out var w))
        {
            w.WriteByte((byte)kart);
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

    public void SendLobbySync(List<(int slot, int kartChar)> players)
    {
        syncedPlayerCount = players.Count;
        Broadcast(true, Msg.LobbySync, w =>
        {
            w.WriteByte((byte)players.Count);
            foreach (var (slot, kartChar) in players)
            {
                w.WriteByte((byte)slot);
                w.WriteByte((byte)kartChar);
            }
        });
    }

    public void SendCourse(int course)
    {
        if (!IsHost) return;
        Broadcast(true, Msg.Course, w => w.WriteByte((byte)course));
    }

    public void SendStart(int course)
    {
        if (!IsHost) return;
        Broadcast(true, Msg.Start, w => w.WriteByte((byte)course));
    }

    public void SendBananaGone(int id)
    {
        if (IsHost)
        {
            Broadcast(true, Msg.BananaGone, w => w.WriteInt(id));
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
            Broadcast(true, Msg.MissileSpawn, w =>
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
            Broadcast(true, Msg.BananaSpawn, w =>
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

    // 自分が操作・計算しているカートの状態をまとめて送る（約 20Hz）
    public void SendStates(List<(int id, NetKartState s)> states)
    {
        nextSend = Time.unscaledTime + PingSeconds;
        if (states.Count == 0) return;
        if (IsHost)
        {
            Broadcast(false, Msg.State, w => WriteStates(w, states));
        }
        else
        {
            if (Open(clientConn, false, Msg.State, out var w))
            {
                WriteStates(w, states);
                driver.EndSend(w);
            }
        }
    }
}
