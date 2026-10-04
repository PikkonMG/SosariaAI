using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A fresh tamer dressed through the real first-day path is a mage-tamer, and the engine's own
/// beasts are judged by the engine's own numbers: a grandmaster takes a dragon, a journeyman
/// does not, and nobody takes a cow.
/// </summary>
[Collection(RealMapCollection.Name)]
public class TamerKitTests
{
    public TamerKitTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.EnsureBeasts();
        }
    }

    private static readonly Point3D BritainStreet = new(1415, 1690, 0);
    private static readonly Point3D BesideTamer = new(1416, 1690, 0);
    private static readonly Point3D DroppedBack = new(1421, 1690, 0);
    private const int DrakeSight = 10;
    private const int WeakerHits = 50;
    private const int NoUnits = 0;

    private static SosariaCharacter Tamer(SkillTier tier, string id) =>
        KitWorld.Dress(KitWorld.Profile(PersonClass.Tamer, tier, PersonWealth.Comfortable, false), id);

    [RealMapFact]
    public void FreshTamer_CarriesBookReagentsBandagesAndPetFood()
    {
        var tamer = Tamer(SkillTier.Expert, "Felucca:osric#11");
        var pack = tamer.Backpack;

        Assert.NotNull(Spellbook.FindRegular(tamer));
        Assert.All(SupplyCheck.ReagentTypes, reagent => Assert.True(pack.GetAmount(reagent) > 0, reagent.Name));
        Assert.True(pack.GetAmount(typeof(Bandage)) > 0);
        Assert.True(pack.GetAmount(typeof(RawRibs)) >= PetFoodRules.PantryTarget);
        Assert.True(tamer.Skills.Magery.Base > 0);
        Assert.True(tamer.Skills.Veterinary.Base > 0);
        Assert.True(tamer.Skills.EvalInt.Base > 0);
    }

    [RealMapFact]
    public void Grandmaster_TakesADragon_AJourneymanDoesNot()
    {
        var grandmaster = Tamer(SkillTier.Grandmaster, "Felucca:osric#12");
        var journeyman = Tamer(SkillTier.Journeyman, "Felucca:osric#13");
        var dragon = new Dragon();

        try
        {
            Assert.True(TamingGrounds.IsQuarry(grandmaster, dragon));
            Assert.False(TamingGrounds.IsQuarry(journeyman, dragon));
            Assert.True(TameRules.IsKeeper(TamingGrounds.ProfileOf(dragon).Power, grandmaster.Skills.AnimalTaming.Value));
        }
        finally
        {
            dragon.Delete();
        }
    }

    [RealMapFact]
    public void ATripThatNeverBeganOrEnded_TakesUpNoBeastThatStrikes()
    {
        // Live crash: a beast struck a tamer whose current trip had no tamer, and the hits check
        // read a null tamer inside the engine's swing.
        var tamer = Tamer(SkillTier.Grandmaster, "Felucca:osric#17");
        var dragon = new Dragon();
        var trip = new TameSkill();

        try
        {
            Assert.False(trip.TakeUp(tamer, dragon));
            Assert.False(trip.TakeUp(null, dragon));

            trip.Abort();

            Assert.False(trip.TakeUp(tamer, dragon));
        }
        finally
        {
            dragon.Delete();
        }
    }

    [RealMapFact]
    public void NoTamer_TakesACowOrAChicken()
    {
        var tamer = Tamer(SkillTier.Novice, "Felucca:osric#14");
        var cow = new Cow();
        var chicken = new Chicken();

        try
        {
            Assert.False(TamingGrounds.IsQuarry(tamer, cow));
            Assert.False(TamingGrounds.IsQuarry(tamer, chicken));
            Assert.True(TameRules.IsLowBeast(TamingGrounds.ProfileOf(cow).Power));
        }
        finally
        {
            cow.Delete();
            chicken.Delete();
        }
    }

    [RealMapFact]
    public void Tamer_KeepsTheFoodItsPetEatsFromTheCook_AndNothingElse()
    {
        var tamer = Tamer(SkillTier.Master, "Felucca:osric#19");
        var drake = new Drake();
        var ribs = new RawRibs();
        var apple = new Apple();
        var gold = new Gold();

        try
        {
            // No pet yet: the first-day ribs are goods like any other.
            Assert.Equal(NoUnits, PetKeeper.FoodKept(tamer, ribs));

            tamer.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
            drake.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            Assert.True(drake.SetControlMaster(tamer));

            Assert.Equal(PetFoodRules.PantryTarget, PetKeeper.FoodKept(tamer, ribs));
            Assert.Equal(NoUnits, PetKeeper.FoodKept(tamer, apple));
            Assert.Equal(NoUnits, PetKeeper.FoodKept(tamer, gold));
        }
        finally
        {
            ribs.Delete();
            apple.Delete();
            gold.Delete();
            drake.Delete();
            tamer.Delete();
        }
    }

    [RealMapFact]
    public void EnginePets_EatThePantryFoodOfEachKindTheyEat_AndNoOther()
    {
        // The drake eats meat and fish, the horse fruit and grain: between them every pantry kind.
        var pets = new BaseCreature[] { new Drake(), new Horse() };
        var foods = new Item[] { new RawRibs(), new Apple(), new RawFishSteak(), new BreadLoaf() };

        try
        {
            foreach (var pantry in PetFoodRules.Pantry)
            {
                var food = Array.Find(foods, item => item.GetType() == pantry.Item);

                foreach (var pet in pets)
                {
                    Assert.Equal((pet.FavoriteFood & pantry.Kind) != FoodType.None, pet.CheckFoodPreference(food));
                }
            }
        }
        finally
        {
            Array.ForEach(foods, food => food.Delete());
            Array.ForEach(pets, pet => pet.Delete());
        }
    }

    [RealMapFact]
    public void TamerWithADrake_IsLowOnceItsFoodRunsOut()
    {
        var tamer = Tamer(SkillTier.Master, "Felucca:osric#20");
        var drake = new Drake();

        try
        {
            tamer.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
            Assert.False(PetPantry.IsLow(tamer));

            drake.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            Assert.True(drake.SetControlMaster(tamer));

            // The first-day ribs fill the pantry.
            Assert.Equal(PetFoodRules.PantryTarget, PetKeeper.FoodOnHand(tamer, drake));
            Assert.False(PetPantry.IsLow(tamer));

            tamer.Backpack.FindItemByType<RawRibs>().Delete();

            Assert.Equal(NoUnits, PetKeeper.FoodOnHand(tamer, drake));
            Assert.True(PetPantry.IsLow(tamer));
        }
        finally
        {
            drake.Delete();
            tamer.Delete();
        }
    }

    [RealMapFact]
    public void TamerWithAPetOut_WillNotRecallAwayFromIt_AndCountsItAsAnAlly()
    {
        var tamer = Tamer(SkillTier.Master, "Felucca:osric#16");
        var drake = new Drake();

        try
        {
            tamer.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
            drake.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            Assert.False(PetKeeper.RecallStrandsPets(tamer));
            Assert.True(drake.SetControlMaster(tamer));

            // Recall takes only bonded pets along; nothing bonds in the Second Age.
            Assert.True(PetKeeper.RecallStrandsPets(tamer));
            Assert.True(PetKeeper.PetsPower(tamer) >= TameRules.KeepPower(tamer.Skills.AnimalTaming.Value));
            Assert.True(Party.AlliesPower(tamer) >= PetKeeper.PetsPower(tamer));
            Assert.True(GateRules.PrefersGate(leadsParty: false, PetKeeper.RecallStrandsPets(tamer), bystanders: 0));
        }
        finally
        {
            drake.Delete();
            tamer.Delete();
        }
    }

    [RealMapFact]
    public void TamerWithTwoDrakes_KeepsTheStrongerAndLetsTheWeakerGo()
    {
        var tamer = Tamer(SkillTier.Master, "Felucca:osric#17");
        var weaker = new Drake();
        var stronger = new Drake();

        try
        {
            tamer.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
            weaker.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            stronger.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            weaker.HitsMaxSeed = stronger.HitsMaxSeed - WeakerHits;
            Assert.True(weaker.SetControlMaster(tamer));
            Assert.True(stronger.SetControlMaster(tamer));

            Assert.Equal(TamingGrounds.ProfileOf(stronger).Power, PetKeeper.FighterPower(tamer));
            Assert.True(PetKeeper.Keeps(tamer, stronger));
            Assert.False(PetKeeper.Keeps(tamer, weaker));

            PetKeeper.LetGoSparePets(tamer);

            Assert.False(weaker.Controlled);
            Assert.True(stronger.Controlled);
            Assert.Equal(stronger, tamer.AllFollowers.Single());
        }
        finally
        {
            weaker.Delete();
            stronger.Delete();
            tamer.Delete();
        }
    }

    [RealMapFact]
    public void TamerOnTheRoad_StandsForAPetThatDroppedBack()
    {
        var tamer = Tamer(SkillTier.Master, "Felucca:osric#18");
        var drake = new Drake();

        try
        {
            tamer.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
            drake.MoveToWorld(BesideTamer, RealMapWorld.Felucca);
            drake.RangePerception = DrakeSight;
            Assert.True(drake.SetControlMaster(tamer));
            drake.ControlTarget = tamer;
            drake.ControlOrder = OrderType.Follow;

            Assert.False(PetKeeper.HoldsForPets(tamer));

            drake.MoveToWorld(DroppedBack, RealMapWorld.Felucca);
            Assert.True(PetKeeper.HoldsForPets(tamer));

            // A pet that stays: nothing to wait for.
            drake.ControlOrder = OrderType.Stay;
            Assert.False(PetKeeper.HoldsForPets(tamer));
        }
        finally
        {
            drake.Delete();
            tamer.Delete();
        }
    }
}
