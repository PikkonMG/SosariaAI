using Server;
using Server.Items;
using SosariaAI.Skills;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// What a seller puts on a counter, read through the kit it keeps: kit pieces are found by
/// type name in the loaded content, so this runs in the real-map collection with the world
/// <see cref="KitWorld"/> sets up, and skips without the client data.
/// </summary>
[Collection(RealMapCollection.Name)]
public class SaleGoodsForSaleTests
{
    // A range no other test class uses: tests that share serials collide in the one engine world.
    private const uint PackSerial = 0x9C01;
    private const uint LogSerial = 0x9C02;
    private const uint GemSerial = 0x9C03;
    private const uint SpareSerial = 0x9C04;
    private const uint PickaxeSerial = 0x9C05;
    private const uint GoldSerial = 0x9C06;
    private const uint BandageSerial = 0x9C07;

    public SaleGoodsForSaleTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void ForSale_HarvestAndLoot_NotToolsGoldSuppliesOrTheSpareWeapon()
    {
        var seller = KitWorld.Blank("Felucca:SaleGoodsSeller#1", female: false);
        var pack = new Backpack((Serial)PackSerial) { Layer = Layer.Backpack };
        pack.Parent = seller;
        seller.Items.Add(pack);
        var log = new Log((Serial)LogSerial);
        var gem = new Amber((Serial)GemSerial);
        var spare = new Katana((Serial)SpareSerial);
        var pickaxe = new Pickaxe((Serial)PickaxeSerial);
        var gold = new Gold((Serial)GoldSerial);
        var bandage = new Bandage((Serial)BandageSerial);

        foreach (var item in new Item[] { log, gem, spare, pickaxe, gold, bandage })
        {
            seller.Backpack.AddItem(item);
        }

        var goods = SaleGoods.ForSale(seller);

        Assert.Contains(log, goods);
        Assert.Contains(gem, goods);
        Assert.DoesNotContain(spare, goods);
        Assert.DoesNotContain(pickaxe, goods);
        Assert.DoesNotContain(gold, goods);
        Assert.DoesNotContain(bandage, goods);
    }
}
