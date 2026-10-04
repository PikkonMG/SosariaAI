using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class ForgeShopTests
{
    [Fact]
    public void IsForgeShop_Weaponsmith_IsFalse()
    {
        Assert.False(ForgeShop.IsForgeShop("Weaponsmith 2216-1167", "Smith"));
        Assert.False(ForgeShop.IsForgeShop("Cove Weaponsmith", ForgeShop.SmithRole));
    }

    [Fact]
    public void IsForgeShop_BlacksmithAndForge_AreTrue()
    {
        Assert.True(ForgeShop.IsForgeShop("Britain Blacksmith", ForgeShop.SmithRole));
        Assert.True(ForgeShop.IsForgeShop("Town Forge", ForgeShop.SmithRole));
        Assert.True(ForgeShop.IsForgeShop("Guildmaster", ForgeShop.SmithRole));
    }

    [Fact]
    public void IsForgeShop_Armorer_IsFalse() =>
        Assert.False(ForgeShop.IsForgeShop("Armorer 2216-1167", "Armorer"));

    [Fact]
    public void IsForgeShop_BlacksmithThatSellsWeapons_IsTrue() =>
        Assert.True(ForgeShop.IsForgeShop("Blacksmith and Weaponsmith", ForgeShop.SmithRole));
}
