using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using BepInEx.Configuration;
using HarmonyLib;

// Exercise the production row patch with real config entries and Harmony.
// The drawer and GUI stand-ins record input availability without running Unity.
internal static class ConfigMenuTests
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string directory in new[] { "BepInEx/core", "NuclearOption_Data/Managed" })
            {
                string path = Path.Combine(args[0], directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Run();
    }

    private static string Label(ConfigEntryBase entry)
    {
        var tag = entry.Description.Tags.Single();
        return (string)tag.GetType().GetField("DispName").GetValue(tag);
    }

    private static void CheckDependency<T>(ConfigFile config, string section, string parentKey,
        string childKey, T defaultValue, T savedValue)
    {
        var parent = config.Bind(section, parentKey, false, ConfigMenu.Describe(parentKey, "Parent"));
        var child = config.Bind(section, childKey, defaultValue, ConfigMenu.Describe(childKey, "Child"));
        child.Value = savedValue;
        var row = new ConfigurationManager.ConfigSettingEntry(child);
        var manager = new ConfigurationManager.ConfigurationManager { AttemptEdit = true, NewValue = defaultValue };
        Check(!Label(parent).StartsWith(" "), parentKey + ": parent switch stays unindented");
        Check(Label(child).StartsWith("    "), childKey + ": child label is indented");

        manager.Draw(row);
        Check(!manager.EnabledDuringDraw, childKey + ": disabled parent grays the row");
        Check(Equals(child.Value, savedValue), childKey + ": disabled editor preserves the saved value");
        Check(UnityEngine.GUI.enabled, childKey + ": drawing restores the GUI for the next row");
        manager.AttemptEdit = false;
        manager.AttemptReset = true;
        manager.Draw(row);
        Check(Equals(child.Value, savedValue), childKey + ": disabled reset preserves the saved value");

        parent.Value = true;
        manager.Draw(row);
        Check(manager.EnabledDuringDraw && Equals(child.Value, defaultValue),
            childKey + ": enabling the parent immediately enables reset");
        manager.AttemptReset = false;
        manager.AttemptEdit = true;
        manager.NewValue = savedValue;
        manager.Draw(row);
        Check(Equals(child.Value, savedValue), childKey + ": enabled editor accepts input");

        UnityEngine.GUI.enabled = false;
        manager.NewValue = defaultValue;
        manager.Draw(row);
        Check(!manager.EnabledDuringDraw && !UnityEngine.GUI.enabled && Equals(child.Value, savedValue),
            childKey + ": an already disabled GUI stays disabled");
        UnityEngine.GUI.enabled = true;
        parent.Value = false;
        manager.Draw(row);
        Check(!manager.EnabledDuringDraw && Equals(child.Value, savedValue),
            childKey + ": disabling the parent takes effect without rebuilding the menu");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var harmony = new Harmony("AceOption.ConfigMenuTests");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        try
        {
            var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "AceOption-menu-" + Guid.NewGuid() + ".cfg"), false)
                { SaveOnConfigSet = false };
            CheckDependency(config, "Other", "AlwaysFreeLook", "FreeLookSensitivity", 1f, 1.5f);
            CheckDependency(config, "Other", "CameraAdjustments", "CameraDistance", .65f, 1.2f);
            CheckDependency(config, "Other", "TargetFocusEnabled", "TargetFocusHoldDelay", .3f, 1f);
            CheckDependency(config, "Chase HUD", "FlightHud", "Map", true, false);
            CheckDependency(config, "Flight Controls", "SmoothButtonYaw", "YawRiseTime", 2f, 1f);
            CheckDependency(config, "Controller Feedback", "ControllerVibration", "VibrationIntensity", .6f, .8f);

            Check(config.TryGetEntry<bool>("Other", "AlwaysFreeLook", out var look) && Label(look) == "Enable Free Look",
                "The visible free-look switch uses the new name with its existing config key");
            foreach (string key in new[] { "AutoCenterDelay", "TargetFocusHoldDelay", "YawRiseTime", "YawReturnTime" })
            {
                var tag = ConfigMenu.Describe(key, "").Tags.Single();
                Check(((string)tag.GetType().GetField("DispName").GetValue(tag)).EndsWith(" (seconds)"),
                    key + ": time unit appears in the label");
            }

            var manager = new ConfigurationManager.ConfigurationManager();
            config.TryGetEntry<float>("Other", "CameraDistance", out var distance);
            var failure = new InvalidOperationException("Drawer failure");
            manager.DrawFailure = failure;
            try
            {
                manager.Draw(new ConfigurationManager.ConfigSettingEntry(distance));
                throw new Exception("Expected drawer failure");
            }
            catch (InvalidOperationException e)
            {
                Check(ReferenceEquals(e, failure), "Drawer failures propagate");
            }
            Check(UnityEngine.GUI.enabled, "A failing disabled drawer restores GUI state");

            manager.DrawFailure = null;
            manager.DisableGuiAfterDrawing = true;
            var foreign = config.Bind("Other plugin", "CameraDistance", 1f);
            manager.Draw(new ConfigurationManager.ConfigSettingEntry(foreign));
            Check(!UnityEngine.GUI.enabled, "Rows from other plugins keep their own GUI behavior");
            UnityEngine.GUI.enabled = true;
            manager.Draw(new ConfigurationManager.SettingEntryBase());
            Check(!UnityEngine.GUI.enabled, "Property-backed rows from other plugins keep their GUI behavior");
            Console.WriteLine("PASS: " + checks + " menu dependency/live-toggle/reset/GUI restoration assertions");
        }
        finally
        {
            UnityEngine.GUI.enabled = true;
            harmony.UnpatchSelf();
        }
    }
}

namespace UnityEngine
{
    public static class GUI { public static bool enabled { get; set; } = true; }
}

namespace ConfigurationManager
{
    public class SettingEntryBase { }

    public sealed class ConfigSettingEntry : SettingEntryBase
    {
        public ConfigEntryBase Entry { get; }
        public ConfigSettingEntry(ConfigEntryBase entry) { Entry = entry; }
    }

    public sealed class ConfigurationManager
    {
        public bool AttemptEdit, AttemptReset, EnabledDuringDraw, DisableGuiAfterDrawing;
        public object NewValue;
        public Exception DrawFailure;
        public void Draw(SettingEntryBase setting) => DrawSingleSetting(setting);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawSingleSetting(SettingEntryBase setting)
        {
            EnabledDuringDraw = UnityEngine.GUI.enabled;
            if (DrawFailure != null) throw DrawFailure;
            if (setting is ConfigSettingEntry config && UnityEngine.GUI.enabled)
            {
                if (AttemptEdit) config.Entry.BoxedValue = NewValue;
                if (AttemptReset) config.Entry.BoxedValue = config.Entry.DefaultValue;
            }
            if (DisableGuiAfterDrawing) UnityEngine.GUI.enabled = false;
        }
    }
}
