using System;
using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class ControllerFeedback
{
    private static readonly VibrationEnvelope envelope = new VibrationEnvelope();
    private static Aircraft owner;
    private static Joystick device;
    private static bool driving, reportedError;

    private static bool Allowed(out Aircraft aircraft)
    {
        aircraft = null;
        if (!Plugin.ControllerVibration.Value || Plugin.VibrationIntensity.Value <= 0 ||
            !Application.isFocused || !CameraCommands.InputAllowed ||
            NuclearOption.UI.LeaderboardMenu.IsOpen() || !GameManager.GetLocalAircraft(out aircraft) ||
            aircraft == null || aircraft.disabled || aircraft.remoteSim || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached()) return false;
        var cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.followingUnit != aircraft ||
            (cam.currentState != cam.chaseState && cam.currentState != cam.cockpitState)) return false;
        if (aircraft.pilots != null)
            foreach (var pilot in aircraft.pilots)
                if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) return true;
        return false;
    }

    internal static void WeaponFired(WeaponStation station, int count)
    {
        if (count <= 0 || !Plugin.VibrationWeapons.Value || !Allowed(out var aircraft) ||
            station == null || station.WeaponInfo == null || aircraft.weaponStations == null ||
            !aircraft.weaponStations.Contains(station)) return;
        Pulse(aircraft, station.WeaponInfo.gun ? VibrationEffect.Gun : VibrationEffect.Ordnance);
    }

    internal static void Damaged(UnitPart part, float previousHitPoints)
    {
        if (!Plugin.VibrationDamage.Value || part == null || !(part.hitPoints < previousHitPoints) ||
            !Allowed(out var aircraft) || part.parentUnit != aircraft) return;
        Pulse(aircraft, VibrationEffect.Damage);
    }

    private static void Pulse(Aircraft aircraft, VibrationEffect effect)
    {
        UseContext(aircraft);
        if (device != null) envelope.Fire(effect, Time.unscaledTime);
    }

    private static void UseContext(Aircraft aircraft)
    {
        var active = GameManager.playerInput.controllers.GetLastActiveController() as Joystick;
        if (owner == aircraft && ReferenceEquals(device, active)) return;
        Reset(); owner = aircraft; device = active;
    }

    internal static void Update()
    {
        if (!Allowed(out var aircraft)) { Reset(); return; }
        // Sending through Player.SetVibration would shake every assigned joystick.
        // Use only the active device and stop the previous one on device changes.
        UseContext(aircraft);
        if (device == null) { envelope.Reset(); return; }
        try
        {
            if (!device.supportsVibration || device.vibrationMotorCount < 1) { Stop(); envelope.Reset(); return; }
            envelope.Sample(Time.unscaledTime, aircraft.gForce, Plugin.VibrationIntensity.Value,
                Plugin.VibrationWeapons.Value, Plugin.VibrationDamage.Value, Plugin.VibrationGForce.Value,
                out float left, out float right);
            if (left <= 0 && right <= 0) { Stop(); return; }
            // Timeout also stops the hardware if updates are interrupted unexpectedly.
            driving = true;
            if (device.vibrationMotorCount == 1) device.SetVibration(0, Mathf.Max(left, right), .15f);
            else
            {
                device.SetVibration(0, left, .15f);
                device.SetVibration(1, right, .15f);
            }
        }
        catch (Exception error) { Stop(); Report(error); }
    }

    private static void Report(Exception error)
    {
        if (reportedError) return;
        reportedError = true;
        Plugin.Logger.LogWarning("Controller vibration unavailable: " + error.Message);
    }
    private static void Stop()
    {
        if (!driving) return;
        driving = false;
        try { device?.StopVibration(); }
        catch (Exception error) { Report(error); }
    }
    internal static void Reset()
    {
        Stop(); device = null; owner = null; envelope.Reset();
    }
}
