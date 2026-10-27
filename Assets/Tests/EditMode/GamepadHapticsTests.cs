using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Input System の仮想ゲームパッド（InputTestFixture）で、GamepadHaptics が XInput 互換の値へ正しく変換することを固定する。
// InputTestFixture は実デバイスを切り離したテスト用の入力環境を作るため、物理パッドの有無に影響されない。
public class GamepadHapticsTests : InputTestFixture
{
    Gamepad pad;

    public override void Setup()
    {
        base.Setup();
        GamepadHaptics.InvalidatePoll();
    }

    public override void TearDown()
    {
        GamepadHaptics.StopAll();
        GamepadHaptics.InvalidatePoll();
        base.TearDown();
    }

    static GamepadHaptics.PadSnapshot Poll(int player)
    {
        GamepadHaptics.InvalidatePoll();
        return GamepadHaptics.GetPad(player);
    }

    [Test]
    public void NoGamepad_IsReportedAsDisconnected()
    {
        var snap = Poll(0);
        Assert.IsFalse(snap.connected);
        Assert.AreEqual(0, snap.current.wButtons);
    }

    [Test]
    public void ConnectedGamepad_IsReportedAsConnectedForPlayer1Only()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        Assert.IsTrue(Poll(0).connected);
        Assert.IsFalse(Poll(1).connected);
    }

    [Test]
    public void SecondGamepad_BelongsToPlayer2()
    {
        var pad1 = InputSystem.AddDevice<Gamepad>();
        var pad2 = InputSystem.AddDevice<Gamepad>();
        Press(pad2.buttonEast);

        Assert.AreEqual(0, Poll(0).current.wButtons & GamepadHaptics.BtnB, "P1 must not see P2's button");
        Assert.AreNotEqual(0, Poll(1).current.wButtons & GamepadHaptics.BtnB, "P2 should see its own button");
        Assert.IsNotNull(pad1);
    }

    [Test]
    public void EveryButton_MapsToItsXInputBit()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        var cases = new (string name, Func<Gamepad, ButtonControl> button, ushort bit)[]
        {
            ("A (south)", g => g.buttonSouth, GamepadHaptics.BtnA),
            ("B (east)", g => g.buttonEast, GamepadHaptics.BtnB),
            ("X (west)", g => g.buttonWest, GamepadHaptics.BtnX),
            ("Y (north)", g => g.buttonNorth, GamepadHaptics.BtnY),
            ("LB", g => g.leftShoulder, GamepadHaptics.BtnLeftShoulder),
            ("RB", g => g.rightShoulder, GamepadHaptics.BtnRightShoulder),
            ("Start", g => g.startButton, GamepadHaptics.BtnStart),
            ("Back (select)", g => g.selectButton, GamepadHaptics.BtnBack),
            ("LS click", g => g.leftStickButton, GamepadHaptics.BtnLeftThumb),
            ("RS click", g => g.rightStickButton, GamepadHaptics.BtnRightThumb),
            ("D-Pad up", g => g.dpad.up, GamepadHaptics.BtnDPadUp),
            ("D-Pad down", g => g.dpad.down, GamepadHaptics.BtnDPadDown),
            ("D-Pad left", g => g.dpad.left, GamepadHaptics.BtnDPadLeft),
            ("D-Pad right", g => g.dpad.right, GamepadHaptics.BtnDPadRight),
        };

        foreach (var c in cases)
        {
            Press(c.button(pad));
            Assert.AreEqual(c.bit, Poll(0).current.wButtons, $"{c.name}: only its own bit should be set");
            Release(c.button(pad));
            Assert.AreEqual(0, Poll(0).current.wButtons, $"{c.name}: bit should clear on release");
        }
    }

    [Test]
    public void Triggers_ScaleToZeroTo255()
    {
        pad = InputSystem.AddDevice<Gamepad>();

        Set(pad.leftTrigger, 1f);
        Set(pad.rightTrigger, 0.5f);
        var snap = Poll(0);
        Assert.AreEqual(255, snap.current.bLeftTrigger);
        Assert.AreEqual(128, snap.current.bRightTrigger, 1);

        Set(pad.leftTrigger, 0f);
        Assert.AreEqual(0, Poll(0).current.bLeftTrigger);
    }

    [Test]
    public void Sticks_ScaleToPlusMinus32767()
    {
        pad = InputSystem.AddDevice<Gamepad>();

        Set(pad.leftStick, new Vector2(1f, -1f));
        Set(pad.rightStick, new Vector2(-1f, 1f));
        var cur = Poll(0).current;
        Assert.AreEqual(32767, cur.sThumbLX);
        Assert.AreEqual(-32767, cur.sThumbLY);
        Assert.AreEqual(-32767, cur.sThumbRX);
        Assert.AreEqual(32767, cur.sThumbRY);
    }

    [Test]
    public void SmallStickInput_IsNotSwallowedByTheInputSystemDeadzone()
    {
        // デッドゾーンは InputSettings.StickDeadzone で一度だけ適用する。ここでは生値が届くこと。
        pad = InputSystem.AddDevice<Gamepad>();
        Set(pad.leftStick, new Vector2(0.1f, 0f));
        Assert.AreEqual(3277, Poll(0).current.sThumbLX, 2);
    }

    [Test]
    public void Snapshot_KeepsThePreviousFrameForEdgeDetection()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        Poll(0);

        Press(pad.buttonSouth);
        var snap = Poll(0);
        Assert.AreNotEqual(0, snap.current.wButtons & GamepadHaptics.BtnA);
        Assert.AreEqual(0, snap.prev.wButtons & GamepadHaptics.BtnA);

        snap = Poll(0);
        Assert.AreNotEqual(0, snap.prev.wButtons & GamepadHaptics.BtnA, "held button should be in prev on the next poll");
    }

    [Test]
    public void PadBinds_ReadThroughTheSnapshot()
    {
        pad = InputSystem.AddDevice<Gamepad>();

        Set(pad.rightTrigger, 1f);
        Press(pad.buttonSouth);
        Poll(0);
        Assert.AreEqual(1f, InputSettings.GetSinglePadAnalog(PadBind.RT, 0), 0.01f);
        Assert.AreEqual(1f, InputSettings.GetSinglePadAnalog(PadBind.A, 0));
        Assert.AreEqual(0f, InputSettings.GetSinglePadAnalog(PadBind.B, 0));
        Assert.IsTrue(InputSettings.GetSinglePadHeld(PadBind.A, 0));
        Assert.IsFalse(InputSettings.GetSinglePadHeld(PadBind.None, 0));
    }

    [Test]
    public void PadDown_FiresOnlyOnTheFrameTheButtonIsPressed()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        Poll(0);

        Press(pad.buttonWest); // PadBind.X = West
        GamepadHaptics.InvalidatePoll();
        Assert.IsTrue(InputSettings.GetSinglePadDown(PadBind.X, 0), "first frame of the press");

        GamepadHaptics.InvalidatePoll();
        Assert.IsFalse(InputSettings.GetSinglePadDown(PadBind.X, 0), "held, not a new press");
    }

    [Test]
    public void TriggerDown_FiresWhenCrossingTheThreshold()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        Poll(0);

        Set(pad.leftTrigger, 1f);
        GamepadHaptics.InvalidatePoll();
        Assert.IsTrue(InputSettings.GetSinglePadDown(PadBind.LT, 0));

        GamepadHaptics.InvalidatePoll();
        Assert.IsFalse(InputSettings.GetSinglePadDown(PadBind.LT, 0));
    }

    [Test]
    public void Steer_UsesTheLeftStickOrDPad()
    {
        pad = InputSystem.AddDevice<Gamepad>();

        Set(pad.leftStick, new Vector2(0.5f, 0f));
        Poll(0);
        Assert.AreEqual(0.5f, InputSettings.GetPadSteer(0, includeUnityAxis: false), 0.01f);

        Set(pad.leftStick, Vector2.zero);
        Press(pad.dpad.left);
        Poll(0);
        Assert.AreEqual(-1f, InputSettings.GetPadSteer(0, includeUnityAxis: false), 0.01f);
    }

    [Test]
    public void Steer_AppliesOurDeadzoneToSmallInput()
    {
        pad = InputSystem.AddDevice<Gamepad>();
        Set(pad.leftStick, new Vector2(0.05f, 0f)); // StickDeadzone（既定 0.15）より小さい
        Poll(0);
        Assert.AreEqual(0f, InputSettings.GetPadSteer(0, includeUnityAxis: false));
    }

    [Test]
    public void Vibration_WithAndWithoutAGamepad_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            GamepadHaptics.Vibrate(0, 0.5f, 0.5f, 0.1f);
            GamepadHaptics.SetDrift(0, 2);
            GamepadHaptics.Update(0.2f);
            GamepadHaptics.StopAll();
        });

        pad = InputSystem.AddDevice<Gamepad>();
        Assert.DoesNotThrow(() =>
        {
            GamepadHaptics.Vibrate(0, 0.5f, 0.5f, 0.1f);
            GamepadHaptics.SetDrift(0, 3);
            GamepadHaptics.Update(0.2f);
            GamepadHaptics.StopAll();
        });
    }
}
