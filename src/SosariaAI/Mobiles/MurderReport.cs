using System.Collections.Generic;
using Server;
using Server.Engines.PlayerMurderSystem;
using Server.Mobiles;

namespace SosariaAI.Mobiles;

/// <summary>
/// The engine's death event marks every reportable attacker as reported before the victim
/// sees the gump. A character reads the same list first, in <c>OnBeforeDeath</c>, so it can
/// file the reports its missing client would have filed.
/// </summary>
public static class MurderReport
{
    /// <summary>
    /// Who killed the dead. The engine names a killer only when a blow takes the hits below
    /// zero; a guard's strike takes them to zero and then kills outright, so 89 of 366 deaths
    /// named nobody. The last one to deal damage is the killer then.
    /// </summary>
    public static T KillerOf<T>(T lastKiller, T lastDamager) where T : class => lastKiller ?? lastDamager;

    public static List<Mobile> Collect(PlayerMobile victim)
    {
        // Guards take no reports of a thief's death.
        if (victim == null || victim.NpcGuild == NpcGuild.ThievesGuild)
        {
            return null;
        }

        List<Mobile> killers = null;

        foreach (var info in victim.Aggressors)
        {
            var attacker = info.Attacker;

            if (attacker is PlayerMobile && attacker != victim && info.CanReportMurder && !info.Reported &&
                !PlayerMurderSystem.IsRecentlyReported(victim, attacker))
            {
                killers ??= [];
                killers.Add(attacker);
            }
        }

        return killers;
    }
}
