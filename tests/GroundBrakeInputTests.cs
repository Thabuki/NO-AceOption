using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;

// Real Harmony field accessors against stand-ins; no Unity/game process is run.
internal static class GroundBrakeInputTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    private static void Sample(Pilot pilot, Rewired.Player player, float strength,
        ControlInputs controls, bool grounded, Action nativeControls = null)
    {
        GroundBrakeCommands.RestoreBrake();
        nativeControls?.Invoke();
        GroundBrakeCommands.Apply(pilot, player, strength, controls, grounded);
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string path = Path.Combine(args[0], "BepInEx", "core", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        VerifySeparatePhysicsParts();
        VerifyMinimumThrottleHold();
        VerifyActivationDelay();
        var gear = new LandingGear(.1f);
        var aircraft = new Aircraft { Gears = new[] { gear } };
        var pilot = new Pilot { aircraft = aircraft };
        pilot.currentState = pilot.playerState;
        var player = new Rewired.Player();
        var controls = new ControlInputs { throttle = 0, customAxis1 = .7f, pitch = .2f, roll = -.3f, yaw = .4f };
        Action sample = () => Sample(pilot, player, 1f, controls, GroundContact.OnGround(pilot.aircraft));
        Action poll = () => { sample(); UnityEngine.Time.time += 1f; sample(); };
        Action press = () => { player.Decrease = false; sample(); player.Decrease = true; };
        poll(); Check(controls.brake == 0, "No throttle input adds no brakes");
        player.Increase = true; poll(); Check(controls.brake == 0, "Increase alone adds no brakes");
        player.Increase = false; player.Decrease = true; poll();
        Check(controls.brake == 1, "Throttle down applies full brakes with wheel contact, regardless of speed");
        Check(controls.throttle == 0 && controls.customAxis1 == .7f && controls.pitch == .2f &&
            controls.roll == -.3f && controls.yaw == .4f, "Braking leaves throttle and every other flight input untouched");
        poll(); poll(); GroundBrakeCommands.Reset();
        Check(controls.brake == 0, "Repeated holds do not capture their own brake value");
        press(); poll(); player.Decrease = false; GroundBrakeCommands.CheckInterrupted();
        Check(controls.brake == 0, "Release between control updates immediately removes added braking");
        player.Decrease = true; poll(); gear.Compression = 0; poll();
        Check(controls.brake == 0, "Leaving the ground removes braking while throttle down stays held");
        gear.Compression = .1f; poll();
        Check(controls.brake == 1, "Touchdown after a press at minimum throttle applies braking after the ground hold");
        player.Increase = true; poll();
        Check(controls.brake == 0, "Both throttle directions preserve the flight-assist shortcut without braking");
        player.Increase = false;

        Action<Action, Action, string> interrupt = (stop, resume, label) => {
            press(); poll(); Check(controls.brake == 1, label + ": starts braking");
            stop(); GroundBrakeCommands.CheckInterrupted();
            Check(controls.brake == 0, label + ": clears added braking");
            poll(); Check(controls.brake == 0, label + ": stays inactive");
            resume();
        };
        interrupt(() => player.Modifier = true, () => player.Modifier = false, "Axis Modifier");
        interrupt(() => AceOption.Plugin.ThrottleDownGroundBrakes.Value = false,
            () => AceOption.Plugin.ThrottleDownGroundBrakes.Value = true, "Live opt-out");
        interrupt(() => CameraCommands.InputAllowed = false, () => CameraCommands.InputAllowed = true, "Menus/map/pause");
        interrupt(() => UnityEngine.Application.isFocused = false, () => UnityEngine.Application.isFocused = true, "Focus loss");
        interrupt(() => NuclearOption.UI.LeaderboardMenu.Open = true, () => NuclearOption.UI.LeaderboardMenu.Open = false, "Leaderboard");
        interrupt(() => pilot.dead = true, () => pilot.dead = false, "Death");
        interrupt(() => pilot.currentState = new object(), () => pilot.currentState = pilot.playerState, "Ejection");
        interrupt(() => aircraft.cockpit.Detached = true, () => aircraft.cockpit.Detached = false, "Cockpit detachment");
        interrupt(() => aircraft.Local = false, () => aircraft.Local = true, "Ownership loss");
        interrupt(() => aircraft.remoteSim = true, () => aircraft.remoteSim = false, "Remote simulation");

        press(); poll(); GroundBrakeCommands.Apply(pilot, player, .19f, controls, true);
        Check(controls.brake == 0, "Pilot control loss removes added braking");
        press(); Sample(pilot, player, .2f, controls, true);
        UnityEngine.Time.time += 1f;
        Sample(pilot, player, .2f, controls, true);
        Check(controls.brake == 1, "Native minimum pilot strength is accepted");
        GroundBrakeCommands.Reset(); controls.brake = .35f; press(); poll(); GroundBrakeCommands.Reset();
        Check(controls.brake == .35f, "Reset restores an existing partial brake contribution");
        controls.brake = 1; press(); poll(); player.Decrease = false; poll();
        Check(controls.brake == 1, "Normal Brake stays held when throttle down is released");
        controls.brake = 0; poll();
        Check(controls.brake == 0, "Normal Brake release is never latched by this feature");
        player.Decrease = true; poll(); controls.brake = .4f; GroundBrakeCommands.Reset();
        Check(controls.brake == .4f, "Cleanup preserves a brake value subsequently written by another source");
        controls.brake = 0;

        press(); poll(); var replacement = new Aircraft(); pilot.aircraft = replacement;
        GroundBrakeCommands.CheckInterrupted();
        Check(controls.brake == 0, "Changing aircraft clears only the old injected brake");
        var other = new ControlInputs { brake = .2f };
        Sample(pilot, player, 1f, other, true); GroundBrakeCommands.Reset();
        Check(other.brake == .2f && controls.brake == 0, "New aircraft retains its own brake baseline");
        pilot.aircraft = aircraft;
        press(); poll(); var otherPilot = new Pilot { aircraft = replacement }; otherPilot.currentState = otherPilot.playerState;
        Sample(otherPilot, player, 1f, other, true);
        Check(controls.brake == 0 && other.brake == .2f, "Changing pilots starts a fresh brake delay");
        UnityEngine.Time.time += 1f;
        Sample(otherPilot, player, 1f, other, true);
        Check(controls.brake == 0 && other.brake == 1, "Changing pilots releases the previous controls");
        GroundBrakeCommands.Reset();
        press(); poll(); pilot.aircraft = null; GroundBrakeCommands.CheckInterrupted();
        Check(controls.brake == 0, "Aircraft loss clears braking");
        GroundBrakeCommands.Apply(null, player, 1f, controls, true);
        pilot.aircraft = aircraft;
        GroundBrakeCommands.Apply(pilot, null, 1f, controls, true);
        GroundBrakeCommands.Apply(pilot, player, 1f, null, true);
        aircraft.cockpit = null; poll();
        Check(controls.brake == 0, "Missing pilot, player, controls or cockpit is safe");
        aircraft.cockpit = new Cockpit();
        press(); poll(); GroundBrakeCommands.Reset(); GroundBrakeCommands.Reset();
        Check(controls.brake == 0, "Lifecycle/unload reset is repeatable");

        Check(aircraft.GearScans == 1, "Landing gear lookup is cached per aircraft");
        Check(!GroundContact.OnGround(null) && !GroundContact.OnGround(new Aircraft()), "Missing aircraft or wheels has no ground contact");
        gear.Compression = 0; Check(!GroundContact.OnGround(aircraft), "Uncompressed gear is airborne");
        gear.Compression = .1f; gear.isActiveAndEnabled = false;
        Check(!GroundContact.OnGround(aircraft), "Disabled gear cannot report stale contact");
        gear.isActiveAndEnabled = true; gear.Part.Detached = true;
        Check(!GroundContact.OnGround(aircraft), "Detached gear cannot report stale contact");
        gear.Part = null; Check(!GroundContact.OnGround(aircraft), "Missing attached part is safe");
        var second = new Aircraft { Gears = new[] { null, new LandingGear(0), new LandingGear(.01f) } };
        Check(GroundContact.OnGround(second), "Any attached, compressed wheel establishes contact");
        Check(!GroundContact.OnGround(new Aircraft { Gears = new[] { new LandingGear(float.NaN) } }), "Invalid compression is not ground contact");
        Console.WriteLine("PASS: " + checks + " ground brake delay/input/contact/cleanup assertions");
    }

    private static void VerifySeparatePhysicsParts()
    {
        // Native AeroPart.CreateRB unparents physical parts without detaching
        // them from the aircraft. Their wheels are absent from its children.
        var part = new AeroPart();
        var gear = new LandingGear(.1f) { Part = part };
        part.Gears = new[] { gear };
        var aircraft = new Aircraft();
        aircraft.partLookup.Add(null);
        aircraft.partLookup.Add(part);
        Check(GroundContact.OnGround(aircraft), "Registered physical parts establish contact outside the aircraft hierarchy");
        GroundContact.OnGround(aircraft);
        Check(aircraft.GearScans == 1 && part.GearScans == 1, "Physical-part wheel discovery is cached");

        var pilot = new Pilot { aircraft = aircraft };
        pilot.currentState = pilot.playerState;
        var player = new Rewired.Player { Decrease = true };
        var controls = new ControlInputs { throttle = 0 };
        Action sample = () => Sample(pilot, player, 1f, controls, GroundContact.OnGround(aircraft));
        UnityEngine.Time.time = 0; sample();
        Check(controls.brake == 0, "Throttle down at minimum starts the grounded delay");
        UnityEngine.Time.time = 1f; sample();
        Check(controls.brake == 1 && controls.throttle == 0, "Throttle down at minimum brakes wheels on separate physics parts");
        player.Decrease = false; GroundBrakeCommands.CheckInterrupted();
        Check(controls.brake == 0 && controls.throttle == 0, "Release at minimum throttle removes added braking");

        gear.Compression = 0;
        Check(!GroundContact.OnGround(aircraft), "Separate physical parts lose contact on takeoff");
        gear.Compression = .1f; part.Detached = true;
        Check(!GroundContact.OnGround(aircraft), "Physically detached parts cannot establish contact through the registry");

        var initializing = new Aircraft();
        Check(!GroundContact.OnGround(initializing), "An aircraft without initialized parts has no wheel contact");
        initializing.partLookup.Add(new AeroPart { Gears = new[] { new LandingGear(.1f) } });
        Check(GroundContact.OnGround(initializing), "Wheel discovery refreshes when physical parts finish registering");
        initializing.partLookup = null;
        Check(!GroundContact.OnGround(initializing), "A missing part registry is safe and clears cached physical wheels");
        GroundBrakeCommands.Reset();
    }

    private static void VerifyMinimumThrottleHold()
    {
        var pilot = new Pilot { aircraft = new Aircraft() };
        pilot.currentState = pilot.playerState;
        var player = new Rewired.Player { Decrease = true };
        var controls = new ControlInputs { throttle = .6f };
        Action sample = () => Sample(pilot, player, 1f, controls, true);
        UnityEngine.Time.time = 0; sample();
        UnityEngine.Time.time = 2f; sample();
        Check(controls.brake == 0 && controls.throttle == .6f,
            "Holding throttle down above minimum never starts ground braking");
        controls.throttle = .1f; sample();
        UnityEngine.Time.time = 3f; controls.throttle = .01f; sample();
        UnityEngine.Time.time = 4f; controls.throttle = 0; sample();
        Check(controls.brake == 0, "Gently reaching zero while held starts a new delay without credit from throttle reduction");
        UnityEngine.Time.time = 4.999f; sample();
        Check(controls.brake == 0, "Brakes stay off until a full second at zero throttle");
        UnityEngine.Time.time = 5f; sample();
        Check(controls.brake == 1, "Gently reaching zero while held activates braking without releasing the trigger");

        player.Decrease = false; sample();
        controls.throttle = .01f; player.Decrease = true;
        UnityEngine.Time.time = 6f;
        Sample(pilot, player, 1f, controls, true, () => controls.throttle = 0);
        for (int i = 0; i < 100; i++)
        {
            GroundBrakeCommands.RestoreBrake();
            GroundBrakeCommands.Apply(pilot, player, 1f, controls, true);
        }
        Check(controls.brake == 0, "Repeated polls when native controls reach zero do not bypass the delay");
        UnityEngine.Time.time = 6.999f; sample();
        Check(controls.brake == 0, "The delay starts from native controls reaching zero");
        UnityEngine.Time.time = 7f; sample();
        Check(controls.brake == 1, "Native controls reaching zero activates brakes after one second of continued holding");
        controls.throttle = .2f; sample();
        Check(controls.brake == 0, "Raising throttle immediately cancels active added braking");
        controls.throttle = 0; UnityEngine.Time.time = 9f; sample();
        Check(controls.brake == 0, "Returning to zero while held restarts the full delay");
        UnityEngine.Time.time = 9.999f; sample();
        Check(controls.brake == 0, "Returning to zero does not reuse the previous completed timer");
        UnityEngine.Time.time = 10f; sample();
        Check(controls.brake == 1, "Returning to zero while held reactivates braking after a full second");

        player.Decrease = false; sample(); player.Decrease = true; sample();
        UnityEngine.Time.time = 10.5f; controls.throttle = .1f; sample();
        controls.throttle = 0; UnityEngine.Time.time = 11f; sample();
        UnityEngine.Time.time = 11.5f; sample();
        Check(controls.brake == 0, "Raising throttle during a pending delay clears its elapsed time");
        UnityEngine.Time.time = 12f; sample();
        Check(controls.brake == 1, "A pending delay restarts at zero without a new press");

        player.Decrease = false; sample(); UnityEngine.Time.time = 20f; sample();
        Check(controls.brake == 0, "Zero throttle without throttle down held cannot count toward braking");
        player.Decrease = true; sample();
        UnityEngine.Time.time = 20.999f; sample();
        Check(controls.brake == 0, "Holding throttle down after sitting at zero starts a fresh delay");
        UnityEngine.Time.time = 21f; sample();
        Check(controls.brake == 1, "Holding throttle down at zero activates after its full delay");

        foreach (float throttle in new[] { float.Epsilon, float.NaN, .5f, 1f })
        {
            player.Decrease = false; sample(); controls.throttle = throttle;
            player.Decrease = true; sample(); UnityEngine.Time.time += 2f; sample();
            Check(controls.brake == 0, "Nonzero or invalid throttle cannot count toward braking: " + throttle);
        }

        player.Decrease = false; sample(); controls.throttle = .6f;
        player.Decrease = true; Sample(pilot, player, 1f, controls, false);
        controls.throttle = 0; UnityEngine.Time.time += 2f;
        Sample(pilot, player, 1f, controls, false); sample();
        Check(controls.brake == 0, "Reaching zero in flight receives no delay credit before wheel contact");
        UnityEngine.Time.time += .5f; sample();
        Check(controls.brake == 0, "Touchdown with zero throttle and throttle down held starts a full delay");
        UnityEngine.Time.time += .5f; sample();
        Check(controls.brake == 1, "A hold begun above idle in flight brakes after one second grounded at zero");
        GroundBrakeCommands.Reset();
    }

    private static void VerifyActivationDelay()
    {
        var pilot = new Pilot { aircraft = new Aircraft() };
        pilot.currentState = pilot.playerState;
        var player = new Rewired.Player { Decrease = true };
        var controls = new ControlInputs { throttle = 0 };
        bool grounded = true;
        Action sample = () => {
            Sample(pilot, player, 1f, controls, grounded);
        };
        Action press = () => { player.Decrease = false; sample(); player.Decrease = true; };
        UnityEngine.Time.time = 0; sample();
        Check(controls.brake == 0 && controls.throttle == 0, "Throttle down at idle starts the delay without changing throttle");
        UnityEngine.Time.time = .5f;
        for (int i = 0; i < 100; i++) {
            GroundBrakeCommands.RestoreBrake();
            GroundBrakeCommands.Apply(pilot, player, 1f, controls, grounded);
            GroundBrakeCommands.CheckInterrupted();
        }
        Check(controls.brake == 0, "Frame and physics polls do not accumulate time or restart the delay");
        UnityEngine.Time.time = .999f; sample();
        Check(controls.brake == 0, "Brakes remain off just before one second");
        UnityEngine.Time.time = 1f; sample();
        Check(controls.brake == 1, "Brakes activate at one full second of grounded throttle down");
        UnityEngine.Time.time = 2f; sample();
        Check(controls.brake == 1, "Brakes stay active throughout a continuous hold");
        player.Decrease = false; GroundBrakeCommands.CheckInterrupted();
        Check(controls.brake == 0, "Release removes braking immediately after activation");

        player.Decrease = true; sample();
        UnityEngine.Time.time = 2.5f; player.Decrease = false; GroundBrakeCommands.CheckInterrupted();
        UnityEngine.Time.time = 3f; player.Decrease = true; sample();
        UnityEngine.Time.time = 3.5f; sample();
        Check(controls.brake == 0, "Separate short presses cannot combine to satisfy the delay");
        UnityEngine.Time.time = 4f; sample();
        Check(controls.brake == 1, "A fresh hold activates after its own full second");

        grounded = false; sample();
        Check(controls.brake == 0, "Leaving the ground clears braking and the hold timer");
        UnityEngine.Time.time = 8f; sample(); grounded = true; sample();
        Check(controls.brake == 0, "Time held in flight does not bypass the touchdown delay");
        UnityEngine.Time.time = 8.5f; sample();
        Check(controls.brake == 0, "Touchdown starts a new one-second grounded hold after a press at idle");
        UnityEngine.Time.time = 9f; sample();
        Check(controls.brake == 1, "A continuous grounded hold after a press at idle activates brakes");

        GroundBrakeCommands.Reset(); press(); UnityEngine.Time.time = 10f; sample();
        UnityEngine.Time.time = 10.5f; CameraCommands.InputAllowed = false;
        GroundBrakeCommands.CheckInterrupted();
        UnityEngine.Time.time = 20f; CameraCommands.InputAllowed = true; sample();
        Check(controls.brake == 0, "Menus/pause cancel a pending delay even when control polling stops");
        UnityEngine.Time.time = 20.5f; sample();
        Check(controls.brake == 0, "Returning from an interruption requires a fresh hold");
        UnityEngine.Time.time = 21f; sample();
        Check(controls.brake == 1, "A valid press at idle resumes braking after a full hold following an interruption");

        GroundBrakeCommands.Reset(); controls.brake = 1; press(); sample();
        Check(controls.brake == 1, "The dedicated Brake remains immediately available during the delay");
        GroundBrakeCommands.Reset(); controls.brake = 0;
        UnityEngine.Time.time = 22f; press(); sample(); UnityEngine.Time.time = 22.5f;
        pilot.aircraft = new Aircraft(); GroundBrakeCommands.CheckInterrupted(); sample();
        press(); sample();
        UnityEngine.Time.time = 23f; sample();
        Check(controls.brake == 0, "Changing aircraft during a pending hold restarts the full delay");
        UnityEngine.Time.time = 23.5f; sample();
        Check(controls.brake == 1, "The new aircraft activates only after its own one-second hold");
        GroundBrakeCommands.Reset();
    }
}

namespace AceOption
{
    internal sealed class Setting<T> { internal T Value; internal Setting(T value) { Value = value; } }
    internal static class Plugin { internal static Setting<bool> ThrottleDownGroundBrakes = new Setting<bool>(true); }
}
namespace AceOption.Helpers { internal static class CameraCommands { internal static bool InputAllowed = true; } }
namespace UnityEngine
{
    internal static class Application { internal static bool isFocused = true; }
    internal static class Time { internal static float time; }
}
namespace NuclearOption.UI { internal static class LeaderboardMenu { internal static bool Open; internal static bool IsOpen() => Open; } }
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
    public bool remoteSim, Local = true;
    public Cockpit cockpit = new Cockpit();
    public LandingGear[] Gears = new LandingGear[0];
    public List<UnitPart> partLookup = new List<UnitPart>();
    public int GearScans;
    public T[] GetComponentsInChildren<T>(bool includeInactive) { GearScans++; return (T[])(object)Gears; }
}
public sealed class ControlInputs { public float throttle, customAxis1, brake, pitch, roll, yaw; }
public sealed class Cockpit { public bool Detached; public bool IsDetached() => Detached; }
public class UnitPart
{
    public bool Detached;
    public LandingGear[] Gears = new LandingGear[0];
    public int GearScans;
    public bool IsDetached() => Detached;
    public T[] GetComponentsInChildren<T>(bool includeInactive) { GearScans++; return (T[])(object)Gears; }
}
public sealed class AeroPart : UnitPart { }
public sealed class LandingGear
{
    private float compressionDistance;
    private AeroPart attachedPart = new AeroPart();
    public bool isActiveAndEnabled = true;
    public float Compression { set { compressionDistance = value; } }
    public AeroPart Part { get { return attachedPart; } set { attachedPart = value; } }
    public LandingGear(float compression) { compressionDistance = compression; }
}
public static class GameManager { public static bool IsLocalAircraft(Aircraft aircraft) => aircraft.Local; }
