using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
internal static class JoystickPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        InstructionEdits.Field(instructions, AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraMode)),
            OpCodes.Ldsfld, AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.EffectiveCameraMode)), 1, "Mouse flight input");
}

[HarmonyPatch]
internal static class WeaponFovPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HUDBombingState), "SetHUDWeaponState");
        yield return AccessTools.Method(typeof(HUDBoresightState), "UpdateWeaponDisplay");
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        InstructionEdits.Field(instructions, AccessTools.Field(typeof(PlayerSettings), nameof(PlayerSettings.defaultFoV)),
            OpCodes.Ldsfld, AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.CorrectDefaultFoV)),
            __originalMethod.DeclaringType == typeof(HUDBombingState) ? 1 : 2, __originalMethod.DeclaringType.Name + "." + __originalMethod.Name);
}

[HarmonyPatch(typeof(Turret), "FixedUpdate")]
internal static class TurretPatch
{
    private static bool Calls(CodeInstruction code, string name, System.Type type = null) =>
        (code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && code.operand is MethodInfo method &&
        method.Name == name && (type == null || method.DeclaringType == type);
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = InstructionEdits.Read(instructions);
        var field = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cockpitState));
        var gate = InstructionEdits.Find(codes, i => codes[i].opcode == OpCodes.Ldfld && Equals(codes[i].operand, field));
        var vector = InstructionEdits.Find(codes, i => i >= 2 && Calls(codes[i], "get_forward", typeof(Transform)) &&
            Calls(codes[i-1], "get_transform") && !codes[i-2].IsLdarg(0));
        // Both replacements must match before opening the chase aiming gate.
        bool validGate = InstructionEdits.Expect("Turret camera gate", gate.Count, 1);
        bool validVector = InstructionEdits.Expect("Turret camera direction", vector.Count, 1);
        if (validGate && validVector)
        {
            InstructionEdits.Call(codes, gate, AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.AimAllowedState)));
            InstructionEdits.Call(codes, vector, AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.AimDirection)));
        }
        return codes;
    }
}

[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DisplayHit))]
internal static class HitAudioPatch
{
    private static float nextSound;
    private static void Prefix(Unit hitUnit)
    {
        if (!Plugin.HitSoundOutsideCockpit.Value || !PlayerSettings.showHitMarkers || hitUnit == null) return;
        var cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState == cam.cockpitState || GameAssets.i == null) return;
        if (!(hitUnit is Aircraft) && !(hitUnit is GroundVehicle) && hitUnit.maxRadius > 8) return;
        if (Time.unscaledTime < nextSound) return;
        nextSound = Time.unscaledTime + .05f;
        SoundManager.PlayInterfaceOneShot(GameAssets.i.hitMarkerSound);
    }
}
