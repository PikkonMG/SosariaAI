using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class CorpseReclaimTests
{
    [Fact]
    public void Decide_Exists_WhenOwnerAndItems()
    {
        Assert.Equal(CorpseReclaimResult.Exists, CorpseReclaim.Decide(true, true, 3));
        Assert.True(CorpseReclaim.CanLoot(CorpseReclaimResult.Exists));
    }

    [Fact]
    public void Decide_Decayed_WhenMissing()
    {
        Assert.Equal(CorpseReclaimResult.Decayed, CorpseReclaim.Decide(false, true, 3));
        Assert.Equal(CorpseReclaimResult.Decayed, CorpseReclaim.Decide(true, false, 3));
        Assert.False(CorpseReclaim.CanLoot(CorpseReclaimResult.Decayed));
    }

    [Fact]
    public void Decide_Stripped_WhenEmpty()
    {
        Assert.Equal(CorpseReclaimResult.Stripped, CorpseReclaim.Decide(true, true, 0));
        Assert.False(CorpseReclaim.CanLoot(CorpseReclaimResult.Stripped));
    }

    [Fact]
    public void AfterLoot_Empty_IsReclaimed()
    {
        Assert.Equal(CorpseReclaimResult.Reclaimed, CorpseReclaim.AfterLoot(0));
        Assert.Equal(CorpseReclaimResult.Exists, CorpseReclaim.AfterLoot(2));
    }

    [Fact]
    public void KeepsEarlierBody_TheBodyWithTheGear_NotTheNakedOne()
    {
        const int Armed = 9;
        const int Naked = 0;
        const int Few = 2;
        const int Many = 12;

        Assert.True(CorpseReclaim.KeepsEarlierBody(Armed, Many, Naked, Few));
        Assert.False(CorpseReclaim.KeepsEarlierBody(Naked, Few, Armed, Many));
        Assert.True(CorpseReclaim.KeepsEarlierBody(Naked, Many, Naked, Few));
        Assert.False(CorpseReclaim.KeepsEarlierBody(Naked, Few, Naked, Few));
    }
}
