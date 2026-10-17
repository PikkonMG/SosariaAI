using System.Collections.Generic;
using Server;
using EngineParty = Server.Engines.PartySystem.Party;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Combat;

/// <summary>
/// The burst calls of each side, by target: a side is the caster's party, else its red gang. A
/// caster that opens a burst on a target calls the tick its spell lands at; a mate of that side
/// times its own cast to land at the same tick (<see cref="GroupBurstRules"/>). Calls are struck
/// off shortly after they land, so the table holds only the fights going on. World thread only.
/// </summary>
public static class GroupBurst
{
    private static readonly Dictionary<(EngineParty Party, int Gang, Serial Target), long> Calls = new();
    private static readonly List<(EngineParty Party, int Gang, Serial Target)> Expired = [];

    /// <summary>
    /// True when the caster's side has an open burst call on <paramref name="target"/>: its
    /// landing tick, <paramref name="landAtTick"/>, is still to come.
    /// </summary>
    public static bool TryCall(SosariaCharacter caster, Mobile target, out long landAtTick)
    {
        landAtTick = 0;

        if (!TryKey(caster, target, out var key) || !Calls.TryGetValue(key, out var tick) ||
            !GroupBurstRules.IsOpen(tick, Core.TickCount))
        {
            return false;
        }

        landAtTick = tick;
        return true;
    }

    /// <summary>
    /// Opens a burst call for the caster's side on <paramref name="target"/> landing at
    /// <paramref name="landAtTick"/>, or keeps the earliest open one (<see cref="GroupBurstRules.Keep"/>).
    /// A caster with no party and no gang calls nothing.
    /// </summary>
    public static void Join(SosariaCharacter caster, Mobile target, long landAtTick)
    {
        if (!TryKey(caster, target, out var key))
        {
            return;
        }

        var now = Core.TickCount;
        Prune(now);
        var open = Calls.TryGetValue(key, out var tick) && GroupBurstRules.IsOpen(tick, now);
        Calls[key] = GroupBurstRules.Keep(open, tick, landAtTick);
    }

    /// <summary>The side of a caster on a target: its party, else its gang. False for a caster of neither.</summary>
    private static bool TryKey(SosariaCharacter caster, Mobile target, out (EngineParty Party, int Gang, Serial Target) key)
    {
        key = default;

        if (caster == null || target == null)
        {
            return false;
        }

        var party = GameParty.Of(caster);

        if (party != null)
        {
            key = (party, PkGangRules.NoGang, target.Serial);
            return true;
        }

        if (caster.OutlawGang == PkGangRules.NoGang)
        {
            return false;
        }

        key = (null, caster.OutlawGang, target.Serial);
        return true;
    }

    private static void Prune(long now)
    {
        Expired.Clear();

        foreach (var (key, tick) in Calls)
        {
            if (GroupBurstRules.IsExpired(tick, now))
            {
                Expired.Add(key);
            }
        }

        foreach (var key in Expired)
        {
            Calls.Remove(key);
        }
    }
}
