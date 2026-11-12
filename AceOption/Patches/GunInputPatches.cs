using System.Collections.Generic;
using System.Reflection;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
internal static class GunInputPatch
{
    private static readonly FieldInfo Linked = AccessTools.Field(typeof(WeaponManager), "gunsLinked");
    private static readonly FieldInfo Targets = AccessTools.Field(typeof(WeaponManager), "targetList");

    private static void Postfix(Pilot ___pilot, Player ___player, float ___pilotStrength)
    {
        if (!GunCommands.Held(___pilot, ___player, ___pilotStrength)) return;
        var manager = ___pilot.aircraft?.weaponManager;
        if (manager == null) return;
        GunCommands.Fire(___pilot, (bool)Linked.GetValue(manager), (IList<Unit>)Targets.GetValue(manager));
    }
}
