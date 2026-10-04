using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class CastTimingTests
{
    private const int FirstCircleMs = 750;
    private const int FlameStrikeMs = 2250;
    private const int ProtectedFirstCircleMs = 1250;
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
    public void CastDelayMs_MatchesTheEngineTicks()
    {
        Assert.Equal(FirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, slowedByProtection: false));
        Assert.Equal(FlameStrikeMs, CastTiming.CastDelayMs(SpellBook.FlameStrike.Circle, slowedByProtection: false));
        Assert.Equal(ProtectedFirstCircleMs, CastTiming.CastDelayMs(CastTiming.FirstCircle, slowedByProtection: true));
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
        Assert.Equal(CastTiming.FirstCircle, CastTiming.SafeCircle(0, false, firstCircleShrugsHits: true));
        Assert.Equal(CastTiming.NoCircle, CastTiming.SafeCircle(0, false, firstCircleShrugsHits: false));
    }

    [Fact]
    public void SafeCircle_ASlowChaserSixTilesOut_LetsTheBigSpellsThrough()
    {
        var blow = CastTiming.BlowInMs(SixTiles, Reach, SlowStepMs, SwingNow, held: false);

        Assert.Equal(SpellBook.MindBlast.Circle, CastTiming.SafeCircle(blow, false, firstCircleShrugsHits: true));
    }

    [Fact]
    public void SafeCircle_AHeldFoe_OpensEveryCircle() =>
        Assert.Equal(CastTiming.TopCircle, CastTiming.SafeCircle(CastTiming.NoBlowMs, false, firstCircleShrugsHits: true));

    [Fact]
    public void Holds_KeepsTheMargin()
    {
        Assert.True(CastTiming.Holds(FirstCircleMs, FirstCircleMs + CastTiming.BlowMarginMs));
        Assert.False(CastTiming.Holds(FirstCircleMs, FirstCircleMs + CastTiming.BlowMarginMs - 1));
    }
}
