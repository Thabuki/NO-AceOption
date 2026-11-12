using System.Collections.Generic;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch(typeof(Aircraft), "SetupLocalPlayerAndUI")]
internal static class InitialCameraPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        InstructionEdits.Field(instructions,
            AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cockpitState)),
            OpCodes.Ldfld, AccessTools.Method(typeof(CameraPreference), nameof(CameraPreference.InitialState)),
            1, "Initial camera view");
}

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.SwitchState))]
internal static class RememberCameraPatch
{
    private static void Postfix(CameraStateManager __instance) => CameraPreference.Remember(__instance);
}
