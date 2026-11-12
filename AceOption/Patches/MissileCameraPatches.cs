using System.Collections.Generic;
using System.Reflection;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerControls")]
internal static class MissileFirePatch
{
    private static void Prefix(Pilot ___pilot, Player ___player, float ___pilotStrength) =>
        MissileCamera.Poll(___pilot, ___player, ___pilotStrength);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = InstructionEdits.Read(instructions);
        var fire = AccessTools.Method(typeof(Pilot), nameof(Pilot.Fire));
        var sites = InstructionEdits.Find(codes, i => codes[i].Calls(fire));
        if (InstructionEdits.Expect("Missile fire", sites.Count, 1))
            InstructionEdits.Call(codes, sites, AccessTools.Method(typeof(MissileCamera), nameof(MissileCamera.Fire)));
        return codes;
    }
}

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
internal static class MissileCameraControlPatch
{
    private static void Prefix(Pilot ___pilot, Player ___player, float ___pilotStrength) =>
        MissileCamera.Poll(___pilot, ___player, ___pilotStrength);
}

[HarmonyPatch(typeof(Missile), "StartMissile")]
internal static class MissileLaunchCameraPatch
{
    private static void Postfix(Missile __instance) => MissileCamera.Launched(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
internal static class MissileCameraUpdatePatch
{
    private static void Prefix() => MissileCamera.Update();
}

[HarmonyPatch(typeof(CameraOrbitState), "Inputs")]
internal static class MissileCameraOrbitInputPatch
{
    // Native orbit treats flight input as spectator movement for a missile subject.
    private static bool Prefix(CameraStateManager cam) => !MissileCamera.ActiveFor(cam);
}

[HarmonyPatch(typeof(CameraStateManager), "SwitchState")]
internal static class MissileCameraSwitchPatch
{
    private static void Prefix(CameraStateManager __instance) => MissileCamera.BeforeSwitch(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), "SetFollowingUnit")]
internal static class MissileCameraSubjectPatch
{
    private static void Prefix(CameraStateManager __instance) => MissileCamera.BeforeFollowingUnit(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), "Cam_OnFollowingUnitDisabled")]
internal static class MissileCameraImpactPatch
{
    private static bool Prefix(CameraStateManager __instance, Unit __0) =>
        MissileCamera.FollowingUnitDisabled(__instance, __0);
}

[HarmonyPatch]
internal static class MissileCameraLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "EnterState");
        yield return AccessTools.Method(typeof(PilotPlayerState), "LeaveState");
    }

    private static void Prefix() => MissileCamera.Reset();
}

[HarmonyPatch(typeof(DynamicMap), "Minimize")]
internal static class MissileCameraMapPatch
{
    // Native camera switching minimizes the map. Returning from a missile view
    // while opening the full map must leave that map available.
    private static bool Prefix() => !MissileCamera.PreserveMap;
}
