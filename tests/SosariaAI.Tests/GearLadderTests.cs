using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The armor ladder the shops sell: every piece is a real armor type with a price, each rung
/// guards better than the one below, and no class climbs past its ceiling.
/// </summary>
public class GearLadderTests
{
    private const uint SerialBase = 0x7F00;
    private const int PeoplePerClass = 40;

    static GearLadderTests() => Timer.Init(0);

    [Fact]
    public void EveryShelfPiece_IsARealArmorType_WithAPrice()
    {
        var serial = SerialBase;

        foreach (var piece in GearLadder.Pieces)
        {
            Assert.NotNull(Build(piece.TypeName, serial++));
            Assert.True(piece.Price > 0, piece.TypeName);
        }
    }

    [Fact]
    public void EachRung_GuardsBetterThanTheOneBelow()
    {
        var serial = SerialBase + 0x80;

        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            var guard = 0;

            foreach (var piece in GearLadder.Pieces.Where(p => p.Slot == slot && p.Female != true).OrderBy(p => p.Material))
            {
                var armorBase = Build(piece.TypeName, serial++).ArmorBase;
                Assert.True(armorBase > guard, $"{piece.TypeName} guards {armorBase}, below {guard}");
                guard = armorBase;
            }
        }
    }

    [Fact]
    public void RungScores_ClimbWithTheMaterial()
    {
        for (var material = GearMaterial.Studded; material <= GearMaterial.Plate; material++)
        {
            Assert.True(GearLadder.ScoreOf(material) > GearLadder.ScoreOf(material - 1));
        }
    }

    [Fact]
    public void BestAtOrBelow_FillsTheSlotsNoShopMakesAtThatRung()
    {
        Assert.Equal("RingmailArms", GearLadder.BestAtOrBelow(GearSlot.Arms, GearMaterial.Chainmail, false)!.Value.TypeName);
        Assert.Equal("StuddedGorget", GearLadder.BestAtOrBelow(GearSlot.Neck, GearMaterial.Chainmail, false)!.Value.TypeName);
        Assert.Equal("LeatherCap", GearLadder.BestAtOrBelow(GearSlot.Helm, GearMaterial.Studded, false)!.Value.TypeName);
        Assert.Equal("FemaleStuddedChest", GearLadder.BestAtOrBelow(GearSlot.Chest, GearMaterial.Studded, true)!.Value.TypeName);
        Assert.Null(GearLadder.BestAtOrBelow(GearSlot.Chest, GearMaterial.None, false));
    }

    [Fact]
    public void Target_StartsAtTheKit_ClimbsWithThePurse_AndStopsAtTheCeiling()
    {
        foreach (var weight in Enum.GetValues<KitArmor>())
        {
            var template = new ClassBuildTemplate { Armor = weight };

            foreach (var tier in Enum.GetValues<SkillTier>())
            {
                var kit = GearLadder.KitMaterial(template, tier);
                var poor = GearLadder.Target(template, tier, PersonWealth.Poor);
                var modest = GearLadder.Target(template, tier, PersonWealth.Modest);
                var rich = GearLadder.Target(template, tier, PersonWealth.Rich);

                Assert.Equal(GearLadder.KitMaterial(weight, tier), kit);
                Assert.True(kit <= GearLadder.Ceiling(weight));
                Assert.Equal(kit, poor);
                Assert.True(modest >= poor && rich >= modest);
                Assert.True(rich <= GearLadder.Ceiling(weight));
            }
        }

        Assert.Equal(GearMaterial.Ringmail, GearLadder.Target(new ClassBuildTemplate { Armor = KitArmor.Heavy }, SkillTier.Novice, PersonWealth.Rich));
        Assert.Equal(GearMaterial.None, GearLadder.Target(new ClassBuildTemplate { Armor = KitArmor.None }, SkillTier.Grandmaster, PersonWealth.Rich));
    }

    [Fact]
    public void WearCeiling_LetsArchersKeepBone_AndMagesWearNone()
    {
        Assert.Equal(GearLadder.BoneScore, GearLadder.WearCeiling(Template(PersonClass.Archer)));
        Assert.Equal(GearLadder.PlateScore, GearLadder.WearCeiling(Template(PersonClass.Warrior)));
        Assert.Equal(GearLadder.StuddedScore, GearLadder.WearCeiling(Template(PersonClass.Thief)));
        Assert.Equal(0, GearLadder.WearCeiling(PureMage()));
    }

    [Fact]
    public void Meditation_HoldsPureCastersToLeather_AndTankMagesToStudded()
    {
        // No mage armor in the Second Age: plate, chain, ring and bone stopped meditation.
        Assert.Equal(GearMaterial.Leather, GearLadder.MeditationCeiling(PureMage()));
        Assert.Equal(GearMaterial.Studded, GearLadder.MeditationCeiling(TankMage()));
        Assert.Equal(GearMaterial.Leather, GearLadder.MeditationCeiling(Template(PersonClass.Tamer)));
        Assert.Equal(GearMaterial.Plate, GearLadder.MeditationCeiling(Template(PersonClass.Warrior)));
        Assert.Equal(GearMaterial.Plate, GearLadder.MeditationCeiling(Template(PersonClass.Archer)));

        Assert.Equal(GearMaterial.None, GearLadder.Ceiling(PureMage()));
        Assert.Equal(GearMaterial.Studded, GearLadder.Ceiling(TankMage()));
        Assert.Equal(GearMaterial.Leather, GearLadder.Ceiling(Template(PersonClass.Tamer)));
        Assert.Equal(GearMaterial.Leather, GearLadder.Ceiling(Template(PersonClass.TreasureHunter)));
        Assert.Equal(GearMaterial.Plate, GearLadder.Ceiling(Template(PersonClass.Fencer)));

        Assert.Equal(GearLadder.LeatherScore, GearLadder.KeepCeiling(PureMage()));
        Assert.Equal(GearLadder.StuddedScore, GearLadder.KeepCeiling(TankMage()));
        Assert.Equal(GearLadder.PlateScore, GearLadder.KeepCeiling(Template(PersonClass.Warrior)));
        Assert.Equal(GearLadder.StuddedScore, GearLadder.WearCeiling(TankMage()));
        Assert.Equal(GearLadder.LeatherScore, GearLadder.WearCeiling(Template(PersonClass.Tamer)));
        Assert.False(GearLadder.WearsBone(TankMage()));
        Assert.True(GearLadder.WearsBone(Template(PersonClass.Archer)));
    }

    [Fact]
    public void NoBuildShopsOrWearsAboveWhatItKeepsOn()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            for (var i = 0; i < PeoplePerClass; i++)
            {
                var template = ClassBuilds.TemplateFor(personClass, CharacterRole.Fighter, $"Felucca:{personClass}#{i}");
                var keep = GearLadder.KeepCeiling(template);

                Assert.True(GearLadder.WearCeiling(template) <= keep, $"{personClass} wears above what it keeps");

                foreach (var tier in Enum.GetValues<SkillTier>())
                {
                    Assert.True(GearLadder.ScoreOf(GearLadder.Target(template, tier, PersonWealth.Rich)) <= keep);
                }
            }
        }
    }

    [Fact]
    public void MageKits_HoldNoArmorAboveMeditation()
    {
        foreach (var personClass in new[] { PersonClass.Mage, PersonClass.Tamer, PersonClass.TreasureHunter, PersonClass.Healer, PersonClass.Bard })
        {
            for (var i = 0; i < PeoplePerClass; i++)
            {
                var id = $"Felucca:{personClass}#{i}";
                var template = ClassBuilds.TemplateFor(personClass, CharacterRole.Fighter, id);
                var keep = GearLadder.KeepCeiling(template);

                foreach (var tier in Enum.GetValues<SkillTier>())
                {
                    foreach (var piece in ClassKits.For(template, tier, i % 2 == 0, id))
                    {
                        Assert.True(!GearLadder.TryScoreOf(piece, out var score) || score <= keep, $"{personClass} {tier} carries {piece}");
                    }
                }
            }
        }
    }

    [Fact]
    public void ClassKits_DressFromTheLadder()
    {
        var shelf = new HashSet<string>(GearLadder.Pieces.Select(p => p.TypeName));
        var template = ClassBuilds.TemplateFor(PersonClass.Fencer, CharacterRole.Fighter, "Felucca:kit#1");

        foreach (var tier in Enum.GetValues<SkillTier>())
        {
            var material = GearLadder.KitMaterial(KitArmor.Heavy, tier);
            var kit = ClassKits.For(template, tier, false, "Felucca:kit#1");

            if (kit.Contains("BoneChest"))
            {
                continue;
            }

            Assert.Contains(GearLadder.Piece(GearSlot.Chest, material, false)!.Value.TypeName, kit);
            Assert.All(kit.Where(name => name.EndsWith("Legs") || name.EndsWith("Arms") || name.EndsWith("Gloves")), name => Assert.Contains(name, shelf));
        }
    }

    [Theory]
    [InlineData("PlateHelm", GearSlot.Helm)]
    [InlineData("FemaleStuddedChest", GearSlot.Chest)]
    [InlineData("Helmet", GearSlot.Helm)]
    [InlineData("BoneArms", GearSlot.Arms)]
    [InlineData("LeatherSkirt", GearSlot.Legs)]
    [InlineData("studdedbustierarms", GearSlot.Chest)]
    public void TrySlotOf_KnowsShelfAndWornOnlyPieces(string typeName, GearSlot slot)
    {
        Assert.True(GearLadder.TrySlotOf(typeName, out var found));
        Assert.Equal(slot, found);
    }

    [Theory]
    [InlineData("Robe")]
    [InlineData("HeaterShield")]
    [InlineData("")]
    public void TrySlotOf_NothingForAPieceOffTheLadder(string typeName) =>
        Assert.False(GearLadder.TrySlotOf(typeName, out _));

    private static ClassBuildTemplate Template(PersonClass personClass) =>
        ClassBuilds.TemplateFor(personClass, CharacterRole.Fighter, "Felucca:ceiling#1");

    private static ClassBuildTemplate PureMage() => MageWhere(template => template.Weapon == null);

    private static ClassBuildTemplate TankMage() => MageWhere(template => template.Weapon != null);

    private static ClassBuildTemplate MageWhere(Func<ClassBuildTemplate, bool> wanted)
    {
        for (var i = 0; ; i++)
        {
            var template = ClassBuilds.TemplateFor(PersonClass.Mage, CharacterRole.Fighter, $"Felucca:mage#{i}");

            if (wanted(template))
            {
                return template;
            }
        }
    }

    private static BaseArmor Build(string typeName, uint serial)
    {
        var type = typeof(PlateChest).Assembly.GetTypes().Single(t => t.Name == typeName && typeof(BaseArmor).IsAssignableFrom(t));
        return (BaseArmor)Activator.CreateInstance(type, (Serial)serial);
    }
}
