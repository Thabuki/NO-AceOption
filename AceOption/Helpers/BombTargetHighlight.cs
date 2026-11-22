using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AceOption.Helpers;

internal static class BombTargetHighlight
{
    private static Image highlighted;
    private static Color originalColor, appliedColor;

    internal static void Restore()
    {
        // Do not overwrite a newer native/theme color written after our tint.
        if (highlighted != null && highlighted.color == appliedColor)
            highlighted.color = originalColor;
        highlighted = null;
    }

    internal static void Apply(Image circle, Aircraft aircraft, List<HUDUnitMarker> contacts)
    {
        Restore();
        if (!Plugin.BombTargetHighlight.Value || circle == null || !circle.isActiveAndEnabled ||
            circle.color.a <= 0 || aircraft == null || aircraft.disabled || aircraft.remoteSim ||
            !GameManager.IsLocalAircraft(aircraft) || DynamicMap.mapMaximized || contacts == null) return;

        var cam = SceneSingleton<CameraStateManager>.i;
        var faction = aircraft.NetworkHQ;
        if (cam == null || cam.mainCamera == null || cam.followingUnit != aircraft || faction == null ||
            (cam.currentState != cam.cockpitState && cam.currentState != cam.chaseState)) return;

        var rect = circle.rectTransform;
        Rect bounds = rect.rect;
        float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
        if (!(radius > 0)) return;
        var canvas = circle.canvas;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        foreach (var marker in contacts)
        {
            var unit = marker != null ? marker.unit : null;
            if (unit == null || unit == aircraft || unit.disabled || unit.NetworkHQ == null ||
                unit.NetworkHQ == faction) continue;
            // Contacts, not the marked list. Never read an enemy's live transform
            // or the marker position: only the faction's currently tracked position.
            if (!faction.IsTargetBeingTracked(unit) ||
                !faction.TryGetKnownPosition(unit, out var known)) continue;
            Vector3 screen = cam.mainCamera.WorldToScreenPoint(known.ToLocalPosition());
            if (!(screen.z > 0) || screen.x < 0 || screen.x > Screen.width ||
                screen.y < 0 || screen.y > Screen.height) continue;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                new Vector2(screen.x, screen.y), uiCamera, out var local)) continue;
            if (!((local - bounds.center).sqrMagnitude <= radius * radius)) continue;

            highlighted = circle;
            originalColor = circle.color;
            appliedColor = new Color(1f, 0f, 0f, originalColor.a);
            circle.color = appliedColor;
            return;
        }
    }
}
