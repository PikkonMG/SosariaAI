using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A crafter's job roll, scorer and plan put its trade first.</summary>
public class CrafterWorkTests
{
    private const int SeedSweep = 400;
    private const double MainWorkShare = 0.5;
    private const double CrafterPhaseShare = 0.75;
    private const int Seed = 11;
    private const double EvenDrive = 0.5;

    private static readonly string[] CrafterSkills =
    [
        SkillKinds.Smith, SkillKinds.Mine, SkillKinds.Cook, SkillKinds.VendorSell, SkillKinds.BankDeposit,
        SkillKinds.BankShop, SkillKinds.Tavern, SkillKinds.VendorBuy, SkillKinds.Travel
    ];

    [Fact]
    public void Weight_ACrafterLeansOnItsTrade()
    {
        var gatherer = Worker(DayPart.Work, trade: null);
        var smith = Worker(DayPart.Work, SkillKinds.Smith);

        Assert.Equal(
            JobRules.Weight(JobKind.Craft, gatherer) * JobRules.CrafterCraftBoost,
            JobRules.Weight(JobKind.Craft, smith),
            precision: 6
        );
        Assert.Equal(JobRules.Weight(JobKind.Bank, gatherer), JobRules.Weight(JobKind.Bank, smith), precision: 6);
    }

    [Fact]
    public void Weight_TheSmithyStaysOpenAtNight()
    {
        Assert.Equal(
            JobRules.Weight(JobKind.Craft, Worker(DayPart.Work, SkillKinds.Smith)),
            JobRules.Weight(JobKind.Craft, Worker(DayPart.Night, SkillKinds.Smith)),
            precision: 6
        );
        Assert.True(
            JobRules.Weight(JobKind.Craft, Worker(DayPart.Night, trade: null)) <
            JobRules.Weight(JobKind.Craft, Worker(DayPart.Work, trade: null))
        );
    }

    [Fact]
    public void Roll_CraftIsACraftersMainWork()
    {
        var smith = Worker(DayPart.Work, SkillKinds.Smith);
        var crafts = Enumerable.Range(0, SeedSweep).Count(seed => JobRules.Roll(smith, seed, CrafterSkills) == JobKind.Craft);

        Assert.True(crafts > SeedSweep * MainWorkShare, $"{crafts} of {SeedSweep} rolls were craft");
    }

    [Fact]
    public void Roll_ACrafterHoldsItsTradeMostPhases_EvenInTheEvening()
    {
        foreach (var part in new[] { DayPart.Work, DayPart.Evening, DayPart.Night })
        {
            var smith = Worker(part, SkillKinds.Smith);
            var crafts = Enumerable.Range(0, SeedSweep).Count(seed => JobRules.Roll(smith, seed, CrafterSkills) == JobKind.Craft);

            Assert.True(crafts >= SeedSweep * CrafterPhaseShare, $"{crafts} of {SeedSweep} {part} rolls were craft");
        }
    }

    [Fact]
    public void Rank_TheOwnTradeGetsTheCrafterBoost()
    {
        ActionCandidate[] candidates =
        [
            new() { Id = new ActionId("craft:Smith:0"), SkillKind = SkillKinds.Smith, RoutineId = "craft", Step = new SkillStepDefinition { Skill = SkillKinds.Smith } },
            new() { Id = new ActionId("town:IdleWander:0"), SkillKind = SkillKinds.IdleWander, RoutineId = "town", Step = new SkillStepDefinition { Skill = SkillKinds.IdleWander } }
        ];
        var goal = JobRules.GoalFor(JobKind.Craft);

        var plain = ActionScorer.Rank(candidates, Worker(DayPart.Work, trade: null), goal, Seed);
        var smith = ActionScorer.Rank(candidates, Worker(DayPart.Work, SkillKinds.Smith), goal, Seed);

        Assert.Equal(
            plain.Ranked.First(action => action.SkillKind == SkillKinds.Smith).Score + ActionScorer.CrafterTradeBoost,
            smith.Ranked.First(action => action.SkillKind == SkillKinds.Smith).Score,
            precision: 6
        );
        Assert.Equal(SkillKinds.Smith, smith.Winner.SkillKind);
    }

    [Fact]
    public void Start_ACrafterWorksItsTrade_ItsOwnOreFirst_NotTheHearth()
    {
        var situation = Worker(DayPart.Work, SkillKinds.Smith);
        var plan = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Craft), situation, CrafterSkills, Seed);

        Assert.Equal(GoalPlanRules.TradeWork, plan.Id);
        Assert.Equal(SkillKinds.Mine, plan.CurrentSkill);
        Assert.Equal(
            [SkillKinds.Mine, SkillKinds.Smith, SkillKinds.VendorSell, SkillKinds.BankDeposit],
            plan.Steps.Select(step => step.SkillKind).Where(kind => CrafterSkills.Contains(kind))
        );
        Assert.True(JobRules.PlanServes(JobKind.Craft, plan.Id));
        Assert.DoesNotContain(plan.Steps, step => step.SkillKind == SkillKinds.Cook);

        // Ingots in reach: the dig is not ready, and the smith goes straight to the forge.
        Assert.Equal(
            SkillKinds.Smith,
            GoalPlanRules.SkipUnavailable(plan, situation, kind => kind != SkillKinds.Mine && CrafterSkills.Contains(kind)).CurrentSkill
        );
        Assert.Equal(SkillKinds.Mine, GoalPlanRules.SkipUnavailable(plan, situation, _ => true).CurrentSkill);
    }

    [Fact]
    public void Rank_TheCraftersOwnOreGetsTheCrafterBoost()
    {
        ActionCandidate[] candidates =
        [
            new() { Id = new ActionId("craft:Mine:1"), SkillKind = SkillKinds.Mine, RoutineId = "craft", Step = new SkillStepDefinition { Skill = SkillKinds.Mine } },
            new() { Id = new ActionId("town:IdleWander:0"), SkillKind = SkillKinds.IdleWander, RoutineId = "town", Step = new SkillStepDefinition { Skill = SkillKinds.IdleWander } }
        ];
        var goal = JobRules.GoalFor(JobKind.Craft);

        var plain = ActionScorer.Rank(candidates, Worker(DayPart.Work, trade: null), goal, Seed);
        var smith = ActionScorer.Rank(candidates, Worker(DayPart.Work, SkillKinds.Smith), goal, Seed);
        var tailor = ActionScorer.Rank(candidates, Worker(DayPart.Work, SkillKinds.Tailor), goal, Seed);

        Assert.Equal(
            plain.Ranked.First(action => action.SkillKind == SkillKinds.Mine).Score + ActionScorer.CrafterTradeBoost,
            smith.Ranked.First(action => action.SkillKind == SkillKinds.Mine).Score,
            precision: 6
        );
        Assert.Equal(
            plain.Ranked.First(action => action.SkillKind == SkillKinds.Mine).Score,
            tailor.Ranked.First(action => action.SkillKind == SkillKinds.Mine).Score,
            precision: 6
        );
    }

    [Fact]
    public void Start_AGathererStillPicksItsOwnWork()
    {
        var plan = GoalPlanRules.Start(JobRules.GoalFor(JobKind.Craft), Worker(DayPart.Work, trade: null), CrafterSkills, Seed);

        Assert.NotEqual(GoalPlanRules.TradeWork, plan.Id);
    }

    private static Situation Worker(DayPart part, string trade, int openOrders = 0) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = part.ToString(),
                Drives = new PersonaDrives(greed: EvenDrive, caution: EvenDrive, valor: EvenDrive, isCustom: true)
            },
            Role = CharacterRole.Worker,
            InTownRegion = true,
            BankCrowdOpen = true,
            Tendencies = PersonProfileRules.RollTendencies("smith", PersonClass.Smith, PersonTrait.None),
            CraftTrade = trade,
            OpenOrders = openOrders
        };

    [Fact]
    public void Weight_ACrafterWithOrdersStaysAndWorks()
    {
        var free = Worker(DayPart.Work, SkillKinds.Smith);
        var busy = Worker(DayPart.Work, SkillKinds.Smith, openOrders: 1);

        Assert.Equal(JobRules.Weight(JobKind.Craft, free) * JobRules.OrderCraftBoost, JobRules.Weight(JobKind.Craft, busy), precision: 6);
        Assert.Equal(0, JobRules.Weight(JobKind.Travel, busy));
        Assert.Equal(0, JobRules.Weight(JobKind.Hunt, busy));
        Assert.Equal(JobRules.Weight(JobKind.Bank, free), JobRules.Weight(JobKind.Bank, busy), precision: 6);
    }
}
