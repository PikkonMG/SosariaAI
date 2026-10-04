using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Common;
using Xunit;

namespace SosariaAI.Tests;

public class FocusRulesTests
{
    private const int Near = 2;
    private const int Far = 9;
    private const int Middle = 5;
    private const double Whole = Vitals.FullHits;
    private const int JustNearer = 4;
    private const double HalfHits = 0.5;
    private const int Alone = 0;
    private const int InAPack = 3;
    private const int EvilKarma = -100;
    private const int GoodKarma = 100;

    [Fact]
    public void Tier_RedOutranksEveryMonster()
    {
        Assert.Equal(FocusRules.OutlawTier, FocusRules.Tier(true, true, false, false, false, false));
        Assert.Equal(FocusRules.AttackerTier, FocusRules.Tier(false, false, false, false, true, false));
        Assert.Equal(FocusRules.PlainTier, FocusRules.Tier(false, false, false, false, false, false));
    }

    [Fact]
    public void Tier_OneOnAFriendRanksJustUnderOneOnMe()
    {
        Assert.Equal(FocusRules.FriendAttackerTier, FocusRules.Tier(false, false, false, true, false, true));
        Assert.Equal(FocusRules.AttackerTier, FocusRules.Tier(false, false, false, true, true, true));
        Assert.True(FocusRules.AttackerTier > FocusRules.FriendAttackerTier);
        Assert.True(FocusRules.FriendAttackerTier > FocusRules.PlainTier);
        Assert.True(FocusRules.OutlawTier > FocusRules.AttackerTier);
    }

    [Fact]
    public void Registers_PastSight_OnlyAFoeInAFightWithMeOrAFriend()
    {
        const int Sight = 10;
        const int Past = 13;

        Assert.True(FocusRules.Registers(Sight, Sight, attacksSelf: false, attacksFriend: false));
        Assert.False(FocusRules.Registers(Past, Sight, attacksSelf: false, attacksFriend: false));
        Assert.True(FocusRules.Registers(Past, Sight, attacksSelf: true, attacksFriend: false));
        Assert.True(FocusRules.Registers(Past, Sight, attacksSelf: false, attacksFriend: true));
    }

    [Fact]
    public void Tier_GrayCountsOnlyWhileAttackingPeople()
    {
        Assert.Equal(FocusRules.OutlawTier, FocusRules.Tier(true, false, true, true, false, false));
        Assert.Equal(FocusRules.PlainTier, FocusRules.Tier(true, false, true, false, false, false));
    }

    [Fact]
    public void Score_FarRedBeatsNearMonsterOnMe() =>
        Assert.True(
            FocusRules.Score(FocusRules.OutlawTier, Far, Whole, Alone) >
            FocusRules.Score(FocusRules.AttackerTier, Near, HalfHits, Alone)
        );

    [Fact]
    public void Score_InARank_NearHurtAndAloneWin()
    {
        var baseline = FocusRules.Score(FocusRules.PlainTier, Middle, Whole, Alone);

        Assert.True(FocusRules.Score(FocusRules.PlainTier, Near, Whole, Alone) > baseline);
        Assert.True(FocusRules.Score(FocusRules.PlainTier, Middle, HalfHits, Alone) > baseline);
        Assert.True(FocusRules.Score(FocusRules.PlainTier, Middle, Whole, InAPack) < baseline);
    }

    [Fact]
    public void IsolationScore_FewestNeighboursFirst()
    {
        Assert.True(FocusRules.IsolationScore(Alone, Far) > FocusRules.IsolationScore(InAPack, Near));
        Assert.True(FocusRules.IsolationScore(Alone, Near) > FocusRules.IsolationScore(Alone, Far));
    }

    [Fact]
    public void PassesFightMode_FollowsModernUoMeaning()
    {
        Assert.False(FocusRules.PassesFightMode(FightMode.None, true, EvilKarma));
        Assert.True(FocusRules.PassesFightMode(FightMode.Aggressor, true, GoodKarma));
        Assert.False(FocusRules.PassesFightMode(FightMode.Aggressor, false, EvilKarma));
        Assert.True(FocusRules.PassesFightMode(FightMode.Evil, false, EvilKarma));
        Assert.False(FocusRules.PassesFightMode(FightMode.Evil, false, GoodKarma));
        Assert.True(FocusRules.PassesFightMode(FightMode.Closest, false, GoodKarma));
    }

    [Fact]
    public void ShouldSwitch_HigherRankAlwaysTakesTheFight()
    {
        Assert.True(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Near, true, Whole, FocusRules.OutlawTier, Far, false));
        Assert.False(FocusRules.ShouldSwitch(FocusRules.OutlawTier, Far, false, Whole, FocusRules.AttackerTier, Near, true));
    }

    [Fact]
    public void ShouldSwitch_ANearlyBeatenFoeIsFinished_UnlessAnAttackerStandsAtArmsReach()
    {
        const double NearlyBeaten = FocusRules.FinishHitsFraction - 0.01;

        Assert.False(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Far, false, NearlyBeaten, FocusRules.OutlawTier, Middle, true));
        Assert.False(FocusRules.ShouldSwitch(FocusRules.PlainTier, Far, false, NearlyBeaten, FocusRules.AttackerTier, Near, true));
        Assert.True(FocusRules.ShouldSwitch(FocusRules.PlainTier, Far, false, NearlyBeaten, FocusRules.AttackerTier, FocusRules.ArmsReachTiles, true));
        Assert.False(FocusRules.ShouldSwitch(FocusRules.PlainTier, Far, false, NearlyBeaten, FocusRules.OutlawTier, FocusRules.ArmsReachTiles, false));
    }

    [Fact]
    public void ClearlyNearer_AFoeHittingBackNeedsTheWiderMargin()
    {
        Assert.True(FocusRules.ClearlyNearer(Near, Near + FocusRules.SwitchSlackTiles, currentAttacksSelf: false));
        Assert.False(FocusRules.ClearlyNearer(Near, Near + FocusRules.SwitchSlackTiles, currentAttacksSelf: true));
        Assert.True(FocusRules.ClearlyNearer(Near, Near + FocusRules.ClearSwitchTiles, currentAttacksSelf: true));
        Assert.False(FocusRules.ClearlyNearer(Middle, Middle, currentAttacksSelf: false));
    }

    [Fact]
    public void ShouldSwitch_SameRank_OnlyAMuchNearerAttacker()
    {
        Assert.False(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Middle, true, Whole, FocusRules.AttackerTier, JustNearer, true));
        Assert.True(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Far, true, Whole, FocusRules.AttackerTier, Near, true));
        Assert.False(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Middle, false, Whole, FocusRules.AttackerTier, Middle, true));
        Assert.True(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Far, false, Whole, FocusRules.AttackerTier, Middle, true));
        Assert.False(FocusRules.ShouldSwitch(FocusRules.AttackerTier, Far, true, Whole, FocusRules.AttackerTier, Near, false));
    }
}
