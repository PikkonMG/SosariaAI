using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// Calls nearby Sosaria people onto the same foe, the nearest first, up to
/// <see cref="AssistRules.MaxHelpers"/> on it. No world types in the rules.
/// </summary>
public static class Assist
{
    private static readonly List<SosariaCharacter> _willing = [];

    public static int Rally(SosariaCharacter ally, Mobile aggressor)
    {
        // An agreed duel is two people's business: nobody piles in.
        if (!People.InWorld(ally) || aggressor == null || aggressor.Deleted ||
            Duels.AreFighting(ally, aggressor))
        {
            return 0;
        }

        var onFoe = 0;
        _willing.Clear();

        foreach (var mobile in ally.Map.GetMobilesInRange(ally.Location, AssistRules.HelpRange))
        {
            if (mobile is not SosariaCharacter helper || helper == ally || helper.Deleted || helper.IsGhost)
            {
                continue;
            }

            // Everyone already on the foe counts toward the cap, near or far, running or not.
            if (helper.Combatant == aggressor)
            {
                onFoe++;
                continue;
            }

            if (WorldPlay.LeavesBeSide(helper, aggressor))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(ally.Location, helper.Location);
            // A runner is busy getting away: handing it a fight while it runs is how it dies.
            var busy = helper.Motor.Action == CharacterAction.Flee || helper.Combatant != null;

            // The weapon comes out only for a yes: asking took the tools out of workers' hands.
            if (!AssistRules.ShouldHelp(
                    helper.Alive,
                    ally.Alive,
                    helper.Map == ally.Map,
                    distance,
                    GearEquip.CanArm(helper),
                    helper.IsEnemy(aggressor),
                    helper.IsGhost,
                    busy,
                    SosariaCharacter.UnderGuards(helper) || SosariaCharacter.UnderGuards(aggressor)
                ))
            {
                continue;
            }

            _willing.Add(helper);
        }

        _willing.Sort((first, second) =>
            NavMetric.Chebyshev(ally.Location, first.Location).CompareTo(NavMetric.Chebyshev(ally.Location, second.Location))
        );
        var called = AssistRules.HelpersToCall(onFoe, _willing.Count);

        // JoinAgainst draws the weapon.
        for (var i = 0; i < called; i++)
        {
            _willing[i].JoinAgainst(aggressor);
        }

        _willing.Clear();
        return onFoe + called;
    }
}
