using System.Collections.Generic;
using System.Reflection;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch]
internal static class FlightAssistInputPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "PlayerControls");
        yield return AccessTools.Method(typeof(PilotPlayerState), "PlayerAxisControls");
    }

    private static void Prefix(Pilot ___pilot, Player ___player, float ___pilotStrength) =>
        FlightAssistCommands.Poll(___pilot, ___player, ___pilotStrength);

    // Reapply after the native assist toggle, if used during the hold.
    private static void Postfix(Pilot ___pilot, Player ___player, float ___pilotStrength) =>
        FlightAssistCommands.Poll(___pilot, ___player, ___pilotStrength);
}

[HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
internal static class FlightAssistThrottlePatch
{
    private static void Prefix(Pilot ___pilot, Player ___player, ControlInputs ___controlInputs,
        float ___simulatedThrottle, out ThrottleSnapshot __state)
    {
        __state = new ThrottleSnapshot {
            // Absolute analog throttle always follows the mapped position,
            // including cancellation at center while the assist chord is held.
            Held = FlightAssistCommands.ActiveFor(___pilot) && !AbsoluteThrottle.TryRead(___player, out _),
            Simulated = ___simulatedThrottle,
            Throttle = ___controlInputs.throttle
        };
    }

    private static void Postfix(ControlInputs ___controlInputs,
        ref float ___simulatedThrottle, ThrottleSnapshot __state)
    {
        if (!__state.Held) return;
        // Hold buttons/relative throttle while preserving native Custom Axis 1.
        // Absolute analog input bypasses this restoration in the prefix.
        ___simulatedThrottle = __state.Simulated;
        ___controlInputs.throttle = __state.Throttle;
    }

    internal struct ThrottleSnapshot
    {
        internal bool Held;
        internal float Simulated, Throttle;
    }
}

[HarmonyPatch]
internal static class FlightAssistLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "EnterState");
        yield return AccessTools.Method(typeof(PilotPlayerState), "LeaveState");
    }

    private static void Prefix() => FlightAssistCommands.Reset();
}
