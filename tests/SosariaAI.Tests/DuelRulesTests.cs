using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class DuelRulesTests
{
    private const int HitsMax = 100;
    private const int NoGuild = -1;
    private const int Militia = 0;
    private const int WestBank = 1;
    private const int EvenTiers = 0;
    private const uint ChallengerSerial = 0x10;
    private const uint PartnerSerial = 0x11;
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MayChallenge_NeedsHealthAndNoRivalGuilds()
    {
        Assert.True(DuelRules.MayChallenge(1.0, 1.0, NoGuild, NoGuild, NoGuild, EvenTiers, false));
        Assert.True(DuelRules.MayChallenge(1.0, 1.0, Militia, Militia, NoGuild, EvenTiers, false));
        Assert.True(DuelRules.MayChallenge(1.0, 1.0, Militia, NoGuild, NoGuild, EvenTiers, false));
        Assert.False(DuelRules.MayChallenge(1.0, 1.0, Militia, WestBank, NoGuild, EvenTiers, false));
        Assert.False(DuelRules.MayChallenge(0.5, 1.0, NoGuild, NoGuild, NoGuild, EvenTiers, false));
    }

    [Fact]
    public void MayChallenge_EvenMatchesOnly_NeverSwornFoes()
    {
        Assert.True(DuelRules.MayChallenge(1.0, 1.0, NoGuild, NoGuild, NoGuild, DuelRules.MaxTierGap, false));
        Assert.True(DuelRules.MayChallenge(1.0, 1.0, NoGuild, NoGuild, NoGuild, -DuelRules.MaxTierGap, false));
        Assert.False(DuelRules.MayChallenge(1.0, 1.0, NoGuild, NoGuild, NoGuild, DuelRules.MaxTierGap + 1, false));
        Assert.False(DuelRules.MayChallenge(1.0, 1.0, NoGuild, NoGuild, NoGuild, EvenTiers, swornFoes: true));
    }

    [Fact]
    public void MayStartAnother_OnlyUnderTheCap()
    {
        Assert.True(DuelRules.MayStartAnother(0));
        Assert.True(DuelRules.MayStartAnother(DuelRules.MaxActive - 1));
        Assert.False(DuelRules.MayStartAnother(DuelRules.MaxActive));
    }

    [Fact]
    public void OnPoints_TheLessHurtWins_TieToTheChallenger()
    {
        Assert.True(DuelRules.ChallengerLosesOnPoints(0.4, 0.6));
        Assert.False(DuelRules.ChallengerLosesOnPoints(0.6, 0.4));
        Assert.False(DuelRules.ChallengerLosesOnPoints(0.5, 0.5));
    }

    [Fact]
    public void AttemptGap_IsMinutesApart() =>
        Assert.True(DuelRules.AttemptGapMin > TimeSpan.Zero && DuelRules.AttemptGapMax > DuelRules.AttemptGapMin);

    [Fact]
    public void Floor_StopsTheDuelInsteadOfTheDuelist()
    {
        var floor = DuelRules.StopHits(HitsMax);

        Assert.Equal((int)(HitsMax * DuelRules.StopHitsFraction), floor);
        Assert.False(DuelRules.EndsDuel(HitsMax, HitsMax, 10));
        Assert.True(DuelRules.EndsDuel(floor + 5, HitsMax, 10));
        Assert.Equal(5, DuelRules.CappedDamage(floor + 5, HitsMax, 10));
        Assert.Equal(0, DuelRules.CappedDamage(floor, HitsMax, 10));
        Assert.Equal(10, DuelRules.CappedDamage(HitsMax, HitsMax, 10));
    }

    [Fact]
    public void Floor_NeverZero() => Assert.Equal(1, DuelRules.StopHits(1));

    [Fact]
    public void TimedOut_ByStage()
    {
        Assert.False(DuelRules.TimedOut(DuelStage.Walking, Now, Now + DuelRules.WalkLimit - TimeSpan.FromSeconds(1)));
        Assert.True(DuelRules.TimedOut(DuelStage.Walking, Now, Now + DuelRules.WalkLimit));
        Assert.True(DuelRules.TimedOut(DuelStage.Fighting, Now, Now + DuelRules.FightLimit));
        Assert.False(DuelRules.TimedOut(DuelStage.Over, Now, Now + DuelRules.FightLimit));
    }

    [Fact]
    public void Rested_AfterTheRest()
    {
        Assert.True(DuelRules.Rested(default, Now));
        Assert.False(DuelRules.Rested(Now, Now + DuelRules.Rest - TimeSpan.FromMinutes(1)));
        Assert.True(DuelRules.Rested(Now, Now + DuelRules.Rest));
    }

    [Fact]
    public void Lines_ChallengeNamesTheOther_CloseSaysGf()
    {
        Assert.Contains("Mina", Talk.Line(TalkCategory.DuelChallenge, 0, new TalkSlots { Name = "Mina" }));
        Assert.Equal("gl", Talk.Line(TalkCategory.DuelAccept, 0, default));
        Assert.Equal("gf", Talk.Line(TalkCategory.DuelWin, 0, default));
        Assert.StartsWith("gf", Talk.Line(TalkCategory.DuelLoss, 0, default));
    }

    [Fact]
    public void DuelSkill_CountsAsAHuntOnlyWhileTheBlowsFly()
    {
        var duel = new Duel((Serial)ChallengerSerial, (Serial)PartnerSerial, Point3D.Zero, Now);
        var skill = new DuelSkill(duel);

        Assert.IsAssignableFrom<IHuntingSkill>(skill);
        Assert.False(skill.IsHunting);

        duel.BeginFight(Now);
        Assert.True(skill.IsHunting);

        duel.Finish((Serial)PartnerSerial, Now);
        Assert.False(skill.IsHunting);
    }
}
