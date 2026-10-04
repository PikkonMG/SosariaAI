using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A vendor drops what the buyer's pack cannot hold at the buyer's feet, paid for. A crafter
/// with a full pack paid for a tool, saw no new tool in the pack, and bought again at the next
/// counter, every tick. Only what the pack holds is bought, and a drop at the feet is picked up.
/// </summary>
public class VendorDealHoldTests
{
    private const uint PackSerial = 0x5D01;
    private const uint SampleSerial = 0x5D02;
    private const uint LoadSerial = 0x5D03;
    private const uint BuyerSerial = 0x5D04;
    private const uint BuyerPackSerial = 0x5D05;
    private const uint OldDropSerial = 0x5D06;
    private const uint NewDropSerial = 0x5D07;
    private const int ArtTileCount = 0x4000;
    private const int IngotsPerStone = 10;
    private const int Asked = 50;
    private const int HammersRoom = 2;
    private const int HammerWeight = 8;
    private const int OpenX = 40;
    private const int OpenY = 40;

    public VendorDealHoldTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        EnsureItemBounds();
    }

    [Fact]
    public void UnitsThatFit_AnEmptyPackTakesTheWholeOrder()
    {
        var pack = Pack(PackSerial);
        var sample = Ingots(SampleSerial, 1);

        Assert.Equal(Asked, VendorDeal.UnitsThatFit(null, pack, sample, Asked));
    }

    [Fact]
    public void UnitsThatFit_ANearlyFullPackTakesOnlyWhatItsWeightAllows()
    {
        var pack = Pack(PackSerial);
        pack.AddItem(Ingots(LoadSerial, (pack.MaxWeight - 1) * IngotsPerStone));
        var sample = Ingots(SampleSerial, 1);

        // One stone of room holds ten ingots of a tenth of a stone each.
        Assert.Equal(IngotsPerStone, VendorDeal.UnitsThatFit(null, pack, sample, Asked));
    }

    [Fact]
    public void UnitsThatFit_EachToolIsItsOwnWeight()
    {
        var pack = Pack(PackSerial);
        pack.AddItem(Ingots(LoadSerial, (pack.MaxWeight - HammersRoom * HammerWeight) * IngotsPerStone));
        var sample = Hammer(SampleSerial);

        Assert.Equal(HammersRoom, VendorDeal.UnitsThatFit(null, pack, sample, Asked));
    }

    [Fact]
    public void UnitsThatFit_AFullPackTakesNothing()
    {
        var pack = Pack(PackSerial);
        pack.AddItem(Ingots(LoadSerial, pack.MaxWeight * IngotsPerStone));
        var sample = Hammer(SampleSerial);

        Assert.Equal(0, VendorDeal.UnitsThatFit(null, pack, sample, 1));
    }

    [Fact]
    public void PickUpDropped_TakesWhatTheVendorDroppedAndLeavesWhatLayThere()
    {
        var land = TestMap.EnsureLand();
        var buyer = new Mobile((Serial)BuyerSerial);
        buyer.DefaultMobileInit();
        var pack = Pack(BuyerPackSerial);
        pack.Layer = Layer.Backpack;
        pack.Parent = buyer;
        buyer.Items.Add(pack);
        buyer.MoveToWorld(new Point3D(OpenX, OpenY, 0), land);

        var old = Hammer(OldDropSerial);
        old.MoveToWorld(buyer.Location, land);
        var alreadyThere = VendorDeal.ItemsAtFeet(buyer, typeof(SmithHammer));
        var dropped = Hammer(NewDropSerial);
        dropped.MoveToWorld(buyer.Location, land);

        VendorDeal.PickUpDropped(buyer, pack, typeof(SmithHammer), alreadyThere);

        Assert.True(dropped.IsChildOf(pack));
        Assert.False(old.IsChildOf(pack));
        Assert.Equal(new HashSet<Serial> { old.Serial }, alreadyThere);

        old.Delete();
        dropped.Delete();
        buyer.Delete();
    }

    // The serial constructors are the load path: they set none of the defaults a new item has.
    private static Backpack Pack(uint serial) =>
        new((Serial)serial) { Movable = true, MaxItems = Container.GlobalMaxItems };

    private static IronIngot Ingots(uint serial, int amount) =>
        new((Serial)serial) { Movable = true, Stackable = true, Amount = amount };

    private static SmithHammer Hammer(uint serial) => new((Serial)serial) { Movable = true, Amount = 1 };

    // A container places a dropped item inside the art bounds, which the test run never loads.
    private static void EnsureItemBounds()
    {
        if (ItemBounds.Bounds == null)
        {
            typeof(ItemBounds).GetProperty(nameof(ItemBounds.Bounds))!.SetValue(null, new Rectangle2D[ArtTileCount]);
        }
    }
}
