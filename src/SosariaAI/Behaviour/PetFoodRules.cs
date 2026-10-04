using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>One food a tamer buys for its pets: the engine's food kind, the item bought, and the shops that sell it.</summary>
public readonly record struct PantryFood(FoodType Kind, Type Item, IReadOnlyList<string> Shops);

/// <summary>One pet's larder: the food kinds it eats and the units in its owner's pack it would eat now.</summary>
public readonly record struct PetLarder(FoodType Eats, int Units);

/// <summary>A pet short of food: the foods it eats that a shop sells, in buying order, and the units to buy.</summary>
public readonly record struct PantryNeed(IReadOnlyList<PantryFood> Foods, int Shortfall);

/// <summary>
/// A tamer buys the food its pets eat, as a player keeps a stack of ribs for the drake and
/// apples for the horse. Tamers started with ten raw ribs and never bought more: a live night
/// of five and a half hours saw thirteen feeds, and a pet whose food ran out lost its loyalty
/// and went wild. Each food kind maps to one item the engine's pets eat and the town shops
/// that stock it (the engine's food lists, BaseCreature; its shop lists, SBInfo): raw ribs at
/// the butcher, apples at the provisioner, raw fish steaks at the fisherman, bread at the
/// baker or the provisioner. The farmer sells eggs and hay too, but no farm is a destination
/// a walk can name, so a pet that eats only eggs has no food in reach.
/// </summary>
public static class PetFoodRules
{
    /// <summary>Under this many feeds of what a pet eats left in the pack, the tamer goes shopping.</summary>
    public const int FeedsLeftLow = 2;

    /// <summary>A pantry is stocked with this many feeds of what each pet eats.</summary>
    public const int FeedsStocked = 5;

    /// <summary>Units of a pet's food under which the tamer goes shopping for it.</summary>
    public const int LowMark = FeedsLeftLow * PetRules.FeedAmount;

    /// <summary>Units of a pet's food a tamer buys up to, and keeps from every sale.</summary>
    public const int PantryTarget = FeedsStocked * PetRules.FeedAmount;

    private static readonly string[] MeatShops = [ShopFinder.ButcherToken];
    private static readonly string[] FruitShops = [ShopFinder.ProvisionerToken];
    private static readonly string[] FishShops = [ShopFinder.FishermanToken];
    private static readonly string[] BreadShops = [ShopFinder.BakerToken, ShopFinder.ProvisionerToken];

    /// <summary>
    /// The foods a tamer buys, in the order it picks among those a pet eats: meat for the
    /// meat eaters (the ribs it walked in with), fruit for the grazers, fish, then bread.
    /// </summary>
    public static readonly PantryFood[] Pantry =
    [
        new(FoodType.Meat, typeof(RawRibs), MeatShops),
        new(FoodType.FruitsAndVeggies, typeof(Apple), FruitShops),
        new(FoodType.Fish, typeof(RawFishSteak), FishShops),
        new(FoodType.GrainsAndHay, typeof(BreadLoaf), BreadShops)
    ];

    /// <summary>The pantry foods a pet that eats <paramref name="eats"/> takes, in buying order.</summary>
    public static List<PantryFood> FoodsFor(FoodType eats)
    {
        var foods = new List<PantryFood>();

        for (var i = 0; i < Pantry.Length; i++)
        {
            if ((eats & Pantry[i].Kind) != FoodType.None)
            {
                foods.Add(Pantry[i]);
            }
        }

        return foods;
    }

    /// <summary>True when a pet has fewer than <see cref="LowMark"/> units of its food left.</summary>
    public static bool IsLow(int units) => units < LowMark;

    /// <summary>Units to buy to bring a pet's food up to <see cref="PantryTarget"/>.</summary>
    public static int Shortfall(int units) => Math.Max(0, PantryTarget - units);

    /// <summary>
    /// The pets short of food that a shop sells, the hungriest first: one need for each pet under
    /// <see cref="LowMark"/> that eats a pantry food. A pet that eats none of them is no reason to shop.
    /// </summary>
    public static List<PantryNeed> Needs(IReadOnlyList<PetLarder> pets)
    {
        var needs = new List<PantryNeed>();

        for (var i = 0; i < (pets?.Count ?? 0); i++)
        {
            var foods = FoodsFor(pets[i].Eats);

            if (!IsLow(pets[i].Units) || foods.Count == 0)
            {
                continue;
            }

            var need = new PantryNeed(foods, Shortfall(pets[i].Units));
            var at = needs.FindIndex(existing => existing.Shortfall < need.Shortfall);
            needs.Insert(at < 0 ? needs.Count : at, need);
        }

        return needs;
    }

    /// <summary>
    /// Adds a buy line for <paramref name="item"/>, or raises the line already there: two pets
    /// that eat the same food share one stack, so the larger shortfall stands.
    /// </summary>
    public static void AddLine(List<(Type Type, int Amount)> wanted, Type item, int amount)
    {
        var at = wanted.FindIndex(line => line.Type == item);

        if (at < 0)
        {
            wanted.Add((item, amount));
        }
        else
        {
            wanted[at] = (item, Math.Max(wanted[at].Amount, amount));
        }
    }
}
