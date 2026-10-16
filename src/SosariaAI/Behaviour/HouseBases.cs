using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>One PvP house: the way into it and the hot spot it stands by.</summary>
public sealed record HouseBase(HouseDoorway Way, HotSpot Spot)
{
    public BaseHouse House => Way.House;
    public Point3D Front => Way.Front;
}

/// <summary>
/// The PvP houses by the hot spots (see <see cref="HouseBaseRules"/>): real engine houses a
/// PvP guild character or a red gang member owns, placed with the engine's own placement
/// rules, ageless, and kept in the world save like any house. Once a minute the houses round
/// each hot spot are read back from the world, one missing house is placed, so a boot never
/// adds a house a spot already has, and each chest is topped up from its owner's bank. The
/// owner's guild or gang are its members: hurt or short near it, they duck inside.
/// </summary>
public static class HouseBases
{
    private static readonly ILogger logger = SosariaLog.For(typeof(HouseBases));
    private static readonly List<HouseBase> Bases = [];
    private static readonly HashSet<(Map, string)> NoPlot = [];
    private static readonly Dictionary<Serial, DateTime> LastRetreat = new();

    public static void Initialize() => ActivityPulse.MinutePassed += Settle;

    /// <summary>
    /// Reads every hot spot's houses back from the world, places one missing house, tops up
    /// the chests, and forgets retreats whose rest ran out. Runs once a minute; a house already
    /// standing is never placed again.
    /// </summary>
    public static void Settle()
    {
        FactionWar.Sweep(LastRetreat, Core.Now, HouseBaseRules.RetreatRest);
        Bases.Clear();
        var placed = false;

        foreach (var map in Map.AllMaps)
        {
            var spots = HotSpots.For(map);

            if (spots.Count == 0)
            {
                continue;
            }

            ReadBack(map, spots);
            placed = placed || PlaceOne(map, spots);
        }

        for (var i = 0; i < Bases.Count; i++)
        {
            TopUp(Bases[i]);
        }
    }

    /// <summary>
    /// A member near its base, out of a fight and badly hurt or short of supplies, ducks inside
    /// to heal and restock (see <see cref="HouseRetreatSkill"/>).
    /// </summary>
    public static void ConsiderRetreat(SosariaCharacter character)
    {
        if (Bases.Count == 0 || character.IsGhost || character.Routine?.CurrentSkill is HouseRetreatSkill ||
            GameParty.InParty(character) || SosariaCharacter.UnderGuards(character) ||
            !HouseBaseRules.Rested(LastRetreat.GetValueOrDefault(character.Serial), Core.Now))
        {
            return;
        }

        if (MemberBase(character) is not { } home)
        {
            return;
        }

        // The supply count reads the whole pack, so it is taken only for a member not already
        // hurt, and a chest with nothing in it is no reason to go in.
        var hits = Vitals.HitsFraction(character);
        var low = hits >= HouseBaseRules.RetreatHits && SupplyCheck.IsLow(character) &&
                  home.Way.Chest()?.TotalItems > 0;
        var fighting = character.Combatant is { Deleted: false, Alive: true };

        if (!HouseBaseRules.ShouldRetreat(hits, low, fighting, NavMetric.Chebyshev(character.Location, home.Front)))
        {
            return;
        }

        LastRetreat[character.Serial] = Core.Now;
        WorldPlay.StartWork(character, new HouseRetreatSkill(home));
        Talk.Say(character, TalkCategory.HouseRetreat);
        WorldPlay.Log($"{character.Name} ducks into {OwnerName(home)}'s house by {home.Spot.Name} to heal");
    }

    /// <summary>
    /// The doorstep of the base a member flees toward: its own base within reach, else null.
    /// A runner leaning that way reaches the door, and the retreat takes it inside.
    /// </summary>
    public static Point3D? SafeDoor(SosariaCharacter character) => MemberBase(character)?.Front;

    public static string OwnerName(HouseBase home) => home.House.Owner?.Name ?? home.Spot.Name;

    // The owner, its guild and its gang use the house.
    private static bool IsMember(SosariaCharacter character, Mobile owner) =>
        owner == character ||
        (owner?.Guild != null && character.Guild == owner.Guild) ||
        (owner is SosariaCharacter { IsPk: true } red && character.IsPk && red.OutlawGang != PkGangRules.NoGang &&
         red.OutlawGang == character.OutlawGang);

    // The nearest standing base this character belongs to, within retreat reach.
    private static HouseBase MemberBase(SosariaCharacter character)
    {
        HouseBase best = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < Bases.Count; i++)
        {
            var home = Bases[i];

            if (home.House.Deleted || home.House.Map != character.Map || !IsMember(character, home.House.Owner))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(character.Location, home.Front);

            if (distance <= HouseBaseRules.RetreatTiles && distance < bestDistance)
            {
                best = home;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Every house a character owns by one of the map's hot spots is that spot's base.
    private static void ReadBack(Map map, IReadOnlyList<HotSpot> spots)
    {
        for (var i = 0; i < BaseHouse.AllHouses.Count; i++)
        {
            var house = BaseHouse.AllHouses[i];

            if (house is not { Deleted: false } || house.Map != map || house.Owner is not SosariaCharacter ||
                SpotOf(house.Location, spots) is not { } spot || Base(house, spot) is not { } found)
            {
                continue;
            }

            Bases.Add(found);
        }
    }

    private static HotSpot SpotOf(Point3D at, IReadOnlyList<HotSpot> spots)
    {
        for (var i = 0; i < spots.Count; i++)
        {
            if (NavMetric.Chebyshev(at, spots[i].Center) <= HouseBaseRules.SpotReachTiles)
            {
                return spots[i];
            }
        }

        return null;
    }

    // A house without a door is no base.
    private static HouseBase Base(BaseHouse house, HotSpot spot) =>
        HouseDoorway.Of(house) is { } way ? new HouseBase(way, spot) : null;

    /// <summary>Places the first missing house of the first spot short of its number. True when a house went up.</summary>
    private static bool PlaceOne(Map map, IReadOnlyList<HotSpot> spots)
    {
        for (var i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            var slot = CountAt(spot);

            if (slot >= spot.Houses || NoPlot.Contains((map, spot.Name)) ||
                Owner(map, spot, HouseBaseRules.WantsRedOwner(slot)) is not { } owner)
            {
                continue;
            }

            if (Build(owner, map, spot, HouseBaseRules.MultiFor(slot, spot.Houses)) is { } built)
            {
                Bases.Add(built);
                logger.Information(
                    "{Owner} placed a PvP house at {Location} by {Spot} ({Count} of {Wanted})",
                    owner.Name,
                    built.House.Location,
                    spot.Name,
                    slot + 1,
                    spot.Houses
                );
                return true;
            }

            NoPlot.Add((map, spot.Name));
            logger.Warning("No house plot passes the placement rules by {Spot} on {Map}", spot.Name, map.Name);
        }

        return false;
    }

    private static int CountAt(HotSpot spot)
    {
        var count = 0;

        for (var i = 0; i < Bases.Count; i++)
        {
            count += ReferenceEquals(Bases[i].Spot, spot) ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// The owner of a new base: a character on the map with no house yet, from a red gang or an
    /// Order or Chaos guild as the slot wants, else the other kind; the one living nearest the
    /// spot. Null when nobody fits.
    /// </summary>
    private static SosariaCharacter Owner(Map map, HotSpot spot, bool wantsRed)
    {
        SosariaCharacter wanted = null;
        SosariaCharacter other = null;
        var wantedDistance = int.MaxValue;
        var otherDistance = int.MaxValue;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not SosariaCharacter { Deleted: false, Alive: true } character || character.Map != map ||
                character.HouseSerial > HouseRules.NoHouseSerial || BaseHouse.HasHouse(character))
            {
                continue;
            }

            var red = character.IsPk && character.OutlawGang != PkGangRules.NoGang;
            var guild = !character.IsPk && EngineGuilds.AlignmentOf(character) != GuildType.Regular;

            if (!red && !guild)
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(character.HomeSpot, spot.Center);

            if (red == wantsRed && distance < wantedDistance)
            {
                wanted = character;
                wantedDistance = distance;
            }
            else if (red != wantsRed && distance < otherDistance)
            {
                other = character;
                otherDistance = distance;
            }
        }

        return wanted ?? other;
    }

    // The first plot on the spot's outskirts the engine lets the owner build on, clear of the
    // camps and the other bases, whose doorstep can walk to the spot.
    private static HouseBase Build(SosariaCharacter owner, Map map, HotSpot spot, int multiId)
    {
        var others = new List<Point3D>();

        for (var i = 0; i < Bases.Count; i++)
        {
            if (Bases[i].House.Map == map)
            {
                others.Add(Bases[i].House.Location);
            }
        }

        var walker = Standable.Walker(map);
        var builds = 0;

        foreach (var plot in HouseBaseRules.Plots(spot.Center))
        {
            if (builds >= HouseBaseRules.MaxBuildTries)
            {
                break;
            }

            if (!HouseBaseRules.ClearOf(plot, spot.Camps, HouseBaseRules.CampClearTiles) ||
                !HouseBaseRules.ClearOf(plot, others, HouseBaseRules.Spacing))
            {
                continue;
            }

            var center = new Point3D(plot.X, plot.Y, map.GetAverageZ(plot.X, plot.Y));

            if (GuardCall.IsGuardedPlace(center, map) ||
                HousePlacement.Check(owner, multiId, center, out var toMove, Direction.South) != HousePlacementResult.Valid)
            {
                continue;
            }

            builds++;
            var house = multiId == HouseBaseRules.SmallTowerId ? (BaseHouse)new SmallTower(owner) : new SmallOldHouse(owner, multiId);
            house.MoveToWorld(center, map);

            if (Base(house, spot) is not { } built ||
                TileRoute.Find(built.Front, spot.Camps[0], walker, (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z)).Count == 0)
            {
                house.RemoveKeys(owner);
                house.Delete();
                continue;
            }

            ClearYard(house, toMove);
            house.RestrictDecay = true;

            if (house.Sign != null)
            {
                house.Sign.Name = owner.Guild?.Name ?? $"{owner.Name}'s house";
            }

            return built;
        }

        return null;
    }

    // What stood where the house went up goes to its doorstep, as the engine's own placement does.
    private static void ClearYard(BaseHouse house, List<IEntity> toMove)
    {
        for (var i = 0; i < toMove.Count; i++)
        {
            switch (toMove[i])
            {
                case Mobile mobile:
                    mobile.Location = house.BanLocation;
                    break;
                case Item item:
                    item.Location = house.BanLocation;
                    break;
            }
        }
    }

    // The chest is found, or set down and locked down, then filled to its marks from the owner's bank.
    private static void TopUp(HouseBase home)
    {
        if (home.House.Owner is not SosariaCharacter { Deleted: false } owner)
        {
            return;
        }

        var chest = home.Way.ChestOrNew(owner);
        Fill(chest, owner, typeof(Bandage), HouseBaseRules.StockBandages, HouseBaseRules.BandagePrice);

        for (var i = 0; i < SupplyCheck.ReagentTypes.Length; i++)
        {
            Fill(chest, owner, SupplyCheck.ReagentTypes[i], HouseBaseRules.StockReagents, HouseBaseRules.ReagentPrice);
        }

        var potions = HouseChest.TopUp(chest.GetAmount(typeof(GreaterHealPotion)), HouseBaseRules.StockPotions);

        if (potions > 0 && Banker.Withdraw(owner, potions * HouseBaseRules.PotionPrice))
        {
            for (var i = 0; i < potions; i++)
            {
                chest.DropItem(new GreaterHealPotion());
            }
        }
    }

    private static void Fill(Container chest, SosariaCharacter owner, Type type, int target, int price)
    {
        var add = HouseChest.TopUp(chest.GetAmount(type), target);

        if (add > 0 && Banker.Withdraw(owner, add * price))
        {
            chest.DropItem(HouseChest.Stack(type, add));
        }
    }
}
