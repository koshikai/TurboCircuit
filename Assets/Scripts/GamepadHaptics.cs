using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Windows XInput を直接 P/Invoke で呼び出す超軽量コントローラー振動システム
// パッケージ追加不要で Xbox / XInput 互換コントローラーのデュアルモーター振動を完全サポート
public static class GamepadHaptics
{
    [StructLayout(LayoutKind.Sequential)]
    struct XInputVibration
    {
        public ushort wLeftMotorSpeed;  // 低周波モーター（重い衝撃・クラッシュ）
        public ushort wRightMotorSpeed; // 高周波モーター（繊細な振動・エンジン・ドリフト）
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }

    public const ushort BtnDPadUp        = 0x0001;
    public const ushort BtnDPadDown      = 0x0002;
    public const ushort BtnDPadLeft      = 0x0004;
    public const ushort BtnDPadRight     = 0x0008;
    public const ushort BtnStart         = 0x0010;
    public const ushort BtnBack          = 0x0020;
    public const ushort BtnLeftThumb     = 0x0040;
    public const ushort BtnRightThumb    = 0x0080;
    public const ushort BtnLeftShoulder  = 0x0100;
    public const ushort BtnRightShoulder = 0x0200;
    public const ushort BtnA             = 0x1000;
    public const ushort BtnB             = 0x2000;
    public const ushort BtnX             = 0x4000;
    public const ushort BtnY             = 0x8000;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    static extern int XInputSetState14(uint dwUserIndex, ref XInputVibration pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    static extern int XInputSetState91(uint dwUserIndex, ref XInputVibration pVibration);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern int XInputGetState14(uint dwUserIndex, out XInputState pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    static extern int XInputGetState91(uint dwUserIndex, out XInputState pState);

    static bool isXInputAvailable = true;
    static bool use14 = true;

    struct MotorState
    {
        public float leftMotor;
        public float rightMotor;
        public float duration;
        public float driftLevel;
        public ushort lastLeft;
        public ushort lastRight;
        public bool initialized;
    }

    public struct PadSnapshot
    {
        public bool connected;
        public XInputGamepad current;
        public XInputGamepad prev;
        public float nextProbeTime;
    }

    static readonly MotorState[] states = new MotorState[4];
    static readonly PadSnapshot[] padSnapshots = new PadSnapshot[2];
    static int lastPolledFrame = -1;

    public static void PollPads()
    {
        if (lastPolledFrame == Time.frameCount) return;
        lastPolledFrame = Time.frameCount;
        float now = Time.unscaledTime;

        for (int i = 0; i < padSnapshots.Length; i++)
        {
            padSnapshots[i].prev = padSnapshots[i].current;
            if (!isXInputAvailable)
            {
                padSnapshots[i].connected = false;
                padSnapshots[i].current = default;
                continue;
            }
            if (!padSnapshots[i].connected && now < padSnapshots[i].nextProbeTime)
                continue;

            int res = CallGetState((uint)i, out var st);
            if (res == 0)
            {
                padSnapshots[i].connected = true;
                padSnapshots[i].current = st.Gamepad;
            }
            else
            {
                padSnapshots[i].connected = false;
                padSnapshots[i].current = default;
                padSnapshots[i].nextProbeTime = now + 1.2f;
            }
        }
    }

    public static PadSnapshot GetPad(int playerIndex)
    {
        PollPads();
        if (playerIndex < 0 || playerIndex >= padSnapshots.Length) return default;
        return padSnapshots[playerIndex];
    }

    static int CallGetState(uint userIndex, out XInputState state)
    {
        state = default;
        if (!isXInputAvailable) return -1;
        try
        {
            if (use14) return XInputGetState14(userIndex, out state);
            else return XInputGetState91(userIndex, out state);
        }
        catch (DllNotFoundException)
        {
            if (use14)
            {
                use14 = false;
                try { return XInputGetState91(userIndex, out state); }
                catch { isXInputAvailable = false; return -1; }
            }
            isXInputAvailable = false;
            return -1;
        }
        catch
        {
            return -1;
        }
    }

    static int CallSetState(uint userIndex, ref XInputVibration vib)
    {
        if (!isXInputAvailable) return -1;
        try
        {
            if (use14) return XInputSetState14(userIndex, ref vib);
            else return XInputSetState91(userIndex, ref vib);
        }
        catch (DllNotFoundException)
        {
            if (use14)
            {
                use14 = false;
                try { return XInputSetState91(userIndex, ref vib); }
                catch { isXInputAvailable = false; return -1; }
            }
            isXInputAvailable = false;
            return -1;
        }
        catch (EntryPointNotFoundException)
        {
            isXInputAvailable = false;
            return -1;
        }
        catch
        {
            return -1;
        }
    }

    // 衝撃パルス振動（壁衝突、被弾、ジャンプ着地、ロケットスタートなど）
    public static void Vibrate(int playerIndex, float lowFreq, float highFreq, float duration)
    {
        if (playerIndex < 0 || playerIndex >= states.Length || duration <= 0f) return;
        states[playerIndex].leftMotor = Mathf.Max(states[playerIndex].leftMotor, Mathf.Clamp01(lowFreq));
        states[playerIndex].rightMotor = Mathf.Max(states[playerIndex].rightMotor, Mathf.Clamp01(highFreq));
        states[playerIndex].duration = Mathf.Max(states[playerIndex].duration, duration);
        Apply(playerIndex);
    }

    // ドリフト中の継続微振動（レベル 1=青, 2=橙, 3=紫）
    public static void SetDrift(int playerIndex, int driftLevel)
    {
        if (playerIndex < 0 || playerIndex >= states.Length) return;
        float targetHigh = driftLevel switch
        {
            1 => 0.18f,
            2 => 0.35f,
            3 => 0.60f,
            _ => 0f
        };
        states[playerIndex].driftLevel = targetHigh;
    }

    // 毎フレーム更新（RaceManager から呼ぶ）
    public static void Update(float dt)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].duration > 0f)
            {
                states[i].duration -= dt;
                if (states[i].duration <= 0f)
                {
                    states[i].duration = 0f;
                    states[i].leftMotor = 0f;
                    states[i].rightMotor = 0f;
                }
            }
            Apply(i);
        }
    }

    public static void StopAll()
    {
        for (int i = 0; i < states.Length; i++)
        {
            states[i] = new MotorState { initialized = true, lastLeft = 0, lastRight = 0 };
            var vib = new XInputVibration();
            CallSetState((uint)i, ref vib);
        }
    }

    static void Apply(int playerIndex)
    {
        float left = states[playerIndex].leftMotor;
        float right = Mathf.Max(states[playerIndex].rightMotor, states[playerIndex].driftLevel);

        ushort qLeft = (ushort)(Mathf.Clamp01(left) * 65535f);
        ushort qRight = (ushort)(Mathf.Clamp01(right) * 65535f);
        if (states[playerIndex].initialized && states[playerIndex].lastLeft == qLeft && states[playerIndex].lastRight == qRight)
            return;

        states[playerIndex].initialized = true;
        states[playerIndex].lastLeft = qLeft;
        states[playerIndex].lastRight = qRight;

        var vib = new XInputVibration
        {
            wLeftMotorSpeed = qLeft,
            wRightMotorSpeed = qRight
        };
        CallSetState((uint)playerIndex, ref vib);
    }
}
