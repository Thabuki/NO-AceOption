using System.Runtime.CompilerServices;
using Rewired;

namespace AceOption.Helpers;

internal static class AbsoluteThrottle
{
    private sealed class InputMode
    {
        public InputMode() { }
        internal bool Absolute;
    }
    private static ConditionalWeakTable<Player, InputMode> modes;

    internal static void Reset() => modes = null;

    internal static bool TryRead(Player player, out float axis)
    {
        axis = 0;
        if (player == null) return false;
        if (PlayerSettings.throttleUseRelative)
        {
            modes?.Remove(player);
            return false;
        }

        axis = player.GetAxisRaw("Throttle");
        bool analog = false, other = false;
        var sources = player.GetCurrentInputSources("Throttle");
        if (sources != null)
            foreach (var source in sources)
            {
                if (source.actionElementMap?.elementType == ControllerElementType.Axis) analog = true;
                else other = true;
            }

        if (modes == null) modes = new ConditionalWeakTable<Player, InputMode>();
        var mode = modes.GetOrCreateValue(player);
        if (analog || other)
            mode.Absolute = analog && !other &&
                player.GetAxisRawCoordinateMode("Throttle") == AxisCoordinateMode.Absolute;
        else if (axis != 0)
            mode.Absolute = false;
        // Centered/cancelled axes may have no active sources. Remember the last
        // source type so returning to zero still centers absolute throttle.
        // Buttons, mixed sources and relative axes keep native handling.
        return mode.Absolute && !player.GetButton("Axis Modifier");
    }
}
