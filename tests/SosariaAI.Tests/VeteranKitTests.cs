using System;
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
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Novice, PersonWealth.Poor), Template(PersonClass.Warrior), "Felucca:p#1");

        Assert.DoesNotContain(pack, stack => stack.TypeName.Contains("Potion"));
        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.Bandage);
    }

    [Fact]
    public void GrandmasterDexxer_CarriesGreaterHealsCuresAndRefreshes()
    {
        var pack = VeteranKit.For(Profile(PersonClass.Warrior, SkillTier.Grandmaster, PersonWealth.Rich), Template(PersonClass.Warrior), "Felucca:p#1");

        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.GreaterHealPotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.GreaterCurePotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.RefreshPotion);
        Assert.Contains(pack, stack => stack.TypeName == VeteranKit.RecallScroll && stack.Amount == VeteranKit.RichScrolls);
    }

    [Fact]
    public void Casters_CarryNoRecallScrolls()
    {
        var mage = Template(PersonClass.Healer);
        var pack = VeteranKit.For(Profile(PersonClass.Healer, SkillTier.Master, PersonWealth.Rich), mage, "Felucca:p#1");

        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
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
                foreach (var stack in VeteranKit.For(Profile(personClass, tier, PersonWealth.Rich), Template(personClass), "Felucca:p#2"))
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
            "Felucca:p#1"
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
        var pack = VeteranKit.For(Profile(personClass, SkillTier.Grandmaster, PersonWealth.Rich), template, "Felucca:p#1");

        Assert.False(template.TravelMagic);
        Assert.DoesNotContain(pack, stack => stack.TypeName == VeteranKit.RecallScroll);
    }

    [Fact]
    public void NoKit_CarriesARunebook_TheRuneKitMakesTheBook()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            var pack = VeteranKit.For(Profile(personClass, SkillTier.Grandmaster, PersonWealth.Rich), Template(personClass), "Felucca:p#3");

            Assert.DoesNotContain(pack, stack => stack.TypeName == "Runebook");
        }
    }
}
