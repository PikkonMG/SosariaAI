using Server;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class KitOnceRulesTests
{
    private const string Katana = "Katana";
    private const string MetalShield = "MetalShield";
    private const string ChainChest = "ChainChest";
    private const string ChainLegs = "ChainLegs";
    private const string ChainCoif = "ChainCoif";
    private const string LeatherGloves = "LeatherGloves";
    private const string Bow = "Bow";
    private const string LeatherChest = "LeatherChest";
    private const string Spellbook = "Spellbook";
    private const string Robe = "Robe";
    private const string Arrow = "Arrow";
    private const string WizardsHat = "WizardsHat";
    private const string HatchetLower = "hatchet";
    private const string PickaxeUpper = "PICKAXE";
    private const string FishingPoleMixed = "fishingPole";
    private const string KatanaLower = "katana";
    private const string ShirtLower = "shirt";
    private const string Empty = "";
    private const string Whitespace = " ";

    [Fact]
    public void ShouldGrantCombatKit_BeforeCareer_IsTrue() =>
        Assert.True(KitOnceRules.ShouldGrantCombatKit(false));

    [Fact]
    public void ShouldGrantCombatKit_AfterCareerStarted_IsFalse() =>
        Assert.False(KitOnceRules.ShouldGrantCombatKit(true));

    [Fact]
    public void ShouldApplyFreshBuild_BeforeCareer_IsTrue() =>
        Assert.True(KitOnceRules.ShouldApplyFreshBuild(false));

    [Fact]
    public void ShouldApplyFreshBuild_AfterCareerStarted_IsFalse() =>
        Assert.False(KitOnceRules.ShouldApplyFreshBuild(true));

    [Fact]
    public void SecondCallWithCareerStarted_DoesNotGrantCombatKit()
    {
        Assert.True(KitOnceRules.ShouldGrantCombatKit(false));

        // The character records that it was equipped, so nothing is granted again.
        const bool careerStarted = true;
        Assert.False(KitOnceRules.ShouldGrantCombatKit(careerStarted));
        Assert.False(KitOnceRules.ShouldApplyFreshBuild(careerStarted));
    }

    [Fact]
    public void IsWorkerTool_HatchetPickaxeFishingPole_IsTrue()
    {
        Assert.True(KitOnceRules.IsWorkerTool(KitOnceRules.Hatchet));
        Assert.True(KitOnceRules.IsWorkerTool(KitOnceRules.Pickaxe));
        Assert.True(KitOnceRules.IsWorkerTool(KitOnceRules.FishingPole));
        Assert.True(KitOnceRules.IsWorkerTool(HatchetLower));
        Assert.True(KitOnceRules.IsWorkerTool(PickaxeUpper));
        Assert.True(KitOnceRules.IsWorkerTool(FishingPoleMixed));
    }

    [Fact]
    public void IsWorkerTool_WeaponsArmorNullEmpty_IsFalse()
    {
        Assert.False(KitOnceRules.IsWorkerTool(Katana));
        Assert.False(KitOnceRules.IsWorkerTool(ChainChest));
        Assert.False(KitOnceRules.IsWorkerTool(Bow));
        Assert.False(KitOnceRules.IsWorkerTool(null));
        Assert.False(KitOnceRules.IsWorkerTool(Empty));
        Assert.False(KitOnceRules.IsWorkerTool(Whitespace));
    }

    [Fact]
    public void IsCombatKitPiece_KnownWeaponsAndArmor_IsTrue()
    {
        Assert.True(KitOnceRules.IsCombatKitPiece(Katana));
        Assert.True(KitOnceRules.IsCombatKitPiece(MetalShield));
        Assert.True(KitOnceRules.IsCombatKitPiece(ChainChest));
        Assert.True(KitOnceRules.IsCombatKitPiece(ChainLegs));
        Assert.True(KitOnceRules.IsCombatKitPiece(ChainCoif));
        Assert.True(KitOnceRules.IsCombatKitPiece(LeatherGloves));
        Assert.True(KitOnceRules.IsCombatKitPiece(Bow));
        Assert.True(KitOnceRules.IsCombatKitPiece(LeatherChest));
        Assert.True(KitOnceRules.IsCombatKitPiece(Spellbook));
        Assert.True(KitOnceRules.IsCombatKitPiece(Robe));
        Assert.True(KitOnceRules.IsCombatKitPiece(Arrow));
        Assert.True(KitOnceRules.IsCombatKitPiece(WizardsHat));
        Assert.True(KitOnceRules.IsCombatKitPiece(KatanaLower));
    }

    [Fact]
    public void IsCombatKitPiece_WorkerToolsClothesNullEmpty_IsFalse()
    {
        Assert.False(KitOnceRules.IsCombatKitPiece(KitOnceRules.Hatchet));
        Assert.False(KitOnceRules.IsCombatKitPiece(KitOnceRules.Pickaxe));
        Assert.False(KitOnceRules.IsCombatKitPiece(KitOnceRules.FishingPole));
        Assert.False(KitOnceRules.IsCombatKitPiece(OutfitRules.Shirt));
        Assert.False(KitOnceRules.IsCombatKitPiece(OutfitRules.LongPants));
        Assert.False(KitOnceRules.IsCombatKitPiece(OutfitRules.Boots));
        Assert.False(KitOnceRules.IsCombatKitPiece(ShirtLower));
        Assert.False(KitOnceRules.IsCombatKitPiece(null));
        Assert.False(KitOnceRules.IsCombatKitPiece(Empty));
        Assert.False(KitOnceRules.IsCombatKitPiece(Whitespace));
    }

    [Fact]
    public void GoesInBackpack_NoLayer_IsTrue()
    {
        Assert.True(KitOnceRules.GoesInBackpack(Layer.Invalid));
        Assert.False(KitOnceRules.GoesInBackpack(Layer.OneHanded));
        Assert.False(KitOnceRules.GoesInBackpack(Layer.Shoes));
    }

    [Fact]
    public void ShouldDress_Ghost_IsFalse()
    {
        Assert.False(KitOnceRules.ShouldDress(true));
        Assert.True(KitOnceRules.ShouldDress(false));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void IsNewbiedKitPiece_TheWeaponAndTheSpellbook(bool weapon, bool spellbook, bool newbied) =>
        Assert.Equal(newbied, KitOnceRules.IsNewbiedKitPiece(weapon, spellbook));
}
