using System;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The first day's kit: most worn pieces are exceptional with a maker's mark, a
/// veteran's magic weapon shows its classic name, and magic loot carries no mark.
/// </summary>
public class KitFinishTests
{
    private const int SearchLimit = 2000;
    private const int Suits = 12;
    private const uint SuitSerialBase = 0x7D00;
    private const uint SuitSerialStride = 0x10;
    private const PersonWealth Purse = PersonWealth.Modest;

    /// <summary>A dexxer's keep ceiling: every armor piece may be the magic one.</summary>
    private const int AnyArmor = GearLadder.PlateScore;

    static KitFinishTests()
    {
        Timer.Init(0);
        DefBlacksmithy.Initialize();
        DefTailoring.Initialize();
        DefBowFletching.Initialize();
    }

    public KitFinishTests() => TestMap.EnsureInternal();

    [Fact]
    public void MakerTradeOf_NamesTheTradeThatMakesThePiece()
    {
        Assert.Same(SmithRules.Trade, KitFinish.MakerTradeOf(new PlateChest((Serial)0x7C01)));
        Assert.Same(SmithRules.Trade, KitFinish.MakerTradeOf(new Katana((Serial)0x7C02)));
        Assert.Same(TailorRules.Trade, KitFinish.MakerTradeOf(new StuddedChest((Serial)0x7C03)));
        Assert.Same(FletchRules.Trade, KitFinish.MakerTradeOf(new Bow((Serial)0x7C04)));
    }

    [Fact]
    public void ExceptionalPiece_CarriesItsMakersMark()
    {
        var id = FindId(i => !KitMagicRules.HasMagicArmor(SkillTier.Journeyman, Purse, i) &&
                             KitCraftRules.IsExceptional(SkillTier.Journeyman, i, (int)Layer.InnerTorso));
        var character = Character((Serial)0x7C10);
        var chest = new PlateChest((Serial)0x7C11) { Layer = Layer.InnerTorso };
        Wear(character, chest);

        KitFinish.Apply(character, SkillTier.Journeyman, Purse, id, EraBand.T2A, AnyArmor);

        Assert.Equal(ArmorQuality.Exceptional, chest.Quality);
        Assert.Equal(KitCraftRules.MakerName(SmithRules.Trade.Kind, id), chest.Crafter);
    }

    [Fact]
    public void ShopPiece_StaysPlain()
    {
        var id = FindId(i => !KitMagicRules.HasMagicWeapon(SkillTier.Novice, Purse, i) &&
                             !KitCraftRules.IsExceptional(SkillTier.Novice, i, (int)Layer.OneHanded));
        var character = Character((Serial)0x7C20);
        var katana = new Katana((Serial)0x7C21) { Layer = Layer.OneHanded, Quality = WeaponQuality.Regular };
        Wear(character, katana);

        KitFinish.Apply(character, SkillTier.Novice, Purse, id, EraBand.T2A, AnyArmor);

        Assert.Equal(WeaponQuality.Regular, katana.Quality);
        Assert.Null(katana.Crafter);
    }

    [Fact]
    public void SecondAgeMagicWeapon_IsIdentifiedLootWithNoMark()
    {
        var id = FindId(i => KitMagicRules.HasMagicWeapon(SkillTier.Grandmaster, Purse, i));
        var character = Character((Serial)0x7C30);
        var katana = new Katana((Serial)0x7C31) { Layer = Layer.OneHanded, Quality = WeaponQuality.Regular };
        Wear(character, katana);

        KitFinish.Apply(character, SkillTier.Grandmaster, Purse, id, EraBand.T2A, AnyArmor);

        Assert.NotEqual(WeaponDamageLevel.Regular, katana.DamageLevel);
        Assert.True(katana.Identified);
        Assert.Equal(WeaponQuality.Regular, katana.Quality);
        Assert.Null(katana.Crafter);
    }

    [Fact]
    public void MagicArmor_FallsToTheShield_WhenNoChestIsWorn()
    {
        var id = FindId(i => KitMagicRules.HasMagicArmor(SkillTier.Grandmaster, Purse, i));
        var character = Character((Serial)0x7C40);
        var shield = new HeaterShield((Serial)0x7C41) { Layer = Layer.TwoHanded };
        Wear(character, shield);

        KitFinish.Apply(character, SkillTier.Grandmaster, Purse, id, EraBand.T2A, AnyArmor);

        Assert.NotEqual(ArmorProtectionLevel.Regular, shield.ProtectionLevel);
        Assert.True(shield.Identified);
    }

    [Fact]
    public void MagicArmor_SkipsAPieceAboveTheBuildsCeiling()
    {
        // A mage's hardening goes on the leather it keeps, never on a plate chest it takes off.
        var id = FindId(i => KitMagicRules.HasMagicArmor(SkillTier.Grandmaster, Purse, i));
        var character = Character((Serial)0x7C60);
        var chest = new PlateChest((Serial)0x7C61) { Layer = Layer.InnerTorso };
        var gloves = new LeatherGloves((Serial)0x7C62) { Layer = Layer.Gloves };
        Wear(character, chest);
        Wear(character, gloves);

        KitFinish.Apply(character, SkillTier.Grandmaster, Purse, id, EraBand.T2A, GearLadder.LeatherScore);

        Assert.Equal(ArmorProtectionLevel.Regular, chest.ProtectionLevel);
        Assert.NotEqual(ArmorProtectionLevel.Regular, gloves.ProtectionLevel);
    }

    [Fact]
    public void MostOfAJourneymansSuit_IsExceptional()
    {
        var exceptional = 0;
        var pieces = 0;

        for (var i = 0; i < Suits; i++)
        {
            var serial = SuitSerialBase + (uint)i * SuitSerialStride;
            var character = Character((Serial)serial);
            var suit = new BaseArmor[]
            {
                new RingmailChest((Serial)(serial + 1)) { Layer = Layer.InnerTorso },
                new RingmailLegs((Serial)(serial + 2)) { Layer = Layer.Pants },
                new RingmailArms((Serial)(serial + 3)) { Layer = Layer.Arms },
                new RingmailGloves((Serial)(serial + 4)) { Layer = Layer.Gloves }
            };

            foreach (var piece in suit)
            {
                Wear(character, piece);
            }

            KitFinish.Apply(character, SkillTier.Journeyman, Purse, $"Felucca:suit#{i}", EraBand.T2A, AnyArmor);
            exceptional += suit.Count(piece => piece.Quality == ArmorQuality.Exceptional);
            pieces += suit.Length;
        }

        Assert.True(exceptional * 2 > pieces, $"{exceptional} of {pieces}");
    }

    [Fact]
    public void RichGrandmaster_SmithMadeSuit_IsColouredOre_ButLeatherStaysLeather()
    {
        var id = FindId(i => KitOreRules.OreFor(SkillTier.Grandmaster, PersonWealth.Rich, i) != CraftResource.Iron &&
                             !KitMagicRules.HasMagicArmor(SkillTier.Grandmaster, PersonWealth.Rich, i) &&
                             KitCraftRules.IsExceptional(SkillTier.Grandmaster, i, (int)Layer.Pants) &&
                             KitCraftRules.IsExceptional(SkillTier.Grandmaster, i, (int)Layer.Gloves));
        var character = Character((Serial)0x7C50);
        var legs = new PlateLegs((Serial)0x7C51) { Layer = Layer.Pants };
        var gloves = new LeatherGloves((Serial)0x7C52) { Layer = Layer.Gloves };
        Wear(character, legs);
        Wear(character, gloves);
        var leather = gloves.Resource;

        KitFinish.Apply(character, SkillTier.Grandmaster, PersonWealth.Rich, id, EraBand.T2A, AnyArmor);

        Assert.Equal(KitOreRules.OreFor(SkillTier.Grandmaster, PersonWealth.Rich, id), legs.Resource);
        Assert.Equal(ArmorQuality.Exceptional, gloves.Quality);
        Assert.Equal(leather, gloves.Resource);
    }

    private static string FindId(Func<string, bool> wanted)
    {
        for (var i = 0; i < SearchLimit; i++)
        {
            var id = $"Felucca:kit#{i}";

            if (wanted(id))
            {
                return id;
            }
        }

        throw new Xunit.Sdk.XunitException("no id matched");
    }

    private static SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        return character;
    }

    private static void Wear(SosariaCharacter character, Item item)
    {
        item.Parent = character;
        character.Items.Add(item);
    }
}
