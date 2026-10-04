using System;
using System.Collections.Generic;
using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GoalPlanTests
{
    private const int FirstSeed = 0;
    private const int SecondSeed = 1;
    private const int HealthyHits = 1;
    private static readonly string[] MinerSkills =
    [
        SkillKinds.Mine,
        SkillKinds.VendorSell,
        SkillKinds.BankDeposit,
        SkillKinds.VendorBuy,
        SkillKinds.Smith
    ];

    private static readonly string[] LeisureSkills =
    [
        SkillKinds.Tavern,
        SkillKinds.Visit,
        SkillKinds.Sightsee,
        SkillKinds.Loiter
    ];

    private const int RedSeedsToTry = 200;
    private static readonly Goal PkGoal = new(GoalKind.Pk, GoalRules.NoTarget);
    private static readonly Goal LeaveDenGoal = new(GoalKind.Leisure, SkillKinds.GoHome);

    private static readonly string[] RedSkills =
    [
        SkillKinds.Conflict,
        SkillKinds.Dungeon,
        SkillKinds.Hunt,
        SkillKinds.GoHome,
        SkillKinds.Heal,
        SkillKinds.VendorSell,
        SkillKinds.BankDeposit,
        SkillKinds.VendorBuy,
        SkillKinds.Loiter,
        SkillKinds.Tavern
    ];

    private static readonly string[] EverySkill =
    [
        SkillKinds.Mine, SkillKinds.Lumberjack, SkillKinds.Fish, SkillKinds.Boat, SkillKinds.Smith, SkillKinds.Mage,
        SkillKinds.Cartography, SkillKinds.VendorSell, SkillKinds.VendorBuy, SkillKinds.BankDeposit,
        SkillKinds.UpgradeGear, SkillKinds.Hunt, SkillKinds.Heal, SkillKinds.Follow, SkillKinds.Dungeon,
        SkillKinds.Meditate, SkillKinds.Rest, SkillKinds.GoHome, SkillKinds.Tavern, SkillKinds.Visit,
        SkillKinds.Sightsee, SkillKinds.Loiter, SkillKinds.House, SkillKinds.PlayerVendor, SkillKinds.Conflict
    ];

    private static readonly string[] WoodAndMineSkills =
    [
        SkillKinds.Mine,
        SkillKinds.Lumberjack,
        SkillKinds.VendorSell,
        SkillKinds.BankDeposit
    ];

    [Fact]
    public void Start_MinerWork_OrdersGatherSellBank()
    {
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        Assert.Equal(GoalPlanRules.MinerWork, plan.Id);
        Assert.Equal(GoalKind.Work, plan.Kind);
        Assert.Equal(SkillKinds.Mine, plan.CurrentSkill);
        Assert.Equal(
            [SkillKinds.Mine, SkillKinds.VendorSell, SkillKinds.BankDeposit, SkillKinds.VendorBuy, SkillKinds.Smith],
            SkillKindsOf(plan)
        );
        Assert.False(string.IsNullOrWhiteSpace(plan.Purpose));
    }

    [Fact]
    public void AfterSuccess_AdvancesToSellThenBank()
    {
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        plan = GoalPlanRules.AfterOutcome(plan, SkillKinds.Mine, success: true);
        Assert.Equal(SkillKinds.VendorSell, plan.CurrentSkill);

        plan = GoalPlanRules.AfterOutcome(plan, SkillKinds.VendorSell, success: true);
        Assert.Equal(SkillKinds.BankDeposit, plan.CurrentSkill);
    }

    [Fact]
    public void PackedGoods_SkipMineAndStartAtSell()
    {
        var plan = GoalPlanRules.ContinueOrStart(
            current: null,
            new Goal(GoalKind.Trade, GoalRules.NoTarget),
            HealthyWork() with { HasHarvestGoods = true },
            MinerSkills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.VendorSell, plan.CurrentSkill);
        Assert.False(plan.IsComplete);
    }

    [Fact]
    public void AfterThreeFailures_SkipsBlockedStep()
    {
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        for (var i = 0; i < GoalPlanRules.MaxStepTries; i++)
        {
            plan = GoalPlanRules.AfterOutcome(plan, SkillKinds.Mine, success: false);
        }

        Assert.Equal(SkillKinds.VendorSell, plan.CurrentSkill);
        Assert.Equal(0, plan.StepFailures);
    }

    [Fact]
    public void FleeInterrupt_DoesNotAdvanceWorkStep()
    {
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        var afterFlee = GoalPlanRules.AfterOutcome(plan, SkillKinds.Flee, success: true);

        Assert.Equal(SkillKinds.Mine, afterFlee.CurrentSkill);
        Assert.Equal(plan.Index, afterFlee.Index);
    }

    [Fact]
    public void AfterFlee_ResumesSameWorkStep()
    {
        var work = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );
        var fleeing = HealthyWork() with { MustFlee = true };
        var kept = GoalPlanRules.ContinueOrStart(
            work,
            new Goal(GoalKind.Flee, GoalRules.NoTarget),
            fleeing,
            MinerSkills,
            FirstSeed
        );

        Assert.Equal(work.Id, kept.Id);
        Assert.Equal(SkillKinds.Mine, kept.CurrentSkill);

        var resumed = GoalPlanRules.ContinueOrStart(
            kept,
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.Mine, resumed.CurrentSkill);
        Assert.Equal(work.Id, resumed.Id);
    }

    [Fact]
    public void HasHouse_SkipsBuyAndGoesToVendor()
    {
        var skills = new[] { SkillKinds.House, SkillKinds.PlayerVendor, SkillKinds.GoHome };
        var plan = GoalPlanRules.ContinueOrStart(
            current: null,
            new Goal(GoalKind.Work, SkillKinds.House),
            HealthyWork() with { HasHouse = true },
            skills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.PlayerVendor, plan.CurrentSkill);
    }

    [Fact]
    public void HasVendor_GoesBackOnlyWithStockOrTakings()
    {
        var skills = new[] { SkillKinds.House, SkillKinds.PlayerVendor, SkillKinds.GoHome };
        var goal = new Goal(GoalKind.Work, SkillKinds.House);
        var tended = HealthyWork() with { HasHouse = true, HasVendor = true };

        var idle = GoalPlanRules.ContinueOrStart(current: null, goal, tended, skills, FirstSeed);
        var errand = GoalPlanRules.ContinueOrStart(current: null, goal, tended with { VendorErrand = true }, skills, FirstSeed);

        Assert.NotEqual(SkillKinds.PlayerVendor, idle?.CurrentSkill);
        Assert.Equal(SkillKinds.PlayerVendor, errand.CurrentSkill);
    }

    [Fact]
    public void HasBoat_KeepsBoatSkillForReuse()
    {
        var skills = new[] { SkillKinds.Boat, SkillKinds.Fish };
        var plan = GoalPlanRules.ContinueOrStart(
            current: null,
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork() with { HasBoat = true },
            skills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.Boat, plan.CurrentSkill);
        Assert.Equal(GoalPlanRules.BoatOwn, plan.Id);
    }

    [Fact]
    public void Copies_DoNotPickTheSameWorkRecipe()
    {
        var a = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            WoodAndMineSkills,
            FirstSeed
        );
        var b = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            WoodAndMineSkills,
            SecondSeed
        );

        Assert.NotEqual(a.Id, b.Id);
    }

    [Theory]
    [InlineData(FirstSeed)]
    [InlineData(SecondSeed)]
    public void Start_SkillAmbition_PicksTheRecipeThatTrainsIt(int seed)
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits },
            Role = CharacterRole.Worker,
            AmbitionSkill = AmbitionRules.SkillLumberjacking
        };
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            situation,
            WoodAndMineSkills,
            seed
        );

        Assert.Equal(GoalPlanRules.WoodWork, plan.Id);
    }

    [Fact]
    public void Start_PlaceAmbition_LeisureStartsWithTheTrip()
    {
        var situation = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits, AmbitionWantsTravel = true }
        };
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Leisure, GoalRules.NoTarget),
            situation,
            LeisureSkills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.Sightsee, plan.CurrentSkill);
    }

    [Fact]
    public void Restore_KeepsIdIndexAndFailures()
    {
        var started = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );
        var advanced = GoalPlanRules.AfterOutcome(started, SkillKinds.Mine, success: true);
        advanced = GoalPlanRules.AfterOutcome(advanced, SkillKinds.VendorSell, success: false);

        var restored = GoalPlanRules.Restore(advanced.Id, advanced.Index, advanced.StepFailures);

        Assert.Equal(advanced.Id, restored.Id);
        Assert.Equal(advanced.Index, restored.Index);
        Assert.Equal(advanced.StepFailures, restored.StepFailures);
        Assert.Equal(advanced.CurrentSkill, restored.CurrentSkill);
    }

    [Fact]
    public void MatchesCurrent_IsTheForcedNextSkill()
    {
        var plan = GoalPlanRules.Start(
            new Goal(GoalKind.Work, GoalRules.NoTarget),
            HealthyWork(),
            MinerSkills,
            FirstSeed
        );

        Assert.True(GoalPlanRules.MatchesCurrent(plan, SkillKinds.Mine));
        Assert.False(GoalPlanRules.MatchesCurrent(plan, SkillKinds.Tavern));
        Assert.True(GoalPlanRules.IsInterruptSkill(SkillKinds.Flee));
        Assert.True(GoalPlanRules.IsInterruptSkill(SkillKinds.Heal));
        Assert.False(GoalPlanRules.IsInterruptSkill(SkillKinds.Mine));
    }

    [Fact]
    public void Start_BankJob_WalksDoesTheThingStaysThenPractises()
    {
        var plan = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Bank), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(JobRules.BankPlan, plan.Id);
        Assert.Equal(
            [
                SkillKinds.GoHome, SkillKinds.BankDeposit, SkillKinds.BankShop, SkillKinds.BankCrowd,
                SkillKinds.Arrive, SkillKinds.Practice
            ],
            SkillKindsOf(plan)
        );
    }

    [Fact]
    public void Start_TravelJob_TripThenArrivalThenTheTownsOffer()
    {
        var plan = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Travel), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(JobRules.TravelPlan, plan.Id);
        Assert.Equal(SkillKinds.Travel, plan.Steps[0].SkillKind);
        Assert.Equal(SkillKinds.Arrive, plan.Steps[1].SkillKind);
    }

    [Fact]
    public void Restore_JobPlan_KeepsItsSteps()
    {
        var plan = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Tavern), HealthyWork(), LeisureSkills, FirstSeed);
        var restored = GoalPlanRules.Restore(plan.Id, 2, 1);

        Assert.Equal(SkillKindsOf(plan), SkillKindsOf(restored));
        Assert.Equal(2, restored.Index);
        Assert.Equal(1, restored.StepFailures);
    }

    [Fact]
    public void ContinueOrStart_NewJob_ReplacesTheOldJobPlan()
    {
        var bank = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Bank), HealthyWork(), LeisureSkills, FirstSeed);
        var next = GoalPlanRules.ContinueOrStart(bank, JobRules.GoalFor(JobKind.Shop), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(JobRules.ShopPlan, next.Id);
    }

    [Fact]
    public void ContinueOrStart_SameJob_KeepsItsPlace()
    {
        var arrive = BankStepIndex(SkillKinds.Arrive);
        var bank = GoalPlanRules.Restore(JobRules.BankPlan, arrive, 0);
        var next = GoalPlanRules.ContinueOrStart(bank, JobRules.GoalFor(JobKind.Bank), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(JobRules.BankPlan, next.Id);
        Assert.Equal(arrive, next.Index);
    }

    [Fact]
    public void PracticeStep_TakesAnyPracticeSkillAndMovesOn()
    {
        var bank = GoalPlanRules.Restore(JobRules.BankPlan, 0, 0);
        var plan = GoalPlanRules.Restore(JobRules.BankPlan, bank.Steps.Count - 1, 0);

        Assert.True(GoalPlanRules.MatchesCurrent(plan, SkillKinds.Tactics));
        Assert.True(GoalPlanRules.MatchesCurrent(plan, SkillKinds.Anatomy));
        Assert.False(GoalPlanRules.MatchesCurrent(plan, SkillKinds.Mine));
        Assert.True(GoalPlanRules.AfterOutcome(plan, SkillKinds.Tactics, success: true).IsComplete);
    }

    [Fact]
    public void AfterOutcome_WalkHomeStep_MovesOnWhenItEnds()
    {
        var plan = GoalPlanRules.Restore(JobRules.BankPlan, 0, 0);

        Assert.Equal(SkillKinds.BankDeposit, GoalPlanRules.AfterOutcome(plan, SkillKinds.GoHome, success: true).CurrentSkill);
    }

    [Fact]
    public void SkipUnavailable_FarFromHome_StillSkipsWhatCannotRun()
    {
        // The leash held every plan in place far from home, so a traveller could do nothing
        // in the town it went to see.
        var plan = GoalPlanRules.Restore(JobRules.TravelPlan, 0, 0);
        var skipped = GoalPlanRules.SkipUnavailable(
            plan,
            HealthyWork() with { BeyondLeash = true, InTownRegion = true },
            skill => skill != SkillKinds.Travel
        );

        Assert.Equal(SkillKinds.Arrive, skipped.CurrentSkill);
    }

    [Fact]
    public void SkipUnavailable_OutOfTown_WalksToTownOnlyWithAnErrand()
    {
        // A woodcutter with an empty pack walked to town for a bank visit that deposited
        // nothing and a shopping trip with nothing to buy.
        var sell = GoalPlanRules.Restore(GoalPlanRules.WoodWork, 1, 0);
        var empty = HealthyWork() with { InTownRegion = false, NothingToBank = true };

        Assert.Equal(SkillKinds.VendorSell, sell.CurrentSkill);
        Assert.True(GoalPlanRules.SkipUnavailable(sell, empty, _ => false).IsComplete);
        Assert.Equal(
            SkillKinds.VendorSell,
            GoalPlanRules.SkipUnavailable(sell, empty with { HasHarvestGoods = true }, _ => false).CurrentSkill
        );
        Assert.Equal(
            SkillKinds.VendorBuy,
            GoalPlanRules.SkipUnavailable(sell, empty with { HasShoppingErrand = true }, _ => false).CurrentSkill
        );
    }

    [Fact]
    public void ContinueOrStart_TradeWithGoodsNoShopInReachBuys_SkipsTheSale()
    {
        // Goods count only when a live shop in reach buys them, so a trade plan made for
        // goods nobody near buys starts at the bank, not at "smelt and sell": that step
        // failed 153 times in one run.
        var noBuyer = HealthyWork() with { InTownRegion = true };
        var plan = GoalPlanRules.ContinueOrStart(null, new Goal(GoalKind.Trade, null), noBuyer, MinerSkills, FirstSeed);
        var withBuyer = GoalPlanRules.ContinueOrStart(
            null,
            new Goal(GoalKind.Trade, null),
            noBuyer with { HasLootGoods = true },
            MinerSkills,
            FirstSeed
        );

        Assert.NotEqual(SkillKinds.VendorSell, plan.CurrentSkill);
        Assert.Equal(SkillKinds.VendorSell, withBuyer.CurrentSkill);
    }

    [Fact]
    public void WoodAndFishWork_BuyTheirToolLast()
    {
        Assert.Equal(SkillKinds.VendorBuy, SkillKindsOf(GoalPlanRules.Restore(GoalPlanRules.WoodWork, 0, 0))[^1]);
        Assert.Equal(SkillKinds.VendorBuy, SkillKindsOf(GoalPlanRules.Restore(GoalPlanRules.FishWork, 0, 0))[^1]);
    }

    [Theory]
    [InlineData(JobKind.Shop)]
    [InlineData(JobKind.Tavern)]
    [InlineData(JobKind.Idle)]
    public void TownJobs_DoNotStandAboutAfterTheirWork(JobKind job)
    {
        // Nine hundred "stay a while" steps in half an hour: a shop visit ended in minutes of
        // standing at the counter. A traveller and a bank visitor still stay a while.
        var plan = GoalPlanRules.Start(JobRules.GoalFor(job), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.DoesNotContain(SkillKinds.Arrive, SkillKindsOf(plan));
    }

    [Fact]
    public void ContinueOrStart_BankCrowdClosed_SkipsToStandingAbout()
    {
        var crowdStep = BankStepIndex(SkillKinds.BankCrowd);
        var plan = GoalPlanRules.Restore(JobRules.BankPlan, crowdStep, 0);
        var closed = GoalPlanRules.ContinueOrStart(plan, JobRules.GoalFor(JobKind.Bank), HealthyWork(), LeisureSkills, FirstSeed);
        var open = GoalPlanRules.ContinueOrStart(
            plan,
            JobRules.GoalFor(JobKind.Bank),
            HealthyWork() with { BankCrowdOpen = true },
            LeisureSkills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.Arrive, closed.CurrentSkill);
        Assert.Equal(SkillKinds.BankCrowd, open.CurrentSkill);
    }

    [Theory]
    [InlineData(SkillKinds.Tailor)]
    [InlineData(SkillKinds.Carpentry)]
    [InlineData(SkillKinds.Fletch)]
    [InlineData(SkillKinds.Tinker)]
    [InlineData(SkillKinds.Alchemy)]
    [InlineData(SkillKinds.Inscription)]
    [InlineData(SkillKinds.Cook)]
    [InlineData(SkillKinds.Smith)]
    public void Start_CraftWork_RunsThePersonsOwnTrade(string trade)
    {
        string[] skills = [trade, SkillKinds.VendorSell, SkillKinds.BankDeposit];
        var work = new Goal(GoalKind.Work, GoalRules.NoTarget);
        var plan = GoalPlanRules.Start(work, HealthyWork(), skills, FirstSeed);

        Assert.Equal(GoalPlanRules.CraftWork, plan.Id);
        Assert.Equal(skills, StepsRun(plan, work, skills));
    }

    public static TheoryData<GoalKind, string> StartedGoals() =>
        new()
        {
            { GoalKind.Work, GoalRules.NoTarget },
            { GoalKind.Work, SkillKinds.House },
            { GoalKind.Trade, GoalRules.NoTarget },
            { GoalKind.Hunt, GoalRules.NoTarget },
            { GoalKind.Dungeon, GoalRules.NoTarget },
            { GoalKind.Recover, GoalRules.NoTarget },
            { GoalKind.Leisure, GoalRules.NoTarget },
            { GoalKind.Pk, GoalRules.NoTarget },
            { JobRules.GoalOf(JobKind.Bank), JobRules.NameOf(JobKind.Bank) },
            { JobRules.GoalOf(JobKind.Travel), JobRules.NameOf(JobKind.Travel) }
        };

    [Theory]
    [MemberData(nameof(StartedGoals))]
    public void Restore_AgreesWithTheStartedPlanStepForStep(GoalKind kind, string target)
    {
        // A save keeps only the id and the index. The restore built its own copy of each
        // recipe, so the saved index named another step: VendorBuy when started, Hunt when restored.
        var goal = new Goal(kind, target);
        var plan = GoalPlanRules.Start(goal, HealthyWork(), EverySkill, FirstSeed);

        while (plan is { IsComplete: false })
        {
            var restored = GoalPlanRules.Restore(plan.Id, plan.Index, plan.StepFailures);

            Assert.Equal(SkillKindsOf(plan), SkillKindsOf(restored));
            Assert.Equal(plan.Kind, restored.Kind);
            Assert.Equal(plan.Purpose, restored.Purpose);
            Assert.Equal(plan.CurrentSkill, restored.CurrentSkill);
            Assert.Equal(plan.CurrentWhy, restored.CurrentWhy);

            plan = GoalPlanRules.AfterOutcome(plan, plan.CurrentSkill, success: true);
        }
    }

    [Fact]
    public void Restore_HuntTripMidway_ResumesTheStepItSaved()
    {
        string[] skills = [SkillKinds.VendorBuy, SkillKinds.Hunt, SkillKinds.Heal, SkillKinds.BankDeposit];
        var hunt = new Goal(GoalKind.Hunt, GoalRules.NoTarget);
        var shopping = HealthyWork() with { HasShoppingErrand = true };
        var plan = GoalPlanRules.ContinueOrStart(null, hunt, shopping, skills, FirstSeed);

        Assert.Equal(SkillKinds.VendorBuy, plan.CurrentSkill);

        var bought = GoalPlanRules.AfterOutcome(plan, SkillKinds.VendorBuy, success: true);
        var restored = GoalPlanRules.ContinueOrStart(
            GoalPlanRules.Restore(bought.Id, bought.Index, bought.StepFailures),
            hunt,
            shopping,
            skills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.Hunt, restored.CurrentSkill);
    }

    [Fact]
    public void Start_FittedRecipe_PassesOverTheStepsThePersonLacks()
    {
        string[] skills = [SkillKinds.Heal, SkillKinds.GoHome];
        var recover = new Goal(GoalKind.Recover, GoalRules.NoTarget);
        var plan = GoalPlanRules.Start(recover, HealthyWork(), skills, FirstSeed);

        Assert.Contains(SkillKinds.Meditate, SkillKindsOf(GoalPlanRules.Restore(GoalPlanRules.Recover, 0, 0)));
        Assert.Equal(skills, StepsRun(plan, recover, skills));
    }

    [Fact]
    public void Restore_CraftWork_HoldsEveryTradeAndSkipsTheOnesThePersonLacks()
    {
        var restored = GoalPlanRules.Restore(GoalPlanRules.CraftWork, 0, 0);
        var kinds = SkillKindsOf(restored);

        Assert.Contains(SkillKinds.Tailor, kinds);
        Assert.Contains(SkillKinds.Cook, kinds);
        Assert.Equal(
            SkillKinds.Tailor,
            GoalPlanRules.SkipUnavailable(restored, HealthyWork(), skill => skill == SkillKinds.Tailor).CurrentSkill
        );
    }

    [Fact]
    public void Start_Outlaw_RidesOutThenBanksRestocksAndHangsAboutTheDen()
    {
        var plan = GoalPlanRules.Start(PkGoal, Red(), RedSkills, FirstSeed, kind => kind == SkillKinds.Conflict);

        Assert.Equal(GoalPlanRules.RedConflictRun, plan.Id);
        Assert.Equal(GoalKind.Pk, plan.Kind);
        Assert.Equal(
            [
                SkillKinds.Conflict, SkillKinds.GoHome, SkillKinds.Heal, SkillKinds.Rest, SkillKinds.VendorSell,
                SkillKinds.BankDeposit, SkillKinds.UpgradeGear, SkillKinds.VendorBuy, SkillKinds.Loiter, SkillKinds.Tavern,
                SkillKinds.Practice
            ],
            SkillKindsOf(plan)
        );
        Assert.Equal(SkillKinds.GoHome, plan.Steps[GoalPlanRules.RedRideHomeStep].SkillKind);
        Assert.Equal(SkillKinds.Loiter, plan.Steps[GoalPlanRules.RedHangOutStep].SkillKind);
    }

    [Fact]
    public void Start_Outlaw_TheOutingVariesWithTheSeed()
    {
        var outings = new HashSet<string>();

        for (var seed = 0; seed < RedSeedsToTry; seed++)
        {
            var plan = GoalPlanRules.Start(PkGoal, Red(), RedSkills, seed);
            Assert.True(GoalPlanRules.IsRedRun(plan.Id), plan.Id);
            Assert.Equal(GoalPlanRules.CoreSkill(plan.Id), plan.CurrentSkill);
            outings.Add(plan.CurrentSkill);
        }

        Assert.Equal([SkillKinds.Conflict, SkillKinds.Dungeon, SkillKinds.Hunt], outings.OrderBy(kind => kind, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Start_Outlaw_HotSpotsBarred_DelvesOrFarmsInstead()
    {
        for (var seed = 0; seed < RedSeedsToTry; seed++)
        {
            var plan = GoalPlanRules.Start(PkGoal, Red(), RedSkills, seed, kind => kind != SkillKinds.Conflict);
            Assert.NotEqual(GoalPlanRules.RedConflictRun, plan.Id);
        }
    }

    [Fact]
    public void Start_Outlaw_WithoutARunCheck_OutingsItHasTheSkillFor()
    {
        for (var seed = 0; seed < RedSeedsToTry; seed++)
        {
            var plan = GoalPlanRules.Start(PkGoal, Red(), [SkillKinds.Conflict, SkillKinds.GoHome], seed);
            Assert.Equal(GoalPlanRules.RedConflictRun, plan.Id);
        }
    }

    [Fact]
    public void Start_LawfulFighterWithAPkReport_KeepsTheOneStepConflictPlan()
    {
        var blue = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Lawful,
            HasPkReport = true
        };

        var plan = GoalPlanRules.Start(PkGoal, blue, RedSkills, FirstSeed);

        Assert.Equal(GoalPlanRules.Conflict, plan.Id);
        Assert.Equal([SkillKinds.Conflict], SkillKindsOf(plan));
    }

    [Theory]
    [InlineData(GoalPlanRules.RedConflictRun, SkillKinds.Conflict)]
    [InlineData(GoalPlanRules.RedDungeonRun, SkillKinds.Dungeon)]
    [InlineData(GoalPlanRules.RedHuntRun, SkillKinds.Hunt)]
    public void Restore_RedRun_KeepsItsOutingAndItsPlace(string id, string outing)
    {
        var restored = GoalPlanRules.Restore(id, GoalPlanRules.RedHangOutStep, 1);

        Assert.Equal(outing, restored.Steps[0].SkillKind);
        Assert.Equal(outing, GoalPlanRules.CoreSkill(id));
        Assert.Equal(GoalKind.Pk, restored.Kind);
        Assert.Equal(SkillKinds.Loiter, restored.CurrentSkill);
        Assert.Equal(1, restored.StepFailures);
    }

    [Fact]
    public void AfterOutcome_RedRunOutingDone_RidesBackToTheDen()
    {
        var plan = GoalPlanRules.Restore(GoalPlanRules.RedDungeonRun, 0, 0);

        Assert.Equal(SkillKinds.GoHome, GoalPlanRules.AfterOutcome(plan, SkillKinds.Dungeon, success: true).CurrentSkill);
    }

    [Fact]
    public void AfterOutcome_RalliedOutWhileHangingAbout_PicksUpAtTheRideBack()
    {
        var tavern = GoalPlanRules.Restore(GoalPlanRules.RedHuntRun, GoalPlanRules.RedHangOutStep + 1, 0);

        var after = GoalPlanRules.AfterOutcome(tavern, SkillKinds.Conflict, success: true);

        Assert.Equal(GoalPlanRules.RedHuntRun, after.Id);
        Assert.Equal(GoalPlanRules.RedRideHomeStep, after.Index);
    }

    [Fact]
    public void AfterOutcome_RallyThatNeverLeft_StaysAtTheDen()
    {
        var tavern = GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, GoalPlanRules.RedHangOutStep + 1, 0);

        Assert.Equal(tavern.Index, GoalPlanRules.AfterOutcome(tavern, SkillKinds.Conflict, success: false).Index);
    }

    [Fact]
    public void AfterOutcome_ABlueConflictOnAHuntPlan_LeavesThePlan()
    {
        var hunt = GoalPlanRules.Restore(GoalPlanRules.HuntTrip, 3, 0);

        Assert.Equal(3, GoalPlanRules.AfterOutcome(hunt, SkillKinds.Conflict, success: true).Index);
    }

    [Fact]
    public void IsHangingOut_OnlyTheDenStepsAfterTheRestock()
    {
        var run = GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, 0, 0);

        for (var i = 0; i < run.Steps.Count; i++)
        {
            var at = GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, i, 0);
            Assert.Equal(i >= GoalPlanRules.RedHangOutStep, GoalPlanRules.IsHangingOut(at));
        }

        Assert.False(GoalPlanRules.IsHangingOut(GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, run.Steps.Count, 0)));
        Assert.False(GoalPlanRules.IsHangingOut(GoalPlanRules.Restore(JobRules.TavernPlan, 1, 0)));
        Assert.False(GoalPlanRules.IsHangingOut(null));
    }

    [Fact]
    public void ContinueOrStart_HurtRedAtTheBank_KeepsItsRun()
    {
        var banking = GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, GoalPlanRules.RedHangOutStep - 1, 0);
        var hurt = Red() with
        {
            Needs = new NeedsSnapshot { HitsFraction = RecoveryRules.RecoverBelowHitsFraction / 2 },
            HasShoppingErrand = true
        };

        var next = GoalPlanRules.ContinueOrStart(banking, new Goal(GoalKind.Recover, GoalRules.NoTarget), hurt, RedSkills, FirstSeed);

        Assert.Equal(GoalPlanRules.RedConflictRun, next.Id);
        Assert.Equal(banking.Index, next.Index);
    }

    [Fact]
    public void Start_OutlawThatLostItsKit_ReArmsAtTheDenBeforeItRidesOut()
    {
        var naked = Red() with { MustReArm = true };

        var plan = GoalPlanRules.Start(PkGoal, naked, RedSkills, FirstSeed, kind => kind == SkillKinds.Conflict);

        Assert.Equal(GoalPlanRules.RedConflictRun, plan.Id);
        Assert.Equal(GoalPlanRules.RedRideHomeStep, plan.Index);
        Assert.Equal(SkillKinds.GoHome, plan.CurrentSkill);
        Assert.Contains(SkillKinds.BankDeposit, SkillKindsOf(plan).GetRange(plan.Index, GoalPlanRules.RedHangOutStep - plan.Index));
        Assert.Contains(SkillKinds.UpgradeGear, SkillKindsOf(plan).GetRange(plan.Index, GoalPlanRules.RedHangOutStep - plan.Index));
    }

    [Fact]
    public void Start_OutlawShortOfSuppliesOrHeavyWithLoot_RestocksAtTheDenBeforeItRidesOut()
    {
        var restocking = Red() with { MustRestock = true };

        var plan = GoalPlanRules.Start(PkGoal, restocking, RedSkills, FirstSeed, kind => kind == SkillKinds.Conflict);

        Assert.True(restocking.GoesToDenFirst);
        Assert.Equal(GoalPlanRules.RedConflictRun, plan.Id);
        Assert.Equal(GoalPlanRules.RedRideHomeStep, plan.Index);
        var denSteps = SkillKindsOf(plan).GetRange(plan.Index, GoalPlanRules.RedHangOutStep - plan.Index);
        Assert.Contains(SkillKinds.VendorSell, denSteps);
        Assert.Contains(SkillKinds.BankDeposit, denSteps);
        Assert.Contains(SkillKinds.VendorBuy, denSteps);
    }

    [Fact]
    public void Start_Outing_RestocksFirstOnlyWithAShoppingErrand()
    {
        string[] skills = [SkillKinds.VendorBuy, SkillKinds.Dungeon, SkillKinds.Hunt, SkillKinds.Heal];
        var dungeon = new Goal(GoalKind.Dungeon, GoalRules.NoTarget);
        var hunt = new Goal(GoalKind.Hunt, GoalRules.NoTarget);
        var shopping = HealthyWork() with { HasShoppingErrand = true };

        Assert.Equal(SkillKinds.VendorBuy, GoalPlanRules.ContinueOrStart(null, dungeon, shopping, skills, FirstSeed).CurrentSkill);
        Assert.Equal(SkillKinds.VendorBuy, GoalPlanRules.ContinueOrStart(null, hunt, shopping, skills, FirstSeed).CurrentSkill);
        Assert.Equal(SkillKinds.Dungeon, GoalPlanRules.ContinueOrStart(null, dungeon, HealthyWork(), skills, FirstSeed).CurrentSkill);
        Assert.Equal(SkillKinds.Hunt, GoalPlanRules.ContinueOrStart(null, hunt, HealthyWork(), skills, FirstSeed).CurrentSkill);
    }

    [Fact]
    public void Start_LeaveTheDen_IsTheWalkHomeAlone()
    {
        var plan = GoalPlanRules.Start(LeaveDenGoal, HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(GoalPlanRules.LeaveDen, plan.Id);
        Assert.Equal([SkillKinds.GoHome], SkillKindsOf(plan));
    }

    [Fact]
    public void ContinueOrStart_LeaveTheDen_DropsTheTavernPlan()
    {
        var tavern = GoalPlanRules.Start(new Goal(GoalKind.Leisure, GoalRules.NoTarget), HealthyWork(), LeisureSkills, FirstSeed);

        var next = GoalPlanRules.ContinueOrStart(tavern, LeaveDenGoal, HealthyWork(), LeisureSkills, FirstSeed);
        var kept = GoalPlanRules.ContinueOrStart(next, LeaveDenGoal, HealthyWork(), LeisureSkills, FirstSeed);
        var after = GoalPlanRules.ContinueOrStart(next, new Goal(GoalKind.Leisure, GoalRules.NoTarget), HealthyWork(), LeisureSkills, FirstSeed);

        Assert.Equal(GoalPlanRules.Leisure, tavern.Id);
        Assert.Equal(GoalPlanRules.LeaveDen, next.Id);
        Assert.Same(next, kept);
        Assert.Equal(GoalPlanRules.Leisure, after.Id);
    }

    [Fact]
    public void Start_StockedOutlaw_RidesOutFirst()
    {
        var plan = GoalPlanRules.Start(PkGoal, Red(), RedSkills, FirstSeed, kind => kind == SkillKinds.Conflict);

        Assert.False(Red().GoesToDenFirst);
        Assert.Equal(0, plan.Index);
        Assert.Equal(SkillKinds.Conflict, plan.CurrentSkill);
    }

    [Fact]
    public void ContinueOrStart_RunDone_RidesOutAgain()
    {
        var done = GoalPlanRules.Restore(GoalPlanRules.RedConflictRun, GoalPlanRules.RedHangOutStep + 3, 0);

        var next = GoalPlanRules.ContinueOrStart(done, PkGoal, Red(), RedSkills, FirstSeed);

        Assert.True(GoalPlanRules.IsRedRun(next.Id));
        Assert.Equal(0, next.Index);
    }

    private static Situation Red() =>
        new()
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Outlaw
        };

    private static int BankStepIndex(string skillKind)
    {
        var steps = GoalPlanRules.Restore(JobRules.BankPlan, 0, 0).Steps;

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].SkillKind == skillKind)
            {
                return i;
            }
        }

        return -1;
    }

    private static Situation HealthyWork() =>
        new()
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits },
            Role = CharacterRole.Worker
        };

    /// <summary>The steps a plan runs when each one succeeds, with the loop's skip between steps.</summary>
    private static List<string> StepsRun(GoalPlan plan, Goal goal, IReadOnlyList<string> skills)
    {
        var run = new List<string>();

        while (plan is { IsComplete: false })
        {
            run.Add(plan.CurrentSkill);
            var next = GoalPlanRules.AfterOutcome(plan, plan.CurrentSkill, success: true);
            plan = next.IsComplete ? next : GoalPlanRules.ContinueOrStart(next, goal, situation: null, skills, FirstSeed);
        }

        return run;
    }

    private static List<string> SkillKindsOf(GoalPlan plan)
    {
        var kinds = new List<string>();

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            kinds.Add(plan.Steps[i].SkillKind);
        }

        return kinds;
    }
}
