using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A stripped person buys back the weapon its kit rolled, not the row's first piece.</summary>
public class UpgradeGearSkillTests
{
    [Fact]
    public void KitWeapon_IsTheRolledPieceOfTheRow()
    {
        Assert.Equal("Scimitar", UpgradeGearSkill.KitWeapon(["Scimitar", "MetalShield", "PlateChest"], KitVariation.OneHandedSword));
        Assert.Equal("Spear", UpgradeGearSkill.KitWeapon(["LeatherChest", "Spear"], KitVariation.TwoHandedFencing));
    }

    [Fact]
    public void KitWeapon_WithoutTheRowInTheKit_IsTheRowItself()
    {
        Assert.Equal(KitVariation.OneHandedFencing, UpgradeGearSkill.KitWeapon(["Cloak", "LeatherChest"], KitVariation.OneHandedFencing));
        Assert.Equal(KitVariation.Longbow, UpgradeGearSkill.KitWeapon(null, KitVariation.Longbow));
    }
}
