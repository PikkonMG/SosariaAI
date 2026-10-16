using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class CastTimingTests
{
    // MagerySpell.GetCastDelay before AOS: 0.5 + 0.25 * (circle - 1) seconds, Protection aside.
    private const int PreAosFirstCircleMs = 500;
    private const int PreAosFlameStrikeMs = 2000;

    // MagerySpell.CastDelayBase from AOS on: (3 + circle - 1) * 0.25 seconds, Protection two ticks more.
    private const int AosFirstCircleMs = 750;
    private const int AosFlameStrikeMs = 2250;
    private const int AosProtectedFirstCircleMs = 1250;
    private const int MonsterStepMs = 200;
    private const int SlowStepMs = 400;
    private const int Reach = 1;
    private const int Adjacent = 1;
    private const int SixTiles = 6;
    private const long SwingNow = 0;
    private const long SwingIn2Seconds = 2000;
    private const long LongAgo = -5000;
    private const long FiveStepsMs = 5 * MonsterStepMs;

    [Fact]
    public void CastDelayMs_PreAos_MatchesTheEngineTicks()
    {
        Assert.Equal(PreAosFirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, aos: false, slowedByProtection: false));
        Assert.Equal(PreAosFlameStrikeMs, CastTiming.CastDelayMs(SpellBook.FlameStrike.Circle, aos: false, slowedByProtection: false));
    }

    [Fact]
    public void CastDelayMs_PreAos_ProtectionSlowsNothing() =>
        Assert.Equal(PreAosFirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, aos: false, slowedByProtection: true));

    [Fact]
    public void CastDelayMs_Aos_MatchesTheEngineTicks()
    {
        Assert.Equal(AosFirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, aos: true, slowedByProtection: false));
        Assert.Equal(AosFlameStrikeMs, CastTiming.CastDelayMs(SpellBook.FlameStrike.Circle, aos: true, slowedByProtection: false));
        Assert.Equal(AosProtectedFirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, aos: true, slowedByProtection: true));
    }

    [Fact]
    public void BlowInMs_WalkThenSwing()
    {
        Assert.Equal(FiveStepsMs, CastTiming.BlowInMs(SixTiles, Reach, MonsterStepMs, SwingNow, held: false));
        Assert.Equal(SwingIn2Seconds, CastTiming.BlowInMs(Adjacent, Reach, MonsterStepMs, SwingIn2Seconds, held: false));
        Assert.Equal(0, CastTiming.BlowInMs(Adjacent, Reach, MonsterStepMs, LongAgo, held: false));
        Assert.Equal(CastTiming.NoBlowMs, CastTiming.BlowInMs(Adjacent, Reach, MonsterStepMs, SwingNow, held: true));
    }

    [Fact]
    public void SafeCircle_AdjacentAndReady_OnlyTheFirstCirclePreAos()
    {
        Assert.Equal(CastTiming.FirstCircle, CastTiming.SafeCircle(0, aos: false, slowedByProtection: false));
        Assert.Equal(CastTiming.NoCircle, CastTiming.SafeCircle(0, aos: true, slowedByProtection: false));
    }

    [Fact]
    public void SafeCircle_ASlowChaserSixTilesOut_LetsTheBigSpellsThrough()
    {
        var blow = CastTiming.BlowInMs(SixTiles, Reach, SlowStepMs, SwingNow, held: false);

        Assert.Equal(SpellBook.EnergyBolt.Circle, CastTiming.SafeCircle(blow, aos: false, slowedByProtection: false));
        Assert.Equal(SpellBook.MindBlast.Circle, CastTiming.SafeCircle(blow, aos: true, slowedByProtection: false));
    }

    [Fact]
    public void SafeCircle_AHeldFoe_OpensEveryCircle() =>
        Assert.Equal(CastTiming.TopCircle, CastTiming.SafeCircle(CastTiming.NoBlowMs, aos: false, slowedByProtection: false));

    [Fact]
    public void Holds_KeepsTheMargin()
    {
        Assert.True(CastTiming.Holds(PreAosFirstCircleMs, PreAosFirstCircleMs + CastTiming.BlowMarginMs));
        Assert.False(CastTiming.Holds(PreAosFirstCircleMs, PreAosFirstCircleMs + CastTiming.BlowMarginMs - 1));
    }
}
