using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AceOption.Helpers;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

internal static class BombTargetHighlightTests
{
    private static int checks;
    private static readonly Color normal = new Color(.1f, .9f, .2f, .37f);
    private static Aircraft aircraft;
    private static CameraStateManager camera;
    private static Image circle;
    private static Unit enemy;
    private static List<HUDUnitMarker> contacts;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Reset()
    {
        BombTargetHighlight.Restore();
        AceOption.Plugin.BombTargetHighlight.Value = true;
        DynamicMap.mapMaximized = false;
        aircraft = new Aircraft { NetworkHQ = new FactionHQ() };
        GameManager.Local = aircraft;
        camera = new CameraStateManager { followingUnit = aircraft };
        camera.currentState = camera.cockpitState;
        SceneSingleton<CameraStateManager>.i = camera;
        circle = new Image { color = normal };
        enemy = new Unit { NetworkHQ = new FactionHQ(), Known = new Vector3(500, 400, 1) };
        contacts = new List<HUDUnitMarker> { new HUDUnitMarker { unit = enemy } };
    }

    private static void Apply() => BombTargetHighlight.Apply(circle, aircraft, contacts);
    private static bool Red => circle.color == new Color(1, 0, 0, normal.a);

    private static void ContactsAndTracking()
    {
        Reset(); Apply();
        Check(Red && !contacts[0].selected, "An unmarked tracked enemy turns the aiming circle red, preserving alpha");
        contacts[0].selected = true; Apply();
        Check(Red, "Marking does not change highlight eligibility");
        contacts[0].selected = false;
        enemy.Known = new Vector3(600, 400, 1); Apply();
        Check(circle.color == normal, "Leaving the circle restores the original color");
        enemy.Known = new Vector3(500, 400, 1); Apply();
        Check(Red, "Reentering the circle restores red");
        enemy.Tracked = false;
        int reads = aircraft.NetworkHQ.KnownReads, projects = camera.mainCamera.Projections;
        Apply();
        Check(circle.color == normal, "A retained contact loses its highlight when native tracking expires");
        Check(aircraft.NetworkHQ.KnownReads == reads && camera.mainCamera.Projections == projects,
            "Untracked contacts never reach known-position lookup or camera projection");
        enemy.Tracked = true; enemy.HasKnown = false; Apply();
        Check(circle.color == normal, "Tracking without an authorized known position fails closed");
        enemy.HasKnown = true; Apply();
        Check(Red, "Faction reacquisition resumes highlighting without marking");
        Check(camera.mainCamera.LastPosition == enemy.Known,
            "The faction-known position is the position projected by the camera");
        enemy.disabled = true; Apply();
        Check(circle.color == normal, "Disabled enemies do not count");
        enemy.disabled = false; enemy.NetworkHQ = aircraft.NetworkHQ; Apply();
        Check(circle.color == normal, "Friendly contacts do not count");
        enemy.NetworkHQ = null; Apply();
        Check(circle.color == normal, "Contacts without a faction do not count");
        contacts[0].unit = aircraft; Apply();
        Check(circle.color == normal, "The local aircraft cannot highlight itself");
        contacts[0].unit = null; contacts.Add(null); Apply();
        Check(circle.color == normal, "Missing contacts are harmless");
        contacts = null; Apply();
        Check(circle.color == normal, "An unavailable contact list fails closed");
        Reset();
        contacts.Insert(0, new HUDUnitMarker { unit = new Unit { Known = new Vector3(600, 400, 1), NetworkHQ = new FactionHQ() } });
        Apply();
        Check(Red, "Any qualifying enemy counts, not just the first contact");
        contacts.Clear(); Apply();
        Check(circle.color == normal, "An empty contact list restores the circle");
    }

    private static void CircleGeometry()
    {
        Reset();
        foreach (var point in new[] { new Vector3(520, 400, 1), new Vector3(480, 400, 1),
            new Vector3(500, 420, 1), new Vector3(500, 380, 1) })
        {
            enemy.Known = point; Apply();
            Check(Red, "The circular boundary counts as inside");
        }
        foreach (var point in new[] { new Vector3(520.1f, 400, 1), new Vector3(515, 415, 1),
            new Vector3(500, 400, -1), new Vector3(500, 400, 0) })
        {
            enemy.Known = point; Apply();
            Check(circle.color == normal, "Outside-circle, square-corner and behind-camera points do not count");
        }
        circle.rectTransform.rect = new Rect(-10, -35, 40, 40);
        enemy.Known = new Vector3(510, 385, 1); Apply();
        Check(Red, "A noncentral pivot uses the rendered rectangle center");
        Reset();
        circle.rectTransform.scaleX = 2; circle.rectTransform.scaleY = 3;
        circle.rectTransform.angle = (float)Math.PI / 2;
        enemy.Known = new Vector3(443, 400, 1); Apply();
        Check(Red, "Parent scaling and rotation are resolved in circle-local coordinates");
        enemy.Known = new Vector3(436, 400, 1); Apply();
        Check(circle.color == normal, "Scaled circles retain their local circular boundary");
        Reset();
        camera.mainCamera.Zoom = 2;
        enemy.Known = new Vector3(509, 400, 1); Apply();
        Check(Red, "Camera zoom projects the authorized position into the visible circle");
        enemy.Known = new Vector3(511, 400, 1); Apply();
        Check(circle.color == normal, "Zoom can move a contact outside the circle");
        Reset();
        circle.rectTransform.x = 0; enemy.Known = new Vector3(-1, 400, 1); Apply();
        Check(circle.color == normal, "A contact outside the viewport cannot trigger an overlapping offscreen ring");
        Reset(); circle.rectTransform.rect = new Rect(0, 0, 0, 40); Apply();
        Check(circle.color == normal, "A zero-size ring cannot highlight");
        Reset(); circle.rectTransform.ConversionSucceeds = false; Apply();
        Check(circle.color == normal, "A failed screen-to-circle conversion fails closed");
        Reset();
        var uiCamera = new Camera();
        circle.canvas = new Canvas { renderMode = RenderMode.ScreenSpaceCamera, worldCamera = uiCamera };
        Apply();
        Check(Red && RectTransformUtility.LastCamera == uiCamera, "A camera canvas uses its own UI camera");
        circle.canvas.renderMode = RenderMode.ScreenSpaceOverlay; Apply();
        Check(Red && RectTransformUtility.LastCamera == null, "An overlay canvas uses overlay coordinate conversion");
    }

    private static void VisibilityAndLifecycle()
    {
        Reset(); camera.currentState = camera.chaseState; Apply();
        Check(Red, "Chase view supports the same unmarked contact highlight");
        camera.currentState = new object(); Apply();
        Check(circle.color == normal, "Spectator and temporary camera states do not highlight");
        Reset(); camera.followingUnit = new Unit(); Apply();
        Check(circle.color == normal, "A camera following another unit does not highlight");
        Reset(); aircraft.remoteSim = true; Apply();
        Check(circle.color == normal, "Remote aircraft do not highlight");
        Reset(); GameManager.Local = new Aircraft(); Apply();
        Check(circle.color == normal, "Only the local aircraft can highlight");
        Reset(); aircraft.NetworkHQ = null; Apply();
        Check(circle.color == normal, "A missing player faction fails closed");
        Reset(); SceneSingleton<CameraStateManager>.i = null; Apply();
        Check(circle.color == normal, "A missing camera manager fails closed");
        Reset(); camera.mainCamera = null; Apply();
        Check(circle.color == normal, "A missing main camera fails closed");
        Reset(); Apply(); circle.enabled = false; Apply();
        Check(circle.color == normal, "A hidden ring releases its tint");
        Reset(); circle.Active = false; Apply();
        Check(circle.color == normal, "An inactive HUD is not highlighted");
        Reset(); circle.color = new Color(0, 1, 0, 0); Apply();
        Check(circle.color.a == 0 && circle.color.g == 1, "A transparent ring stays transparent");
        Reset(); Apply(); AceOption.Plugin.BombTargetHighlight.Value = false; Apply();
        Check(circle.color == normal, "Disabling the feature restores the original color");
        Reset(); Apply(); DynamicMap.mapMaximized = true; Apply();
        Check(circle.color == normal, "The full map cannot retain the tint");
        Reset(); Apply(); BombTargetHighlight.Restore(); BombTargetHighlight.Restore();
        Check(circle.color == normal, "Weapon/aircraft/plugin cleanup is safe to repeat");
        Reset(); Apply();
        var previous = circle;
        circle = new Image { color = normal }; Apply();
        Check(previous.color == normal && Red, "Replacing the HUD restores the old image and highlights the new one");
        var theme = new Color(.8f, .6f, .1f, .7f);
        circle.color = theme; BombTargetHighlight.Restore();
        Check(circle.color == theme, "Cleanup preserves a newer native or theme color");
        Apply(); BombTargetHighlight.Restore();
        Check(circle.color == theme, "A later highlight restores the updated theme color");
        Reset(); Apply();
        BombTargetHighlight.Apply(null, aircraft, contacts);
        Check(circle.color == normal, "A missing replacement ring releases the previous tint");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HarmonyHooks()
    {
        var harmony = new Harmony("AceOption.BombTargetHighlightTests");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        try
        {
            Reset();
            var hud = new CombatHUD(aircraft, contacts);
            SceneSingleton<CombatHUD>.i = hud;
            var bombing = new HUDBombingState(circle);
            var marked = new List<Unit>();
            bombing.UpdateWeaponDisplay(aircraft, marked);
            Check(Red && marked.Count == 0 && bombing.Updates == 1,
                "The real Harmony postfix highlights an unmarked faction contact after the native display runs");
            bombing.UpdateWeaponDisplay(aircraft, marked);
            Check(bombing.ColorDuringNative == normal && Red,
                "The real Harmony prefix restores the previous color before each native update");
            enemy.Tracked = false; bombing.UpdateWeaponDisplay(aircraft, marked);
            Check(circle.color == normal, "The patched display clears a stale contact's highlight");
            enemy.Tracked = true;
            bombing.UpdateWeaponDisplay(aircraft, marked);
            hud.ShowWeaponStation(new WeaponStation());
            Check(circle.color == normal && hud.WeaponChanges == 1,
                "The actual weapon-change hook restores the circle without suppressing the native method");
            bombing.UpdateWeaponDisplay(aircraft, marked);
            hud.RemoveAircraft();
            Check(circle.color == normal && hud.aircraft == null,
                "The actual aircraft-removal hook restores the circle before native cleanup");
            hud.aircraft = new Aircraft(); bombing.UpdateWeaponDisplay(aircraft, marked);
            Check(circle.color == normal, "The postfix refuses contacts belonging to another aircraft's HUD");
            hud.aircraft = aircraft;
            bombing.NativeColor = new Color(.7f, .8f, .9f, .6f);
            bombing.UpdateWeaponDisplay(aircraft, marked);
            Check(circle.color == new Color(1, 0, 0, .6f),
                "The postfix preserves the alpha written by the native display during this update");
            BombTargetHighlight.Restore();
            Check(circle.color == bombing.NativeColor.Value, "Cleanup restores the latest native display color");
        }
        finally
        {
            BombTargetHighlight.Restore();
            harmony.UnpatchSelf();
        }
    }

    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string path = Path.Combine(args[0], "BepInEx", "core", new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        ContactsAndTracking(); CircleGeometry(); VisibilityAndLifecycle();
        HarmonyHooks();
        BombTargetHighlight.Restore();
        Console.WriteLine("PASS: " + checks + " bomb-circle tracking, geometry and color lifecycle assertions with real Harmony hooks");
    }
}

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x*x + y*y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static bool operator ==(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode();
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static bool operator ==(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        public static bool operator !=(Color a, Color b) => !(a == b);
        public override bool Equals(object o) => o is Color c && this == c;
        public override int GetHashCode() => r.GetHashCode();
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public Vector2 center => new Vector2(x+width/2, y+height/2);
    }
    public class RectTransform
    {
        public Rect rect = new Rect(-20, -20, 40, 40);
        public float x = 500, y = 400, scaleX = 1, scaleY = 1, angle;
        public bool ConversionSucceeds = true;
    }
    public static class RectTransformUtility
    {
        public static Camera LastCamera;
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screen, Camera cam, out Vector2 local)
        {
            LastCamera = cam;
            float x = screen.x-rect.x, y = screen.y-rect.y;
            float cos = (float)Math.Cos(rect.angle), sin = (float)Math.Sin(rect.angle);
            local = new Vector2((cos*x+sin*y)/rect.scaleX, (-sin*x+cos*y)/rect.scaleY);
            return rect.ConversionSucceeds;
        }
    }
    public class Camera
    {
        public float Zoom = 1;
        public int Projections;
        public Vector3 LastPosition;
        public Vector3 WorldToScreenPoint(Vector3 position)
        {
            Projections++; LastPosition = position;
            return new Vector3(500+(position.x-500)*Zoom, 400+(position.y-400)*Zoom, position.z);
        }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class Canvas { public RenderMode renderMode; public Camera worldCamera; }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a,b); }
    public static class Screen { public static int width = 1000, height = 800; }
}
namespace UnityEngine.UI
{
    public class Image
    {
        public bool enabled = true, Active = true;
        public bool isActiveAndEnabled => enabled && Active;
        public Color color;
        public RectTransform rectTransform = new RectTransform();
        public Canvas canvas;
    }
}
namespace AceOption
{
    public class Setting<T> { public T Value; public Setting(T value) { Value = value; } }
    public static class Plugin { public static Setting<bool> BombTargetHighlight = new Setting<bool>(false); }
}
public class SceneSingleton<T> { public static T i; }
public class Unit
{
    public FactionHQ NetworkHQ;
    public bool disabled, Tracked = true, HasKnown = true;
    public Vector3 Known;
    public object transform => throw new Exception("Enemy live transforms must never be read");
}
public class Aircraft : Unit { public bool remoteSim; }
public class HUDUnitMarker { public Unit unit; public bool selected; }
public class WeaponStation { }
public class CombatHUD
{
    public Aircraft aircraft;
    private List<HUDUnitMarker> markers;
    public int WeaponChanges;
    public CombatHUD(Aircraft aircraft, List<HUDUnitMarker> contacts) { this.aircraft = aircraft; markers = contacts; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ShowWeaponStation(WeaponStation station) { WeaponChanges++; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void RemoveAircraft() { aircraft = null; }
}
public class HUDBombingState
{
    private Image ccipPipper;
    public int Updates;
    public Color ColorDuringNative;
    public Color? NativeColor;
    public HUDBombingState(Image circle) { ccipPipper = circle; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UpdateWeaponDisplay(Aircraft aircraft, List<Unit> targetList)
    {
        Updates++; ColorDuringNative = ccipPipper.color;
        if (NativeColor.HasValue) ccipPipper.color = NativeColor.Value;
    }
}
public struct GlobalPosition { public Vector3 Value; public Vector3 ToLocalPosition() => Value; }
public class FactionHQ
{
    public int KnownReads;
    public bool IsTargetBeingTracked(Unit unit) => unit.Tracked;
    public bool TryGetKnownPosition(Unit unit, out GlobalPosition position)
    {
        KnownReads++;
        if (!unit.Tracked) throw new Exception("Known position queried without tracking");
        position = new GlobalPosition { Value = unit.Known }; return unit.HasKnown;
    }
}
public class CameraStateManager
{
    public Camera mainCamera = new Camera();
    public Unit followingUnit;
    public object cockpitState = new object(), chaseState = new object(), currentState;
}
public static class GameManager
{
    public static Aircraft Local;
    public static bool IsLocalAircraft(Aircraft aircraft) => aircraft == Local;
}
public static class DynamicMap { public static bool mapMaximized; }
