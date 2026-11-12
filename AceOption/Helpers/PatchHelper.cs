using UnityEngine;

namespace AceOption.Helpers;

internal static class PatchHelper
{
    public static CameraMode EffectiveCameraMode() => CameraStateManager.cameraMode == CameraMode.chase &&
        Plugin.VirtualJoystick.Value && !Plugin.AlwaysFreeLook.Value && !TargetFocus.Active
        ? CameraMode.cockpit : CameraStateManager.cameraMode;

    public static float CorrectDefaultFoV()
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        return !Plugin.FixWeaponFoV.Value || cam == null || cam.currentState == cam.cockpitState
            ? PlayerSettings.defaultFoV : PlayerSettings.defaultExternalFoV;
    }
    public static CameraBaseState AimAllowedState(CameraStateManager cam) => cam == null ? null :
        cam.currentState == cam.chaseState && GameManager.IsLocalAircraft(cam.followingUnit)
            ? cam.currentState : cam.cockpitState;
    public static Vector3 AimDirection(Transform view)
    {
        if (view == null) return Vector3.forward;
        var cam = SceneSingleton<CameraStateManager>.i;
        if (cam != null && cam.currentState == cam.chaseState && !Plugin.TurretAim.Value &&
            GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null) return aircraft.transform.forward;
        return view.forward;
    }
    public static bool ChaseCenterSwitchRequested(Rewired.Player player, string action) =>
        !Plugin.AlwaysFreeLook.Value && player.GetButtonDown(action);
}
