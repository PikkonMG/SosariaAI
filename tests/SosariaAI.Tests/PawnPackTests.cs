using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>The take from a fight is pawn goods; gold, supplies and junk are not.</summary>
public class PawnPackTests
{
    [Fact]
    public void IsPawnable_LootYes_GoldSuppliesAndJunkNo()
    {
        Assert.True(PawnPack.IsPawnable(new Katana((Serial)0x7D01)));
        Assert.True(PawnPack.IsPawnable(new LeatherChest((Serial)0x7D02)));
        Assert.True(PawnPack.IsPawnable(new Shirt((Serial)0x7D03)));
        Assert.True(PawnPack.IsPawnable(new Amber((Serial)0x7D04)));
        Assert.True(PawnPack.IsPawnable(new HealPotion((Serial)0x7D05)));
        Assert.True(PawnPack.IsPawnable(
            new Katana((Serial)0x7D06) { DamageLevel = WeaponDamageLevel.Ruin }));

        Assert.False(PawnPack.IsPawnable(new Gold((Serial)0x7D07)));
        Assert.False(PawnPack.IsPawnable(new Bandage((Serial)0x7D08)));
        Assert.False(PawnPack.IsPawnable(new Arrow((Serial)0x7D09)));
        Assert.False(PawnPack.IsPawnable(new Bolt((Serial)0x7D0A)));
        Assert.False(PawnPack.IsPawnable(new BlackPearl((Serial)0x7D0B)));
        Assert.False(PawnPack.IsPawnable(new RecallScroll((Serial)0x7D0C)));
        Assert.False(PawnPack.IsPawnable(new Torch((Serial)0x7D0D)));
        Assert.False(PawnPack.IsPawnable(null));
    }

    [Fact]
    public void IsJunkable_PlainGearOnly()
    {
        Assert.True(PawnPack.IsJunkable(new Katana((Serial)0x7D11)));
        Assert.True(PawnPack.IsJunkable(new LeatherChest((Serial)0x7D12)));
        Assert.True(PawnPack.IsJunkable(new Shirt((Serial)0x7D13)));
        Assert.True(PawnPack.IsJunkable(new Hides((Serial)0x7D14)));

        Assert.False(PawnPack.IsJunkable(
            new Katana((Serial)0x7D15) { DamageLevel = WeaponDamageLevel.Ruin }));
        Assert.False(PawnPack.IsJunkable(new Amber((Serial)0x7D16)));
        Assert.False(PawnPack.IsJunkable(new Gold((Serial)0x7D17)));
        Assert.False(PawnPack.IsJunkable(new Bandage((Serial)0x7D18)));
        Assert.False(PawnPack.IsJunkable(null));
    }
}
