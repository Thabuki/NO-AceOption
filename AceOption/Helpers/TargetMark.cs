using Rewired;

namespace AceOption.Helpers;

internal static class TargetMark
{
    private static Player input;
    private static bool held, pending, consumed, bulkReady;

    internal static void Reset()
    {
        input = null;
        held = pending = consumed = bulkReady = false;
    }

    private static void Observe(Player player, string action)
    {
        if (input != player) { Reset(); input = player; }
        // Both the camera and HUD may observe the press in either LateUpdate order.
        if (!held && player.GetButtonDown(action)) { pending = true; consumed = bulkReady = false; }
        held = player.GetButton(action);
    }

    internal static void FocusActivated()
    {
        if (!CameraCommands.InputAllowed) return;
        var player = GameManager.playerInput;
        Observe(player, "Select");
        if (pending && held) consumed = true;
    }

    public static bool Released(Player player, string action, float minimum, float maximum)
    {
        if (DynamicMap.mapMaximized)
        {
            Reset();
            return player.GetButtonTimedPressUp(action, minimum, maximum);
        }
        if (!CameraCommands.InputAllowed) { Reset(); return false; }
        Observe(player, action);
        if (!player.GetButtonUp(action))
        {
            if (!held) Reset();
            return false;
        }
        // Keep a long, unused press for the native bulk-selection branch below.
        if (pending && !consumed && bulkReady) return false;
        bool mark = pending && !consumed;
        Reset();
        return mark;
    }

    public static bool Held(Player player, string action, float duration)
    {
        if (DynamicMap.mapMaximized) return player.GetButtonTimedPressDown(action, duration);
        if (!CameraCommands.InputAllowed) { Reset(); return false; }
        Observe(player, action);
        if (held)
        {
            // Use the game's hold threshold, but defer bulk marking until release.
            bulkReady |= pending && player.GetButtonTimedPressDown(action, duration);
            return false;
        }
        bool mark = player.GetButtonUp(action) && pending && !consumed && bulkReady;
        Reset();
        return mark;
    }
}
