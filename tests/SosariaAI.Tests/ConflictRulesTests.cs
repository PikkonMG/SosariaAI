using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class ConflictRulesTests
{
    private const int Leash = 400;
    private const int ShortLeash = 100;
    private static readonly DateTime Now = new(2026, 9, 30, 10, 0, 0);

    [Fact]
    public void PickTraffic_UsesARealHuntOrDungeonDestination()
    {
        var catalog = new DestinationCatalog(
            [
                new Destination { Name = "Yew Graveyard", Kind = "Hunt", Role = "Graveyard", X = 100, Y = 100 },
                new Destination { Name = "Despise", Kind = "Dungeon", Role = "Despise", X = 200, Y = 200 }
            ]
        );

        Assert.Equal(new Point3D(100, 100, 0), ConflictRules.PickTraffic(catalog, Point3D.Zero, 1));
        Assert.Equal(new Point3D(200, 200, 0), ConflictRules.PickTraffic(catalog, Point3D.Zero, 2));
    }

    [Fact]
    public void BlueRides_OnlyToAReportInReachAndNotProvedUnreachable()
    {
        // Minoc tamers chose a murder in the hills with no road near it, failed at once, and
        // chose it again: 160 outings never began.
        var minoc = new Point3D(2500, 540, 0);
        var hills = new ShardEvent { At = Now, Type = ShardEventType.Pk, X = 2170, Y = 660 };
        var spot = ConflictRules.SpotOf(hills);

        Assert.True(ConflictRules.BlueRides(hills, minoc, Leash, [], [], Now));
        Assert.False(ConflictRules.BlueRides(hills, minoc, Leash, [spot], [], Now));
        Assert.False(ConflictRules.BlueRides(hills, minoc, Leash, [new Point3D(spot.X + SpotMemory.SameSpotTiles, spot.Y, 0)], [], Now));
        Assert.True(ConflictRules.BlueRides(hills, minoc, Leash, [new Point3D(spot.X + SpotMemory.SameSpotTiles + 1, spot.Y, 0)], [], Now));
        Assert.False(ConflictRules.BlueRides(hills, minoc, ShortLeash, [], [], Now));
        Assert.False(ConflictRules.BlueRides(null, minoc, Leash, [], [], Now));
    }

    [Fact]
    public void BlueRides_NotBackToWhereItJustRanFromAThreat()
    {
        // Magincia blues rode to the murder the red still stood at, ran home from it, and rode
        // back: six times in fifteen minutes, each run home "because a threat is in sight".
        var home = new Point3D(3713, 2117, 20);
        var murder = new ShardEvent { At = Now, Type = ShardEventType.Pk, X = 3647, Y = 2097 };
        var spot = ConflictRules.SpotOf(murder);

        Assert.False(ConflictRules.BlueRides(murder, home, Leash, [], [spot], Now));
        Assert.False(ConflictRules.BlueRides(murder, home, Leash, [], [new Point3D(spot.X + NavSearch.AvoidRadiusTiles, spot.Y, 0)], Now));
        Assert.True(ConflictRules.BlueRides(murder, home, Leash, [], [new Point3D(spot.X + NavSearch.AvoidRadiusTiles + 1, spot.Y, 0)], Now));
    }

    [Fact]
    public void BlueAnswers_NeverAMurderInTheRedsTown()
    {
        var den = PkRules.BucsDenHaven;

        Assert.False(ConflictRules.BlueAnswers(new ShardEvent { At = Now, Type = ShardEventType.Pk, X = den.X, Y = den.Y }, Now));
        Assert.True(ConflictRules.BlueAnswers(new ShardEvent { At = Now, Type = ShardEventType.Pk, X = PkRules.BucsDenMinX - 1, Y = den.Y }, Now));
        Assert.False(ConflictRules.BlueAnswers(null, Now));
    }

    [Fact]
    public void BlueAnswers_OnlyAFreshMurder_ByTheFirstFewBlues()
    {
        var murder = new ShardEvent { At = Now, Type = ShardEventType.Pk, X = PkRules.BucsDenMinX - 1, Y = PkRules.BucsDenHaven.Y };

        Assert.True(ConflictRules.BlueAnswers(murder, Now + ConflictRules.AnswerAge));
        Assert.False(ConflictRules.BlueAnswers(murder, Now + ConflictRules.AnswerAge + TimeSpan.FromSeconds(1)));

        murder.Answerers = ConflictRules.MaxAnswerers - 1;
        Assert.True(ConflictRules.BlueAnswers(murder, Now));
        murder.Answerers = ConflictRules.MaxAnswerers;
        Assert.False(ConflictRules.BlueAnswers(murder, Now));
    }

    [Theory]
    [InlineData(GangRunPhase.Muster, false, "gather the gang to ride to destard and kill players")]
    [InlineData(GangRunPhase.Ride, false, "ride with the gang to destard to kill players")]
    [InlineData(GangRunPhase.Gather, false, "ride with the gang to destard to kill players")]
    [InlineData(GangRunPhase.Work, false, "patrol destard to kill players")]
    [InlineData(GangRunPhase.Work, true, "lie in wait at destard to kill players")]
    public void DoingPhrase_NamesTheRedsSpotAndPurpose(GangRunPhase phase, bool lurking, string expected)
    {
        // Leander camped Destard as a red, but his chat only knew "seek player conflict" and a
        // want of "traveling to Minoc", so he asked a player along to Minoc and never left.
        Assert.Equal(expected, ConflictRules.DoingPhrase("destard", phase, lurking, false, false, false));
    }

    [Fact]
    public void DoingPhrase_NamesTheBluesWork()
    {
        Assert.Equal("hunt reds in Buccaneer's Den", ConflictRules.DoingPhrase(null, GangRunPhase.Ride, false, true, true, false));
        Assert.Equal("hunt reds at their camps", ConflictRules.DoingPhrase(null, GangRunPhase.Ride, false, true, false, false));
        Assert.Equal("ride to a fresh murder to catch the killer", ConflictRules.DoingPhrase(null, GangRunPhase.Ride, false, false, false, true));
    }

    [Fact]
    public void DoingPhrase_IsNullWithNothingSpecific()
    {
        Assert.Null(ConflictRules.DoingPhrase(null, GangRunPhase.Ride, false, false, false, false));
    }
}
