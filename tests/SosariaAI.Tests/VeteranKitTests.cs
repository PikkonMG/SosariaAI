using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class VeteranKitTests
{
    private static PersonProfile Profile(PersonClass personClass, SkillTier tier, PersonWealth wealth) =>
        new(personClass, tier, PersonTrait.None, wealth, ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female: false);

    private static ClassBuildTemplate Template(PersonClass personClass) =>
        ClassBuilds.TemplateFor(personClass, CharacterRole.Fighter, "Felucca:p#1");

    [Fact]
    public void Novice_CarriesNoPotionsAndNoScrolls()
    {
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Novice, PersonWealth.Poor), Template(PersonClass.Warrior), "Felucca:p#1", pvpMinded: false);

        Assert.DoesNotContain(pack, stack => stack.TypeName.Contains("Potion"));
        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.Bandage);
    }

    [Fact]
    public void GrandmasterDexxer_CarriesGreaterHealsCuresAndRefreshes()
    {
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Grandmaster, PersonWealth.Rich), Template(PersonClass.Warrior), "Felucca:p#1", pvpMinded: false);

        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.GreaterHealPotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.GreaterCurePotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.RefreshPotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.RecallScroll && stack.Amount == VeteranKit.RichScrolls);
    }

    [Fact]
    public void Casters_CarryNoRecallScrolls()
    {
        var mage = Template(PersonClass.Healer);
        var pack = VeteranKit.For(Profile(PersonClass.Healer, SkillTier.Master, PersonWealth.Rich), mage, "Felucca:p#1", pvpMinded: false);

        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
    }

    [Fact]
    public void PvpMinded_RedsAndFighters()
    {
        Assert.True(VeteranKit.PvpMinded(red: true, CharacterRole.Worker));
        Assert.True(VeteranKit.PvpMinded(red: false, CharacterRole.Fighter));
        Assert.False(VeteranKit.PvpMinded(red: false, CharacterRole.Worker));
        Assert.False(VeteranKit.PvpMinded(red: false, null));
    }

    [Fact]
    public void PvpMage_CarriesExplosionPotionsAndTrappedPouches()
    {
        var mage = Template(PersonClass.Mage);
        var pack = VeteranKit.For(Profile(PersonClass.Mage, SkillTier.Grandmaster, PersonWealth.Rich), mage, "Felucca:p#1", pvpMinded: true);

        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.GreaterExplosionPotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.TrappedPouch);
    }

    [Fact]
    public void PvpDexxer_CarriesNoExplosionPotions_ItWouldWalkIntoTheBlast()
    {
        var warrior = Template(PersonClass.Warrior);
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Grandmaster, PersonWealth.Rich), warrior, "Felucca:p#1", pvpMinded: true);

        Assert.Equal(CombatStyle.Melee, warrior.Style);
        Assert.DoesNotContain(pack, stack => stack.TypeName.Contains("Explosion"));
        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.TrappedPouch);
    }

    [Fact]
    public void PvpTools_NotForTheUnminded_NorTheGreen()
    {
        var mage = Template(PersonClass.Mage);
        var calm = VeteranKit.For(Profile(PersonClass.Mage, SkillTier.Grandmaster, PersonWealth.Rich), mage, "Felucca:p#1", pvpMinded: false);
        var green = VeteranKit.For(Profile(PersonClass.Mage, SkillTier.Apprentice, PersonWealth.Rich), mage, "Felucca:p#1", pvpMinded: true);

        Assert.DoesNotContain(calm, stack => stack.TypeName.Contains("Explosion") || stack.TypeName == VeteranKit.TrappedPouch);
        Assert.DoesNotContain(green, stack => stack.TypeName.Contains("Explosion") || stack.TypeName == VeteranKit.TrappedPouch);
    }

    [Fact]
    public void TankMage_CarriesTrappedPouchesNotExplosionPotions()
    {
        var tankMage = new ClassBuildTemplate
        {
            Primary = SkillName.Magery,
            Secondary = [SkillName.Swords, SkillName.Tactics, SkillName.MagicResist],
            Style = CombatStyle.Melee
        };
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Master, PersonWealth.Rich), tankMage, "Felucca:p#1", pvpMinded: true);

        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.TrappedPouch);
        Assert.DoesNotContain(pack, stack => stack.TypeName.Contains("Explosion"));
    }

    [Fact]
    public void Purse_GrowsWithWealth()
    {
        for (var i = 0; i < 50; i++)
        {
            var id = $"Felucca:p#{i}";
            Assert.True(VeteranKit.Purse(PersonWealth.Poor, id) < VeteranKit.Purse(PersonWealth.Rich, id));
            Assert.InRange(VeteranKit.Purse(PersonWealth.Modest, id), VeteranKit.ModestPurse, VeteranKit.ComfortablePurse);
        }
    }

    [Fact]
    public void EveryStack_IsARealItem_WithAPositiveAmount()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            foreach (var tier in Enum.GetValues<SkillTier>())
            {
                foreach (var stack in VeteranKit.For(Profile(personClass, tier, PersonWealth.Rich), Template(personClass), "Felucca:p#2", pvpMinded: true))
                {
                    Assert.True(ContentTypes.IsItem(stack.TypeName), stack.TypeName);
                    Assert.True(stack.Amount > 0, stack.TypeName);
                }
            }
        }
    }

    [Fact]
    public void BandageTable_CoversEveryTier() =>
        Assert.Equal(Enum.GetValues<SkillTier>().Length, VeteranKit.BandagesByTier.Length);

    [Fact]
    public void For_Tamer_CarriesPetFoodAndBandages()
    {
        var kit = VeteranKit.For(
            Profile(PersonClass.Tamer, SkillTier.Journeyman, PersonWealth.Modest),
            Template(PersonClass.Tamer),
            "Felucca:p#1",
            pvpMinded: false
        );

        Assert.Contains(kit, stack => stack.TypeName == VeteranKit.RawRibs && stack.Amount == PetFoodRules.PantryTarget);
        Assert.Contains(kit, stack => stack.TypeName == VeteranKit.Bandage);
    }

    [Theory]
    [InlineData(PersonClass.Ranger)]
    [InlineData(PersonClass.Thief)]
    [InlineData(PersonClass.Ninja)]
    public void NoMagery_CarriesNoRecallScrolls_EvenWhenRich(PersonClass personClass)
    {
        var template = Template(personClass);
        var pack = VeteranKit.For(Profile(personClass, SkillTier.Grandmaster, PersonWealth.Rich), template, "Felucca:p#1", pvpMinded: false);

        Assert.False(template.TravelMagic);
        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
    }

    [Fact]
    public void NoKit_CarriesARunebook_TheRuneKitMakesTheBook()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            var pack = VeteranKit.For(Profile(personClass, SkillTier.Grandmaster, PersonWealth.Rich), Template(personClass), "Felucca:p#3", pvpMinded: false);

            Assert.DoesNotContain(pack, stack => stack.TypeName == "Runebook");
        }
    }
}
