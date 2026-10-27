#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 旧 KeyCode（キーコンフィグの保存形式）から Input System の Key への対応表。
// 対応が無い KeyCode（None、マウス、ジョイスティック等）は TryGetKey が false を返す。
public static class KeyCodeMap
{
    static readonly Dictionary<KeyCode, Key> map = Build();

    public static bool TryGetKey(KeyCode code, out Key key) => map.TryGetValue(code, out key);

    public static int Count => map.Count;

    static Dictionary<KeyCode, Key> Build()
    {
        var d = new Dictionary<KeyCode, Key>();

        for (int i = 0; i < 26; i++)
            d[(KeyCode)((int)KeyCode.A + i)] = (Key)((int)Key.A + i);

        Add(d, KeyCode.Alpha0, Key.Digit0); Add(d, KeyCode.Alpha1, Key.Digit1); Add(d, KeyCode.Alpha2, Key.Digit2);
        Add(d, KeyCode.Alpha3, Key.Digit3); Add(d, KeyCode.Alpha4, Key.Digit4); Add(d, KeyCode.Alpha5, Key.Digit5);
        Add(d, KeyCode.Alpha6, Key.Digit6); Add(d, KeyCode.Alpha7, Key.Digit7); Add(d, KeyCode.Alpha8, Key.Digit8);
        Add(d, KeyCode.Alpha9, Key.Digit9);

        Add(d, KeyCode.Keypad0, Key.Numpad0); Add(d, KeyCode.Keypad1, Key.Numpad1); Add(d, KeyCode.Keypad2, Key.Numpad2);
        Add(d, KeyCode.Keypad3, Key.Numpad3); Add(d, KeyCode.Keypad4, Key.Numpad4); Add(d, KeyCode.Keypad5, Key.Numpad5);
        Add(d, KeyCode.Keypad6, Key.Numpad6); Add(d, KeyCode.Keypad7, Key.Numpad7); Add(d, KeyCode.Keypad8, Key.Numpad8);
        Add(d, KeyCode.Keypad9, Key.Numpad9);
        Add(d, KeyCode.KeypadEnter, Key.NumpadEnter); Add(d, KeyCode.KeypadPlus, Key.NumpadPlus);
        Add(d, KeyCode.KeypadMinus, Key.NumpadMinus); Add(d, KeyCode.KeypadMultiply, Key.NumpadMultiply);
        Add(d, KeyCode.KeypadDivide, Key.NumpadDivide); Add(d, KeyCode.KeypadPeriod, Key.NumpadPeriod);
        Add(d, KeyCode.KeypadEquals, Key.NumpadEquals);

        Add(d, KeyCode.F1, Key.F1); Add(d, KeyCode.F2, Key.F2); Add(d, KeyCode.F3, Key.F3); Add(d, KeyCode.F4, Key.F4);
        Add(d, KeyCode.F5, Key.F5); Add(d, KeyCode.F6, Key.F6); Add(d, KeyCode.F7, Key.F7); Add(d, KeyCode.F8, Key.F8);
        Add(d, KeyCode.F9, Key.F9); Add(d, KeyCode.F10, Key.F10); Add(d, KeyCode.F11, Key.F11); Add(d, KeyCode.F12, Key.F12);

        Add(d, KeyCode.UpArrow, Key.UpArrow); Add(d, KeyCode.DownArrow, Key.DownArrow);
        Add(d, KeyCode.LeftArrow, Key.LeftArrow); Add(d, KeyCode.RightArrow, Key.RightArrow);

        Add(d, KeyCode.Space, Key.Space); Add(d, KeyCode.Return, Key.Enter); Add(d, KeyCode.Tab, Key.Tab);
        Add(d, KeyCode.Backspace, Key.Backspace); Add(d, KeyCode.Escape, Key.Escape); Add(d, KeyCode.Delete, Key.Delete);
        Add(d, KeyCode.Insert, Key.Insert); Add(d, KeyCode.Home, Key.Home); Add(d, KeyCode.End, Key.End);
        Add(d, KeyCode.PageUp, Key.PageUp); Add(d, KeyCode.PageDown, Key.PageDown);

        Add(d, KeyCode.LeftShift, Key.LeftShift); Add(d, KeyCode.RightShift, Key.RightShift);
        Add(d, KeyCode.LeftControl, Key.LeftCtrl); Add(d, KeyCode.RightControl, Key.RightCtrl);
        Add(d, KeyCode.LeftAlt, Key.LeftAlt); Add(d, KeyCode.RightAlt, Key.RightAlt);

        Add(d, KeyCode.Comma, Key.Comma); Add(d, KeyCode.Period, Key.Period); Add(d, KeyCode.Slash, Key.Slash);
        Add(d, KeyCode.Backslash, Key.Backslash); Add(d, KeyCode.Semicolon, Key.Semicolon);
        Add(d, KeyCode.Quote, Key.Quote); Add(d, KeyCode.BackQuote, Key.Backquote);
        Add(d, KeyCode.Minus, Key.Minus); Add(d, KeyCode.Equals, Key.Equals);
        Add(d, KeyCode.LeftBracket, Key.LeftBracket); Add(d, KeyCode.RightBracket, Key.RightBracket);

        Add(d, KeyCode.CapsLock, Key.CapsLock); Add(d, KeyCode.Numlock, Key.NumLock);
        Add(d, KeyCode.ScrollLock, Key.ScrollLock); Add(d, KeyCode.Print, Key.PrintScreen);
        Add(d, KeyCode.Pause, Key.Pause);

        return d;
    }

    static void Add(Dictionary<KeyCode, Key> d, KeyCode code, Key key) => d[code] = key;
}
#endif
