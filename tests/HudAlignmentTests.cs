using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

// Exercise the actual Harmony hooks and private Update delegate on stand-ins.
// Projection values are supplied by the camera; this checks frame ordering and
// ownership, not Unity's renderer or projection math.
internal static class HudAlignmentTests
{
    private static int checks;
    internal static readonly List<string> Events = new List<string>();
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static void Near(Vector3 expected, Vector3 actual, string message) =>
        Check(expected.x == actual.x && expected.y == actual.y && expected.z == actual.z, message);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var harmony = new Harmony("AceOption.HudAlignmentTests");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        try
        {
            var plane = new Aircraft();
            GameManager.Local = plane;
            var cam = new CameraStateManager { followingUnit = plane };
            cam.currentState = cam.chaseState;
            SceneSingleton<CameraStateManager>.i = cam;
            var hud = new FlightHud(plane);
            SceneSingleton<FlightHud>.i = hud;
            Check(!AceOption.Plugin.HudFollowsCamera.Value, "Camera following starts disabled");

            // Reproduce an Update projection against the previous camera pose,
            // followed by the final free-look/focus/framing/origin-shift pose.
            for (int frame = 1; frame <= 6; frame++)
            {
                cam.projectedNose = new Vector3(100 + frame, 200, 0);
                hud.Tick();
                Near(cam.projectedNose, hud.GetHUDCenter().position, "Native early projection runs");
                cam.finalNose = new Vector3(400 + frame, 300 + frame, frame == 6 ? -10 : 0);
                cam.finalRoll = frame * 15;
                cam.finalVelocity = new Vector3(500 - frame, 250, 0);
                Events.Clear();
                cam.Tick();
                Check(Events.Count == 2 && Events[0] == "camera" && Events[1] == "hud",
                    "HUD recalculates once after the final camera pose");
                Near(cam.finalNose, hud.GetHUDCenter().position, "Default HUD follows the current aircraft projection, including behind-view depth");
                Check(hud.GetHUDCenter().rotation.Roll == cam.finalRoll, "Default keeps native aircraft-relative roll");
                Near(cam.finalVelocity, hud.Velocity.position, "Native velocity vector uses the final camera pose");
                Check(hud.AircraftPitch == plane.Pitch, "Aircraft telemetry remains native");
            }

            AceOption.Plugin.HudFollowsCamera.Value = true;
            cam.mainCamera.viewportCenter = new Vector3(800, 450, 0);
            hud.Tick();
            Near(cam.mainCamera.viewportCenter, hud.GetHUDCenter().position, "Camera following also applies during native Update");
            for (int frame = 0; frame < 5; frame++)
            {
                cam.finalNose = new Vector3(-100 * frame, 500, frame == 4 ? -100 : 0);
                cam.finalRoll = 90 * frame;
                cam.finalVelocity = new Vector3(700 + frame, 200, 0);
                cam.Tick();
                Near(cam.mainCamera.viewportCenter, hud.GetHUDCenter().position, "Camera HUD stays centered through look/focus/rear views");
                Check(hud.GetHUDCenter().rotation.Roll == 0, "Camera HUD remains upright during aircraft roll");
                Near(cam.finalVelocity, hud.Velocity.position, "Camera following retains native velocity projection");
                Check(hud.AircraftPitch == plane.Pitch, "Camera following retains aircraft attitude values");
            }
            cam.mainCamera.viewportCenter = new Vector3(350, 300, 0);
            cam.Tick();
            Near(cam.mainCamera.viewportCenter, hud.GetHUDCenter().position, "Resized/offset camera viewport is honored");

            AceOption.Plugin.HudFollowsCamera.Value = false;
            cam.Tick();
            Near(cam.finalNose, hud.GetHUDCenter().position, "Disabling live immediately restores aircraft following");
            Check(hud.GetHUDCenter().rotation.Roll == cam.finalRoll, "Disabling restores native roll");
            AceOption.Plugin.HudFollowsCamera.Value = true;

            Action<Action, Action, string> exclude = (change, restore, label) => {
                change();
                int count = hud.Updates;
                var before = hud.GetHUDCenter().position;
                HudAlignment.AfterCamera(cam);
                HudAlignment.FollowCamera(hud);
                Check(hud.Updates == count, label + ": no late native update");
                Near(before, hud.GetHUDCenter().position, label + ": no camera lock writes");
                restore();
            };
            foreach (var state in new[] { cam.cockpitState, new object() })
                exclude(() => cam.currentState = state, () => cam.currentState = cam.chaseState, "Other camera state");
            exclude(() => AceOption.Plugin.FlightHud.Value = false, () => AceOption.Plugin.FlightHud.Value = true, "HUD disabled");
            exclude(() => hud.isActiveAndEnabled = false, () => hud.isActiveAndEnabled = true, "HUD component inactive");
            exclude(() => cam.followingUnit = new Aircraft(), () => cam.followingUnit = plane, "Spectator aircraft");
            exclude(() => GameManager.Local = null, () => GameManager.Local = plane, "Local ownership lost");
            exclude(() => cam.followingUnit = null, () => cam.followingUnit = plane, "Death/cleared subject");
            var camera = cam.mainCamera;
            exclude(() => cam.mainCamera = null, () => cam.mainCamera = camera, "Missing render camera");
            exclude(() => SceneSingleton<FlightHud>.i = null, () => SceneSingleton<FlightHud>.i = hud, "HUD scene unloaded");
            HudAlignment.AfterCamera(null);
            HudAlignment.FollowCamera(null);
            Check(true, "Null HUD/camera calls are harmless");

            cam.currentState = cam.cockpitState;
            hud.Tick();
            Near(cam.projectedNose, hud.GetHUDCenter().position, "Leaving chase restores native positioning on the next Update");
            var replacement = new FlightHud(plane);
            SceneSingleton<FlightHud>.i = replacement;
            cam.currentState = cam.chaseState;
            cam.Tick();
            Near(camera.viewportCenter, replacement.GetHUDCenter().position, "A replacement HUD uses the live option without stale scene references");
            var tag = ConfigMenu.Describe("HudFollowsCamera", "Follow camera.").Tags[0];
            Check((string)tag.GetType().GetField("Category").GetValue(tag) == "Chase HUD" &&
                ((string)tag.GetType().GetField("DispName").GetValue(tag)).TrimStart() == "HUD Follows Camera",
                "Camera-follow toggle appears in the Chase HUD group");

            var combat = new CombatHUD { aircraft = plane };
            SceneSingleton<CombatHUD>.i = combat;
            combat.Reticle.parent = replacement.GetHUDCenter();
            foreach (bool cameraLock in new[] { false, true })
            foreach (bool combatFirst in new[] { false, true })
            {
                AceOption.Plugin.HudFollowsCamera.Value = cameraLock;
                Time.frameCount++;
                Events.Clear();
                int count = combat.Updates;
                if (combatFirst)
                {
                    combat.Tick();
                    Check(combat.Updates == count, "Early combat HUD waits for final camera/layout");
                }
                cam.finalNose = new Vector3(200 + Time.frameCount, 100, 0);
                cam.Tick();
                if (!combatFirst) combat.Tick();
                Check(Events.Count == 3 && Events[0] == "camera" && Events[1] == "hud" && Events[2] == "combat",
                    "Both Unity LateUpdate orders run camera, layout, then weapon cues");
                Check(combat.Updates == count + 1, "Combat input and weapon-display updates execute exactly once per frame");
                Near(cam.finalNose, combat.Reticle.position, "Weapon cue retains its final world projection in both alignment modes");
                Near(cameraLock ? camera.viewportCenter : cam.finalNose, replacement.GetHUDCenter().position,
                    "Weapon updates do not change the selected layout mode");
            }
            Time.frameCount++;
            cam.currentState = cam.cockpitState;
            int nativeCount = combat.Updates;
            combat.Tick();
            Check(combat.Updates == nativeCount + 1, "Cockpit combat update is never deferred");
            cam.currentState = cam.chaseState;
            combat.aircraft = new Aircraft();
            combat.Tick();
            Check(combat.Updates == nativeCount + 2, "Unrelated combat HUD is never deferred");
            combat.aircraft = plane;
            cam.isActiveAndEnabled = false;
            combat.Tick();
            Check(combat.Updates == nativeCount + 3, "Inactive camera never defers native combat updates");
            cam.isActiveAndEnabled = true;
            Time.frameCount++;
            combat.Tick();
            AceOption.Plugin.FlightHud.Value = false;
            cam.Tick();
            Check(combat.Updates == nativeCount + 4, "Disabling HUD mid-frame still flushes native combat input exactly once");
            AceOption.Plugin.FlightHud.Value = true;
            Time.frameCount++;
            combat.Tick();
            HudAlignment.Reset();
            cam.Tick();
            Check(combat.Updates == nativeCount + 4, "Chase exit reset drops stale deferred HUD instances");

            // HMD visibility reads HUDCenter in Update. Depending on Unity's Update
            // order it used to see either the previous final pose or an early pose.
            // Make both early poses disagree with the current camera projection.
            var display = new HeadMountedDisplay();
            foreach (bool cameraLock in new[] { false, true })
            foreach (bool displayFirst in new[] { false, true })
            foreach (bool combatFirst in new[] { false, true })
            foreach (float nose in new[] { 0f, 1000f, 0f, 1000f })
            {
                Time.frameCount++;
                AceOption.Plugin.HudFollowsCamera.Value = cameraLock;
                replacement.GetHUDCenter().position = new Vector3(nose == 0 ? 1000 : 0, 0, 0);
                cam.projectedNose = replacement.GetHUDCenter().position;
                cam.finalNose = new Vector3(nose, 0, 0);
                int count = display.Updates;
                if (displayFirst) display.Tick();
                replacement.Tick();
                if (!displayFirst) display.Tick();
                Check(display.Updates == count, "Look-away visibility waits for the final camera in either Update order");
                if (combatFirst) combat.Tick();
                Events.Clear();
                cam.Tick();
                if (!combatFirst) combat.Tick();
                Check(Events.Count == 4 && Events[0] == "camera" && Events[1] == "hud" &&
                    Events[2] == "look-away" && Events[3] == "combat",
                    "Layout, look-away visibility and weapon cues all use the final camera pose");
                Check(display.Updates == count + 1, "Native look-away refresh runs once per frame");
                Check(display.Visible == ((cameraLock ? camera.viewportCenter.x : nose) > 500),
                    "Look-away instruments agree with the rendered HUD layout");
            }

            Action<Action, Action, string> nativeDisplay = (change, restore, reason) => {
                Time.frameCount++;
                change();
                int count = display.Updates;
                display.Tick();
                Check(display.Updates == count + 1, reason + ": look-away update stays native");
                restore();
            };
            nativeDisplay(() => cam.currentState = cam.cockpitState, () => cam.currentState = cam.chaseState, "Cockpit");
            nativeDisplay(() => AceOption.Plugin.FlightHud.Value = false, () => AceOption.Plugin.FlightHud.Value = true, "HUD opt-out");
            nativeDisplay(() => combat.aircraft = new Aircraft(), () => combat.aircraft = plane, "Unrelated aircraft");
            nativeDisplay(() => cam.isActiveAndEnabled = false, () => cam.isActiveAndEnabled = true, "Inactive camera");
            nativeDisplay(() => SceneSingleton<CombatHUD>.i = null, () => SceneSingleton<CombatHUD>.i = combat, "Missing combat HUD");
            nativeDisplay(() => GameManager.Local = null, () => GameManager.Local = plane, "Lost ownership");
            nativeDisplay(() => SceneSingleton<FlightHud>.i = null, () => SceneSingleton<FlightHud>.i = replacement, "HUD unloaded");

            Time.frameCount++;
            int displayCount = display.Updates;
            display.Tick();
            AceOption.Plugin.FlightHud.Value = false;
            cam.Tick();
            Check(display.Updates == displayCount + 1, "Mid-frame opt-out flushes look-away refresh once");
            AceOption.Plugin.FlightHud.Value = true;
            Time.frameCount++;
            display.Tick();
            display.isActiveAndEnabled = false;
            cam.Tick();
            Check(display.Updates == displayCount + 1, "Disabled look-away component is not replayed");
            display.isActiveAndEnabled = true;
            Time.frameCount++;
            display.Tick();
            HudAlignment.Reset();
            cam.Tick();
            Check(display.Updates == displayCount + 1, "Chase exit discards deferred look-away instances");
            Time.frameCount++;
            display.Tick();
            Time.frameCount++;
            cam.Tick();
            Check(display.Updates == displayCount + 1, "An old deferred look-away update never leaks into a later frame");
            Console.WriteLine("PASS: " + checks + " HUD alignment/order/live-toggle assertions with real Harmony hooks");
        }
        finally { harmony.UnpatchSelf(); }
    }
}

namespace AceOption
{
    public sealed class Toggle { public bool Value; }
    public static class Plugin
    {
        public static Toggle FlightHud = new Toggle { Value = true };
        public static Toggle HudFollowsCamera = new Toggle();
    }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public struct Quaternion
    {
        public float Roll;
        public static Quaternion identity => new Quaternion();
    }
    public sealed class Transform
    {
        public Transform parent;
        private Vector3 localPosition;
        public Vector3 position
        {
            get
            {
                var origin = parent == null ? new Vector3() : parent.position;
                return new Vector3(origin.x + localPosition.x, origin.y + localPosition.y, origin.z + localPosition.z);
            }
            set
            {
                var origin = parent == null ? new Vector3() : parent.position;
                localPosition = new Vector3(value.x - origin.x, value.y - origin.y, value.z - origin.z);
            }
        }
        public Quaternion rotation;
    }
    public sealed class Camera
    {
        public Vector3 viewportCenter = new Vector3(960, 540, 0);
        public Vector3 ViewportToScreenPoint(Vector3 value)
        {
            if (value.x != .5f || value.y != .5f || value.z != 0) throw new Exception("Expected viewport center");
            return viewportCenter;
        }
    }
    public static class Time { public static int frameCount; }
}
namespace UnityEngine.UI
{
    public sealed class Image { public readonly Transform transform = new Transform(); }
}
public class Unit { }
public sealed class Aircraft : Unit { public float Pitch = 25; }
public static class GameManager
{
    public static Unit Local;
    public static bool IsLocalAircraft(Unit unit) => unit != null && unit == Local;
}
public static class SceneSingleton<T> { public static T i; }
public sealed class FlightHud
{
    private Aircraft aircraft;
    private readonly Transform center = new Transform();
    private readonly UnityEngine.UI.Image velocityVector = new UnityEngine.UI.Image();
    public Transform Velocity => velocityVector.transform;
    public bool isActiveAndEnabled = true;
    public int Updates;
    public float AircraftPitch;
    public FlightHud(Aircraft aircraft) { this.aircraft = aircraft; Velocity.parent = center; }
    public Transform GetHUDCenter() => center;
    public void Tick() => Update();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Update()
    {
        HudAlignmentTests.Events.Add("hud");
        Updates++;
        var cam = SceneSingleton<CameraStateManager>.i;
        center.position = cam.projectedNose;
        center.rotation = new Quaternion { Roll = cam.projectedRoll };
        Velocity.position = cam.projectedVelocity;
        AircraftPitch = aircraft.Pitch;
    }
}
public sealed class CameraStateManager
{
    public bool isActiveAndEnabled = true;
    public Unit followingUnit;
    public object currentState, chaseState = new object(), cockpitState = new object();
    public Camera mainCamera = new Camera();
    public Vector3 projectedNose, finalNose, projectedVelocity, finalVelocity;
    public float projectedRoll, finalRoll;
    public void Tick() => LateUpdate();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LateUpdate()
    {
        HudAlignmentTests.Events.Add("camera");
        projectedNose = finalNose;
        projectedRoll = finalRoll;
        projectedVelocity = finalVelocity;
    }
}
public sealed class CombatHUD
{
    public Aircraft aircraft;
    public bool isActiveAndEnabled = true;
    public int Updates;
    public readonly Transform Reticle = new Transform();
    public void Tick() => LateUpdate();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LateUpdate()
    {
        HudAlignmentTests.Events.Add("combat");
        Updates++;
        Reticle.position = SceneSingleton<CameraStateManager>.i.projectedNose;
    }
}
public sealed class HeadMountedDisplay
{
    public bool isActiveAndEnabled = true;
    public int Updates;
    public bool Visible;
    public void Tick() => Update();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Update()
    {
        HudAlignmentTests.Events.Add("look-away");
        Updates++;
        var hud = SceneSingleton<FlightHud>.i;
        if (hud != null) Visible = hud.GetHUDCenter().position.x > 500;
    }
}
