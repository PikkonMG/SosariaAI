using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Misc;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// What a person carries against the engine's own carry limit (<see cref="StaminaSystem"/>),
/// how much more of a piece it takes and still walks, and the way a player lightens an
/// overloaded pack so it can walk again: the haul onto its pack beast when the beast stands
/// by, into the bank box at the counter, else the surplus on the ground at its feet. World
/// thread only.
/// </summary>
public static class CarryLoad
{
    private const string IntoBankWay = "into the bank";
    private const string OnGroundWay = "on the ground";
    private const double WholeLimit = 1.0;

    private static readonly ILogger logger = SosariaLog.For(typeof(CarryLoad));

    /// <summary>The body and everything it wears and carries, as the engine weighs a step.</summary>
    public static int Carried(Mobile person) => Mobile.BodyWeight + person.TotalWeight;

    /// <summary>The most the body carries before the engine taxes its steps.</summary>
    public static int Limit(Mobile person) => StaminaSystem.GetMaxWeight(person);

    /// <summary>True when the load has reached <paramref name="fillFraction"/> of the body's limit.</summary>
    public static bool IsLoaded(Mobile person, double fillFraction) =>
        PackFill.IsAtOrAboveFraction(Carried(person), Limit(person), fillFraction);

    /// <summary>Stones that still fit under <paramref name="fillFraction"/> of the body's limit.</summary>
    public static int RoomStones(Mobile person, double fillFraction) =>
        CarryRules.RoomStones(Carried(person), Limit(person), fillFraction);

    /// <summary>The weight one more <paramref name="sample"/> adds: a pile adds its own weight, each other piece its whole self.</summary>
    public static double UnitWeight(Item sample) => sample.Stackable ? sample.Weight : sample.PileWeight + sample.TotalWeight;

    /// <summary>Units of <paramref name="sample"/> the body still carries under its limit, never more than <paramref name="units"/>.</summary>
    public static int UnitsItCarries(Mobile person, Item sample, int units) =>
        CarryRules.UnitsInRoom(RoomStones(person, WholeLimit), UnitWeight(sample), units);

    /// <summary>
    /// What a person may shed: its harvest, and the craft stock a crafter bought (hides, leather,
    /// boards, ingots). Gear, tools, reagents and bandages stay.
    /// </summary>
    public static bool IsHaul(Item item) =>
        HarvestPack.IsHarvest(item) || CraftResources.GetFromType(item.GetType()) != CraftResource.None;

    /// <summary>
    /// Lightens an overloaded person down to its limit. The whole haul goes onto a pack beast
    /// in reach; what is still too much goes into the bank box at a banker, else on the
    /// ground, heaviest pieces first. Returns the pieces shed; zero when the person walks freely.
    /// </summary>
    public static int Shed(SosariaCharacter person)
    {
        if (person?.Backpack == null || !StaminaSystem.IsOverloaded(person))
        {
            return 0;
        }

        // The beast's own line tells this load.
        var onBeast = PackAnimals.Load(person);

        if (!StaminaSystem.IsOverloaded(person))
        {
            return onBeast;
        }

        // Only the haul is shed. A hunter's loot stays, and LootRules keeps it under the limit.
        var haul = HaulHeaviestFirst(person.Backpack);

        if (haul.Count == 0)
        {
            return onBeast;
        }

        var carriedBefore = Carried(person);
        var box = BankTeller.FindBanker(person) != null && BankTeller.OpenBox(person) ? person.BankBox : null;
        var banked = 0;
        var dropped = 0;

        foreach (var stack in haul)
        {
            var units = CarryRules.UnitsToShed(
                CarryRules.SurplusStones(Carried(person), Limit(person)),
                stack.Weight,
                stack.Amount
            );

            if (units <= 0)
            {
                break;
            }

            if (units < stack.Amount)
            {
                Mobile.LiftItemDupe(stack, units);
            }

            if (box != null && box.TryDropItem(person, stack, false))
            {
                banked += units;
                continue;
            }

            stack.MoveToWorld(person.Location, person.Map);
            dropped += units;
        }

        if (banked > 0)
        {
            LogShed(person, banked, IntoBankWay, carriedBefore);
        }

        if (dropped > 0)
        {
            LogShed(person, dropped, OnGroundWay, carriedBefore);
        }

        return onBeast + banked + dropped;
    }

    private static List<Item> HaulHeaviestFirst(Container pack)
    {
        var haul = new List<Item>();

        foreach (var item in pack.Items)
        {
            if (item is { Deleted: false, Movable: true } && IsHaul(item))
            {
                haul.Add(item);
            }
        }

        haul.Sort((a, b) => CarryRules.HeaviestFirst(a.Weight, b.Weight));
        return haul;
    }

    private static void LogShed(SosariaCharacter person, int pieces, string way, int carriedBefore)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} was overloaded at {Carried} of {Limit} stones and put {Count} pieces of its haul {Way}",
                person.Name,
                carriedBefore,
                Limit(person),
                pieces,
                way
            );
        }
    }
}
