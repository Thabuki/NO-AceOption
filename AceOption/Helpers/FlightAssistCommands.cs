using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class FlightAssistCommands
{
    private static Pilot owner;
    private static Player input;
    private static Aircraft borrowedAircraft;
    private static Aircraft reportingAircraft;
    private static bool previousAssist;

    private static bool Allowed(Pilot pilot, Player player, float strength) =>
        Plugin.ThrottleChordFlightAssist.Value && CameraCommands.InputAllowed &&
        Application.isFocused &&
        !NuclearOption.UI.LeaderboardMenu.IsOpen() && player != null &&
        pilot != null && !pilot.dead && pilot.currentState == pilot.playerState &&
        strength >= .2f && pilot.aircraft != null && !pilot.aircraft.remoteSim &&
        GameManager.IsLocalAircraft(pilot.aircraft) && pilot.aircraft.cockpit != null &&
        !pilot.aircraft.cockpit.IsDetached();

    private static bool BothHeld(Player player) => !player.GetButton("Axis Modifier") &&
        // Rewired tracks the two poles independently, even when GetAxisRaw is zero.
        // Its mapping, modifier, enabled-map and axis-button thresholds still apply.
        player.GetButton("Throttle") && player.GetNegativeButton("Throttle");

    internal static bool ActiveFor(Pilot pilot) => borrowedAircraft != null &&
        owner == pilot && pilot != null && pilot.aircraft == borrowedAircraft;

    internal static bool HeldFor(Aircraft aircraft) => aircraft != null && borrowedAircraft == aircraft &&
        owner != null && owner.aircraft == aircraft && Allowed(owner, input, 1f) && BothHeld(input);

    // The native filter's report is the only call suppressed. Keep the native
    // setter and its aircraft/HUD events, including when restoring on release.
    internal static bool SuppressReportFor(Aircraft aircraft) => aircraft != null &&
        (borrowedAircraft == aircraft || reportingAircraft == aircraft);

    internal static void Report(AircraftActionsReport report, string text, float duration, Aircraft aircraft)
    {
        if (!SuppressReportFor(aircraft)) report.ReportText(text, duration);
    }

    private static void SetAssist(Aircraft aircraft, bool enabled)
    {
        var previous = reportingAircraft;
        reportingAircraft = aircraft;
        try { aircraft.SetFlightAssist(enabled); }
        finally { reportingAircraft = previous; }
    }

    internal static void Poll(Pilot pilot, Player player, float strength)
    {
        if (!Allowed(pilot, player, strength) || !BothHeld(player))
        {
            Reset();
            return;
        }
        var aircraft = pilot.aircraft;
        if (borrowedAircraft != aircraft || owner != pilot || input != player)
        {
            Reset();
            var filter = aircraft.GetControlsFilter();
            if (filter == null || !filter.HasFlightAssist()) return;
            owner = pilot;
            input = player;
            borrowedAircraft = aircraft;
            previousAssist = aircraft.flightAssist;
        }
        if (aircraft.flightAssist) SetAssist(aircraft, false);
    }

    // Fixed updates stop on pause. Also restore if an aircraft disappears or a
    // menu opens between control updates, rather than leaving assist disabled.
    internal static void CheckInterrupted()
    {
        if (owner == null || !Allowed(owner, input, 1f) ||
            owner.aircraft != borrowedAircraft || !BothHeld(input)) Reset();
    }

    internal static void Reset()
    {
        var aircraft = borrowedAircraft;
        bool restore = previousAssist;
        owner = null;
        input = null;
        borrowedAircraft = null;
        previousAssist = false;
        // Use the native setter so HUD reports and aircraft automation receive
        // the same events as the game's own flight-assist control.
        if (aircraft != null && aircraft.flightAssist != restore)
            SetAssist(aircraft, restore);
    }
}
