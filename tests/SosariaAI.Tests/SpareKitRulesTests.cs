using System;
using SosariaAI.Combat;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class SpareKitRulesTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Seeds_OnlyAKeeperWithNoBagYet(bool keepsSpare, bool hasBag, bool seeds) =>
        Assert.Equal(seeds, SpareKitRules.Seeds(keepsSpare, hasBag));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void KeepsSpare_RedsAndBlueFighters(bool red, bool fighter, bool keeps) =>
        Assert.Equal(keeps, SpareKitRules.KeepsSpare(red, fighter));

    [Fact]
    public void Reserve_HoldsSeveralFullRestocks()
    {
        Assert.True(SpareKitRules.ReserveRestocks > 1);
        Assert.Equal(SupplyRules.ReagentTarget * SpareKitRules.ReserveRestocks, SpareKitRules.ReagentReserve);
        Assert.Equal(SupplyRules.BandageTarget * SpareKitRules.ReserveRestocks, SpareKitRules.BandageReserve);
    }

    [Theory]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, false, true, false, false)]
    public void TakesOut_ALostCombatPieceTheBagHolds_NotAStack(bool combatPiece, bool carried, bool inBag, bool stackable, bool takes) =>
        Assert.Equal(takes, SpareKitRules.TakesOut(combatPiece, carried, inBag, stackable));

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    public void Stows_ACarriedSpareTheBagLacks(bool combatPiece, bool inBag, bool looseSpare, bool stows) =>
        Assert.Equal(stows, SpareKitRules.Stows(combatPiece, inBag, looseSpare));

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void Lacks_ACombatPieceNeitherTheBagNorThePackHolds(bool combatPiece, bool inBag, bool inPack, bool lacks) =>
        Assert.Equal(lacks, SpareKitRules.Lacks(combatPiece, inBag, inPack));

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void LacksArms_NoKitOrNoWeaponForAWeaponBuild(bool kitMissing, bool carriesWeapon, bool armed, bool lacks) =>
        Assert.Equal(lacks, SpareKitRules.LacksArms(kitMissing, carriesWeapon, armed, GearLadder.Slots(KitArmor.Light), bodyArmored: true));

    [Theory]
    [InlineData(KitArmor.Light, false, true)]
    [InlineData(KitArmor.Heavy, false, true)]
    [InlineData(KitArmor.Light, true, false)]
    [InlineData(KitArmor.None, false, false)]
    [InlineData(KitArmor.Work, false, false)]
    public void LacksArms_AnArmorBuildWithNoChestPiece_EvenWithItsBookOrWeapon(KitArmor weight, bool bodyArmored, bool lacks) =>
        Assert.Equal(lacks, SpareKitRules.LacksArms(kitMissing: false, carriesWeapon: true, armed: true, GearLadder.Slots(weight), bodyArmored));

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public void MustReArm_ARedThatLacksArms_WhenTheDenCanArmIt(bool red, bool lacksArms, bool canReArm, bool must) =>
        Assert.Equal(must, SpareKitRules.MustReArm(red, lacksArms, canReArm));

    [Theory]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, false, false, false, false)]
    public void Keeps_AFittingPieceTheBagOrTheBackLacks_OnceARole(bool fits, bool inBag, bool worn, bool carriedLoose, bool keeps) =>
        Assert.Equal(keeps, SpareKitRules.Keeps(fits, inBag, worn, carriedLoose));

    [Fact]
    public void RoleOf_AWeaponOfTheRow_AShelfShield_OrArmorOfASlot()
    {
        Assert.Equal(new SpareRole(SpareRoleKind.Weapon, default), SpareKitRules.RoleOf("Broadsword", KitVariation.OneHandedSword, "Katana"));
        Assert.Equal(new SpareRole(SpareRoleKind.Shield, default), SpareKitRules.RoleOf("HeaterShield", KitVariation.OneHandedSword, "Katana"));
        Assert.Equal(new SpareRole(SpareRoleKind.Armor, GearSlot.Chest), SpareKitRules.RoleOf("ChainChest", KitVariation.OneHandedSword, "Katana"));
        Assert.Equal(new SpareRole(SpareRoleKind.Armor, GearSlot.Helm), SpareKitRules.RoleOf("Helmet", null, null));
    }

    [Fact]
    public void RoleOf_NothingForAWeaponOffTheRow_ACrossbowForABow_OrAnUnknownPiece()
    {
        Assert.Equal(SpareRole.None, SpareKitRules.RoleOf("WarHammer", KitVariation.OneHandedSword, "Katana"));
        Assert.Equal(SpareRole.None, SpareKitRules.RoleOf("Crossbow", KitVariation.Longbow, "Bow"));
        Assert.Equal(SpareRole.None, SpareKitRules.RoleOf("Katana", null, null));
        Assert.Equal(SpareRole.None, SpareKitRules.RoleOf("Robe", KitVariation.OneHandedSword, "Katana"));
        Assert.Equal(SpareRole.None, SpareKitRules.RoleOf(null, KitVariation.OneHandedSword, "Katana"));
    }

    [Fact]
    public void RoleOf_ACrossbowStandsInForAHeavyCrossbow()
    {
        Assert.Equal(SpareRoleKind.Weapon, SpareKitRules.RoleOf("Crossbow", KitVariation.Longbow, "HeavyCrossbow").Kind);
        Assert.Equal(SpareRoleKind.Weapon, SpareKitRules.RoleOf("Bow", KitVariation.Longbow, null).Kind);
    }

    [Fact]
    public void FitsBuild_ArmorOnACoveredSlot_NoHeavierThanTheBuildWears()
    {
        var chest = new SpareRole(SpareRoleKind.Armor, GearSlot.Chest);
        var helm = new SpareRole(SpareRoleKind.Armor, GearSlot.Helm);
        var fullSuit = GearLadder.Slots(KitArmor.Heavy);
        var lightSuit = GearLadder.Slots(KitArmor.Light);

        Assert.True(SpareKitRules.FitsBuild(chest, false, fullSuit, GearLadder.PlateScore, GearLadder.PlateScore));
        Assert.False(SpareKitRules.FitsBuild(chest, false, fullSuit, GearLadder.PlateScore, GearLadder.StuddedScore));
        Assert.False(SpareKitRules.FitsBuild(helm, false, lightSuit, GearLadder.LeatherScore, GearLadder.StuddedScore));
        Assert.True(SpareKitRules.FitsBuild(chest, false, lightSuit, GearLadder.LeatherScore, GearLadder.StuddedScore));
    }

    [Fact]
    public void FitsBuild_AShieldOnlyForAShieldBuild_TheWeaponAlways_NothingElse()
    {
        var shield = new SpareRole(SpareRoleKind.Shield, default);
        var weapon = new SpareRole(SpareRoleKind.Weapon, default);

        Assert.True(SpareKitRules.FitsBuild(shield, true, [], 0, 0));
        Assert.False(SpareKitRules.FitsBuild(shield, false, [], 0, 0));
        Assert.True(SpareKitRules.FitsBuild(weapon, false, [], 0, 0));
        Assert.False(SpareKitRules.FitsBuild(SpareRole.None, true, GearLadder.Slots(KitArmor.Heavy), 0, GearLadder.PlateScore));
    }

    [Fact]
    public void ShopRests_ForTheRestAfterAMiss_AndNeverBeforeOne()
    {
        var missed = new DateTime(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);

        Assert.True(SpareKitRules.ShopRests(missed, missed));
        Assert.True(SpareKitRules.ShopRests(missed, missed + SpareKitRules.ShopMissRest - TimeSpan.FromSeconds(1)));
        Assert.False(SpareKitRules.ShopRests(missed, missed + SpareKitRules.ShopMissRest));
        Assert.False(SpareKitRules.ShopRests(default, missed));
    }
}
