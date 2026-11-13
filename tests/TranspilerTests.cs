using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class TranspilerTests
{
    private static string[] _directories;
    private static int _checks;

    public static int Main(string[] args)
    {
        _directories = new[] {
            Path.Combine(args[0], "NuclearOption_Data", "Managed"),
            Path.Combine(args[0], "BepInEx", "core"),
            Path.GetDirectoryName(args[1])
        };
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string directory in _directories) {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(args[1], args.Skip(2).Contains("--optional-combat"), args.Skip(2).Contains("--absolute-throttle")); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string pluginPath, bool optionalCombatOnly, bool absoluteThrottleOnly)
    {
        var plugin = Assembly.LoadFrom(pluginPath);
        var pluginType = plugin.GetType("AceOption.Plugin", true);
        pluginType.GetField("Logger", BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, new BepInEx.Logging.ManualLogSource("Offline tests"));
        if (absoluteThrottleOnly)
        {
            VerifyAbsoluteThrottleContracts(plugin);
            VerifyFlightAssistContracts(plugin);
            VerifyIdentity(plugin, pluginType);
            Console.WriteLine("PASS: " + _checks + " absolute-throttle/assist/identity contract assertions against the installed game");
            return;
        }
        if (optionalCombatOnly)
        {
            VerifyOptionalCombatContracts(plugin);
            Console.WriteLine("PASS: " + _checks + " optional combat contract assertions against the installed game");
            return;
        }
        var patch = plugin.GetType("AceOption.Patches.GForcePatches", true);
        var transpiler = patch.GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static);
        var cameraField = AccessTools.Field(typeof(CameraStateManager), "cameraMode");

        foreach (string name in new[] { "SimulateGLOC", "GLOC_OnSwitchCamera" })
        {
            var method = AccessTools.Method(typeof(GLOC), name);
            var original = PatchProcessor.GetOriginalInstructions(method, (ILGenerator)null);
            var input = original.Select(c => new CodeInstruction(c)).ToList();
            var result = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null,
                new object[] { input, method })).ToList();
            Check(result.Count == original.Count, name + ": no instructions added/removed");
            int changed = 0;
            for (int i = 0; i < original.Count; i++)
            {
                var before = original[i];
                var after = result[i];
                if (before.opcode != after.opcode || !Equals(before.operand, after.operand))
                {
                    changed++;
                    Check(before.opcode == OpCodes.Ldsfld && Equals(before.operand, cameraField),
                        name + ": only the camera gate changes");
                    Check(after.opcode == OpCodes.Call && ((MethodInfo)after.operand).Name == "EffectiveCameraMode",
                        name + ": replacement is the scoped camera helper");
                }
                Check(before.labels.SequenceEqual(after.labels), name + ": branch labels preserved");
                Check(before.blocks.SequenceEqual(after.blocks), name + ": exception blocks preserved");
            }
            Check(changed == 1, name + ": exactly one replacement");

            foreach (int count in new[] { 0, 2 })
            {
                var unusual = Enumerable.Range(0, count)
                    .Select(_ => new CodeInstruction(OpCodes.Ldsfld, cameraField)).ToList();
                var unchanged = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null,
                    new object[] { unusual, method })).ToList();
                Check(unchanged.Count == count && unchanged.All(c => c.opcode == OpCodes.Ldsfld
                    && Equals(c.operand, cameraField)), name + ": unexpected input remains unchanged");
            }
            Console.WriteLine("PASS: " + name + " changes only its camera gate; all simulation/effect instructions preserved");
        }
        VerifyPatch(plugin, "JoystickPatch", typeof(PilotPlayerState), "PlayerAxisControls", 1, "EffectiveCameraMode");
        VerifyPatch(plugin, "WeaponFovPatch", typeof(HUDBombingState), "SetHUDWeaponState", 1, "CorrectDefaultFoV");
        VerifyPatch(plugin, "WeaponFovPatch", typeof(HUDBoresightState), "UpdateWeaponDisplay", 2, "CorrectDefaultFoV");
        VerifyPatch(plugin, "TurretPatch", typeof(Turret), "FixedUpdate", 2, "AimAllowedState", "AimDirection");
        VerifyPatch(plugin, "CameraChaseStatePatches+UpdateState", typeof(CameraChaseState), "UpdateState", 1, "ChaseCenterSwitchRequested");
        VerifyPatch(plugin, "TargetMarkPatches", typeof(CombatHUD), "LateUpdate", 2, "Released", "Held");
        VerifyPatch(plugin, "YawInputPatch", typeof(PilotPlayerState), "PlayerAxisControls", 1, "Read");
        VerifyPatch(plugin, "InitialCameraPatch", typeof(Aircraft), "SetupLocalPlayerAndUI", 1, "InitialState");
        VerifyPatch(plugin, "MissileFirePatch", typeof(PilotPlayerState), "PlayerControls", 1, "Fire");
        VerifyCameraPreferenceContracts(plugin);
        VerifyTargetFocusContracts(plugin);
        VerifyConfigMenuContracts(plugin);
        VerifyHudAlignmentContracts(plugin);
        VerifyYawContracts(plugin);
        VerifyAbsoluteThrottleContracts(plugin);
        VerifyAutoLevelContracts(plugin);
        VerifyIdentity(plugin, pluginType);
        VerifyTurretContracts(plugin);
        VerifyGunContracts(plugin);
        VerifyMissileCameraContracts(plugin);
        VerifyFlightAssistContracts(plugin);
        VerifyFlightStatusContracts(plugin);
        VerifyGroundBrakeContracts(plugin);
        VerifyOptionalCombatContracts(plugin);
        Console.WriteLine("PASS: " + _checks + " transpiler/identity assertions against the installed game");
    }

    private static void VerifyPatch(Assembly plugin, string patchName, Type gameType, string methodName, int expected, params string[] helpers)
    {
        var patch = plugin.GetType("AceOption.Patches." + patchName, true);
        var transpiler = patch.GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static);
        var method = AccessTools.Method(gameType, methodName);
        var original = PatchProcessor.GetOriginalInstructions(method, (ILGenerator)null);
        // Add sentinel metadata at every site to verify it survives replacements.
        var generator = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        foreach (var code in original)
        {
            code.labels.Add(generator.DefineLabel());
            code.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        }
        Func<List<CodeInstruction>, List<CodeInstruction>> run = input =>
            ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, transpiler.GetParameters().Length == 1
                ? new object[] { input } : new object[] { input, method })).ToList();
        var result = run(original.Select(c => new CodeInstruction(c)).ToList());
        Check(result.Count == original.Count, patchName + ": instruction count");
        int changed = 0;
        var seen = new HashSet<string>();
        for (int i = 0; i < original.Count; i++)
        {
            Check(original[i].labels.SequenceEqual(result[i].labels), patchName + ": labels");
            Check(original[i].blocks.SequenceEqual(result[i].blocks), patchName + ": exception boundaries");
            if (original[i].opcode == result[i].opcode && Equals(original[i].operand, result[i].operand)) continue;
            changed++;
            Check(result[i].opcode == OpCodes.Call && result[i].operand is MethodInfo &&
                helpers.Contains(((MethodInfo)result[i].operand).Name), patchName + ": only approved helper substitutions");
            seen.Add(((MethodInfo)result[i].operand).Name);
        }
        Check(changed == expected && seen.SetEquals(helpers), patchName + ": expected substitutions");
        foreach (var malformed in new[] {
            new List<CodeInstruction> { new CodeInstruction(OpCodes.Nop) },
            original.Concat(original).Select(c => new CodeInstruction(c)).ToList()
        })
        {
            var snapshot = malformed.Select(c => new CodeInstruction(c)).ToList();
            var unchanged = run(malformed);
            Check(snapshot.Count == unchanged.Count, patchName + ": mismatch count");
            for (int i = 0; i < snapshot.Count; i++)
                Check(snapshot[i].opcode == unchanged[i].opcode && Equals(snapshot[i].operand, unchanged[i].operand) &&
                    snapshot[i].labels.SequenceEqual(unchanged[i].labels) && snapshot[i].blocks.SequenceEqual(unchanged[i].blocks),
                    patchName + ": mismatches retain original IL and metadata");
        }
        Console.WriteLine("PASS: " + gameType.Name + "." + methodName + " substitutions and mismatch fallback");
    }

    private static List<CodeInstruction> ReadMetadataInstructions(MethodBase method)
    {
        // Unity's mscorlib supplies Span<T>, which the offline .NET Framework
        // host lacks. Read these method bodies as metadata rather than asking
        // Reflection/Harmony to resolve their Span locals. Resolve the target-list
        // field so the native assignment contract can be checked below.
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Name);
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(method.DeclaringType.Assembly.Location))
        {
            var type = assembly.MainModule.Types.Single(t => t.FullName == method.DeclaringType.FullName);
            var definition = type.Methods.Single(m => m.Name == method.Name);
            return definition.Body.Instructions.Select(i => new CodeInstruction(opcodes[i.OpCode.Name],
                i.Operand is Mono.Cecil.FieldReference field && field.Name == "targetList" && field.DeclaringType.Name == "WeaponManager"
                    ? (object)AccessTools.Field(typeof(WeaponManager), "targetList") : i.Operand)).ToList();
        }
    }

    private static void VerifyTurretContracts(Assembly plugin)
    {
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Turret), "FixedUpdate"), (ILGenerator)null);
        int vector = native.FindIndex(c => c.operand is MethodInfo m && m.Name == "SetVector" && m.DeclaringType == typeof(Turret));
        int target = native.FindIndex(c => c.operand is FieldInfo f && f.Name == "target" && f.DeclaringType == typeof(Turret));
        Check(target >= 0 && vector > target && native[target + 3].opcode.FlowControl == FlowControl.Cond_Branch,
            "Native assigned-target gate precedes manual camera vector input");
        var branch = (Label)native[target + 3].operand;
        int automatic = native.FindIndex(c => c.labels.Contains(branch));
        int trackedAim = native.FindIndex(c => c.operand is MethodInfo m && m.Name == "AimTurret" &&
            m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(WeaponStation));
        Check(automatic > vector && trackedAim >= automatic,
            "An assigned target bypasses camera input and uses native tracked aim");
        Check(native.Any(c => c.operand is MethodInfo m && m.Name == "SetTurretVector" && m.DeclaringType == typeof(Aircraft)),
            "Native manual vector path replicates the camera direction");
        foreach (string name in new[] { "TargetListChanged", "SetActiveStation" })
        {
            var method = AccessTools.Method(typeof(WeaponManager), name);
            var instructions = ReadMetadataInstructions(method);
            var reads = instructions.Where(c => c.operand is FieldInfo f && f.Name == "targetList").ToList();
            Check(reads.Count == 3 && reads.All(c => c.opcode == OpCodes.Ldfld),
                name + ": native target list reads remain available for automatic aiming");
            string receiver = name == "TargetListChanged" ? "Aircraft" : "WeaponStation";
            Check(instructions.Any(c => c.operand != null && c.operand.ToString().Contains(receiver + "::SetStationTargets(")),
                name + ": native station assignment/replication receives selected targets");
        }
        var active = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(WeaponStation), "SetStationActive"), (ILGenerator)null);
        Check(active.Any(c => c.operand is MethodInfo m && m.Name == "SetManual" && m.DeclaringType == typeof(Turret)),
            "Native station selection puts fitted turrets under manual control");
        Check(plugin.GetType("AceOption.Patches.TurretTargetsPatch") == null,
            "No turret target-assignment override is installed");
        Check(plugin.GetType("AceOption.Helpers.TurretCamera") == null,
            "No camera polling can clear or resync turret targets");
        Console.WriteLine("PASS: native automatic turret target priority and manual vector replication are preserved");
    }

    private static void VerifyYawContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.YawInputPatch", true);
        foreach (var parameter in AccessTools.Method(patch, "Prefix").GetParameters())
        {
            var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
            Check(field != null && field.FieldType == parameter.ParameterType,
                "Yaw Harmony field injection: " + parameter.Name);
        }
        var lifecycle = plugin.GetType("AceOption.Patches.YawLifecyclePatch", true);
        var methods = ((IEnumerable<MethodBase>)AccessTools.Method(lifecycle, "TargetMethods").Invoke(null, null)).ToList();
        Check(methods.Count == 2 && methods.All(m => m != null && m.DeclaringType == typeof(PilotPlayerState)),
            "Yaw enter/leave hooks exist in the installed game");
        var fixedUpdate = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(PilotPlayerState), "FixedUpdateState"), (ILGenerator)null);
        int axes = fixedUpdate.FindIndex(c => c.operand is MethodInfo m && m.Name == "PlayerAxisControls");
        int assists = fixedUpdate.FindIndex(c => c.operand is MethodInfo m && m.Name == "FilterInputs");
        Check(axes >= 0 && assists > axes, "Smoothed yaw enters the native flight-assist filter");
        Console.WriteLine("PASS: yaw reset hooks and native flight-assist ordering match the installed game");
    }

    private static void VerifyAbsoluteThrottleContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.AbsoluteThrottleInputPatch", true);
        foreach (var parameter in AccessTools.Method(patch, "Prefix").GetParameters())
        {
            var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
            var type = parameter.ParameterType;
            Check(field != null && field.FieldType == (type.IsByRef ? type.GetElementType() : type),
                "Absolute throttle Harmony field injection: " + parameter.Name);
        }
        var lifecycle = plugin.GetType("AceOption.Patches.AbsoluteThrottleLifecyclePatch", true);
        var hooks = ((IEnumerable<MethodBase>)AccessTools.Method(lifecycle, "TargetMethods").Invoke(null, null)).ToList();
        Check(hooks.Count == 2 && hooks.All(m => m != null && m.DeclaringType == typeof(PilotPlayerState)),
            "Absolute throttle enter/leave hooks exist");
        foreach (string name in new[] { "throttleUseRelative", "throttleUseNegative", "invertCollective" })
            Check(AccessTools.Field(typeof(PlayerSettings), name)?.FieldType == typeof(bool),
                "Native throttle setting exists: " + name);
        Check(AccessTools.Method(typeof(Rewired.Player), "GetAxisRawCoordinateMode", new[] { typeof(string) })?.ReturnType == typeof(Rewired.AxisCoordinateMode),
            "Rewired exposes raw axis coordinate mode");
        Check(AccessTools.Method(typeof(Rewired.Player), "GetCurrentInputSources", new[] { typeof(string) }) != null &&
            AccessTools.Property(typeof(Rewired.ActionElementMap), "elementType")?.PropertyType == typeof(Rewired.ControllerElementType),
            "Rewired exposes physical source types");

        var native = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls"), (ILGenerator)null);
        foreach (string name in new[] { "throttleUseRelative", "throttleUseNegative", "invertCollective" })
            Check(native.Any(c => c.operand is FieldInfo f && f.DeclaringType == typeof(PlayerSettings) && f.Name == name),
                "Native range/relative/collective handling remains in the throttle method: " + name);
        int seededRead = native.FindIndex(c => c.opcode == OpCodes.Ldfld && c.operand is FieldInfo f && f.Name == "simulatedThrottle");
        int firstWrite = native.FindIndex(c => c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.Name == "simulatedThrottle");
        // The first write is the native direct-axis assignment. The other path
        // reads the seeded field before ramping toward that same current axis.
        Check(firstWrite >= 0 && seededRead > firstWrite && native.Count(c => c.opcode == OpCodes.Stfld &&
            c.operand is FieldInfo f && f.Name == "simulatedThrottle") == 2,
            "Native heuristic retains its direct-assignment and seeded-ramp paths");
        Check(native.Any(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "Axis Modifier")) &&
            native.Any(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "Custom Axis 1")),
            "Native modifier/custom-axis handling remains available");
        Console.WriteLine("PASS: absolute throttle source/setting/field hooks match the installed game and Rewired");
    }

    private static void VerifyAutoLevelContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.AutoLevelInputPatch", true);
        var hook = AccessTools.Method(patch, "Postfix");
        foreach (var parameter in hook.GetParameters())
        {
            var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
            Check(field != null && field.FieldType == parameter.ParameterType,
                "Auto-level Harmony field injection: " + parameter.Name);
        }
        var attribute = patch.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single();
        Check(attribute.info.declaringType == typeof(PilotPlayerState) && attribute.info.methodName == "PlayerAxisControls",
            "Auto-level runs after player axes, before the native aircraft filter");
        var body = AccessTools.Method(typeof(Aircraft), "CockpitRB");
        Check(body != null && body.ReturnType == typeof(UnityEngine.Rigidbody), "Native cockpit body supplies attitude/rates");
        Check(typeof(HeloControlsFilter).IsSubclassOf(typeof(ControlsFilter)), "Native helicopter filter can be excluded");
        foreach (string name in new[] { "GetButton", "GetNegativeButton" })
            Check(AccessTools.Method(typeof(Rewired.Player), name, new[] { typeof(string) })?.ReturnType == typeof(bool),
                "Independent yaw pole API exists: " + name);
        var altitude = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Aircraft), "CheckRadarAlt"), (ILGenerator)null);
        int airborne = altitude.FindIndex(c => c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.Name == "airborne");
        Check(airborne >= 4 && altitude[airborne - 1].opcode == OpCodes.Cgt &&
            altitude[airborne - 2].opcode == OpCodes.Ldc_R4 && (float)altitude[airborne - 2].operand == .2f &&
            altitude[airborne - 3].operand is FieldInfo radar && radar.Name == "radarAlt",
            "Auto-level's ground gate matches the native airborne radar-altitude threshold");
        var commands = plugin.GetType("AceOption.Helpers.AutoLevelCommands", true);
        var instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(commands, "Apply"), (ILGenerator)null);
        Check(!instructions.Any(c => c.operand is MethodInfo m &&
            (m.Name.StartsWith("set_") || m.Name == "SetFlightAssist" || m.Name == "AddTorque")),
            "Auto-level never writes aircraft attitude/velocity, forces or assist state");
        var writes = instructions.Where(c => c.opcode == OpCodes.Stfld).Select(c => (FieldInfo)c.operand).ToList();
        Check(writes.Count == 3 && writes.All(f => f.DeclaringType == typeof(ControlInputs)) &&
            writes.Select(f => f.Name).OrderBy(n => n).SequenceEqual(new[] { "pitch", "roll", "yaw" }),
            "Auto-level writes only native pitch/roll/yaw inputs");
        Check(AccessTools.Field(plugin.GetType("AceOption.Plugin", true), "YawChordAutoLevel") != null,
            "Auto-level has an independent configuration switch");
        Console.WriteLine("PASS: auto-level hooks, ground gate and native input-only contracts match the installed game");
    }

    private static void VerifyGroundBrakeContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.GroundBrakeInputPatch", true);
        foreach (string hook in new[] { "Prefix", "Postfix" })
        foreach (var parameter in AccessTools.Method(patch, hook).GetParameters())
        {
            var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
            Check(field != null && field.FieldType == parameter.ParameterType,
                "Ground brake Harmony field injection: " + parameter.Name);
        }
        foreach (string name in new[] { "GroundBrakeInputPatch", "GroundBrakeLifecyclePatch" })
        {
            var methods = ((IEnumerable<MethodBase>)AccessTools.Method(
                plugin.GetType("AceOption.Patches." + name, true), "TargetMethods").Invoke(null, null)).ToList();
            var expected = name == "GroundBrakeInputPatch"
                ? new[] { "PlayerAxisControls", "PlayerControls" } : new[] { "EnterState", "LeaveState" };
            Check(methods.Count == 2 && methods.All(m => m != null && m.DeclaringType == typeof(PilotPlayerState)) &&
                methods.Select(m => m.Name).OrderBy(n => n).SequenceEqual(expected), name + ": installed hooks match");
        }
        Check(AccessTools.Field(typeof(LandingGear), "compressionDistance")?.FieldType == typeof(float),
            "Native suspension compression is available for ground contact");
        Check(AccessTools.Field(typeof(LandingGear), "attachedPart")?.FieldType == typeof(AeroPart),
            "Native landing gear attachment is available to exclude detached wheels");
        var partRegistry = AccessTools.Field(typeof(Unit), "partLookup");
        Check(partRegistry?.FieldType == typeof(List<UnitPart>),
            "Native part registry is available for wheels outside the aircraft hierarchy");
        var contact = plugin.GetType("AceOption.Helpers.GroundContact", true);
        RuntimeHelpers.RunClassConstructor(contact.TypeHandle);
        Check(AccessTools.Field(contact, "compression").GetValue(null) is Delegate &&
            AccessTools.Field(contact, "attachedPart").GetValue(null) is Delegate,
            "Ground-contact field accessors initialize against the installed game");
        var discovery = PatchProcessor.GetOriginalInstructions(AccessTools.Method(contact, "OnGround"), (ILGenerator)null);
        Check(discovery.Any(c => c.opcode == OpCodes.Ldfld && Equals(c.operand, partRegistry)),
            "Compiled ground-contact discovery reads the native physical-part registry");
        var createBody = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(AeroPart), "CreateRB"), (ILGenerator)null);
        Check(createBody.Where((c, i) => i >= 2 && c.operand is MethodInfo m &&
            m.DeclaringType == typeof(UnityEngine.Transform) && m.Name == "SetParent" &&
            createBody[i - 2].opcode == OpCodes.Ldnull).Any(),
            "Native physical parts can leave the aircraft transform hierarchy");
        var register = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(UnitPart), "Awake"), (ILGenerator)null);
        Check(register.Any(c => c.opcode == OpCodes.Ldfld && Equals(c.operand, partRegistry)) &&
            register.Any(c => c.operand is MethodInfo m && m.DeclaringType == typeof(List<UnitPart>) && m.Name == "Add"),
            "Native part initialization registers physical parts with their unit");
        var gear = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(LandingGear), "FixedUpdate"), (ILGenerator)null);
        Check(gear.Any(c => c.operand is MethodInfo m && m.Name == "Linecast") &&
            gear.Any(c => c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.Name == "compressionDistance"),
            "Native wheel contact updates suspension compression from a physics linecast");
        Check(gear.Where((c, i) => i > 0 && c.opcode == OpCodes.Stfld &&
            c.operand is FieldInfo f && f.Name == "compressionDistance" &&
            gear[i - 1].opcode == OpCodes.Ldc_R4 && (float)gear[i - 1].operand == 0).Any(),
            "Native contact loss clears suspension compression");
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(PilotPlayerState), "PlayerControls"), (ILGenerator)null);
        Check(native.Any(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "Brake")) &&
            native.Any(c => c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.Name == "brake"),
            "Normal Brake remains handled by native player controls");
        var commands = plugin.GetType("AceOption.Helpers.GroundBrakeCommands", true);
        var prefix = PatchProcessor.GetOriginalInstructions(AccessTools.Method(patch, "Prefix"), (ILGenerator)null);
        Check(prefix.Any(c => c.operand is MethodInfo m && m.DeclaringType == commands && m.Name == "RestoreBrake"),
            "Ground-brake contribution is restored before native control updates");
        var apply = PatchProcessor.GetOriginalInstructions(AccessTools.Method(commands, "Apply"), (ILGenerator)null);
        Check(apply.Any(c => c.opcode == OpCodes.Ldfld && c.operand is FieldInfo f &&
                f.DeclaringType == typeof(ControlInputs) && f.Name == "throttle"),
            "Ground-brake delay reads the current native throttle after controls update");
        var writes = apply.Where(c => c.opcode == OpCodes.Stfld).Select(c => (FieldInfo)c.operand).ToList();
        Check(writes.Count == 1 && writes[0].DeclaringType == typeof(ControlInputs) && writes[0].Name == "brake",
            "Ground braking writes only the native brake input");
        var inputs = new ControlInputs { brake = 1f, throttle = .6f };
        AccessTools.Field(commands, "borrowedInputs").SetValue(null, inputs);
        AccessTools.Field(commands, "previousBrake").SetValue(null, .35f);
        AccessTools.Field(commands, "heldSince").SetValue(null, 17f);
        AccessTools.Method(patch, "Prefix").Invoke(null, null);
        Check(inputs.brake == .35f && (float)AccessTools.Field(commands, "heldSince").GetValue(null) == 17f,
            "Compiled per-control cleanup preserves the pending brake activation timer");
        inputs.brake = 1f;
        AccessTools.Field(commands, "borrowedInputs").SetValue(null, inputs);
        AccessTools.Field(commands, "previousBrake").SetValue(null, .35f);
        AccessTools.Method(commands, "Reset").Invoke(null, null);
        Check(inputs.brake == .35f && inputs.throttle == .6f,
            "Compiled cleanup restores native brake input without changing throttle");
        Check(AccessTools.Field(plugin.GetType("AceOption.Plugin", true), "ThrottleDownGroundBrakes") != null,
            "Ground braking has an independent configuration switch");
        Console.WriteLine("PASS: ground-brake hooks, wheel-contact accessors and compiled cleanup match the installed game");
    }

    private static void VerifyFlightAssistContracts(Assembly plugin)
    {
        foreach (string name in new[] { "FlightAssistInputPatch", "FlightAssistThrottlePatch" })
        {
            var patch = plugin.GetType("AceOption.Patches." + name, true);
            foreach (string hook in new[] { "Prefix", "Postfix" })
                foreach (var parameter in AccessTools.Method(patch, hook).GetParameters().Where(p => p.Name.StartsWith("___")))
                {
                    var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
                    var type = parameter.ParameterType;
                    Check(field != null && field.FieldType == (type.IsByRef ? type.GetElementType() : type),
                        "Flight assist Harmony field injection: " + parameter.Name);
                }
        }
        foreach (string name in new[] { "FlightAssistInputPatch", "FlightAssistLifecyclePatch" })
        {
            var patch = plugin.GetType("AceOption.Patches." + name, true);
            var methods = ((IEnumerable<MethodBase>)AccessTools.Method(patch, "TargetMethods").Invoke(null, null)).ToList();
            Check(methods.Count == 2 && methods.All(m => m != null && m.DeclaringType == typeof(PilotPlayerState)),
                name + ": installed hooks exist");
        }
        Check(AccessTools.Method(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls") != null,
            "Native throttle/custom-axis hook exists");
        var setter = AccessTools.Method(typeof(Aircraft), "SetFlightAssist", new[] { typeof(bool) });
        var native = PatchProcessor.GetOriginalInstructions(setter, (ILGenerator)null);
        Check(native.Any(c => c.operand is MethodInfo m && m.Name == "SetFlightAssist" && m.DeclaringType == typeof(ControlsFilter)),
            "Native setter updates the aircraft's control filter");
        Check(native.Any(c => c.operand is FieldInfo f && f.Name == "onSetFlightAssist"),
            "Native setter notifies HUD/aircraft assist listeners");

        // Run the compiled postfix on native plain-data inputs, without creating
        // a Unity object. Custom Axis 1 must survive the throttle restoration.
        var throttlePatch = plugin.GetType("AceOption.Patches.FlightAssistThrottlePatch", true);
        var snapshotType = throttlePatch.GetNestedType("ThrottleSnapshot", BindingFlags.NonPublic);
        var snapshot = Activator.CreateInstance(snapshotType);
        AccessTools.Field(snapshotType, "Held").SetValue(snapshot, true);
        AccessTools.Field(snapshotType, "Simulated").SetValue(snapshot, .65f);
        AccessTools.Field(snapshotType, "Throttle").SetValue(snapshot, .75f);
        var inputs = new ControlInputs { throttle = .2f, customAxis1 = .7f };
        object[] arguments = { inputs, .1f, snapshot };
        AccessTools.Method(throttlePatch, "Postfix").Invoke(null, arguments);
        Check((float)arguments[1] == .65f && inputs.throttle == .75f && inputs.customAxis1 == .7f,
            "Compiled hold restores simulated/output throttle while retaining native custom axis");
        AccessTools.Field(snapshotType, "Held").SetValue(snapshot, false);
        inputs.throttle = .3f;
        arguments = new object[] { inputs, .4f, snapshot };
        AccessTools.Method(throttlePatch, "Postfix").Invoke(null, arguments);
        Check((float)arguments[1] == .4f && inputs.throttle == .3f,
            "Compiled throttle hook passes through when the chord is inactive");
        Console.WriteLine("PASS: flight-assist hooks/setter contracts and compiled throttle preservation match the installed game");
    }

    private static void VerifyFlightStatusContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.FlightAssistReportPatch", true);
        var attribute = patch.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single();
        Check(attribute.info.declaringType == typeof(ControlsFilter) && attribute.info.methodName == "SetFlightAssist",
            "Assist notification suppression targets only the control filter's assist setter");
        var nativeMethod = AccessTools.Method(typeof(ControlsFilter), "SetFlightAssist", new[] { typeof(bool), typeof(Aircraft) });
        Check(nativeMethod != null && nativeMethod.GetParameters()[1].ParameterType == typeof(Aircraft),
            "Assist report helper receives the native setter's aircraft argument");
        var original = PatchProcessor.GetOriginalInstructions(nativeMethod, (ILGenerator)null);
        var report = AccessTools.Method(typeof(AircraftActionsReport), "ReportText", new[] { typeof(string), typeof(float) });
        int site = original.FindIndex(c => c.Calls(report));
        Check(site >= 0 && original.Count(c => c.Calls(report)) == 1, "Native assist setter has one timed action report");
        var generator = new DynamicMethod("AssistReportLabels", typeof(void), Type.EmptyTypes).GetILGenerator();
        foreach (var code in original)
        {
            code.labels.Add(generator.DefineLabel());
            code.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        }
        var transpiler = AccessTools.Method(patch, "Transpiler");
        Func<List<CodeInstruction>, List<CodeInstruction>> run = input =>
            ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { input })).ToList();
        var result = run(original.Select(c => new CodeInstruction(c)).ToList());
        Check(result.Count == original.Count + 1 && result[site].opcode == OpCodes.Ldarg_2,
            "Assist report patch adds only the aircraft load");
        Check(result[site].labels.SequenceEqual(original[site].labels) && result[site].blocks.SequenceEqual(original[site].blocks),
            "Report branches and exception boundaries enter before the added argument load");
        var commands = plugin.GetType("AceOption.Helpers.FlightAssistCommands", true);
        Check(result[site + 1].opcode == OpCodes.Call && Equals(result[site + 1].operand, AccessTools.Method(commands, "Report")) &&
            result[site + 1].labels.Count == 0 && result[site + 1].blocks.Count == 0,
            "Only the native report call is replaced, with no duplicated metadata");
        for (int i = 0; i < original.Count; i++)
        {
            if (i == site) continue;
            var after = result[i < site ? i : i + 1];
            Check(original[i].opcode == after.opcode && Equals(original[i].operand, after.operand) &&
                original[i].labels.SequenceEqual(after.labels) && original[i].blocks.SequenceEqual(after.blocks),
                "Assist filter logic and all other IL/metadata remain native");
        }
        foreach (var malformed in new[] {
            new List<CodeInstruction> { new CodeInstruction(OpCodes.Nop) },
            original.Concat(original).Select(c => new CodeInstruction(c)).ToList()
        })
        {
            var snapshot = malformed.Select(c => new CodeInstruction(c)).ToList();
            var unchanged = run(malformed);
            Check(unchanged.Count == snapshot.Count, "Unexpected assist report layout retains its instruction count");
            for (int i = 0; i < snapshot.Count; i++)
                Check(snapshot[i].opcode == unchanged[i].opcode && Equals(snapshot[i].operand, unchanged[i].operand) &&
                    snapshot[i].labels.SequenceEqual(unchanged[i].labels) && snapshot[i].blocks.SequenceEqual(unchanged[i].blocks),
                    "Unexpected assist report layout retains its IL and metadata");
        }
        var helper = PatchProcessor.GetOriginalInstructions(AccessTools.Method(commands, "Report"), (ILGenerator)null);
        Check(helper.Count(c => c.Calls(report)) == 1 && helper.Any(c => c.operand is MethodInfo m && m.Name == "SuppressReportFor"),
            "Assist report helper gates the native report through aircraft-scoped suppression");
        Check(!helper.Any(c => c.operand is MethodInfo m && m.Name == "ClearMessages"),
            "Suppressing assist reports never clears unrelated queued messages");

        Check(AccessTools.Field(typeof(AircraftActionsReport), "actionsText")?.FieldType.FullName == "TMPro.TextMeshProUGUI" &&
            AccessTools.Field(typeof(FlightHud), "canvas")?.FieldType.FullName == "UnityEngine.Canvas",
            "Native HUD canvas and themed action-text template are available");
        var status = plugin.GetType("AceOption.Helpers.FlightStatusHud", true);
        RuntimeHelpers.RunClassConstructor(status.TypeHandle);
        Check(AccessTools.Field(status, "actionText").GetValue(null) is Delegate &&
            AccessTools.Field(status, "hudCanvas").GetValue(null) is Delegate,
            "Status HUD field accessors initialize against installed game types");
        var render = ReadMetadataInstructions(AccessTools.Method(status, "Refresh"));
        Check(render.Any(c => c.operand is Mono.Cecil.MethodReference m && m.DeclaringType.Name == "AutoLevelCommands" && m.Name == "ActiveFor") &&
            render.Any(c => c.operand is Mono.Cecil.MethodReference m && m.DeclaringType.Name == "FlightAssistCommands" && m.Name == "HeldFor"),
            "Status HUD uses the successful leveling state and currently held assist shortcut");
        Check(!render.Any(c => c.operand is Mono.Cecil.MethodReference m && m.Name == "ReportText"),
            "Hold indicators never enter the timed message queue");
        Console.WriteLine("PASS: held-status HUD accessors and scoped assist-report substitution match the installed game");
    }

    private static void VerifyOptionalCombatContracts(Assembly plugin)
    {
        var targetPatch = plugin.GetType("AceOption.Patches.TargetCyclingPatches", true);
        var prefix = AccessTools.Method(targetPatch, "Prefix");
        var targetSelect = AccessTools.Method(typeof(CombatHUD), "TargetSelect");
        Check(targetSelect != null && targetSelect.GetParameters().Length == 1 &&
            targetSelect.GetParameters()[0].ParameterType == typeof(bool), "Native target cycling entry point matches");
        foreach (var parameter in prefix.GetParameters().Where(p => p.Name.StartsWith("___")))
        {
            var field = AccessTools.Field(typeof(CombatHUD), parameter.Name.Substring(3));
            Check(field != null && field.FieldType == parameter.ParameterType,
                "Target cycling Harmony field injection: " + parameter.Name);
        }
        Check(AccessTools.Method(typeof(CombatHUD), "DeselectAll").GetParameters()[0].ParameterType == typeof(bool),
            "Native deselection retains map/HUD cleanup");
        Check(AccessTools.Method(typeof(WeaponManager), "TargetListChanged") != null,
            "Native target notification exists");
        Console.WriteLine("PASS: optional targeting hooks match the installed game");
    }

    private static void VerifyTargetFocusContracts(Assembly plugin)
    {
        var tracked = AccessTools.Method(typeof(FactionHQ), "IsTargetBeingTracked", new[] { typeof(Unit) });
        var known = AccessTools.Method(typeof(FactionHQ), "TryGetKnownPosition");
        Check(tracked != null && tracked.ReturnType == typeof(bool), "Native faction tracking gate is available");
        Check(known != null && known.ReturnType == typeof(bool) && known.GetParameters().Length == 2 &&
            known.GetParameters()[0].ParameterType == typeof(Unit) && known.GetParameters()[1].IsOut &&
            known.GetParameters()[1].ParameterType.GetElementType().Name == "GlobalPosition",
            "Native known-position API supplies an authorized global position");
        var type = plugin.GetType("AceOption.Helpers.TargetFocus", true);
        var resolve = ReadMetadataInstructions(AccessTools.Method(type, "TryGetPosition"));
        Func<CodeInstruction, string, string, bool> calls = (code, owner, name) =>
            code.operand is Mono.Cecil.MethodReference method && method.DeclaringType.Name == owner && method.Name == name;
        int gate = resolve.FindIndex(code => calls(code, "FactionHQ", "IsTargetBeingTracked"));
        int position = resolve.FindIndex(code => calls(code, "FactionHQ", "TryGetKnownPosition"));
        Check(gate >= 0 && position > gate, "Focus checks tracking before resolving a known position");
        Check(resolve.Skip(gate + 1).Take(position - gate - 1).Any(code => code.opcode.FlowControl == FlowControl.Cond_Branch),
            "Failed tracking bypasses the known-position lookup");
        Check(resolve.Any(code => calls(code, "GlobalPositionExtensions", "ToLocalPosition")),
            "Authorized global positions use the native floating-origin conversion");
        Check(!resolve.Any(code => code.operand is Mono.Cecil.MethodReference method &&
            (method.Name == "get_transform" || method.Name == "get_position")),
            "The focus position resolver never reads a target transform");
        foreach (string name in new[] { "GetTarget", "Apply" })
            Check(ReadMetadataInstructions(AccessTools.Method(type, name)).Any(code => calls(code, "TargetFocus", "TryGetPosition")),
                name + " revalidates faction tracking and position");
        var nativeTracking = ReadMetadataInstructions(tracked);
        Check(nativeTracking.Any(code => code.operand is Mono.Cecil.FieldReference field && field.Name == "lastSpottedTime") &&
            nativeTracking.Any(code => code.opcode == OpCodes.Ldc_R4 && Equals(code.operand, 4f)),
            "The installed game's tracking gate retains its native four-second freshness interval");
        Console.WriteLine("PASS: target focus uses native faction tracking and known-position contracts");
    }

    private static void VerifyCameraPreferenceContracts(Assembly plugin)
    {
        var setup = AccessTools.Method(typeof(Aircraft), "SetupLocalPlayerAndUI");
        Check(setup != null && setup.GetParameters().Length == 0, "Local aircraft setup hook exists");
        var codes = PatchProcessor.GetOriginalInstructions(setup, (ILGenerator)null);
        int follow = codes.FindIndex(c => c.Calls(AccessTools.Method(typeof(CameraStateManager), "SetFollowingUnit")));
        int cockpit = codes.FindIndex(c => c.opcode == OpCodes.Ldfld &&
            Equals(c.operand, AccessTools.Field(typeof(CameraStateManager), "cockpitState")));
        int pilotState = codes.FindIndex(c => c.Calls(AccessTools.Method(typeof(Pilot), "SwitchState")));
        Check(pilotState >= 0 && follow > pilotState && cockpit > follow,
            "Native initial camera selection follows pilot state and camera subject setup");
        Check(codes[cockpit + 1].Calls(AccessTools.Method(typeof(CameraStateManager), "SwitchState")),
            "Initial cockpit read supplies the native SwitchState argument");
        var helper = plugin.GetType("AceOption.Helpers.CameraPreference", true);
        Check(helper.GetMethod("InitialState").ReturnType == typeof(CameraBaseState),
            "Initial camera helper returns the native SwitchState base type");
        var remember = plugin.GetType("AceOption.Patches.RememberCameraPatch", true);
        var hook = remember.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
        Check(hook.GetParameters().Single().Name == "__instance" &&
            hook.GetParameters()[0].ParameterType == typeof(CameraStateManager), "Camera selection hook receives the camera instance");
        var target = remember.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single().info;
        Check(target.declaringType == typeof(CameraStateManager) && target.methodName == "SwitchState",
            "Preference observes native and custom camera switching");
        Console.WriteLine("PASS: camera preference hooks match native local aircraft initialization and camera switching");
    }

    private static void VerifyConfigMenuContracts(Assembly plugin)
    {
        var groups = new[] {
            new {
                Name = "Free Look",
                Keys = new[] { "AlwaysFreeLook", "FreeLookSensitivity", "FreeLookDeadzone", "AutoCenterDelay", "AutoCenterSpeed" }
            },
            new {
                Name = "Chase Camera",
                Keys = new[] {
                    "RememberCameraView", "CameraAdjustments", "CameraDistance", "PlaneVerticalOffset",
                    "TargetFocusEnabled", "TargetFocusHoldDelay", "FixWeaponFoV", "HitSoundOutsideCockpit"
                }
            },
            new {
                Name = "Chase HUD",
                Keys = new[] {
                    "FlightHud", "HudFollowsCamera", "Map", "PitchLadder", "Waterline",
                    "LeftInstruments", "RightInstruments", "Compass", "VirtualJoystickIndicator"
                }
            },
            new {
                Name = "Flight Controls",
                Keys = new[] {
                    "VirtualJoystick", "ThrottleChordFlightAssist", "ThrottleDownGroundBrakes",
                    "YawChordAutoLevel", "SmoothButtonYaw", "YawRiseTime", "YawReturnTime"
                }
            },
            new {
                Name = "Combat",
                Keys = new[] { "MissileFollowCamera", "TargetCycling", "TurretAim" }
            }
        };
        var awake = ReadMetadataInstructions(AccessTools.Method(plugin.GetType("AceOption.Plugin", true), "Awake"));
        var describe = AccessTools.Method(plugin.GetType("AceOption.Helpers.ConfigMenu", true), "Describe");
        var settings = awake.Where(c => c.opcode == OpCodes.Stsfld && c.operand is Mono.Cecil.FieldReference field &&
                field.FieldType.FullName.StartsWith("BepInEx.Configuration.ConfigEntry`1"))
            .Select(c => ((Mono.Cecil.FieldReference)c.operand).Name)
            .Select(key => {
                var description = (BepInEx.Configuration.ConfigDescription)describe.Invoke(null, new object[] { key, "", null });
                var tag = description.Tags.Single();
                return new {
                    Key = key,
                    Category = (string)AccessTools.Field(tag.GetType(), "Category").GetValue(tag),
                    Order = (int?)AccessTools.Field(tag.GetType(), "Order").GetValue(tag),
                    Browsable = (bool?)AccessTools.Field(tag.GetType(), "Browsable").GetValue(tag)
                };
            }).Where(s => s.Browsable != false).ToList();
        Check(settings.Select(s => s.Category).Distinct().SequenceEqual(groups.Select(g => g.Name)),
            "Menu registration preserves the five requested unnumbered sections in order");
        foreach (var group in groups)
            Check(settings.Where(s => s.Category == group.Name).OrderByDescending(s => s.Order)
                    .Select(s => s.Key).SequenceEqual(group.Keys),
                "Menu options and dependency order match " + group.Name);

        int focus = awake.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "TargetFocusEnabled"));
        Check(focus > 0 && Equals(awake[focus - 1].operand, "Other") && awake[focus + 1].opcode == OpCodes.Ldc_I4_0,
            "Target focus has an independent default-off configuration flag");
        int deadzone = awake.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "FreeLookDeadzone"));
        Check(deadzone >= 0 && Equals(awake[deadzone + 2].operand, 0f) && Equals(awake[deadzone + 3].operand, 1f),
            "Look Deadzone binds with the full 0 to 1 range");
        int speed = awake.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "AutoCenterSpeed"));
        Check(speed >= 0 && Equals(awake[speed + 1].operand, 5f) &&
            Equals(awake[speed + 2].operand, .1f) && Equals(awake[speed + 3].operand, 20f),
            "Auto-Center Speed retains the original factor of 5 with a 0.1 to 20 range");
        var speedDescription = (BepInEx.Configuration.ConfigDescription)describe.Invoke(null,
            new object[] { "AutoCenterSpeed", "", null });
        var speedMenu = speedDescription.Tags.Single();
        var speedParent = (BepInEx.Configuration.ConfigDefinition)AccessTools.Field(speedMenu.GetType(), "Parent").GetValue(speedMenu);
        Check(speedParent.Section == "Other" && speedParent.Key == "AlwaysFreeLook",
            "Auto-Center Speed depends on Enable Free Look");
        Console.WriteLine("PASS: five unnumbered menu groups, option placement, target focus flag, deadzone and recenter speed");
    }

    private static void VerifyHudAlignmentContracts(Assembly plugin)
    {
        foreach (var spec in new[] {
            new { Patch = "HudCameraFollowPatch", Target = typeof(FlightHud), Method = "Update" },
            new { Patch = "HudAfterCameraPatch", Target = typeof(CameraStateManager), Method = "LateUpdate" },
            new { Patch = "HudOriginShiftPatch", Target = typeof(CameraStateManager), Method = "CheckOriginShift" },
            new { Patch = "CombatHudAfterCameraPatch", Target = typeof(CombatHUD), Method = "LateUpdate" },
            new { Patch = "LookAwayHudAfterCameraPatch", Target = typeof(HeadMountedDisplay), Method = "Update" }
        })
        {
            var patch = plugin.GetType("AceOption.Patches." + spec.Patch, true);
            var attribute = patch.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single();
            Check(attribute.info.declaringType == spec.Target && attribute.info.methodName == spec.Method &&
                AccessTools.Method(spec.Target, spec.Method)?.GetParameters().Length == 0,
                "HUD alignment hook matches installed game: " + spec.Patch);
        }
        var helper = plugin.GetType("AceOption.Helpers.HudAlignment", true);
        RuntimeHelpers.RunClassConstructor(helper.TypeHandle);
        foreach (string field in new[] { "updateHud", "updateCombatHud", "updateLookAway", "aircraft", "velocityVector", "canvas" })
            Check(AccessTools.Field(helper, field).GetValue(null) is Delegate,
                "HUD delegate/accessor initializes against installed game: " + field);

        var camera = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(CameraStateManager), "LateUpdate"), (ILGenerator)null);
        int update = camera.FindIndex(c => c.operand is MethodInfo m && m.Name == "UpdateState");
        int origin = camera.FindIndex(c => c.operand is MethodInfo m && m.Name == "CheckOriginShift");
        Check(update >= 0 && origin > update, "Final camera hook follows both camera state update and origin shifting");
        var shift = ReadMetadataInstructions(AccessTools.Method(typeof(FloatingOrigin), "OriginShift"));
        Check(shift.Any(c => c.operand is Mono.Cecil.MethodReference m && m.Name == "GetRootGameObjects") &&
            shift.Any(c => c.operand is Mono.Cecil.MethodReference m && m.Name == "set_position") &&
            !shift.Any(c => c.operand is Mono.Cecil.MethodReference m && m.DeclaringType.FullName == "UnityEngine.Canvas"),
            "Native origin shifting translates scene roots without excluding overlay canvases");
        var canvasType = AccessTools.Field(typeof(FlightHud), "canvas").FieldType;
        Check(canvasType.FullName == "UnityEngine.Canvas" &&
            AccessTools.Property(canvasType, "rootCanvas") != null &&
            AccessTools.Property(canvasType, "renderMode") != null,
            "Overlay canvas snapshot accessors match installed game and Unity APIs");
        var hud = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(FlightHud), "Update"), (ILGenerator)null);
        Check(hud.Any(c => c.operand is MethodInfo m && m.Name == "WorldToScreenPoint") &&
            hud.Any(c => c.operand is MethodInfo m && m.Name == "get_velocity"),
            "Native HUD update still provides aircraft and velocity projection");
        Check(!hud.Any(c => c.opcode == OpCodes.Stfld || c.opcode == OpCodes.Stsfld),
            "Re-evaluated native flight HUD writes only presentation properties and locals");
        var lookAway = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(HeadMountedDisplay), "Update"), (ILGenerator)null);
        int center = lookAway.FindIndex(c => c.operand is MethodInfo m && m.Name == "GetHUDCenter");
        int range = lookAway.FindIndex(c => c.operand is MethodInfo m && m.Name == "InRange");
        int visibility = lookAway.FindIndex(c => c.operand is MethodInfo m && m.Name == "SetActive");
        Check(center >= 0 && range > center && visibility > range,
            "Native look-away visibility consumes HUDCenter before range checks and activation");
        var awake = ReadMetadataInstructions(AccessTools.Method(plugin.GetType("AceOption.Plugin", true), "Awake"));
        int setting = awake.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "HudFollowsCamera"));
        Check(setting > 0 && Equals(awake[setting - 1].operand, "Chase HUD") &&
            awake[setting + 1].opcode == OpCodes.Ldc_I4_0,
            "HUD Follows Camera binds in Chase HUD with default off");
        Console.WriteLine("PASS: chase HUD/look-away ordering hooks, native projection delegates and default-off setting match the installed game");
    }

    private static void VerifyIdentity(Assembly plugin, Type pluginType)
    {
        var info = pluginType.GetCustomAttributes(typeof(BepInEx.BepInPlugin), false).Cast<BepInEx.BepInPlugin>().Single();
        Check(info.GUID == "Thabuki.NuclearOption.AceOption", "Standalone plugin GUID");
        Check(info.Name == "Ace Option", "Standalone display name");
        var version = plugin.GetName().Version;
        Check(info.Version.Major == version.Major && info.Version.Minor == version.Minor &&
            info.Version.Build == version.Build, "Plugin and assembly version match");
        Check(pluginType.GetCustomAttributes(typeof(BepInEx.BepInIncompatibility), false)
            .Cast<BepInEx.BepInIncompatibility>().Any(a => a.IncompatibilityGUID == "ChaseCamPlus"), "Conflicting plugin rejected");
        Check(pluginType.GetCustomAttributes(typeof(BepInEx.BepInIncompatibility), false)
            .Cast<BepInEx.BepInIncompatibility>().Any(a => a.IncompatibilityGUID == "Thabuki.NuclearOption.CasualQoL"), "Pre-rename plugin rejected");
        Check(!plugin.GetReferencedAssemblies().Any(a => a.Name == "ChaseCamPlus"), "No dependency on legacy mod");
        var input = plugin.GetType("AceOption.Patches.RewiredAwakePatches", true);
        var stable = input.GetMethod("StableId", BindingFlags.NonPublic | BindingFlags.Static);
        var used = new HashSet<int>();
        var expected = new[] { "ChaseCamPlus::ToggleChase", "ChaseCamPlus::TargetFocusHold", "AceOption::FireGuns" };
        var names = new HashSet<string>();
        foreach (var field in pluginType.GetFields().Where(f => f.Name.StartsWith("ACTION_")))
        {
            var name = (string)field.GetRawConstantValue();
            names.Add(name);
            uint hash = 2166136261;
            unchecked { foreach (char c in name) { hash ^= c; hash *= 16777619; } }
            int id = (int)stable.Invoke(null, new object[] { name });
            Check(id == 1000 + (int)(hash % 9000), "Stable action ID: " + name);
            Check(used.Add(id), "No built-in action ID collision: " + name);
        }
        Check(names.SetEquals(expected), "Only chase toggle, target focus and gun actions remain");
        var bindings = (Array)input.GetField("Bindings", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var registered = bindings.Cast<object>().Select(b => (string)b.GetType()
            .GetField("Name", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(b));
        Check(registered.SequenceEqual(expected), "Flight Controls registers only the three remaining actions");
    }

    private static void VerifyGunContracts(Assembly plugin)
    {
        var patch = plugin.GetType("AceOption.Patches.GunInputPatch", true);
        foreach (var parameter in AccessTools.Method(patch, "Postfix").GetParameters())
        {
            var field = AccessTools.Field(typeof(PilotPlayerState), parameter.Name.Substring(3));
            Check(field != null && field.FieldType == parameter.ParameterType,
                "Gun input Harmony field injection: " + parameter.Name);
        }
        Check(AccessTools.Method(typeof(PilotPlayerState), "PlayerControls") != null, "PlayerControls hook exists");
        Check(AccessTools.Field(typeof(WeaponManager), "gunsLinked").FieldType == typeof(bool), "Native gun-link flag exists");
        Check(AccessTools.Field(typeof(WeaponManager), "targetList").FieldType == typeof(List<Unit>), "Native targets exist");
        Check(plugin.GetType("AceOption.Patches.GunSelectionPatch") == null,
            "Dedicated gun trigger does not hook native weapon selection");
        var nativeGunFire = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(WeaponManager), "FireGuns"), (ILGenerator)null);
        foreach (string name in new[] { "TurretCount", "Ready", "Fire" })
            Check(nativeGunFire.Any(c => c.operand is MethodInfo m && m.Name == name), "Native gun firing retains " + name);
        int turretCount = nativeGunFire.FindIndex(c => c.Calls(AccessTools.Method(typeof(WeaponStation), "TurretCount")));
        Check(turretCount >= 0 && (nativeGunFire[turretCount + 1].opcode == OpCodes.Brtrue ||
            nativeGunFire[turretCount + 1].opcode == OpCodes.Brtrue_S),
            "Native linked-gun dispatcher skips turret stations handled by the custom trigger");
        var nativeStationFire = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(WeaponStation), "Fire"), (ILGenerator)null);
        Check(nativeStationFire.Any(c => c.operand is MethodInfo m && m.Name == "SafetyIsOn"), "Native station firing checks safety");
        var commands = plugin.GetType("AceOption.Helpers.GunCommands", true);
        var addedFire = PatchProcessor.GetOriginalInstructions(AccessTools.Method(commands, "Fire"), (ILGenerator)null);
        var ammoCost = AccessTools.Field(typeof(WeaponInfo), "costPerRound");
        Check(ammoCost?.FieldType == typeof(float) && addedFire.Any(c => c.opcode == OpCodes.Ldfld &&
            c.operand is FieldInfo f && f == ammoCost), "Dedicated gun choice uses the native ammunition cost per round");
        Check(!addedFire.Any(c => c.opcode == OpCodes.Ldfld && c.operand is FieldInfo f && f.Name == "currentWeaponStation"),
            "Dedicated gun choice is independent of the normally selected weapon");
        Check(addedFire.Count(c => c.Calls(AccessTools.Method(typeof(WeaponManager), "FireGuns"))) == 1,
            "Custom linked trigger delegates fixed-gun firing once");
        Check(addedFire.Count(c => c.Calls(AccessTools.Method(typeof(WeaponStation), "Ready"))) == 2 &&
            addedFire.Count(c => c.Calls(AccessTools.Method(typeof(WeaponStation), "Fire"))) == 2,
            "Linked turret and unlinked gun paths use native readiness and firing");
        var gunCheck = PatchProcessor.GetOriginalInstructions(AccessTools.Method(commands, "IsGun"), (ILGenerator)null);
        Check(!gunCheck.Any(c => c.Calls(AccessTools.Method(typeof(WeaponStation), "TurretCount"))),
            "Gun selection accepts turret-mounted guns");
        Check(!addedFire.Any(c => c.opcode == OpCodes.Stfld && c.operand is FieldInfo f && f.Name == "currentWeaponStation"),
            "Extra gun fire never changes the selected station");
        Check(!addedFire.Any(c => c.operand is MethodInfo m && new[] { "SetActiveStation", "NextWeaponStation", "PreviousWeaponStation" }.Contains(m.Name)),
            "Extra gun fire never cycles or activates another station");
        Check(!addedFire.Any(c => c.operand is MethodInfo m && new[] { "SetManual", "SetStationTargets", "SetTarget" }.Contains(m.Name)),
            "Extra gun fire preserves native turret aiming and target control");
        Console.WriteLine("PASS: gun input/selection hooks and native firing contracts match the installed game");
    }

    private static void VerifyMissileCameraContracts(Assembly plugin)
    {
        foreach (var spec in new[] {
            new { Patch = "MissileFirePatch", Target = typeof(PilotPlayerState), Method = "PlayerControls" },
            new { Patch = "MissileCameraControlPatch", Target = typeof(PilotPlayerState), Method = "PlayerAxisControls" },
            new { Patch = "MissileLaunchCameraPatch", Target = typeof(Missile), Method = "StartMissile" },
            new { Patch = "MissileCameraUpdatePatch", Target = typeof(CameraStateManager), Method = "LateUpdate" },
            new { Patch = "MissileCameraOrbitInputPatch", Target = typeof(CameraOrbitState), Method = "Inputs" },
            new { Patch = "MissileCameraSwitchPatch", Target = typeof(CameraStateManager), Method = "SwitchState" },
            new { Patch = "MissileCameraSubjectPatch", Target = typeof(CameraStateManager), Method = "SetFollowingUnit" },
            new { Patch = "MissileCameraImpactPatch", Target = typeof(CameraStateManager), Method = "Cam_OnFollowingUnitDisabled" },
            new { Patch = "MissileCameraMapPatch", Target = typeof(DynamicMap), Method = "Minimize" }
        })
        {
            var patch = plugin.GetType("AceOption.Patches." + spec.Patch, true);
            var attribute = patch.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Single().info;
            var method = AccessTools.Method(spec.Target, spec.Method);
            Check(attribute.declaringType == spec.Target && attribute.methodName == spec.Method && method != null,
                "Missile camera hook matches installed game: " + spec.Patch);
            var hook = AccessTools.Method(patch, spec.Patch == "MissileLaunchCameraPatch" ? "Postfix" : "Prefix");
            foreach (var parameter in hook.GetParameters())
            {
                if (parameter.Name.StartsWith("___"))
                    Check(AccessTools.Field(spec.Target, parameter.Name.Substring(3))?.FieldType == parameter.ParameterType,
                        "Missile camera field injection: " + parameter.Name);
                else if (parameter.Name == "__instance")
                    Check(parameter.ParameterType == spec.Target, "Missile camera instance injection");
                else if (parameter.Name == "__0")
                    Check(method.GetParameters()[0].ParameterType == parameter.ParameterType, "Missile impact argument type");
                else
                    Check(method.GetParameters().Any(p => p.Name == parameter.Name && p.ParameterType == parameter.ParameterType),
                        "Missile camera native argument: " + parameter.Name);
            }
        }
        var lifecycle = plugin.GetType("AceOption.Patches.MissileCameraLifecyclePatch", true);
        var lifecycleMethods = ((IEnumerable<MethodBase>)AccessTools.Method(lifecycle, "TargetMethods").Invoke(null, null)).ToList();
        Check(lifecycleMethods.Count == 2 && lifecycleMethods.All(m => m != null && m.DeclaringType == typeof(PilotPlayerState)),
            "Missile camera lifecycle hooks exist");

        var start = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Missile), "StartMissile"), (ILGenerator)null);
        Check(start.Any(c => c.Calls(AccessTools.PropertySetter(typeof(Missile), "owner"))) &&
            start.Any(c => c.operand is MethodInfo m && m.Name == "RegisterUnit"),
            "Missile spawn observation follows native owner and unit initialization");
        var network = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Missile), "OnStartNetwork"), (ILGenerator)null);
        Check(network.Any(c => c.Calls(AccessTools.Method(typeof(Missile), "StartMissile"))),
            "Network missile startup reaches the observed method");

        var helper = plugin.GetType("AceOption.Helpers.MissileCamera", true);
        var fire = ReadMetadataInstructions(AccessTools.Method(helper, "Fire"));
        Check(fire.Count(c => c.operand is Mono.Cecil.MethodReference m && m.DeclaringType.Name == "WeaponStation" && m.Name == "LaunchMount") == 1,
            "Missile press uses one native mount launch site");
        foreach (string name in new[] { "SafetyIsOn", "Ready", "GetTargetList" })
            Check(fire.Any(c => c.operand is Mono.Cecil.MethodReference m && m.Name == name), "Missile input retains native " + name);
        Check(!fire.Any(c => c.operand is Mono.Cecil.MethodReference m && m.Name == "SalvoFire"), "Single missile launch cannot enter a native salvo");
        Check(fire.Any(c => c.operand is Mono.Cecil.MethodReference m && m.DeclaringType.Name == "Pilot" && m.Name == "Fire"),
            "Other weapons retain native Pilot.Fire");
        var mount = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(WeaponStation), "LaunchMount"), (ILGenerator)null);
        Check(mount.Any(c => c.operand is MethodInfo m && m.DeclaringType == typeof(Weapon) && m.Name == "Fire") &&
            mount.Any(c => c.operand is MethodInfo m && m.Name == "UpdateLastFired"),
            "Native mount launch retains weapon dispatch and station accounting");
        var mountedFire = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(MountedMissile), "Fire"), (ILGenerator)null);
        foreach (string name in new[] { "RpcLaunchMissile", "CmdLaunchMissile", "RailLaunch" })
            Check(mountedFire.Any(c => c.operand is MethodInfo m && m.Name == name), "Native mounted missile retains " + name);
        var switchState = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(CameraStateManager), "SwitchState"), (ILGenerator)null);
        Check(switchState.Any(c => c.Calls(AccessTools.Method(typeof(DynamicMap), "Minimize"))),
            "Native camera restoration reaches the scoped map-preservation hook");
        var cameraCommands = plugin.GetType("AceOption.Helpers.CameraCommands", true);
        var poll = PatchProcessor.GetOriginalInstructions(AccessTools.Method(cameraCommands, "Poll"), (ILGenerator)null);
        Check(poll.Any(c => c.Calls(AccessTools.Method(helper, "BeforeSwitch"))),
            "Chase toggle returns from missile follow before changing aircraft view");

        var awake = ReadMetadataInstructions(AccessTools.Method(plugin.GetType("AceOption.Plugin", true), "Awake"));
        int setting = awake.FindIndex(c => c.opcode == OpCodes.Ldstr && Equals(c.operand, "MissileFollowCamera"));
        Check(setting > 0 && Equals(awake[setting - 1].operand, "Combat") && awake[setting + 1].opcode == OpCodes.Ldc_I4_0,
            "Missile camera binds in Combat with default off");
        Console.WriteLine("PASS: missile input/camera hooks, single-launch routing and default-off setting match the installed game");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
}
