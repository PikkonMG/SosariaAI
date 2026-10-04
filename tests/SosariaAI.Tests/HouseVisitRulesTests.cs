using Server.Multis;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class HouseVisitRulesTests
{
    private const int FirstSeed = 0;
    private const int RankSeed = 2;
    private const double HealthyHits = 1;
    private const double LightPack = 0.1;
    private const int WorkerPower = 40;

    private static readonly string[] HouseSkills = [SkillKinds.House, SkillKinds.PlayerVendor, SkillKinds.GoHome];

    [Theory]
    [InlineData(DecayLevel.LikeNew, false)]
    [InlineData(DecayLevel.Slightly, false)]
    [InlineData(DecayLevel.Somewhat, true)]
    [InlineData(DecayLevel.IDOC, true)]
    [InlineData(DecayLevel.Collapsed, false)]
    public void VisitDue_ManualRefresh_FollowsTheSign(DecayLevel level, bool due) =>
        Assert.Equal(due, HouseRules.VisitDue(DecayType.ManualRefresh, level));

    [Theory]
    [InlineData(DecayType.Ageless)]
    [InlineData(DecayType.AutoRefresh)]
    [InlineData(DecayType.Condemned)]
    public void VisitDue_NoHandRefresh_NeverDue(DecayType type) =>
        Assert.False(HouseRules.VisitDue(type, DecayLevel.Greatly));

    [Fact]
    public void Pick_HouseDue_WorksTowardHouse()
    {
        var goal = GoalRules.Pick(Owner(due: true));

        Assert.Equal(GoalKind.Work, goal.Kind);
        Assert.Equal(SkillKinds.House, goal.Target);
    }

    [Fact]
    public void Plan_HouseDue_KeepsTheHouseStep()
    {
        var plan = GoalPlanRules.ContinueOrStart(
            current: null,
            new Goal(GoalKind.Work, SkillKinds.House),
            Owner(due: true),
            HouseSkills,
            FirstSeed
        );

        Assert.Equal(SkillKinds.House, plan.CurrentSkill);
    }

    [Fact]
    public void Plan_HouseNotDue_SkipsTheHouseStep()
    {
        var plan = GoalPlanRules.ContinueOrStart(
            current: null,
            new Goal(GoalKind.Work, SkillKinds.House),
            Owner(due: false),
            HouseSkills,
            FirstSeed
        );

        Assert.NotEqual(SkillKinds.House, plan.CurrentSkill);
    }

    [Fact]
    public void Rank_HouseDue_PicksHouse()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var catalog = ActionCatalog.From(connor, catalog: null);
        var situation = Owner(due: true) with
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits, PackFillFraction = LightPack, Power = WorkerPower },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true
        };
        var goal = GoalRules.Pick(situation);
        var result = ActionScorer.Rank(catalog, situation, goal, seed: RankSeed);

        Assert.Equal(SkillKinds.House, result.Winner.SkillKind);
        Assert.Contains("house needs a visit", result.WinnerReason);
    }

    private static Situation Owner(bool due) =>
        new()
        {
            Needs = new NeedsSnapshot { HitsFraction = HealthyHits },
            Role = CharacterRole.Worker,
            HasHouse = true,
            HasVendor = true,
            HouseDue = due
        };
}
