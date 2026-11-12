using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch]
internal static class GForcePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GLOC), nameof(GLOC.SimulateGLOC));
        yield return AccessTools.Method(typeof(GLOC), "GLOC_OnSwitchCamera");
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        InstructionEdits.Field(instructions, AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraMode)),
            OpCodes.Ldsfld, AccessTools.Method(typeof(GForcePatches), nameof(EffectiveCameraMode)), 1, "GLOC." + __originalMethod.Name);

    internal static CameraMode EffectiveCameraMode()
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        bool ownChase = CameraStateManager.cameraMode == CameraMode.chase && cam != null && cam.currentState == cam.chaseState &&
            cam.followingUnit != null && GameManager.IsLocalAircraft(cam.followingUnit);
        return ownChase ? CameraMode.cockpit : CameraStateManager.cameraMode;
    }
}
