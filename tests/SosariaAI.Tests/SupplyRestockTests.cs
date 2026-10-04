using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A supply the supply check restocks is a supply everywhere: the pack keeps it from the box,
/// the shop and the hawker's cry. Valka banked her twenty lockpicks, the supply draw took them
/// straight back out, and she walked to the Den's counter again every two seconds.
/// </summary>
public class SupplyRestockTests
{
    [Fact]
    public void IsRestocked_EverySupplyKindsOwnType()
    {
        Assert.True(SupplyCheck.IsRestocked(new Lockpick((Serial)0x7F6A)));
        Assert.True(SupplyCheck.IsRestocked(new RecallRune((Serial)0x7F6B)));
        Assert.True(SupplyCheck.IsRestocked(new Arrow((Serial)0x7F6C)));
        Assert.True(SupplyCheck.IsRestocked(new Bandage((Serial)0x7F6D)));
        Assert.True(SupplyCheck.IsRestocked(new RecallScroll((Serial)0x7F6E)));
        Assert.True(SupplyCheck.IsRestocked(new Nightshade((Serial)0x7F6F)));

        Assert.False(SupplyCheck.IsRestocked(new Log((Serial)0x7F70)));
        Assert.False(SupplyCheck.IsRestocked(new Amber((Serial)0x7F71)));
        Assert.False(SupplyCheck.IsRestocked(null));
    }

    [Fact]
    public void IsRestocked_TheScissorsButNotTheClothATailorSells()
    {
        Assert.True(SupplyCheck.IsRestocked(new Scissors((Serial)0x7F78)));
        Assert.False(SupplyCheck.IsRestocked(new Cloth((Serial)0x7F79)));
        Assert.False(SupplyCheck.IsRestocked(new UncutCloth((Serial)0x7F7A)));
        Assert.False(SupplyCheck.IsRestocked(new BoltOfCloth((Serial)0x7F7B)));
    }

    [Fact]
    public void IsSupply_TheScissorsAreNeverSoldPawnedOrTossed()
    {
        var scissors = new Scissors((Serial)0x7F7C);

        Assert.True(HawkerGoods.IsSupply(scissors));
        Assert.False(PawnPack.IsPawnable(scissors));
        Assert.False(PawnPack.IsJunkable(scissors));
    }

    [Fact]
    public void IsSupply_TheLockpickIsASupply_TheLootIsNot()
    {
        Assert.True(HawkerGoods.IsSupply(new Lockpick((Serial)0x7F72)));
        Assert.True(HawkerGoods.IsSupply(new BlackPearl((Serial)0x7F73)));
        Assert.True(HawkerGoods.IsSupply(new Bolt((Serial)0x7F74)));

        Assert.False(HawkerGoods.IsSupply(new Amber((Serial)0x7F75)));
        Assert.False(HawkerGoods.IsSupply(new Katana((Serial)0x7F76)));
    }
}
