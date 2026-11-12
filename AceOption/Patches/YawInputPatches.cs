using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;

namespace AceOption.Patches;

[HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
internal static class YawInputPatch
{
    private static void Prefix(Pilot ___pilot, Player ___player, float ___pilotStrength)
    {
        if (___pilot == null || ___pilot.dead || ___pilotStrength < .2f ||
            ___pilot.aircraft == null || ___pilot.aircraft.cockpit == null ||
            ___pilot.aircraft.cockpit.IsDetached()) YawCommands.Reset(___player);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = InstructionEdits.Read(instructions);
        var getAxis = AccessTools.Method(typeof(Player), nameof(Player.GetAxis), new[] { typeof(string) });
        var sites = InstructionEdits.Find(codes, i => i > 0 &&
            codes[i - 1].opcode == OpCodes.Ldstr && Equals(codes[i - 1].operand, "Yaw") &&
            codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, getAxis));
        if (InstructionEdits.Expect("Player yaw input", sites.Count, 1))
            InstructionEdits.Call(codes, sites, AccessTools.Method(typeof(YawCommands), nameof(YawCommands.Read)));
        return codes;
    }
}

[HarmonyPatch]
internal static class YawLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PilotPlayerState), "EnterState");
        yield return AccessTools.Method(typeof(PilotPlayerState), "LeaveState");
    }

    private static void Prefix() => YawCommands.Reset();
}
