using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using AceOption.Helpers;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Patches;

internal static class CameraChaseStatePatches
{
    private static readonly FreeLookOrbit Orbit = new FreeLookOrbit();
    private static float pan, tilt;
    private static bool reportedLeave;
    private static void ResetOrbit() { Orbit.Reset(); pan = tilt = 0; }

    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.EnterState))]
    private static class EnterState
    {
        private static void Postfix()
        {
            ResetOrbit(); ChasePlacement.Reset(); TargetFocus.Reset(); HudAlignment.Reset();
        }
    }
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.LeaveState))]
    private static class LeaveState
    {
        private static Exception Finalizer(Exception __exception, CameraStateManager cam)
        {
            ChasePlacement.Reset(); TargetFocus.Reset(); HudAlignment.Reset(); HudPresentation.Release();
            if (!(__exception is NullReferenceException) || cam == null || cam.followingUnit != null) return __exception;
            if (!reportedLeave) Plugin.Logger.LogInfo("Ignored vanilla chase exit dereference after followed unit was cleared.");
            reportedLeave = true;
            return null;
        }
    }
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.CheckHUD))]
    private static class CheckHUD
    {
        private static void Prefix(ref bool ___showHUD) { if (Plugin.FlightHud.Value) ___showHUD = true; }
    }
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.UpdateState))]
    private static class UpdateState
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = InstructionEdits.Read(instructions);
            var query = AccessTools.Method(typeof(Rewired.Player), "GetButtonDown", new[] { typeof(string) });
            var sites = InstructionEdits.Find(codes, i => i > 0 && codes[i-1].opcode == OpCodes.Ldstr &&
                Equals(codes[i-1].operand, "Center") && codes[i].Calls(query));
            if (InstructionEdits.Expect("Chase Center", sites.Count, 1))
                InstructionEdits.Call(codes, sites, AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.ChaseCenterSwitchRequested)));
            return codes;
        }
        private static bool Prefix(CameraStateManager cam)
        {
            if (cam == null || cam.followingUnit == null) { ChasePlacement.Reset(); TargetFocus.Reset(); return false; }
            ChasePlacement.RestoreForUpdate(cam);
            return true;
        }
        private static void Postfix(CameraStateManager cam)
        {
            if (cam == null || cam.currentState != cam.chaseState) return;
            HudPresentation.Refresh();
            if (cam.followingUnit == null || cam.cameraPivot == null) return;
            ChasePlacement.Capture(cam);
            ChasePlacement.ApplyDistance(cam);
            AvoidTerrain(cam);

            bool input = CameraCommands.InputAllowed;
            float dt = input ? Mathf.Max(0, Time.unscaledDeltaTime) : 0;
            bool automatic = Plugin.AlwaysFreeLook.Value;
            var player = GameManager.playerInput;
            Unit target = TargetFocus.GetTarget(cam, input && CameraCommands.Held(Plugin.TargetFocusActionId), dt, Plugin.TargetFocusHoldDelay.Value);
            bool focus = target != null || TargetFocus.Active;
            if (input && automatic && player.GetButtonDown("Center"))
            {
                ResetOrbit(); TargetFocus.Center(); target = null;
            }
            else if (!focus && input && automatic)
                Orbit.Update(player.GetAxis("Pan View"), player.GetAxis("Tilt View"),
                    120 * PlayerSettings.viewSensitivity * Plugin.FreeLookSensitivity.Value,
                    PlayerSettings.viewInvertPitch ? -1 : 1, dt,
                    Plugin.AutoCenterDelay.Value, Plugin.FreeLookDeadzone.Value);
            else if (!focus && input && !automatic) Orbit.Reset();

            float smoothing = Orbit.GetSmoothing(dt, PlayerSettings.viewSmoothing, Plugin.AutoCenterSpeed.Value);
            pan = Mathf.Lerp(pan, Orbit.Pan, smoothing);
            tilt = Mathf.Lerp(tilt, Orbit.Tilt, smoothing);
            Vector3 pivot = cam.cameraPivot.position;
            Vector3 boom = cam.transform.position - pivot;
            if ((Mathf.Abs(pan) >= .01f || Mathf.Abs(tilt) >= .01f) && boom.sqrMagnitude >= .0001f)
            {
                var rotation = Quaternion.AngleAxis(pan, cam.followingUnit.transform.up) *
                    Quaternion.AngleAxis(tilt, cam.transform.right);
                cam.transform.position = pivot + rotation * boom;
                cam.transform.rotation = rotation * cam.transform.rotation;
                AvoidTerrain(cam);
            }
            ChasePlacement.ApplyFraming(cam);
            if (TargetFocus.Apply(cam, target, dt))
            {
                if (target != null) TargetMark.FocusActivated();
                AvoidTerrain(cam);
            }
        }
    }

    private static void AvoidTerrain(CameraStateManager cam)
    {
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 inward = pivot - cam.transform.position;
        if (inward.sqrMagnitude < .0001f || !Physics.Linecast(pivot, pivot - inward * 2, out RaycastHit hit, PhysicsLayers.StaticsMask)) return;
        Vector3 safe = hit.point + inward.normalized / Mathf.Max(.1f, Vector3.Dot(inward.normalized, hit.normal));
        if ((safe - pivot).sqrMagnitude < inward.sqrMagnitude) cam.transform.position = safe;
    }
}
