using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class JobRulesTests
{
    private const int SeedSweep = 400;
    private const int MinDistinctJobs = 4;
    private const int FixedSeed = 12345;
    private const double BraveValor = 0.9;
    private const double LowCaution = 0.1;
    private const double EvenDrive = 0.5;
    private const double TimidValor = 0.1;
    private const double HighCaution = 0.9;
    private const double MinOutingShare = 0.4;
    private const double MinDelveShare = 0.15;
    private const double DelveToHuntRatio = 0.75;

    private static readonly string[] FighterSkills =
    [
        SkillKinds.Hunt, SkillKinds.Dungeon, SkillKinds.BankShop, SkillKinds.BankDeposit,
        SkillKinds.Tavern, SkillKinds.VendorBuy, SkillKinds.Travel, SkillKinds.Tactics
    ];

    private static readonly string[] TownSkills = [SkillKinds.BankShop, SkillKinds.Tavern];

    [Fact]
    public void NameOf_RoundTripsEveryJob()
    {
        foreach (var job in new[] { JobKind.Bank, JobKind.Hunt, JobKind.Travel, JobKind.Idle })
        {
            Assert.True(JobRules.TryParse(JobRules.NameOf(job), out var parsed));
            Assert.Equal(job, parsed);
        }

        Assert.False(JobRules.IsJob(SkillKinds.House));
        Assert.False(JobRules.IsJob(GoalRules.NoTarget));
    }

    [Fact]
    public void GoalFor_CarriesTheJobName()
    {
        var goal = JobRules.GoalFor(JobKind.Bank);

        Assert.Equal(GoalKind.Trade, goal.Kind);
        Assert.Equal(JobRules.NameOf(JobKind.Bank), goal.Target);
        Assert.True(JobRules.IsTownJob(goal.Target));
        Assert.False(JobRules.IsTownJob(JobRules.NameOf(JobKind.Hunt)));
        Assert.True(JobRules.IsTravel(JobRules.NameOf(JobKind.Travel)));
    }

    [Fact]
    public void Roll_SameSeedSameFacts_KeepsTheJob()
    {
        var situation = Fighter();

        Assert.Equal(
            JobRules.Roll(situation, FixedSeed, FighterSkills),
            JobRules.Roll(situation, FixedSeed, FighterSkills)
        );
    }

    [Fact]
    public void Roll_AcrossPhases_GivesAVariedLife()
    {
        // A work rota picked the same short errand again and again. The dice give a mix.
        var seen = new HashSet<JobKind>();

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            seen.Add(JobRules.Roll(Fighter(), seed, FighterSkills));
        }

        Assert.True(seen.Count >= MinDistinctJobs, $"only {seen.Count} jobs");
    }

    [Fact]
    public void Roll_OnlyPicksJobsThePersonCanDo()
    {
        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var job = JobRules.Roll(Fighter(), seed, TownSkills);
            Assert.Contains(job, new[] { JobKind.Bank, JobKind.Tavern, JobKind.Idle });
        }
    }

    [Fact]
    public void Roll_Excluded_IsNeverPicked()
    {
        var excluded = new[] { JobKind.Hunt, JobKind.Dungeon, JobKind.Bank };

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            Assert.DoesNotContain(JobRules.Roll(Fighter(), seed, FighterSkills, excluded), excluded);
        }
    }

    [Fact]
    public void Roll_NothingDoable_Idles()
    {
        Assert.Equal(JobKind.Idle, JobRules.Roll(Fighter(), FixedSeed, []));
    }

    [Fact]
    public void Weight_NightCutsOutingsAndFavoursTown()
    {
        var day = Fighter(DayPart.Work);
        var night = Fighter(DayPart.Night);

        Assert.True(JobRules.Weight(JobKind.Hunt, night) < JobRules.Weight(JobKind.Hunt, day));
        Assert.True(JobRules.Weight(JobKind.Tavern, night) > JobRules.Weight(JobKind.Tavern, day));
    }

    [Fact]
    public void Weight_DeathAndFleeCutOutings()
    {
        var calm = Fighter();
        var died = Fighter() with { DiedRecently = true };
        var ran = Fighter() with { RecentRuns = 1 };

        Assert.True(JobRules.Weight(JobKind.Hunt, died) < JobRules.Weight(JobKind.Hunt, calm));
        Assert.True(JobRules.Weight(JobKind.Dungeon, ran) < JobRules.Weight(JobKind.Dungeon, calm));
        Assert.True(JobRules.Weight(JobKind.Travel, ran) < JobRules.Weight(JobKind.Travel, calm));
    }

    [Fact]
    public void Weight_AwayFromHome_HeadsHomeRatherThanOut()
    {
        var home = Fighter();
        var away = Fighter() with { BeyondLeash = true };

        Assert.True(JobRules.Weight(JobKind.Travel, away) < JobRules.Weight(JobKind.Travel, home));
        Assert.True(JobRules.Weight(JobKind.Hunt, away) < JobRules.Weight(JobKind.Hunt, home));
        Assert.Equal(JobRules.Weight(JobKind.Tavern, home), JobRules.Weight(JobKind.Tavern, away));
    }

    [Fact]
    public void Roll_AnAdventurousFighter_SpendsALargeShareOnHuntsAndDungeons()
    {
        // In the first live run 41 of some 2,400 choices were a delve, and nobody stood in a
        // dungeon. An even-tempered adventurer, as the profile rolls one, goes out often.
        var outings = 0;
        var delves = 0;
        var adventurer = EvenAdventurer();

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var job = JobRules.Roll(adventurer, seed, FighterSkills);
            outings += job is JobKind.Hunt or JobKind.Dungeon ? 1 : 0;
            delves += job == JobKind.Dungeon ? 1 : 0;
        }

        Assert.True(outings >= SeedSweep * MinOutingShare, $"only {outings} of {SeedSweep} outings");
        Assert.True(delves >= SeedSweep * MinDelveShare, $"only {delves} of {SeedSweep} delves");
    }

    [Fact]
    public void Weight_AFightersDelve_WeighsAboutAsMuchAsItsHunt()
    {
        var adventurer = EvenAdventurer();

        Assert.True(
            JobRules.Weight(JobKind.Dungeon, adventurer) >= JobRules.Weight(JobKind.Hunt, adventurer) * DelveToHuntRatio
        );
    }

    [Fact]
    public void Weight_ATimidFighter_DelvesLessButStillAtTheFloor()
    {
        var timid = EvenAdventurer() with
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = DayPart.Work.ToString(),
                Drives = new PersonaDrives(greed: EvenDrive, caution: HighCaution, valor: TimidValor, isCustom: true)
            },
            Tendencies = null
        };
        var even = EvenAdventurer() with { Tendencies = null };

        Assert.True(JobRules.Weight(JobKind.Dungeon, timid) < JobRules.Weight(JobKind.Dungeon, even));
        Assert.True(JobRules.Weight(JobKind.Dungeon, timid) >= JobRules.DungeonFloor);
    }

    [Fact]
    public void Weight_WorkerDoesNotDelve()
    {
        Assert.Equal(0, JobRules.Weight(JobKind.Dungeon, Fighter() with { Role = CharacterRole.Worker }));
    }

    [Fact]
    public void PlanServes_OnlyItsOwnJob()
    {
        Assert.True(JobRules.PlanServes(JobKind.Bank, JobRules.BankPlan));
        Assert.False(JobRules.PlanServes(JobKind.Shop, JobRules.BankPlan));
        Assert.True(JobRules.PlanServes(JobKind.Hunt, GoalPlanRules.HuntTrip));
        Assert.True(JobRules.PlanServes(JobKind.Craft, GoalPlanRules.MinerWork));
        Assert.True(JobRules.PlanServes(JobKind.Craft, GoalPlanRules.SellBank));
        Assert.False(JobRules.PlanServes(JobKind.Tavern, GoalPlanRules.MinerWork));
    }

    private static Situation EvenAdventurer() =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = DayPart.Work.ToString(),
                Drives = new PersonaDrives(greed: EvenDrive, caution: EvenDrive, valor: EvenDrive, isCustom: true)
            },
            Role = CharacterRole.Fighter,
            BankCrowdOpen = true,
            Tendencies = PersonProfileRules.RollTendencies("adventurer", PersonClass.Warrior, PersonTrait.None)
        };

    private static Situation Fighter(DayPart part = DayPart.Work) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = part.ToString(),
                Drives = new PersonaDrives(greed: 0.5, caution: LowCaution, valor: BraveValor, isCustom: true)
            },
            Role = CharacterRole.Fighter
        };

    [Fact]
    public void Weight_EmptyHallsPullFightersUnderground()
    {
        var calm = Fighter();
        var pulled = calm with { DungeonDemand = DungeonShareRules.MaxDemand };

        Assert.Equal(
            JobRules.Weight(JobKind.Dungeon, calm) * DungeonShareRules.MaxDemand,
            JobRules.Weight(JobKind.Dungeon, pulled),
            precision: 6
        );
        Assert.Equal(JobRules.Weight(JobKind.Hunt, calm), JobRules.Weight(JobKind.Hunt, pulled));
    }

    [Fact]
    public void Odds_AreTheJobsShareOfTheOpenWeights()
    {
        var pulled = Fighter() with { DungeonDemand = DungeonShareRules.MaxDemand };
        var total = 0.0;

        foreach (var job in Enum.GetValues<JobKind>())
        {
            total += JobRules.Weight(job, pulled);
        }

        Assert.Equal(JobRules.Weight(JobKind.Dungeon, pulled) / total, JobRules.Odds(JobKind.Dungeon, pulled, skills: null), precision: 6);
        Assert.Equal(0, JobRules.Odds(JobKind.Dungeon, pulled, skills: [SkillKinds.Tavern]));
    }

    [Fact]
    public void Weight_AFighterInADungeonFightsOn_EvenFarFromHome()
    {
        var away = Fighter() with { BeyondLeash = true };
        var underground = away with { InDungeon = true };

        Assert.True(JobRules.Weight(JobKind.Dungeon, underground) > JobRules.Weight(JobKind.Dungeon, away));
        Assert.True(JobRules.Weight(JobKind.Dungeon, underground) > JobRules.Weight(JobKind.Bank, underground));
    }

    [Fact]
    public void SupplyFactor_PantryShortOfPetFoodSendsATamerShopping()
    {
        var tamer = Fighter() with { NeedsPetFood = true };

        Assert.Equal(JobRules.SuppliesShopBoost, JobRules.SupplyFactor(JobKind.Shop, tamer));
        Assert.Equal(JobRules.Full, JobRules.SupplyFactor(JobKind.Shop, Fighter()));
    }

    [Fact]
    public void SupplyFactor_OnlyRefillableLowSuppliesKeepAFighterOutOfTheDungeon()
    {
        // The situation sets SuppliesLow only when a stocked shop in reach sells the supply at a
        // price the person can pay; a fighter who cannot refill is not held back at all.
        var refillable = Fighter() with { SuppliesLow = true };

        Assert.Equal(JobRules.SuppliesOutingCut, JobRules.SupplyFactor(JobKind.Dungeon, refillable));
        Assert.Equal(JobRules.Full, JobRules.SupplyFactor(JobKind.Dungeon, Fighter()));
    }
}
