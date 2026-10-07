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

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    static extern int XInputSetState14(uint dwUserIndex, ref XInputVibration pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    static extern int XInputSetState91(uint dwUserIndex, ref XInputVibration pVibration);

    static bool isXInputAvailable = true;
    static bool use14 = true;

    struct MotorState
    {
        public float leftMotor;
        public float rightMotor;
        public float duration;
        public float driftLevel;
    }

    static readonly MotorState[] states = new MotorState[4];

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
        if (playerIndex < 0 || playerIndex >= states.Length) return;
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
            states[i] = default;
            var vib = new XInputVibration();
            CallSetState((uint)i, ref vib);
        }
    }

    static void Apply(int playerIndex)
    {
        float left = states[playerIndex].leftMotor;
        float right = Mathf.Max(states[playerIndex].rightMotor, states[playerIndex].driftLevel);

        var vib = new XInputVibration
        {
            wLeftMotorSpeed = (ushort)(Mathf.Clamp01(left) * 65535f),
            wRightMotorSpeed = (ushort)(Mathf.Clamp01(right) * 65535f)
        };
        CallSetState((uint)playerIndex, ref vib);
    }
}
