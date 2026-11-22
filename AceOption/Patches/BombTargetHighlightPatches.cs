using System.Collections.Generic;
using AceOption.Helpers;
using HarmonyLib;
using UnityEngine.UI;

namespace AceOption.Patches;

[HarmonyPatch(typeof(HUDBombingState), "UpdateWeaponDisplay")]
internal static class BombTargetHighlightPatch
{
    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> contacts =
        AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");

    private static void Prefix() => BombTargetHighlight.Restore();

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Aircraft __0, Image ___ccipPipper)
    {
        var hud = SceneSingleton<CombatHUD>.i;
        BombTargetHighlight.Apply(___ccipPipper, __0,
            hud != null && hud.aircraft == __0 ? contacts(hud) : null);
    }
}

[HarmonyPatch(typeof(CombatHUD), "ShowWeaponStation")]
internal static class BombHighlightWeaponChangePatch
{
    private static void Prefix() => BombTargetHighlight.Restore();
}

[HarmonyPatch(typeof(CombatHUD), "RemoveAircraft")]
internal static class BombHighlightAircraftChangePatch
{
    private static void Prefix() => BombTargetHighlight.Restore();
}
