using System;

namespace AceOption.Helpers;

// A button acts like a spring-centered rudder, with configurable press and return times.
// Timing is in seconds and has no Unity dependency, for offline verification.
internal sealed class ButtonYaw
{
    private float value;
    private bool usingButtons;

    internal float Update(float native, float raw, bool buttonSource, bool analogSource,
        float deltaTime, float riseTime, float returnTime)
    {
        // Pedals/triggers can also reach +/-1. Identify buttons from source metadata.
        // Unknown nonzero sources retain the game's response as well.
        if (analogSource || (!buttonSource && raw != 0))
        {
            Reset();
            return native;
        }
        if (buttonSource) usingButtons = true;
        if (!usingButtons) return native;

        float target = Math.Max(-1, Math.Min(1, raw));
        float remaining = Math.Max(0, deltaTime);
        if (value * target < 0)
        {
            // Unload the old direction first, then spend leftover time building
            // the other direction. Reversal timing stays independent of tick rate.
            float toCenter = Math.Abs(value) * Math.Max(0, returnTime);
            value = Move(value, 0, remaining, returnTime);
            remaining = Math.Max(0, remaining - toCenter);
        }
        float duration = Math.Abs(target) < Math.Abs(value) ? returnTime : riseTime;
        value = Move(value, target, remaining, duration);
        return value;
    }

    internal void Reset() { value = 0; usingButtons = false; }

    private static float Move(float from, float to, float elapsed, float duration)
    {
        if (duration <= 0) return to;
        float step = elapsed / duration;
        return from + Math.Sign(to - from) * Math.Min(Math.Abs(to - from), step);
    }
}
