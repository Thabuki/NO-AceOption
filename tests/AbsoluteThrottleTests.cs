using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

internal static class AbsoluteThrottleTests
{
    private static int checks;
    private static void Near(float expected, float actual, string message)
    {
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > .0001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
        checks++;
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string path = Path.Combine(args[0], "BepInEx", "core", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        // Reproduce the game's delta-based heuristic before applying real Harmony
        // hooks. This model covers input routing, not Rewired hardware or Unity.
        var state = new PilotPlayerState();
        state.player.Set(.8f, 0);
        state.player.Set(1, 1);
        state.simulatedThrottle = .6f;
        state.Tick();
        Near(.8f, state.controlInputs.throttle, "Unpatched cancellation retains a stale 80 percent throttle");

        var harmony = new Harmony("AceOption.AbsoluteThrottleTests");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        try
        {
            state.Tick();
            Near(.5f, state.controlInputs.throttle, "Both full triggers center the previously stale throttle");
            VerifyPressOrders(state);
            VerifyAnalogRange(state);
            VerifyNativeRouting(state);
            VerifyLifecycle(state);
        }
        finally { harmony.UnpatchSelf(); }
        Console.WriteLine("PASS: " + checks + " absolute-throttle cancellation/range/routing assertions");
    }

    private static void VerifyPressOrders(PilotPlayerState state)
    {
        foreach (bool increaseFirst in new[] { true, false })
            foreach (float oldThrottle in new[] { -1f, -.7f, 0f, .3f, 1f })
            {
                AbsoluteThrottle.Reset();
                state.simulatedThrottle = oldThrottle;
                state.player.Set(increaseFirst ? 1 : 0, increaseFirst ? 0 : 1);
                state.Tick();
                Near(increaseFirst ? 1 : 0, state.controlInputs.throttle, "First trigger takes effect immediately");
                state.player.Set(1, 1);
                for (int tick = 0; tick < 4; tick++)
                {
                    state.Tick();
                    Near(.5f, state.controlInputs.throttle, "Equal triggers stay centered in either press order");
                }
                state.player.Set(increaseFirst ? 0 : 1, increaseFirst ? 1 : 0);
                state.Tick();
                Near(increaseFirst ? 0 : 1, state.controlInputs.throttle, "Releasing either trigger follows the remaining input");
                state.player.Set(0, 0);
                state.Tick();
                Near(.5f, state.controlInputs.throttle, "Full release centers even with no active source metadata");
            }

        AbsoluteThrottle.Reset();
        state.simulatedThrottle = .9f;
        state.player.Set(1, 1);
        state.Tick();
        Near(.5f, state.controlInputs.throttle, "Simultaneous first press centers without prior analog history");
    }

    private static void VerifyAnalogRange(PilotPlayerState state)
    {
        foreach (int increase in new[] { 0, 1, 4, 5, 6, 9, 10 })
            foreach (int decrease in new[] { 0, 1, 4, 5, 6, 9, 10 })
            {
                state.player.Set(increase / 10f, decrease / 10f);
                state.Tick();
                float axis = (increase - decrease) / 10f;
                Near((1 + axis) / 2, state.controlInputs.throttle, "Unequal trigger pressures retain their proportional difference");
                Near(axis, state.simulatedThrottle, "Internal throttle tracks the absolute axis too");
                Near(.7f, state.controlInputs.customAxis1, "Custom Axis 1 stays under native control");
            }

        state.player.Set(1, 1);
        foreach (float dt in new[] { 1f / 30, 1f / 50, 1f / 120 })
        {
            state.DeltaTime = dt;
            state.simulatedThrottle = -.8f;
            state.Tick();
            Near(.5f, state.controlInputs.throttle, "Cancellation is independent of frame rate");
        }
        state.DeltaTime = .02f;
        state.collective = true;
        PlayerSettings.invertCollective = true;
        state.player.Set(.8f, .2f);
        state.Tick();
        Near(.2f, state.controlInputs.throttle, "Native collective inversion still applies");
        state.collective = false;
        PlayerSettings.invertCollective = false;
        PlayerSettings.throttleUseNegative = false;
        state.Tick();
        Near(.6f, state.controlInputs.throttle, "Native unipolar range conversion still applies");
        state.player.Set(.2f, .8f);
        state.Tick();
        Near(0, state.controlInputs.throttle, "Native unipolar clamping still applies");
        PlayerSettings.throttleUseNegative = true;
    }

    private static void VerifyNativeRouting(PilotPlayerState state)
    {
        PlayerSettings.throttleUseRelative = true;
        state.player.Set(1, 0);
        state.simulatedThrottle = .2f;
        state.Tick();
        Near(.22f, state.simulatedThrottle, "Relative mode keeps the native throttle ramp");
        state.player.Set(0, 0);
        state.Tick();
        Near(.22f, state.simulatedThrottle, "Relative release keeps the current throttle");
        PlayerSettings.throttleUseRelative = false;

        state.player.Set(1, 0, ControllerElementType.Button);
        state.simulatedThrottle = .4f;
        state.Tick();
        Near(.42f, state.simulatedThrottle, "Keyboard/buttons keep native relative behavior in the default mode");
        state.player.Set(1, 1, ControllerElementType.Button);
        state.Tick();
        Near(.42f, state.simulatedThrottle, "Opposing digital buttons retain the current throttle");
        state.player.Set(0, 0, ControllerElementType.Button);
        state.Tick();
        Near(.42f, state.simulatedThrottle, "Button release cannot revive an earlier analog mode");

        state.player.Set(1, 0);
        state.player.Sources.Add(new InputActionSourceData(ControllerElementType.Button));
        state.simulatedThrottle = .3f;
        state.Tick();
        Near(.32f, state.simulatedThrottle, "Mixed sources retain native arbitration");
        state.player.Set(1, 0);
        state.player.CoordinateMode = AxisCoordinateMode.Relative;
        state.simulatedThrottle = .3f;
        state.Tick();
        Near(.32f, state.simulatedThrottle, "Relative-coordinate axes retain native behavior");
        state.player.CoordinateMode = AxisCoordinateMode.Absolute;

        state.player.Set(.8f, 0);
        state.player.Modifier = true;
        state.simulatedThrottle = .3f;
        state.Tick();
        Near(.3f, state.simulatedThrottle, "Axis Modifier preserves throttle");
        Near(1, state.controlInputs.customAxis1, "Axis Modifier still routes throttle to Custom Axis 1");
        state.player.Modifier = false;

        FlightAssistCommands.Held = true;
        state.simulatedThrottle = .34f;
        state.controlInputs.throttle = .67f;
        state.player.Set(1, 1);
        state.Tick();
        Near(0, state.simulatedThrottle, "Assist chord cannot retain a stale absolute axis");
        Near(.5f, state.controlInputs.throttle, "Absolute centering takes precedence while the assist chord is active");
        state.player.Set(1, .8f);
        state.Tick();
        Near(.6f, state.controlInputs.throttle, "Unequal absolute pressure remains proportional during the assist chord");
        PlayerSettings.throttleUseRelative = true;
        state.simulatedThrottle = .34f;
        state.controlInputs.throttle = .67f;
        state.Tick();
        Near(.34f, state.simulatedThrottle, "Relative assist chord retains the previous internal throttle");
        Near(.67f, state.controlInputs.throttle, "Relative assist chord still holds the previous output throttle");
        PlayerSettings.throttleUseRelative = false;
        FlightAssistCommands.Held = false;

        state.player.Sources = null;
        state.player.Raw = .8f;
        state.player.Previous = .8f;
        state.simulatedThrottle = .3f;
        state.Tick();
        Near(.32f, state.simulatedThrottle, "Unknown nonzero sources fall back to native handling");
        state.player.Sources = new List<InputActionSourceData>();
        Near(0, AbsoluteThrottle.TryRead(null, out float unused) ? 1 : 0, "Missing player is safe");
    }

    private static void VerifyLifecycle(PilotPlayerState state)
    {
        foreach (Action reset in new Action[] { state.EnterState, state.LeaveState, AbsoluteThrottle.Reset })
        {
            state.player.Set(.3f, 0);
            state.Tick();
            reset();
            state.player.Set(0, 0);
            state.player.Previous = 0;
            state.simulatedThrottle = .8f;
            state.Tick();
            Near(.8f, state.simulatedThrottle, "Lifecycle reset clears remembered analog ownership");
        }
        var second = new PilotPlayerState();
        state.player.Set(1, 0);
        state.Tick();
        second.simulatedThrottle = .4f;
        second.Tick();
        Near(.4f, second.simulatedThrottle, "Analog ownership is independent per player");
    }
}

namespace Rewired
{
    public enum ControllerElementType { Axis, Button }
    public enum AxisCoordinateMode { Absolute, Relative }
    public sealed class ActionElementMap { public ControllerElementType elementType; }
    public sealed class InputActionSourceData
    {
        public ActionElementMap actionElementMap;
        public InputActionSourceData(ControllerElementType type) { actionElementMap = new ActionElementMap { elementType = type }; }
    }
    public sealed class Player
    {
        public float Raw, Previous;
        public bool Modifier;
        public AxisCoordinateMode CoordinateMode;
        public List<InputActionSourceData> Sources = new List<InputActionSourceData>();
        public void Set(float increase, float decrease, ControllerElementType type = ControllerElementType.Axis)
        {
            Previous = Raw;
            Raw = increase - decrease;
            Sources.Clear();
            if (increase > 0) Sources.Add(new InputActionSourceData(type));
            if (decrease > 0) Sources.Add(new InputActionSourceData(type));
        }
        public float GetAxisRaw(string action) => Raw;
        public float GetAxisRawPrev(string action) => Previous;
        public bool GetButton(string action) => action == "Axis Modifier" && Modifier;
        public IList<InputActionSourceData> GetCurrentInputSources(string action) => Sources;
        public AxisCoordinateMode GetAxisRawCoordinateMode(string action) => CoordinateMode;
    }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
namespace AceOption.Helpers
{
    internal static class FlightAssistCommands
    {
        internal static bool Held;
        internal static bool ActiveFor(Pilot pilot) => Held;
        internal static void Poll(Pilot pilot, Player player, float strength) { }
        internal static void Reset() => Held = false;
    }
}
public static class PlayerSettings
{
    public static bool throttleUseRelative, invertCollective;
    public static bool throttleUseNegative = true;
}
public sealed class Pilot { }
public sealed class ControlInputs { public float throttle, customAxis1 = .7f; }
public sealed class PilotPlayerState
{
    public Pilot pilot = new Pilot();
    public Player player = new Player();
    public ControlInputs controlInputs = new ControlInputs();
    public float simulatedThrottle, pilotStrength = 1, DeltaTime = .02f;
    public bool collective;
    public void Tick() => PlayerThrottleAxis1Controls();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PlayerThrottleAxis1Controls()
    {
        // Minimal behavioral model of the native change heuristic: gradual
        // changes assign an absolute value; large changes ramp; zero can stick.
        float axis = player.Raw, previous = player.Previous;
        if (PlayerSettings.throttleUseRelative)
        {
            axis = Math.Abs(axis) > .1f ? Math.Sign(axis) : 0;
            previous = Math.Abs(previous) > .1f ? Math.Sign(previous) : 0;
        }
        controlInputs.customAxis1 = .7f;
        if (player.Modifier)
        {
            controlInputs.customAxis1 = UnityEngine.Mathf.Clamp(.7f + axis, 0, 1);
            axis = 0;
        }
        float change = Math.Abs(axis - previous);
        if (change > 0 && change < .5f) simulatedThrottle = axis;
        else if (Math.Abs(axis) > .5f)
            simulatedThrottle += UnityEngine.Mathf.Clamp(axis - simulatedThrottle, -DeltaTime, DeltaTime);
        float output = PlayerSettings.throttleUseNegative ? (1 + simulatedThrottle) / 2 : simulatedThrottle;
        if (collective && PlayerSettings.invertCollective) output = 1 - output;
        controlInputs.throttle = UnityEngine.Mathf.Clamp(output, 0, 1);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public void PlayerControls() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void PlayerAxisControls() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void EnterState() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void LeaveState() { }
}
