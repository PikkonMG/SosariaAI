using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class FightTrendTests
{
    private const double Tolerance = 1e-9;
    private const int Second = 1000;
    private const int HitsMax = 100;

    [Fact]
    public void Ledger_ReadsHitsLostAndDealtPerSecond()
    {
        var ledger = new FightLedger();
        ledger.Note(0, 100);
        ledger.Dealt(20);
        ledger.Note(2 * Second, 96);
        ledger.Dealt(20);
        ledger.Note(4 * Second, 92);

        var trend = ledger.Read(4 * Second, foeHitsLeft: 60);

        Assert.Equal(2, trend.LostPerSecond, Tolerance);
        Assert.Equal(10, trend.DealtPerSecond, Tolerance);
        Assert.Equal(60, trend.FoeHitsLeft);
        Assert.Equal(4 * Second, trend.SampleMs);
    }

    [Fact]
    public void Ledger_ReadsOnlyTheWindow()
    {
        var ledger = new FightLedger();
        ledger.Note(0, 100);
        ledger.Note(10 * Second, 40);
        ledger.Note(12 * Second, 40);
        ledger.Note(14 * Second, 40);

        var trend = ledger.Read(14 * Second, foeHitsLeft: 50);

        Assert.Equal(0, trend.LostPerSecond, Tolerance);
        Assert.Equal(4 * Second, trend.SampleMs);
    }

    [Fact]
    public void Ledger_ClearStartsAFreshFight()
    {
        var ledger = new FightLedger();
        ledger.Note(0, 100);
        ledger.Note(Second, 50);
        ledger.Clear();

        Assert.Equal(0, ledger.Read(Second, 0).SampleMs);
    }

    [Fact]
    public void Outlook_ThreeOrcsBarelyHurtingHim_IsWinning()
    {
        // He loses 1 hit a second from 60, they lose 12 a second from 150 between them.
        var trend = new FightTrend(LostPerSecond: 1, DealtPerSecond: 12, FoeHitsLeft: 150, SampleMs: 4 * Second);

        Assert.Equal(FightOutlook.Winning, FightTrendRules.Outlook(trend, hits: 60));
        Assert.True(FightTrendRules.LightDamage(trend, HitsMax));
    }

    [Fact]
    public void Outlook_FallingBeforeTheFoes_IsLosing()
    {
        var trend = new FightTrend(LostPerSecond: 8, DealtPerSecond: 3, FoeHitsLeft: 150, SampleMs: 4 * Second);

        Assert.Equal(FightOutlook.Losing, FightTrendRules.Outlook(trend, hits: 40));
        Assert.False(FightTrendRules.LightDamage(trend, HitsMax));
    }

    [Fact]
    public void Outlook_OuthealingTheBlows_IsWinningWhileItDealsDamage()
    {
        var healing = new FightTrend(LostPerSecond: -1, DealtPerSecond: 5, FoeHitsLeft: 200, SampleMs: 4 * Second);
        var stalemate = healing with { DealtPerSecond = 0 };

        Assert.Equal(FightOutlook.Winning, FightTrendRules.Outlook(healing, hits: 50));
        Assert.Equal(FightOutlook.Even, FightTrendRules.Outlook(stalemate, hits: 50));
    }

    [Fact]
    public void Outlook_DealingNothingWhileHurt_IsLosing() =>
        Assert.Equal(
            FightOutlook.Losing,
            FightTrendRules.Outlook(new FightTrend(2, 0, 100, 4 * Second), hits: 80)
        );

    [Fact]
    public void Outlook_TooShortASample_IsUnknown() =>
        Assert.Equal(
            FightOutlook.Unknown,
            FightTrendRules.Outlook(new FightTrend(1, 12, 150, FightTrendRules.MinSampleMs - 1), hits: 60)
        );
}
