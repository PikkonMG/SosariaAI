using System;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class FlattenExtraBagsTests : IDisposable
{
    private const uint RootSerial = 0x6101;
    private const uint NestedSerial = 0x6102;
    private const uint GoldSerial = 0x6103;
    private const uint InnerSerial = 0x6104;
    private const uint OreSerial = 0x6105;
    private const int GoldAmount = 12;

    // The pack lies on the test land, where a later class would find it.
    private readonly Backpack _root = new((Serial)RootSerial);

    public FlattenExtraBagsTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
    }

    public void Dispose() => _root.Delete();

    [Fact]
    public void FlattenExtraBags_NestedBag_MovesLootToRootAndDeletesBag()
    {
        var nested = new Bag((Serial)NestedSerial);
        var gold = new Gold((Serial)GoldSerial) { Amount = GoldAmount };
        _root.Map = TestMap.EnsureLand();
        _root.AddItem(nested);
        nested.AddItem(gold);

        SosariaCharacter.FlattenExtraBags(_root);

        Assert.Same(_root, gold.Parent);
        Assert.True(nested.Deleted);
        Assert.False(gold.OnLinkList);
    }

    [Fact]
    public void FlattenExtraBags_TwoNestedCarryBags_MovesInnerLootToRoot()
    {
        var nested = new Bag((Serial)NestedSerial);
        var inner = new Pouch((Serial)InnerSerial);
        var ore = new Item((Serial)OreSerial);
        _root.Map = TestMap.EnsureLand();
        _root.AddItem(nested);
        nested.AddItem(inner);
        inner.AddItem(ore);

        SosariaCharacter.FlattenExtraBags(_root);

        Assert.Same(_root, ore.Parent);
        Assert.True(nested.Deleted);
        Assert.True(inner.Deleted);
        Assert.False(ore.OnLinkList);
    }
}
