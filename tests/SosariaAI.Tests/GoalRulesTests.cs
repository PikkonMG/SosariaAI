using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class GoalRulesTests
{
    private const int SeedSweep = 200;
    private const int FixedSeed = 7;

    private static Situation HealthyWorker() =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = DayPart.Work.ToString(),
                Drives = new PersonaDrives(greed: 0.8, caution: 0.4, valor: 0.2, isCustom: true),
                AmbitionWantsWork = true
            },
            Role = CharacterRole.Worker
        };

    [Fact]
    public void Pick_Hurt_Recovers()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.2 },
            Role = CharacterRole.Fighter
        });
        Assert.Equal(GoalKind.Recover, goal.Kind);
    }

    [Fact]
    public void Pick_HitsBelowRecoverLine_Recovers()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 0.50 },
            Role = CharacterRole.Fighter
        });
        Assert.Equal(GoalKind.Recover, goal.Kind);
    }

    [Fact]
    public void Pick_BlueWithNoFightInTheDen_GoesHomeFirst()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Lawful,
            HasPkReport = true,
            LeavesDen = true
        });

        Assert.Equal(new Goal(GoalKind.Leisure, SkillKinds.GoHome), goal);
    }

    [Fact]
    public void Pick_PkHunter_HuntsRedsInMostPhases()
    {
        var hunter = new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Lawful,
            IsPkHunter = true
        };
        var hunts = 0;

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var goal = GoalRules.Pick(hunter, seed);
            Assert.Equal(PkHunterRules.HuntsThisPhase(seed), goal.Kind == GoalKind.Pk);
            hunts += goal.Kind == GoalKind.Pk ? 1 : 0;
        }

        Assert.True(hunts > SeedSweep / 2);
        Assert.True(hunts < SeedSweep);
    }

    [Fact]
    public void Pick_LawfulFighterWithNoReport_IsNoHunter()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Lawful
        }, FixedSeed);

        Assert.NotEqual(GoalKind.Pk, goal.Kind);
    }

    [Fact]
    public void Pick_PackedGoods_Trades()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, PackFillFraction = 0.9 },
            HasHarvestGoods = true,
            Role = CharacterRole.Worker
        });
        Assert.Equal(GoalKind.Trade, goal.Kind);
    }

    [Fact]
    public void Pick_Ghost_IsGhost()
    {
        var goal = GoalRules.Pick(new Situation { IsGhost = true });
        Assert.Equal(GoalKind.Ghost, goal.Kind);
    }

    [Fact]
    public void Pick_Night_PicksAJobNotBed()
    {
        // Everyone recovered at once when the shared night began. Night now tilts the job
        // dice toward town; the person's session ends its day.
        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var goal = GoalRules.Pick(
                new Situation
                {
                    Needs = new NeedsSnapshot { HitsFraction = 1, DayPart = DayPart.Night.ToString() },
                    Role = CharacterRole.Worker
                },
                seed
            );

            Assert.NotEqual(GoalKind.Recover, goal.Kind);
            Assert.True(JobRules.IsJob(goal.Target), goal.Target);
        }
    }

    [Fact]
    public void Pick_NightWithGoods_Trades()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, DayPart = DayPart.Night.ToString() },
            HasHarvestGoods = true,
            Role = CharacterRole.Worker
        });
        Assert.Equal(GoalKind.Trade, goal.Kind);
    }

    [Fact]
    public void Pick_NightCanBuyHouse_WorksTowardHouse()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1, DayPart = DayPart.Night.ToString() },
            CanBuyHouse = true,
            Role = CharacterRole.Worker
        });
        Assert.Equal(GoalKind.Work, goal.Kind);
        Assert.Equal(SkillKinds.House, goal.Target);
    }

    [Fact]
    public void Pick_NightPartyForming_Hunts()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                DayPart = DayPart.Night.ToString(),
                PartyForming = true
            },
            Role = CharacterRole.Fighter
        });
        Assert.Equal(GoalKind.Hunt, goal.Kind);
    }

    [Fact]
    public void Pick_CanBuyHouse_WorksTowardHouse()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            CanBuyHouse = true,
            Role = CharacterRole.Worker
        });
        Assert.Equal(GoalKind.Work, goal.Kind);
        Assert.Equal(SkillKinds.House, goal.Target);
    }

    [Fact]
    public void Pick_HealthyDay_PicksAJobNotRecover()
    {
        var goal = GoalRules.Pick(HealthyWorker(), FixedSeed);

        Assert.NotEqual(GoalKind.Recover, goal.Kind);
        Assert.True(JobRules.TryParse(goal.Target, out var job));
        Assert.Equal(JobRules.GoalOf(job), goal.Kind);
    }

    [Fact]
    public void Pick_SameSeed_KeepsTheJob()
    {
        Assert.Equal(GoalRules.Pick(HealthyWorker(), FixedSeed), GoalRules.Pick(HealthyWorker(), FixedSeed));
    }

    [Fact]
    public void Pick_GreedyWorker_MostlyWorks()
    {
        var work = 0;

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            if (GoalRules.Pick(HealthyWorker(), seed).Kind == GoalKind.Work)
            {
                work++;
            }
        }

        Assert.True(work > SeedSweep / 4, $"{work} of {SeedSweep}");
        Assert.True(work < SeedSweep, "a worker with a life does more than work");
    }

    [Fact]
    public void Pick_WithSkills_KeepsToJobsThePersonCanDo()
    {
        string[] skills = [SkillKinds.Tavern];

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var target = GoalRules.Pick(HealthyWorker(), seed, skills).Target;
            Assert.True(
                target == JobRules.NameOf(JobKind.Tavern) || target == JobRules.NameOf(JobKind.Idle),
                target
            );
        }
    }

    [Fact]
    public void Pick_MustFlee_IsFlee()
    {
        var goal = GoalRules.Pick(new Situation { MustFlee = true });
        Assert.Equal(GoalKind.Flee, goal.Kind);
    }

    [Fact]
    public void Pick_Outlaw_SeeksPlayerConflict()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Outlaw
        });

        Assert.Equal(GoalKind.Pk, goal.Kind);
    }

    [Fact]
    public void Pick_LawfulFighterWithPkReport_SeeksConflict()
    {
        var goal = GoalRules.Pick(new Situation
        {
            Needs = new NeedsSnapshot { HitsFraction = 1 },
            Role = CharacterRole.Fighter,
            Disposition = DispositionKind.Lawful,
            HasPkReport = true
        });

        Assert.Equal(GoalKind.Pk, goal.Kind);
    }
}
