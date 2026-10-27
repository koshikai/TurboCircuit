using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// キーコンフィグ（KeyCode で保存）から Input System の Key への対応表を固定する。
public class KeyCodeMapTests
{
    // ゲーム内で既定値・ハードコードとして使っている KeyCode（Assets/Scripts の KeyCode.* 参照を棚卸ししたもの）。
    static readonly KeyCode[] KeysUsedByTheGame =
    {
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.E, KeyCode.C,
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.Period, KeyCode.Slash, KeyCode.Comma, KeyCode.Backslash,
        KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape, KeyCode.Tab, KeyCode.Backspace,
        KeyCode.Q, KeyCode.T, KeyCode.O, KeyCode.U, KeyCode.R, KeyCode.P, KeyCode.M, KeyCode.L, KeyCode.B,
        KeyCode.F12, KeyCode.End,
    };

    [TestCaseSource(nameof(KeysUsedByTheGame))]
    public void KeysUsedByTheGame_AreMapped(KeyCode code)
    {
        Assert.IsTrue(KeyCodeMap.TryGetKey(code, out _), $"{code} has no Input System Key");
    }

    [Test]
    public void Letters_MapToTheSameNamedKey()
    {
        for (char c = 'A'; c <= 'Z'; c++)
        {
            var code = (KeyCode)System.Enum.Parse(typeof(KeyCode), c.ToString());
            Assert.IsTrue(KeyCodeMap.TryGetKey(code, out var key), $"{code} not mapped");
            Assert.AreEqual(c.ToString(), key.ToString());
        }
    }

    [Test]
    public void Digits_MapToDigitKeys()
    {
        for (int i = 0; i <= 9; i++)
        {
            var code = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Alpha" + i);
            Assert.IsTrue(KeyCodeMap.TryGetKey(code, out var key), $"{code} not mapped");
            Assert.AreEqual("Digit" + i, key.ToString());
        }
    }

    [Test]
    public void KeypadDigits_MapToNumpadKeys()
    {
        for (int i = 0; i <= 9; i++)
        {
            var code = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Keypad" + i);
            Assert.IsTrue(KeyCodeMap.TryGetKey(code, out var key), $"{code} not mapped");
            Assert.AreEqual("Numpad" + i, key.ToString());
        }
    }

    [Test]
    public void FunctionKeys_MapToTheSameNamedKey()
    {
        for (int i = 1; i <= 12; i++)
        {
            var code = (KeyCode)System.Enum.Parse(typeof(KeyCode), "F" + i);
            Assert.IsTrue(KeyCodeMap.TryGetKey(code, out var key), $"{code} not mapped");
            Assert.AreEqual("F" + i, key.ToString());
        }
    }

    [Test]
    public void SpecialKeys_MapToExpectedKeys()
    {
        AssertMaps(KeyCode.Return, Key.Enter);
        AssertMaps(KeyCode.KeypadEnter, Key.NumpadEnter);
        AssertMaps(KeyCode.LeftControl, Key.LeftCtrl);
        AssertMaps(KeyCode.RightControl, Key.RightCtrl);
        AssertMaps(KeyCode.BackQuote, Key.Backquote);
        AssertMaps(KeyCode.Print, Key.PrintScreen);
        AssertMaps(KeyCode.Numlock, Key.NumLock);
    }

    [Test]
    public void KeysWithoutAKeyboardEquivalent_AreNotMapped()
    {
        Assert.IsFalse(KeyCodeMap.TryGetKey(KeyCode.None, out _));
        Assert.IsFalse(KeyCodeMap.TryGetKey(KeyCode.Mouse0, out _));
        Assert.IsFalse(KeyCodeMap.TryGetKey(KeyCode.JoystickButton0, out _));
    }

    [Test]
    public void NoTwoKeyCodesShareTheSameKey()
    {
        var seen = new Dictionary<Key, KeyCode>();
        var visited = new HashSet<KeyCode>();
        foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!visited.Add(code)) continue; // KeyCode には同値の別名がある
            if (!KeyCodeMap.TryGetKey(code, out var key)) continue;
            Assert.IsFalse(seen.TryGetValue(key, out var other), $"{code} and {other} both map to {key}");
            seen[key] = code;
        }
        Assert.AreEqual(KeyCodeMap.Count, seen.Count);
    }

    [TestCase(KeyCode.JoystickButton0, GamepadButton.South)]
    [TestCase(KeyCode.JoystickButton1, GamepadButton.East)]
    [TestCase(KeyCode.JoystickButton2, GamepadButton.West)]
    [TestCase(KeyCode.JoystickButton3, GamepadButton.North)]
    [TestCase(KeyCode.JoystickButton4, GamepadButton.LeftShoulder)]
    [TestCase(KeyCode.JoystickButton5, GamepadButton.RightShoulder)]
    [TestCase(KeyCode.JoystickButton6, GamepadButton.Select)]
    [TestCase(KeyCode.JoystickButton7, GamepadButton.Start)]
    [TestCase(KeyCode.JoystickButton8, GamepadButton.LeftStick)]
    [TestCase(KeyCode.JoystickButton9, GamepadButton.RightStick)]
    public void JoystickButtons_MapToXboxStyleGamepadButtons(KeyCode code, GamepadButton expected)
    {
        Assert.IsTrue(GameInput.TryGetGamepadButton(code, out var button));
        Assert.AreEqual(expected, button);
    }

    [Test]
    public void JoystickButton10_IsNotMapped()
    {
        Assert.IsFalse(GameInput.TryGetGamepadButton(KeyCode.JoystickButton10, out _));
    }

    [Test]
    public void GameInput_WithoutDevices_ReturnsNeutralValues()
    {
        // バッチ実行では物理キーボード等が無い場合がある。null 安全で例外を出さず、押されていない扱いになること。
        Assert.DoesNotThrow(() =>
        {
            GameInput.GetKey(KeyCode.W);
            GameInput.GetKeyDown(KeyCode.Space);
            GameInput.GetMouseButtonDown(0);
            GameInput.GetAxisRaw("Horizontal");
            GameInput.GetAxis("Unknown");
        });
        Assert.AreEqual(0f, GameInput.GetAxis("Unknown"));
    }

    static void AssertMaps(KeyCode code, Key expected)
    {
        Assert.IsTrue(KeyCodeMap.TryGetKey(code, out var key), $"{code} not mapped");
        Assert.AreEqual(expected, key);
    }
}
