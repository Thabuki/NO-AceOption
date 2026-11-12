using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class AutoLevelCommands
{
    // Only presentation remembers the last successful application. Recovery
    // commands remain stateless; there is no aircraft state to restore.
    private static Pilot owner;
    private static Player input;
    private static Aircraft activeAircraft;
    private static float pilotStrength;

    internal static void Apply(Pilot pilot, Player player, float strength, ControlInputs inputs)
    {
        Reset();
        if (inputs == null || !TryInputs(pilot, player, strength, out float pitch, out float roll)) return;
        inputs.pitch = pitch;
        inputs.roll = roll;
        inputs.yaw = 0;
        // Discard any pre-chord rudder tail so release starts a fresh button ramp.
        YawCommands.Reset(player);
        owner = pilot;
        input = player;
        activeAircraft = pilot.aircraft;
        pilotStrength = strength;
    }

    internal static bool ActiveFor(Aircraft aircraft) => aircraft != null && activeAircraft == aircraft &&
        owner != null && owner.aircraft == aircraft && TryInputs(owner, input, pilotStrength, out _, out _);

    internal static void CheckInterrupted()
    {
        if (activeAircraft != null && !ActiveFor(activeAircraft)) Reset();
    }

    internal static void Reset() { owner = null; input = null; activeAircraft = null; pilotStrength = 0; }

    private static bool TryInputs(Pilot pilot, Player player, float strength, out float pitch, out float roll)
    {
        pitch = roll = 0;
        if (!Plugin.YawChordAutoLevel.Value || !CameraCommands.InputAllowed ||
            !Application.isFocused || NuclearOption.UI.LeaderboardMenu.IsOpen() ||
            player == null || pilot == null || pilot.dead ||
            pilot.currentState != pilot.playerState || strength < .2f) return false;
        var aircraft = pilot.aircraft;
        if (aircraft == null || aircraft.remoteSim || !GameManager.IsLocalAircraft(aircraft) ||
            // Native CheckRadarAlt uses the same ground-relative threshold.
            !(aircraft.radarAlt > .2f) || aircraft.cockpit == null || aircraft.cockpit.IsDetached() ||
            aircraft.GetControlsFilter() is HeloControlsFilter || aircraft.IsAutoHoverEnabled()) return false;
        // Read both poles, not GetAxis: opposite buttons may cancel, or Rewired
        // may favor the most recent one. Existing mappings/thresholds still apply.
        if (!player.GetButton("Yaw") || !player.GetNegativeButton("Yaw")) return false;
        var body = aircraft.CockpitRB();
        if (body == null) return false;
        var inverse = Quaternion.Inverse(body.rotation);
        var up = inverse * Vector3.up;
        var rates = inverse * body.angularVelocity;
        return HorizonLevel.TryInputs(up.x, up.y, up.z, rates.x, rates.z, out pitch, out roll);
    }
}
