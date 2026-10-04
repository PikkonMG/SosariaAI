using System;
using System.Collections.Generic;
using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class OutfitRulesTests
{
    private const int People = 200;

    private static PersonProfile Profile(PersonClass personClass, PersonWealth wealth, bool female) =>
        new(personClass, SkillTier.Expert, PersonTrait.None, wealth, ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female);

    [Fact]
    public void EveryPiece_IsARealItem_WithAnEraHue()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            foreach (var wealth in Enum.GetValues<PersonWealth>())
            {
                for (var i = 0; i < People / 10; i++)
                {
                    foreach (var piece in OutfitRules.For(Profile(personClass, wealth, i % 2 == 0), $"Felucca:p#{i}"))
                    {
                        Assert.True(ContentTypes.IsItem(piece.TypeName), piece.TypeName);
                        Assert.True(
                            piece.Hue is OutfitRules.Undyed or OutfitRules.TrueBlack ||
                            piece.Hue is >= OutfitRules.FirstDyeHue and <= OutfitRules.LastDyeHue,
                            $"{piece.TypeName} hue {piece.Hue}"
                        );
                    }
                }
            }
        }
    }

    [Fact]
    public void EveryOutfit_WearsOnePiecePerKind()
    {
        for (var i = 0; i < People; i++)
        {
            foreach (var personClass in Enum.GetValues<PersonClass>())
            {
                var types = OutfitRules.For(Profile(personClass, PersonWealth.Modest, i % 2 == 0), $"Felucca:p#{i}")
                    .Select(piece => piece.TypeName)
                    .ToList();

                Assert.Equal(types.Count, types.Distinct().Count());
            }
        }
    }

    [Fact]
    public void Mages_MostlyWearRobes_AndSmithsWearAprons()
    {
        var robes = 0;
        var necroRobes = 0;

        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            robes += OutfitRules.For(Profile(PersonClass.Mage, PersonWealth.Modest, false), id).Any(p => p.TypeName == OutfitRules.Robe) ? 1 : 0;
            necroRobes += OutfitRules.For(Profile(PersonClass.Necromancer, PersonWealth.Modest, false), id).Any(p => p.TypeName == OutfitRules.Robe) ? 1 : 0;
            Assert.Contains(OutfitRules.For(Profile(PersonClass.Smith, PersonWealth.Modest, false), id), p => p.TypeName == OutfitRules.FullApron);
        }

        Assert.True(robes > People * 2 / 3);
        Assert.True(necroRobes > People * 2 / 3);
    }

    [Fact]
    public void Copies_WearManyColours_AndTrueBlackIsRareAndRich()
    {
        var hues = new HashSet<int>();
        var poorBlack = 0;
        var richBlack = 0;

        for (var i = 0; i < People * 5; i++)
        {
            var id = $"Felucca:p#{i}";
            hues.Add(OutfitRules.DyeHue(id, 1, PersonWealth.Modest, OutfitRules.UndyedPercent));
            poorBlack += OutfitRules.DyeHue(id, 1, PersonWealth.Poor, OutfitRules.UndyedPercent) == OutfitRules.TrueBlack ? 1 : 0;
            richBlack += OutfitRules.DyeHue(id, 1, PersonWealth.Rich, OutfitRules.UndyedPercent) == OutfitRules.TrueBlack ? 1 : 0;
        }

        Assert.True(hues.Count > People);
        Assert.Equal(0, poorBlack);
        Assert.InRange(richBlack, 1, People / 4);
    }

    [Fact]
    public void Outfit_IsStableForOneId()
    {
        var profile = Profile(PersonClass.Bard, PersonWealth.Rich, true);
        Assert.Equal(OutfitRules.For(profile, "Felucca:orla#3"), OutfitRules.For(profile, "Felucca:orla#3"));
    }

    [Fact]
    public void OverBodyArmor_NoKiltSkirtOrDress()
    {
        for (var i = 0; i < People; i++)
        {
            foreach (var personClass in Enum.GetValues<PersonClass>())
            {
                var outfit = OutfitRules.For(Profile(personClass, PersonWealth.Rich, i % 2 == 0), $"Felucca:p#{i}", armored: true);

                Assert.DoesNotContain(outfit, piece => OutfitRules.HidesArmor(piece.TypeName));
            }
        }
    }

    [Fact]
    public void DressesStay_OnUnarmouredMerchantsAndTailors()
    {
        var dresses = Enumerable.Range(0, People)
            .Count(i => OutfitRules.For(Profile(PersonClass.Merchant, PersonWealth.Modest, true), $"Felucca:p#{i}")
                .Any(piece => piece.TypeName is OutfitRules.PlainDress or OutfitRules.FancyDress));

        Assert.InRange(dresses, 1, People - 1);
        Assert.True(OutfitRules.HidesArmor(OutfitRules.Kilt));
        Assert.False(OutfitRules.HidesArmor(OutfitRules.BodySash));
        Assert.False(OutfitRules.HidesArmor(OutfitRules.Robe));
    }

    [Fact]
    public void WearsRobe_FollowsTheLook()
    {
        for (var i = 0; i < People; i++)
        {
            var mage = Profile(PersonClass.Mage, PersonWealth.Modest, false);
            var id = $"Felucca:p#{i}";

            Assert.Equal(OutfitRules.For(mage, id).Any(p => p.TypeName == OutfitRules.Robe), OutfitRules.WearsRobe(mage, id));
            Assert.False(OutfitRules.WearsRobe(Profile(PersonClass.Warrior, PersonWealth.Modest, false), id));
        }
    }
}
