using System.Collections.Generic;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;

namespace AceOption.Patches;

[HarmonyPatch(typeof(ControlsFilter), nameof(ControlsFilter.SetFlightAssist))]
internal static class FlightAssistReportPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = InstructionEdits.Read(instructions);
        var report = AccessTools.Method(typeof(AircraftActionsReport), nameof(AircraftActionsReport.ReportText),
            new[] { typeof(string), typeof(float) });
        var sites = InstructionEdits.Find(codes, i => codes[i].Calls(report));
        if (!InstructionEdits.Expect("Flight-assist shortcut report", sites.Count, 1)) return codes;
        int index = sites[0];
        // Append the filter's aircraft argument to the report call. Branches/EH
        // must enter at the new load, so the helper always receives all arguments.
        var load = new CodeInstruction(OpCodes.Ldarg_2);
        load.labels.AddRange(codes[index].labels);
        load.blocks.AddRange(codes[index].blocks);
        codes[index].labels.Clear();
        codes[index].blocks.Clear();
        codes[index].opcode = OpCodes.Call;
        codes[index].operand = AccessTools.Method(typeof(FlightAssistCommands), nameof(FlightAssistCommands.Report));
        codes.Insert(index, load);
        return codes;
    }
}
