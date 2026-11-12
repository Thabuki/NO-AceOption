using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace AceOption.Helpers;

internal static class GroundContact
{
    private sealed class GearSet
    {
        internal LandingGear[] Gears;
        internal List<UnitPart> Parts;
        internal int PartCount;
    }

    private static readonly ConditionalWeakTable<Aircraft, GearSet> gears =
        new ConditionalWeakTable<Aircraft, GearSet>();
    private static readonly AccessTools.FieldRef<LandingGear, float> compression =
        AccessTools.FieldRefAccess<LandingGear, float>("compressionDistance");
    private static readonly AccessTools.FieldRef<LandingGear, AeroPart> attachedPart =
        AccessTools.FieldRefAccess<LandingGear, AeroPart>("attachedPart");

    internal static bool OnGround(Aircraft aircraft)
    {
        if (aircraft == null) return false;
        // IsLanded() also requires speed < 2.5 m/s. Native suspension contact
        // works at rollout speeds and on moving decks, without an altitude guess.
        var set = gears.GetValue(aircraft, _ => new GearSet());
        var parts = aircraft.partLookup;
        int count = parts?.Count ?? 0;
        if (set.Gears == null || set.Parts != parts || set.PartCount != count)
        {
            var found = new HashSet<LandingGear>(aircraft.GetComponentsInChildren<LandingGear>(true));
            // AeroPart.CreateRB unparents physical parts without marking them
            // detached. The native registry still owns them and their wheels.
            if (parts != null)
                foreach (var part in parts)
                    if (part != null)
                        foreach (var gear in part.GetComponentsInChildren<LandingGear>(true))
                            found.Add(gear);
            set.Gears = new LandingGear[found.Count];
            found.CopyTo(set.Gears);
            set.Parts = parts;
            // Retry discovery if the first poll precedes part registration.
            set.PartCount = count;
        }
        foreach (var gear in set.Gears)
        {
            if (gear == null || !gear.isActiveAndEnabled) continue;
            var part = attachedPart(gear);
            if (part != null && !part.IsDetached() && compression(gear) > 0) return true;
        }
        return false;
    }
}
