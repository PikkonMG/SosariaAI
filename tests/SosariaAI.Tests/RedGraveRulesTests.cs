using System;
using SosariaAI.Behaviour;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class RedGraveRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 3, 0, 0);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void Shuns_TheGroundWhereItOrAGangMateFell() =>
        Assert.True(RedGraveRules.Shuns(ownSideFell: true, redsFell: 0));

    [Fact]
    public void Shuns_GroundTheBluesHold_TwoRedsDownThere()
    {
        Assert.True(RedGraveRules.Shuns(ownSideFell: false, RedGraveRules.HeldFalls));
        Assert.False(RedGraveRules.Shuns(ownSideFell: false, RedGraveRules.HeldFalls - 1));
    }

    [Fact]
    public void Fresh_AFallCountsOnlyInsideItsWindow()
    {
        Assert.True(RedGraveRules.Fresh(Now, Now, RedGraveRules.HeldFor));
        Assert.True(RedGraveRules.Fresh(Now, Now + RedGraveRules.HeldFor - OneSecond, RedGraveRules.HeldFor));
        Assert.False(RedGraveRules.Fresh(Now, Now + RedGraveRules.HeldFor, RedGraveRules.HeldFor));
        Assert.False(RedGraveRules.Fresh(Now + OneSecond, Now, RedGraveRules.HeldFor));
    }

    [Fact]
    public void HeldFor_IsTheHalfLifeOfTheShardsDangerHeat() =>
        Assert.Equal(TimeSpan.FromMinutes(DangerMap.HalfLifeMinutes), RedGraveRules.HeldFor);

    [Fact]
    public void Remembered_CoversBothWindows()
    {
        Assert.True(RedGraveRules.Remembered >= RedGraveRules.HeldFor);
        Assert.True(RedGraveRules.Remembered >= RedGraveRules.OwnGraveFor);
    }
}
