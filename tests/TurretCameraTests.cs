using System;
using AceOption.Helpers;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 forward => new Vector3 { z = 1 };
    }
    public class Transform { public Vector3 forward = Vector3.forward; }
}
namespace Rewired
{
    public class Player { public bool CenterPressed; public bool GetButtonDown(string action) => action == "Center" && CenterPressed; }
}
public class Unit { public UnityEngine.Transform transform = new UnityEngine.Transform(); }
public class Aircraft : Unit { }
public class CameraBaseState { }
public enum CameraMode { cockpit, chase }
public class CameraStateManager
{
    public static CameraMode cameraMode;
    public CameraBaseState currentState, chaseState = new CameraBaseState(), cockpitState = new CameraBaseState();
    public Unit followingUnit;
}
public static class SceneSingleton<T> { public static T i; }
public static class GameManager
{
    public static Aircraft local;
    public static bool IsLocalAircraft(Unit unit) => unit != null && unit == local;
    public static bool GetLocalAircraft(out Aircraft aircraft) { aircraft = local; return aircraft != null; }
}
public static class PlayerSettings { public static float defaultFoV = 60, defaultExternalFoV = 70; }
namespace AceOption
{
    public class Setting { public bool Value; }
    public static class Plugin
    {
        public static Setting TurretAim = new Setting { Value = true }, AlwaysFreeLook = new Setting { Value = true },
            VirtualJoystick = new Setting(), FixWeaponFoV = new Setting();
    }
}
namespace AceOption.Helpers
{
    internal static class TargetFocus { internal static bool Active; }
}

internal static class TurretCameraTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    public static void Main()
    {
        CameraStateManager.cameraMode = CameraMode.chase;
        AceOption.Plugin.VirtualJoystick.Value = true;
        Check(PatchHelper.EffectiveCameraMode() == CameraMode.chase, "Enable Free Look reserves mouse input for the camera");
        var player = new Rewired.Player { CenterPressed = true };
        Check(!PatchHelper.ChaseCenterSwitchRequested(player, "Center"), "Center resets the view while Enable Free Look is enabled");
        AceOption.Plugin.AlwaysFreeLook.Value = false;
        Check(PatchHelper.EffectiveCameraMode() == CameraMode.cockpit, "Disabling Enable Free Look allows mouse flight input in chase");
        Check(PatchHelper.ChaseCenterSwitchRequested(player, "Center"), "Center retains native switching with manual chase look disabled");
        player.CenterPressed = false;
        Check(!PatchHelper.ChaseCenterSwitchRequested(player, "Center"), "Native switching still requires a Center press");
        TargetFocus.Active = true;
        Check(PatchHelper.EffectiveCameraMode() == CameraMode.chase, "Target focus reserves the mouse while manual chase look is disabled");
        TargetFocus.Active = false;
        AceOption.Plugin.VirtualJoystick.Value = false;
        Check(PatchHelper.EffectiveCameraMode() == CameraMode.chase, "Mouse flight input remains optional");
        CameraStateManager.cameraMode = CameraMode.cockpit;
        Check(PatchHelper.EffectiveCameraMode() == CameraMode.cockpit, "Cockpit input retains its native mode");
        AceOption.Plugin.AlwaysFreeLook.Value = true;

        var aircraft = new Aircraft();
        GameManager.local = aircraft;
        var cam = new CameraStateManager { followingUnit = aircraft };
        cam.currentState = cam.chaseState;
        SceneSingleton<CameraStateManager>.i = cam;
        Check(PatchHelper.AimAllowedState(cam) == cam.chaseState, "Local chase can supply manual aim when native target handling permits it");
        var view = new UnityEngine.Transform { forward = new UnityEngine.Vector3 { x = .6f, y = -.2f, z = .7f } };
        var direction = PatchHelper.AimDirection(view);
        Check(direction.x == .6f && direction.y == -.2f && direction.z == .7f, "Manual direction follows camera pan and tilt");
        view.forward = UnityEngine.Vector3.forward;
        direction = PatchHelper.AimDirection(view);
        Check(direction.x == 0 && direction.y == 0 && direction.z == 1, "Manual direction recenters with the camera");

        aircraft.transform.forward = new UnityEngine.Vector3 { x = 1 };
        AceOption.Plugin.TurretAim.Value = false;
        Check(PatchHelper.AimDirection(view).x == 1, "Without camera aim, the manual direction follows the aircraft nose");
        cam.currentState = cam.cockpitState;
        Check(PatchHelper.AimAllowedState(cam) == cam.cockpitState, "Cockpit manual aiming retains its native gate");
        Check(PatchHelper.AimDirection(view).z == 1, "Nose-aim option does not change cockpit look direction");
        cam.currentState = new CameraBaseState();
        Check(PatchHelper.AimAllowedState(cam) == cam.cockpitState, "Other views cannot open the chase aim gate");
        Check(PatchHelper.AimDirection(view).z == 1, "Other views retain their supplied direction");

        cam.currentState = cam.chaseState;
        cam.followingUnit = new Aircraft();
        Check(PatchHelper.AimAllowedState(cam) == cam.cockpitState, "Spectator chase cannot control the local turret");
        cam.followingUnit = null;
        Check(PatchHelper.AimAllowedState(cam) == cam.cockpitState, "Missing followed aircraft cannot open the chase gate");
        Check(PatchHelper.AimAllowedState(null) == null, "Missing camera gate is safe");
        GameManager.local = null;
        Check(PatchHelper.AimDirection(view).z == 1, "Missing local aircraft retains the supplied direction");
        SceneSingleton<CameraStateManager>.i = null;
        Check(PatchHelper.AimDirection(view).z == 1, "Missing camera retains the supplied direction");
        Check(PatchHelper.AimDirection(null).z == 1, "Missing view has a valid fallback direction");
        Console.WriteLine("PASS: " + checks + " camera input/turret direction/scope assertions");
    }
}
