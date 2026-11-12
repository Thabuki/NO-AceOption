using System;
using System.Collections.Generic;
using AceOption.Helpers;

internal static class OptionalCombatTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) =>
        Check(Math.Abs(actual - expected) < .0001f, message);
    private static HUDUnitMarker Marker(Unit unit, float x, float y = 400) =>
        new HUDUnitMarker { unit = unit, image = new UnityEngine.Image { transform = new UnityEngine.Transform {
            position = new UnityEngine.Vector3(x, y, 0) } } };

    private static Aircraft Aircraft()
    {
        var aircraft = new Aircraft { NetworkHQ = new FactionHQ(), cockpit = new Cockpit(),
            pilots = new[] { new Pilot() }, weaponManager = new WeaponManager() };
        aircraft.pilots[0].currentState = aircraft.pilots[0].playerState;
        return aircraft;
    }

    private static void TargetTests()
    {
        var aircraft = Aircraft(); GameManager.Local = aircraft;
        var hud = new CombatHUD { aircraft = aircraft };
        hud.targetDesignator.transform.position = new UnityEngine.Vector3(500, 400, 0);
        SceneSingleton<TargetListSelector>.i = new TargetListSelector();
        var enemyHQ = new FactionHQ();
        var a = new Unit { NetworkHQ = enemyHQ };
        var b = new Unit { NetworkHQ = enemyHQ };
        var markers = new List<HUDUnitMarker> { Marker(b, 600), Marker(a, 510) };
        CombatHUD.AllMarkers = markers;
        hud.Targets.Add(b);
        Check(!TargetCycling.TrySelect(hud, false, markers, null), "Cycling defaults to off");
        Check(hud.Targets.Count == 1 && hud.Targets[0] == b && hud.Clears == 0,
            "Opting out leaves selections and callbacks untouched");
        AceOption.Plugin.TargetCycling.Value = true;
        hud.Targets.Clear();
        Check(TargetCycling.TrySelect(hud, false, markers, null), "Opt-in handles a tap");
        Check(hud.Targets.Count == 1 && hud.Targets[0] == a, "First selection prefers the screen center");
        TargetCycling.TrySelect(hud, false, markers, null);
        Check(hud.Targets.Count == 1 && hud.Targets[0] == b, "Next tap replaces rather than accumulates targets");
        TargetCycling.TrySelect(hud, false, markers, null);
        Check(hud.Targets[0] == a && !markers[0].selected && markers[1].selected,
            "Cycling wraps and native selection updates marker state");
        Check(aircraft.weaponManager.Changes >= 3, "Selection notifies the native weapon manager");
        a.Suitable = false;
        TargetCycling.TrySelect(hud, false, markers, null);
        Check(hud.Targets[0] == b, "Compatible weapons prefer suitable targets");
        int clears = hud.Clears;
        TargetCycling.TrySelect(hud, false, markers, null);
        Check(hud.Clears == clears, "A sole eligible target does not lose and reacquire its lock");
        b.Suitable = false;
        TargetCycling.TrySelect(hud, false, markers, null);
        Check(hud.Targets[0] == a, "No suitable target falls back to visible contacts");

        foreach (Action block in new Action[] {
            () => a.Known = false, () => a.disabled = true, () => a.Excluded = true,
            () => markers[1].outdated = true, () => markers[1].image.enabled = false,
            () => markers[1].image.gameObject.activeInHierarchy = false,
            () => markers[1].image.transform.position = new UnityEngine.Vector3(-1, 400, 0),
            () => a.NetworkHQ = aircraft.NetworkHQ, () => a.NetworkHQ = null })
        {
            a.Known = true; a.disabled = false; a.Excluded = false; a.NetworkHQ = enemyHQ;
            markers[1].outdated = false; markers[1].image.enabled = true;
            markers[1].image.gameObject.activeInHierarchy = true;
            markers[1].image.transform.position = new UnityEngine.Vector3(510, 400, 0);
            hud.Targets.Clear(); block();
            TargetCycling.TrySelect(hud, false, markers, null);
            Check(hud.Targets.Count == 1 && hud.Targets[0] == b, "Hidden, excluded, stale and friendly contacts cannot be selected");
        }
        markers[0].image.enabled = false;
        clears = hud.Clears;
        Check(TargetCycling.TrySelect(hud, false, markers, null) && hud.Clears == clears,
            "No eligible contacts preserves current selection");
        Check(!TargetCycling.TrySelect(hud, true, markers, null), "Unused long-hold bulk marking remains native");
        DynamicMap.mapMaximized = true;
        Check(!TargetCycling.TrySelect(hud, false, markers, null), "Map controls stay native");
        DynamicMap.mapMaximized = false;
        CameraCommands.InputAllowed = false;
        Check(!TargetCycling.TrySelect(hud, false, markers, null), "Menus do not cycle targets");
        CameraCommands.InputAllowed = true;
        aircraft.remoteSim = true;
        Check(!TargetCycling.TrySelect(hud, false, markers, null), "Remote simulation is not modified");
        aircraft.remoteSim = false; GameManager.Local = Aircraft();
        Check(!TargetCycling.TrySelect(hud, false, markers, null), "Other aircraft are not modified");
        GameManager.Local = aircraft; AceOption.Plugin.TargetCycling.Value = false;
        Check(!TargetCycling.TrySelect(hud, false, markers, null) && hud.Clears == clears,
            "Live opt-out restores native handling without clearing the selection");
    }

    private static void EnvelopeTests()
    {
        var envelope = new VibrationEnvelope();
        envelope.Fire(VibrationEffect.Damage, 0);
        envelope.Fire(VibrationEffect.Gun, .1f);
        envelope.Sample(.3f, 1, 1, true, true, true, out float left, out float right);
        Near(left, 0, "Gun fire does not extend a damage pulse"); Near(right, 0, "Pulses expire");
        envelope.Fire(VibrationEffect.Ordnance, 1);
        envelope.Sample(1, 1, .5f, true, true, true, out left, out right);
        Near(left, .225f, "Master strength scales the low motor");
        Near(right, .275f, "Master strength scales the high motor");
        envelope.Sample(1.01f, 1, 1, false, true, true, out left, out right);
        Near(left, 0, "Weapon toggle silences active weapon feedback");
        envelope.Sample(1.02f, 1, 1, true, true, true, out left, out right);
        Near(right, 0, "Re-enabling weapons does not replay old feedback");
        foreach (float g in new[] { 1f, 4f, -2f })
        {
            envelope.Sample(2, g, 1, true, true, true, out left, out right);
            Near(left + right, 0, "Normal flight is silent");
        }
        envelope.Sample(2, 9, 1, true, true, true, out left, out right);
        Near(left, .22f, "Positive G produces restrained feedback");
        envelope.Sample(2, -5, 1, true, true, true, out left, out right);
        Near(right, .3f, "Negative G produces restrained feedback");
        envelope.Sample(2, 100, 5, true, true, true, out left, out right);
        Check(left <= 1 && right <= 1, "Motor levels remain bounded");
        envelope.Sample(2, float.NaN, float.PositiveInfinity, true, true, true, out left, out right);
        Near(left + right, 0, "Invalid telemetry cannot reach the motors");
        envelope.Reset(); envelope.Sample(2, 9, 0, true, true, true, out left, out right);
        Near(left + right, 0, "Zero intensity silences every effect");
    }

    private static void FeedbackTests()
    {
        var aircraft = Aircraft(); GameManager.Local = aircraft;
        var cam = new CameraStateManager { followingUnit = aircraft };
        cam.currentState = cam.chaseState; SceneSingleton<CameraStateManager>.i = cam;
        var device = new Rewired.Joystick();
        GameManager.playerInput.controllers.Active = device;
        var station = new WeaponStation { WeaponInfo = new WeaponInfo { gun = true } };
        aircraft.weaponStations.Add(station);
        ControllerFeedback.Reset();
        Check(!AceOption.Plugin.ControllerVibration.Value, "Vibration defaults to off");
        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        Check(device.Writes == 0, "Disabled vibration never writes to hardware");
        AceOption.Plugin.ControllerVibration.Value = true;
        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        Check(device.Left > 0 && device.Right > 0, "The first actual shot reaches the active controller");
        Check(device.Timeout > 0 && device.Timeout < 1, "Hardware output always has a short failsafe timeout");
        UnityEngine.Time.unscaledTime = 1; ControllerFeedback.Update();
        Check(device.Stops == 1, "Expired feedback releases the device");
        int writes = device.Writes;
        ControllerFeedback.WeaponFired(station, 0);
        ControllerFeedback.WeaponFired(new WeaponStation { WeaponInfo = station.WeaponInfo }, 1);
        ControllerFeedback.Update(); Check(device.Writes == writes, "Dry fire and remote stations do not vibrate");
        var part = new UnitPart { parentUnit = aircraft, hitPoints = 9 };
        ControllerFeedback.Damaged(part, 10); ControllerFeedback.Update();
        Check(device.Left > .4f, "Actual local damage produces an impact pulse");
        int stops = device.Stops;
        CameraCommands.InputAllowed = false; ControllerFeedback.Update();
        Check(device.Stops == stops + 1, "Opening a menu stops active motors");
        CameraCommands.InputAllowed = true; ControllerFeedback.Update();
        Check(device.Stops == stops + 1, "Returning from a menu does not replay a pulse");

        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        var other = new Rewired.Joystick { vibrationMotorCount = 1 };
        GameManager.playerInput.controllers.Active = other;
        ControllerFeedback.Update(); Check(device.Stops == stops + 2 && other.Writes == 0,
            "Switching controllers stops the old device without replaying its pulse");
        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        Check(other.Writes == 1 && other.Left > 0, "One-motor devices receive one combined level");
        GameManager.playerInput.controllers.Active = new Rewired.Controller();
        ControllerFeedback.Update(); Check(other.Stops == 1, "Switching to keyboard stops the controller");
        GameManager.playerInput.controllers.Active = device;
        device.supportsVibration = false; writes = device.Writes;
        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        Check(device.Writes == writes, "Unsupported devices are ignored");
        device.supportsVibration = true;
        ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
        AceOption.Plugin.ControllerVibration.Value = false; stops = device.Stops;
        ControllerFeedback.Update(); Check(device.Stops == stops + 1, "Live opt-out stops the motors");

        AceOption.Plugin.ControllerVibration.Value = true;
        foreach (Action interrupt in new Action[] {
            () => UnityEngine.Application.isFocused = false,
            () => aircraft.disabled = true, () => aircraft.remoteSim = true,
            () => aircraft.cockpit.Detached = true, () => aircraft.pilots[0].dead = true,
            () => cam.currentState = new object(), () => NuclearOption.UI.LeaderboardMenu.Open = true,
            () => GameManager.Local = Aircraft(), () => AceOption.Plugin.VibrationIntensity.Value = 0 })
        {
            GameManager.Local = aircraft; UnityEngine.Application.isFocused = true;
            aircraft.disabled = aircraft.remoteSim = aircraft.cockpit.Detached = aircraft.pilots[0].dead = false;
            cam.currentState = cam.chaseState; NuclearOption.UI.LeaderboardMenu.Open = false;
            AceOption.Plugin.VibrationIntensity.Value = .6f;
            ControllerFeedback.WeaponFired(station, 1); ControllerFeedback.Update();
            stops = device.Stops; interrupt(); ControllerFeedback.Update();
            Check(device.Stops == stops + 1, "Lifecycle and control interruptions stop feedback");
        }
        ControllerFeedback.Reset();
    }

    public static void Main()
    {
        TargetTests(); EnvelopeTests(); FeedbackTests();
        Console.WriteLine("PASS: " + checks + " optional targeting and controller feedback assertions");
    }
}

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x*x + y*y + z*z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    public class Transform { public Vector3 position; }
    public class GameObject { public bool activeInHierarchy = true; }
    public class Image { public Transform transform = new Transform(); public GameObject gameObject = new GameObject(); public bool enabled = true; }
    public class AudioClip { }
    public static class Screen { public static int width = 1000, height = 800; }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool isFocused = true; }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a,b); }
}
namespace Rewired
{
    public class Controller { }
    public class Joystick : Controller
    {
        public bool supportsVibration = true;
        public int vibrationMotorCount = 2, Writes, Stops;
        public float Left, Right, Timeout;
        public void SetVibration(int motor, float level, float timeout)
        { Writes++; if (motor == 0) Left = level; else Right = level; Timeout = timeout; }
        public void StopVibration() { Stops++; Left = Right = 0; }
    }
    public class ControllerHelper { public Controller Active; public Controller GetLastActiveController() => Active; }
    public class Player { public ControllerHelper controllers = new ControllerHelper(); }
}
namespace AceOption
{
    public class Setting<T> { public T Value; public Setting(T value) { Value = value; } }
    public static class Plugin
    {
        public static Setting<bool> TargetCycling = new Setting<bool>(false), ControllerVibration = new Setting<bool>(false),
            VibrationWeapons = new Setting<bool>(true), VibrationDamage = new Setting<bool>(true), VibrationGForce = new Setting<bool>(true);
        public static Setting<float> VibrationIntensity = new Setting<float>(.6f);
        public static Log Logger = new Log();
    }
    public class Log { public void LogWarning(string message) { } }
}
namespace AceOption.Helpers { internal static class CameraCommands { internal static bool InputAllowed = true; } }
namespace NuclearOption.UI { public static class LeaderboardMenu { public static bool Open; public static bool IsOpen() => Open; } }
public class SceneSingleton<T> { public static T i; }
public class Unit { public FactionHQ NetworkHQ; public bool disabled, Known = true, Excluded, Suitable = true; public Unit persistentID => this; }
public class Aircraft : Unit
{
    public bool remoteSim; public float gForce = 1; public Cockpit cockpit; public Pilot[] pilots;
    public WeaponManager weaponManager; public List<WeaponStation> weaponStations = new List<WeaponStation>();
}
public class Pilot { public bool dead; public object currentState, playerState = new object(); }
public class Cockpit { public bool Detached; public bool IsDetached() => Detached; }
public class UnitPart { public Unit parentUnit; public float hitPoints; }
public class FactionHQ
{
    public bool TryGetKnownPosition(Unit unit, out object position) { position = null; return unit.Known; }
    public Unit GetTrackingData(Unit unit) => unit;
}
public class WeaponInfo { public bool gun; }
public class WeaponStation { public WeaponInfo WeaponInfo; }
public class WeaponManager { public int Changes; public void TargetListChanged() { Changes++; } }
public class HUDUnitMarker
{
    public Unit unit; public bool outdated, selected; public UnityEngine.Image image;
    public void SelectMarker() { selected = true; }
}
public class TargetListSelector { public bool CheckExclusions(Unit unit) => unit.Excluded; }
public class CombatHUD
{
    public Aircraft aircraft; public UnityEngine.Image targetDesignator = new UnityEngine.Image();
    public List<Unit> Targets = new List<Unit>(); public int Clears;
    public WeaponStation GetWeaponStation() => new WeaponStation();
    public List<Unit> GetTargetList() => Targets;
    public void DeselectAll(bool sound)
    {
        Clears++; if (Targets.Count > 0) aircraft.weaponManager.TargetListChanged();
        Targets.Clear(); foreach(var marker in AllMarkers) marker.selected = false;
    }
    public static List<HUDUnitMarker> AllMarkers = new List<HUDUnitMarker>();
}
public class CameraStateManager { public Unit followingUnit; public object currentState, chaseState = new object(), cockpitState = new object(); }
public static class CombatAI
{
    public struct Analysis { public float opportunity; }
    public static Analysis AnalyzeTarget(WeaponStation station, Unit aircraft, Unit target, float a, float b, float c) =>
        new Analysis { opportunity = target.Suitable ? 1 : 0 };
}
public static class SoundManager { public static void PlayInterfaceOneShot(UnityEngine.AudioClip sound) { } }
public static class DynamicMap { public static bool mapMaximized; }
public static class GameManager
{
    public static Aircraft Local; public static Rewired.Player playerInput = new Rewired.Player();
    public static bool IsLocalAircraft(Aircraft aircraft) => aircraft == Local;
    public static bool GetLocalAircraft(out Aircraft aircraft) { aircraft = Local; return Local != null; }
}
