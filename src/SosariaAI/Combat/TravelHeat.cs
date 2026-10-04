using System;
using Server;
using Server.Spells;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// The heat of battle as players met it: after a blow on or from a player, no public moongate,
/// recall or gate for half a minute, and a gray person takes no moongate at all. A person caught
/// in a fight runs, hides, and waits it out first. The engine before AOS asks only after blows
/// the traveler dealt (SpellHelper.CheckCombat), and the moongate hop of a character skipped the
/// engine's gate checks: 20 characters took a moongate or recalled within half a minute of a
/// fight in ten minutes, some three seconds after it, in the middle of a gank.
/// </summary>
public static class TravelHeat
{
    /// <summary>The engine's own heat of battle (SpellHelper.CombatHeatDelay).</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(30);

    /// <summary>True while a blow with a player at <paramref name="lastPlayerBlowAt"/> still holds the heat.</summary>
    public static bool InHeat(DateTime lastPlayerBlowAt, DateTime now) =>
        !TimeRules.Rested(lastPlayerBlowAt, now, Delay);

    /// <summary>How long until the heat of a blow at <paramref name="lastPlayerBlowAt"/> cools; zero once it has.</summary>
    public static TimeSpan CoolsIn(DateTime lastPlayerBlowAt, DateTime now) =>
        InHeat(lastPlayerBlowAt, now) ? Delay - (now - lastPlayerBlowAt) : TimeSpan.Zero;

    /// <summary>A public moongate takes a person out of the heat and not gray, as the engine's own gate does for players.</summary>
    public static bool MayTakeMoongate(bool criminal, bool inHeat) => !criminal && !inHeat;

    /// <summary>The last blow this person dealt to or took from a player, or default when there is none.</summary>
    public static DateTime LastPlayerBlowAt(Mobile mobile)
    {
        var last = default(DateTime);

        if (mobile == null)
        {
            return last;
        }

        foreach (var info in mobile.Aggressed)
        {
            if (info.Defender?.Player == true && info.LastCombatTime > last)
            {
                last = info.LastCombatTime;
            }
        }

        foreach (var info in mobile.Aggressors)
        {
            if (info.Attacker?.Player == true && info.LastCombatTime > last)
            {
                last = info.LastCombatTime;
            }
        }

        return last;
    }

    /// <summary>True while this living person is in the heat of battle, by this rule or the engine's.</summary>
    public static bool Hot(Mobile mobile) =>
        mobile is { Alive: true } && (InHeat(LastPlayerBlowAt(mobile), Core.Now) || SpellHelper.CheckCombat(mobile));

    /// <summary>How long until this person's heat of battle cools.</summary>
    public static TimeSpan CoolsIn(Mobile mobile) => CoolsIn(LastPlayerBlowAt(mobile), Core.Now);
}
