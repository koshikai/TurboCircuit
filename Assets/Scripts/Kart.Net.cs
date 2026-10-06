using UnityEngine;

// オンライン同期：状態の送信用スナップショットと、リモートカートの追従再現
public partial class Kart
{
    // ───────────────────────── オンライン同期 ─────────────────────────

    NetKartState net;
    bool hasNet;
    float netAge;

    public NetKartState MakeNetState()
    {
        byte f = 0;
        if (drifting) f |= NetKartState.FDrift;
        if (boostTimer > 0) f |= NetKartState.FBoost;
        if (shieldTimer > 0) f |= NetKartState.FShield;
        if (spinTimer > 0) f |= NetKartState.FSpin;
        if (isAirborne) f |= NetKartState.FAir;
        if (isBraking) f |= NetKartState.FBrake;
        if (Finished) f |= NetKartState.FDone;
        return new NetKartState
        {
            Pos = transform.position, Heading = Heading, Speed = Speed, Steer = steerVis, Progress = Progress,
            FinishTime = FinishTime, Flags = f, DriftLevel = DriftLevel, DriftDir = driftDir, Lap = Lap, MaxLap = MaxLap,
        };
    }

    public void ApplyNetState(NetKartState s)
    {
        net = s;
        netAge = 0;
        if (!hasNet) { transform.position = s.Pos; Heading = s.Heading; }
        hasNet = true;
    }

    // 受信した状態に追従し、見た目とエフェクトだけを再現する
    void RemoteTick(float dt)
    {
        if (hasNet)
        {
            netAge += dt;
            Vector3 fwd = Quaternion.Euler(0, net.Heading, 0) * Vector3.forward;
            Vector3 target = net.Pos + fwd * net.Speed * Mathf.Min(netAge, 0.25f);
            float k = 1f - Mathf.Exp(-14f * dt);
            transform.position = (target - transform.position).sqrMagnitude > 64f ? target : Vector3.Lerp(transform.position, target, k);
            Heading = Mathf.LerpAngle(Heading, net.Heading, k);
            Speed = net.Speed;
            VelDir = Forward;

            byte f = net.Flags;
            drifting = (f & NetKartState.FDrift) != 0;
            driftDir = net.DriftDir == 0 ? 1 : net.DriftDir;
            driftCharge = net.DriftLevel == 3 ? 2.7f : net.DriftLevel == 2 ? 1.7f : net.DriftLevel == 1 ? 0.9f : 0f;
            boostTimer = (f & NetKartState.FBoost) != 0 ? 0.3f : 0f;
            shieldTimer = (f & NetKartState.FShield) != 0 ? 3f : 0f;
            spinTimer = (f & NetKartState.FSpin) != 0 ? 0.3f : 0f;
            isAirborne = (f & NetKartState.FAir) != 0;
            isBraking = (f & NetKartState.FBrake) != 0;
            Lap = net.Lap;
            MaxLap = net.MaxLap;
            Finished = (f & NetKartState.FDone) != 0;
            FinishTime = net.FinishTime;
        }
        airTrickSpin = isAirborne ? airTrickSpin + 720f * dt : 0f;
        track.Locate(transform.position, ref Index, out Lateral, out float localProgress);
        Progress = hasNet ? net.Progress : localProgress;
        Offroad = Mathf.Abs(Lateral) > Track.HalfWidth + Track.CurbWidth;

        bool skidding = drifting || (isBraking && Speed > 6f) || (spinTimer > 0 && Speed > 3f);
        if (skidTrails[0] != null)
        {
            skidTrails[0].emitting = skidding;
            skidTrails[1].emitting = skidding;
        }
        Effects(dt);
        UpdateVisual(dt, spinTimer > 0 ? 0 : net.Steer);
    }
}
