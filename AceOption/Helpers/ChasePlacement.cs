using UnityEngine;

namespace AceOption.Helpers;

internal static class ChasePlacement
{
    private static Transform owner, parent;
    private static Vector3 position;
    private static Quaternion rotation;

    internal static void Reset() { owner = parent = null; }
    internal static void Capture(CameraStateManager cam)
    {
        owner = cam.transform;
        parent = owner.parent;
        position = owner.localPosition;
        rotation = owner.localRotation;
    }
    internal static void RestoreForUpdate(CameraStateManager cam)
    {
        if (owner == null || owner != cam.transform || owner.parent != parent) { Reset(); return; }
        // Relative transforms survive origin shifts and prevent overlay feedback into vanilla smoothing.
        owner.localPosition = position;
        owner.localRotation = rotation;
    }
    internal static void ApplyDistance(CameraStateManager cam)
    {
        if (!Plugin.CameraAdjustments.Value) return;
        Vector3 boom = cam.transform.position - cam.cameraPivot.position;
        cam.transform.position = cam.cameraPivot.position + boom * Plugin.CameraDistance.Value;
    }
    internal static void ApplyFraming(CameraStateManager cam)
    {
        if (!Plugin.CameraAdjustments.Value || cam.mainCamera == null || cam.followingUnit == null || Plugin.PlaneVerticalOffset.Value == 0) return;
        Vector3 aircraft = Quaternion.Inverse(cam.transform.rotation) *
            (cam.followingUnit.transform.position - cam.transform.position);
        if (aircraft.z <= .01f) return;
        float slope = aircraft.y / aircraft.z;
        float change = 2 * Plugin.PlaneVerticalOffset.Value * Mathf.Tan(cam.mainCamera.fieldOfView * Mathf.Deg2Rad / 2);
        float angle = Mathf.Rad2Deg * (Mathf.Atan(slope + change) - Mathf.Atan(slope));
        cam.transform.rotation *= Quaternion.AngleAxis(angle, Vector3.right);
    }
}
