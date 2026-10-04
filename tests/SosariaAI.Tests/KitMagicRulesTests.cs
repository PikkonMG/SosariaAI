using System.Linq;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class KitMagicRulesTests
{
    private const int People = 400;
    private const int RareDivisor = 10;
    private const double VeteranWeaponShare = 0.4;
    private const PersonWealth Purse = PersonWealth.Modest;

    [Fact]
    public void Novices_NeverStartWithMagic()
    {
        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            Assert.Equal(WeaponDamageLevel.Regular, KitMagicRules.WeaponDamage(SkillTier.Novice, Purse, id));
            Assert.Equal(ArmorProtectionLevel.Regular, KitMagicRules.ArmorProtection(SkillTier.Novice, Purse, id));
        }
    }

    [Theory]
    [InlineData(SkillTier.Apprentice)]
    [InlineData(SkillTier.Journeyman)]
    public void LowTiers_RarelyOwnMagic(SkillTier tier)
    {
        var magic = Enumerable.Range(0, People).Count(i => KitMagicRules.HasMagicWeapon(tier, Purse, $"Felucca:p#{i}"));

        Assert.InRange(magic, 1, People / RareDivisor);
    }

    [Fact]
    public void Veterans_CommonlyOwnMagic()
    {
        var weapons = Enumerable.Range(0, People)
            .Count(i => KitMagicRules.WeaponDamage(SkillTier.Grandmaster, Purse, $"Felucca:p#{i}") != WeaponDamageLevel.Regular);
        var armor = Enumerable.Range(0, People)
            .Count(i => KitMagicRules.ArmorProtection(SkillTier.Master, Purse, $"Felucca:p#{i}") != ArmorProtectionLevel.Regular);

        Assert.True(weapons > People * VeteranWeaponShare, weapons.ToString());
        Assert.True(armor > People / RareDivisor, armor.ToString());
    }

    [Fact]
    public void MagicOdds_RiseWithEveryTier()
    {
        for (var tier = SkillTier.Apprentice; tier <= SkillTier.Grandmaster; tier++)
        {
            Assert.True(KitMagicRules.WeaponPercentByTier[(int)tier] > KitMagicRules.WeaponPercentByTier[(int)tier - 1]);
            Assert.True(KitMagicRules.ArmorPercentByTier[(int)tier] > KitMagicRules.ArmorPercentByTier[(int)tier - 1]);
        }
    }

    [Theory]
    [InlineData(SkillTier.Apprentice)]
    [InlineData(SkillTier.Journeyman)]
    [InlineData(SkillTier.Expert)]
    public void BelowAdept_NeverRollsAboveMight(SkillTier tier)
    {
        for (var i = 0; i < People; i++)
        {
            Assert.True(KitMagicRules.WeaponDamage(tier, Purse, $"Felucca:p#{i}") <= WeaponDamageLevel.Might);
            Assert.True(KitMagicRules.ArmorProtection(tier, Purse, $"Felucca:p#{i}") <= ArmorProtectionLevel.Guarding);
        }
    }

    [Theory]
    [InlineData(EraBand.T2A, false)]
    [InlineData(EraBand.ML, true)]
    [InlineData(EraBand.Modern, true)]
    public void ItemProperties_OnlyAfterTheSecondAge(EraBand band, bool properties) =>
        Assert.Equal(properties, KitMagicRules.UsesItemProperties(band));

    [Fact]
    public void AosMagic_GrowsWithTheTier()
    {
        var tiers = new[] { SkillTier.Expert, SkillTier.Adept, SkillTier.Master, SkillTier.Grandmaster };

        for (var i = 1; i < tiers.Length; i++)
        {
            var lower = KitMagicRules.AosMagicFor(tiers[i - 1]);
            var higher = KitMagicRules.AosMagicFor(tiers[i]);

            Assert.True(higher.Properties > lower.Properties);
            Assert.True(higher.MaxIntensity > lower.MaxIntensity);
            Assert.True(higher.MinIntensity <= higher.MaxIntensity);
        }
    }

    [Fact]
    public void MagicOdds_AreTheSameInEveryEra()
    {
        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            Assert.Equal(KitMagicRules.HasMagicWeapon(SkillTier.Master, Purse, id), KitMagicRules.WeaponDamage(SkillTier.Master, Purse, id) != WeaponDamageLevel.Regular);
            Assert.Equal(KitMagicRules.HasMagicArmor(SkillTier.Master, Purse, id), KitMagicRules.ArmorProtection(SkillTier.Master, Purse, id) != ArmorProtectionLevel.Regular);
        }
    }

    [Fact]
    public void RichPeople_OwnMoreMagic_AndNoviceNoneWhateverThePurse()
    {
        var poor = Enumerable.Range(0, People).Count(i => KitMagicRules.HasMagicWeapon(SkillTier.Master, PersonWealth.Poor, $"Felucca:p#{i}"));
        var rich = Enumerable.Range(0, People).Count(i => KitMagicRules.HasMagicWeapon(SkillTier.Master, PersonWealth.Rich, $"Felucca:p#{i}"));

        Assert.True(rich > poor * 2, $"{poor} poor, {rich} rich");
        Assert.Equal(0, KitMagicRules.Percent(KitMagicRules.WeaponPercentByTier, SkillTier.Novice, PersonWealth.Rich));
        Assert.Equal(0, KitMagicRules.Percent(KitMagicRules.ArmorPercentByTier, SkillTier.Novice, PersonWealth.Rich));
    }
}
