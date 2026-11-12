using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Helpers;

internal static class HudPresentation
{
    private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
    private static object Read(FlightHud hud, string name)
    {
        if (!Fields.TryGetValue(name, out var field)) Fields[name] = field = AccessTools.Field(typeof(FlightHud), name);
        return field?.GetValue(hud);
    }
    internal static void Refresh()
    {
        var hud = SceneSingleton<FlightHud>.i;
        bool active = hud != null && (!(Read(hud, "canvas") is Component canvas) || canvas.gameObject.activeSelf);
        bool desired = Plugin.FlightHud.Value && !DynamicMap.mapMaximized;
        if (hud != null && active != desired) FlightHud.EnableCanvas(desired);
        if (DynamicMap.mapMaximized) { MapWidgets(true); return; }
        if (!Plugin.FlightHud.Value) return;
        MapWidgets(Plugin.Map.Value);
        Elements(true);
    }
    internal static void Release() { MapWidgets(true); Elements(false); }
    private static void Elements(bool chase)
    {
        var hud = SceneSingleton<FlightHud>.i;
        if (hud == null) return;
        if (Read(hud, "pitchCompassCenter") is GameObject ladder)
            ladder.SetActive(!chase || Plugin.PitchLadder.Value);
        SetGraphic(Read(hud, "waterline"), !chase || Plugin.Waterline.Value);
        SetGraphic(Read(hud, "virtualJoystickPos"), !chase || Plugin.VirtualJoystickIndicator.Value);
        SetGraphic(Read(hud, "virtualJoystickVector"), !chase || Plugin.VirtualJoystickIndicator.Value);
        if (chase) HudElements.Apply(Plugin.LeftInstruments.Value, Plugin.RightInstruments.Value, Plugin.Compass.Value);
        else HudElements.ShowAll();
    }
    private static void SetGraphic(object value, bool show)
    {
        if (value is Behaviour graphic && graphic.enabled != show) graphic.enabled = show;
    }
    private static void MapWidgets(bool visible)
    {
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null) return;
        var anchor = map.transform.parent;
        var panel = anchor != null ? anchor.parent : null;
        if (anchor != null && anchor.gameObject.activeSelf != visible) anchor.gameObject.SetActive(visible);
        if (visible && !map.gameObject.activeSelf) DynamicMap.EnableCanvas(true);
        if (panel == null) return;
        // Leave the threat-warning children alive when hiding the map background.
        foreach (var component in panel.GetComponents<Component>())
            if (component is Behaviour image && component.GetType().Name == "Image" && image.enabled != visible)
                image.enabled = visible;
    }
}
