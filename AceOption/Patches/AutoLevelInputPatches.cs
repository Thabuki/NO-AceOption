using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
internal static class AutoLevelInputPatch
{
    // Runs after native stick/mouse summation, before Aircraft.FilterInputs.
    // Flight-assist filtering and the aircraft's normal authority remain native.
    private static void Postfix(Pilot ___pilot, Player ___player, float ___pilotStrength,
        ControlInputs ___controlInputs) =>
        AutoLevelCommands.Apply(___pilot, ___player, ___pilotStrength, ___controlInputs);
}
