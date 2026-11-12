using BepInEx.Configuration;

namespace AceOption.Helpers;

// Display categories are independent of saved section/key names, preserving existing configs.
internal static class ConfigMenu
{
    private const string Look = "Free Look", Camera = "Chase Camera", Hud = "Chase HUD",
        Flight = "Flight Controls", Combat = "Combat";

    internal static ConfigDescription Describe(string key, string help, AcceptableValueBase range = null)
    {
        var menu = key switch
        {
            "AlwaysFreeLook" => new ConfigurationManagerAttributes(Look, "Enable Free Look", 40),
            "FreeLookSensitivity" => new ConfigurationManagerAttributes(Look, "Look Sensitivity", 30, "AlwaysFreeLook"),
            "FreeLookDeadzone" => new ConfigurationManagerAttributes(Look, "Look Deadzone", 20, "AlwaysFreeLook"),
            "AutoCenterDelay" => new ConfigurationManagerAttributes(Look, "Auto-Center Delay (seconds)", 10, "AlwaysFreeLook"),
            "AutoCenterSpeed" => new ConfigurationManagerAttributes(Look, "Auto-Center Speed", 0, "AlwaysFreeLook"),

            "RememberCameraView" => new ConfigurationManagerAttributes(Camera, "Remember Camera View", 100),
            "LastCameraView" => new ConfigurationManagerAttributes(Camera, "Last Camera View", 0) { Browsable = false },
            "CameraAdjustments" => new ConfigurationManagerAttributes(Camera, "Enable Camera Adjustments", 90),
            "CameraDistance" => new ConfigurationManagerAttributes(Camera, "Camera Distance", 80, "CameraAdjustments"),
            "PlaneVerticalOffset" => new ConfigurationManagerAttributes(Camera, "Aircraft Vertical Offset", 70, "CameraAdjustments"),
            "TargetFocusEnabled" => new ConfigurationManagerAttributes(Camera, "Enable Target Focus", 60),
            "TargetFocusHoldDelay" => new ConfigurationManagerAttributes(Camera, "Target Focus Hold Delay (seconds)", 50, "TargetFocusEnabled"),
            "FixWeaponFoV" => new ConfigurationManagerAttributes(Camera, "Correct External Weapon FOV", 40),
            "HitSoundOutsideCockpit" => new ConfigurationManagerAttributes(Camera, "Hit Sound Outside Cockpit", 30),

            "FlightHud" => new ConfigurationManagerAttributes(Hud, "Show Flight HUD", 70),
            "HudFollowsCamera" => new ConfigurationManagerAttributes(Hud, "HUD Follows Camera", 65, "FlightHud", "Chase HUD"),
            "Map" => new ConfigurationManagerAttributes(Hud, "Show Minimap", 60, "FlightHud", "Chase HUD"),
            "PitchLadder" => new ConfigurationManagerAttributes(Hud, "Show Pitch Ladder", 50, "FlightHud", "Chase HUD"),
            "Waterline" => new ConfigurationManagerAttributes(Hud, "Show Aircraft Reference", 40, "FlightHud", "Chase HUD"),
            "LeftInstruments" => new ConfigurationManagerAttributes(Hud, "Show Left Instruments", 30, "FlightHud", "Chase HUD"),
            "RightInstruments" => new ConfigurationManagerAttributes(Hud, "Show Right Instruments", 20, "FlightHud", "Chase HUD"),
            "Compass" => new ConfigurationManagerAttributes(Hud, "Show Compass", 10, "FlightHud", "Chase HUD"),
            "VirtualJoystickIndicator" => new ConfigurationManagerAttributes(Hud, "Show Joystick Indicator", 0, "FlightHud", "Chase HUD"),

            "VirtualJoystick" => new ConfigurationManagerAttributes(Flight, "Enable Mouse Flight Input", 50),
            "ThrottleChordFlightAssist" => new ConfigurationManagerAttributes(Flight, "Throttle Chord: Assist Off (Hold)", 40),
            "ThrottleDownGroundBrakes" => new ConfigurationManagerAttributes(Flight, "Throttle Down: Ground Brakes (Hold)", 38),
            "YawChordAutoLevel" => new ConfigurationManagerAttributes(Flight, "Yaw Chord: Auto-Level (Hold)", 35),
            "SmoothButtonYaw" => new ConfigurationManagerAttributes(Flight, "Smooth Button Yaw", 30),
            "YawRiseTime" => new ConfigurationManagerAttributes(Flight, "Yaw Press Time (seconds)", 20, "SmoothButtonYaw", "Flight Controls"),
            "YawReturnTime" => new ConfigurationManagerAttributes(Flight, "Yaw Return Time (seconds)", 10, "SmoothButtonYaw", "Flight Controls"),

            "MissileFollowCamera" => new ConfigurationManagerAttributes(Combat, "Missile Camera (Hold Fire)", 50),
            "TargetCycling" => new ConfigurationManagerAttributes(Combat, "Single-Target Cycling", 40),
            "TurretAim" => new ConfigurationManagerAttributes(Combat, "Aim Turrets with Camera", 30),

            _ => null
        };
        return menu == null ? new ConfigDescription(help, range) : new ConfigDescription(help, range, menu);
    }

    internal static bool? GetEnabledState(ConfigEntryBase entry)
    {
        foreach (var tag in entry.Description.Tags)
        {
            if (tag is ConfigurationManagerAttributes menu)
                return menu.Parent == null ||
                    entry.ConfigFile.TryGetEntry<bool>(menu.Parent, out var parent) && parent.Value;
        }
        return null;
    }

    // Configuration Manager reads these optional fields by name; no DLL dependency is needed.
    private sealed class ConfigurationManagerAttributes
    {
        public readonly string Category, DispName;
        public readonly int? Order;
        public readonly bool? ShowRangeAsPercent = false;
        public bool? Browsable;
        internal readonly ConfigDefinition Parent;

        internal ConfigurationManagerAttributes(string category, string name, int order,
            string parentKey = null, string parentSection = "Other")
        {
            Category = category;
            DispName = parentKey == null ? name : "    " + name;
            Order = order;
            Parent = parentKey == null ? null : new ConfigDefinition(parentSection, parentKey);
        }
    }
}
