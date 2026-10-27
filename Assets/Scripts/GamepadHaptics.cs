using UnityEngine;
using UnityEngine.InputSystem;

// ゲームパッドの状態取得とデュアルモーター振動（Input System の Gamepad ベース）。
// 呼び出し側（Kart.Input.cs など）は XInput 互換の値で扱っているため、PadSnapshot は従来どおり
// ボタンのビットマスク / トリガー 0〜255 / スティック ±32767 の形式で返す。
// Xbox / XInput 互換パッドに加えて、Input System が Gamepad として認識するパッド（PlayStation / Switch 系など）も使える。
// P1 = Gamepad.all[0]、P2 = Gamepad.all[1]（デバイスの認識順）。
public static class GamepadHaptics
{
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
    }

    static readonly MotorState[] states = new MotorState[4];
    static readonly PadSnapshot[] padSnapshots = new PadSnapshot[2];
    static int lastPolledFrame = -1;

    public static void PollPads()
    {
        if (lastPolledFrame == Time.frameCount) return;
        lastPolledFrame = Time.frameCount;

        for (int i = 0; i < padSnapshots.Length; i++)
        {
            padSnapshots[i].prev = padSnapshots[i].current;
            var pad = GetGamepad(i);
            if (pad == null)
            {
                padSnapshots[i].connected = false;
                padSnapshots[i].current = default;
                continue;
            }
            padSnapshots[i].connected = true;
            padSnapshots[i].current = Read(pad);
        }
    }

    // 同じフレーム内の再取得を許すためのキャッシュ破棄（テストと、デバイス構成が変わった直後に使う）。
    public static void InvalidatePoll()
    {
        lastPolledFrame = -1;
    }

    public static PadSnapshot GetPad(int playerIndex)
    {
        PollPads();
        if (playerIndex < 0 || playerIndex >= padSnapshots.Length) return default;
        return padSnapshots[playerIndex];
    }

    static Gamepad GetGamepad(int playerIndex)
    {
        var all = Gamepad.all;
        return playerIndex >= 0 && playerIndex < all.Count ? all[playerIndex] : null;
    }

    // Gamepad の現在値を XInput 互換の形式へ変換する。
    // スティックとトリガーは Input System のデッドゾーン処理前の生値を使う（デッドゾーンは InputSettings.StickDeadzone 側で適用するため二重にしない）。
    public static XInputGamepad Read(Gamepad pad)
    {
        ushort buttons = 0;
        if (pad.dpad.up.isPressed) buttons |= BtnDPadUp;
        if (pad.dpad.down.isPressed) buttons |= BtnDPadDown;
        if (pad.dpad.left.isPressed) buttons |= BtnDPadLeft;
        if (pad.dpad.right.isPressed) buttons |= BtnDPadRight;
        if (pad.startButton.isPressed) buttons |= BtnStart;
        if (pad.selectButton.isPressed) buttons |= BtnBack;
        if (pad.leftStickButton.isPressed) buttons |= BtnLeftThumb;
        if (pad.rightStickButton.isPressed) buttons |= BtnRightThumb;
        if (pad.leftShoulder.isPressed) buttons |= BtnLeftShoulder;
        if (pad.rightShoulder.isPressed) buttons |= BtnRightShoulder;
        if (pad.buttonSouth.isPressed) buttons |= BtnA;
        if (pad.buttonEast.isPressed) buttons |= BtnB;
        if (pad.buttonWest.isPressed) buttons |= BtnX;
        if (pad.buttonNorth.isPressed) buttons |= BtnY;

        return new XInputGamepad
        {
            wButtons = buttons,
            bLeftTrigger = ToTrigger(pad.leftTrigger.ReadUnprocessedValue()),
            bRightTrigger = ToTrigger(pad.rightTrigger.ReadUnprocessedValue()),
            sThumbLX = ToThumb(pad.leftStick.x.ReadUnprocessedValue()),
            sThumbLY = ToThumb(pad.leftStick.y.ReadUnprocessedValue()),
            sThumbRX = ToThumb(pad.rightStick.x.ReadUnprocessedValue()),
            sThumbRY = ToThumb(pad.rightStick.y.ReadUnprocessedValue()),
        };
    }

    static byte ToTrigger(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

    static short ToThumb(float v) => (short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f);

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
            GetGamepad(i)?.ResetHaptics();
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

        GetGamepad(playerIndex)?.SetMotorSpeeds(qLeft / 65535f, qRight / 65535f);
    }
}
