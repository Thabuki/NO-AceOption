using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class GroundBrakeCommands
{
    private const float ActivationDelay = 1f;
    private static Pilot owner;
    private static Player input;
    private static Aircraft aircraft;
    private static ControlInputs borrowedInputs;
    private static float previousBrake;
    private static float heldSince;

    internal static bool Held(Pilot pilot, Player player, float strength) =>
        Plugin.ThrottleDownGroundBrakes.Value && CameraCommands.InputAllowed &&
        Application.isFocused &&
        !NuclearOption.UI.LeaderboardMenu.IsOpen() && player != null &&
        pilot != null && !pilot.dead && pilot.currentState == pilot.playerState &&
        strength >= .2f && pilot.aircraft != null && !pilot.aircraft.remoteSim &&
        GameManager.IsLocalAircraft(pilot.aircraft) && pilot.aircraft.cockpit != null &&
        !pilot.aircraft.cockpit.IsDetached() && !player.GetButton("Axis Modifier") &&
        !player.GetButton("Throttle") && player.GetNegativeButton("Throttle");

    internal static void Apply(Pilot pilot, Player player, float strength,
        ControlInputs controls, bool grounded)
    {
        RestoreBrake();
        // Count only time with the current native throttle at zero, regardless
        // of when the button/trigger first crossed its pressed threshold.
        if (controls == null || controls.throttle != 0f || !grounded || !Held(pilot, player, strength))
        {
            Reset();
            return;
        }
        if (owner != pilot || input != player || aircraft != pilot.aircraft)
        {
            owner = pilot;
            input = player;
            aircraft = pilot.aircraft;
            heldSince = Time.time;
        }
        // Both frame and physics controls poll this helper. Use elapsed game
        // time so repeated polls cannot shorten the required ground hold.
        if (Time.time - heldSince < ActivationDelay) return;
        borrowedInputs = controls;
        previousBrake = controls.brake;
        controls.brake = 1f;
    }

    // Native control updates can stop in menus or on pause. Remove only our
    // contribution, retaining the native brake input captured before applying it.
    internal static void CheckInterrupted()
    {
        if (owner == null || owner.aircraft != aircraft || !Held(owner, input, 1f)) Reset();
    }

    internal static void RestoreBrake()
    {
        if (borrowedInputs != null && borrowedInputs.brake == 1f)
            borrowedInputs.brake = previousBrake;
        borrowedInputs = null;
        previousBrake = 0;
    }

    internal static void Reset()
    {
        RestoreBrake();
        owner = null;
        input = null;
        aircraft = null;
        heldSince = 0;
    }
}
