using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace AceOption.Helpers;

// Validate the complete pattern before changing any instruction. Keep labels and EH boundaries.
internal static class InstructionEdits
{
    internal static List<CodeInstruction> Read(IEnumerable<CodeInstruction> source) => new List<CodeInstruction>(source);
    internal static List<int> Find(List<CodeInstruction> codes, Func<int, bool> predicate)
    {
        var found = new List<int>();
        for (int i = 0; i < codes.Count; i++) if (predicate(i)) found.Add(i);
        return found;
    }
    internal static bool Expect(string site, int actual, int expected)
    {
        if (actual == expected) return true;
        Plugin.Logger.LogError($"{site}: expected {expected} patch sites, found {actual}; original method retained.");
        return false;
    }
    internal static void Call(List<CodeInstruction> codes, IEnumerable<int> indices, MethodInfo helper)
    {
        foreach (int i in indices) { codes[i].opcode = OpCodes.Call; codes[i].operand = helper; }
    }
    internal static IEnumerable<CodeInstruction> Field(IEnumerable<CodeInstruction> source, FieldInfo field,
        OpCode load, MethodInfo helper, int count, string site)
    {
        var codes = Read(source);
        var found = Find(codes, i => codes[i].opcode == load && Equals(codes[i].operand, field));
        if (Expect(site, found.Count, count)) Call(codes, found, helper);
        return codes;
    }
}
