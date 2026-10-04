using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SupplyRulesTests
{
    private const double NoMagery = 0;
    private const double CasterMagery = 80;
    private const int PoorPurse = 100;
    private const int ModestPurse = 2000;
    private const int ComfortablePurse = 8000;
    private const int RichPurse = 50000;

    private static SupplyProfile Archer(int arrows) =>
        new(true, false, arrows, 0, false, 0, false, 0, NoMagery, PoorPurse, 0);

    [Fact]
    public void LowNeeds_ArcherBelowTheLowMark_NeedsArrows()
    {
        var needs = SupplyRules.LowNeeds(Archer(SupplyRules.ArrowLowMark - 1));

        var need = Assert.Single(needs);
        Assert.Equal(SupplyKind.Arrows, need.Kind);
        Assert.Equal(SupplyRules.ArrowTarget - (SupplyRules.ArrowLowMark - 1), need.Shortfall);
    }

    [Fact]
    public void KeepOf_TheHighestTargetAmongTheKindsItBurns_NoneOfAKindItDoesNot()
    {
        var caster = new SupplyProfile(false, false, 0, 0, false, 0, true, 0, CasterMagery, PoorPurse, 0, TravelCasts: true);
        var fighter = new SupplyProfile(false, false, 0, 0, false, 0, false, 0, NoMagery, PoorPurse, 0);
        var pearl = SupplyMarket.KindsOf(typeof(Server.Items.BlackPearl));

        Assert.Equal([SupplyKind.Reagents, SupplyKind.TravelReagents], pearl);
        Assert.Equal(SupplyRules.ReagentTarget, SupplyRules.KeepOf(caster, pearl));
        Assert.Equal(SupplyRules.NoTarget, SupplyRules.KeepOf(fighter, pearl));
    }

    [Fact]
    public void Spare_OnlyWhatLiesPastTheKeep()
    {
        Assert.Equal(SupplyRules.BandageTarget, SupplyRules.Spare(SupplyRules.BandageTarget * 2, SupplyRules.BandageTarget));
        Assert.Equal(0, SupplyRules.Spare(SupplyRules.BandageTarget - 1, SupplyRules.BandageTarget));
        Assert.Equal(SupplyRules.BandageTarget, SupplyRules.Spare(SupplyRules.BandageTarget, SupplyRules.NoTarget));
    }

    [Fact]
    public void LowNeeds_ArcherAtTheLowMark_IsStocked() =>
        Assert.Empty(SupplyRules.LowNeeds(Archer(SupplyRules.ArrowLowMark)));

    [Fact]
    public void LowNeeds_NonArcher_NeverNeedsArrows() =>
        Assert.Empty(SupplyRules.LowNeeds(new SupplyProfile(false, false, 0, 0, false, 0, false, 0, NoMagery, PoorPurse, 0)));

    [Fact]
    public void LowNeeds_MostUrgentFirst()
    {
        var profile = new SupplyProfile(false, true, 0, 0, true, 0, true, 0, CasterMagery, ModestPurse, 0);

        var needs = SupplyRules.LowNeeds(profile);

        Assert.Equal(
            [SupplyKind.Bolts, SupplyKind.Bandages, SupplyKind.Reagents, SupplyKind.RecallScrolls],
            needs.ConvertAll(need => need.Kind)
        );
    }

    [Fact]
    public void AnyStopsFighting_RecallScrollsAloneNeverEndAFight()
    {
        // No shop sells recall scrolls. A person short only of them was "low" for good: every
        // hunt ended at once and every delve was cut to a fifth.
        var scrollsOnly = new SupplyProfile(false, false, 0, 0, false, 0, false, 0, CasterMagery, ModestPurse, 0);
        var noBandages = scrollsOnly with { Heals = true };

        Assert.Equal(SupplyKind.RecallScrolls, Assert.Single(SupplyRules.LowNeeds(scrollsOnly)).Kind);
        Assert.False(SupplyRules.AnyStopsFighting(SupplyRules.LowNeeds(scrollsOnly)));
        Assert.True(SupplyRules.AnyStopsFighting(SupplyRules.LowNeeds(noBandages)));
        Assert.False(SupplyRules.AnyStopsFighting(null));
    }

    [Theory]
    [InlineData(SupplyKind.Arrows, new[] { ShopFinder.BowyerToken, ShopFinder.ProvisionerToken })]
    [InlineData(SupplyKind.Bandages, new[] { SupplyRules.HealerToken })]
    [InlineData(SupplyKind.Reagents, new[] { ShopFinder.MageToken, ShopFinder.AlchemistToken })]
    [InlineData(SupplyKind.RecallScrolls, new string[0])]
    public void ShopTokens_FirstChoiceThenTheOtherShop(SupplyKind kind, string[] tokens) =>
        Assert.Equal(tokens, SupplyRules.ShopTokens(kind));

    [Fact]
    public void RedRefillable_KeepsWhatTheBankLiftsOrADenShopSells()
    {
        var reagents = new SupplyNeed(SupplyKind.Reagents, 0, SupplyRules.ReagentTarget);
        var bandages = new SupplyNeed(SupplyKind.Bandages, 0, SupplyRules.BandageTarget);
        var arrows = new SupplyNeed(SupplyKind.Arrows, 0, SupplyRules.ArrowTarget);
        SupplyNeed[] needs = [arrows, bandages, reagents];
        static bool DenHealerAndProvisioner(SupplyNeed need) => need.Kind is SupplyKind.Arrows or SupplyKind.Bandages;

        var nothingBanked = SupplyRules.RedRefillable(needs, _ => false, DenHealerAndProvisioner);
        var reagentsBanked = SupplyRules.RedRefillable(needs, need => need.Kind == SupplyKind.Reagents, DenHealerAndProvisioner);

        Assert.Equal([arrows, bandages], nothingBanked);
        Assert.Equal([arrows, bandages, reagents], reagentsBanked);
        Assert.False(SupplyRules.AnyStopsFighting(SupplyRules.RedRefillable([reagents], _ => false, DenHealerAndProvisioner)));
        Assert.Empty(SupplyRules.RedRefillable(needs, _ => false, _ => false));
        Assert.Empty(SupplyRules.RedRefillable(null, _ => true, _ => true));
    }
}
