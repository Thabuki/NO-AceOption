using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class MissileCamera
{
    internal const float HoldDelay = .3f;
    private const float SpawnTimeout = 10f;

    private sealed class Launch
    {
        internal Aircraft Aircraft;
        internal WeaponInfo Info;
        internal float PressTime;
    }

    // Keep cancelled taps until their delayed rail/network spawns arrive. A later
    // hold must never pick up a missile requested by an earlier tap.
    private static readonly List<Launch> pending = new List<Launch>();
    private static Launch currentLaunch;
    private static Pilot owner;
    private static Player input;
    private static bool wasHeld, pressAvailable, switching;
    private static float pressTime;
    private static Missile missile;
    private static CameraStateManager camera;
    private static CameraBaseState returnState;
    private static Aircraft returnAircraft;
    private static float returnFov;
    internal static bool PreserveMap { get; private set; }

    private static bool Allowed(Pilot pilot, Player player, float strength) =>
        Plugin.MissileFollowCamera.Value && CameraCommands.InputAllowed && Application.isFocused &&
        !NuclearOption.UI.LeaderboardMenu.IsOpen() && player != null && player == GameManager.playerInput &&
        pilot != null && !pilot.dead && pilot.currentState == pilot.playerState && strength >= .2f &&
        pilot.aircraft != null && !pilot.aircraft.disabled && !pilot.aircraft.remoteSim &&
        GameManager.IsLocalAircraft(pilot.aircraft) && pilot.aircraft.cockpit != null &&
        !pilot.aircraft.cockpit.IsDetached();

    internal static bool ActiveFor(CameraStateManager cam) => camera != null && camera == cam;

    internal static void Poll(Pilot pilot, Player player, float strength)
    {
        if (!Allowed(pilot, player, strength))
        {
            Reset();
            return;
        }
        if (owner != pilot || input != player)
        {
            Reset();
            owner = pilot;
            input = player;
            // Entering an aircraft or enabling the option during a hold needs a fresh press.
            wasHeld = player.GetButton("Fire") && !player.GetButtonDown("Fire");
        }
        bool held = player.GetButton("Fire");
        if (held && !wasHeld && player.GetButtonDown("Fire"))
        {
            pressAvailable = true;
            pressTime = Time.unscaledTime;
        }
        wasHeld = held;
        if (!held)
        {
            pressAvailable = false;
            CancelFollow();
        }
    }

    // Replaces only the native player Fire call. Guns and other weapon types
    // continue through Pilot.Fire, including its normal linked-gun behavior.
    public static void Fire(Pilot pilot)
    {
        var aircraft = pilot.aircraft;
        var manager = aircraft?.weaponManager;
        var station = manager?.currentWeaponStation;
        var info = station?.WeaponInfo;
        if (!Plugin.MissileFollowCamera.Value || info == null || !info.missile || info.gun || info.bomb)
        {
            if (owner == pilot) pressAvailable = false;
            pilot.Fire();
            return;
        }
        if (owner != pilot || !pressAvailable || !Allowed(pilot, input, 1f)) return;
        // Consume even an empty, cooling or safety-blocked press. Holding cannot
        // queue a later launch, change station and fire again, or start a salvo.
        pressAvailable = false;
        CancelFollow();
        if (aircraft.weaponStations == null || aircraft.weaponStations.Count == 0 ||
            station.SafetyIsOn(aircraft) || !station.Ready() || station.SalvoInProgress) return;

        var cam = SceneSingleton<CameraStateManager>.i;
        var launch = new Launch { Aircraft = aircraft, Info = info, PressTime = pressTime };
        pending.Add(launch);
        if (cam != null && cam.followingUnit == aircraft &&
            (cam.currentState == cam.cockpitState || cam.currentState == cam.chaseState))
            currentLaunch = launch;

        var targets = manager.GetTargetList();
        try
        {
            // Native LaunchMount releases one mount and retains rail/bay delays,
            // ammo accounting and the aircraft's existing multiplayer launch path.
            station.LaunchMount(aircraft, targets != null && targets.Count > 0 ? targets[0] : null,
                aircraft.GlobalPosition() + aircraft.transform.forward * 50000f);
        }
        catch
        {
            pending.Remove(launch);
            CancelFollow();
            throw;
        }
    }

    internal static void Launched(Missile launched)
    {
        if (launched == null) return;
        ExpirePending();
        for (int i = 0; i < pending.Count; i++)
        {
            var launch = pending[i];
            if (launched.owner != launch.Aircraft || launched.GetWeaponInfo() != launch.Info) continue;
            pending.RemoveAt(i);
            if (launch == currentLaunch && Allowed(owner, input, 1f) && input.GetButton("Fire"))
                missile = launched;
            return;
        }
    }

    private static void ExpirePending() => pending.RemoveAll(launch =>
        launch.Aircraft == null || Time.unscaledTime - launch.PressTime > SpawnTimeout);

    // Called before native camera motion, so its origin-shift and presentation
    // work use the missile pose. Update also handles pause and lost input promptly.
    internal static void Update()
    {
        ExpirePending();
        if (!Allowed(owner, input, 1f) || !input.GetButton("Fire"))
        {
            Reset();
            return;
        }
        if (currentLaunch == null) return;
        if (owner.aircraft != currentLaunch.Aircraft ||
            (Time.unscaledTime - currentLaunch.PressTime > SpawnTimeout && missile == null))
        {
            CancelFollow();
            return;
        }
        var cam = SceneSingleton<CameraStateManager>.i;
        if (camera != null)
        {
            if (cam != camera || missile == null || missile.disabled || !missile.gameObject.activeInHierarchy ||
                cam.followingUnit != missile || cam.currentState != cam.orbitState)
                CancelFollow();
            return;
        }
        if (missile == null || missile.disabled || !missile.gameObject.activeInHierarchy ||
            Time.unscaledTime - currentLaunch.PressTime < HoldDelay) return;
        if (cam == null || cam.mainCamera == null || cam.followingUnit != currentLaunch.Aircraft ||
            (cam.currentState != cam.cockpitState && cam.currentState != cam.chaseState))
        {
            CancelFollow();
            return;
        }
        camera = cam;
        returnAircraft = currentLaunch.Aircraft;
        returnState = cam.currentState;
        returnFov = cam.mainCamera.fieldOfView;
        switching = true;
        try { cam.SetFollowingUnit(missile); }
        finally { switching = false; }
    }

    internal static void BeforeSwitch(CameraStateManager cam)
    {
        if (!switching && ActiveFor(cam)) CancelFollow();
    }

    internal static void BeforeFollowingUnit(CameraStateManager cam)
    {
        // An explicit spectator/subject change takes ownership of the camera.
        if (!switching && ActiveFor(cam))
        {
            camera = null;
            CancelFollow();
        }
    }

    internal static bool FollowingUnitDisabled(CameraStateManager cam, Unit unit)
    {
        if (!ActiveFor(cam) || unit != missile) return true;
        CancelFollow();
        return false;
    }

    private static void CancelFollow()
    {
        var cam = camera;
        var aircraft = returnAircraft;
        var state = returnState;
        var followed = missile;
        camera = null;
        returnAircraft = null;
        returnState = null;
        missile = null;
        currentLaunch = null;
        if (cam == null || cam != SceneSingleton<CameraStateManager>.i ||
            !ReferenceEquals(cam.followingUnit, followed)) return;
        switching = true;
        PreserveMap = DynamicMap.mapMaximized;
        try
        {
            cam.SetFollowingUnit(aircraft);
            if (aircraft != null && !aircraft.disabled && !aircraft.remoteSim && aircraft.cockpit != null &&
                !aircraft.cockpit.IsDetached() && aircraft.pilots != null && aircraft.pilots.Length > 0 &&
                aircraft.pilots[0] != null && !aircraft.pilots[0].dead &&
                aircraft.pilots[0].currentState == aircraft.pilots[0].playerState && GameManager.IsLocalAircraft(aircraft))
            {
                cam.SwitchState(state);
                cam.SetDesiredFoV(returnFov, returnFov);
                cam.mainCamera.fieldOfView = returnFov;
            }
        }
        finally { switching = false; PreserveMap = false; }
    }

    internal static void Reset()
    {
        CancelFollow();
        owner = null;
        input = null;
        pressAvailable = false;
        wasHeld = true;
    }

    internal static void Release()
    {
        Reset();
        pending.Clear();
    }
}
