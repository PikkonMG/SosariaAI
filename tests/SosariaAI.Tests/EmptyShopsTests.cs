using System;
using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A shop marker found with nobody at it is passed over for a while, then tried again.</summary>
public class EmptyShopsTests
{
    private const string Felucca = "Felucca";
    private const string Trammel = "Trammel";

    private static readonly DateTime Start = new(2026, 9, 27, 0, 19, 5, DateTimeKind.Utc);
    private static readonly Point3D SkaraMarker = new(646, 2153, 0);

    [Fact]
    public void IsEmpty_UntilTheMemoryRunsOut_OnThatFacetOnly()
    {
        var shops = new EmptyShops();
        var until = Start + ShopFinder.EmptyShopMemory;
        shops.Note(Felucca, SkaraMarker, until);

        Assert.True(shops.IsEmpty(Felucca, SkaraMarker, Start));
        Assert.False(shops.IsEmpty(Trammel, SkaraMarker, Start));
        Assert.False(shops.IsEmpty(Felucca, new Point3D(SkaraMarker.X + 1, SkaraMarker.Y, SkaraMarker.Z), Start));
        Assert.False(shops.IsEmpty(Felucca, SkaraMarker, until));
        Assert.False(shops.IsEmpty(Felucca, SkaraMarker, Start));
    }
}
