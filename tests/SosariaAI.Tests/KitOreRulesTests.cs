using System.Linq;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>Coloured ore is a veteran's luxury, commoner in rich hands, and valorite is a legend.</summary>
public class KitOreRulesTests
{
    private const int People = 2000;

    [Fact]
    public void NewPlayers_WearIron_WhateverThePurse()
    {
        for (var i = 0; i < People; i++)
        {
            Assert.Equal(CraftResource.Iron, KitOreRules.OreFor(SkillTier.Novice, PersonWealth.Rich, $"Felucca:p#{i}"));
            Assert.Equal(CraftResource.Iron, KitOreRules.OreFor(SkillTier.Apprentice, PersonWealth.Rich, $"Felucca:p#{i}"));
        }
    }

    [Fact]
    public void RichGrandmasters_OftenWearColour_AndDullCopperOutnumbersValorite()
    {
        var ores = Enumerable.Range(0, People)
            .Select(i => KitOreRules.OreFor(SkillTier.Grandmaster, PersonWealth.Rich, $"Felucca:p#{i}"))
            .ToList();
        var poor = Enumerable.Range(0, People)
            .Count(i => KitOreRules.OreFor(SkillTier.Grandmaster, PersonWealth.Poor, $"Felucca:p#{i}") != CraftResource.Iron);
        var coloured = ores.Count(ore => ore != CraftResource.Iron);

        Assert.True(coloured > poor, $"{coloured} rich, {poor} poor");
        Assert.True(ores.Count(ore => ore == CraftResource.DullCopper) > ores.Count(ore => ore == CraftResource.Valorite));
        Assert.All(ores, ore => Assert.InRange(ore, CraftResource.Iron, CraftResource.Valorite));
    }

    [Fact]
    public void OreTables_CoverEveryTierAndPurse()
    {
        Assert.Equal(System.Enum.GetValues<SkillTier>().Length, KitOreRules.OrePercentByTier.Length);
        Assert.Equal(System.Enum.GetValues<PersonWealth>().Length, KitOreRules.WealthBonus.Length);
    }
}
