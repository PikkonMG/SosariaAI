using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Common;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// PvP houses, the way 1999 PvP guilds and red gangs kept them: a few small houses and a
/// tower on the outskirts of the hot spots, the Yew gate above all (see <see cref="HouseBases"/>).
/// A member hurt or short of supplies near its base ducks inside, heals, takes what it needs
/// from the chest and comes back out to fight on the doorstep. The engine's own placement
/// rules decide where a house may stand; these decide where to look, who owns it, and when
/// a member goes in and out. Pure.
/// </summary>
public static class HouseBaseRules
{
    /// <summary>A base stands at least this far from its spot's middle: on the outskirts, clear of the fight.</summary>
    public const int MinTiles = 18;

    /// <summary>A base stands at most this far from its spot's middle.</summary>
    public const int MaxTiles = 42;

    /// <summary>Plots are tried on rings this many tiles apart, and this far apart along a ring.</summary>
    public const int RingStep = 4;

    /// <summary>A base keeps this far from every camp, so nobody walls a camp off.</summary>
    public const int CampClearTiles = 8;

    /// <summary>Two bases keep this far apart: yards and doorsteps of their own.</summary>
    public const int Spacing = 14;

    /// <summary>
    /// Houses put up on plots the engine allows, then taken down again because their doorstep
    /// cannot walk to the spot, before a spot is given up for this boot.
    /// </summary>
    public const int MaxBuildTries = 5;

    /// <summary>A house's chest stands this close to the house's middle.</summary>
    public const int FootprintTiles = 8;

    /// <summary>A character's house this close to a spot's middle is one of that spot's bases.</summary>
    public const int SpotReachTiles = MaxTiles + RingStep;

    /// <summary>Every this-many-th base of a spot belongs to a red gang; the rest to PvP guilds.</summary>
    public const int RedHouseEvery = 3;

    /// <summary>A member this close to its base retreats there when hurt.</summary>
    public const int RetreatTiles = 40;

    /// <summary>A member under this share of its hits ducks inside.</summary>
    public const double RetreatHits = 0.5;

    /// <summary>A member at this share of its hits, and not poisoned, comes back out.</summary>
    public const double ReturnHits = 0.9;

    /// <summary>The longest a member stays inside before it comes back out anyway.</summary>
    public static readonly TimeSpan StayLimit = TimeSpan.FromMinutes(3);

    /// <summary>A member that came back out waits this long before it retreats again.</summary>
    public static readonly TimeSpan RetreatRest = TimeSpan.FromMinutes(2);

    /// <summary>The engine's small old houses: stone and plaster, fieldstone, small brick.</summary>
    public static readonly int[] SmallHouseIds = [0x64, 0x66, 0x68];

    /// <summary>The engine's small stone tower.</summary>
    public const int SmallTowerId = 0x98;

    public const int StockBandages = 100;
    public const int StockReagents = 30;
    public const int StockPotions = 10;

    /// <summary>What the owner's bank pays per item to keep the chest stocked, at the era's shop prices.</summary>
    public const int BandagePrice = 2;
    public const int ReagentPrice = 5;
    public const int PotionPrice = 30;

    /// <summary>A member takes up to this many heal potions from the chest at a time.</summary>
    public const int PotionsTaken = 3;

    /// <summary>The first base of a spot that holds more than one is a tower; the rest are small houses.</summary>
    public static int MultiFor(int slot, int housesAtSpot) =>
        slot == 0 && housesAtSpot > HotSpotRules.SpotHouses ? SmallTowerId : SmallHouseIds[Math.Abs(slot) % SmallHouseIds.Length];

    public static bool WantsRedOwner(int slot) => slot % RedHouseEvery == RedHouseEvery - 1;

    /// <summary>The plots to try round a spot's middle, nearest ring first.</summary>
    public static IEnumerable<Point3D> Plots(Point3D center)
    {
        for (var ring = MinTiles; ring <= MaxTiles; ring += RingStep)
        {
            for (var offset = -ring; offset < ring; offset += RingStep)
            {
                yield return new Point3D(center.X + offset, center.Y - ring, center.Z);
                yield return new Point3D(center.X + ring, center.Y + offset, center.Z);
                yield return new Point3D(center.X - offset, center.Y + ring, center.Z);
                yield return new Point3D(center.X - ring, center.Y - offset, center.Z);
            }
        }
    }

    /// <summary>True when no point of <paramref name="others"/> lies within <paramref name="tiles"/> of the spot.</summary>
    public static bool ClearOf(Point3D spot, IReadOnlyList<Point3D> others, int tiles)
    {
        for (var i = 0; i < (others?.Count ?? 0); i++)
        {
            if (NavMetric.Chebyshev(spot, others[i]) <= tiles)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A member out of a fight, near its base, ducks inside when badly hurt or short of the
    /// supplies it fights with.
    /// </summary>
    public static bool ShouldRetreat(double hitsFraction, bool suppliesLow, bool fighting, int tilesToBase) =>
        !fighting && tilesToBase <= RetreatTiles && (hitsFraction < RetreatHits || suppliesLow);

    /// <summary>Healed enough to fight on, or inside long enough.</summary>
    public static bool ReadyToReturn(double hitsFraction, bool poisoned, TimeSpan inside) =>
        (!poisoned && hitsFraction >= ReturnHits) || inside >= StayLimit;

    public static bool Rested(DateTime lastRetreat, DateTime now) => TimeRules.Rested(lastRetreat, now, RetreatRest);

    /// <summary>How many to add to bring a stock up to its mark.</summary>
    public static int TopUp(int have, int target) => Math.Max(0, target - Math.Max(0, have));

    /// <summary>How many a member takes: what it wants, as far as the chest holds.</summary>
    public static int Take(int want, int inChest) => Math.Max(0, Math.Min(want, inChest));
}
