using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Finds a thief's mark in the crowd, remembers the marks each thief tried, reads what left a
/// mark's pack, and frees the thief's hands. Shared by stealing and snooping.
/// </summary>
public static class StealMarks
{
    /// <summary>Once this many tries are held, the ones whose rest ran out are dropped.</summary>
    public const int SweepAt = 512;

    private static readonly Dictionary<(Serial Thief, Serial Mark), DateTime> Tried = new();

    /// <summary>
    /// The best mark in reach: a living person, not staff, not young, not hidden or fighting, not
    /// a red, not a fellow thief, not of the thief's own guild or party, not tried lately, not
    /// standing by a town NPC unless the thief is bold, and carrying something worth lifting.
    /// A real player comes before a character. <paramref name="loot"/> is the item.
    /// </summary>
    public static Mobile Find(SosariaCharacter thief, out Item loot)
    {
        loot = null;
        Mobile best = null;
        var bestScore = 0.0;

        if (!People.InWorld(thief))
        {
            return null;
        }

        var now = Core.Now;

        foreach (var mobile in thief.Map.GetMobilesInRange(thief.Location, StealRules.MarkRange))
        {
            if (!IsMark(thief, mobile) || !StealRules.MarkRested(Tried.GetValueOrDefault((thief.Serial, mobile.Serial)), now) ||
                Hot(mobile) && !StealRules.TakesHotMark(thief.Skills.Stealing.Value, Utility.Random(PercentRoll.Scale)))
            {
                continue;
            }

            var item = StealRules.PickLoot(mobile.Backpack, Stealing.MaxWeightToSteal);

            if (item == null || !StealRules.HasRoomBeside(FreeTilesBeside(mobile)) || !thief.InLOS(mobile))
            {
                continue;
            }

            var score = StealRules.MarkScore(
                StealRules.Worth(StealRules.KindOf(item), item.Amount),
                NavMetric.Chebyshev(thief.Location, mobile.Location),
                mobile is not SosariaCharacter
            );

            if (score > bestScore)
            {
                bestScore = score;
                best = mobile;
                loot = item;
            }
        }

        return best;
    }

    /// <summary>Tiles next to the mark a person can stand on, no one standing there.</summary>
    public static int FreeTilesBeside(Mobile mark)
    {
        var map = mark.Map;
        var neighbours = HarvestStand.Neighbours(mark.Location);
        var free = 0;

        for (var i = 0; i < neighbours.Count; i++)
        {
            var tile = neighbours[i];
            var z = map.GetAverageZ(tile.X, tile.Y);

            if (map.CanFit(tile.X, tile.Y, z, PersonBody.Height, false, true))
            {
                free++;
            }
        }

        return free;
    }

    /// <summary>People besides the thief close enough to make a crowd to work.</summary>
    public static int CrowdAround(SosariaCharacter thief)
    {
        var people = 0;

        if (!People.InWorld(thief))
        {
            return people;
        }

        foreach (var mobile in thief.Map.GetMobilesInRange(thief.Location, StealRules.MarkRange))
        {
            if (mobile != thief && People.IsLivingPlayer(mobile) && thief.CanSee(mobile))
            {
                people++;
            }
        }

        return people;
    }

    public static bool IsMark(SosariaCharacter thief, Mobile mobile) =>
        mobile is PlayerMobile { Deleted: false, Alive: true, Young: false } person &&
        person != thief && People.Perceives(thief, person) &&
        person.AccessLevel == AccessLevel.Player &&
        person.Backpack != null &&
        person.Combatant == null &&
        !person.Murderer &&
        !(person is SosariaCharacter other && ThiefWork.IsThief(other)) &&
        (thief.Guild == null || person.Guild != thief.Guild) &&
        (GameParty.Of(thief) is not { } party || !party.Contains(person));

    /// <summary>This thief tried this mark now: it leaves the mark alone for the rest.</summary>
    public static void NoteTried(Mobile thief, Mobile mark, DateTime now)
    {
        if (thief == null || mark == null)
        {
            return;
        }

        if (Tried.Count >= SweepAt)
        {
            List<(Serial, Serial)> rested = null;

            foreach (var (pair, at) in Tried)
            {
                if (StealRules.MarkRested(at, now))
                {
                    (rested ??= []).Add(pair);
                }
            }

            for (var i = 0; rested != null && i < rested.Count; i++)
            {
                Tried.Remove(rested[i]);
            }
        }

        Tried[(thief.Serial, mark.Serial)] = now;
    }

    /// <summary>What lies on top of a pack now, and how many of each.</summary>
    public static List<(Item Item, int Amount)> Tally(Container pack)
    {
        var tally = new List<(Item, int)>();

        for (var i = 0; pack != null && i < pack.Items.Count; i++)
        {
            tally.Add((pack.Items[i], pack.Items[i].Amount));
        }

        return tally;
    }

    /// <summary>
    /// The first thing that left the mark since the tally, and how many: a whole item gone, or
    /// a stack that shrank. Null when nothing did. A lifted stack merges into the thief's own,
    /// so the mark's pack is the truth.
    /// </summary>
    public static Item Lifted(IReadOnlyList<(Item Item, int Amount)> before, Mobile mark, out int amount)
    {
        for (var i = 0; i < before.Count; i++)
        {
            var (item, had) = before[i];

            if (item.Deleted || item.RootParent != mark)
            {
                amount = had;
                return item;
            }

            if (item.Amount < had)
            {
                amount = had - item.Amount;
                return item;
            }
        }

        amount = 0;
        return null;
    }

    /// <summary>The engine steals only with empty hands. The weapon goes to the pack until the next fight.</summary>
    public static void FreeHands(Mobile thief)
    {
        if (thief.FindItemOnLayer(Layer.OneHanded) is { } oneHanded)
        {
            thief.AddToBackpack(oneHanded);
        }

        if (thief.FindItemOnLayer(Layer.TwoHanded) is { } twoHanded)
        {
            thief.AddToBackpack(twoHanded);
        }
    }

    // A town NPC stands close enough to the mark to yell for the guards when the thief flags.
    private static bool Hot(Mobile mark)
    {
        if (!SosariaCharacter.UnderGuards(mark))
        {
            return false;
        }

        foreach (var mobile in mark.Map.GetMobilesInRange(mark.Location, StealRules.NpcHeatRange))
        {
            if (mobile is BaseCreature { Alive: true } creature && creature.IsHumanInTown())
            {
                return true;
            }
        }

        return false;
    }
}
