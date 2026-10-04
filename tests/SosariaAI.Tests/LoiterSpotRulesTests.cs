using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LoiterSpotRulesTests
{
    private static readonly Point3D Plaza = new(1428, 1694, 0);
    private static readonly Point3D Corner = new(1449, 1723, 6);
    private static readonly Point3D Shop = new(1469, 1668, 0);

    private static LoiterSpotLook Street(Point3D spot, int others) => new(spot, others, OnBankPlaza: false);

    private static LoiterSpotLook OnPlaza(int idlers) => new(Plaza, idlers, OnBankPlaza: true);

    [Fact]
    public void IsFull_ASpotHoldsAHandfulAndThePlazaOnlyACouple()
    {
        Assert.False(LoiterSpotRules.IsFull(Street(Corner, LoiterSpotRules.SpotCap - 1)));
        Assert.True(LoiterSpotRules.IsFull(Street(Corner, LoiterSpotRules.SpotCap)));
        Assert.False(LoiterSpotRules.IsFull(OnPlaza(LoiterSpotRules.PlazaIdlerCap - 1)));
        Assert.True(LoiterSpotRules.IsFull(OnPlaza(LoiterSpotRules.PlazaIdlerCap)));
        Assert.True(LoiterSpotRules.PlazaIdlerCap < LoiterSpotRules.SpotCap);
    }

    [Fact]
    public void Pick_WantedSpotWhenThereIsRoom() =>
        Assert.Equal(0, LoiterSpotRules.Pick([OnPlaza(0), Street(Corner, 0)]));

    [Fact]
    public void Pick_FullPlazaSendsThePersonToItsCorner()
    {
        // The hundred in the Britain bank: every stay began where the last errand ended.
        Assert.Equal(1, LoiterSpotRules.Pick([OnPlaza(LoiterSpotRules.PlazaIdlerCap), Street(Corner, 1), Street(Shop, 0)]));
    }

    [Fact]
    public void Pick_FullCornerGoesOnToTheNextPlaceWithRoom() =>
        Assert.Equal(
            2,
            LoiterSpotRules.Pick([OnPlaza(LoiterSpotRules.PlazaIdlerCap), Street(Corner, LoiterSpotRules.SpotCap), Street(Shop, 0)])
        );

    [Fact]
    public void Pick_AllFullTakesTheLeastFull()
    {
        var looks = new[]
        {
            OnPlaza(LoiterSpotRules.PlazaIdlerCap * 3),
            Street(Corner, LoiterSpotRules.SpotCap + 4),
            Street(Shop, LoiterSpotRules.SpotCap)
        };

        Assert.Equal(2, LoiterSpotRules.Pick(looks));
        Assert.Equal(LoiterSpotRules.NoSpot, LoiterSpotRules.Pick([]));
        Assert.Equal(LoiterSpotRules.NoSpot, LoiterSpotRules.Pick(null));
    }
}
