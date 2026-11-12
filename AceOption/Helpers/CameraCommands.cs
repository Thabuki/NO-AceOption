using UnityEngine;

namespace AceOption.Helpers;

internal static class CameraCommands
{
    internal static bool InputAllowed
    {
        get
        {
            var picker = SceneSingleton<CameraControlUI>.i;
            // Menus can leave flight controls enabled, including while a multiplayer game keeps running.
            return Time.timeScale > 0 && !Cursor.visible &&
                GameManager.playerInput != null && GameManager.flightControlsEnabled &&
                !DynamicMap.mapMaximized && !RadialMenuMain.IsInUse() && (picker == null || !picker.isOpen);
        }
    }

    internal static bool Held(int id) => id >= 0 && GameManager.playerInput != null && GameManager.playerInput.GetButton(id);
    private static bool Pressed(int id) => id >= 0 && GameManager.playerInput.GetButtonDown(id);
    private static bool CockpitAvailable(Unit unit) => unit is Aircraft a && a.pilots != null &&
        a.pilots.Length > 0 && a.pilots[0] != null && !a.pilots[0].dead;

    internal static void Poll()
    {
        var cam = SceneSingleton<CameraStateManager>.i;
        if (!InputAllowed || cam == null) return;
        if (Pressed(Plugin.ToggleActionId))
        {
            MissileCamera.BeforeSwitch(cam);
            if (cam.currentState == cam.cockpitState && cam.followingUnit is Aircraft) cam.SwitchState(cam.chaseState);
            else if (CockpitAvailable(cam.followingUnit)) cam.SwitchState(cam.cockpitState);
        }
    }
}
