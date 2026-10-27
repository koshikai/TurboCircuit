using UnityEngine;

public enum PadBind
{
    None = 0,
    A = 1,       // Button 0 (South)
    B = 2,       // Button 1 (East)
    X = 3,       // Button 2 (West)
    Y = 4,       // Button 3 (North)
    LB = 5,      // Button 4 (Left Shoulder)
    RB = 6,      // Button 5 (Right Shoulder)
    Back = 7,    // Button 6 (View / Back / Select)
    Start = 8,   // Button 7 (Menu / Start)
    LS = 9,      // Button 8 (Left Stick Click)
    RS = 10,     // Button 9 (Right Stick Click)
    LT = 11,     // Left Trigger
    RT = 12,     // Right Trigger
}

// 人間の操作入力（キーボード / ゲームパッド / 2P 分割）
public partial class Kart
{
    // ───────────────────────── 入力 ─────────────────────────

    public KartInput PlayerInput()
    {
        InputSettings.Init();
        GamepadHaptics.PollPads();
        var inp = new KartInput();
        if (rm.TwoPlayer) return SplitScreenInput(inp);

        float padAccel = InputSettings.GetPadAnalog(InputSettings.PadAccel, InputSettings.PadAccelAlt, 0);
        float padBrake = InputSettings.GetPadAnalog(InputSettings.PadBrake, InputSettings.PadBrakeAlt, 0);

        bool keyUp = GameInput.GetKey(InputSettings.KeyAccel) || GameInput.GetKey(InputSettings.KeyAccelAlt);
        bool keyDown = GameInput.GetKey(InputSettings.KeyBrake) || GameInput.GetKey(InputSettings.KeyBrakeAlt);

        if (keyUp || padAccel > 0.15f)
            inp.throttle = keyUp ? 1f : padAccel;
        else if (keyDown || padBrake > 0.15f)
            inp.throttle = -(keyDown ? 1f : padBrake);
        else
            inp.throttle = 0f;

        float steerAxis = InputSettings.GetPadSteer(0);

        float keySteer = 0f;
        if (GameInput.GetKey(InputSettings.KeySteerRight) || GameInput.GetKey(InputSettings.KeySteerRightAlt)) keySteer += 1f;
        if (GameInput.GetKey(InputSettings.KeySteerLeft) || GameInput.GetKey(InputSettings.KeySteerLeftAlt)) keySteer -= 1f;

        inp.steer = Mathf.Clamp(Mathf.Abs(keySteer) > 0.01f ? keySteer : steerAxis, -1f, 1f);

        inp.drift = GameInput.GetKey(InputSettings.KeyDrift) || GameInput.GetKey(InputSettings.KeyDriftAlt)
                 || GameInput.GetKey(KeyCode.RightShift)
                 || InputSettings.GetPadHeld(InputSettings.PadDrift, InputSettings.PadDriftAlt, 0);
        inp.driftDown = GameInput.GetKeyDown(InputSettings.KeyDrift) || GameInput.GetKeyDown(InputSettings.KeyDriftAlt)
                     || GameInput.GetKeyDown(KeyCode.RightShift)
                     || InputSettings.GetPadDown(InputSettings.PadDrift, InputSettings.PadDriftAlt, 0);

        inp.useItem = GameInput.GetKeyDown(InputSettings.KeyItem) || GameInput.GetKeyDown(InputSettings.KeyItemAlt)
                   || GameInput.GetKeyDown(KeyCode.RightControl)
                   || InputSettings.GetPadDown(InputSettings.PadItem, InputSettings.PadItemAlt, 0);
        return inp;
    }

    // 2P 対戦時のキー割り当て：P1 = カスタムキー/WASD + Pad1、P2 = 矢印キー + Pad2
    KartInput SplitScreenInput(KartInput inp)
    {
        int p = PlayerIndex;
        float padAccel = InputSettings.GetPadAnalog(InputSettings.PadAccel, InputSettings.PadAccelAlt, p);
        float padBrake = InputSettings.GetPadAnalog(InputSettings.PadBrake, InputSettings.PadBrakeAlt, p);
        float padSteer = InputSettings.GetPadSteer(p, includeUnityAxis: false);
        bool padDrift = InputSettings.GetPadHeld(InputSettings.PadDrift, InputSettings.PadDriftAlt, p);
        bool padDriftDown = InputSettings.GetPadDown(InputSettings.PadDrift, InputSettings.PadDriftAlt, p);
        bool padItem = InputSettings.GetPadDown(InputSettings.PadItem, InputSettings.PadItemAlt, p);

        if (p == 0)
        {
            bool kUp = GameInput.GetKey(InputSettings.KeyAccel);
            bool kDown = GameInput.GetKey(InputSettings.KeyBrake);
            inp.throttle = (kUp || padAccel > 0.15f) ? (kUp ? 1f : padAccel) : (kDown || padBrake > 0.15f) ? -(kDown ? 1f : padBrake) : 0f;
            float kSteer = (GameInput.GetKey(InputSettings.KeySteerRight) ? 1f : 0f) - (GameInput.GetKey(InputSettings.KeySteerLeft) ? 1f : 0f);
            inp.steer = Mathf.Abs(kSteer) > 0.01f ? kSteer : padSteer;
            inp.drift = GameInput.GetKey(InputSettings.KeyDrift) || GameInput.GetKey(KeyCode.LeftShift) || padDrift;
            inp.driftDown = GameInput.GetKeyDown(InputSettings.KeyDrift) || GameInput.GetKeyDown(KeyCode.LeftShift) || padDriftDown;
            inp.useItem = GameInput.GetKeyDown(InputSettings.KeyItem) || GameInput.GetKeyDown(KeyCode.LeftControl) || padItem;
        }
        else
        {
            bool kUp = GameInput.GetKey(KeyCode.UpArrow);
            bool kDown = GameInput.GetKey(KeyCode.DownArrow);
            inp.throttle = (kUp || padAccel > 0.15f) ? (kUp ? 1f : padAccel) : (kDown || padBrake > 0.15f) ? -(kDown ? 1f : padBrake) : 0f;
            float kSteer = (GameInput.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (GameInput.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            inp.steer = Mathf.Abs(kSteer) > 0.01f ? kSteer : padSteer;
            inp.drift = GameInput.GetKey(KeyCode.RightShift) || GameInput.GetKey(KeyCode.Period) || padDrift;
            inp.driftDown = GameInput.GetKeyDown(KeyCode.RightShift) || GameInput.GetKeyDown(KeyCode.Period) || padDriftDown;
            inp.useItem = GameInput.GetKeyDown(KeyCode.RightControl) || GameInput.GetKeyDown(KeyCode.Slash) || padItem;
        }
        return inp;
    }
}

// キーコンフィグ & コントローラー入力設定
public static class InputSettings
{
    public static KeyCode KeyAccel = KeyCode.W;
    public static KeyCode KeyBrake = KeyCode.S;
    public static KeyCode KeySteerLeft = KeyCode.A;
    public static KeyCode KeySteerRight = KeyCode.D;
    public static KeyCode KeyDrift = KeyCode.Space;
    public static KeyCode KeyItem = KeyCode.E;
    public static KeyCode KeyRearView = KeyCode.C;

    public static KeyCode KeyAccelAlt = KeyCode.UpArrow;
    public static KeyCode KeyBrakeAlt = KeyCode.DownArrow;
    public static KeyCode KeySteerLeftAlt = KeyCode.LeftArrow;
    public static KeyCode KeySteerRightAlt = KeyCode.RightArrow;
    public static KeyCode KeyDriftAlt = KeyCode.LeftShift;
    public static KeyCode KeyItemAlt = KeyCode.LeftControl;

    // コントローラー（ゲームパッド）割り当て
    public static PadBind PadAccel = PadBind.RT;
    public static PadBind PadAccelAlt = PadBind.A;
    public static PadBind PadBrake = PadBind.LT;
    public static PadBind PadBrakeAlt = PadBind.B;
    public static PadBind PadDrift = PadBind.RB;
    public static PadBind PadDriftAlt = PadBind.LB;
    public static PadBind PadItem = PadBind.X;
    public static PadBind PadItemAlt = PadBind.Y;
    public static PadBind PadRearView = PadBind.RS;
    public static PadBind PadRearViewAlt = PadBind.None;

    public static float StickDeadzone = 0.15f;

    static bool initialized = false;
    static bool prevUnityLT = false;
    static bool prevUnityRT = false;
    static bool curUnityLT = false;
    static bool curUnityRT = false;
    static int lastTrigFrame = -1;

    public static void Init()
    {
        if (initialized) return;
        initialized = true;
        Load();
    }

    public static void Load()
    {
        KeyAccel = (KeyCode)PlayerPrefs.GetInt("tc_key_accel", (int)KeyCode.W);
        KeyBrake = (KeyCode)PlayerPrefs.GetInt("tc_key_brake", (int)KeyCode.S);
        KeySteerLeft = (KeyCode)PlayerPrefs.GetInt("tc_key_left", (int)KeyCode.A);
        KeySteerRight = (KeyCode)PlayerPrefs.GetInt("tc_key_right", (int)KeyCode.D);
        KeyDrift = (KeyCode)PlayerPrefs.GetInt("tc_key_drift", (int)KeyCode.Space);
        KeyItem = (KeyCode)PlayerPrefs.GetInt("tc_key_item", (int)KeyCode.E);
        KeyRearView = (KeyCode)PlayerPrefs.GetInt("tc_key_rear", (int)KeyCode.C);

        PadAccel = (PadBind)PlayerPrefs.GetInt("tc_pad_accel", (int)PadBind.RT);
        PadAccelAlt = (PadBind)PlayerPrefs.GetInt("tc_pad_accel_alt", (int)PadBind.A);
        PadBrake = (PadBind)PlayerPrefs.GetInt("tc_pad_brake", (int)PadBind.LT);
        PadBrakeAlt = (PadBind)PlayerPrefs.GetInt("tc_pad_brake_alt", (int)PadBind.B);
        PadDrift = (PadBind)PlayerPrefs.GetInt("tc_pad_drift", (int)PadBind.RB);
        PadDriftAlt = (PadBind)PlayerPrefs.GetInt("tc_pad_drift_alt", (int)PadBind.LB);
        PadItem = (PadBind)PlayerPrefs.GetInt("tc_pad_item", (int)PadBind.X);
        PadItemAlt = (PadBind)PlayerPrefs.GetInt("tc_pad_item_alt", (int)PadBind.Y);
        PadRearView = (PadBind)PlayerPrefs.GetInt("tc_pad_rear", (int)PadBind.RS);
        PadRearViewAlt = (PadBind)PlayerPrefs.GetInt("tc_pad_rear_alt", (int)PadBind.None);

        StickDeadzone = PlayerPrefs.GetFloat("tc_pad_deadzone", 0.15f);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("tc_key_accel", (int)KeyAccel);
        PlayerPrefs.SetInt("tc_key_brake", (int)KeyBrake);
        PlayerPrefs.SetInt("tc_key_left", (int)KeySteerLeft);
        PlayerPrefs.SetInt("tc_key_right", (int)KeySteerRight);
        PlayerPrefs.SetInt("tc_key_drift", (int)KeyDrift);
        PlayerPrefs.SetInt("tc_key_item", (int)KeyItem);
        PlayerPrefs.SetInt("tc_key_rear", (int)KeyRearView);

        PlayerPrefs.SetInt("tc_pad_accel", (int)PadAccel);
        PlayerPrefs.SetInt("tc_pad_accel_alt", (int)PadAccelAlt);
        PlayerPrefs.SetInt("tc_pad_brake", (int)PadBrake);
        PlayerPrefs.SetInt("tc_pad_brake_alt", (int)PadBrakeAlt);
        PlayerPrefs.SetInt("tc_pad_drift", (int)PadDrift);
        PlayerPrefs.SetInt("tc_pad_drift_alt", (int)PadDriftAlt);
        PlayerPrefs.SetInt("tc_pad_item", (int)PadItem);
        PlayerPrefs.SetInt("tc_pad_item_alt", (int)PadItemAlt);
        PlayerPrefs.SetInt("tc_pad_rear", (int)PadRearView);
        PlayerPrefs.SetInt("tc_pad_rear_alt", (int)PadRearViewAlt);

        PlayerPrefs.SetFloat("tc_pad_deadzone", StickDeadzone);
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        KeyAccel = KeyCode.W;
        KeyBrake = KeyCode.S;
        KeySteerLeft = KeyCode.A;
        KeySteerRight = KeyCode.D;
        KeyDrift = KeyCode.Space;
        KeyItem = KeyCode.E;
        KeyRearView = KeyCode.C;
        KeyAccelAlt = KeyCode.UpArrow;
        KeyBrakeAlt = KeyCode.DownArrow;
        KeySteerLeftAlt = KeyCode.LeftArrow;
        KeySteerRightAlt = KeyCode.RightArrow;
        KeyDriftAlt = KeyCode.LeftShift;
        KeyItemAlt = KeyCode.LeftControl;

        PadAccel = PadBind.RT;
        PadAccelAlt = PadBind.A;
        PadBrake = PadBind.LT;
        PadBrakeAlt = PadBind.B;
        PadDrift = PadBind.RB;
        PadDriftAlt = PadBind.LB;
        PadItem = PadBind.X;
        PadItemAlt = PadBind.Y;
        PadRearView = PadBind.RS;
        PadRearViewAlt = PadBind.None;

        StickDeadzone = 0.15f;
        Save();
    }

    public static string PadBindName(PadBind b) => b switch
    {
        PadBind.A => "A",
        PadBind.B => "B",
        PadBind.X => "X",
        PadBind.Y => "Y",
        PadBind.LB => "LB",
        PadBind.RB => "RB",
        PadBind.Back => "BACK",
        PadBind.Start => "START",
        PadBind.LS => "L-STICK",
        PadBind.RS => "R-STICK",
        PadBind.LT => "LT",
        PadBind.RT => "RT",
        _ => "NONE"
    };

    public static string FormatPadAction(string actionId)
    {
        GetActionPadBinds(actionId, out var pri, out var alt);
        if (pri == PadBind.None) return "[ NONE ]";
        if (alt != PadBind.None && alt != pri)
            return $"[ {PadBindName(pri)} / {PadBindName(alt)} ]";
        return $"[ {PadBindName(pri)} ]";
    }

    public static void GetActionPadBinds(string actionId, out PadBind pri, out PadBind alt)
    {
        switch (actionId)
        {
            case "accel": pri = PadAccel; alt = PadAccelAlt; break;
            case "brake": pri = PadBrake; alt = PadBrakeAlt; break;
            case "drift": pri = PadDrift; alt = PadDriftAlt; break;
            case "item":  pri = PadItem;  alt = PadItemAlt;  break;
            case "rear":  pri = PadRearView; alt = PadRearViewAlt; break;
            default: pri = PadBind.None; alt = PadBind.None; break;
        }
    }

    public static void SetPadBinding(string actionId, PadBind newBind)
    {
        if (newBind == PadBind.None || newBind == PadBind.Start) return;
        GetActionPadBinds(actionId, out var oldPri, out var oldAlt);

        // 他のアクションのサブ割り当て（Alt）に同じボタンがあればクリアし、プライマリが重複していたら入れ替える
        string[] all = { "accel", "brake", "drift", "item", "rear" };
        foreach (var other in all)
        {
            if (other == actionId) continue;
            GetActionPadBinds(other, out var p, out var a);
            if (a == newBind) a = PadBind.None;
            if (p == newBind)
            {
                if (a != PadBind.None) { p = a; a = PadBind.None; }
                else p = oldPri;
            }
            WriteActionPadBinds(other, p, a);
        }
        WriteActionPadBinds(actionId, newBind, newBind == oldPri ? oldAlt : PadBind.None);
        Save();
    }

    static void WriteActionPadBinds(string actionId, PadBind pri, PadBind alt)
    {
        switch (actionId)
        {
            case "accel": PadAccel = pri; PadAccelAlt = alt; break;
            case "brake": PadBrake = pri; PadBrakeAlt = alt; break;
            case "drift": PadDrift = pri; PadDriftAlt = alt; break;
            case "item":  PadItem = pri;  PadItemAlt = alt;  break;
            case "rear":  PadRearView = pri; PadRearViewAlt = alt; break;
        }
    }

    static void UpdateUnityTriggerEdges()
    {
        if (lastTrigFrame == Time.frameCount) return;
        lastTrigFrame = Time.frameCount;
        prevUnityLT = curUnityLT;
        prevUnityRT = curUnityRT;
        float lt = 0f, rt = 0f;
        try
        {
            rt = Mathf.Clamp01(GameInput.GetAxis("RightTrigger"));
            lt = Mathf.Clamp01(GameInput.GetAxis("LeftTrigger"));
        }
        catch { }
        curUnityLT = lt > 0.35f;
        curUnityRT = rt > 0.35f;
    }

    static ushort ToXInputMask(PadBind b) => b switch
    {
        PadBind.A => GamepadHaptics.BtnA,
        PadBind.B => GamepadHaptics.BtnB,
        PadBind.X => GamepadHaptics.BtnX,
        PadBind.Y => GamepadHaptics.BtnY,
        PadBind.LB => GamepadHaptics.BtnLeftShoulder,
        PadBind.RB => GamepadHaptics.BtnRightShoulder,
        PadBind.Back => GamepadHaptics.BtnBack,
        PadBind.Start => GamepadHaptics.BtnStart,
        PadBind.LS => GamepadHaptics.BtnLeftThumb,
        PadBind.RS => GamepadHaptics.BtnRightThumb,
        _ => 0
    };

    static KeyCode ToUnityKeyCode(PadBind b) => b switch
    {
        PadBind.A => KeyCode.JoystickButton0,
        PadBind.B => KeyCode.JoystickButton1,
        PadBind.X => KeyCode.JoystickButton2,
        PadBind.Y => KeyCode.JoystickButton3,
        PadBind.LB => KeyCode.JoystickButton4,
        PadBind.RB => KeyCode.JoystickButton5,
        PadBind.Back => KeyCode.JoystickButton6,
        PadBind.Start => KeyCode.JoystickButton7,
        PadBind.LS => KeyCode.JoystickButton8,
        PadBind.RS => KeyCode.JoystickButton9,
        _ => KeyCode.None
    };

    public static float GetSinglePadAnalog(PadBind b, int playerIndex = 0)
    {
        if (b == PadBind.None) return 0f;
        var snap = GamepadHaptics.GetPad(playerIndex);
        if (snap.connected)
        {
            if (b == PadBind.LT) return snap.current.bLeftTrigger / 255f;
            if (b == PadBind.RT) return snap.current.bRightTrigger / 255f;
            ushort mask = ToXInputMask(b);
            if (mask != 0 && (snap.current.wButtons & mask) != 0) return 1f;
            return 0f;
        }
        if (playerIndex == 0)
        {
            if (b == PadBind.LT)
            {
                try { return Mathf.Clamp01(GameInput.GetAxis("LeftTrigger")); } catch { return 0f; }
            }
            if (b == PadBind.RT)
            {
                try { return Mathf.Clamp01(GameInput.GetAxis("RightTrigger")); } catch { return 0f; }
            }
            var kc = ToUnityKeyCode(b);
            if (kc != KeyCode.None && GameInput.GetKey(kc)) return 1f;
        }
        return 0f;
    }

    public static float GetPadAnalog(PadBind pri, PadBind alt, int playerIndex = 0)
    {
        return Mathf.Max(GetSinglePadAnalog(pri, playerIndex), GetSinglePadAnalog(alt, playerIndex));
    }

    public static bool GetSinglePadHeld(PadBind b, int playerIndex = 0) => GetSinglePadAnalog(b, playerIndex) > 0.25f;

    public static bool GetPadHeld(PadBind pri, PadBind alt, int playerIndex = 0)
    {
        return GetSinglePadHeld(pri, playerIndex) || GetSinglePadHeld(alt, playerIndex);
    }

    public static bool GetSinglePadDown(PadBind b, int playerIndex = 0)
    {
        if (b == PadBind.None) return false;
        UpdateUnityTriggerEdges();
        var snap = GamepadHaptics.GetPad(playerIndex);
        if (snap.connected)
        {
            if (b == PadBind.LT) return snap.current.bLeftTrigger >= 90 && snap.prev.bLeftTrigger < 90;
            if (b == PadBind.RT) return snap.current.bRightTrigger >= 90 && snap.prev.bRightTrigger < 90;
            ushort mask = ToXInputMask(b);
            if (mask != 0 && (snap.current.wButtons & mask) != 0 && (snap.prev.wButtons & mask) == 0) return true;
            return false;
        }
        if (playerIndex == 0)
        {
            if (b == PadBind.LT) return curUnityLT && !prevUnityLT;
            if (b == PadBind.RT) return curUnityRT && !prevUnityRT;
            var kc = ToUnityKeyCode(b);
            if (kc != KeyCode.None && GameInput.GetKeyDown(kc)) return true;
        }
        return false;
    }

    public static bool GetPadDown(PadBind pri, PadBind alt, int playerIndex = 0)
    {
        return GetSinglePadDown(pri, playerIndex) || GetSinglePadDown(alt, playerIndex);
    }

    public static float GetPadSteer(int playerIndex = 0, bool includeUnityAxis = true)
    {
        var snap = GamepadHaptics.GetPad(playerIndex);
        if (snap.connected)
        {
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadLeft) != 0) return -1f;
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadRight) != 0) return 1f;
            float sx = Mathf.Clamp(snap.current.sThumbLX / 32767f, -1f, 1f);
            if (Mathf.Abs(sx) >= StickDeadzone) return sx;
            return 0f;
        }
        if (includeUnityAxis && playerIndex == 0
            && !GameInput.GetKey(KeyCode.LeftArrow) && !GameInput.GetKey(KeyCode.RightArrow)
            && !GameInput.GetKey(KeyCode.A) && !GameInput.GetKey(KeyCode.D))
        {
            float ax = GameInput.GetAxisRaw("Horizontal");
            if (Mathf.Abs(ax) >= StickDeadzone) return Mathf.Clamp(ax, -1f, 1f);
        }
        return 0f;
    }

    // メニュー操作用：D-Pad および左スティックの入力ベクトルを取得
    public static Vector2 GetMenuPadVector()
    {
        GamepadHaptics.PollPads();
        for (int i = 0; i < 2; i++)
        {
            var snap = GamepadHaptics.GetPad(i);
            if (!snap.connected) continue;
            float dx = 0f, dy = 0f;
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadLeft) != 0) dx -= 1f;
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadRight) != 0) dx += 1f;
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadUp) != 0) dy += 1f;
            if ((snap.current.wButtons & GamepadHaptics.BtnDPadDown) != 0) dy -= 1f;
            if (Mathf.Abs(dx) > 0.1f || Mathf.Abs(dy) > 0.1f) return new Vector2(dx, dy);

            float sx = Mathf.Clamp(snap.current.sThumbLX / 32767f, -1f, 1f);
            float sy = Mathf.Clamp(snap.current.sThumbLY / 32767f, -1f, 1f);
            if (Mathf.Abs(sx) > 0.45f || Mathf.Abs(sy) > 0.45f) return new Vector2(sx, sy);
        }
        return Vector2.zero;
    }

    // コントローラーキーコンフィグ待機中に押されたボタン・トリガーを検出
    public static bool TryDetectPadBindDown(out PadBind detected, out bool cancelPressed)
    {
        GamepadHaptics.PollPads();
        UpdateUnityTriggerEdges();
        detected = PadBind.None;
        cancelPressed = GetSinglePadDown(PadBind.Start, 0) || GameInput.GetKeyDown(KeyCode.Escape);
        if (cancelPressed) return false;

        PadBind[] candidates =
        {
            PadBind.A, PadBind.B, PadBind.X, PadBind.Y,
            PadBind.LB, PadBind.RB, PadBind.LT, PadBind.RT,
            PadBind.Back, PadBind.LS, PadBind.RS
        };
        foreach (var c in candidates)
        {
            if (GetSinglePadDown(c, 0) || GetSinglePadDown(c, 1))
            {
                detected = c;
                return true;
            }
        }
        return false;
    }
}

// カートの入力供給インターフェース（責務分離: 単一責任の原則）
public interface IKartInputProvider
{
    KartInput GetInput(Kart kart, float dt);
}

// 人間プレイヤーの入力プロバイダー（キーボード / ゲームパッド）
public class PlayerInputProvider : IKartInputProvider
{
    public KartInput GetInput(Kart kart, float dt) => kart.PlayerInput();
}

// AI (CPU) の自動運転入力プロバイダー（Stanley制御 / MPC予測）
public class AIInputProvider : IKartInputProvider
{
    public KartInput GetInput(Kart kart, float dt) => kart.AIInput(dt);
}

// リプレイやゴーストカー用の再生プロバイダー
public class GhostInputProvider : IKartInputProvider
{
    public KartInput RecordedInput;
    public KartInput GetInput(Kart kart, float dt) => RecordedInput;
}
