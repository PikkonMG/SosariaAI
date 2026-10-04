using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The pack llama or pack horse a miner or woodcutter leads: bought once from the animal
/// trainer, following on foot, carrying a second load. A full pack is emptied into the
/// beast's pack at the patch, and at the shop the load comes back into the pack a pack at a
/// time. Every person stays in the world, so the beast stays out with its owner: a gatherer
/// has no pet to rest and no slots to free, the only reasons to stable
/// (<see cref="Behaviour.StableRules.ReasonToStable"/>). A beast left in the stables is claimed back
/// when its owner stands idle (<see cref="Behaviour.PetKeeper"/>). World thread only.
/// </summary>
public static class PackAnimals
{
    /// <summary>Tiles within which the owner reaches into the beast's pack.</summary>
    public const int ReachTiles = 2;

    /// <summary>Follower slots a pack beast takes.</summary>
    public const int BeastSlots = 1;

    /// <summary>The animal trainer's prices (SBAnimalTrainer). A buyer short of them never walks there.</summary>
    public const int LlamaPrice = 565;

    public const int HorsePrice = 631;

    /// <summary>
    /// The owner takes the load back until it carries this share of its body's limit, or its
    /// pack is this full. A pack filled to its own 400 stones overloads any body, and the
    /// seller stood at the counter unable to walk to the next shop.
    /// </summary>
    public const double UnloadFillFraction = 0.9;

    private const double MinUnitWeight = 0.1;

    private static readonly ILogger logger = SosariaLog.For(typeof(PackAnimals));

    /// <summary>A miner or a woodcutter: the trades whose haul is too heavy for one pack.</summary>
    public static bool IsGatherer(SosariaCharacter worker) =>
        Works(worker, SkillKinds.Mine) || Works(worker, SkillKinds.Lumberjack);

    public static bool IsPackBeast(Type type) => type == typeof(PackLlama) || type == typeof(PackHorse);

    /// <summary>A miner leads a llama, a woodcutter a horse.</summary>
    public static Type BeastFor(SosariaCharacter worker) =>
        Works(worker, SkillKinds.Mine) ? typeof(PackLlama) : typeof(PackHorse);

    public static int PriceOf(Type beast) => beast == typeof(PackLlama) ? LlamaPrice : HorsePrice;

    /// <summary>
    /// A gatherer with no pack beast of its own, out or stabled, a free follower slot, and
    /// the price in its pack.
    /// </summary>
    public static bool WantsBeast(SosariaCharacter worker) =>
        worker != null && IsGatherer(worker) && !OwnsBeast(worker) &&
        worker.Followers + BeastSlots <= worker.FollowersMax &&
        PackFunds.FundPack(worker, PriceOf(BeastFor(worker)));

    /// <summary>The owner's pack beast following it on this map, or null.</summary>
    public static BaseCreature BeastOf(SosariaCharacter owner)
    {
        foreach (var follower in owner?.AllFollowers ?? [])
        {
            if (IsOwnedBeast(owner, follower) && follower.Map == owner.Map && follower.Alive)
            {
                return (BaseCreature)follower;
            }
        }

        return null;
    }

    /// <summary>True when a pack beast of the owner waits in the stables.</summary>
    public static bool HasStabledBeast(SosariaCharacter owner)
    {
        foreach (var stabled in owner?.Stabled ?? [])
        {
            if (stabled is { Deleted: false } && IsPackBeast(stabled.GetType()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Buys the beast from the animal trainers across the counter through the vendor's own
    /// buy path. The trainer hands it over tame; the owner tells it to follow.
    /// </summary>
    public static bool Buy(SosariaCharacter buyer, IReadOnlyList<BaseVendor> vendors, Type beast)
    {
        for (var i = 0; i < (vendors?.Count ?? 0); i++)
        {
            var line = VendorDeal.ShelfLine(vendors[i], [beast]);
            var entity = line?.GetDisplayEntity();

            if (entity == null || !PackFunds.FundPack(buyer, Math.Max(1, line.Price)) ||
                !vendors[i].OnBuyItems(buyer, [new BuyItemResponse(entity.Serial, 1)]))
            {
                continue;
            }

            if (BeastOf(buyer) is not { } bought)
            {
                return false;
            }

            bought.ControlTarget = buyer;
            bought.ControlOrder = OrderType.Follow;

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} bought {Beast} to carry the haul", buyer.Name, bought.Name);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves the harvest in the owner's pack into the beast's pack while the beast stands in
    /// reach and has room. Returns the units moved.
    /// </summary>
    public static int Load(SosariaCharacter owner)
    {
        var beastPack = PackInReach(owner);
        var moved = 0;

        if (beastPack == null || owner.Backpack == null)
        {
            return 0;
        }

        foreach (var item in HarvestIn(owner.Backpack))
        {
            var amount = item.Amount;

            if (beastPack.TryDropItem(owner, item, false))
            {
                moved += amount;
            }
        }

        if (moved > 0 && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} loaded {Count} onto its pack beast", owner.Name, moved);
        }

        return moved;
    }

    /// <summary>
    /// Takes the beast's load back into the owner's pack, splitting a stack when the pack
    /// fills or the owner would carry too much, as a player drags goods off the llama at the
    /// counter. Returns the units moved.
    /// </summary>
    public static int Unload(SosariaCharacter owner)
    {
        var beastPack = PackInReach(owner);
        var pack = owner?.Backpack;
        var moved = 0;

        if (beastPack == null || pack == null)
        {
            return 0;
        }

        foreach (var item in HarvestIn(beastPack))
        {
            var room = Math.Min(
                pack.MaxWeight * UnloadFillFraction - pack.TotalWeight,
                CarryLoad.RoomStones(owner, UnloadFillFraction)
            );
            var units = Math.Min(item.Amount, (int)(room / Math.Max(item.Weight, MinUnitWeight)));

            if (units <= 0)
            {
                break;
            }

            SupplyCheck.MoveUnits(item, units, pack);
            moved += units;
        }

        return moved;
    }

    private static Container PackInReach(SosariaCharacter owner)
    {
        var beast = BeastOf(owner);
        return beast != null && owner.InRange(beast, ReachTiles) ? beast.Backpack : null;
    }

    private static List<Item> HarvestIn(Container pack)
    {
        var found = new List<Item>();

        foreach (var item in pack.Items)
        {
            if (item is { Deleted: false } && HarvestPack.IsHarvest(item))
            {
                found.Add(item);
            }
        }

        return found;
    }

    private static bool OwnsBeast(SosariaCharacter owner)
    {
        foreach (var follower in owner.AllFollowers ?? [])
        {
            if (IsOwnedBeast(owner, follower))
            {
                return true;
            }
        }

        return HasStabledBeast(owner);
    }

    private static bool IsOwnedBeast(Mobile owner, Mobile follower) =>
        follower is BaseCreature { Deleted: false, Controlled: true } creature && creature.ControlMaster == owner &&
        IsPackBeast(creature.GetType());

    private static bool Works(SosariaCharacter worker, string kind) =>
        worker?.Routine?.HasSkill(kind) == true || worker?.Definition?.UsesSkill(kind) == true;
}
