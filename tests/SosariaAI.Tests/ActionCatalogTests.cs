using System.Linq;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ActionCatalogTests
{
    [Fact]
    public void From_Woodcutter_SkipsDecideAndKeepsWorkSteps()
    {
        var file = CharactersFile.CreateDefault();
        var connor = file.Facets[FacetNames.Felucca].Roster[0];
        var actions = ActionCatalog.From(connor, catalog: null);

        Assert.Contains(actions, a => a.SkillKind == SkillKinds.GoTo);
        Assert.Contains(actions, a => a.SkillKind == SkillKinds.Lumberjack);
        Assert.Contains(actions, a => a.SkillKind == SkillKinds.VendorSell);
        Assert.Contains(actions, a => a.SkillKind == SkillKinds.BankDeposit);
        Assert.Contains(actions, a => a.SkillKind == SkillKinds.Heal);
        Assert.DoesNotContain(actions, a => a.SkillKind == SkillKinds.Decide);
        Assert.True(ActionId.From("work", connor.Routines["work"][0], 0).Value !=
                    ActionId.From("work", connor.Routines["work"][3], 3).Value);
    }

    [Fact]
    public void From_APersonWithNoShopStep_MayRestockSupplies()
    {
        // A fighter low on bandages rolled the shop job four times as often, found no buy
        // step and stood "looking over the wares" while its hunts and delves were cut.
        var roster = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster;
        var fighter = roster.First(c => PersonJobs.Of(c.Build) == PersonJobs.Fighter && !c.UsesSkill(SkillKinds.VendorBuy));
        var shopper = roster.First(c => c.UsesSkill(SkillKinds.VendorBuy));

        Assert.Contains(ActionCatalog.From(fighter, catalog: null), a => a.Id.Value == ActionCatalog.RestockId);
        Assert.DoesNotContain(ActionCatalog.From(shopper, catalog: null), a => a.Id.Value == ActionCatalog.RestockId);
    }

    [Fact]
    public void From_EveryPerson_MayMeditateOnce()
    {
        // Meditation was only a routine step; a mage out of mana between jobs never sat down.
        var roster = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster;

        foreach (var person in roster)
        {
            var actions = ActionCatalog.From(person, catalog: null);
            Assert.Single(actions, a => a.SkillKind == SkillKinds.Meditate);
        }
    }
}
