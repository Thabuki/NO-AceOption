using System;
using System.Collections.Generic;
using AceOption.Helpers;

internal static class OptionalCombatTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static HUDUnitMarker Marker(Unit unit, float x, float y = 400) =>
        new HUDUnitMarker { unit = unit, image = new UnityEngine.Image { transform = new UnityEngine.Transform {
            position = new UnityEngine.Vector3(x, y, 0) } } };

    private static Aircraft Aircraft()
    {
        return new Aircraft { NetworkHQ = new FactionHQ(), weaponManager = new WeaponManager() };
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

    public static void Main()
    {
        TargetTests();
        Console.WriteLine("PASS: " + checks + " optional targeting assertions");
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
}
namespace AceOption
{
    public class Setting<T> { public T Value; public Setting(T value) { Value = value; } }
    public static class Plugin
    {
        public static Setting<bool> TargetCycling = new Setting<bool>(false);
    }
}
namespace AceOption.Helpers { internal static class CameraCommands { internal static bool InputAllowed = true; } }
public class SceneSingleton<T> { public static T i; }
public class Unit { public FactionHQ NetworkHQ; public bool disabled, Known = true, Excluded, Suitable = true; public Unit persistentID => this; }
public class Aircraft : Unit
{
    public bool remoteSim;
    public WeaponManager weaponManager;
}
public class FactionHQ
{
    public bool TryGetKnownPosition(Unit unit, out object position) { position = null; return unit.Known; }
    public Unit GetTrackingData(Unit unit) => unit;
}
public class WeaponStation { }
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
    public static Aircraft Local;
    public static bool IsLocalAircraft(Aircraft aircraft) => aircraft == Local;
}
