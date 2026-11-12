namespace AceOption.Helpers;

internal enum FlightCameraView { Cockpit, Chase }

internal static class CameraPreference
{
    private static bool CanRemember(CameraStateManager cam) => cam != null &&
        cam.followingUnit is Aircraft aircraft && GameManager.IsLocalAircraft(aircraft) &&
        !aircraft.disabled && !aircraft.remoteSim && aircraft.cockpit != null &&
        !aircraft.cockpit.IsDetached() && aircraft.pilots != null && aircraft.pilots.Length > 0 &&
        aircraft.pilots[0] != null && !aircraft.pilots[0].dead &&
        aircraft.pilots[0].currentState == aircraft.pilots[0].playerState;

    // Replace only the initial cockpit selection, after native local-player/HUD setup.
    // SetFollowingUnit's temporary orbit view must never overwrite the preference.
    public static CameraBaseState InitialState(CameraStateManager cam) =>
        Plugin.RememberCameraView.Value && CanRemember(cam) &&
        Plugin.LastCameraView.Value == FlightCameraView.Chase ? cam.chaseState : cam.cockpitState;

    internal static void Remember(CameraStateManager cam)
    {
        if (!Plugin.RememberCameraView.Value || !CanRemember(cam)) return;
        FlightCameraView view;
        if (cam.currentState == cam.cockpitState) view = FlightCameraView.Cockpit;
        else if (cam.currentState == cam.chaseState) view = FlightCameraView.Chase;
        else return;

        // ConfigEntry saves changes to disk, so the choice survives game restarts.
        if (Plugin.LastCameraView.Value != view) Plugin.LastCameraView.Value = view;
    }
}
