using System;
using System.Collections.Generic;
using AceOption.Helpers;
using Rewired;

internal static class YawInputTests
{
    private static int checks;

    private static void Near(float expected, float actual, string message)
    {
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > .0001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
        checks++;
    }

    private static float Step(ButtonYaw yaw, float raw, float dt, float rise = .25f, float release = .1f) =>
        yaw.Update(raw, raw, raw != 0, false, dt, rise, release);

    public static void Main()
    {
        var yaw = new ButtonYaw();
        Near(.08f, Step(yaw, 1, .02f), "First tick responds gently without an initial dead period");
        Near(.24f, Step(yaw, 1, .04f), "A 60 ms tap reaches only 24 percent rudder");
        Near(.04f, Step(yaw, 0, .02f), "Short tap returns proportionally faster");
        Near(0, Step(yaw, 0, .02f), "Tap leaves no lingering rudder");
        Near(1, Step(yaw, 1, .25f), "Hold reaches full authority in the configured time");
        Near(1, Step(yaw, 1, 10), "Long hold cannot overshoot");
        Near(.5f, Step(yaw, 0, .05f), "Full rudder is halfway centered after 50 ms");
        Near(0, Step(yaw, 0, .05f), "Full rudder centers after 100 ms");
        Near(-1, Step(yaw, -1, .25f), "Left has the same full authority");
        Near(-.5f, Step(yaw, 1, .05f), "Reversal first unloads the old direction");
        Near(.2f, Step(yaw, 1, .1f), "Reversal spends remaining time in the new direction");
        Near(1, Step(yaw, 1, .2f), "Reversal reaches full opposite authority");
        Near(0, Step(yaw, 0, 1), "Opposing buttons cancelling to zero center cleanly");
        Near(.04f, Step(yaw, 1, .02f, .5f), "Live rise setting changes the press ramp");
        Near(0, Step(yaw, 0, .02f, .5f, .02f), "Live return setting changes the release ramp");
        Near(0, Step(yaw, 1, 0), "No advancement without simulation time");
        Near(0, Step(yaw, 1, -.1f), "Negative time does not reverse the ramp");
        Near(1, Step(yaw, 1, 1, .25f), "Large tick still respects the axis limit");
        yaw.Reset();
        Near(-.08f, Step(yaw, -1, .02f), "Reset forgets the previous direction");

        // Equal elapsed simulation time must give equal output across physics rates,
        // including a reversal that crosses center partway through a tick.
        foreach (int rate in new[] { 30, 50, 60, 120, 240 })
        {
            yaw.Reset();
            float dt = 1f / rate;
            for (int tick = 0; tick < rate / 5; tick++) Step(yaw, 1, dt);
            Near(.8f, Step(yaw, 1, 0), "200 ms press at " + rate + " Hz");
            for (int tick = 0; tick < rate / 10; tick++) Step(yaw, -1, dt);
            Near(-.08f, Step(yaw, -1, 0), "100 ms reversal at " + rate + " Hz");
        }

        VerifyInputRouting();
        Console.WriteLine("PASS: " + checks + " yaw timing/source/reset assertions");
    }

    private static void VerifyInputRouting()
    {
        var player = new Player();
        var button = new InputActionSourceData(ControllerElementType.Button);
        var axis = new InputActionSourceData(ControllerElementType.Axis);
        Func<float> read = () => YawCommands.Read(player, "Yaw");
        player.Sources.Add(button);
        player.Raw = 1;
        player.Native = .01f;
        Near(.08f, read(), "Physical button uses the mod ramp without double smoothing");
        player.Sources.Clear(); player.Raw = 0; player.Native = .9f;
        Near(0, read(), "Release follows raw zero instead of native smoothing tail");
        Near(0, read(), "Native button tail never reappears after centering");

        player.Sources.Add(axis); player.Raw = 1; player.Native = .7f;
        Near(.7f, read(), "Analog full deflection is not mistaken for a button");
        player.Raw = .3f; player.Native = .3f;
        Near(.3f, read(), "Analog proportional input passes through");
        player.Sources.Add(button); player.Raw = 1; player.Native = 1;
        Near(1, read(), "Mixed analog/button sources preserve native arbitration");
        player.Sources.Remove(axis);
        Near(.08f, read(), "Switching back to buttons starts a fresh ramp");

        player.Sources.Clear(); player.Raw = .4f; player.Native = .35f;
        Near(.35f, read(), "Unknown nonzero source keeps native input");
        player.Sources = null; player.Raw = 0; player.Native = .2f;
        Near(.2f, read(), "Missing source list is safe and keeps unsmoothed native input");
        player.Sources = new List<InputActionSourceData> { button };
        player.Raw = 1; player.Native = 1;
        Near(.08f, read(), "Returning from unknown source starts cleanly");

        AceOption.Plugin.SmoothButtonYaw.Value = false;
        Near(1, read(), "Disabling restores native yaw immediately");
        AceOption.Plugin.SmoothButtonYaw.Value = true;
        Near(.08f, read(), "Re-enabling does not reuse old rudder");

        CameraCommands.InputAllowed = false;
        Near(0, read(), "Open menus suppress yaw input and clear the ramp");
        CameraCommands.InputAllowed = true;
        Near(.08f, read(), "Resuming while holding starts gently");
        YawCommands.Reset();
        Near(.08f, read(), "Pause/lifecycle reset forgets a held direction");
        YawCommands.Reset(player);
        Near(.08f, read(), "Blackout/detachment reset forgets a held direction");

        var second = new Player { Raw = -1, Native = -1 };
        second.Sources.Add(button);
        Near(-.08f, YawCommands.Read(second, "Yaw"), "Different players have independent rudder state");
        Near(.16f, read(), "Other player's input does not disturb the first player");
        AceOption.Plugin.YawRiseTime.Value = .5f;
        Near(.2f, read(), "Live configuration reaches the active input filter");
        YawCommands.Reset();
        AceOption.Plugin.YawRiseTime.Value = .25f;
    }
}

namespace AceOption
{
    internal sealed class Setting<T> { internal T Value; internal Setting(T value) { Value = value; } }
    internal static class Plugin
    {
        internal static Setting<bool> SmoothButtonYaw = new Setting<bool>(true);
        internal static Setting<float> YawRiseTime = new Setting<float>(.25f), YawReturnTime = new Setting<float>(.1f);
    }
}
namespace AceOption.Helpers
{
    internal static class CameraCommands { internal static bool InputAllowed = true; }
}
namespace UnityEngine
{
    internal static class Time { internal static float fixedDeltaTime = .02f; }
}
namespace Rewired
{
    public enum ControllerElementType { Axis, Button }
    public sealed class ActionElementMap { public ControllerElementType elementType; }
    public sealed class InputActionSourceData
    {
        public ActionElementMap actionElementMap;
        public InputActionSourceData(ControllerElementType type) { actionElementMap = new ActionElementMap { elementType = type }; }
    }
    public sealed class Player
    {
        public float Raw, Native;
        public List<InputActionSourceData> Sources = new List<InputActionSourceData>();
        public float GetAxis(string action) => Native;
        public float GetAxisRaw(string action) => Raw;
        public IList<InputActionSourceData> GetCurrentInputSources(string action) => Sources;
    }
}
