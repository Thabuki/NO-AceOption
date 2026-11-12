using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AceOption.Helpers;

// Persistent hold indicators are separate from AircraftActionsReport's timed
// message feed. Nothing is queued, and native warnings/messages remain intact.
internal static class FlightStatusHud
{
    private static readonly AccessTools.FieldRef<AircraftActionsReport, TextMeshProUGUI> actionText =
        AccessTools.FieldRefAccess<AircraftActionsReport, TextMeshProUGUI>("actionsText");
    private static readonly AccessTools.FieldRef<FlightHud, Canvas> hudCanvas =
        AccessTools.FieldRefAccess<FlightHud, Canvas>("canvas");
    private static TextMeshProUGUI label;
    private static Canvas owner;

    internal static void Refresh()
    {
        var hud = SceneSingleton<FlightHud>.i;
        var cam = SceneSingleton<CameraStateManager>.i;
        if (!Application.isFocused || !CameraCommands.InputAllowed || hud == null || cam == null ||
            (cam.currentState != cam.cockpitState && cam.currentState != cam.chaseState) ||
            !GameManager.GetLocalAircraft(out var aircraft) || aircraft == null || aircraft.disabled ||
            cam.followingUnit != aircraft)
        { Hide(); return; }

        bool leveling = AutoLevelCommands.ActiveFor(aircraft);
        bool assist = FlightAssistCommands.HeldFor(aircraft);
        if (!leveling && !assist) { Hide(); return; }
        var canvas = hudCanvas(hud);
        var report = SceneSingleton<AircraftActionsReport>.i;
        var source = report != null ? actionText(report) : null;
        if (canvas == null || !canvas.isActiveAndEnabled || source == null) { Hide(); return; }
        if (label == null || owner != canvas)
        {
            Release();
            owner = canvas;
            var obj = new GameObject("AceOption Flight Status", typeof(RectTransform));
            obj.layer = canvas.gameObject.layer;
            obj.transform.SetParent(canvas.transform, false);
            label = obj.AddComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            // Keep both status lines above the camera-centered HUD instruments.
            rect.anchorMin = new Vector2(0, .9f);
            rect.anchorMax = new Vector2(1, .9f);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            label.alignment = TextAlignmentOptions.Top;
            label.raycastTarget = false;
            label.richText = false;
            label.enableWordWrapping = false;
        }
        // Follow native theme/font changes without cloning message-feed scripts.
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        // Keep held-state feedback quieter than the native action messages.
        label.fontSize = source.fontSize * .85f;
        label.fontStyle = source.fontStyle;
        label.color = source.color;
        label.rectTransform.sizeDelta = new Vector2(-32, Mathf.Max(80, label.fontSize * 3));
        label.text = leveling ? (assist ? "AUTO-LEVEL\nFLIGHT ASSIST OFF" : "AUTO-LEVEL") : "FLIGHT ASSIST OFF";
        label.gameObject.SetActive(true);
    }

    internal static void Hide() { if (label != null) label.gameObject.SetActive(false); }
    internal static void Release()
    {
        if (label != null) { label.gameObject.SetActive(false); Object.Destroy(label.gameObject); }
        label = null;
        owner = null;
    }
}
