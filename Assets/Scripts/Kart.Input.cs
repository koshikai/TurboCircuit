using UnityEngine;

// 人間の操作入力（キーボード / ゲームパッド / 2P 分割）
public partial class Kart
{
    // ───────────────────────── 入力 ─────────────────────────

    KartInput PlayerInput()
    {
        var inp = new KartInput();
        if (rm.TwoPlayer) return SplitScreenInput(inp);

        // XInput の LT / RT は個別軸（9th / 10th）で取得する。
        // 第3軸（LT/RT 合成）は XInput 以外のパッドではスティック等に割り当たっていることがあり誤入力の原因になるため使わない
        float rt = 0f, lt = 0f;
        try
        {
            rt = Mathf.Clamp01(Input.GetAxis("RightTrigger"));
            lt = Mathf.Clamp01(Input.GetAxis("LeftTrigger"));
        }
        catch { }

        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.JoystickButton0) || rt > 0.15f;
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.JoystickButton1) || lt > 0.15f;
        inp.throttle = up ? Mathf.Max(rt > 0.15f ? rt : 1f, 0f) : down ? -Mathf.Max(lt > 0.15f ? lt : 1f, 0f) : 0f;
        inp.steer = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
        inp.drift = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5);
        inp.driftDown = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.JoystickButton5);
        inp.useItem = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton3);
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
