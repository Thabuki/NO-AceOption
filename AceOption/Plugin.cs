using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using AceOption.Helpers;
using HarmonyLib;

namespace AceOption;

[BepInPlugin(Id, "Ace Option", "1.0.1")]
[BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
[BepInIncompatibility("ChaseCamPlus")]
[BepInIncompatibility("Thabuki.NuclearOption.CasualQoL")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = "Thabuki.NuclearOption.AceOption";
    internal static new ManualLogSource Logger;
    private Harmony patches;

    public static ConfigEntry<bool> VirtualJoystick, FlightHud, Map, TurretAim,
        HudFollowsCamera, PitchLadder, Waterline, LeftInstruments, RightInstruments, Compass,
        VirtualJoystickIndicator, AlwaysFreeLook, FixWeaponFoV, HitSoundOutsideCockpit, SmoothButtonYaw,
        ThrottleChordFlightAssist, YawChordAutoLevel, ThrottleDownGroundBrakes, TargetCycling,
        CameraAdjustments, TargetFocusEnabled, RememberCameraView, MissileFollowCamera;
    internal static ConfigEntry<FlightCameraView> LastCameraView;
    public static ConfigEntry<float> FreeLookSensitivity, CameraDistance, PlaneVerticalOffset,
        TargetFocusHoldDelay, AutoCenterDelay, AutoCenterSpeed, FreeLookDeadzone, YawRiseTime, YawReturnTime;

    // Retain the serialized input names so existing controller bindings remain usable.
    public const string ACTION_TOGGLE = "ChaseCamPlus::ToggleChase";
    public const string ACTION_TARGET_FOCUS = "ChaseCamPlus::TargetFocusHold";
    public const string ACTION_FIRE_GUNS = "AceOption::FireGuns";
    public static int ToggleActionId = -1, TargetFocusActionId = -1, FireGunsActionId = -1;

    private void Awake()
    {
        Logger = base.Logger;

        // Configuration Manager preserves the first registration of each display category.
        // Keep these groups in menu order; the saved section/key names remain independent.
        AlwaysFreeLook = Flag("Other", "AlwaysFreeLook", true, "Look around in chase with the existing 'Pan View' and 'Tilt View' controls. While enabled, mouse input is reserved for looking and 'Center' resets the camera. Disable to turn off manual chase look and allow mouse flight input when Enable Mouse Flight Input is on. Enable Target Focus controls hold-to-focus separately.");
        FreeLookSensitivity = Config.Bind("Other", "FreeLookSensitivity", 1f, ConfigMenu.Describe("FreeLookSensitivity", "Scale chase look speed on the existing 'Pan View' and 'Tilt View' controls. 1 keeps the game's view sensitivity. Requires Enable Free Look to be enabled."));
        FreeLookDeadzone = Number("FreeLookDeadzone", .15f, 0f, 1f, "Ignore small 'Pan View' and 'Tilt View' inputs to reduce stick drift. Range 0 to 1. Requires Enable Free Look to be enabled.");
        AutoCenterDelay = Number("AutoCenterDelay", .15f, 0f, 30f, "Seconds without look input before the chase camera smoothly returns to center. Requires Enable Free Look to be enabled. 0 starts the smooth return immediately; the existing 'Center' action resets the view instantly.");
        AutoCenterSpeed = Number("AutoCenterSpeed", 5f, .1f, 20f, "How quickly the chase camera returns to center after Auto-Center Delay. 5 keeps the original return speed; lower values return more slowly and higher values return faster. Still respects the game's view smoothing. Requires Enable Free Look. Applies immediately; manual look smoothing and the instant 'Center' action are unchanged.");

        RememberCameraView = Flag("Other", "RememberCameraView", false, "Remember your last cockpit or chase view when entering an aircraft, including in a new match or after restarting the game. Disabled by default to use the game's normal cockpit start. Enable to save either view chosen with the normal camera controls or Chase Cam Toggle. Spectator views and death/ejection do not replace the saved choice.");
        LastCameraView = Config.Bind("Other", "LastCameraView", FlightCameraView.Cockpit,
            ConfigMenu.Describe("LastCameraView", "Last cockpit or chase view, saved automatically by Remember Camera View."));
        CameraAdjustments = Flag("Other", "CameraAdjustments", false, "Apply Camera Distance and Aircraft Vertical Offset in chase view. Disabled by default to retain the current camera mode's distance and framing. Enable to apply the suggested or saved slider values; your slider values are retained when disabled.");
        CameraDistance = Number("CameraDistance", .65f, .25f, 2f, "Adjust chase camera distance, including while looking around or focusing a target. Requires Enable Camera Adjustments. 1 uses the original distance; lower values bring the camera closer.");
        PlaneVerticalOffset = Number("PlaneVerticalOffset", -.15f, -.35f, .35f, "Move the aircraft up or down in the chase view, as a fraction of screen height. Requires Enable Camera Adjustments. Negative values place it lower. Target focus centers the target instead.");
        TargetFocusEnabled = Flag("Other", "TargetFocusEnabled", false, "Hold 'Chase Target Focus (Hold)' to follow your newest marked target after Target Focus Hold Delay (seconds); release to return to chase. Disabled by default. Works independently of Enable Camera Adjustments and Enable Free Look. Disabling ends focus and preserves your delay and binding.\nRequired binding: assign 'Chase Target Focus (Hold)' in the game's Flight Controls; this custom action starts unbound. It can share the 'Mark Target' button. Holds that activate focus add no mark; unused long holds keep bulk marking.");
        TargetFocusHoldDelay = Number("TargetFocusHoldDelay", .3f, .3f, 2f, "Seconds to hold before the chase camera follows your newest marked target. Requires Enable Target Focus and the 'Chase Target Focus (Hold)' binding in the game's Flight Controls. Minimum 0.3 seconds; release to return to chase. The action starts unbound and can share the 'Mark Target' button: taps mark on release, while holds that activate focus add no mark. Long holds that never activate focus keep bulk marking.");
        FixWeaponFoV = Flag("Other", "FixWeaponFoV", true, "Use the game's external field of view for bomb aiming and boresight zoom outside the cockpit, keeping weapon displays aligned with the external camera.");
        HitSoundOutsideCockpit = Flag("Other", "HitSoundOutsideCockpit", true, "Play hit-confirmation audio outside the cockpit. Requires hit markers to be enabled in the game's settings.");

        FlightHud = Flag("Chase HUD", "FlightHud", true, "Show the flight HUD in chase view. Enable this to use the minimap, instrument visibility options and joystick indicator below.");
        HudFollowsCamera = Flag("Chase HUD", "HudFollowsCamera", false, "Keep the chase flight HUD centered and upright in the camera view, including free look and target focus. Disabled by default: the HUD follows the aircraft nose. Requires Show Flight HUD. Pitch and heading instruments still report aircraft attitude. Applies immediately; cockpit and other views retain native behavior.");
        Map = Flag("Chase HUD", "Map", true, "Show the minimap in chase view. Requires Show Flight HUD. Full-map controls keep their normal behavior.");
        PitchLadder = Flag("Chase HUD", "PitchLadder", true, "Show the pitch ladder and horizon line in chase view. Requires Show Flight HUD. These indicate aircraft attitude, even when the camera looks away from the nose.");
        Waterline = Flag("Chase HUD", "Waterline", true, "Show the fixed aircraft reference mark in chase view. Requires Show Flight HUD.");
        LeftInstruments = Flag("Chase HUD", "LeftInstruments", true, "Show speed, angle of attack (AoA), Mach, G-force and fuel instruments in chase view. Requires Show Flight HUD. Hiding this group leaves warning widgets available.");
        RightInstruments = Flag("Chase HUD", "RightInstruments", true, "Show throttle, RPM, altitude and climb instruments in chase view. Requires Show Flight HUD.");
        Compass = Flag("Chase HUD", "Compass", true, "Show the heading ribbon and bearing in chase view. Requires Show Flight HUD. Heading follows the aircraft, even when the camera looks away.");

        VirtualJoystickIndicator = Flag("Chase HUD", "VirtualJoystickIndicator", true, "Show the mouse stick line and dot in chase view. Requires Show Flight HUD. This controls the drawing only; turn on 'Enable Mouse Flight Input' separately to steer with the mouse.");

        VirtualJoystick = Flag("Virtual Joystick", "VirtualJoystick", false, "Use the game's existing mouse flight input in chase view. Disabled by default. Requires Enable Free Look to be off; target focus temporarily reserves the mouse for the camera. Mouse flight input needs no new binding.");
        ThrottleChordFlightAssist = Flag("Flight Controls", "ThrottleChordFlightAssist", false,
            "Hold the existing Increase Throttle and Decrease Throttle controls together to temporarily turn flight assist off. Buttons and relative throttle retain the current throttle; absolute analog throttle follows the combined input, with equal trigger pressure centered at 50% when the negative-axis range is enabled. Disabled by default. Release either to restore the previous assist state. Requires both directions of the game's 'Throttle' action to be assigned to buttons, keys or separate triggers; no custom binding is needed. Also affects native VTOL automation. Holding 'Axis Modifier' bypasses this shortcut.");
        ThrottleDownGroundBrakes = Flag("Flight Controls", "ThrottleDownGroundBrakes", false,
            "Hold the existing Decrease Throttle control with throttle at minimum (0) for 1 continuous second while the landing gear is touching the ground to apply wheel brakes, including during landing rollout. Disabled by default. If already holding it while reducing throttle, the delay starts when throttle reaches zero; no release or new press is needed. Release, throttle above zero, leaving the ground or an interruption clears added braking and restarts the delay. The normal Brake control remains available immediately. Uses the negative side of the game's 'Throttle' action with its existing button/trigger threshold; no custom binding is needed. Holding Increase Throttle together or 'Axis Modifier' bypasses this shortcut.");
        YawChordAutoLevel = Flag("Flight Controls", "YawChordAutoLevel", false,
            "Hold both directions of the existing 'Yaw' action together to level the wings and nose to the horizon. Disabled by default. Overrides pitch/roll and centers rudder while held; release either to resume normal control. Bind yaw left/right to buttons or keys such as L1/LB and R1/RB; no custom binding is needed. Works independently of Smooth Button Yaw. Airborne planes only; helicopters and auto-hover are excluded. Uses normal control authority, without holding altitude or changing throttle/flight assist.");
        SmoothButtonYaw = Flag("Flight Controls", "SmoothButtonYaw", false, "Build rudder gradually for small tap corrections and full rudder on hold. Disabled by default to retain native button yaw response. Requires buttons or keys assigned to the negative and positive sides of the game's existing 'Yaw' action, such as L1/LB and R1/RB; no custom binding is needed. Analog axes keep their normal response.");
        YawRiseTime = Config.Bind("Flight Controls", "YawRiseTime", 2f,
            ConfigMenu.Describe("YawRiseTime", "Seconds from centered to full rudder while holding a yaw button. Longer times give finer tap corrections. Requires Smooth Button Yaw and buttons or keys assigned to the existing 'Yaw' action.", new AcceptableValueRange<float>(.05f, 5f)));
        YawReturnTime = Config.Bind("Flight Controls", "YawReturnTime", 2f,
            ConfigMenu.Describe("YawReturnTime", "Seconds to return from full rudder to center after releasing a yaw button. Smaller inputs return sooner. Reversing first returns through center at this rate. Requires Smooth Button Yaw and buttons or keys assigned to the existing 'Yaw' action.", new AcceptableValueRange<float>(.02f, 5f)));

        MissileFollowCamera = Flag("Combat", "MissileFollowCamera", false,
            "Fire one missile per press of the existing 'Fire' control. Hold for 0.3 seconds to follow that missile with the camera; release or missile impact returns to your previous cockpit/chase view. Disabled by default. Release and press again for another missile. Multiple marked targets use the first target for this single launch. Guns and other weapon types keep their normal firing controls. Requires no new binding.");
        TargetCycling = Flag("Combat", "TargetCycling", false,
            "Tap the existing 'Mark Target' control to cycle one visible, detected enemy at a time, preferring targets suited to the selected weapon. Cycling needs no new binding. Disable to restore additive marking without clearing current targets. Long holds that never activate focus keep bulk marking; full-map controls are unchanged.\nOptional hold-to-focus requires Enable Target Focus and binding 'Chase Target Focus (Hold)' in the game's Flight Controls (unbound by default). It can share the 'Mark Target' button; adjust its delay under Target Focus Hold Delay (seconds).");
        TurretAim = Flag("Chase HUD", "TurretAim", true, "Make manual turrets without an assigned target follow the chase camera, including its return to center. Disable to aim along the aircraft nose instead. Automatic aiming at assigned targets takes priority. Uses existing turret controls. Turn on Enable Free Look to look around manually; Enable Target Focus controls hold-to-focus separately.");

        patches = new Harmony(Id);
        patches.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Ace Option 1.0.1 loaded");
    }

    private ConfigEntry<bool> Flag(string section, string key, bool value, string help) =>
        Config.Bind(section, key, value, ConfigMenu.Describe(key, help));
    private ConfigEntry<float> Number(string key, float value, float min, float max, string help) =>
        Config.Bind("Other", key, value, ConfigMenu.Describe(key, help, new AcceptableValueRange<float>(min, max)));

    private void Update()
    {
        CameraCommands.Poll();
        MissileCamera.Update();
        // Fixed updates stop while paused; clear stored rudder here as well.
        if (!CameraCommands.InputAllowed || !SmoothButtonYaw.Value) YawCommands.Reset();
        if (!CameraCommands.InputAllowed) AbsoluteThrottle.Reset();
        FlightAssistCommands.CheckInterrupted();
        AutoLevelCommands.CheckInterrupted();
        GroundBrakeCommands.CheckInterrupted();
    }

    private void LateUpdate() => FlightStatusHud.Refresh();

    private static void StopShortcuts()
    {
        MissileCamera.Reset();
        AbsoluteThrottle.Reset();
        FlightAssistCommands.Reset();
        AutoLevelCommands.Reset();
        GroundBrakeCommands.Reset();
        FlightStatusHud.Hide();
    }

    private void OnDisable() => StopShortcuts();
    private void OnApplicationFocus(bool focused) { if (!focused) StopShortcuts(); }
    private void OnApplicationPause(bool paused) { if (paused) StopShortcuts(); }

    private void OnDestroy()
    {
        MissileCamera.Release();
        FlightAssistCommands.Reset();
        GroundBrakeCommands.Reset();
        AutoLevelCommands.Reset();
        FlightStatusHud.Release();
        patches?.UnpatchSelf();
        YawCommands.Reset();
        AbsoluteThrottle.Reset();
        TargetMark.Reset();
        TargetFocus.Reset();
        ChasePlacement.Reset();
        HudAlignment.Reset();
        HudPresentation.Release();
    }
}
