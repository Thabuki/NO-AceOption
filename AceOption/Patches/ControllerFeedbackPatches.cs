using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.UpdateLastFired))]
internal static class WeaponFeedbackPatch
{
    private static void Postfix(WeaponStation __instance, int __0) => ControllerFeedback.WeaponFired(__instance, __0);
}

[HarmonyPatch(typeof(UnitPart), nameof(UnitPart.ApplyDamage))]
internal static class DamageFeedbackPatch
{
    private static void Prefix(UnitPart __instance, out float __state) => __state = __instance.hitPoints;
    private static void Postfix(UnitPart __instance, float __state) => ControllerFeedback.Damaged(__instance, __state);
}
