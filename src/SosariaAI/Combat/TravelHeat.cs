using System;
using Server;
using Server.Spells;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// The heat of battle as the engine counts it (SpellHelper.CheckCombat): after a blow this person
/// dealt to a player, no public moongate, recall or gate for half a minute, and a gray person takes
/// no moongate at all. Before AOS a blow only taken does not count, so a victim that only runs may
/// still recall or take a moongate; one that hit back stays in the heat while the fight goes on.
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

    /// <summary>
    /// The last blow that holds this person in the heat, or default when there is none: as
    /// SpellHelper.CheckCombat counts it, a blow dealt to a player, and from AOS on also a blow on itself.
    /// </summary>
    public static DateTime LastPlayerBlowAt(Mobile mobile)
    {
        var last = default(DateTime);

        if (mobile == null)
        {
            return last;
        }

        foreach (var info in mobile.Aggressed)
        {
            if (CountsBlow(dealt: true, info.Defender?.Player == true, attackerIsSelf: true, Core.AOS) &&
                info.LastCombatTime > last)
            {
                last = info.LastCombatTime;
            }
        }

        foreach (var info in mobile.Aggressors)
        {
            if (CountsBlow(dealt: false, mobile.Player, info.Attacker == mobile, Core.AOS) && info.LastCombatTime > last)
            {
                last = info.LastCombatTime;
            }
        }

        return last;
    }

    /// <summary>
    /// A blow that holds a person in the heat, as SpellHelper.CheckCombat counts it: one it dealt
    /// to a player; a blow it took never does, and from AOS on only a blow it dealt itself.
    /// </summary>
    public static bool CountsBlow(bool dealt, bool defenderIsPlayer, bool attackerIsSelf, bool aos) =>
        dealt ? defenderIsPlayer : aos && attackerIsSelf && defenderIsPlayer;

    /// <summary>True while this living person is in the engine's heat of battle.</summary>
    public static bool Hot(Mobile mobile) => mobile is { Alive: true } && SpellHelper.CheckCombat(mobile);

    /// <summary>How long until this person's heat of battle cools.</summary>
    public static TimeSpan CoolsIn(Mobile mobile) => CoolsIn(LastPlayerBlowAt(mobile), Core.Now);
}
