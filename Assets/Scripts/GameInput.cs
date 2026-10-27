using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// 入力の窓口。キーボード / マウス / ゲームパッド補助経路はここを経由して読む。
// Input System が有効（ENABLE_INPUT_SYSTEM）なら新方式、無効なら旧 Input クラスを使う。
// 旧方式へ戻すには ProjectSettings の Active Input Handling を「Input Manager (Old)」にする。
// キーコンフィグは KeyCode のまま保存されているため、API も KeyCode で受ける（保存済みの設定互換を保つ）。
// ゲームパッドの主経路は GamepadHaptics の XInput 直読みで、ここを通らない。
public static class GameInput
{
#if ENABLE_INPUT_SYSTEM
    public static bool GetKey(KeyCode code)
    {
        if (TryGetGamepadButton(code, out var button))
        {
            foreach (var pad in Gamepad.all)
                if (pad[button].isPressed) return true;
            return false;
        }
        var kb = Keyboard.current;
        return kb != null && KeyCodeMap.TryGetKey(code, out var key) && kb[key].isPressed;
    }

    public static bool GetKeyDown(KeyCode code)
    {
        if (TryGetGamepadButton(code, out var button))
        {
            foreach (var pad in Gamepad.all)
                if (pad[button].wasPressedThisFrame) return true;
            return false;
        }
        var kb = Keyboard.current;
        return kb != null && KeyCodeMap.TryGetKey(code, out var key) && kb[key].wasPressedThisFrame;
    }

    public static bool GetMouseButtonDown(int button)
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        switch (button)
        {
            case 0: return mouse.leftButton.wasPressedThisFrame;
            case 1: return mouse.rightButton.wasPressedThisFrame;
            case 2: return mouse.middleButton.wasPressedThisFrame;
            default: return false;
        }
    }

    // 旧 Input Manager の軸名（"Horizontal" / "Vertical" / "LeftTrigger" / "RightTrigger"）だけを扱う。未知の軸名は 0。
    // GetAxis の平滑化は再現しない（現在の呼び出し元はトリガーのみで、閾値判定に使っている）。
    public static float GetAxis(string axisName) => ReadAxis(axisName);
    public static float GetAxisRaw(string axisName) => ReadAxis(axisName);

    static float ReadAxis(string axisName)
    {
        switch (axisName)
        {
            case "LeftTrigger": return MaxOverPads(p => p.leftTrigger.ReadValue());
            case "RightTrigger": return MaxOverPads(p => p.rightTrigger.ReadValue());
            case "Horizontal":
                return LargerMagnitude(KeyAxis(Key.RightArrow, Key.D, Key.LeftArrow, Key.A),
                                       MaxMagnitudeOverPads(p => p.leftStick.x.ReadValue()));
            case "Vertical":
                return LargerMagnitude(KeyAxis(Key.UpArrow, Key.W, Key.DownArrow, Key.S),
                                       MaxMagnitudeOverPads(p => p.leftStick.y.ReadValue()));
            default: return 0f;
        }
    }

    static float KeyAxis(Key positiveA, Key positiveB, Key negativeA, Key negativeB)
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;
        float v = 0f;
        if (kb[positiveA].isPressed || kb[positiveB].isPressed) v += 1f;
        if (kb[negativeA].isPressed || kb[negativeB].isPressed) v -= 1f;
        return v;
    }

    static float LargerMagnitude(float a, float b) => Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;

    static float MaxOverPads(System.Func<Gamepad, float> read)
    {
        float best = 0f;
        foreach (var pad in Gamepad.all) best = Mathf.Max(best, read(pad));
        return best;
    }

    static float MaxMagnitudeOverPads(System.Func<Gamepad, float> read)
    {
        float best = 0f;
        foreach (var pad in Gamepad.all)
        {
            float v = read(pad);
            if (Mathf.Abs(v) > Mathf.Abs(best)) best = v;
        }
        return best;
    }

    // 旧 Input の KeyCode.JoystickButton0〜9（Xbox 系の並び）を Gamepad のボタンへ対応づける。
    public static bool TryGetGamepadButton(KeyCode code, out GamepadButton button)
    {
        switch (code)
        {
            case KeyCode.JoystickButton0: button = GamepadButton.South; return true;
            case KeyCode.JoystickButton1: button = GamepadButton.East; return true;
            case KeyCode.JoystickButton2: button = GamepadButton.West; return true;
            case KeyCode.JoystickButton3: button = GamepadButton.North; return true;
            case KeyCode.JoystickButton4: button = GamepadButton.LeftShoulder; return true;
            case KeyCode.JoystickButton5: button = GamepadButton.RightShoulder; return true;
            case KeyCode.JoystickButton6: button = GamepadButton.Select; return true;
            case KeyCode.JoystickButton7: button = GamepadButton.Start; return true;
            case KeyCode.JoystickButton8: button = GamepadButton.LeftStick; return true;
            case KeyCode.JoystickButton9: button = GamepadButton.RightStick; return true;
            default: button = default; return false;
        }
    }
#else
    public static bool GetKey(KeyCode code) => Input.GetKey(code);
    public static bool GetKeyDown(KeyCode code) => Input.GetKeyDown(code);
    public static bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
    public static float GetAxis(string axisName) => Input.GetAxis(axisName);
    public static float GetAxisRaw(string axisName) => Input.GetAxisRaw(axisName);
#endif
}
