using System;
using AceOption.Helpers;

// Exercises ownership, release and interruption using stand-ins. Real Rewired
// bindings, Unity events, aircraft handling and networking need flight testing.
internal static class FlightAssistInputTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    private static Pilot Pilot(bool assist = true)
    {
        var pilot = new Pilot { aircraft = new Aircraft { flightAssist = assist } };
        pilot.currentState = pilot.playerState;
        return pilot;
    }

    public static void Main()
    {
        var pilot = Pilot();
        var aircraft = pilot.aircraft;
        var player = new Rewired.Player();
        Action poll = () => FlightAssistCommands.Poll(pilot, player, 1f);
        poll();
        Check(aircraft.flightAssist, "No input leaves assist enabled");
        player.Increase = true; poll();
        Check(aircraft.flightAssist, "Increase alone keeps assist enabled");
        player.Increase = false; player.Decrease = true; poll();
        Check(aircraft.flightAssist, "Decrease alone keeps assist enabled");
        player.Increase = true; poll();
        Check(!aircraft.flightAssist && FlightAssistCommands.ActiveFor(pilot),
            "Opposing held poles disable assist, including a cancelled throttle axis");
        Check(aircraft.SetCalls == 1, "Entering the chord uses the native setter once");
        Check(FlightAssistCommands.HeldFor(aircraft), "Held indicator belongs to the active aircraft");
        Check(!FlightAssistCommands.HeldFor(new Aircraft()), "Held indicator never follows a different aircraft");
        Check(aircraft.Reports == 0, "Shortcut activation does not queue a native assist notification");
        poll(); FlightAssistCommands.CheckInterrupted(); poll();
        Check(aircraft.SetCalls == 1, "Holding does not spam native reports/events");
        player.Increase = false; poll();
        Check(aircraft.flightAssist && !FlightAssistCommands.ActiveFor(pilot), "Releasing increase restores assist");
        Check(!FlightAssistCommands.HeldFor(aircraft) && aircraft.Reports == 0,
            "Release removes the indicator and silently restores assist");
        Check(!FlightAssistCommands.SuppressReportFor(aircraft), "Release ends report suppression");
        Check(aircraft.SetCalls == 2, "Release sends one native restore event");
        player.Increase = true; poll(); player.Decrease = false; poll();
        Check(aircraft.flightAssist, "Releasing decrease also restores assist");

        aircraft.flightAssist = false; player.Decrease = true;
        int calls = aircraft.SetCalls;
        poll();
        Check(FlightAssistCommands.ActiveFor(pilot), "An already-disabled assist still recognizes the chord");
        player.Increase = false; poll();
        Check(!aircraft.flightAssist && aircraft.SetCalls == calls, "Already-disabled assist stays off without extra events");
        aircraft.flightAssist = true; player.Increase = true;

        Interrupt(poll, pilot, () => player.Increase = false, () => player.Increase = true, "Release between control updates");
        Interrupt(poll, pilot, () => CameraCommands.InputAllowed = false, () => CameraCommands.InputAllowed = true, "Menus/map/pause");
        Interrupt(poll, pilot, () => UnityEngine.Application.isFocused = false, () => UnityEngine.Application.isFocused = true, "Focus loss");
        Interrupt(poll, pilot, () => NuclearOption.UI.LeaderboardMenu.Open = true, () => NuclearOption.UI.LeaderboardMenu.Open = false, "Leaderboard");
        Interrupt(poll, pilot, () => AceOption.Plugin.ThrottleChordFlightAssist.Value = false,
            () => AceOption.Plugin.ThrottleChordFlightAssist.Value = true, "Live config disable");
        Interrupt(poll, pilot, () => player.Modifier = true, () => player.Modifier = false, "Axis Modifier");
        Interrupt(poll, pilot, () => pilot.dead = true, () => pilot.dead = false, "Death");
        Interrupt(poll, pilot, () => pilot.currentState = new object(), () => pilot.currentState = pilot.playerState, "Ejection/state change");
        Interrupt(poll, pilot, () => aircraft.cockpit.Detached = true, () => aircraft.cockpit.Detached = false, "Cockpit detachment");
        Interrupt(poll, pilot, () => aircraft.Local = false, () => aircraft.Local = true, "Loss of local ownership");
        Interrupt(poll, pilot, () => aircraft.remoteSim = true, () => aircraft.remoteSim = false, "Remote simulation");

        poll();
        FlightAssistCommands.Poll(pilot, player, .19f);
        Check(aircraft.flightAssist, "Blackout control loss restores assist");
        poll(); FlightAssistCommands.Reset();
        Check(aircraft.flightAssist, "Lifecycle/unload reset restores assist");
        FlightAssistCommands.Reset();
        Check(!FlightAssistCommands.ActiveFor(pilot), "Repeated reset clears ownership safely");

        poll(); calls = aircraft.Reports; aircraft.SetFlightAssist(true); poll();
        Check(!aircraft.flightAssist, "Assist remains off while both controls stay held, including after native toggle");
        Check(aircraft.Reports == calls, "Native toggle during the chord cannot enqueue extra assist reports");
        player.Decrease = false; poll();
        Check(aircraft.flightAssist, "Native toggle during hold does not replace the pre-hold state");
        player.Decrease = true; poll();
        var replacement = new Aircraft { flightAssist = false };
        pilot.aircraft = replacement; FlightAssistCommands.CheckInterrupted();
        Check(aircraft.flightAssist && !replacement.flightAssist, "Aircraft change restores only the borrowed aircraft");
        poll();
        Check(FlightAssistCommands.ActiveFor(pilot) && !replacement.flightAssist, "New aircraft starts with its own prior state");
        FlightAssistCommands.Reset();
        Check(!replacement.flightAssist, "Old aircraft's enabled state never leaks into a new aircraft");
        pilot.aircraft = aircraft;

        poll();
        var otherPilot = Pilot(false);
        FlightAssistCommands.Poll(otherPilot, new Rewired.Player { Increase = true, Decrease = true }, 1f);
        Check(aircraft.flightAssist && !otherPilot.aircraft.flightAssist, "Changing pilot restores the old aircraft and borrows the new one");
        FlightAssistCommands.Reset();
        Check(!otherPilot.aircraft.flightAssist, "Changing pilot preserves the new aircraft's disabled state");

        aircraft.Filter.Supported = false; calls = aircraft.SetCalls; poll();
        Check(aircraft.flightAssist && !FlightAssistCommands.ActiveFor(pilot) && aircraft.SetCalls == calls,
            "Aircraft without flight assist are untouched");
        aircraft.Filter = null; poll();
        Check(aircraft.flightAssist && !FlightAssistCommands.ActiveFor(pilot), "Missing filter is safe");
        aircraft.Filter = new ControlsFilter();
        FlightAssistCommands.Poll(null, player, 1f);
        FlightAssistCommands.Poll(pilot, null, 1f);
        Check(aircraft.flightAssist, "Missing player/pilot is safe");
        poll(); pilot.aircraft = null; FlightAssistCommands.CheckInterrupted();
        Check(aircraft.flightAssist, "Aircraft loss restores the previous state");
        FlightAssistCommands.CheckInterrupted();
        Check(!FlightAssistCommands.ActiveFor(null), "Interrupted state does not retain ownership");

        var manualAircraft = new Aircraft();
        manualAircraft.SetFlightAssist(false);
        Check(manualAircraft.Reports == 1, "Ordinary assist changes still use the native message feed");
        pilot.aircraft = aircraft; player.Increase = player.Decrease = true;
        poll();
        Check(!FlightAssistCommands.SuppressReportFor(manualAircraft), "An active chord does not suppress another aircraft's reports");
        manualAircraft.SetFlightAssist(true);
        Check(manualAircraft.Reports == 2, "Another aircraft's assist report still reaches the native feed during the hold");
        aircraft.ActionReport.ReportText("Gear down", 5);
        Check(aircraft.Reports == 1, "Unrelated action messages still reach the native feed during the hold");
        player.Increase = false;
        Check(!FlightAssistCommands.HeldFor(aircraft), "Indicator notices release before the next control update");
        FlightAssistCommands.CheckInterrupted();

        // A failed native setter must never leave notification suppression stuck on.
        player.Increase = true; aircraft.ThrowOnSet = true;
        try { poll(); } catch (InvalidOperationException) { }
        aircraft.ThrowOnSet = false;
        FlightAssistCommands.Reset();
        Check(!FlightAssistCommands.SuppressReportFor(aircraft), "Setter exceptions cannot leak report suppression");
        Console.WriteLine("PASS: " + checks + " throttle-chord assist/ownership/reset assertions");
    }

    private static void Interrupt(Action poll, Pilot pilot, Action interrupt, Action resume, string message)
    {
        poll(); Check(!pilot.aircraft.flightAssist, message + ": starts held");
        interrupt(); FlightAssistCommands.CheckInterrupted();
        Check(pilot.aircraft.flightAssist && !FlightAssistCommands.ActiveFor(pilot), message + ": restores promptly");
        Check(!FlightAssistCommands.HeldFor(pilot.aircraft) && !FlightAssistCommands.SuppressReportFor(pilot.aircraft),
            message + ": clears hold indicator and suppression");
        poll(); Check(pilot.aircraft.flightAssist, message + ": cannot reactivate while interrupted");
        resume();
    }
}

namespace AceOption
{
    internal sealed class Setting<T> { internal T Value; internal Setting(T value) { Value = value; } }
    internal static class Plugin { internal static Setting<bool> ThrottleChordFlightAssist = new Setting<bool>(true); }
}
namespace AceOption.Helpers
{
    internal static class CameraCommands { internal static bool InputAllowed = true; }
}
namespace NuclearOption.UI
{
    internal static class LeaderboardMenu { internal static bool Open; internal static bool IsOpen() => Open; }
}
namespace UnityEngine { public static class Application { public static bool isFocused = true; } }
namespace Rewired
{
    public sealed class Player
    {
        public bool Increase, Decrease, Modifier;
        public bool GetButton(string action) => action == "Throttle" ? Increase : action == "Axis Modifier" && Modifier;
        public bool GetNegativeButton(string action) => action == "Throttle" && Decrease;
    }
}
public sealed class Pilot
{
    public Aircraft aircraft;
    public bool dead;
    public object currentState, playerState = new object();
}
public sealed class Aircraft
{
    public bool flightAssist, remoteSim, Local = true;
    public Cockpit cockpit = new Cockpit();
    public ControlsFilter Filter = new ControlsFilter();
    public int SetCalls;
    public readonly AircraftActionsReport ActionReport = new AircraftActionsReport();
    public int Reports => ActionReport.Count;
    public bool ThrowOnSet;
    public ControlsFilter GetControlsFilter() => Filter;
    public void SetFlightAssist(bool value)
    {
        if (ThrowOnSet) throw new InvalidOperationException();
        FlightAssistCommands.Report(ActionReport, value ? "Assist enabled" : "Assist disabled", 5, this);
        flightAssist = value; SetCalls++;
    }
}
public sealed class AircraftActionsReport
{
    public int Count;
    public void ReportText(string text, float duration) { Count++; }
}
public sealed class ControlsFilter
{
    public bool Supported = true;
    public bool HasFlightAssist() => Supported;
}
public sealed class Cockpit { public bool Detached; public bool IsDetached() => Detached; }
public static class GameManager { public static bool IsLocalAircraft(Aircraft aircraft) => aircraft.Local; }
