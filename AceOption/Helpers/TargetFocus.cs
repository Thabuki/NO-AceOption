using UnityEngine;

namespace AceOption.Helpers;

internal static class TargetFocus
{
    private static Quaternion orbit = Quaternion.identity, aim = Quaternion.identity;
    private static float heldTime;
    private static bool armed, blocked;
    internal static bool Active { get; private set; }

    internal static void Reset()
    {
        orbit = aim = Quaternion.identity;
        heldTime = 0;
        armed = blocked = Active = false;
    }
    internal static void Center() { Reset(); blocked = true; }

    internal static Unit GetTarget(CameraStateManager cam, bool held, float deltaTime, float holdDelay)
    {
        if (!held || !Plugin.TargetFocusEnabled.Value) { heldTime = 0; armed = blocked = false; return null; }
        if (blocked || cam == null || cam.currentState != cam.chaseState ||
            cam.followingUnit == null || !GameManager.IsLocalAircraft(cam.followingUnit))
        { heldTime = 0; armed = false; return null; }
        heldTime += Mathf.Max(0, deltaTime);
        armed |= heldTime >= Mathf.Max(.3f, holdDelay);
        if (!armed) return null;
        var hud = SceneSingleton<CombatHUD>.i;
        if (hud == null || hud.aircraft != cam.followingUnit) return null;
        var marked = hud.GetTargetList();
        if (marked == null || marked.Count == 0) return null;
        Unit newest = marked[0];
        return newest != null && newest != cam.followingUnit && !newest.disabled ? newest : null;
    }

    internal static bool Apply(CameraStateManager cam, Unit target, float deltaTime)
    {
        if (!Active && target == null) return false;
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 original = cam.transform.position;
        Quaternion basis = cam.transform.rotation;
        Quaternion inverse = Quaternion.Inverse(basis);
        Vector3 up = cam.followingUnit.transform.up;
        Vector3 destination = target != null ? target.transform.position : pivot;
        Vector3 direction = destination - pivot;
        if (direction.sqrMagnitude < .0001f) target = null;
        float blend = 1 - Mathf.Exp(-10 * Mathf.Max(0, deltaTime));
        Quaternion orbitGoal = target == null ? Quaternion.identity : inverse * SafeAim(direction, up, basis * orbit);
        orbit = Quaternion.Slerp(orbit, orbitGoal, blend);
        Vector3 camera = pivot + (basis * orbit * inverse) * (original - pivot);
        Vector3 sightline = destination - camera;
        Quaternion aimGoal = target == null || sightline.sqrMagnitude < .0001f
            ? Quaternion.identity : inverse * SafeAim(sightline, up, basis * aim);
        aim = Quaternion.Slerp(aim, aimGoal, blend);
        if (target == null && Quaternion.Angle(orbit, Quaternion.identity) < .05f &&
            Quaternion.Angle(aim, Quaternion.identity) < .05f)
        {
            orbit = aim = Quaternion.identity;
            Active = false;
            return false;
        }
        Active = true;
        cam.transform.position = camera;
        cam.transform.rotation = basis * aim;
        return true;
    }

    private static Quaternion SafeAim(Vector3 direction, Vector3 up, Quaternion previous)
    {
        direction = direction.normalized;
        if (Vector3.Cross(direction, up).sqrMagnitude < .001f) up = previous * Vector3.up;
        if (Vector3.Cross(direction, up).sqrMagnitude < .001f) up = previous * Vector3.right;
        return Quaternion.LookRotation(direction, up);
    }
}
