using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using HarmonyLib;
using Rewired;
using UnityEngine;

internal static class MissileCameraTests
{
    private static int checks;
    private static Pilot pilot;
    private static Player player;
    private static CameraStateManager cam;
    private static WeaponStation station;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string path = Path.Combine(args[0], "BepInEx", "core", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        return Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        var harmony = new Harmony("AceOption.MissileCameraTests");
        try
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Firing();
            Following();
            DelayedLaunches();
            Interruptions();
            Console.WriteLine("PASS: " + checks + " missile single-fire/follow/cleanup assertions with real Harmony hooks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { MissileCamera.Release(); harmony.UnpatchSelf(); }
    }

    private static void Setup(bool cockpit = false)
    {
        MissileCamera.Release();
        Time.unscaledTime = 0;
        Application.isFocused = true;
        CameraCommands.InputAllowed = true;
        DynamicMap.mapMaximized = false;
        NuclearOption.UI.LeaderboardMenu.Open = false;
        AceOption.Plugin.MissileFollowCamera.Value = true;
        player = new Player();
        GameManager.playerInput = player;
        var aircraft = new Aircraft();
        pilot = new Pilot { aircraft = aircraft };
        pilot.playerState = new PilotPlayerState(pilot, player);
        pilot.currentState = pilot.playerState;
        aircraft.pilots = new[] { pilot };
        GameManager.Local = aircraft;
        station = new WeaponStation { WeaponInfo = new WeaponInfo { missile = true } };
        aircraft.weaponStations.Add(station);
        aircraft.weaponManager = new WeaponManager { aircraft = aircraft, currentWeaponStation = station };
        cam = new CameraStateManager { followingUnit = aircraft };
        SceneSingleton<CameraStateManager>.i = cam;
        cam.currentState = cockpit ? cam.cockpitState : cam.chaseState;
        cam.mainCamera.fieldOfView = 73;
    }

    private static void Controls(bool held, bool down = false)
    {
        player.Held = held;
        player.Down = down;
        pilot.playerState.PlayerControls();
    }

    private static Missile Spawn(int index = 0)
    {
        var launch = station.Launches[index];
        var spawned = new Missile { owner = launch.Owner, Info = launch.Info };
        spawned.StartMissile();
        return spawned;
    }

    private static void Tick(float delta)
    {
        Time.unscaledTime += delta;
        player.Down = false;
        cam.LateUpdate();
    }

    private static void Firing()
    {
        Setup();
        Controls(true, true);
        Check(station.Launches.Count == 1, "Native player entry launches one missile on press");
        for (int i = 0; i < 5; i++)
        {
            pilot.playerState.PlayerAxisControls();
            pilot.playerState.PlayerControls();
        }
        Check(station.Launches.Count == 1, "Repeated frame/physics polls consume a press once");
        Tick(.5f);
        Controls(true);
        Check(station.Launches.Count == 1, "Continuing the hold cannot repeat fire");
        Controls(false);
        Controls(true, true);
        Check(station.Launches.Count == 2, "Release and repress permit another missile");

        Setup();
        var first = new Unit();
        pilot.aircraft.weaponManager.Targets.Add(first);
        pilot.aircraft.weaponManager.Targets.Add(new Unit());
        Controls(true, true);
        Check(station.Launches.Count == 1 && station.Launches[0].Target == first, "Multiple marks launch once at the first target");
        Check(pilot.aircraft.weaponManager.FireCalls == 0, "Single launch bypasses the native salvo entry");

        foreach (string block in new[] { "empty", "cooldown", "safety", "salvo" })
        {
            Setup();
            station.Ammo = block == "empty" ? 0 : 20;
            station.Cooling = block == "cooldown";
            station.Safety = block == "safety";
            station.SalvoInProgress = block == "salvo";
            Controls(true, true);
            Check(station.Launches.Count == 0, block + " blocks launch");
            station.Ammo = 20;
            station.Cooling = station.Safety = station.SalvoInProgress = false;
            Controls(true);
            Check(station.Launches.Count == 0, block + " hold does not queue a later launch");
            Controls(false);
            Controls(true, true);
            Check(station.Launches.Count == 1, block + " recovers on a new press");
        }

        foreach (string weapon in new[] { "gun", "bomb", "other", "disabled" })
        {
            Setup();
            station.WeaponInfo.missile = weapon == "disabled";
            station.WeaponInfo.gun = weapon == "gun";
            station.WeaponInfo.bomb = weapon == "bomb";
            AceOption.Plugin.MissileFollowCamera.Value = weapon != "disabled";
            Controls(true, true);
            Controls(true);
            Check(pilot.aircraft.weaponManager.FireCalls == 2, weapon + " retains native held Fire");
            Check(station.Launches.Count == 0, weapon + " never uses the missile override");
        }

        Setup();
        station.WeaponInfo.gun = true;
        station.WeaponInfo.missile = false;
        Controls(true, true);
        station.WeaponInfo.gun = false;
        station.WeaponInfo.missile = true;
        Controls(true);
        Check(station.Launches.Count == 0, "Changing from gun to missile during a hold needs a new press");
        Controls(false);
        Controls(true, true);
        var otherStation = new WeaponStation { WeaponInfo = new WeaponInfo { missile = true } };
        pilot.aircraft.weaponManager.currentWeaponStation = otherStation;
        Controls(true);
        Check(otherStation.Launches.Count == 0, "Changing missile stations cannot fire twice during one press");
    }

    private static Missile BeginFollowing(bool cockpit = false)
    {
        Setup(cockpit);
        Controls(true, true);
        var spawned = Spawn();
        Tick(.31f);
        Check(cam.followingUnit == spawned && cam.currentState == cam.orbitState, "Held Fire follows the actual launched missile");
        Check(MissileCamera.ActiveFor(cam), "Temporary follow owns this camera");
        return spawned;
    }

    private static void Following()
    {
        foreach (bool cockpit in new[] { false, true })
        {
            Setup(cockpit);
            var original = cam.currentState;
            Controls(true, true);
            var spawned = Spawn();
            Tick(.29f);
            Check(cam.followingUnit == pilot.aircraft && cam.currentState == original, "Short taps retain the current aircraft view");
            Tick(.02f);
            Check(cam.followingUnit == spawned && cam.currentState == cam.orbitState, "Hold threshold starts missile follow");
            Check(cam.orbitState.InputCalls == 1, "Only the aircraft-view frame can reach orbit inputs before follow");
            Tick(1);
            Check(cam.followingUnit == spawned && cam.orbitState.InputCalls == 1, "Spectator movement cannot detach the held missile camera");
            cam.mainCamera.fieldOfView = 45;
            Controls(false);
            Check(cam.followingUnit == pilot.aircraft && cam.currentState == original, "Release restores the previous cockpit/chase view");
            Check(cam.mainCamera.fieldOfView == 73 && cam.DesiredFov == 73, "Release restores the previous FOV");
            Check(!MissileCamera.ActiveFor(cam), "Release clears camera ownership");
        }

        var missile = BeginFollowing();
        missile.disabled = true;
        cam.Cam_OnFollowingUnitDisabled(missile);
        Check(cam.followingUnit == pilot.aircraft && cam.currentState == cam.chaseState, "Impact restores aircraft before native subject clearing");
        Check(cam.DisabledCalls == 0, "Missile impact does not run native free-camera fallback");
        Controls(true);
        Check(station.Launches.Count == 1, "Impact while held cannot fire again");

        BeginFollowing();
        cam.SwitchState(cam.cockpitState);
        Check(cam.followingUnit == pilot.aircraft && cam.currentState == cam.cockpitState, "An explicit view switch operates on the aircraft");
        Tick(1);
        Check(!MissileCamera.ActiveFor(cam), "An explicit view switch cancels follow until a new shot");

        BeginFollowing();
        var spectator = new Unit();
        cam.SetFollowingUnit(spectator);
        Tick(1);
        Check(cam.followingUnit == spectator && !MissileCamera.ActiveFor(cam), "Explicit subject selection takes camera ownership");

        Setup();
        Controls(true, true);
        Controls(false);
        Spawn();
        Tick(1);
        Check(cam.followingUnit == pilot.aircraft, "A released tap never starts follow when its spawn arrives");
    }

    private static void DelayedLaunches()
    {
        Setup();
        Controls(true, true);
        Tick(.5f);
        Check(cam.followingUnit == pilot.aircraft, "A delayed spawn keeps the aircraft camera");
        var spawned = Spawn();
        Tick(.01f);
        Check(cam.followingUnit == spawned, "A delayed rail/network spawn can start follow while still held");

        Setup();
        Controls(true, true);
        Controls(false);
        Time.unscaledTime = .1f;
        Controls(true, true);
        var earlier = Spawn(0);
        Tick(.31f);
        Check(cam.followingUnit == pilot.aircraft, "A previous tap's delayed spawn cannot steal a later hold");
        var latest = Spawn(1);
        Tick(.01f);
        Check(cam.followingUnit == latest && latest != earlier, "The later hold follows its own missile");

        Setup();
        Controls(true, true);
        new Missile { owner = new Aircraft(), Info = station.WeaponInfo }.StartMissile();
        new Missile { owner = pilot.aircraft, Info = new WeaponInfo { missile = true } }.StartMissile();
        Tick(.5f);
        Check(cam.followingUnit == pilot.aircraft, "Other owners and weapon types cannot claim the pending shot");
        Spawn();
        Tick(.01f);
        Check(MissileCamera.ActiveFor(cam), "The matching owner and weapon can claim the pending shot");

        Setup();
        Controls(true, true);
        Tick(11);
        Spawn();
        Tick(.1f);
        Check(!MissileCamera.ActiveFor(cam), "Expired spawn requests cannot activate a stale camera");
    }

    private static void Interruptions()
    {
        foreach (string reason in new[] { "menu", "focus", "leaderboard", "option", "dead", "detached", "remote", "ownership", "state", "strength", "missile", "inactive", "leave" })
        {
            var followed = BeginFollowing();
            switch (reason)
            {
                case "menu": CameraCommands.InputAllowed = false; break;
                case "focus": Application.isFocused = false; break;
                case "leaderboard": NuclearOption.UI.LeaderboardMenu.Open = true; break;
                case "option": AceOption.Plugin.MissileFollowCamera.Value = false; break;
                case "dead": pilot.dead = true; break;
                case "detached": pilot.aircraft.cockpit.Detached = true; break;
                case "remote": pilot.aircraft.remoteSim = true; break;
                case "ownership": GameManager.Local = new Aircraft(); break;
                case "state": pilot.currentState = new object(); break;
                case "strength": pilot.playerState.Strength = .1f; pilot.playerState.PlayerAxisControls(); break;
                case "missile": followed.disabled = true; break;
                case "inactive": followed.gameObject.activeInHierarchy = false; break;
                case "leave": pilot.playerState.LeaveState(); break;
            }
            MissileCamera.Update();
            Check(!MissileCamera.ActiveFor(cam) && cam.followingUnit == pilot.aircraft, reason + " ends temporary missile follow");
            Check(station.Launches.Count == 1, reason + " cannot create a second shot");
        }

        Setup();
        Controls(true, false);
        Check(station.Launches.Count == 0, "An already-held Fire on entry needs release/repress");
        Controls(false);
        Controls(true, true);
        Check(station.Launches.Count == 1, "A fresh press after entry works");

        Setup();
        CameraCommands.InputAllowed = false;
        Controls(true, true);
        player.Down = false;
        CameraCommands.InputAllowed = true;
        Controls(true);
        Check(station.Launches.Count == 0, "Closing a menu while holding Fire cannot launch");
        Controls(false);
        Controls(true, true);
        Check(station.Launches.Count == 1, "Menu interruption recovers on a new press");

        BeginFollowing();
        DynamicMap.mapMaximized = true;
        CameraCommands.InputAllowed = false;
        MissileCamera.Update();
        Check(cam.followingUnit == pilot.aircraft && DynamicMap.mapMaximized,
            "Restoring the aircraft view preserves an opened full map");
        SceneSingleton<DynamicMap>.i.Minimize();
        Check(!DynamicMap.mapMaximized, "Normal map minimization remains available after restoration");
    }
}

namespace AceOption
{
    public sealed class Toggle { public bool Value = true; }
    public static class Plugin
    {
        public static readonly Toggle MissileFollowCamera = new Toggle();
        public static readonly TestLogger Logger = new TestLogger();
    }
    public sealed class TestLogger { public void LogError(string text) { } }
}

namespace AceOption.Helpers
{
    internal static class CameraCommands { internal static bool InputAllowed = true; }
}

namespace UnityEngine
{
    public sealed class GameObject { public bool activeInHierarchy = true; }
    public sealed class Transform { public Vector3 forward = new Vector3(); }
    public struct Vector3 { public static Vector3 operator *(Vector3 value, float scale) => value; }
    public sealed class Camera { public float fieldOfView; }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool isFocused = true; }
}

namespace Rewired
{
    public sealed class Player
    {
        public bool Held, Down;
        public bool GetButton(string name) => Held;
        public bool GetButtonDown(string name) => Down;
    }
}

namespace NuclearOption.UI
{
    public static class LeaderboardMenu { public static bool Open; public static bool IsOpen() => Open; }
}

public class Unit
{
    public bool disabled, remoteSim;
    public Transform transform = new Transform();
    public GameObject gameObject = new GameObject();
}
public sealed class UnitPart { public bool Detached; public bool IsDetached() => Detached; }
public sealed class Aircraft : Unit
{
    public UnitPart cockpit = new UnitPart();
    public Pilot[] pilots;
    public List<WeaponStation> weaponStations = new List<WeaponStation>();
    public WeaponManager weaponManager;
}
public struct GlobalPosition
{
    public static GlobalPosition operator +(GlobalPosition position, Vector3 offset) => position;
}
public static class GlobalPositionExtensions { public static GlobalPosition GlobalPosition(this Unit unit) => new GlobalPosition(); }
public sealed class WeaponInfo { public bool missile, gun, bomb; }
public sealed class WeaponStation
{
    public sealed class Launch { public Unit Owner, Target; public WeaponInfo Info; }
    public WeaponInfo WeaponInfo;
    public bool Safety, Cooling, SalvoInProgress;
    public int Ammo = 20;
    public readonly List<Launch> Launches = new List<Launch>();
    public bool SafetyIsOn(Aircraft aircraft) => Safety;
    public bool Ready() => Ammo > 0 && !Cooling;
    public void LaunchMount(Unit owner, Unit target, GlobalPosition aimpoint)
    {
        Ammo--;
        Launches.Add(new Launch { Owner = owner, Target = target, Info = WeaponInfo });
    }
}
public sealed class WeaponManager
{
    public Aircraft aircraft;
    public WeaponStation currentWeaponStation;
    public List<Unit> Targets = new List<Unit>();
    public int FireCalls;
    public List<Unit> GetTargetList() => Targets;
    public void Fire() { FireCalls++; }
}
public sealed class Pilot
{
    public Aircraft aircraft;
    public bool dead;
    public object currentState;
    public PilotPlayerState playerState;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Fire() => aircraft.weaponManager.Fire();
}
public sealed class PilotPlayerState
{
    private Pilot pilot;
    private Player player;
    private float pilotStrength = 1;
    public float Strength { set { pilotStrength = value; } }
    public PilotPlayerState(Pilot pilot, Player player) { this.pilot = pilot; this.player = player; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PlayerControls() { if (pilotStrength >= .2f && player.GetButton("Fire")) pilot.Fire(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void PlayerAxisControls() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void EnterState() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LeaveState() { }
}
public sealed class Missile : Unit
{
    public Unit owner;
    public WeaponInfo Info;
    public WeaponInfo GetWeaponInfo() => Info;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void StartMissile() { }
}
public class CameraBaseState { }
public sealed class CameraOrbitState : CameraBaseState
{
    public int InputCalls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Inputs(CameraStateManager cam)
    {
        InputCalls++;
        if (cam.followingUnit is Missile) cam.SetFollowingUnit(null);
    }
}
public sealed class CameraStateManager
{
    public Unit followingUnit;
    public CameraBaseState currentState, cockpitState = new CameraBaseState(), chaseState = new CameraBaseState(), freeState = new CameraBaseState();
    public CameraOrbitState orbitState = new CameraOrbitState();
    public Camera mainCamera = new Camera();
    public float DesiredFov;
    public int DisabledCalls;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SwitchState(CameraBaseState state) { currentState = state; SceneSingleton<DynamicMap>.i.Minimize(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetFollowingUnit(Unit unit) { followingUnit = unit; SwitchState(unit == null ? freeState : orbitState); }
    public void SetDesiredFoV(float min, float max) { DesiredFov = max; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LateUpdate() { orbitState.Inputs(this); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Cam_OnFollowingUnitDisabled(Unit unit) { DisabledCalls++; SetFollowingUnit(null); }
}
public static class SceneSingleton<T> { public static T i; }
public sealed class DynamicMap
{
    public static bool mapMaximized;
    static DynamicMap() { SceneSingleton<DynamicMap>.i = new DynamicMap(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Minimize() { mapMaximized = false; }
}
public static class GameManager
{
    public static Player playerInput;
    public static Aircraft Local;
    public static bool IsLocalAircraft(Unit unit) => unit == Local;
}
