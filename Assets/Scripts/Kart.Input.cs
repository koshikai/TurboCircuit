using UnityEngine;

// 人間の操作入力（キーボード / ゲームパッド / 2P 分割）
public partial class Kart
{
    // ───────────────────────── 入力 ─────────────────────────

    public KartInput PlayerInput()
    {
        InputSettings.Init();
        var inp = new KartInput();
        if (rm.TwoPlayer) return SplitScreenInput(inp);

        // XInput の LT / RT は個別軸（9th / 10th）で取得する。
        float rt = 0f, lt = 0f;
        try
        {
            rt = Mathf.Clamp01(Input.GetAxis("RightTrigger"));
            lt = Mathf.Clamp01(Input.GetAxis("LeftTrigger"));
        }
        catch { }

        bool up = Input.GetKey(InputSettings.KeyAccel) || Input.GetKey(InputSettings.KeyAccelAlt) 
               || Input.GetKey(KeyCode.JoystickButton0) || rt > 0.15f;
        bool down = Input.GetKey(InputSettings.KeyBrake) || Input.GetKey(InputSettings.KeyBrakeAlt) 
                 || Input.GetKey(KeyCode.JoystickButton1) || lt > 0.15f;

        inp.throttle = up ? Mathf.Max(rt > 0.15f ? rt : 1f, 0f) : down ? -Mathf.Max(lt > 0.15f ? lt : 1f, 0f) : 0f;

        float steerAxis = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(steerAxis) < InputSettings.StickDeadzone) steerAxis = 0f;

        float keySteer = 0f;
        if (Input.GetKey(InputSettings.KeySteerRight) || Input.GetKey(InputSettings.KeySteerRightAlt)) keySteer += 1f;
        if (Input.GetKey(InputSettings.KeySteerLeft) || Input.GetKey(InputSettings.KeySteerLeftAlt)) keySteer -= 1f;

        inp.steer = Mathf.Clamp(Mathf.Abs(keySteer) > 0.01f ? keySteer : steerAxis, -1f, 1f);

        inp.drift = Input.GetKey(InputSettings.KeyDrift) || Input.GetKey(InputSettings.KeyDriftAlt) 
                 || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5);
        inp.driftDown = Input.GetKeyDown(InputSettings.KeyDrift) || Input.GetKeyDown(InputSettings.KeyDriftAlt) 
                     || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.JoystickButton5);

        inp.useItem = Input.GetKeyDown(InputSettings.KeyItem) || Input.GetKeyDown(InputSettings.KeyItemAlt) 
                   || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton3);
        return inp;
    }

    // 2P 対戦時のキー割り当て：P1 = WASD、P2 = 矢印キー（キーボード 1 台で操作できる）
    KartInput SplitScreenInput(KartInput inp)
    {
        if (PlayerIndex == 0)
        {
            inp.throttle = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
            inp.steer = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            inp.drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);
            inp.driftDown = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);
            inp.useItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl);
        }
        else
        {
            inp.throttle = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
            inp.steer = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            inp.drift = Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.Period);
            inp.driftDown = Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.Period);
            inp.useItem = Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Slash);
        }
        return inp;
    }
}

// キーコンフィグ & 入力設定
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

    public static float StickDeadzone = 0.15f;

    static bool initialized = false;

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
        StickDeadzone = 0.15f;
        Save();
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
