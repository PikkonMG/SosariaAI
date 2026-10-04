using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class MusicRulesTests
{
    private const int ExpectedBaseReachTiles = 8;
    private const int ExpectedBardRangeSkillDivisor = 15;
    private const double NoSongSkill = 0;
    private const double GrandmasterSongSkill = 100;
    private const int GrandmasterReachTiles = 14;

    [Fact]
    public void BaseReach_IsEightTiles() =>
        Assert.Equal(ExpectedBaseReachTiles, MusicRules.BaseReachTiles);

    [Fact]
    public void BardRange_UsesBasePlusSkillOverFifteen()
    {
        Assert.Equal(ExpectedBardRangeSkillDivisor, MusicRules.BardRangeSkillDivisor);
        Assert.Equal(MusicRules.BaseReachTiles, MusicRules.BardRange(NoSongSkill));
        Assert.Equal(MusicRules.BaseReachTiles, MusicRules.BardRange(MusicRules.BardRangeSkillDivisor - 1));
        Assert.Equal(MusicRules.BaseReachTiles + 1, MusicRules.BardRange(MusicRules.BardRangeSkillDivisor));
        Assert.Equal(GrandmasterReachTiles, MusicRules.BardRange(GrandmasterSongSkill));
    }

    [Fact]
    public void FindInstrument_NoBard_IsNull() =>
        Assert.Null(MusicRules.FindInstrument(null));
}
