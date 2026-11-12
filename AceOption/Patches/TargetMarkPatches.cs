using System.Collections.Generic;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
internal static class TargetMarkPatches
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = InstructionEdits.Read(instructions);
        var release = AccessTools.Method(typeof(Rewired.Player), "GetButtonTimedPressUp",
            new[] { typeof(string), typeof(float), typeof(float) });
        var hold = AccessTools.Method(typeof(Rewired.Player), "GetButtonTimedPressDown",
            new[] { typeof(string), typeof(float) });
        var releases = InstructionEdits.Find(codes, i => i >= 3 && codes[i-3].opcode == OpCodes.Ldstr &&
            Equals(codes[i-3].operand, "Select") && codes[i].Calls(release));
        var holds = InstructionEdits.Find(codes, i => i >= 2 && codes[i-2].opcode == OpCodes.Ldstr &&
            Equals(codes[i-2].operand, "Select") && codes[i].Calls(hold));
        bool validRelease = InstructionEdits.Expect("Target mark release", releases.Count, 1);
        bool validHold = InstructionEdits.Expect("Target mark hold", holds.Count, 1);
        if (validRelease && validHold)
        {
            InstructionEdits.Call(codes, releases, AccessTools.Method(typeof(TargetMark), nameof(TargetMark.Released)));
            InstructionEdits.Call(codes, holds, AccessTools.Method(typeof(TargetMark), nameof(TargetMark.Held)));
        }
        return codes;
    }
}
