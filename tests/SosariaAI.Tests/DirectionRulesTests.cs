using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DirectionRulesTests
{
    private static readonly Point3D Start = new(1000, 1000, 0);
    private static readonly IReadOnlyList<Point3D> NoGates = [];

    [Theory]
    [InlineData("where is eastholm?", "eastholm")]
    [InlineData("where's eastholm", "eastholm")]
    [InlineData("How do I get to the eastholm bank?", "the eastholm bank")]
    [InlineData("Bob, which way to southmere", "southmere")]
    [InlineData("anyone know where southmere is", "southmere is")]
    [InlineData("how far is the healer from here", "the healer from here")]
    [InlineData("hi bob, where is eastholm", "eastholm")]
    [InlineData("u know where southmere is?", "southmere is")]
    [InlineData("whats the way to the healer?", "the healer")]
    public void AskedPlace_ReadsThePlaceAfterTheWayWords(string text, string expected) =>
        Assert.Equal(expected, SpeechIntent.AskedPlace(text));

    [Theory]
    [InlineData("where is everyone")]
    [InlineData("where is he")]
    [InlineData("where is my horse")]
    [InlineData("how far is it")]
    [InlineData("where r u")]
    [InlineData("where is")]
    [InlineData("nowhere is safe")]
    [InlineData("im on my way to southmere")]
    [InlineData("i know where southmere is")]
    [InlineData("nice weather")]
    [InlineData("")]
    public void AskedPlace_ALineThatAsksNoWayHasNone(string text) =>
        Assert.Null(SpeechIntent.AskedPlace(text));

    [Fact]
    public void Classify_AWayQuestionBehindAGreetingIsAQuestion()
    {
        Assert.Equal(SpeechIntentKind.Question, SpeechIntent.Classify("hi bob, where is eastholm", "Bob"));
        Assert.Equal(SpeechIntentKind.Greeting, SpeechIntent.Classify("hi bob", "Bob"));
    }

    [Fact]
    public void AsksTheWay_OnlyAQuestionThatNamesAPlace()
    {
        Assert.True(SpeechResponder.AsksTheWay(SpeechIntentKind.Question, "where is eastholm"));
        Assert.False(SpeechResponder.AsksTheWay(SpeechIntentKind.Question, "what time is it"));
        Assert.False(SpeechResponder.AsksTheWay(SpeechIntentKind.Trade, "where is eastholm"));
    }

    [Fact]
    public void After_GivesTheWordsAfterTheFirstPhraseFound()
    {
        Assert.Equal("the bank", SpeechIntent.After("ok go to the bank!", ["go to"]));
        Assert.Null(SpeechIntent.After("hello there", ["go to"]));
    }

    [Fact]
    public void PlaceLookup_AKindSaidWithAPlaceIsTheOneNearThatPlace()
    {
        // The asker stands by the southern bank; "the eastholm bank" is still Eastholm's.
        var asker = new Point3D(1810, 2820, 0);
        var found = PlaceLookup.Resolve(Catalog(), "the eastholm bank", asker);

        Assert.Equal("Eastholm Teller", found?.Name);
        Assert.Equal("Southmere Teller", PlaceLookup.Resolve(Catalog(), "bank", asker)?.Name);
        Assert.Equal("Eastholm Teller", PlaceLookup.Resolve(Catalog(), "bank in eastholm", asker)?.Name);
    }

    [Fact]
    public void PlaceLookup_ANamedPlaceWordByWord()
    {
        Assert.Equal("Southmere", PlaceLookup.Resolve(Catalog(), "southmere from here", Start)?.Name);
        Assert.Null(PlaceLookup.Resolve(Catalog(), "atlantis", Start));
        Assert.Null(PlaceLookup.Resolve(null, "southmere", Start));
        Assert.Null(PlaceLookup.Resolve(Catalog(), null, Start));
    }

    [Theory]
    [InlineData(1000, 900, "north")]
    [InlineData(1100, 900, "north-east")]
    [InlineData(1100, 1000, "east")]
    [InlineData(1100, 1100, "south-east")]
    [InlineData(1010, 1100, "south")]
    [InlineData(900, 1100, "south-west")]
    [InlineData(900, 1010, "west")]
    [InlineData(900, 900, "north-west")]
    public void Compass_UsesTheMapsOwnAxes(int x, int y, string expected) =>
        Assert.Equal(expected, DirectionRules.Compass(Start, new Point3D(x, y, 0)));

    [Theory]
    [InlineData(1010, 1000, WayKind.Here)]
    [InlineData(1100, 1000, WayKind.Close)]
    [InlineData(1300, 1000, WayKind.ShortWalk)]
    [InlineData(1000, 1800, WayKind.Far)]
    [InlineData(1000, 3000, WayKind.Unknown)]
    public void Answer_DistanceInPlainWords(int x, int y, WayKind expected) =>
        Assert.Equal(expected, DirectionRules.Answer(Start, new Point3D(x, y, 0), NoGates, fromInside: false).Kind);

    [Fact]
    public void Answer_AFarPlaceTheGatesBringNearSaysTakeTheMoongate()
    {
        var far = new Point3D(3050, 1000, 0);
        IReadOnlyList<Point3D> gates = [new Point3D(1000, 1100, 0), new Point3D(3000, 1000, 0)];
        IReadOnlyList<Point3D> gateInSight = [new Point3D(1010, 1005, 0), new Point3D(3000, 1000, 0)];

        Assert.Equal(new WayAnswer(WayKind.Moongate, "south"), DirectionRules.Answer(Start, far, gates, fromInside: false));
        Assert.Equal(new WayAnswer(WayKind.MoongateHere, null), DirectionRules.Answer(Start, far, gateInSight, fromInside: false));
    }

    [Fact]
    public void Answer_ANearPlaceIsWalkedEvenWithAGateCloser()
    {
        IReadOnlyList<Point3D> gates = [new Point3D(1005, 1000, 0), new Point3D(1300, 1000, 0)];

        Assert.Equal(WayKind.ShortWalk, DirectionRules.Answer(Start, new Point3D(1310, 1000, 0), gates, fromInside: false).Kind);
    }

    [Fact]
    public void Answer_LandNoGateReachesIsNotGuessed()
    {
        // The gate saves tiles, but the land past it is still too far to tell.
        IReadOnlyList<Point3D> gates = [new Point3D(1500, 1000, 0), new Point3D(5000, 1000, 0)];

        Assert.Equal(WayKind.Unknown, DirectionRules.Answer(Start, new Point3D(5000, 3000, 0), gates, fromInside: false).Kind);
    }

    [Fact]
    public void Answer_FromInsideADungeonGivesNoCompass()
    {
        Assert.Equal(new WayAnswer(WayKind.Inside, null), DirectionRules.Answer(Start, new Point3D(1300, 1000, 0), NoGates, fromInside: true));
        Assert.Equal(WayKind.Here, DirectionRules.Answer(Start, new Point3D(1005, 1000, 0), NoGates, fromInside: true).Kind);
    }

    [Theory]
    [InlineData("the moongate", true)]
    [InlineData("nearest gate", true)]
    [InlineData("moongate from here", true)]
    [InlineData("the eastholm bank", false)]
    [InlineData(null, false)]
    public void NamesMoongate_TheGateWordsOnly(string words, bool expected) =>
        Assert.Equal(expected, DirectionRules.NamesMoongate(words));

    [Fact]
    public void Nearest_PicksTheClosestGate()
    {
        IReadOnlyList<Point3D> gates = [new Point3D(3000, 1000, 0), new Point3D(1200, 1100, 0), new Point3D(1000, 2500, 0)];

        Assert.Equal(new Point3D(1200, 1100, 0), DirectionRules.Nearest(Start, gates));
    }

    [Fact]
    public void Line_FillsTheDirection_AndVariesWithTheSeed()
    {
        var answer = new WayAnswer(WayKind.ShortWalk, "south-east");
        var seen = new HashSet<string>();

        for (var seed = 0; seed < 20; seed++)
        {
            var line = DirectionRules.Line(answer, seed);
            Assert.Contains("south-east", line);
            Assert.DoesNotContain(DirectionRules.DirectionToken, line);
            seen.Add(line);
        }

        Assert.True(seen.Count > 1);
        Assert.Null(DirectionRules.Line(new WayAnswer(WayKind.Unknown, null), 0));
    }

    [Fact]
    public void Lines_AreShortLowerCasePlayerTalkThatPromisesNothing()
    {
        foreach (var template in DirectionRules.AllLines())
        {
            var line = template.Replace(DirectionRules.DirectionToken, "north-west", System.StringComparison.Ordinal);

            Assert.Equal(line.ToLowerInvariant(), line);
            Assert.True(line.Length <= BrainConfiguration.DefaultMaxReplyCharacters, line);
            Assert.False(PromiseLines.IsPromise(line), line);
            Assert.True(SpeechFacts.ClaimsFree(line), line);
        }
    }

    private static DestinationCatalog Catalog() =>
        new(
            [
                Place("Eastholm", "Vendor", 1592, 1680),
                Place("Eastholm Teller", "Bank", 1650, 1608),
                Place("Westgate Teller", "Bank", 1425, 1690),
                Place("Southmere", "Vendor", 1914, 2717),
                Place("Southmere Teller", "Bank", 1813, 2825)
            ]
        );

    private static Destination Place(string name, string kind, int x, int y) =>
        new() { Name = name, Kind = kind, Role = name, X = x, Y = y };
}
