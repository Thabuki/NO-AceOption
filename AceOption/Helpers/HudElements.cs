using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Helpers;

internal static class HudElements
{
    private static readonly Dictionary<string, int> Groups = new Dictionary<string, int>
    {
        { "SpeedGauge", 0 }, { "AoADisplay", 0 }, { "AoAIndexer", 0 }, { "MachIndicator", 0 },
        { "GIndicators", 0 }, { "FuelGauge", 0 }, { "ThrottleGauge", 1 }, { "RPMGauge", 1 },
        { "Altitude", 1 }, { "Climbrate", 1 }, { "Bearing", 2 }
    };
    private static readonly List<GameObject>[] elements = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
    private static Transform root;

    internal static void Apply(bool showLeft, bool showRight, bool showCompass)
    {
        var hud = SceneSingleton<FlightHud>.i;
        if (hud == null) { root = null; return; }
        bool scan = root != hud.transform;
        foreach (var group in elements) foreach (var item in group) if (item == null) scan = true;
        if (scan) Discover(hud);
        Set(elements[0], showLeft); Set(elements[1], showRight); Set(elements[2], showCompass);
    }
    internal static void ShowAll() { foreach (var group in elements) Set(group, true); }
    private static void Set(List<GameObject> group, bool visible)
    {
        foreach (var item in group) if (item != null && item.activeSelf != visible) item.SetActive(visible);
    }
    private static void Discover(FlightHud hud)
    {
        root = hud.transform;
        foreach (var group in elements) group.Clear();
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
        {
            // The native look-away display has its own visibility state machine.
            if (node.GetComponentInParent<HeadMountedDisplay>(true) != null) continue;
            foreach (var component in node.GetComponents<Component>())
                if (component != null && Groups.TryGetValue(component.GetType().Name, out int group) &&
                    !elements[group].Contains(node.gameObject)) elements[group].Add(node.gameObject);
        }
        if (AccessTools.Field(typeof(FlightHud), "compass")?.GetValue(hud) is Component ribbon &&
            ribbon.GetComponentInParent<HeadMountedDisplay>(true) == null && !elements[2].Contains(ribbon.gameObject))
            elements[2].Add(ribbon.gameObject);
    }
}
