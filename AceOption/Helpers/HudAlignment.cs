using System;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Helpers;

internal static class HudAlignment
{
    private static readonly Action<FlightHud> updateHud =
        AccessTools.MethodDelegate<Action<FlightHud>>(AccessTools.Method(typeof(FlightHud), "Update"));
    private static readonly Action<CombatHUD> updateCombatHud =
        AccessTools.MethodDelegate<Action<CombatHUD>>(AccessTools.Method(typeof(CombatHUD), "LateUpdate"));
    private static readonly Action<HeadMountedDisplay> updateLookAway =
        AccessTools.MethodDelegate<Action<HeadMountedDisplay>>(AccessTools.Method(typeof(HeadMountedDisplay), "Update"));
    private static readonly AccessTools.FieldRef<FlightHud, Aircraft> aircraft =
        AccessTools.FieldRefAccess<FlightHud, Aircraft>("aircraft");
    private static readonly AccessTools.FieldRef<FlightHud, UnityEngine.UI.Image> velocityVector =
        AccessTools.FieldRefAccess<FlightHud, UnityEngine.UI.Image>("velocityVector");
    private static readonly AccessTools.FieldRef<FlightHud, Canvas> canvas =
        AccessTools.FieldRefAccess<FlightHud, Canvas>("canvas");
    private static int cameraFrame = -1, deferredFrame = -1, lookAwayFrame = -1;
    private static CombatHUD deferredCombatHud;
    private static HeadMountedDisplay deferredLookAway;

    private static bool OwnChaseHud(FlightHud hud, CameraStateManager cam) =>
        Plugin.FlightHud.Value && hud != null && hud == SceneSingleton<FlightHud>.i &&
        hud.isActiveAndEnabled && cam != null && cam.isActiveAndEnabled &&
        cam.currentState == cam.chaseState && cam.mainCamera != null && cam.followingUnit != null &&
        aircraft(hud) == cam.followingUnit && GameManager.IsLocalAircraft(cam.followingUnit);

    // FloatingOrigin translates every scene root. Screen-space overlay canvases
    // are restored by Unity for rendering, but our late world-to-screen writes
    // must already use that restored coordinate frame. Otherwise the subsequent
    // canvas restoration moves the HUD and weapon cues a second time.
    internal readonly struct OverlayPosition
    {
        private readonly Transform root;
        private readonly Vector3 position;
        internal OverlayPosition(Canvas owner)
        {
            var overlay = owner != null ? owner.rootCanvas : null;
            root = overlay != null && overlay.renderMode == RenderMode.ScreenSpaceOverlay ? overlay.transform : null;
            position = root != null ? root.position : default;
        }
        internal void Restore()
        {
            if (root != null && root.position != position) root.position = position;
        }
    }

    internal readonly struct OriginShiftState
    {
        private readonly OverlayPosition flight, combat, lookAway;
        internal OriginShiftState(FlightHud hud, CombatHUD combatHud, HeadMountedDisplay display)
        {
            flight = new OverlayPosition(canvas(hud));
            combat = new OverlayPosition(combatHud != null ? combatHud.GetComponentInParent<Canvas>(true) : null);
            lookAway = new OverlayPosition(display != null ? display.GetComponentInParent<Canvas>(true) : null);
        }
        internal void Restore() { flight.Restore(); combat.Restore(); lookAway.Restore(); }
    }

    internal static OriginShiftState BeforeOriginShift(CameraStateManager cam)
    {
        var hud = SceneSingleton<FlightHud>.i;
        return OwnChaseHud(hud, cam)
            ? new OriginShiftState(hud, SceneSingleton<CombatHUD>.i, SceneSingleton<HeadMountedDisplay>.i)
            : default;
    }

    internal static void AfterCamera(CameraStateManager cam)
    {
        var hud = SceneSingleton<FlightHud>.i;
        var deferred = deferredCombatHud;
        var lookAway = deferredLookAway;
        deferredCombatHud = null;
        deferredLookAway = null;
        if (OwnChaseHud(hud, cam))
        {
            cameraFrame = Time.frameCount;
            // Native FlightHud.Update projects against the camera before its LateUpdate.
            // Re-evaluate presentation after the final chase pose and floating-origin shift.
            // Keep the native attitude, velocity-vector and FOV calculations in both modes.
            updateHud(hud);
        }
        // HMD visibility depends on HUDCenter. Reading it in Update races the
        // early flight projection and sees a layout that changes again before render.
        if (lookAway != null && lookAwayFrame == Time.frameCount && lookAway.isActiveAndEnabled)
            updateLookAway(lookAway);
        // Combat HUD places world-space weapon cues under HUDCenter. Run it once
        // after both camera and layout, so moving the parent cannot shift those cues.
        if (deferred != null && deferredFrame == Time.frameCount && deferred.isActiveAndEnabled)
            updateCombatHud(deferred);
    }

    internal static bool BeforeCombat(CombatHUD hud)
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        if (!OwnChaseHud(SceneSingleton<FlightHud>.i, cam) || hud == null ||
            hud.aircraft != cam.followingUnit || cameraFrame == Time.frameCount) return true;
        deferredCombatHud = hud;
        deferredFrame = Time.frameCount;
        return false;
    }

    internal static bool BeforeLookAway(HeadMountedDisplay display)
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        var combat = SceneSingleton<CombatHUD>.i;
        if (!OwnChaseHud(SceneSingleton<FlightHud>.i, cam) || display == null ||
            combat == null || combat.aircraft != cam.followingUnit || cameraFrame == Time.frameCount) return true;
        deferredLookAway = display;
        lookAwayFrame = Time.frameCount;
        return false;
    }

    internal static void Reset()
    {
        cameraFrame = deferredFrame = lookAwayFrame = -1;
        deferredCombatHud = null;
        deferredLookAway = null;
    }

    internal static void FollowCamera(FlightHud hud)
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        if (!Plugin.HudFollowsCamera.Value || !OwnChaseHud(hud, cam)) return;
        var center = hud.GetHUDCenter();
        if (center == null) return;
        // The velocity cue is already projected in screen space by native Update.
        // Preserve it when its HUD parent is recentered or leveled.
        var velocity = velocityVector(hud);
        var velocityPosition = velocity != null ? velocity.transform.position : default;
        // Use the camera's viewport, including its pixel rect, rather than Screen size.
        center.position = cam.mainCamera.ViewportToScreenPoint(new Vector3(.5f, .5f, 0));
        center.rotation = Quaternion.identity;
        if (velocity != null) velocity.transform.position = velocityPosition;
    }
}
