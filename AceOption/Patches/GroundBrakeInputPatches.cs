using System.Collections.Generic;
using System.Reflection;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch]
internal static class GroundBrakeInputPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "PlayerControls");
        yield return AccessTools.Method(typeof(PilotPlayerState), "PlayerAxisControls");
    }

    private static void Prefix() => GroundBrakeCommands.RestoreBrake();

    private static void Postfix(Pilot ___pilot, Player ___player, float ___pilotStrength,
        ControlInputs ___controlInputs) => GroundBrakeCommands.Apply(___pilot, ___player,
            ___pilotStrength, ___controlInputs,
            GroundBrakeCommands.Held(___pilot, ___player, ___pilotStrength) &&
            GroundContact.OnGround(___pilot.aircraft));
}

[HarmonyPatch]
internal static class GroundBrakeLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "EnterState");
        yield return AccessTools.Method(typeof(PilotPlayerState), "LeaveState");
    }

    private static void Prefix() => GroundBrakeCommands.Reset();
}
