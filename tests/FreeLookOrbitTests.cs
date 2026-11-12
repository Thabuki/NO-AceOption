using System;
using AceOption.Helpers;

internal static class FreeLookOrbitTests
{
    private static int _checks;
    private static void Near(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
        _checks++;
    }

    public static void Main()
    {
        var orbit = new FreeLookOrbit();
        orbit.Update(1, 0.5f, 120, 1, 0.5f, 3, 0.05f);
        Near(60, orbit.Pan, "Look works without a held button");
        Near(30, orbit.Tilt, "Both axes work");
        orbit.Update(0, 0, 120, 1, 2.9f, 3, 0.05f);
        Near(60, orbit.Pan, "Release preserves angle before delay");
        orbit.Update(0, 0, 120, 1, 0.11f, 3, 0.05f);
        Near(0, orbit.Pan, "Delay expiry resets pan target");
        Near(0, orbit.Tilt, "Delay expiry resets tilt target");

        orbit.Update(1, 0, 120, 1, 0.5f, 3, 0.05f);
        orbit.Update(0, 0, 120, 1, 2.5f, 3, 0.05f);
        orbit.Update(-0.5f, 0, 120, 1, 0.1f, 3, 0.05f);
        orbit.Update(0, 0, 120, 1, 2.5f, 3, 0.05f);
        Near(54, orbit.Pan, "New movement restarts the full delay");
        orbit.Update(0.04f, -0.04f, 120, 1, 0.51f, 3, 0.05f);
        Near(0, orbit.Pan, "Drift inside deadzone does not restart timer");
        Near(0, orbit.Tilt, "Drift does not move tilt");

        orbit.Update(1, 0, 120, 1, 0.5f, 3, 0.05f);
        orbit.Update(0, 0, 120, 1, 1, 3, 0.05f);
        orbit.Update(0, 0, 120, 1, 0.1f, 0.5f, 0.05f);
        Near(0, orbit.Pan, "Changed delay applies to current timer");
        orbit.Update(1, 0, 120, 1, 0.5f, 3, 0.05f);
        orbit.Update(0, 0, 120, 1, 0.01f, 0, 0.05f);
        Near(0, orbit.Pan, "Zero delay returns on release");

        orbit.Update(1, 1, 120, -1, 10, 3, 0.05f);
        Near(170, orbit.Pan, "Right pan stops ten degrees before directly behind");
        Near(-85, orbit.Tilt, "Pitch inversion and limit");
        orbit.Update(1, 0, 120, 1, 10, 3, 0.05f);
        Near(170, orbit.Pan, "Holding at right limit does not wrap");
        orbit.Update(-1, 0, 120, 1, 0.1f, 3, 0.05f);
        Near(158, orbit.Pan, "Reversing from the limit responds immediately");
        orbit.Update(-1, 0, 120, 1, 10, 3, 0.05f);
        Near(-170, orbit.Pan, "Left pan limit is symmetric");
        orbit.Update(-1, 0, 120, 1, 10, 3, 0.05f);
        Near(-170, orbit.Pan, "Holding at left limit does not wrap");
        orbit.Update(0, 0, 120, 1, 3, 3, 0.05f);
        Near(0, orbit.Pan, "Auto-center works from the limit");
        orbit.Reset();
        Near(0, orbit.Pan, "Explicit center resets pan");
        Near(0, orbit.Tilt, "Explicit center resets tilt");

        orbit.Update(1, -1, 120, 1, 0.5f, 3, 1);
        Near(0, orbit.Pan, "Maximum deadzone ignores full positive input");
        Near(0, orbit.Tilt, "Maximum deadzone ignores full negative input");
        orbit.Update(1, -1, 120, 1, 0.5f, 3, 0.75f);
        Near(60, orbit.Pan, "Input above an extended deadzone still pans");
        Near(-60, orbit.Tilt, "Input above an extended deadzone still tilts");

        orbit.Reset();
        orbit.Update(1, -.5f, 120, 1, .5f, .15f, .05f);
        foreach (float speed in new[] { .1f, 5f, 20f })
            Near(.1f, orbit.GetSmoothing(.02f, 1f, speed), "Recenter speed does not change manual look smoothing");
        orbit.Update(0, 0, 120, 1, .1f, .15f, .05f);
        Near(60, orbit.Pan, "Speed does not shorten the idle delay");
        Near(.1f, orbit.GetSmoothing(.02f, 1f, 20f), "Look smoothing stays native during the delay");
        orbit.Update(0, 0, 120, 1, .06f, .15f, .05f);
        Near(0, orbit.Pan, "Recenter target becomes zero after the delay");
        Near(0, orbit.Tilt, "Both axes recenter after the same delay");

        foreach (float dt in new[] { 0f, 1f / 240, 1f / 60, 1f / 30, .5f })
            foreach (float smoothing in new[] { 0f, .01f, .1f, 1f, 2f })
                Near(Math.Min(1, 5 * dt / Math.Max(.01f, smoothing)), orbit.GetSmoothing(dt, smoothing, 5f),
                    "Default recenter speed preserves the previous frame blend");

        // These are the same angle blends used by the chase patch on each rendered axis.
        Near(58.8f, 60 * (1 - orbit.GetSmoothing(.02f, 1f, 1f)), "Lower speed returns pan more slowly");
        Near(-29.4f, -30 * (1 - orbit.GetSmoothing(.02f, 1f, 1f)), "Lower speed returns tilt more slowly");
        Near(48, 60 * (1 - orbit.GetSmoothing(.02f, 1f, 10f)), "Higher speed returns pan faster");
        Near(-24, -30 * (1 - orbit.GetSmoothing(.02f, 1f, 10f)), "Higher speed returns tilt faster");
        Near(.002f, orbit.GetSmoothing(.02f, 1f, .1f), "Slowest configured speed applies during a return");
        Near(.4f, orbit.GetSmoothing(.02f, 1f, 20f), "Changing speed applies to the current return immediately");
        orbit.Update(0, 0, 120, 1, 0, 30, .05f);
        Near(.4f, orbit.GetSmoothing(.02f, 1f, 20f), "Changing delay does not alter an already active return's speed");
        Near(0, orbit.GetSmoothing(0, 1f, 20f), "Paused or blocked input freezes recentering");
        Near(1, orbit.GetSmoothing(.5f, .01f, 20f), "Fast recentering clamps without overshooting");
        orbit.Update(-1, 0, 120, 1, .02f, .15f, .05f);
        Near(.1f, orbit.GetSmoothing(.02f, 1f, .1f), "New look input immediately restores native look smoothing");
        orbit.Update(0, 0, 120, 1, .02f, 0, .05f);
        Near(.4f, orbit.GetSmoothing(.02f, 1f, 20f), "Zero delay also uses the configured recenter speed");
        orbit.Reset();
        Near(0, orbit.Pan, "Explicit Center remains instant with a custom speed");
        Near(0, orbit.Tilt, "Explicit Center resets both axes with a custom speed");
        Near(.1f, orbit.GetSmoothing(.02f, 1f, .1f), "Center clears recenter smoothing state");

        Console.WriteLine("PASS: " + _checks + " orbit/timer/recenter-speed assertions");
    }
}
