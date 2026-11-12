using System;

namespace AceOption.Helpers;

internal enum VibrationEffect { Gun, Ordnance, Damage }

// Independent envelopes prevent repeated gun shots from prolonging a damage pulse.
internal sealed class VibrationEnvelope
{
    private struct Pulse
    {
        internal float Start, Duration, Left, Right;
        internal float Level(float now) => Duration > 0 && now >= Start
            ? Math.Max(0, 1 - (now - Start) / Duration) : 0;
    }
    private readonly Pulse[] pulses = new Pulse[3];
    internal void Reset() => Array.Clear(pulses, 0, pulses.Length);

    internal void Fire(VibrationEffect effect, float now)
    {
        if (!Finite(now)) return;
        pulses[(int)effect] = effect switch
        {
            VibrationEffect.Gun => new Pulse { Start = now, Duration = .08f, Left = .18f, Right = .32f },
            VibrationEffect.Ordnance => new Pulse { Start = now, Duration = .18f, Left = .45f, Right = .55f },
            _ => new Pulse { Start = now, Duration = .3f, Left = .8f, Right = .5f }
        };
    }

    internal void Sample(float now, float gForce, float intensity, bool weapons, bool damage, bool strain,
        out float left, out float right)
    {
        float load = strain && Finite(gForce)
            ? Clamp(gForce >= 0 ? (gForce - 4) / 5 : (-gForce - 2) / 3) : 0;
        left = load * .22f;
        right = load * .3f;
        if (Finite(now))
            for (int i = 0; i < pulses.Length; i++)
            {
                if (i == (int)VibrationEffect.Damage ? !damage : !weapons)
                { pulses[i] = default; continue; }
                float level = pulses[i].Level(now);
                left = Math.Max(left, pulses[i].Left * level);
                right = Math.Max(right, pulses[i].Right * level);
            }
        float gain = Finite(intensity) ? Clamp(intensity) : 0;
        left = Clamp(left * gain);
        right = Clamp(right * gain);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
}
