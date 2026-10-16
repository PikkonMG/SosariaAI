using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GearEquipTests
{
    private const int OneArrow = 1;
    private const int FighterStat = 100;
    private const double TrainedSkill = 100;

    static GearEquipTests() => Timer.Init(0);

    public GearEquipTests() => TestMap.EnsureInternal();

    [Fact]
    public void ConflictingLayers_HandLayers_CoverBothHands()
    {
        // Either hand can block the other, so both are read for an item in hand.
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.OneHanded));
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.TwoHanded));
        Assert.Equal([Layer.InnerTorso], GearEquip.ConflictingLayers(Layer.InnerTorso));
    }

    [Fact]
    public void WornConflicts_TwoHandedWeapon_FlagsTheOneHandedWeapon()
    {
        var character = Character((Serial)0x7301);
        var hatchet = new Hatchet((Serial)0x7401) { Layer = Layer.OneHanded };
        character.Items.Add(hatchet);
        var staff = new QuarterStaff((Serial)0x7402) { Layer = Layer.TwoHanded };

        var conflicts = GearEquip.WornConflicts(character, staff);

        Assert.Same(hatchet, Assert.Single(conflicts));
    }

    [Fact]
    public void WornConflicts_OneHandedWeapon_FlagsTheTwoHandedWeapon()
    {
        var character = Character((Serial)0x7302);
        var staff = new QuarterStaff((Serial)0x7403) { Layer = Layer.TwoHanded };
        character.Items.Add(staff);
        var katana = new Katana((Serial)0x7404) { Layer = Layer.OneHanded };

        var conflicts = GearEquip.WornConflicts(character, katana);

        Assert.Same(staff, Assert.Single(conflicts));
    }

    [Fact]
    public void WornConflicts_SkipsTheItemBeingEquipped()
    {
        var character = Character((Serial)0x7304);
        var hatchet = new Hatchet((Serial)0x7405) { Layer = Layer.OneHanded };
        var pickaxe = new Pickaxe((Serial)0x7406) { Layer = Layer.TwoHanded };
        character.Items.Add(hatchet);
        character.Items.Add(pickaxe);

        var conflicts = GearEquip.WornConflicts(character, pickaxe);

        Assert.Same(hatchet, Assert.Single(conflicts));
    }

    [Fact]
    public void WornConflicts_ShieldBesideOneHandedWeapon_KeepsTheWeapon()
    {
        // The engine wears a one-handed weapon and a shield together; buying a shield packed
        // the sword, and drawing the sword packed the shield.
        var character = Character((Serial)0x7306);
        var katana = new Katana((Serial)0x7407) { Layer = Layer.OneHanded };
        character.Items.Add(katana);
        var shield = new MetalShield((Serial)0x7408) { Layer = Layer.TwoHanded };

        Assert.Empty(GearEquip.WornConflicts(character, shield));
    }

    [Fact]
    public void WornConflicts_OneHandedWeaponBesideShield_KeepsTheShield()
    {
        var character = Character((Serial)0x7308);
        var shield = new MetalShield((Serial)0x7409) { Layer = Layer.TwoHanded };
        character.Items.Add(shield);
        var katana = new Katana((Serial)0x740A) { Layer = Layer.OneHanded };

        Assert.Empty(GearEquip.WornConflicts(character, katana));
    }

    [Fact]
    public void WornConflicts_NewShield_FlagsTheOldShield()
    {
        var character = Character((Serial)0x730A);
        var old = new Buckler((Serial)0x740B) { Layer = Layer.TwoHanded };
        character.Items.Add(old);
        var shield = new MetalShield((Serial)0x740C) { Layer = Layer.TwoHanded };

        Assert.Same(old, Assert.Single(GearEquip.WornConflicts(character, shield)));
    }

    [Fact]
    public void FreeBusyHands_WeaponAndTool_PacksTheSecondHand()
    {
        // A save made before layer checks can hold a hatchet and a pickaxe at once.
        // HasWeapon saw the pair as armed and never repaired it.
        var character = Character((Serial)0x7310);
        var hatchet = new Hatchet((Serial)0x7410) { Layer = Layer.OneHanded };
        var pickaxe = new Pickaxe((Serial)0x7411) { Layer = Layer.TwoHanded };
        Wear(character, hatchet);
        Wear(character, pickaxe);

        Assert.Same(pickaxe, GearEquip.BusyHandConflict(character));
    }

    [Fact]
    public void FreeBusyHands_WeaponAndShield_StaysWorn()
    {
        var character = Character((Serial)0x7312);
        var katana = new Katana((Serial)0x7412) { Layer = Layer.OneHanded };
        var shield = new MetalShield((Serial)0x7413) { Layer = Layer.TwoHanded };
        Wear(character, katana);
        Wear(character, shield);

        Assert.Null(GearEquip.BusyHandConflict(character));
    }

    [Fact]
    public void FreeBusyHands_ShieldInWeaponHand_KeepsTheWeapon()
    {
        var character = Character((Serial)0x7314);
        var shield = new MetalShield((Serial)0x7414) { Layer = Layer.OneHanded };
        var staff = new QuarterStaff((Serial)0x7415) { Layer = Layer.TwoHanded };
        Wear(character, shield);
        Wear(character, staff);

        Assert.Same(shield, GearEquip.BusyHandConflict(character));
    }

    [Fact]
    public void BowWithoutAmmo_AnEmptyQuiverIsNotToppedUp()
    {
        // Nothing refills a quiver: an archer with no arrows is out of arrows.
        var character = Character((Serial)0x7316);
        Wear(character, new Bow((Serial)0x7416) { Layer = Layer.TwoHanded });

        Assert.True(GearEquip.BowWithoutAmmo(character));

        GearEquip.EquipReadyWeapon(character);

        Assert.Equal(0, character.Backpack.GetAmount(typeof(Arrow)));
    }

    [Fact]
    public void BowWithoutAmmo_ArrowsInThePackFire()
    {
        var character = Character((Serial)0x7318);
        Wear(character, new Bow((Serial)0x7418) { Layer = Layer.TwoHanded });
        // A serial-constructed item starts with no amount, as it would before load.
        character.Backpack.AddItem(new Arrow((Serial)0x7419) { Amount = OneArrow });

        Assert.False(GearEquip.BowWithoutAmmo(character));
    }

    [Fact]
    public void BowWithoutAmmo_MeleeWeaponNeedsNoAmmo()
    {
        var character = Character((Serial)0x731A);
        Wear(character, new Katana((Serial)0x741A) { Layer = Layer.OneHanded });

        Assert.False(GearEquip.BowWithoutAmmo(character));
    }

    [Fact]
    public void EquipReadyWeapon_EmptyHands_DrawTheBestPackWeapon()
    {
        // The pickaxe went into the pack first; the exceptional katana must still come out.
        var character = Character((Serial)0x731C);
        character.Backpack.AddItem(new Pickaxe());
        var katana = new Katana { Quality = WeaponQuality.Exceptional };
        character.Backpack.AddItem(katana);

        Assert.Same(katana, GearEquip.BestPackWeapon(character, allowRanged: true));
    }

    [Fact]
    public void EquipReadyWeapon_WhileCasting_TheWeaponWaits()
    {
        // Every arrow on a casting mage drew its weapon, and the equip broke the Magic Arrow
        // a blow could not break before AOS: 87 of 97 casts in one fight.
        var character = Character((Serial)0x7324);
        var katana = new Katana((Serial)0x7424);
        character.Backpack.AddItem(katana);
        var spell = new WordsInTheHands();
        character.Spell = spell;

        Assert.False(GearEquip.EquipReadyWeapon(character));
        Assert.False(spell.EquipAsked);
        Assert.True(katana.IsChildOf(character.Backpack));

        character.Spell = null;
    }

    [Fact]
    public void BestPackWeapon_DryBow_SkipsRangedWeapons()
    {
        var character = Character((Serial)0x731E);
        character.Build = Trains(CombatStyle.Archer, SkillName.Archery, SkillName.Swords);
        character.Backpack.AddItem(new HeavyCrossbow());
        var sword = new Broadsword();
        character.Backpack.AddItem(sword);

        Assert.Same(sword, GearEquip.BestPackWeapon(character, allowRanged: false));
    }

    [Fact]
    public void CanArm_WeaponInThePack_LeavesTheToolInHand()
    {
        var character = Character((Serial)0x7320);
        var pole = new FishingPole((Serial)0x7420) { Layer = Layer.TwoHanded };
        Wear(character, pole);
        character.Backpack.AddItem(new Katana());

        Assert.True(GearEquip.CanArm(character));
        Assert.Same(pole, character.FindItemOnLayer(Layer.TwoHanded));
    }

    [Fact]
    public void BestPackWeapon_LootedWeaponOfAnotherSkill_IsGoods()
    {
        // A swordsman keeps its katana; the mace off an orc is for the shop.
        var character = Character((Serial)0x7330);
        character.Backpack.AddItem(new Mace());

        Assert.Null(GearEquip.BestPackWeapon(character, allowRanged: true));
        Assert.False(GearEquip.CanArm(character));
        Assert.False(GearEquip.EquipReadyWeapon(character));
    }

    [Fact]
    public void BestPackWeapon_Mage_FindsNoWeapon()
    {
        var mage = Mage((Serial)0x7332);
        mage.Backpack.AddItem(new Katana());
        mage.Backpack.AddItem(new Mace());

        Assert.Null(GearEquip.BestPackWeapon(mage, allowRanged: true));
        Assert.False(GearEquip.HasOwnWeapon(mage));
    }

    [Fact]
    public void EquipReadyWeapon_MageHoldingAMace_PutsItInThePack()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        // Mages drew a looted mace between casts and walked in to swing it.
        var mage = Mage((Serial)0x7334);
        var mace = new Mace { Layer = Layer.OneHanded };
        Wear(mage, mace);

        Assert.False(GearEquip.EquipReadyWeapon(mage));
        Assert.True(mace.IsChildOf(mage.Backpack));
        Assert.Null(mage.FindItemOnLayer(Layer.OneHanded));
    }

    [Fact]
    public void EquipReadyWeapon_FighterHoldingAWeaponOfAnotherSkill_PutsItInThePack()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        var character = Character((Serial)0x7336);
        var mace = new Mace { Layer = Layer.OneHanded };
        Wear(character, mace);

        GearEquip.EquipReadyWeapon(character);

        Assert.True(mace.IsChildOf(character.Backpack));
    }

    [Fact]
    public void CanArm_Mage_ReadsItsSpellbook_NotAWeapon()
    {
        var mage = Mage((Serial)0x7338);
        mage.Backpack.AddItem(new Mace());

        Assert.False(GearEquip.CanArm(mage));

        mage.Backpack.AddItem(new Spellbook());

        Assert.True(GearEquip.CanArm(mage));
    }

    [Fact]
    public void EquipOrPack_WeaponOfAnotherSkill_GoesToThePack()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        var character = Character((Serial)0x733A);
        var mace = new Mace { Layer = Layer.OneHanded };

        GearEquip.EquipOrPack(character, mace);

        Assert.True(mace.IsChildOf(character.Backpack));
        Assert.Null(character.FindItemOnLayer(Layer.OneHanded));
    }

    [Fact]
    public void EquipOrPack_FightersTravelBook_GoesToThePack_AndTheSwordStays()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        var character = Character((Serial)0x733C);
        var katana = new Katana { Layer = Layer.OneHanded };
        Wear(character, katana);
        var book = new Spellbook();

        GearEquip.EquipOrPack(character, book);

        Assert.True(book.IsChildOf(character.Backpack));
        Assert.Same(katana, character.FindItemOnLayer(Layer.OneHanded));
    }

    [Fact]
    public void EquipOrPack_ShieldBesideTheSword_BothStayWorn()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        var character = Fighter((Serial)0x733E);
        var katana = new Katana { Layer = Layer.OneHanded };
        Wear(character, katana);
        var shield = new MetalShield { Layer = Layer.TwoHanded };

        GearEquip.EquipOrPack(character, shield);

        Assert.Same(katana, character.FindItemOnLayer(Layer.OneHanded));
        Assert.Same(shield, character.FindItemOnLayer(Layer.TwoHanded));
    }

    [Fact]
    public void WearFromPack_LegArmorGoesOnOverClothPants()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        // The pants a stripped body gets back held the legs layer, and the spare kit's legs
        // came out at the bank and went back in on every visit.
        var character = Fighter((Serial)0x7326);
        var pants = new LongPants { Layer = Layer.Pants, Movable = true };
        Wear(character, pants);
        var legs = new LeatherLegs { Layer = Layer.Pants, Movable = true };
        character.Backpack.AddItem(legs);

        Assert.Equal(1, GearEquip.WearFromPack(character, int.MaxValue, robeLook: false));
        Assert.Same(legs, character.FindItemOnLayer(Layer.Pants));
        Assert.True(pants.IsChildOf(character.Backpack));
    }

    [Fact]
    public void WearFromPack_ClothStaysOffArmor()
    {
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        var character = Fighter((Serial)0x7328);
        var legs = new LeatherLegs { Layer = Layer.Pants, Movable = true };
        Wear(character, legs);
        var pants = new LongPants { Layer = Layer.Pants, Movable = true };
        character.Backpack.AddItem(pants);

        Assert.Equal(0, GearEquip.WearFromPack(character, int.MaxValue, robeLook: false));
        Assert.Same(legs, character.FindItemOnLayer(Layer.Pants));
        Assert.True(pants.IsChildOf(character.Backpack));
    }

    [Fact]
    public void CanArm_EmptyHandsAndPack_IsFalse() =>
        Assert.False(GearEquip.CanArm(Character((Serial)0x7322)));

    /// <summary>A pure mage: it fights with its spellbook.</summary>
    private static SosariaCharacter Mage(Serial serial)
    {
        var character = Character(serial);
        character.Build = BuildPresets.MageNovice();
        return character;
    }

    /// <summary>A build of <paramref name="style"/> that trains only <paramref name="skills"/>.</summary>
    private static ResolvedBuild Trains(CombatStyle style, params SkillName[] skills)
    {
        var trained = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            trained[skill.ToString()] = TrainedSkill;
        }

        return new ResolvedBuild { Style = style, Skills = trained };
    }

    /// <summary>A person with a fighter's stats, strong enough for any armor.</summary>
    private static SosariaCharacter Fighter(Serial serial)
    {
        var character = Character(serial);
        character.RawStr = FighterStat;
        character.RawDex = FighterStat;
        character.RawInt = FighterStat;
        return character;
    }

    private static SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        Wear(character, new Backpack((Serial)(serial.Value + 0x100)) { Layer = Layer.Backpack });
        return character;
    }

    /// <summary>A spell being said: records an equip that would break it, and refuses it.</summary>
    private sealed class WordsInTheHands : ISpell
    {
        public bool EquipAsked { get; private set; }

        public bool BlocksMovement => true;

        public bool IsCasting => true;

        public void OnCasterHurt()
        {
        }

        public void OnCasterKilled()
        {
        }

        public void OnConnectionChanged()
        {
        }

        public bool OnCasterMoving(Direction d) => false;

        public bool OnCasterEquipping(Item item)
        {
            EquipAsked = true;
            return false;
        }

        public bool OnCasterUsingObject(IEntity entity) => false;

        public bool OnCastInTown(Region r) => true;

        public void FinishSequence()
        {
        }
    }

    /// <summary>Wears an item the way a real equip does: parent set and on the item list.</summary>
    private static void Wear(SosariaCharacter character, Item item)
    {
        item.Parent = character;
        character.Items.Add(item);
    }
}
