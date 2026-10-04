using System;
using Server;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TreasureNewsTests
{
    private const string Digger = "Aldo";
    private const string Place = "the Britain moors";
    private const string Facet = "Felucca";
    private const int X = 1200;
    private const int Y = 1400;
    private const int Z = 5;
    private static readonly DateTime At = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EventFor_CarriesTheStory()
    {
        var evt = TreasureNews.EventFor(ShardEventType.Treasure, Digger, Place, Facet, new Point3D(X, Y, Z), At);

        Assert.Equal(ShardEventType.Treasure, evt.Type);
        Assert.Equal(Digger, evt.Actor);
        Assert.Equal(Place, evt.Place);
        Assert.Equal(Facet, evt.Facet);
        Assert.Equal(X, evt.X);
        Assert.Equal(Y, evt.Y);
        Assert.Equal(Z, evt.Z);
        Assert.Equal(At, evt.At);
        Assert.True(evt.IsActor(Digger));
    }

    [Fact]
    public void Types_AreDistinct() =>
        Assert.NotEqual(ShardEventType.Treasure, ShardEventType.SeaFind);
}
