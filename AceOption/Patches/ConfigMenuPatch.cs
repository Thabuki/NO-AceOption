using System.Reflection;
using AceOption.Helpers;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AceOption.Patches;

[HarmonyPatch]
internal static class ConfigMenuPatch
{
    private static MethodInfo drawSetting;
    private static PropertyInfo entryProperty;

    private static bool Prepare()
    {
        var manager = AccessTools.TypeByName("ConfigurationManager.ConfigurationManager");
        var setting = AccessTools.TypeByName("ConfigurationManager.ConfigSettingEntry");
        if (manager == null || setting == null) return false;
        drawSetting = AccessTools.Method(manager, "DrawSingleSetting");
        entryProperty = AccessTools.Property(setting, "Entry");
        return drawSetting != null && drawSetting.GetParameters().Length == 1 &&
            drawSetting.GetParameters()[0].ParameterType.IsAssignableFrom(setting) &&
            entryProperty?.PropertyType == typeof(ConfigEntryBase);
    }

    private static MethodBase TargetMethod() => drawSetting;

    private static void Prefix(object __0, out bool? __state)
    {
        __state = null;
        if (!entryProperty.DeclaringType.IsInstanceOfType(__0)) return;
        var entry = (ConfigEntryBase)entryProperty.GetValue(__0, null);
        if (entry == null) return;
        var enabled = ConfigMenu.GetEnabledState(entry);
        if (!enabled.HasValue) return;
        __state = GUI.enabled;
        GUI.enabled = __state.Value && enabled.Value;
    }

    // Restore the GUI state even if a drawer throws, keeping later rows usable.
    private static void Finalizer(bool? __state)
    {
        if (__state.HasValue) GUI.enabled = __state.Value;
    }
}
