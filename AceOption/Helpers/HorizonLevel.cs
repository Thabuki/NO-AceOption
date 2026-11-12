using System;

namespace AceOption.Helpers;

// Attitude feedback only: world up expressed in aircraft axes, and body rates
// in radians/second. Native pitch is positive nose-down; roll is positive right.
internal static class HorizonLevel
{
    internal static bool TryInputs(float upX, float upY, float upZ,
        float pitchRate, float rollRate, out float pitch, out float roll)
    {
        pitch = roll = 0;
        if (!Finite(upX) || !Finite(upY) || !Finite(upZ) ||
            !Finite(pitchRate) || !Finite(rollRate)) return false;
        double length = Math.Sqrt(upX * upX + upY * upY + upZ * upZ);
        if (length < .001) return false;
        double x = upX / length, y = upY / length, z = upZ / length;
        double horizontal = Math.Sqrt(x * x + y * y);
        // Bank becomes undefined when pointing straight up/down. Fade its
        // correction there, letting pitch bring the nose back to the horizon.
        double bankWeight = Math.Min(1, horizontal / .1);
        double bank = horizontal > .000001 ? Math.Atan2(x, y) : 0;
        double elevation = Math.Atan2(z, horizontal);
        // Recover upright before pitching when inverted; avoid a half-loop.
        double pitchAuthority = 1 - bankWeight + bankWeight *
            (horizontal > .000001 ? Math.Max(0, y / horizontal) : 1);
        pitch = Clamp((elevation * 1.5 - pitchRate * .4) * pitchAuthority);
        roll = Clamp(bank * bankWeight * 1.25 + rollRate * .3);
        return true;
    }

    private static float Clamp(double value) => (float)Math.Max(-1, Math.Min(1, value));
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
