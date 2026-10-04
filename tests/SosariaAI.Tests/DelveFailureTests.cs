using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A failed dungeon trip says why, once. Morwenna of Yew "ended Dungeon (Failed)" and gave
/// up after three tries with no word of the cause.
/// </summary>
public class DelveFailureTests
{
    private const string Name = "Morwenna of Yew";
    private static readonly Point3D Magincia = new(3764, 2127, 25);

    [Fact]
    public void ShouldSay_OnlyTheFirstFailureOfATrip()
    {
        var failure = new DelveFailure();

        Assert.True(failure.ShouldSay());
        Assert.False(failure.ShouldSay());

        failure.Reset();

        Assert.True(failure.ShouldSay());
    }

    [Fact]
    public void Check_PassesTheStatusThrough()
    {
        var failure = new DelveFailure();

        Assert.Equal(SkillStatus.Running, failure.Check(null, SkillStatus.Running, DelveFailure.NoWayToHall));
        Assert.Equal(SkillStatus.Failed, failure.Check(null, SkillStatus.Failed, DelveFailure.NoWayToHall));
        Assert.False(failure.Refuse(null, DelveFailure.NoWayToDoor));
    }

    [Fact]
    public void Line_NamesThePersonThePlaceAndTheReason()
    {
        var line = DelveFailure.Line(Name, DelveFailure.NoWayToDoor, Magincia);

        Assert.Contains(Name, line);
        Assert.Contains(Magincia.ToString(), line);
        Assert.EndsWith(DelveFailure.NoWayToDoor, line);
    }
}
