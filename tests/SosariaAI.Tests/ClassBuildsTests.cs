using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class ClassBuildsTests
{
    private const int PeoplePerCase = 40;
    private const int MaxTemplateSkills = 7;

    private static IEnumerable<(PersonProfile Profile, CharacterRole Role, string Id)> Everyone()
    {
        foreach (var personClass in Enum.GetValues<PersonClass>())
        {
            foreach (var tier in Enum.GetValues<SkillTier>())
            {
                foreach (var role in Enum.GetValues<CharacterRole>())
                {
                    for (var i = 0; i < PeoplePerCase; i++)
                    {
                        var profile = new PersonProfile(
                            personClass, tier, PersonTrait.None, PersonWealth.Modest, ActivityTendencies.Even,
                            PersonProfile.NeutralPhaseLength, female: i % 2 == 0
                        );
                        yield return (profile, role, $"Felucca:{personClass}#{(int)tier * PeoplePerCase + i}");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(EraBand.T2A)]
    [InlineData(EraBand.ML)]
    [InlineData(EraBand.Modern)]
    public void EveryBuild_StaysInsideTheCapsOfItsEra(EraBand band)
    {
        var statCap = EraBuildCaps.StatCapFor(band);

        foreach (var (profile, role, id) in Everyone())
        {
            var build = ClassBuilds.For(profile, role, id, band);

            Assert.True(build.Strength + build.Dexterity + build.Intelligence <= EraBuildCaps.StatTotalCap, id);
            Assert.InRange(build.Strength, EraBuildCaps.MinStat, statCap);
            Assert.InRange(build.Dexterity, EraBuildCaps.MinStat, statCap);
            Assert.InRange(build.Intelligence, EraBuildCaps.MinStat, statCap);
            Assert.True(build.Skills.Values.Sum() <= EraBuildCaps.SkillTotalCap, id);
            Assert.True(build.Skills.Count <= MaxTemplateSkills, id);
            Assert.All(build.Skills.Values, value => Assert.InRange(value, 0, EraBuildCaps.SkillCap));
            Assert.All(build.Skills.Keys, key => Assert.True(Enum.TryParse<SkillName>(key, out _), key));
        }
    }

    [Fact]
    public void EveryKitPiece_IsARealItemType()
    {
        foreach (var (profile, role, id) in Everyone())
        {
            foreach (var piece in ClassBuilds.For(profile, role, id, EraBand.T2A).Kit)
            {
                Assert.True(ContentTypes.IsItem(piece), $"{piece} in {profile.Class} kit");
            }
        }
    }

    [Fact]
    public void Grandmaster_LeadsAtOneHundred_AndNoviceFarBelow()
    {
        var gm = Profile(PersonClass.Warrior, SkillTier.Grandmaster);
        var novice = Profile(PersonClass.Warrior, SkillTier.Novice);

        var gmBuild = ClassBuilds.For(gm, CharacterRole.Fighter, "Felucca:bran#1", EraBand.T2A);
        var noviceBuild = ClassBuilds.For(novice, CharacterRole.Fighter, "Felucca:bran#1", EraBand.T2A);

        Assert.Equal(EraBuildCaps.SkillCap, gmBuild.Skills.Values.Max());
        Assert.True(noviceBuild.Skills.Values.Max() < SkillTierRules.JourneymanSkill);
        Assert.True(gmBuild.Veteran);
        Assert.False(noviceBuild.Veteran);
    }

    [Fact]
    public void TankMages_SwingATwoHander_AndTrainTactics()
    {
        var sawTank = false;

        for (var i = 0; i < 60; i++)
        {
            var id = "Felucca:sela#" + i;
            var template = ClassBuilds.TemplateFor(PersonClass.Mage, CharacterRole.Fighter, id);

            if (template.Weapon == null)
            {
                Assert.Contains(SkillName.Inscribe, template.Secondary);
                continue;
            }

            sawTank = true;
            Assert.Contains(SkillName.Tactics, template.Secondary);
            Assert.False(template.Shield);
            Assert.Equal(CombatStyle.Mage, template.Style);
        }

        Assert.True(sawTank);
    }

    [Fact]
    public void Shields_OnlyBesideOneHandedRows()
    {
        var oneHanded = new[] { KitVariation.OneHandedSword, KitVariation.OneHandedFencing, KitVariation.OneHandedMace };

        foreach (var (profile, role, id) in Everyone())
        {
            var template = ClassBuilds.TemplateFor(profile.Class, role, id);

            if (template.Shield)
            {
                Assert.Contains(template.Weapon, oneHanded);
            }
        }
    }

    [Fact]
    public void WorkerClasses_CarryTheWorkerDefence()
    {
        var workers = new[]
        {
            PersonClass.Smith, PersonClass.Miner, PersonClass.Lumberjack, PersonClass.Carpenter,
            PersonClass.Tailor, PersonClass.Fisherman, PersonClass.Merchant
        };

        foreach (var personClass in workers)
        {
            var template = ClassBuilds.TemplateFor(personClass, CharacterRole.Worker, "Felucca:mira#2");

            Assert.Contains(SkillName.Swords, template.Secondary);
            Assert.Contains(SkillName.Tactics, template.Secondary);
            Assert.Contains(SkillName.Healing, template.Secondary);
        }
    }

    /// <summary>
    /// A smith that digs its own ore, and a carpenter or bowyer that cuts its own logs, gathers
    /// at the level it works: the gather skill follows the trade one step down.
    /// </summary>
    [Theory]
    [InlineData(PersonClass.Smith, SkillName.Blacksmith, SkillName.Mining)]
    [InlineData(PersonClass.Carpenter, SkillName.Carpentry, SkillName.Lumberjacking)]
    [InlineData(PersonClass.Bowyer, SkillName.Fletching, SkillName.Lumberjacking)]
    public void GathererCrafters_CarryTheirGatherSkillNextToTheTrade(PersonClass personClass, SkillName trade, SkillName gather)
    {
        foreach (var tier in Enum.GetValues<SkillTier>())
        {
            for (var i = 0; i < PeoplePerCase; i++)
            {
                var profile = new PersonProfile(
                    personClass, tier, PersonTrait.None, PersonWealth.Modest, ActivityTendencies.Even,
                    PersonProfile.NeutralPhaseLength, female: false
                );
                var skills = ClassBuilds.For(profile, CharacterRole.Worker, $"Felucca:{personClass}#{i}", EraBand.T2A).Skills;

                Assert.InRange(
                    skills[gather.ToString()],
                    skills[trade.ToString()] - ClassBuilds.SecondaryGap - ClassBuilds.SecondaryJitterSides,
                    skills[trade.ToString()]
                );
            }
        }
    }

    [Fact]
    public void WorkerWarrior_NeverRollsTheMaceRow()
    {
        for (var i = 0; i < 80; i++)
        {
            var template = ClassBuilds.TemplateFor(PersonClass.Warrior, CharacterRole.Worker, "Felucca:hal#" + i);
            Assert.Equal(SkillName.Swords, template.Primary);
        }
    }

    [Fact]
    public void Copies_CarryVariedWeapons()
    {
        var weapons = new HashSet<string>();

        for (var i = 0; i < 60; i++)
        {
            weapons.Add(ClassBuilds.For(Profile(PersonClass.Warrior, SkillTier.Expert), CharacterRole.Fighter, "Felucca:dunn#" + i, EraBand.T2A).Kit[0]);
        }

        Assert.True(weapons.Count >= 5, string.Join(",", weapons));
    }

    [Fact]
    public void HeavyArmor_ClimbsLeatherStuddedRingChainPlate()
    {
        var gm = ClassBuilds.For(Profile(PersonClass.Fencer, SkillTier.Grandmaster), CharacterRole.Fighter, "Felucca:x#1", EraBand.T2A).Kit;

        Assert.True(gm.Contains("PlateLegs") || gm.Contains("BoneLegs"));
        Assert.Contains("LeatherArms", HeavyKit(SkillTier.Novice));
        Assert.Contains("StuddedLegs", HeavyKit(SkillTier.Apprentice));
        Assert.Contains("RingmailLegs", HeavyKit(SkillTier.Journeyman));
        Assert.Contains("ChainLegs", HeavyKit(SkillTier.Expert));
    }

    private static IReadOnlyList<string> HeavyKit(SkillTier tier) =>
        ClassBuilds.For(Profile(PersonClass.Fencer, tier), CharacterRole.Fighter, "Felucca:x#1", EraBand.T2A).Kit;

    [Fact]
    public void Build_IsStableForOneId()
    {
        var profile = Profile(PersonClass.Bard, SkillTier.Adept);
        var first = ClassBuilds.For(profile, CharacterRole.Fighter, "Felucca:orla#5", EraBand.T2A);
        var again = ClassBuilds.For(profile, CharacterRole.Fighter, "Felucca:orla#5", EraBand.T2A);

        Assert.Equal(first.Kit, again.Kit);
        Assert.Equal(first.Skills, again.Skills);
    }

    [Fact]
    public void EveryTemplate_TrainsOnlySkillsItsClassEraHas()
    {
        foreach (var (profile, role, id) in Everyone())
        {
            var template = ClassBuilds.TemplateFor(profile.Class, role, id);
            var floor = PersonClassRules.Floor(profile.Class);
            var skills = template.Secondary.Concat(template.Utility).Append(template.Primary);

            Assert.All(skills, skill => Assert.True(EraRules.SkillFloor(skill) <= floor, $"{skill} in {profile.Class}"));
        }
    }

    [Theory]
    [InlineData(PersonClass.Paladin, SkillName.Chivalry, ClassKits.BookOfChivalry)]
    [InlineData(PersonClass.Samurai, SkillName.Bushido, ClassKits.BookOfBushido)]
    [InlineData(PersonClass.Ninja, SkillName.Ninjitsu, ClassKits.BookOfNinjitsu)]
    [InlineData(PersonClass.Necromancer, SkillName.Necromancy, ClassKits.NecromancerSpellbook)]
    public void LaterClasses_LeadWithTheirSkill_AndCarryTheirBook(PersonClass personClass, SkillName skill, string book)
    {
        var build = ClassBuilds.For(Profile(personClass, SkillTier.Grandmaster), CharacterRole.Fighter, "Felucca:p#1", EraBand.ML);

        Assert.Equal(EraBuildCaps.SkillCap, build.Skills[skill.ToString()]);
        Assert.Contains(book, build.Kit);
    }

    [Fact]
    public void Necromancer_CarriesTheMageBookFirst_AndCastsMagery()
    {
        var template = ClassBuilds.TemplateFor(PersonClass.Necromancer, CharacterRole.Fighter, "Felucca:p#1");
        var kit = ClassBuilds.For(Profile(PersonClass.Necromancer, SkillTier.Adept), CharacterRole.Fighter, "Felucca:p#1", EraBand.ML).Kit.ToList();

        Assert.Equal(CombatStyle.Mage, template.Style);
        Assert.Contains(SkillName.Magery, template.Secondary);
        Assert.True(kit.IndexOf(ClassKits.Spellbook) < kit.IndexOf(ClassKits.NecromancerSpellbook));
    }

    private static PersonProfile Profile(PersonClass personClass, SkillTier tier) =>
        new(personClass, tier, PersonTrait.None, PersonWealth.Modest, ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female: false);

    [Fact]
    public void Tamer_IsAMageTamer()
    {
        var template = ClassBuilds.TemplateFor(PersonClass.Tamer, CharacterRole.Fighter, "Felucca:osric#1");

        Assert.Equal(SkillName.AnimalTaming, template.Primary);
        Assert.True(template.Trains(SkillName.AnimalLore));
        Assert.True(template.Trains(SkillName.Veterinary));
        Assert.True(template.Trains(SkillName.Magery));
        Assert.True(template.Trains(SkillName.EvalInt));
        Assert.True(template.Trains(SkillName.Meditation));
        Assert.False(template.Trains(SkillName.Wrestling));
        Assert.True(template.Caster);
    }

    [Fact]
    public void GrandmasterTamer_HasEveryTamerSkillNearTheCap_AndABookWithReagents()
    {
        var profile = new PersonProfile(
            PersonClass.Tamer, SkillTier.Grandmaster, PersonTrait.None, PersonWealth.Modest, ActivityTendencies.Even,
            PersonProfile.NeutralPhaseLength, female: false
        );
        var build = ClassBuilds.For(profile, CharacterRole.Fighter, "Felucca:osric#2", EraBand.T2A);

        Assert.Equal(EraBuildCaps.SkillCap, build.Skills[nameof(SkillName.AnimalTaming)]);
        Assert.All(
            ClassBuilds.TamerCore,
            skill => Assert.InRange(build.Skills[skill.ToString()], SkillTierRules.MasterSkill, EraBuildCaps.SkillCap));
        Assert.Equal(CombatStyle.Mage, build.Style);
        Assert.Contains(ClassKits.Spellbook, build.Kit);
        Assert.Contains(ClassKits.RecallRune, build.Kit);
        Assert.All(ClassKits.AllReagents, reagent => Assert.Contains(reagent, build.Kit));
    }
}
