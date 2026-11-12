using System;
using AceOption.Helpers;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

// Stand-in rigidbody math verifies direction, damping, singularities and input
// routing. The response model below is not Nuclear Option's flight simulation.
internal static class AutoLevelInputTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }

    private static NQuaternion Attitude(float pitch, float roll, float yaw = 0) =>
        NQuaternion.CreateFromYawPitchRoll(yaw * Radians, pitch * Radians, roll * Radians);
    private const float Radians = (float)Math.PI / 180;

    private static void Inputs(NQuaternion rotation, float pitchRate, float rollRate,
        out float pitch, out float roll)
    {
        var up = NVector3.Transform(NVector3.UnitY, NQuaternion.Inverse(rotation));
        Check(HorizonLevel.TryInputs(up.X, up.Y, up.Z, pitchRate, rollRate, out pitch, out roll),
            "Finite attitude produces usable inputs");
    }

    public static void Main()
    {
        Inputs(Attitude(0, 0), 0, 0, out float pitch, out float roll);
        Check(pitch == 0 && roll == 0, "Level stationary plane needs no correction");
        Inputs(Attitude(-20, 0), 0, 0, out pitch, out roll);
        Check(pitch > 0 && roll == 0, "Nose-up asks for native positive/nose-down pitch");
        Inputs(Attitude(20, 0), 0, 0, out pitch, out roll);
        Check(pitch < 0 && roll == 0, "Nose-down asks for nose-up pitch");
        Inputs(Attitude(0, 30), 0, 0, out pitch, out roll);
        Check(roll > 0, "Left bank asks for native positive/right roll");
        Inputs(Attitude(0, -30), 0, 0, out pitch, out roll);
        Check(roll < 0, "Right bank asks for left roll");
        Inputs(Attitude(-30, 180), 0, 0, out pitch, out roll);
        Check(Math.Abs(roll) == 1 && pitch == 0, "Inverted recovery rolls upright before pitching");
        Inputs(Attitude(-90, 0), 0, 0, out pitch, out roll);
        Check(pitch > .99f && Math.Abs(roll) < .001f, "Vertical climb exits through pitch without undefined bank");
        Inputs(Attitude(90, 0), 0, 0, out pitch, out roll);
        Check(pitch < -.99f && Math.Abs(roll) < .001f, "Vertical dive exits through pitch");
        Inputs(Attitude(0, 0), 1, 1, out pitch, out roll);
        Check(pitch < 0 && roll > 0, "Both body-rate signs are damped at the horizon");
        Inputs(Attitude(-10, 10), 0, 0, out float stillPitch, out float stillRoll);
        Inputs(Attitude(-10, 10), .3f, -.3f, out pitch, out roll);
        Check(pitch < stillPitch && roll < stillRoll, "Recovery brakes as the nose/wings approach level");
        Check(!HorizonLevel.TryInputs(float.NaN, 1, 0, 0, 0, out pitch, out roll) && pitch == 0 && roll == 0,
            "Invalid attitude never injects NaN controls");
        Check(!HorizonLevel.TryInputs(0, 0, 0, 0, 0, out pitch, out roll), "Missing up vector is rejected");
        Check(!HorizonLevel.TryInputs(0, 1, 0, float.PositiveInfinity, 0, out pitch, out roll),
            "Invalid body rates are rejected");

        foreach (float yaw in new[] { 0f, 73f, 180f })
        foreach (var angles in new[] { (-20f, 45f), (25f, -65f), (-30f, 179f), (-90f, 0f), (90f, 0f) })
            Recover(Attitude(angles.Item1, angles.Item2, yaw));
        VerifyRouting();
        Console.WriteLine("PASS: " + checks + " auto-level attitude/recovery/input assertions");
    }

    private static void Recover(NQuaternion rotation)
    {
        var rate = NVector3.Zero;
        const float dt = .02f;
        for (int tick = 0; tick < 750; tick++)
        {
            var up = NVector3.Transform(NVector3.UnitY, NQuaternion.Inverse(rotation));
            if (!HorizonLevel.TryInputs(up.X, up.Y, up.Z, rate.X, rate.Z, out float pitch, out float roll))
                throw new Exception("Recovery produced invalid attitude");
            if (Math.Abs(pitch) > 1 || Math.Abs(roll) > 1) throw new Exception("Recovery exceeds native input limits");
            rate += (new NVector3(pitch * .8f, 0, -roll * 1.4f) - rate) * (dt / .2f);
            float speed = rate.Length();
            if (speed > .000001f)
                rotation = NQuaternion.Normalize(rotation * NQuaternion.CreateFromAxisAngle(rate / speed, speed * dt));
        }
        var finalUp = NVector3.Transform(NVector3.UnitY, NQuaternion.Inverse(rotation));
        Check(finalUp.Y > .999f, "Bank/pitch/inverted/vertical recovery converges upright independently of heading");
        Check(rate.Length() < .01f, "Recovery settles rather than oscillating at the horizon");
    }

    private static void VerifyRouting()
    {
        var pilot = new Pilot();
        pilot.currentState = pilot.playerState;
        var aircraft = pilot.aircraft;
        aircraft.Body.rotation = Attitude(-20, 30);
        var player = new Rewired.Player();
        var inputs = new ControlInputs { throttle = .75f, customAxis1 = .6f, brake = .2f };
        Action apply = () => AutoLevelCommands.Apply(pilot, player, 1, inputs);
        Action original = () => { inputs.pitch = .31f; inputs.roll = -.42f; inputs.yaw = .53f; };
        Action unchanged = () => Check(inputs.pitch == .31f && inputs.roll == -.42f && inputs.yaw == .53f,
            "Inactive/blocked chord preserves native axes");
        original(); apply(); unchanged();
        Check(!AutoLevelCommands.ActiveFor(aircraft), "Inactive chord has no leveling indicator");
        player.Right = true; apply(); unchanged();
        player.Right = false; player.Left = true; apply(); unchanged();
        player.Right = true; apply();
        Check(inputs.pitch > 0 && inputs.roll > 0 && inputs.yaw == 0, "Both yaw poles override stick/mouse axes and neutralize rudder");
        Check(YawCommands.Resets == 1, "Active chord discards old smoothed rudder");
        Check(AutoLevelCommands.ActiveFor(aircraft), "Successful leveling displays its held indicator");
        Check(!AutoLevelCommands.ActiveFor(new Aircraft()), "Leveling indicator belongs only to the controlled aircraft");
        Check(inputs.throttle == .75f && inputs.customAxis1 == .6f && inputs.brake == .2f && aircraft.flightAssist,
            "Throttle, custom axis, brakes and assist state are preserved");
        aircraft.flightAssist = false; apply();
        Check(!aircraft.flightAssist, "Auto-level never enables native assist");
        aircraft.flightAssist = true;
        player.Left = false;
        Check(!AutoLevelCommands.ActiveFor(aircraft), "Releasing either button hides leveling before the next physics update");
        original(); apply(); unchanged();
        player.Left = true; player.Right = false; apply(); unchanged();
        player.Right = true;

        Action<Action, Action> blocked = (stop, resume) => {
            stop(); original(); int resets = YawCommands.Resets; apply(); unchanged();
            Check(!AutoLevelCommands.ActiveFor(aircraft), "Blocked chord has no leveling indicator");
            Check(YawCommands.Resets == resets, "Blocked chord does not alter yaw smoothing");
            resume();
        };
        blocked(() => AceOption.Plugin.YawChordAutoLevel.Value = false, () => AceOption.Plugin.YawChordAutoLevel.Value = true);
        blocked(() => CameraCommands.InputAllowed = false, () => CameraCommands.InputAllowed = true);
        blocked(() => Application.isFocused = false, () => Application.isFocused = true);
        blocked(() => NuclearOption.UI.LeaderboardMenu.Open = true, () => NuclearOption.UI.LeaderboardMenu.Open = false);
        blocked(() => pilot.dead = true, () => pilot.dead = false);
        blocked(() => pilot.currentState = null, () => pilot.currentState = pilot.playerState);
        blocked(() => aircraft.remoteSim = true, () => aircraft.remoteSim = false);
        blocked(() => aircraft.Local = false, () => aircraft.Local = true);
        blocked(() => aircraft.radarAlt = .2f, () => aircraft.radarAlt = 500);
        blocked(() => aircraft.cockpit.Detached = true, () => aircraft.cockpit.Detached = false);
        blocked(() => aircraft.Filter = new HeloControlsFilter(), () => aircraft.Filter = new ControlsFilter());
        blocked(() => aircraft.Hover = true, () => aircraft.Hover = false);
        var body = aircraft.Body;
        blocked(() => aircraft.Body = null, () => aircraft.Body = body);
        var cockpit = aircraft.cockpit;
        blocked(() => aircraft.cockpit = null, () => aircraft.cockpit = cockpit);
        blocked(() => pilot.aircraft = null, () => pilot.aircraft = aircraft);
        original(); AutoLevelCommands.Apply(pilot, player, .19f, inputs); unchanged();
        AutoLevelCommands.Apply(null, player, 1, inputs); unchanged();
        AutoLevelCommands.Apply(pilot, null, 1, inputs); unchanged();
        AutoLevelCommands.Apply(pilot, player, 1, null); unchanged();
        apply(); Check(inputs.yaw == 0 && inputs.pitch != .31f, "Resuming a held chord computes fresh corrections");
        pilot.aircraft = new Aircraft(); original(); apply();
        Check(inputs.pitch == 0 && inputs.roll == 0 && inputs.yaw == 0, "New aircraft uses its own attitude without cached commands");
        Check(!AutoLevelCommands.ActiveFor(aircraft) && AutoLevelCommands.ActiveFor(pilot.aircraft),
            "Aircraft change cannot retain the previous aircraft's indicator");
        CameraCommands.InputAllowed = false; AutoLevelCommands.CheckInterrupted(); CameraCommands.InputAllowed = true;
        Check(!AutoLevelCommands.ActiveFor(pilot.aircraft), "Menu interruption clears status until leveling actually applies again");
        apply(); AutoLevelCommands.Reset();
        Check(!AutoLevelCommands.ActiveFor(pilot.aircraft), "Lifecycle reset removes leveling status");
    }
}

namespace AceOption
{
    internal sealed class Setting<T> { internal T Value; internal Setting(T value) { Value = value; } }
    internal static class Plugin { internal static Setting<bool> YawChordAutoLevel = new Setting<bool>(true); }
}
namespace AceOption.Helpers
{
    internal static class CameraCommands { internal static bool InputAllowed = true; }
    internal static class YawCommands { internal static int Resets; internal static void Reset(Rewired.Player player) { Resets++; } }
}
namespace NuclearOption.UI
{
    internal static class LeaderboardMenu { internal static bool Open; internal static bool IsOpen() => Open; }
}
namespace Rewired
{
    public sealed class Player
    {
        public bool Left, Right;
        public bool GetButton(string action) => action == "Yaw" && Right;
        public bool GetNegativeButton(string action) => action == "Yaw" && Left;
    }
}
namespace UnityEngine
{
    public static class Application { public static bool isFocused = true; }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 up => new Vector3 { y = 1 };
    }
    public struct Quaternion
    {
        public NQuaternion Value;
        public static implicit operator Quaternion(NQuaternion q) => new Quaternion { Value = q };
        public static Quaternion Inverse(Quaternion q) => NQuaternion.Inverse(q.Value);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var transformed = NVector3.Transform(new NVector3(v.x, v.y, v.z), q.Value);
            return new Vector3 { x = transformed.X, y = transformed.Y, z = transformed.Z };
        }
    }
    public sealed class Rigidbody
    {
        public Quaternion rotation = NQuaternion.Identity;
        public Vector3 angularVelocity = new Vector3();
    }
}
public sealed class Pilot
{
    public Aircraft aircraft = new Aircraft();
    public bool dead;
    public object currentState, playerState = new object();
}
public sealed class Aircraft
{
    public bool remoteSim, Local = true, Hover, flightAssist = true;
    public float radarAlt = 500;
    public Cockpit cockpit = new Cockpit();
    public Rigidbody Body = new Rigidbody();
    public ControlsFilter Filter = new ControlsFilter();
    public Rigidbody CockpitRB() => Body;
    public ControlsFilter GetControlsFilter() => Filter;
    public bool IsAutoHoverEnabled() => Hover;
}
public class ControlsFilter { }
public sealed class HeloControlsFilter : ControlsFilter { }
public sealed class Cockpit { public bool Detached; public bool IsDetached() => Detached; }
public sealed class ControlInputs { public float pitch, roll, yaw, throttle, customAxis1, brake; }
public static class GameManager { public static bool IsLocalAircraft(Aircraft aircraft) => aircraft.Local; }
