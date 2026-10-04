using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class FightPullRulesTests
{
    private const int TrollHits = 110;
    private const int TrollStrength = 190;
    private const int TrollAverageDamage = 11;
    private const int ArcherVeteranPower = 117;
    private const int FreshPower = 40;
    private const int OneTrollThreat = 130;
    private const int TwoTrollThreat = 240;
    private const int NoAlliesPower = 0;
    private const double FullHitsFraction = 1.0;
    private const int ExpectedIsolateRange = 8;

    private static readonly HostileStats Troll = new(TrollHits, TrollStrength, TrollAverageDamage);

    [Fact]
    public void Score_OneAndTwoTrolls_MatchPullCase()
    {
        Assert.Equal(OneTrollThreat, ThreatRating.Score([Troll]));
        Assert.Equal(TwoTrollThreat, ThreatRating.Score([Troll, Troll]));
    }

    [Fact]
    public void CanPullOne_VeteranArcherVsTwoTrolls_IsTrue()
    {
        Assert.True(
            Pull(
                ArcherVeteranPower,
                OneTrollThreat,
                TwoTrollThreat
            )
        );
    }

    [Fact]
    public void CanPullOne_FreshVsOneTroll_IsFalse()
    {
        Assert.False(
            Pull(
                FreshPower,
                OneTrollThreat,
                OneTrollThreat
            )
        );
    }

    [Fact]
    public void CanPullOne_AlreadyAttacked_IsFalse()
    {
        Assert.False(
            Pull(
                ArcherVeteranPower,
                OneTrollThreat,
                TwoTrollThreat,
                alreadyAttacked: true
            )
        );
    }

    [Fact]
    public void CanPullOne_FocusTooHard_IsFalse()
    {
        Assert.False(
            Pull(
                ArcherVeteranPower,
                TwoTrollThreat,
                TwoTrollThreat
            )
        );
    }

    [Fact]
    public void NeedsIsolate_WhenExtrasPresent()
    {
        Assert.False(FightPullRules.NeedsIsolate(0));
        Assert.True(FightPullRules.NeedsIsolate(1));
        Assert.Equal(ExpectedIsolateRange, FightPullRules.IsolateRange);
    }

    private static bool Pull(
        int power,
        int focusThreat,
        int groupThreat,
        bool alreadyAttacked = false
    ) =>
        FightPullRules.CanPullOne(
            power,
            focusThreat,
            groupThreat,
            FullHitsFraction,
            hasHealing: true,
            NoAlliesPower,
            ThreatRating.DefaultThreatMultiple,
            ThreatRating.OpenFightHitsFraction,
            alreadyAttacked
        );

    [Fact]
    public void KeepsDrawing_UntilTheTimeTheSoleTargetOrTheGap()
    {
        const long Started = 0;
        const int Crowd = 3;
        const int Close = 2;

        Assert.True(FightPullRules.KeepsDrawing(Started, Crowd, Close));
        Assert.False(FightPullRules.KeepsDrawing(FightPullRules.DrawMs, Crowd, Close));
        Assert.False(FightPullRules.KeepsDrawing(Started, FightPullRules.SoleTarget, Close));
        Assert.False(FightPullRules.KeepsDrawing(Started, Crowd, FightPullRules.DrawClearTiles));
    }
}
