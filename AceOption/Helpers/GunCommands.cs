using System.Collections.Generic;
using Rewired;

namespace AceOption.Helpers;

internal static class GunCommands
{
    private static bool IsGun(WeaponStation station) => station != null &&
        station.WeaponInfo != null && station.WeaponInfo.gun;

    internal static bool Held(Pilot pilot, Player player, float pilotStrength) =>
        Plugin.FireGunsActionId >= 0 && player != null && CameraCommands.InputAllowed &&
        pilot != null && !pilot.dead && pilot.currentState == pilot.playerState &&
        pilotStrength >= .2f && player.GetButton(Plugin.FireGunsActionId);

    internal static void Fire(Pilot pilot, bool linked, IList<Unit> targets)
    {
        var aircraft = pilot.aircraft;
        if (aircraft == null || aircraft.remoteSim || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached()) return;
        var manager = aircraft.weaponManager;
        if (manager == null || aircraft.weaponStations == null) return;
        if (linked)
        {
            // Native linked-gun firing handles fixed guns but skips turret stations.
            manager.FireGuns();
            foreach (var station in aircraft.weaponStations)
                if (IsGun(station) && station.TurretCount() > 0 && station.Ready())
                    station.Fire(aircraft, targets != null && targets.Count > 0 ? targets[0] : null);
            return;
        }

        // Keep the dedicated trigger independent of normal Fire's weapon selection.
        // Prefer the cheapest ammunition per round, including turret guns.
        // Equal prices retain native weapon order; readiness never changes the choice.
        WeaponStation gun = null;
        foreach (var station in aircraft.weaponStations)
            if (IsGun(station) && (gun == null ||
                station.WeaponInfo.costPerRound < gun.WeaponInfo.costPerRound))
                gun = station;
        // Do not fall through to another gun when the chosen type is empty or cooling down.
        // Gun.Fire requests a trigger hold; native fixed-update simulation controls cadence
        // even if normal Fire also requested the same station during this frame.
        if (gun != null && gun.Ready())
            gun.Fire(aircraft, targets != null && targets.Count > 0 ? targets[0] : null);
    }
}
