using System;

namespace AceOption.Helpers;

internal sealed class FreeLookOrbit
{
    public float Pan { get; private set; }
    public float Tilt { get; private set; }
    public bool Recentering { get; private set; }
    private float idle;

    public void Reset() { Pan = Tilt = idle = 0; Recentering = false; }

    // Preserve the original look smoothing; only idle recentering uses the configured rate.
    public float GetSmoothing(float deltaTime, float viewSmoothing, float recenterSpeed) =>
        Math.Min(1, (Recentering ? recenterSpeed : 5f) * Math.Max(0, deltaTime) / Math.Max(.01f, viewSmoothing));

    // The caller freezes this state while menus or target focus own input.
    public void Update(float panAxis, float tiltAxis, float degreesPerSecond,
        float pitchSign, float deltaTime, float delay, float deadzone)
    {
        float seconds = Math.Max(deltaTime, 0);
        bool quiet = Math.Abs(panAxis) <= deadzone && Math.Abs(tiltAxis) <= deadzone;
        if (quiet)
        {
            idle += seconds;
            if (idle >= Math.Max(0, delay))
            {
                Recentering = true;
                Pan = Tilt = 0;
            }
            return;
        }
        idle = 0;
        Recentering = false;
        if (Math.Abs(panAxis) <= deadzone) panAxis = 0;
        if (Math.Abs(tiltAxis) <= deadzone) tiltAxis = 0;
        float step = seconds * degreesPerSecond;
        Pan = Limit(Pan + panAxis * step, 170);
        Tilt = Limit(Tilt + tiltAxis * pitchSign * step, 85);
    }

    private static float Limit(float angle, float extent) => Math.Min(extent, Math.Max(-extent, angle));
}
