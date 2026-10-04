using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class GoalLoopTests
{
    private const int BranRosterIndex = 5;
    private const int FighterPower = 150;
    private const int SeedsToTry = 200;
    private static readonly Goal DungeonJob = JobRules.GoalFor(JobKind.Dungeon);

    [Fact]
    public void CoreSkill_NamesTheFightOfAnOuting()
    {
        Assert.Equal(SkillKinds.Dungeon, GoalPlanRules.CoreSkill(GoalPlanRules.DungeonTrip));
        Assert.Equal(SkillKinds.Hunt, GoalPlanRules.CoreSkill(GoalPlanRules.HuntTrip));
        Assert.Null(GoalPlanRules.CoreSkill(GoalPlanRules.MinerWork));
        Assert.Null(GoalPlanRules.CoreSkill(null));
    }

    [Fact]
    public void Score_DungeonJobWithTheDelveBarred_RollsAnotherJob()
    {
        // The live log: a dungeon job whose delve could not run kept its bank step and
        // walked the person to the bank, home and round town "for Dungeon" all phase.
        var bran = Bran();
        var dungeonRolls = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var open = GoalLoop.Score(bran, Fighter(delveBarred: false), DungeonJob, seed, catalog: null);

            if (open.Goal.Kind != GoalKind.Dungeon)
            {
                continue;
            }

            dungeonRolls++;
            var barred = GoalLoop.Score(bran, Fighter(delveBarred: true), DungeonJob, seed, catalog: null);
            Assert.NotEqual(GoalKind.Dungeon, barred.Goal.Kind);
        }

        Assert.True(dungeonRolls > 0);
    }

    [Fact]
    public void Score_RedSeekingConflict_RidesOutOnARunFromTheDen()
    {
        // The live log: a red's plan was the one step "seek player conflict", so it never
        // banked, and between failed camps it browsed the shops (42 picks) and played music.
        var bran = Bran();
        var conflictRuns = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var result = GoalLoop.Score(bran, Red(outingsBarred: []), PkSeek, seed, catalog: null);

            Assert.True(GoalPlanRules.IsRedRun(result.Plan.Id), result.Plan.Id);
            Assert.Equal(GoalPlanRules.CoreSkill(result.Plan.Id), result.Plan.CurrentSkill);
            Assert.Equal(result.Plan.CurrentSkill, result.Winner.SkillKind);
            conflictRuns += result.Plan.Id == GoalPlanRules.RedConflictRun ? 1 : 0;
        }

        Assert.True(conflictRuns > SeedsToTry / 2);
    }

    [Fact]
    public void Score_RedWithTheHotSpotsCoolingDown_DelvesOrFarmsInstead()
    {
        var bran = Bran();

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var result = GoalLoop.Score(bran, Red(outingsBarred: [SkillKinds.Conflict]), PkSeek, seed, catalog: null);

            Assert.NotEqual(GoalPlanRules.RedConflictRun, result.Plan.Id);
            Assert.NotEqual(SkillKinds.Conflict, result.Winner.SkillKind);
        }
    }

    [Fact]
    public void Score_RedWithNoOutingOpen_HangsAboutTheDenInstead()
    {
        var result = GoalLoop.Score(
            Bran(),
            Red(outingsBarred: [SkillKinds.Conflict, SkillKinds.Dungeon, SkillKinds.Hunt]),
            PkSeek,
            seed: 0,
            catalog: null
        );

        Assert.True(GoalPlanRules.IsRedRun(result.Plan.Id));
        Assert.True(result.Plan.Index > 0);
        Assert.False(RedGangRunRules.IsOuting(result.Winner.SkillKind));
    }

    private static readonly Goal PkSeek = new(GoalKind.Pk, GoalRules.NoTarget);

    private static Situation Red(string[] outingsBarred) =>
        Fighter(delveBarred: false) with
        {
            Disposition = DispositionKind.Outlaw,
            BlockedSkillKinds = outingsBarred
        };

    private static CharacterDefinition Bran() =>
        CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster[BranRosterIndex];

    private static Situation Fighter(bool delveBarred) =>
        new()
        {
            Needs = new NeedsSnapshot
            {
                HitsFraction = 1,
                Power = FighterPower,
                AmbitionWantsHunt = true,
                Drives = new PersonaDrives(greed: 0.4, caution: 0.3, valor: 0.8, isCustom: true)
            },
            Location = CharactersFile.DefaultBankSpot,
            InTownRegion = true,
            Role = CharacterRole.Fighter,
            BlockedSkillKinds = delveBarred ? [SkillKinds.Dungeon, SkillKinds.Hunt] : [],
            NothingToBank = false
        };
}
