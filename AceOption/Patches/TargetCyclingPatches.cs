using System.Collections.Generic;
using AceOption.Helpers;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Patches;

[HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
internal static class TargetCyclingPatches
{
    private static bool Prefix(CombatHUD __instance, bool __0, List<HUDUnitMarker> ___markers, AudioClip ___selectSound) =>
        !TargetCycling.TrySelect(__instance, __0, ___markers, ___selectSound);
}
