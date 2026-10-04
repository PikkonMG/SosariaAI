using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using SosariaAI.Navigation;

namespace SosariaAI.Economy;

/// <summary>
/// Ore to ingot amounts from the game's own smelting table. Iron is difficulty 50.
/// Small piles (0x19B7) need two ore for one ingot. Large piles (0x19B9) give two.
/// </summary>
public static class SmeltRules
{
    public const int SmallOreItemId = 0x19B7;
    public const int LargeOreItemId = 0x19B9;
    public const int SmallOrePerIngot = 2;
    public const int LargeIngotsPerOre = 2;
    public const int MaxConsume = 30000;
    public const double IronDifficulty = 50.0;
    public const double DifficultyBand = 25.0;
    public const int ForgeRange = 2;
    public const int ForgeSearchRange = 16;
    public const int ClassicForgeItemId = 4017;
    public const int FireForgeIdMin = 6522;
    public const int FireForgeIdMax = 6569;
    public const int ElvenForgeItemId = 11736;
    public const int ItemIdMask = 0x3FFF;

    /// <summary>
    /// Forge spots to try, nearest first. A forge within <see cref="ForgeSearchRange"/> of
    /// one already kept is the same smithy: the forge search on arrival finds it anyway.
    /// </summary>
    public static List<Point3D> ForgeSpots(Point3D from, IReadOnlyList<Point3D> forges, int limit)
    {
        var sorted = new List<Point3D>(forges ?? []);
        sorted.Sort((a, b) => NavMetric.Chebyshev(from, a).CompareTo(NavMetric.Chebyshev(from, b)));

        var kept = new List<Point3D>();

        for (var i = 0; i < sorted.Count && kept.Count < limit; i++)
        {
            var sameSmithy = false;

            for (var k = 0; k < kept.Count; k++)
            {
                if (NavMetric.Chebyshev(kept[k], sorted[i]) <= ForgeSearchRange)
                {
                    sameSmithy = true;
                    break;
                }
            }

            if (!sameSmithy)
            {
                kept.Add(sorted[i]);
            }
        }

        return kept;
    }

    public static bool KnowsHow(double miningValue, double difficulty) =>
        difficulty <= IronDifficulty || miningValue >= difficulty;

    public static double MinSkill(double difficulty) => difficulty - DifficultyBand;

    public static double MaxSkill(double difficulty) => difficulty + DifficultyBand;

    public static int IngotsFrom(int itemId, int amount)
    {
        var take = ConsumeAmount(itemId, amount);

        if (take <= 0)
        {
            return 0;
        }

        if (itemId == SmallOreItemId)
        {
            return take / SmallOrePerIngot;
        }

        if (itemId == LargeOreItemId)
        {
            return take * LargeIngotsPerOre;
        }

        return take;
    }

    public static int ConsumeAmount(int itemId, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var take = amount > MaxConsume ? MaxConsume : amount;

        if (itemId == SmallOreItemId)
        {
            if (take < SmallOrePerIngot)
            {
                return 0;
            }

            if (take % SmallOrePerIngot != 0)
            {
                take--;
            }
        }

        return take;
    }

    public static int AfterFailure(int amount) => amount < 2 ? amount : amount / 2;

    public static bool IsForgeId(int itemId)
    {
        var id = itemId & ItemIdMask;
        return id == ClassicForgeItemId ||
               id is >= FireForgeIdMin and <= FireForgeIdMax ||
               id == ElvenForgeItemId;
    }

    public static bool IsForge(Item item) =>
        item is { Deleted: false } &&
        (item.GetType().IsDefined(typeof(ForgeAttribute), inherit: false) || IsForgeId(item.ItemID));

    /// <summary>
    /// The forge nearest <paramref name="from"/> within <see cref="ForgeSearchRange"/>, or
    /// <see cref="Point3D.Zero"/>. A forge placed as an item and a forge that is part of the
    /// map itself both count, as the engine's own smelting check counts both: the Magincia
    /// smithy's forge is map art, and a miner stood beside it finding no forge.
    /// </summary>
    public static Point3D FindNearestForge(Map map, Point3D from)
    {
        var forges = ForgesNear(map, from, ForgeSearchRange);
        var found = Point3D.Zero;
        var best = int.MaxValue;

        for (var i = 0; i < forges.Count; i++)
        {
            var distance = NavMetric.Chebyshev(from, forges[i]);

            if (distance < best)
            {
                best = distance;
                found = forges[i];
            }
        }

        return found;
    }

    /// <summary>Every forge within <paramref name="range"/> of <paramref name="from"/>: placed items and map art.</summary>
    public static List<Point3D> ForgesNear(Map map, Point3D from, int range)
    {
        if (map == null || map == Map.Internal)
        {
            return [];
        }

        var found = StaticForges.Near(map, from, range);

        foreach (var item in map.GetItemsInRange(from, range))
        {
            if (IsForge(item))
            {
                found.Add(item.Location);
            }
        }

        return found;
    }
}
