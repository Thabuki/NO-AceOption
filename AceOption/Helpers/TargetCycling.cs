using System.Collections.Generic;
using UnityEngine;

namespace AceOption.Helpers;

internal static class TargetCycling
{
    private sealed class Candidate
    {
        internal HUDUnitMarker Marker;
        internal float Distance;
        internal bool Suitable;
        internal int Order;
    }

    // False leaves the complete native selection routine in charge. In particular,
    // opting out never clears a target, changes a marker, or notifies the weapon manager.
    internal static bool TrySelect(CombatHUD hud, bool multiple, List<HUDUnitMarker> markers, AudioClip sound)
    {
        if (!Plugin.TargetCycling.Value || multiple || DynamicMap.mapMaximized ||
            !CameraCommands.InputAllowed || hud == null || hud.aircraft == null ||
            hud.aircraft.remoteSim || !GameManager.IsLocalAircraft(hud.aircraft)) return false;
        var aircraft = hud.aircraft;
        var selector = SceneSingleton<TargetListSelector>.i;
        if (markers == null || hud.targetDesignator == null || selector == null ||
            aircraft.NetworkHQ == null || aircraft.weaponManager == null) return false;

        var candidates = new List<Candidate>();
        var station = hud.GetWeaponStation();
        Vector3 center = hud.targetDesignator.transform.position;
        bool hasSuitable = false;
        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            var unit = marker != null ? marker.unit : null;
            if (unit == null || unit == aircraft || unit.disabled || unit.NetworkHQ == null ||
                unit.NetworkHQ == aircraft.NetworkHQ || marker.outdated || marker.image == null ||
                !marker.image.enabled || !marker.image.gameObject.activeInHierarchy ||
                selector.CheckExclusions(unit)) continue;
            // Use only the game's displayed contact and known position, never search
            // the scene for units or project an undetected unit's live transform.
            if (!aircraft.NetworkHQ.TryGetKnownPosition(unit, out _)) continue;
            Vector3 position = marker.image.transform.position;
            if (position.x < 0 || position.x > Screen.width || position.y < 0 || position.y > Screen.height) continue;
            bool suitable = station != null && CombatAI.AnalyzeTarget(station, aircraft,
                aircraft.NetworkHQ.GetTrackingData(unit.persistentID), 0, 1000, 100).opportunity > 0;
            candidates.Add(new Candidate { Marker = marker, Distance = (position - center).sqrMagnitude,
                Suitable = suitable, Order = i });
            hasSuitable |= suitable;
        }
        // Keep the current selection when there is nothing eligible to cycle to.
        if (candidates.Count == 0) return true;
        if (hasSuitable) candidates.RemoveAll(candidate => !candidate.Suitable);
        candidates.Sort((a, b) => {
            int order = a.Distance.CompareTo(b.Distance);
            return order != 0 ? order : a.Order.CompareTo(b.Order);
        });
        var targets = hud.GetTargetList();
        if (targets == null) return false;
        Unit current = targets.Count > 0 ? targets[0] : null;
        int index = candidates.FindIndex(candidate => candidate.Marker.unit == current);
        var next = candidates[(index + 1) % candidates.Count].Marker;
        if (targets.Count == 1 && current == next.unit) return true;
        // Native methods keep the map icons, HUD and weapon manager in sync.
        hud.DeselectAll(false);
        targets.Add(next.unit);
        next.SelectMarker();
        aircraft.weaponManager.TargetListChanged();
        SoundManager.PlayInterfaceOneShot(sound);
        return true;
    }
}
