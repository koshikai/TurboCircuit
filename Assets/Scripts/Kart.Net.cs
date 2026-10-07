using System.Collections.Generic;
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

    struct TimedNetSnapshot
    {
        public float time;
        public NetKartState state;
    }

    readonly List<TimedNetSnapshot> snapBuffer = new List<TimedNetSnapshot>();
    const float InterpDelay = 0.085f; // 85ms 遅延補間バッファ（ジッターとパケット揺らぎを完全吸収）

    public void ApplyNetState(NetKartState s)
    {
        net = s;
        if (!hasNet)
        {
            transform.position = s.Pos;
            Heading = s.Heading;
            snapBuffer.Clear();
        }
        hasNet = true;

        float now = Time.unscaledTime;
        snapBuffer.Add(new TimedNetSnapshot { time = now, state = s });
        // 直近約 0.6 秒分（最大16スナップショット）だけ保持
        while (snapBuffer.Count > 16 || (snapBuffer.Count > 2 && now - snapBuffer[0].time > 0.6f))
        {
            snapBuffer.RemoveAt(0);
        }
    }

    // 受信したスナップショットバッファを時間軸補間し、極めて滑らかなゴースト走行を再現
    void RemoteTick(float dt)
    {
        if (hasNet)
        {
            float renderTime = Time.unscaledTime - InterpDelay;
            Vector3 targetPos = net.Pos;
            float targetHeading = net.Heading;
            float targetSpeed = net.Speed;

            // バッファから renderTime を挟む 2 フレームを探して補間
            bool interpolated = false;
            if (snapBuffer.Count >= 2)
            {
                for (int i = 0; i < snapBuffer.Count - 1; i++)
                {
                    if (snapBuffer[i].time <= renderTime && renderTime <= snapBuffer[i + 1].time)
                    {
                        float span = snapBuffer[i + 1].time - snapBuffer[i].time;
                        float t = span > 0.0001f ? (renderTime - snapBuffer[i].time) / span : 0f;
                        targetPos = Vector3.Lerp(snapBuffer[i].state.Pos, snapBuffer[i + 1].state.Pos, t);
                        targetHeading = Mathf.LerpAngle(snapBuffer[i].state.Heading, snapBuffer[i + 1].state.Heading, t);
                        targetSpeed = Mathf.Lerp(snapBuffer[i].state.Speed, snapBuffer[i + 1].state.Speed, t);
                        interpolated = true;
                        break;
                    }
                }
            }

            if (!interpolated)
            {
                // バッファを超えた場合は最新フレームからの穏やかな外挿（デッドレコニング）
                float extrapolateAge = Mathf.Min(Time.unscaledTime - (snapBuffer.Count > 0 ? snapBuffer[^1].time : Time.unscaledTime), 0.25f);
                Vector3 fwd = Quaternion.Euler(0, net.Heading, 0) * Vector3.forward;
                targetPos = net.Pos + fwd * net.Speed * extrapolateAge;
                targetHeading = net.Heading;
                targetSpeed = net.Speed;
            }

            float k = 1f - Mathf.Exp(-18f * dt);
            transform.position = (targetPos - transform.position).sqrMagnitude > 64f ? targetPos : Vector3.Lerp(transform.position, targetPos, k);
            Heading = Mathf.LerpAngle(Heading, targetHeading, k);
            Speed = targetSpeed;
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
