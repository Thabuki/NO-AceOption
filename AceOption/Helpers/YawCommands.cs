using System.Runtime.CompilerServices;
using Rewired;
using UnityEngine;

namespace AceOption.Helpers;

internal static class YawCommands
{
    private static ConditionalWeakTable<Player, ButtonYaw> states;

    internal static void Reset() => states = null;
    internal static void Reset(Player player)
    {
        if (player != null) states?.Remove(player);
    }

    // Replaces only the native Yaw GetAxis call, before summation, clamping and
    // Aircraft.FilterInputs. Native blackout/detachment branches remain intact.
    public static float Read(Player player, string action)
    {
        float native = player.GetAxis(action);
        if (!Plugin.SmoothButtonYaw.Value)
        {
            Reset(player);
            return native;
        }
        if (!CameraCommands.InputAllowed)
        {
            Reset(player);
            return 0;
        }

        bool buttons = false, analog = false;
        var sources = player.GetCurrentInputSources(action);
        if (sources != null)
            foreach (var source in sources)
            {
                var map = source.actionElementMap;
                if (map != null && map.elementType == ControllerElementType.Button) buttons = true;
                else analog = true;
            }
        if (states == null) states = new ConditionalWeakTable<Player, ButtonYaw>();
        // Raw bypasses Rewired's button-axis slew, avoiding two smoothing stages.
        return states.GetOrCreateValue(player).Update(native, player.GetAxisRaw(action),
            buttons, analog, Time.fixedDeltaTime, Plugin.YawRiseTime.Value, Plugin.YawReturnTime.Value);
    }
}
