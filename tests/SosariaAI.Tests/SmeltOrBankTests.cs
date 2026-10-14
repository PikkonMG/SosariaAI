using Server;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SmeltOrBankTests
{
    private const double GrandmasterMining = 100.0;
    private const double SkillStep = 0.1;
    private const int OneTile = 1;

    private static readonly Point3D Forge = new(636, 2193, 0);

    /// <summary>Where the Skara Brae seller stood when its walk to the forge ended, three tiles off.</summary>
    private static readonly Point3D SkaraSeller = new(633, 2192, 0);

    [Fact]
    public void MaySmelt_OnlyAMiningAboveTheBandsFloorGivesTheSmeltAChance()
    {
        var floor = SmeltRules.MinSkill(SmeltRules.IronDifficulty);

        Assert.False(SmeltOrBank.MaySmelt(0));
        Assert.False(SmeltOrBank.MaySmelt(floor));
        Assert.True(SmeltOrBank.MaySmelt(floor + SkillStep));
        Assert.True(SmeltOrBank.MaySmelt(GrandmasterMining));
    }

    [Fact]
    public void BanksHaul_OreAMinerCannotSmeltGoesInTheBox()
    {
        var floor = SmeltRules.MinSkill(SmeltRules.IronDifficulty);

        Assert.True(SmeltOrBank.BanksHaul(sellableHarvest: true, isOre: true, noOutlet: false, floor));
        Assert.False(SmeltOrBank.BanksHaul(sellableHarvest: true, isOre: true, noOutlet: false, GrandmasterMining));
        Assert.False(SmeltOrBank.BanksHaul(sellableHarvest: true, isOre: false, noOutlet: false, floor));
    }

    [Fact]
    public void BanksHaul_AHaulNoForgeOrCounterTookGoesInTheBox()
    {
        // Tilda Hallow banked nothing of her 27 ore: the box left it for a smith behind a
        // forge she never reached.
        Assert.True(SmeltOrBank.BanksHaul(sellableHarvest: true, isOre: true, noOutlet: true, GrandmasterMining));
        Assert.True(SmeltOrBank.BanksHaul(sellableHarvest: true, isOre: false, noOutlet: true, GrandmasterMining));
    }

    [Fact]
    public void BanksHaul_LeavesWhatIsNoSellableHarvestToTheOtherRules()
    {
        Assert.False(SmeltOrBank.BanksHaul(sellableHarvest: false, isOre: false, noOutlet: true, 0));
    }

    [Fact]
    public void NearestStand_PicksTheTileInReachNearestTheSmelter()
    {
        var near = new Point3D(Forge.X - SmeltRules.ForgeRange, Forge.Y - OneTile, Forge.Z);
        var far = new Point3D(Forge.X + SmeltRules.ForgeRange, Forge.Y, Forge.Z);

        Assert.True(SmeltOrBank.NearestStand([far, near], Forge, SkaraSeller, out var stand));
        Assert.Equal(near, stand);
        Assert.True(NavMetric.Chebyshev(stand, Forge) <= SmeltRules.ForgeRange);
    }

    [Fact]
    public void NearestStand_OnATieTheTileNearerTheForge()
    {
        var edge = new Point3D(Forge.X - SmeltRules.ForgeRange, Forge.Y, Forge.Z);
        var beside = new Point3D(Forge.X - OneTile, Forge.Y, Forge.Z);
        var smelter = new Point3D(Forge.X - SmeltRules.ForgeRange - OneTile, Forge.Y - SmeltRules.ForgeRange, Forge.Z);

        Assert.True(SmeltOrBank.NearestStand([edge, beside], Forge, smelter, out var stand));
        Assert.Equal(beside, stand);
    }

    [Fact]
    public void NearestStand_NeverTheForgeTile_AndNoneWithoutATileThatStands()
    {
        Assert.False(SmeltOrBank.NearestStand([Forge], Forge, SkaraSeller, out _));
        Assert.False(SmeltOrBank.NearestStand([], Forge, SkaraSeller, out _));
    }
}
