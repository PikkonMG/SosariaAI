using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PetFoodRulesTests
{
    private const string ButcherToken = "vendor:Butcher";
    private const string ProvisionerToken = "vendor:Provisioner";
    private const string FishermanToken = "vendor:Fisherman";
    private const string BakerToken = "vendor:Baker";
    private const int NoFood = 0;
    private const int OneUnit = 1;
    private const int ShortStack = 3;
    private const int SmallerShortfall = 4;
    private const int ShortPets = 2;

    // The engine's drake and horse (Drake.FavoriteFood, Horse.FavoriteFood).
    private const FoodType DrakeEats = FoodType.Meat | FoodType.Fish;
    private const FoodType HorseEats = FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    [Theory]
    [InlineData(FoodType.Meat, typeof(RawRibs), new[] { ButcherToken })]
    [InlineData(FoodType.FruitsAndVeggies, typeof(Apple), new[] { ProvisionerToken })]
    [InlineData(FoodType.Fish, typeof(RawFishSteak), new[] { FishermanToken })]
    [InlineData(FoodType.GrainsAndHay, typeof(BreadLoaf), new[] { BakerToken, ProvisionerToken })]
    public void Pantry_MapsAFoodKindToTheItemAndTheShopsThatSellIt(FoodType kind, Type item, string[] shops)
    {
        var food = Assert.Single(PetFoodRules.FoodsFor(kind));

        Assert.Equal(item, food.Item);
        Assert.Equal(shops, food.Shops);
    }

    [Fact]
    public void FoodsFor_ADrake_RibsThenFish()
    {
        Assert.Equal(
            [typeof(RawRibs), typeof(RawFishSteak)],
            PetFoodRules.FoodsFor(DrakeEats).ConvertAll(food => food.Item)
        );
    }

    [Fact]
    public void FoodsFor_AHorse_ApplesThenBread()
    {
        Assert.Equal(
            [typeof(Apple), typeof(BreadLoaf)],
            PetFoodRules.FoodsFor(HorseEats).ConvertAll(food => food.Item)
        );
    }

    [Fact]
    public void FoodsFor_AnEggEater_NothingAShopInReachSells()
    {
        // The farmer sells eggs, and no farm is a destination.
        Assert.Empty(PetFoodRules.FoodsFor(FoodType.Eggs));
        Assert.Empty(PetFoodRules.FoodsFor(FoodType.None));
    }

    [Fact]
    public void LowMarkAndTarget_AreWholeFeeds_TheTargetAboveTheLowMark()
    {
        Assert.Equal(0, PetFoodRules.LowMark % PetRules.FeedAmount);
        Assert.Equal(0, PetFoodRules.PantryTarget % PetRules.FeedAmount);
        Assert.True(PetFoodRules.PantryTarget > PetFoodRules.LowMark);
    }

    [Fact]
    public void IsLow_UnderTheLowMarkOnly()
    {
        Assert.True(PetFoodRules.IsLow(NoFood));
        Assert.True(PetFoodRules.IsLow(PetFoodRules.LowMark - 1));
        Assert.False(PetFoodRules.IsLow(PetFoodRules.LowMark));
        Assert.False(PetFoodRules.IsLow(PetFoodRules.PantryTarget));
    }

    [Fact]
    public void Shortfall_UpToTheTarget_NoneAboveIt()
    {
        Assert.Equal(PetFoodRules.PantryTarget, PetFoodRules.Shortfall(NoFood));
        Assert.Equal(PetFoodRules.PantryTarget - ShortStack, PetFoodRules.Shortfall(ShortStack));
        Assert.Equal(0, PetFoodRules.Shortfall(PetFoodRules.PantryTarget + 1));
    }

    [Fact]
    public void Needs_AFedPet_NoNeed()
    {
        Assert.Empty(PetFoodRules.Needs([new PetLarder(DrakeEats, PetFoodRules.LowMark)]));
        Assert.Empty(PetFoodRules.Needs([]));
        Assert.Empty(PetFoodRules.Needs(null));
    }

    [Fact]
    public void Needs_AnEggEaterOutOfFood_NoReasonToShop()
    {
        Assert.Empty(PetFoodRules.Needs([new PetLarder(FoodType.Eggs, NoFood)]));
    }

    [Fact]
    public void Needs_TheHungriestPetFirst_EachWithItsOwnFoods()
    {
        var needs = PetFoodRules.Needs(
            [
                new PetLarder(HorseEats, OneUnit),
                new PetLarder(DrakeEats, PetFoodRules.PantryTarget),
                new PetLarder(DrakeEats, NoFood)
            ]
        );

        Assert.Equal(ShortPets, needs.Count);
        Assert.Equal(typeof(RawRibs), needs[0].Foods[0].Item);
        Assert.Equal(PetFoodRules.PantryTarget, needs[0].Shortfall);
        Assert.Equal(typeof(Apple), needs[1].Foods[0].Item);
        Assert.Equal(PetFoodRules.PantryTarget - OneUnit, needs[1].Shortfall);
    }

    [Fact]
    public void AddLine_TwoPetsOfOneFood_ShareTheStack_TheLargerShortfallStands()
    {
        var wanted = new List<(Type Type, int Amount)>();

        PetFoodRules.AddLine(wanted, typeof(RawRibs), SmallerShortfall);
        PetFoodRules.AddLine(wanted, typeof(RawRibs), PetFoodRules.PantryTarget);
        PetFoodRules.AddLine(wanted, typeof(RawRibs), SmallerShortfall);
        PetFoodRules.AddLine(wanted, typeof(Apple), SmallerShortfall);

        Assert.Equal([(typeof(RawRibs), PetFoodRules.PantryTarget), (typeof(Apple), SmallerShortfall)], wanted);
    }
}
