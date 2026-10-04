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

// 2 人対戦用のネットワーク層。Unity Transport の上に最小限のメッセージを載せる。
// 接続方法は Unity Relay（ルームコード）と、IP 直接接続（LAN / VPN / ポート開放済み）の 2 種類。
// ホスト = P1（レースの進行と CPU カートを担当）、クライアント = P2。
public class NetSession : MonoBehaviour
{
    public enum Phase { Offline, Starting, Waiting, Connecting, Connected }

    enum Msg : byte { Hello = 1, Course, Start, State, BananaSpawn, BananaGone, MissileSpawn }

    public const ushort DefaultPort = 7777;
    const float PingSeconds = 0.05f;

    public Phase State { get; private set; }
    public bool IsHost { get; private set; }
    public string JoinCode { get; private set; } = "";
    public string Message { get; private set; } = "";
    public bool Connected => State == Phase.Connected;
    public bool Busy => State != Phase.Offline;
    public bool SendDue => Connected && Time.unscaledTime >= nextSend;

    public Action OnConnected, OnDisconnected;
    public Action<int> OnRivalKart, OnCourse, OnStart, OnBananaGone;
    public Action<int, NetKartState> OnState;
    public Action<int, int, Vector3> OnBanana;
    public Action<int, int> OnMissile;

    NetworkDriver driver;
    NetworkConnection conn;
    NetworkPipeline reliable;
    float nextSend;

    // ───────────────────────── 接続 ─────────────────────────

    public async void HostRelay()
    {
        if (Busy) return;
        Begin(true, "Contacting Relay...");
        try
        {
            await EnsureServices();
            var alloc = await RelayService.Instance.CreateAllocationAsync(1);
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
            State = Phase.Waiting;
            Message = "Waiting for a player...";
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
            var alloc = await RelayService.Instance.JoinAllocationAsync(code);
            var ep = alloc.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls") ?? alloc.ServerEndpoints.First();
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.HostConnectionData, alloc.Key, ep.Secure);
            if (State != Phase.Starting) return;
            var settings = Settings();
            settings.WithRelayParameters(ref data);
            CreateDriver(settings);
            conn = driver.Connect(data.Endpoint);
            State = Phase.Connecting;
            Message = "Connecting...";
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
        Message = "Waiting for a player on port " + port + "...";
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
        conn = driver.Connect(endpoint);
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
            if (conn.IsCreated) driver.Disconnect(conn);
            driver.ScheduleUpdate().Complete();
            driver.Dispose();
        }
        conn = default;
        State = Phase.Offline;
        JoinCode = "";
    }

    void OnDestroy()
    {
        if (driver.IsCreated) driver.Dispose();
    }

    // ───────────────────────── 受信 ─────────────────────────

    void Update()
    {
        if (!driver.IsCreated) return;
        driver.ScheduleUpdate().Complete();

        if (IsHost && State == Phase.Waiting)
        {
            var accepted = driver.Accept();
            if (accepted.IsCreated)
            {
                conn = accepted;
                State = Phase.Connected;
                Message = "Connected";
                OnConnected?.Invoke();
            }
        }
        if (!conn.IsCreated) return;

        NetworkEvent.Type cmd;
        while (driver.IsCreated && conn.IsCreated && (cmd = driver.PopEventForConnection(conn, out var stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                State = Phase.Connected;
                Message = "Connected";
                OnConnected?.Invoke();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                Handle(ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                bool wasConnected = Connected;
                Teardown();
                Message = wasConnected ? "Opponent disconnected." : "Could not connect.";
                if (wasConnected) OnDisconnected?.Invoke();
                return;
            }
        }
    }

    void Handle(ref DataStreamReader r)
    {
        var type = (Msg)r.ReadByte();
        switch (type)
        {
            case Msg.Hello: OnRivalKart?.Invoke(r.ReadByte()); break;
            case Msg.Course: OnCourse?.Invoke(r.ReadByte()); break;
            case Msg.Start: OnStart?.Invoke(r.ReadByte()); break;
            case Msg.State:
                int n = r.ReadByte();
                for (int i = 0; i < n; i++)
                {
                    int id = r.ReadByte();
                    var s = new NetKartState
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
                    OnState?.Invoke(id, s);
                }
                break;
            case Msg.BananaSpawn:
                int bid = r.ReadInt(), owner = r.ReadByte();
                OnBanana?.Invoke(bid, owner, new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat()));
                break;
            case Msg.BananaGone: OnBananaGone?.Invoke(r.ReadInt()); break;
            case Msg.MissileSpawn: OnMissile?.Invoke(r.ReadByte(), r.ReadByte()); break;
        }
    }

    // ───────────────────────── 送信 ─────────────────────────

    bool Open(bool isReliable, Msg type, out DataStreamWriter w)
    {
        w = default;
        if (!Connected || !driver.IsCreated) return false;
        int r = isReliable ? driver.BeginSend(reliable, conn, out w) : driver.BeginSend(conn, out w);
        if (r != 0) return false;
        w.WriteByte((byte)type);
        return true;
    }

    public void SendHello(int kart) { if (Open(true, Msg.Hello, out var w)) { w.WriteByte((byte)kart); driver.EndSend(w); } }
    public void SendCourse(int course) { if (Open(true, Msg.Course, out var w)) { w.WriteByte((byte)course); driver.EndSend(w); } }
    public void SendStart(int course) { if (Open(true, Msg.Start, out var w)) { w.WriteByte((byte)course); driver.EndSend(w); } }
    public void SendBananaGone(int id) { if (Open(true, Msg.BananaGone, out var w)) { w.WriteInt(id); driver.EndSend(w); } }
    public void SendMissile(int owner, int target) { if (Open(true, Msg.MissileSpawn, out var w)) { w.WriteByte((byte)owner); w.WriteByte((byte)(target < 0 ? 255 : target)); driver.EndSend(w); } }

    public void SendBanana(int id, int owner, Vector3 pos)
    {
        if (!Open(true, Msg.BananaSpawn, out var w)) return;
        w.WriteInt(id);
        w.WriteByte((byte)owner);
        w.WriteFloat(pos.x); w.WriteFloat(pos.y); w.WriteFloat(pos.z);
        driver.EndSend(w);
    }

    // 自分が操作・計算しているカートの状態をまとめて送る（約 20Hz）
    public void SendStates(List<(int id, NetKartState s)> states)
    {
        nextSend = Time.unscaledTime + PingSeconds;
        if (states.Count == 0 || !Open(false, Msg.State, out var w)) return;
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
        driver.EndSend(w);
    }
}
