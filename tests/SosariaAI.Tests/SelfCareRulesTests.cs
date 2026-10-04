using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class SelfCareRulesTests
{
    private const double FullHits = 1.0;
    private const double Scratched = 0.8;
    private const double Wounded = 0.55;
    private const double Dying = 0.3;
    private const double Grandmaster = 100;
    private const double NoSkill = 0;
    private const double LowHealing = 40;
    private const int FullMana = 100;
    private const int NoMana = 0;
    private const int OpenGround = CastTiming.TopCircle;
    private const int Pressed = CastTiming.FirstCircle;

    [Fact]
    public void NeedsCare_CleanAndAboveLine_IsFalse()
    {
        Assert.False(SelfCareRules.NeedsCare(false, FullHits, inFight: true));
        Assert.False(SelfCareRules.NeedsCare(false, Scratched, inFight: true));
        Assert.True(SelfCareRules.NeedsCare(false, Scratched, inFight: false));
        Assert.True(SelfCareRules.NeedsCare(true, FullHits, inFight: true));
    }

    [Fact]
    public void Choose_Poisoned_CureSpellFirst() =>
        Assert.Equal(CareChoice.CureSpell, SelfCareRules.Choose(Everything() with { Poisoned = true }));

    [Fact]
    public void Choose_PoisonedNoMagery_CurePotion() =>
        Assert.Equal(
            CareChoice.CurePotion,
            SelfCareRules.Choose(Everything() with { Poisoned = true, Magery = NoSkill })
        );

    [Fact]
    public void Choose_PoisonedOnlyBandage_NeedsHealingAndAnatomy()
    {
        var bandageOnly = Everything() with { Poisoned = true, Magery = NoSkill, HasCurePotion = false };

        Assert.Equal(CareChoice.Bandage, SelfCareRules.Choose(bandageOnly));
        Assert.Equal(CareChoice.None, SelfCareRules.Choose(bandageOnly with { Healing = LowHealing }));
    }

    [Fact]
    public void Choose_PoisonedAndPressed_SkipsSecondCircleCure() =>
        Assert.Equal(
            CareChoice.CurePotion,
            SelfCareRules.Choose(Everything() with { Poisoned = true, SafeCircle = Pressed })
        );

    [Fact]
    public void Choose_DyingInFight_DrinksFirst() =>
        Assert.Equal(CareChoice.HealPotion, SelfCareRules.Choose(Everything() with { HitsFraction = Dying }));

    [Fact]
    public void Choose_WoundedInFight_Bandages() =>
        Assert.Equal(CareChoice.Bandage, SelfCareRules.Choose(Everything() with { HitsFraction = Wounded }));

    [Fact]
    public void Choose_MageWithoutBandages_GreaterHealOnADeepWound() =>
        Assert.Equal(
            CareChoice.GreaterHealSpell,
            SelfCareRules.Choose(Everything() with { HitsFraction = Wounded, HasBandage = false })
        );

    [Fact]
    public void Choose_MagePressed_OnlyTheFirstCircleHeal() =>
        Assert.Equal(
            CareChoice.HealSpell,
            SelfCareRules.Choose(Everything() with { HitsFraction = Wounded, HasBandage = false, SafeCircle = Pressed })
        );

    [Fact]
    public void Choose_ASwingWindow_LetsGreaterHealThrough() =>
        Assert.Equal(
            CareChoice.GreaterHealSpell,
            SelfCareRules.Choose(
                Everything() with { HitsFraction = Wounded, HasBandage = false, SafeCircle = SpellBook.GreaterHeal.Circle }
            )
        );

    [Fact]
    public void WantsRoom_OnlyWhenTheBestCareIsASpellThatDoesNotFit()
    {
        var mage = Everything() with { HitsFraction = Wounded, HasBandage = false, HasHealPotion = false };

        Assert.True(SelfCareRules.WantsRoom(mage with { SafeCircle = Pressed }));
        Assert.False(SelfCareRules.WantsRoom(mage));
        Assert.False(SelfCareRules.WantsRoom(Everything() with { HitsFraction = Wounded, SafeCircle = Pressed }));
    }

    [Fact]
    public void Choose_OnCooldownOrOutOfMana_None() =>
        Assert.Equal(
            CareChoice.None,
            SelfCareRules.Choose(
                Everything() with
                {
                    HitsFraction = Dying,
                    PotionReady = false,
                    BandageReady = false,
                    Mana = NoMana
                }
            )
        );

    [Fact]
    public void Choose_AtRest_TopsUpWithABandageNotAPotion()
    {
        var resting = Everything() with { InFight = false, HitsFraction = Scratched };

        Assert.Equal(CareChoice.Bandage, SelfCareRules.Choose(resting));
        Assert.Equal(CareChoice.HealSpell, SelfCareRules.Choose(resting with { HasBandage = false }));
    }

    [Fact]
    public void Choose_Healthy_None() =>
        Assert.Equal(CareChoice.None, SelfCareRules.Choose(Everything()));

    [Fact]
    public void Choose_NoHealingSkill_NeverBandages() =>
        Assert.Equal(
            CareChoice.GreaterHealSpell,
            SelfCareRules.Choose(Everything() with { HitsFraction = Wounded, Healing = NoSkill })
        );

    [Fact]
    public void ShouldMeditate_CasterSitsLow_AndGetsUpRested()
    {
        const double Low = 0.4;
        const double Middle = 0.7;
        const double Rested = 0.95;

        Assert.True(SelfCareRules.ShouldMeditate(meditating: false, caster: true, Low));
        Assert.False(SelfCareRules.ShouldMeditate(meditating: false, caster: true, Middle));
        Assert.True(SelfCareRules.ShouldMeditate(meditating: true, caster: true, Middle));
        Assert.False(SelfCareRules.ShouldMeditate(meditating: true, caster: true, Rested));
        Assert.False(SelfCareRules.ShouldMeditate(meditating: false, caster: false, Low));
    }

    [Fact]
    public void HasRemedy_NothingInThePack_IsFalse()
    {
        var hurt = Everything() with { HitsFraction = Dying, InFight = false };
        var empty = hurt with { HasBandage = false, HasHealPotion = false, Magery = NoSkill };

        Assert.True(SelfCareRules.HasRemedy(hurt));
        Assert.False(SelfCareRules.HasRemedy(empty));
        Assert.True(SelfCareRules.HasRemedy(empty with { HasBandage = true }));
    }

    private static CareFacts Everything() =>
        new(
            Poisoned: false,
            HitsFraction: FullHits,
            InFight: true,
            SafeCircle: OpenGround,
            SpellReady: true,
            Magery: Grandmaster,
            Mana: FullMana,
            PotionReady: true,
            HasCurePotion: true,
            HasHealPotion: true,
            BandageReady: true,
            HasBandage: true,
            Healing: Grandmaster,
            Anatomy: Grandmaster
        );
}
