using System.Collections.Generic;
using System.Reflection;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
internal static class AbsoluteThrottleInputPatch
{
    private static void Prefix(Player ___player, ref float ___simulatedThrottle)
    {
        if (AbsoluteThrottle.TryRead(___player, out float axis))
            // Native input-change heuristics otherwise retain a stale value at
            // zero, or ramp large absolute changes as though they were buttons.
            // Seed the current axis before that logic; native range conversion,
            // collective inversion and Custom Axis 1 handling still run.
            ___simulatedThrottle = Mathf.Clamp(axis, -1f, 1f);
    }
}

[HarmonyPatch]
internal static class AbsoluteThrottleLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "EnterState");
        yield return AccessTools.Method(typeof(PilotPlayerState), "LeaveState");
    }

    private static void Prefix() => AbsoluteThrottle.Reset();
}
