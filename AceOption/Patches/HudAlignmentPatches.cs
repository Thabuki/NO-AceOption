using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch(typeof(FlightHud), "Update")]
internal static class HudCameraFollowPatch
{
    private static void Postfix(FlightHud __instance) => HudAlignment.FollowCamera(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
internal static class HudAfterCameraPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CameraStateManager __instance) => HudAlignment.AfterCamera(__instance);
}

[HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
internal static class CombatHudAfterCameraPatch
{
    private static bool Prefix(CombatHUD __instance) => HudAlignment.BeforeCombat(__instance);
}

[HarmonyPatch(typeof(HeadMountedDisplay), "Update")]
internal static class LookAwayHudAfterCameraPatch
{
    private static bool Prefix(HeadMountedDisplay __instance) => HudAlignment.BeforeLookAway(__instance);
}
