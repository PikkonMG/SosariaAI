using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A fresh person dressed through the real first-day path: profile, class build, kit,
/// clothes and finish. What the paperdoll shows is what the owner sees at the bank: a
/// fighter in its armor with its weapon in hand, never cloth pants over leg armor, never a
/// dress or a kilt over a suit, and nothing of the worn kit left in the pack.
/// </summary>
[Collection(RealMapCollection.Name)]
public class FreshKitWearTests
{
    private const int PeoplePerClass = 8;
    private const int Suits = 60;

    private static readonly PersonClass[] ArmoredClasses =
    [
        PersonClass.Warrior, PersonClass.Fencer, PersonClass.Archer, PersonClass.Ranger, PersonClass.Tamer, PersonClass.Thief
    ];

    public FreshKitWearTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void EveryArmoredClass_WearsBodyArmor_AtEveryTier()
    {
        foreach (var (character, personClass, tier) in People(ArmoredClasses))
        {
            Assert.True(
                character.FindItemOnLayer(Layer.InnerTorso) is BaseArmor,
                $"{personClass} {tier} {character.CharacterId} has no chest armor: {Paperdoll(character)}"
            );
        }
    }

    [RealMapFact]
    public void EveryLayer_ShowsOnePiece_AndArmorIsNeverHidden()
    {
        foreach (var (character, personClass, tier) in People(Enum.GetValues<PersonClass>().Where(Allowed).ToArray()))
        {
            Assert.All(character.Items.Where(Worn).GroupBy(item => item.Layer), layer => Assert.Single(layer));

            if (GearEquip.WearsBodyArmor(character))
            {
                Assert.False(character.FindItemOnLayer(Layer.OuterLegs) is Kilt or Skirt, $"{personClass} {tier}: {Paperdoll(character)}");
                Assert.False(character.FindItemOnLayer(Layer.OuterTorso) is PlainDress or FancyDress, $"{personClass} {tier}: {Paperdoll(character)}");
            }
        }
    }

    [RealMapFact]
    public void WornKit_IsEquipped_NotPacked()
    {
        foreach (var (character, personClass, tier) in People(ArmoredClasses))
        {
            Assert.False(
                character.Backpack.Items.Any(item => item is BaseArmor),
                $"{personClass} {tier} {character.CharacterId} packed armor: {Paperdoll(character)} | {string.Join(", ", character.Backpack.Items.Select(i => i.GetType().Name))}"
            );
            Assert.True(Weapon(character) != null, $"{personClass} {tier} holds nothing: {Paperdoll(character)}");
        }
    }

    [RealMapFact]
    public void Fighters_HoldTheirClassWeapon()
    {
        foreach (var (character, personClass, _) in People([PersonClass.Warrior, PersonClass.Fencer, PersonClass.Archer, PersonClass.Thief]))
        {
            var weapon = Weapon(character);
            Assert.NotNull(weapon);

            switch (personClass)
            {
                case PersonClass.Warrior:
                    Assert.Contains(weapon.DefSkill, new[] { SkillName.Swords, SkillName.Macing });
                    break;
                case PersonClass.Fencer:
                    Assert.Equal(SkillName.Fencing, weapon.DefSkill);
                    break;
                case PersonClass.Archer:
                    var bow = Assert.IsAssignableFrom<BaseRanged>(weapon);
                    Assert.True(character.Backpack.GetAmount(bow.AmmoType) > 0);
                    break;
                default:
                    Assert.IsType<Dagger>(weapon);
                    break;
            }
        }
    }

    [RealMapFact]
    public void Casters_CarryTheirBook_AndPureMagesWearCloth()
    {
        foreach (var (character, _, tier) in People([PersonClass.Mage]))
        {
            var book = character.FindItemOnLayer(Layer.OneHanded) as Spellbook ?? character.Backpack.FindItemByType<Spellbook>();
            Assert.NotNull(book);

            if (Weapon(character) == null)
            {
                // A pure mage: the book in hand and no armor under its cloth.
                Assert.Same(book, character.FindItemOnLayer(Layer.OneHanded));
                Assert.False(GearEquip.WearsBodyArmor(character), $"{tier}: {Paperdoll(character)}");
            }
        }
    }

    [RealMapFact]
    public void Workers_HoldTheirTool_AndWearNoBodyArmor()
    {
        foreach (var (character, personClass, tier) in People([PersonClass.Miner, PersonClass.Lumberjack, PersonClass.Tailor]))
        {
            Assert.NotNull(Weapon(character));
            Assert.False(GearEquip.WearsBodyArmor(character), $"{personClass} {tier}: {Paperdoll(character)}");
        }
    }

    [RealMapFact]
    public void RichVeterans_ShowColouredOre_AndSecondAgeMagicOnly()
    {
        var coloured = 0;
        var magic = 0;

        for (var i = 0; i < Suits; i++)
        {
            var character = KitWorld.Dress(
                KitWorld.Profile(PersonClass.Warrior, SkillTier.Grandmaster, PersonWealth.Rich, false),
                $"Felucca:rich#{i}"
            );

            foreach (var item in character.Items)
            {
                if (item is BaseArmor { Resource: > CraftResource.Iron and <= CraftResource.Valorite })
                {
                    coloured++;
                }

                if (item is BaseWeapon weapon)
                {
                    magic += weapon.DamageLevel != WeaponDamageLevel.Regular ? 1 : 0;

                    // The Second Age knew no item properties.
                    Assert.True(weapon.Attributes.IsEmpty && weapon.WeaponAttributes.IsEmpty);
                }
            }
        }

        Assert.True(coloured > 0, "no coloured ore on sixty rich grandmasters");
        Assert.True(magic > Suits / 2, $"{magic} magic weapons on {Suits} rich grandmasters");
    }

    private static IEnumerable<(SosariaCharacter, PersonClass, SkillTier)> People(PersonClass[] classes)
    {
        var purses = Enum.GetValues<PersonWealth>();

        foreach (var personClass in classes)
        {
            foreach (var tier in Enum.GetValues<SkillTier>())
            {
                for (var i = 0; i < PeoplePerClass; i++)
                {
                    var profile = KitWorld.Profile(personClass, tier, purses[i % purses.Length], female: i % 2 == 1);
                    yield return (KitWorld.Dress(profile, $"Felucca:{personClass}{tier}#{i}"), personClass, tier);
                }
            }
        }
    }

    private static bool Allowed(PersonClass personClass) =>
        PersonClassRules.Allowed(personClass, EraRules.Current());

    private static bool Worn(Item item) =>
        item.Layer is not (Layer.Backpack or Layer.Hair or Layer.FacialHair or Layer.Invalid);

    private static BaseWeapon Weapon(Mobile mobile) =>
        mobile.FindItemOnLayer(Layer.OneHanded) as BaseWeapon ?? mobile.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;

    private static string Paperdoll(Mobile mobile) =>
        string.Join(", ", mobile.Items.Where(Worn).Select(item => $"{item.Layer}:{item.GetType().Name}"));
}
