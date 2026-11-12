using System;
using AceOption.Helpers;

namespace Rewired
{
    public class Player
    {
        public bool Down, Held, Up, TimedUp, TimedDown;
        public float Minimum, Maximum, Duration;
        public bool GetButtonDown(string action) => Down;
        public bool GetButton(string action) => Held;
        public bool GetButtonUp(string action) => Up;
        public bool GetButtonTimedPressUp(string action, float minimum, float maximum)
        { Minimum = minimum; Maximum = maximum; return TimedUp; }
        public bool GetButtonTimedPressDown(string action, float duration)
        { Duration = duration; return TimedDown; }
    }
}
public static class GameManager { public static Rewired.Player playerInput; }
public static class DynamicMap { public static bool mapMaximized; }
namespace AceOption.Helpers
{
    internal static class CameraCommands { internal static bool InputAllowed = true; }
}

internal static class TargetMarkTests
{
    private static int checks;
    private static bool bulk;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static bool Read()
    {
        // Follow CombatHUD's single-select / else-if bulk-select branches.
        bulk = false;
        var player = GameManager.playerInput;
        if (TargetMark.Released(player, "Select", 0, .25f)) return true;
        bulk = TargetMark.Held(player, "Select", .5f) && !DynamicMap.mapMaximized;
        return bulk;
    }
    private static void Frame(bool down = false, bool held = false, bool up = false)
    {
        var player = GameManager.playerInput;
        player.Down = down; player.Held = held; player.Up = up;
        player.TimedDown = false;
    }
    private static void Press()
    {
        Frame(down: true, held: true);
        Check(!Read(), "Press must not mark a target");
    }
    private static void Release(bool expected, string message, bool bulkExpected = false)
    {
        Frame(up: true);
        Check(Read() == expected, message);
        if (expected) Check(bulk == bulkExpected, "Release must choose the correct single/bulk marking mode");
        Check(!Read(), "Release must not mark twice");
    }

    public static void Main()
    {
        GameManager.playerInput = new Rewired.Player();
        TargetMark.Reset();
        Press();
        Release(true, "A quick tap must mark on release");
        Press();
        Frame(held: true);
        GameManager.playerInput.TimedDown = true;
        Check(!TargetMark.Held(GameManager.playerInput, "Select", .5f), "A long hold must not mark before release");
        for (int i = 0; i < 10; i++) Check(!Read(), "Holding must not mark a target");
        // TimedUp is false: an unused long hold must retain bulk marking beyond the native click limit.
        Release(true, "A long hold with focus unbound must bulk-mark on release", bulkExpected: true);

        Press();
        Frame(held: true);
        Check(!Read(), "Holding below the native bulk threshold must not mark");
        Release(true, "A hold below the bulk threshold must still single-mark on release");

        Press();
        Frame(held: true);
        GameManager.playerInput.TimedDown = true;
        Check(!Read(), "Crossing the bulk threshold must not mark before release");
        Frame(held: true);
        Check(!Read(), "Bulk readiness must survive past the one-frame timed-press event");
        Release(true, "A hold that never activated look-at must retain bulk readiness until release", bulkExpected: true);

        // HUD observes the shared press before the camera activates focus.
        Press();
        Frame(held: true);
        GameManager.playerInput.TimedDown = true;
        Check(!Read(), "A shared hold may become bulk-ready before focus activates");
        TargetMark.FocusActivated();
        Check(!Read(), "Activating focus must not mark during the hold");
        Frame(held: true);
        Check(!Read(), "Canceling focus or losing its target must not forget that the press was consumed");
        Release(false, "A press that activated focus must not mark on release");
        Press();
        Release(true, "The next tap after focus must mark normally");

        // A hold can cross the native bulk threshold after focus already consumed it.
        Press();
        Frame(held: true);
        TargetMark.FocusActivated();
        GameManager.playerInput.TimedDown = true;
        Check(!Read(), "Crossing the bulk threshold after focus must not mark");
        Release(false, "Focus must suppress both single and bulk marking on release");

        // Camera observes/consumes first, including a long frame that crosses the focus threshold immediately.
        Frame(down: true, held: true);
        TargetMark.FocusActivated();
        Check(!Read(), "HUD observation must not undo camera consumption in the same frame");
        Release(false, "Camera-first activation must suppress release marking");

        Frame();
        TargetMark.FocusActivated();
        Press();
        Release(true, "An independently bound focus button must not consume a later target-mark press");

        Press();
        CameraCommands.InputAllowed = false;
        Frame(held: true);
        Check(!Read(), "Menus must cancel a pending mark");
        CameraCommands.InputAllowed = true;
        Release(false, "Resuming after a menu must not mark from the interrupted press");
        Frame(held: true);
        Check(!Read(), "An already held button must not arm marking without a press");
        Release(false, "A release with no observed press must not mark");

        Press();
        GameManager.playerInput = new Rewired.Player { Up = true };
        Check(!Read(), "Changing the input player must discard the previous player's gesture");
        Press();
        TargetMark.Reset();
        Release(false, "Reset must discard pending marking");

        Press();
        DynamicMap.mapMaximized = true;
        GameManager.playerInput.TimedUp = true;
        Check(Read(), "Map selection must retain the native timed-release result");
        Check(GameManager.playerInput.Minimum == 0 && GameManager.playerInput.Maximum == .25f,
            "Map selection must retain the native click limits");
        GameManager.playerInput.TimedDown = true;
        Check(TargetMark.Held(GameManager.playerInput, "Select", .5f), "Map must retain native held selection");
        Check(GameManager.playerInput.Duration == .5f, "Map must retain the native hold threshold");
        DynamicMap.mapMaximized = false;
        Release(false, "Returning from the map must not mark from the previous press");
        Press();
        Release(true, "A fresh press after the map must mark normally");

        Console.WriteLine("PASS: " + checks + " target mark assertions");
    }
}
