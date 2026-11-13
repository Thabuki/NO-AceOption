// Exercise production camera helpers outside Unity. System.Numerics supplies the
// quaternion/vector math; this cannot validate Unity rendering, collisions or input timing.
using System;
using System.Collections.Generic;
using AceOption.Helpers;
using UnityEngine;
using N = System.Numerics;

namespace UnityEngine
{
    public struct Vector3
    {
        internal N.Vector3 value;
        public Vector3(float x, float y, float z) { value = new N.Vector3(x, y, z); }
        internal Vector3(N.Vector3 v) { value = v; }
        public float sqrMagnitude => value.LengthSquared();
        public float magnitude => value.Length();
        public float y => value.Y;
        public float z => value.Z;
        public Vector3 normalized => new Vector3(N.Vector3.Normalize(value));
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.value + b.value);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.value - b.value);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.value * b);
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.value / b);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.value);
        public static float Dot(Vector3 a, Vector3 b) => N.Vector3.Dot(a.value, b.value);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(N.Vector3.Lerp(a.value, b.value, t));
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            float angle = (float)Math.Acos(Math.Max(-1, Math.Min(1, Dot(a.normalized, b.normalized))));
            if (angle < .001f) return Lerp(a, b, t);
            return (a.normalized * (float)Math.Sin((1-t)*angle) + b.normalized * (float)Math.Sin(t*angle)) /
                (float)Math.Sin(angle) * (a.magnitude + (b.magnitude-a.magnitude)*t);
        }
        public static Vector3 ClampMagnitude(Vector3 a, float limit) => a.magnitude > limit ? a.normalized * limit : a;
        public static Vector3 Project(Vector3 a, Vector3 onto) => onto * (Dot(a, onto) / onto.sqrMagnitude);
        public static float Angle(Vector3 a, Vector3 b) => (float)Math.Acos(Math.Max(-1, Math.Min(1, Dot(a.normalized,b.normalized)))) * Mathf.Rad2Deg;
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float smooth, float maxSpeed, float dt)
        {
            float omega = 2 / smooth;
            float x = omega * dt;
            float decay = 1 / (1 + x + .48f*x*x + .235f*x*x*x);
            Vector3 change = current - target;
            Vector3 temp = (velocity + change * omega) * dt;
            velocity = (velocity - temp * omega) * decay;
            Vector3 result = target + (change + temp) * decay;
            if (Dot(target-current, result-target) > 0) { result = target; velocity = Vector3.zero; }
            return result;
        }
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(N.Vector3.Cross(a.value, b.value));
    }
    public struct Quaternion
    {
        internal N.Quaternion value;
        internal Quaternion(N.Quaternion q) { value = q; }
        public static Quaternion identity => new Quaternion(N.Quaternion.Identity);
        public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion AngleAxis(float degrees, Vector3 axis) =>
            new Quaternion(N.Quaternion.CreateFromAxisAngle(axis.normalized.value, degrees * (float)Math.PI / 180f));
        public static Quaternion Inverse(Quaternion q) => new Quaternion(N.Quaternion.Inverse(q.value));
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(a.value * b.value);
        public static Vector3 operator *(Quaternion q, Vector3 v) => new Vector3(N.Vector3.Transform(v.value, q.value));
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) =>
            new Quaternion(N.Quaternion.Slerp(a.value, b.value, t));
        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Min(1, Math.Abs(N.Quaternion.Dot(a.value, b.value)));
            // Unity's managed Quaternion.Angle treats dot > 0.999999 as identical.
            return dot > 0.999999f ? 0f : (float)(2 * 180 / Math.PI * Math.Acos(dot));
        }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = N.Vector3.Normalize(forward.value);
            var r = N.Vector3.Normalize(N.Vector3.Cross(up.value, f));
            var u = N.Vector3.Cross(f, r);
            var m = new N.Matrix4x4(r.X,r.Y,r.Z,0, u.X,u.Y,u.Z,0, f.X,f.Y,f.Z,0, 0,0,0,1);
            return new Quaternion(N.Quaternion.CreateFromRotationMatrix(m));
        }
    }
    public class Transform
    {
        public int PositionReads;
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 position
        {
            get { PositionReads++; return parent == null ? localPosition : parent.position + parent.rotation * localPosition; }
            set => localPosition = parent == null ? value : Quaternion.Inverse(parent.rotation) * (value - parent.position);
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public Vector3 up => rotation * Vector3.up;
        public Vector3 forward => rotation * Vector3.forward;
        public void SetPositionAndRotation(Vector3 point, Quaternion look) { position = point; rotation = look; }
    }
    public static class Mathf
    {
        public const float Deg2Rad = (float)Math.PI / 180f;
        public const float Rad2Deg = 180f / (float)Math.PI;
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Atan(float v) => (float)Math.Atan(v);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float a) => Math.Abs(a);
        public static float Clamp(float v, float a, float b) => Math.Min(b, Math.Max(a,v));
        public static float Lerp(float a, float b, float t) => a + (b-a)*Clamp(t,0,1);
        public static float InverseLerp(float a, float b, float v) => Clamp((v-a)/(b-a),0,1);
        public static float MoveTowards(float a, float b, float step) => Math.Abs(b-a) <= step ? b : a + Math.Sign(b-a)*step;
        public static float SmoothStep(float a, float b, float t) { t = Clamp(t,0,1); return Lerp(a,b,t*t*(3-2*t)); }
    }
    public class Camera { public float fieldOfView = 60f; }
    public static class Time { public static float deltaTime = 1f / 60; }
    public class Rigidbody { public Vector3 velocity; }
}
public class UnitDefinition { public float length = 16, width = 12; }
public class Unit
{
    public bool disabled;
    public Transform transform = new Transform();
    public UnitDefinition definition = new UnitDefinition();
    public FactionHQ NetworkHQ = new FactionHQ();
}
public struct GlobalPosition { public Vector3 Position; }
public static class GlobalPositionExtensions
{
    public static Vector3 Origin;
    public static Vector3 ToLocalPosition(this GlobalPosition position) => position.Position + Origin;
}
public class FactionHQ
{
    public HashSet<Unit> Untracked = new HashSet<Unit>(), Unknown = new HashSet<Unit>();
    public Dictionary<Unit, Vector3> Positions = new Dictionary<Unit, Vector3>();
    public int KnownPositionRequests;
    public bool IsTargetBeingTracked(Unit unit) => unit.NetworkHQ == this || !Untracked.Contains(unit);
    public bool TryGetKnownPosition(Unit unit, out GlobalPosition position)
    {
        KnownPositionRequests++;
        position = new GlobalPosition();
        if (Unknown.Contains(unit)) return false;
        position.Position = Positions.TryGetValue(unit, out var known) ? known : unit.transform.position;
        return true;
    }
}
public class Aircraft : Unit { }
public class CameraStateManager
{
    public Transform transform = new Transform();
    public Transform cameraPivot = new Transform();
    public Camera mainCamera = new Camera();
    public object chaseState = new object();
    public object currentState;
    public Unit followingUnit;
    public Rigidbody followingRB = new Rigidbody();
}
public static class GameManager
{
    public static Unit local;
    public static bool IsLocalAircraft(Unit unit) => unit == local;
}
public static class SceneSingleton<T> { public static T i; }
public class CombatHUD
{
    public Aircraft aircraft;
    public List<Unit> targets = new List<Unit>();
    public List<Unit> GetTargetList() => targets;
}
namespace AceOption
{
    public class Setting { public float Value = 0.75f; }
    public class Toggle { public bool Value = true; }
    public static class Plugin
    {
        public static Toggle CameraAdjustments = new Toggle(), TargetFocusEnabled = new Toggle();
        public static Setting CameraDistance = new Setting();
        public static Setting PlaneVerticalOffset = new Setting { Value = 0f };
    }
}

internal static class CameraOverlayTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Near(Vector3 expected, Vector3 actual, string message) =>
        Check((expected - actual).sqrMagnitude < 0.002f, message);
    private static float PlaneViewportY(CameraStateManager cam)
    {
        Vector3 point = Quaternion.Inverse(cam.transform.rotation)
            * (cam.followingUnit.transform.position - cam.transform.position);
        return 0.5f + point.y / point.z / (2f * (float)Math.Tan(cam.mainCamera.fieldOfView * Math.PI / 360));
    }
    private static void DetectionTests()
    {
        TargetFocus.Reset();
        var aircraft = new Aircraft();
        var faction = aircraft.NetworkHQ;
        var target = new Unit();
        target.transform.position = new Vector3(-1000, 100, 0);
        faction.Positions[target] = new Vector3(1000, 0, 0);
        var cam = new CameraStateManager { followingUnit = aircraft };
        cam.currentState = cam.chaseState;
        GameManager.local = aircraft;
        var hud = new CombatHUD { aircraft = aircraft };
        hud.targets.Add(target);
        SceneSingleton<CombatHUD>.i = hud;
        faction.Untracked.Add(target);
        Check(TargetFocus.GetTarget(cam, true, .3f, .3f) == null,
            "A retained stale mark cannot start focus even when a known position exists");
        Check(!TargetFocus.Apply(cam, target, 1f / 60) && !TargetFocus.Active,
            "Applying an untracked target directly cannot start focus");
        Check(faction.KnownPositionRequests == 0 && target.transform.PositionReads == 0,
            "Untracked targets never resolve a position or read their live transform");

        faction.Untracked.Clear();
        Check(TargetFocus.GetTarget(cam, true, 0, .3f) == target,
            "Faction reacquisition permits focus while the action remains held");
        Action<Unit> frame = unit => {
            cam.transform.SetPositionAndRotation(new Vector3(0, 3, -20), Quaternion.identity);
            TargetFocus.Apply(cam, unit, 1f / 60);
        };
        for (int i = 0; i < 240; i++) frame(target);
        Near((faction.Positions[target] - cam.transform.position).normalized, cam.transform.forward,
            "Focus aims at the faction-known position even when the live position differs");
        faction.Positions[target] = new Vector3(0, 0, -1000);
        GlobalPositionExtensions.Origin = new Vector3(250, 0, 100);
        for (int i = 0; i < 240; i++) frame(target);
        Near((faction.Positions[target] + GlobalPositionExtensions.Origin - cam.transform.position).normalized,
            cam.transform.forward, "Focus refreshes authorized positions and converts the current floating origin");
        Check(target.transform.PositionReads == 0, "Tracked focus never bypasses faction position data for a live transform");
        GlobalPositionExtensions.Origin = Vector3.zero;

        // Detection can change after selection and before camera application.
        var selected = TargetFocus.GetTarget(cam, true, 0, .3f);
        faction.Untracked.Add(target);
        int requests = faction.KnownPositionRequests;
        for (int i = 0; i < 240; i++)
        {
            target.transform.position = new Vector3(i * 100, 200, -i * 200);
            Check(TargetFocus.GetTarget(cam, true, 1f / 60, .3f) == null,
                "A held action cannot select a lost contact");
            frame(selected);
        }
        Check(!TargetFocus.Active, "Detection loss smoothly finishes returning to chase even with a retained target reference");
        Near(new Vector3(0, 3, -20), cam.transform.position, "Detection loss restores chase position");
        Near(Vector3.forward, cam.transform.forward, "Detection loss restores chase direction");
        Check(faction.KnownPositionRequests == requests && target.transform.PositionReads == 0,
            "Moving a lost target never affects camera return or performs position lookups");
        Check(hud.targets.Count == 1 && hud.targets[0] == target,
            "Ending camera focus preserves the native marked-target list");

        faction.Untracked.Clear();
        faction.Unknown.Add(target);
        Check(TargetFocus.GetTarget(cam, true, 0, .3f) == null && !TargetFocus.Apply(cam, target, 1f / 60),
            "A tracked target without an authorized known position fails closed");
        faction.Unknown.Clear();
        aircraft.NetworkHQ = null;
        Check(TargetFocus.GetTarget(cam, true, 0, .3f) == null && !TargetFocus.Apply(cam, target, 1f / 60),
            "An absent player faction fails closed");
        aircraft.NetworkHQ = faction;
        Check(TargetFocus.GetTarget(cam, true, 0, .3f) == target,
            "Restoring faction data allows a held focus to resume");
        target.disabled = true;
        Check(!TargetFocus.Apply(cam, target, 1f / 60), "A target disabled after selection cannot activate focus");
        target.disabled = false;
        hud.targets.Insert(0, aircraft);
        Check(TargetFocus.GetTarget(cam, true, 0, .3f) == null, "The local aircraft cannot focus itself");
        TargetFocus.Reset();
    }
    public static void Main()
    {
        var aircraft = new Aircraft();
        GameManager.local = aircraft;
        var cam = new CameraStateManager { followingUnit = aircraft };
        cam.currentState = cam.chaseState;
        cam.transform.parent = cam.cameraPivot;
        cam.transform.localPosition = new Vector3(0, 3, -20);
        var hud = new CombatHUD { aircraft = aircraft };
        SceneSingleton<CombatHUD>.i = hud;
        var first = new Unit(); var last = new Unit();
        hud.targets.Add(first); hud.targets.Insert(0, last);

        AceOption.Plugin.CameraAdjustments.Value = false;
        AceOption.Plugin.TargetFocusEnabled.Value = false;
        Check(TargetFocus.GetTarget(cam, true, 3f, 0.3f) == null, "Disabled target focus ignores a bound hold");
        AceOption.Plugin.TargetFocusEnabled.Value = true;
        Check(TargetFocus.GetTarget(cam, true, 0.2f, 0.3f) == null, "Enabling focus starts a fresh hold delay");
        Check(TargetFocus.GetTarget(cam, true, 0.11f, 0.3f) == last, "Target focus works with camera adjustments disabled");
        AceOption.Plugin.TargetFocusEnabled.Value = false;
        Check(TargetFocus.GetTarget(cam, true, 0f, 0.3f) == null, "Disabling focus clears an armed hold");
        AceOption.Plugin.TargetFocusEnabled.Value = true;
        Check(TargetFocus.GetTarget(cam, true, 0.2f, 0.3f) == null, "Re-enabling focus waits the full delay again");
        AceOption.Plugin.CameraAdjustments.Value = true;

        TargetFocus.Reset();
        Check(TargetFocus.GetTarget(cam, true, 0.1f, 0.3f) == null, "Short selection tap must not focus");
        Check(!TargetFocus.Active, "Pending hold must not claim the camera");
        Check(TargetFocus.GetTarget(cam, false, 0f, 0.3f) == null, "Release must cancel pending hold");
        Check(TargetFocus.GetTarget(cam, true, 0.2f, 0.3f) == null, "Separate taps must not accumulate");
        var newlySelected = new Unit();
        hud.targets.Insert(0, newlySelected);
        Check(TargetFocus.GetTarget(cam, true, 0.11f, 0.3f) == newlySelected,
            "Threshold must focus the target selected during the hold");
        Check(TargetFocus.GetTarget(cam, true, 0.01f, 2f) == newlySelected,
            "Increasing delay must not interrupt an already armed hold");
        Check(TargetFocus.GetTarget(cam, false, 0f, 2f) == null, "Release must stop focus without a delay");
        Check(TargetFocus.GetTarget(cam, true, 0.1f, 0.3f) == null, "Repress must wait the full delay again");
        TargetFocus.Center();
        Check(TargetFocus.GetTarget(cam, true, 1f, 0.3f) == null, "Center must keep focus blocked past the threshold");
        TargetFocus.GetTarget(cam, false, 0f, 0.3f);
        Check(TargetFocus.GetTarget(cam, true, 0.1f, 0.3f) == null, "Rearmed hold must still wait");
        Check(TargetFocus.GetTarget(cam, true, 0f, 0.05f) == null,
            "Lowering delay below the minimum must not bypass the 0.3 second wait");
        Check(TargetFocus.GetTarget(cam, true, 0.21f, 0.05f) == newlySelected,
            "An undersized delay must activate once the minimum wait has passed");
        TargetFocus.Reset();
        Check(TargetFocus.GetTarget(cam, true, 0.2f, 0.3f) == null, "New chase session must reset hold timer");
        cam.currentState = new object();
        Check(TargetFocus.GetTarget(cam, true, 1f, 0.3f) == null, "Other views cannot complete the hold timer");
        cam.currentState = cam.chaseState;
        Check(TargetFocus.GetTarget(cam, true, 0.2f, 0.3f) == null, "Leaving chase must clear pending time");
        TargetFocus.Reset();
        hud.targets.Remove(newlySelected);

        Check(TargetFocus.GetTarget(cam, false, 0f, 0f) == null, "Not held must not focus");
        Check(TargetFocus.GetTarget(cam, true, 0.29f, 0f) == null, "A zero delay must still wait at least 0.3 seconds");
        Check(TargetFocus.GetTarget(cam, true, 0.02f, 0f) == last, "Focus must select most recently marked target");
        last.disabled = true;
        Check(TargetFocus.GetTarget(cam, true, 0f, 0f) == null, "Destroyed target must release focus");
        last.disabled = false;
        TargetFocus.Center();
        Check(TargetFocus.GetTarget(cam, true, 0f, 0f) == null, "Center must block held focus");
        TargetFocus.GetTarget(cam, false, 0f, 0f);
        Check(TargetFocus.GetTarget(cam, true, 0.3f, 0f) == last, "Releasing and holding must rearm focus after the minimum wait");
        cam.currentState = new object();
        Check(TargetFocus.GetTarget(cam, true, 0f, 0f) == null, "Other camera modes must be untouched");
        cam.currentState = cam.chaseState;
        cam.followingUnit = new Aircraft();
        Check(TargetFocus.GetTarget(cam, true, 0f, 0f) == null, "Spectated aircraft must be untouched");
        cam.followingUnit = aircraft;
        hud.targets.Clear();
        Check(TargetFocus.GetTarget(cam, true, 0.3f, 0f) == null, "No marked target must be a no-op");

        var baseline = cam.transform.localPosition;
        ChasePlacement.Capture(cam);
        for (int frame = 0; frame < 120; frame++)
        {
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.Capture(cam);
            ChasePlacement.ApplyDistance(cam);
            Near(baseline * 0.75f, cam.transform.localPosition, "Distance must not compound across frames");
            cam.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        }
        ChasePlacement.RestoreForUpdate(cam);
        Near(Vector3.forward, cam.transform.rotation * Vector3.forward, "Previous overlay rotation must not leak back");
        cam.cameraPivot.position = new Vector3(100000, 0, -100000);
        ChasePlacement.RestoreForUpdate(cam);
        Near(baseline, cam.transform.localPosition, "Origin shifts must preserve local baseline");
        cam.cameraPivot.position = Vector3.zero;

        // Disabling live must remove the previous overlays on the next update,
        // without overwriting the suggested or customized slider values.
        AceOption.Plugin.CameraDistance.Value = 0.65f;
        AceOption.Plugin.PlaneVerticalOffset.Value = -0.15f;
        ChasePlacement.RestoreForUpdate(cam);
        ChasePlacement.Capture(cam);
        float unadjustedHeight = PlaneViewportY(cam);
        ChasePlacement.ApplyDistance(cam);
        ChasePlacement.ApplyFraming(cam);
        Near(baseline * 0.65f, cam.transform.localPosition, "Enabled adjustments must apply suggested distance");
        AceOption.Plugin.CameraAdjustments.Value = false;
        for (int frame = 0; frame < 3; frame++)
        {
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.Capture(cam);
            ChasePlacement.ApplyDistance(cam);
            ChasePlacement.ApplyFraming(cam);
            Near(baseline, cam.transform.localPosition, "Disabled adjustments must restore unscaled distance");
            Near(Vector3.forward, cam.transform.rotation * Vector3.forward, "Disabled adjustments must restore unadjusted framing");
        }
        Check(AceOption.Plugin.CameraDistance.Value == 0.65f && AceOption.Plugin.PlaneVerticalOffset.Value == -0.15f,
            "Disabling adjustments must retain both slider values");
        AceOption.Plugin.CameraAdjustments.Value = true;
        ChasePlacement.RestoreForUpdate(cam);
        ChasePlacement.Capture(cam);
        ChasePlacement.ApplyDistance(cam);
        ChasePlacement.ApplyFraming(cam);
        Near(baseline * 0.65f, cam.transform.localPosition, "Re-enabling adjustments must restore saved distance");
        Check(Math.Abs(PlaneViewportY(cam) - (unadjustedHeight - 0.15f)) < 0.0001f,
            "Re-enabling adjustments must restore saved framing");

        foreach (float distance in new[] { 0.25f, 1f, 2f })
        {
            AceOption.Plugin.CameraDistance.Value = distance;
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.ApplyDistance(cam);
            Near(baseline * distance, cam.transform.localPosition, "Live distance change incorrect");
        }
        // A framing offset is measured in viewport height, independent of distance/FOV.
        foreach (float fov in new[] { 35f, 60f, 100f })
        foreach (float distance in new[] { 0.25f, 0.75f, 2f })
        {
            cam.mainCamera.fieldOfView = fov;
            AceOption.Plugin.CameraDistance.Value = distance;
            AceOption.Plugin.PlaneVerticalOffset.Value = -0.15f;
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.ApplyDistance(cam);
            var positionBefore = cam.transform.position;
            float heightBefore = PlaneViewportY(cam);
            ChasePlacement.ApplyFraming(cam);
            Check(Math.Abs(PlaneViewportY(cam) - (heightBefore - 0.15f)) < 0.0001f,
                "Negative framing must move the plane down by the requested screen fraction");
            Near(positionBefore, cam.transform.position, "Framing must not change camera distance or position");
        }
        cam.mainCamera.fieldOfView = 60f;
        ChasePlacement.RestoreForUpdate(cam);
        AceOption.Plugin.PlaneVerticalOffset.Value = 0.1f;
        float originalHeight = PlaneViewportY(cam);
        ChasePlacement.ApplyFraming(cam);
        Check(Math.Abs(PlaneViewportY(cam) - (originalHeight + 0.1f)) < 0.0001f,
            "Positive framing must move the plane higher");
        ChasePlacement.RestoreForUpdate(cam);
        AceOption.Plugin.PlaneVerticalOffset.Value = 0f;
        ChasePlacement.ApplyFraming(cam);
        Near(Vector3.forward, cam.transform.rotation * Vector3.forward, "Zero offset must preserve original framing");
        AceOption.Plugin.PlaneVerticalOffset.Value = -0.15f;
        for (int frame = 0; frame < 120; frame++)
        {
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.Capture(cam);
            ChasePlacement.ApplyFraming(cam);
        }
        Check(Math.Abs(PlaneViewportY(cam) - (originalHeight - 0.15f)) < 0.0001f,
            "Framing must not accumulate across frames");
        var framedForward = cam.transform.rotation * Vector3.forward;

        foreach (var point in new[] { new Vector3(1000, 0, 0), new Vector3(0, 0, -1000), new Vector3(0, 1000, 0) })
        {
            TargetFocus.Reset();
            last.transform.position = point;
            for (int frame = 0; frame < 240; frame++)
            {
                ChasePlacement.RestoreForUpdate(cam);
                ChasePlacement.ApplyFraming(cam);
                TargetFocus.Apply(cam, last, 1f / 60);
            }
            Near((point - cam.transform.position).normalized,
                cam.transform.rotation * Vector3.forward, "Camera must face target, including behind/above");
            Check(Math.Abs(cam.transform.position.sqrMagnitude - baseline.sqrMagnitude) < 0.01,
                "Target focus must preserve orbit radius");
            for (int frame = 0; frame < 240; frame++)
            {
                ChasePlacement.RestoreForUpdate(cam);
                ChasePlacement.ApplyFraming(cam);
                TargetFocus.Apply(cam, null, 1f / 60);
            }
            Check(!TargetFocus.Active, "Release must finish returning");
            Near(baseline, cam.transform.localPosition, "Release must restore chase position");
            Near(framedForward, cam.transform.rotation * Vector3.forward, "Release must restore configured chase framing");
        }
        TargetFocus.Reset();
        last.transform.position = new Vector3(1000, 0, 0);
        hud.targets.Add(last);
        TargetFocus.Apply(cam, TargetFocus.GetTarget(cam, true, 0.3f, 0.3f), 1f / 60);
        Check(TargetFocus.Active, "A held focus can be disabled while active");
        AceOption.Plugin.TargetFocusEnabled.Value = false;
        for (int frame = 0; frame < 240; frame++)
        {
            ChasePlacement.RestoreForUpdate(cam);
            ChasePlacement.ApplyFraming(cam);
            var target = TargetFocus.GetTarget(cam, true, 1f / 60, 0.3f);
            Check(target == null, "Disabled focus cannot reacquire while the binding remains held");
            TargetFocus.Apply(cam, target, 1f / 60);
        }
        Check(!TargetFocus.Active, "Disabling target focus finishes returning to chase");
        Near(baseline, cam.transform.localPosition, "Disabling target focus restores chase position");
        Near(framedForward, cam.transform.rotation * Vector3.forward, "Disabling target focus restores chase framing");
        AceOption.Plugin.TargetFocusEnabled.Value = true;
        TargetFocus.Reset();
        last.transform.position = cam.cameraPivot.position;
        TargetFocus.Apply(cam, last, 1f / 60);
        Check(!TargetFocus.Active, "Coincident target must not produce an invalid rotation");
        cam.transform.parent = null;
        cam.transform.localPosition = new Vector3(42, 0, 0);
        ChasePlacement.RestoreForUpdate(cam);
        Near(new Vector3(42, 0, 0), cam.transform.position, "Reparented camera must discard stale baseline");
        DetectionTests();
        Console.WriteLine("PASS: " + checks + " camera overlay assertions (stand-in math and transforms)");
    }
}
