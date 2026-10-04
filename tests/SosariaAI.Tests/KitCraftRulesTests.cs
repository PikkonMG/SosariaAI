using System.Collections.Generic;
using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The 1999 look: most people wear exceptional work bought from a few grandmaster
/// crafters, veterans more than novices, and the maker's mark is a player's name.
/// </summary>
public class KitCraftRulesTests
{
    private const int People = 400;
    private const int ChestPiece = 13;
    private const int MostPercent = 50;

    [Fact]
    public void ExceptionalOdds_RiseWithEveryTier()
    {
        for (var tier = SkillTier.Apprentice; tier <= SkillTier.Grandmaster; tier++)
        {
            Assert.True(KitCraftRules.ExceptionalPercent(tier) > KitCraftRules.ExceptionalPercent(tier - 1));
        }
    }

    [Fact]
    public void MostJourneymenWearExceptionalWork()
    {
        var exceptional = Enumerable.Range(0, People)
            .Count(i => KitCraftRules.IsExceptional(SkillTier.Journeyman, $"Felucca:p#{i}", ChestPiece));

        Assert.True(exceptional * 100 > People * MostPercent, exceptional.ToString());
    }

    [Fact]
    public void Grandmasters_WearMoreExceptionalWorkThanNovices()
    {
        var novices = Enumerable.Range(0, People).Count(i => KitCraftRules.IsExceptional(SkillTier.Novice, $"Felucca:p#{i}", ChestPiece));
        var grandmasters = Enumerable.Range(0, People).Count(i => KitCraftRules.IsExceptional(SkillTier.Grandmaster, $"Felucca:p#{i}", ChestPiece));

        Assert.True(grandmasters > novices);
    }

    [Fact]
    public void MakerName_IsAValidPlayerName_AndStableForOnePerson()
    {
        var first = KitCraftRules.MakerName(SmithRules.Trade.Kind, "Felucca:p#7");

        Assert.True(PlayerNameRules.IsValid(first), first);
        Assert.Equal(first, KitCraftRules.MakerName(SmithRules.Trade.Kind, "Felucca:p#7"));
    }

    [Fact]
    public void AFewMakers_DressTheWholeShard()
    {
        var smiths = new HashSet<string>();

        for (var i = 0; i < People; i++)
        {
            smiths.Add(KitCraftRules.MakerId(SmithRules.Trade.Kind, $"Felucca:p#{i}"));
        }

        Assert.InRange(smiths.Count, 2, KitCraftRules.MakersPerTrade);
    }

    [Fact]
    public void EachTrade_HasItsOwnMakers() =>
        Assert.NotEqual(
            KitCraftRules.MakerId(SmithRules.Trade.Kind, "Felucca:p#3"),
            KitCraftRules.MakerId(TailorRules.Trade.Kind, "Felucca:p#3")
        );
}
