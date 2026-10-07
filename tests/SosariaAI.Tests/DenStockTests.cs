using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class DenStockTests
{
    [Fact]
    public void SellsAll_ASupplyNoShopStocks_IsNotSold() =>
        Assert.False(DenStock.SellsAll(SupplyKind.RecallScrolls, [(typeof(RecallScroll), 1)]));

    [Fact]
    public void SellsAll_NothingShort_IsNotATripHome()
    {
        Assert.False(DenStock.SellsAll(SupplyKind.Bandages, []));
        Assert.False(DenStock.SellsAll(SupplyKind.Bandages, null));
    }

    [Fact]
    public void SellsAll_NoWorldLoaded_IsNotSold() =>
        Assert.False(DenStock.SellsAll(SupplyKind.Bandages, [(typeof(Bandage), 1)]));

    [Fact]
    public void PaysAtTheDen_NoShelfIsNoRefillHoweverRich()
    {
        const int richPurse = 100000;

        Assert.False(DenStock.PaysAtTheDen(SupplyKind.Bandages, [(typeof(Bandage), 1)], richPurse, richPurse));
        Assert.False(DenStock.PaysAtTheDen(SupplyKind.Bandages, null, richPurse, richPurse));
    }
}
