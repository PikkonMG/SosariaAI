using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class RedGangRunRulesTests
{
    private const int SeedsToTry = 400;
    private const double Healthy = 1;
    private const double Hurt = 0.3;
    private static readonly DateTime Now = new(2026, 9, 27, 1, 0, 0);
    private static readonly DateTime RunEnds = Now.AddMinutes(10);

    [Fact]
    public void PickOuting_MostRunsWorkTheHotSpots_TheRestDelveOrFarm()
    {
        var picks = Tally(_ => true);

        Assert.True(picks[SkillKinds.Conflict] > picks[SkillKinds.Dungeon]);
        Assert.True(picks[SkillKinds.Conflict] > picks[SkillKinds.Hunt]);
        Assert.True(picks[SkillKinds.Dungeon] > 0);
        Assert.True(picks[SkillKinds.Hunt] > 0);
    }

    [Fact]
    public void PickOuting_TheSameSeed_TheSameRun() =>
        Assert.Equal(RedGangRunRules.PickOuting(7, _ => true), RedGangRunRules.PickOuting(7, _ => true));

    [Fact]
    public void PickOuting_AnOutingThatCannotRun_IsNeverPicked()
    {
        var picks = Tally(kind => kind != SkillKinds.Conflict);

        Assert.Equal(0, picks[SkillKinds.Conflict]);
        Assert.Equal(SeedsToTry, picks[SkillKinds.Dungeon] + picks[SkillKinds.Hunt]);
    }

    [Fact]
    public void PickOuting_NothingCanRun_TheHotSpotRunWaitsInThePlan()
    {
        Assert.Equal(SkillKinds.Conflict, RedGangRunRules.PickOuting(3, _ => false));
        Assert.Equal(SkillKinds.Conflict, RedGangRunRules.PickOuting(3, null));
    }

    [Theory]
    [InlineData(SkillKinds.Conflict, true)]
    [InlineData(SkillKinds.Dungeon, true)]
    [InlineData(SkillKinds.Hunt, true)]
    [InlineData(SkillKinds.Tavern, false)]
    [InlineData(null, false)]
    public void IsOuting_TheHotSpotTheDungeonAndTheFarm(string skillKind, bool outing) =>
        Assert.Equal(outing, RedGangRunRules.IsOuting(skillKind));

    [Fact]
    public void WhyHeadHome_AHeavyPack_GoesToTheBank() =>
        Assert.Equal(
            HuntEndReason.PackFull,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, packFull: true, Healthy, lowHitsCount: 0, suppliesLow: false, runsOff: 0)
        );

    [Fact]
    public void WhyHeadHome_OutOfReagentsOrBandages_GoesToRestock() =>
        Assert.Equal(
            HuntEndReason.SuppliesLow,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, packFull: false, Healthy, lowHitsCount: 0, suppliesLow: true, runsOff: 0)
        );

    [Fact]
    public void WhyHeadHome_HurtAgainAndAgain_GoesToHeal()
    {
        Assert.Equal(
            HuntEndReason.Hurt,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, false, Hurt, SosariaCombat.HuntLowHitsLimit, false, 0)
        );
        Assert.Equal(
            HuntEndReason.None,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, false, Hurt, SosariaCombat.HuntLowHitsLimit - 1, false, 0)
        );
    }

    [Fact]
    public void WhyHeadHome_TheRunTimeIsUp_GoesHome()
    {
        Assert.Equal(HuntEndReason.TimeUp, RedGangRunRules.WhyHeadHome(RunEnds, RunEnds, false, Healthy, 0, false, 0));
        Assert.Equal(HuntEndReason.None, RedGangRunRules.WhyHeadHome(Now, RunEnds, false, Healthy, 0, false, 0));
    }

    [Fact]
    public void WhyHeadHome_RunOffTheSpotAgainAndAgain_GivesItUp()
    {
        Assert.Equal(
            HuntEndReason.Hurt,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, false, Healthy, 0, false, RedGangRunRules.RunsOffLimit)
        );
        Assert.Equal(
            HuntEndReason.None,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, false, Healthy, 0, false, RedGangRunRules.RunsOffLimit - 1)
        );
        Assert.Equal(
            HuntEndReason.PackFull,
            RedGangRunRules.WhyHeadHome(Now, RunEnds, true, Healthy, 0, false, RedGangRunRules.RunsOffLimit)
        );
    }

    [Fact]
    public void WhyNotSetOut_AHeavyPackOrShortSuppliesKeepTheRedFromTheSpot()
    {
        Assert.Equal(HuntEndReason.PackFull, RedGangRunRules.WhyNotSetOut(packFull: true, suppliesLow: false));
        Assert.Equal(HuntEndReason.SuppliesLow, RedGangRunRules.WhyNotSetOut(packFull: false, suppliesLow: true));
        Assert.Equal(HuntEndReason.None, RedGangRunRules.WhyNotSetOut(packFull: false, suppliesLow: false));
    }

    [Fact]
    public void WhyHeadHome_NeverEndsForWantOfPrey() =>
        Assert.Equal(
            HuntEndReason.None,
            RedGangRunRules.WhyHeadHome(Now.AddHours(1), default, false, Healthy, 0, false, 0)
        );

    [Fact]
    public void HeadHomeBelowHits_IsWhereARedStopsToRecover() =>
        Assert.Equal(RecoveryRules.RecoverBelowHitsFraction, RedGangRunRules.HeadHomeBelowHitsFraction);

    [Theory]
    [InlineData(HuntEndReason.PackFull)]
    [InlineData(HuntEndReason.SuppliesLow)]
    [InlineData(HuntEndReason.Hurt)]
    [InlineData(HuntEndReason.TimeUp)]
    public void Because_SaysWhyTheRunEnded(HuntEndReason reason) =>
        Assert.False(string.IsNullOrWhiteSpace(RedGangRunRules.Because(reason)));

    [Fact]
    public void Because_EachReasonItsOwnWords()
    {
        var said = new HashSet<string>
        {
            RedGangRunRules.Because(HuntEndReason.PackFull),
            RedGangRunRules.Because(HuntEndReason.SuppliesLow),
            RedGangRunRules.Because(HuntEndReason.Hurt),
            RedGangRunRules.Because(HuntEndReason.TimeUp)
        };

        Assert.Equal(4, said.Count);
    }

    private static Dictionary<string, int> Tally(Func<string, bool> canRun)
    {
        var picks = new Dictionary<string, int>
        {
            [SkillKinds.Conflict] = 0,
            [SkillKinds.Dungeon] = 0,
            [SkillKinds.Hunt] = 0
        };

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            picks[RedGangRunRules.PickOuting(seed, canRun)]++;
        }

        return picks;
    }
}
