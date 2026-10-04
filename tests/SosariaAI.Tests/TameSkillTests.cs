using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TameSkillTests
{
    /// <summary>The engine's taming timer gives up once the beast is this far off before Age of Shadows.</summary>
    private const int EngineTameRange = 6;

    [Fact]
    public void TargetRange_IsTheEngineCursorReach()
    {
        Assert.Equal(2, TameSkill.PreAosTargetRange);
        Assert.Equal(3, TameSkill.AosTargetRange);
    }

    [Fact]
    public void StayClose_KeepsInsideTheEngineTamingRange()
    {
        Assert.True(TameSkill.StayCloseTiles < EngineTameRange);
        Assert.True(TameSkill.PreAosTargetRange < TameSkill.StayCloseTiles);
    }

    [Fact]
    public void KeepsTaming_UntilTheHitsRunLow()
    {
        // A dragon mauls the tamer between tries; the tamer heals and tries again, and fights
        // only when it is losing.
        Assert.True(TameSkill.KeepsTaming(1.0));
        Assert.True(TameSkill.KeepsTaming(TameSkill.GiveUpHitsFraction));
        Assert.False(TameSkill.KeepsTaming(TameSkill.GiveUpHitsFraction - 0.01));
    }

    [Fact]
    public void Attempts_CoverADragonAtGrandmaster()
    {
        // One in eight per try: forty tries leave a grandmaster about one trip in two hundred with no dragon.
        var odds = TameRules.TameOdds(100, 93.9, 0);
        Assert.True(System.Math.Pow(1 - odds, TameSkill.MaxAttempts) < 0.01);
        Assert.True(TameSkill.HasMoreAttempts(TameSkill.MaxAttempts - 1));
        Assert.False(TameSkill.HasMoreAttempts(TameSkill.MaxAttempts));
    }

    [Fact]
    public void Trip_TriesAnotherGround_ThenStops()
    {
        Assert.True(TameSkill.MayTryAnotherGround(1));
        Assert.False(TameSkill.MayTryAnotherGround(TameSkill.MaxGroundsPerTrip));
    }

    [Fact]
    public void GroundSight_CoversTheSpawnersRoam()
    {
        Assert.Equal(HuntGround.AreaRadius, TameSkill.GroundSightTiles);
        Assert.True(TameSkill.SightTiles < TameSkill.GroundSightTiles);
        Assert.Equal(PetRules.PetScanRange, TameSkill.SightTiles);
    }

    [Fact]
    public void OsricTameRoutine_IsTheTamerTrip()
    {
        // The old routine walked to the sheep of the Britain field before it tamed anything.
        var file = CharactersFile.CreateDefault();
        var osric = file.Facets[FacetNames.Felucca].FindRoster(PersonasFile.OsricId);

        Assert.NotNull(osric);
        Assert.Equal(
            TamerLife.TameTrip().Select(step => step.Skill),
            osric.Routines[TamerLife.TameRoutineId].Select(step => step.Skill));
        Assert.Equal(SkillKinds.Hunt, osric.Routines[TamerLife.HuntRoutineId][0].Skill);
        Assert.Contains(osric.Choices, choice => choice.Routine == TamerLife.HuntRoutineId);
    }

    [Fact]
    public void PetHold_LeavesThePetsInsideTheReachOfTheNextOrder()
    {
        // A pet trails a tile or two behind; told to stay at the hold, it still hears "all follow me".
        const int FollowSlack = 2;
        Assert.True(TameSkill.PetHoldTiles + FollowSlack <= PetRules.PetScanRange);
        Assert.True(TameSkill.StayCloseTiles < TameSkill.PetHoldTiles);
    }
}
