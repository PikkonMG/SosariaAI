using System;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GearEquipTests
{
    private const int OneArrow = 1;
    private const int FighterStat = 100;

    static GearEquipTests() => Timer.Init(0);

    public GearEquipTests() => TestMap.EnsureInternal();

    [Fact]
    public void ConflictingLayers_HandLayers_CoverBothHands()
    {
        // EquipItem only checks the item's own layer, so a two-handed tool slipped
        // onto the free hand and showed a hatchet and a pickaxe at once.
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.OneHanded));
        Assert.Equal([Layer.OneHanded, Layer.TwoHanded], GearEquip.ConflictingLayers(Layer.TwoHanded));
        Assert.Equal([Layer.InnerTorso], GearEquip.ConflictingLayers(Layer.InnerTorso));
    }

    [Fact]
    public void WornConflicts_TwoHanded_FlagsTheOneHandedWeapon()
    {
        var character = Character((Serial)0x7301);
        var hatchet = new Hatchet((Serial)0x7401) { Layer = Layer.OneHanded };
        character.Items.Add(hatchet);

        var conflicts = GearEquip.WornConflicts(character, Layer.TwoHanded);

        Assert.Same(hatchet, Assert.Single(conflicts));
    }

    [Fact]
    public void WornConflicts_OneHanded_FlagsTheTwoHandedWeapon()
    {
        var character = Character((Serial)0x7302);
        var staff = new QuarterStaff((Serial)0x7403) { Layer = Layer.TwoHanded };
        character.Items.Add(staff);

        var conflicts = GearEquip.WornConflicts(character, Layer.OneHanded);

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

        var conflicts = GearEquip.WornConflicts(character, Layer.TwoHanded, keep: pickaxe);

        Assert.Same(hatchet, Assert.Single(conflicts));
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
        character.Backpack.AddItem(new Pickaxe((Serial)0x741C));
        var katana = new Katana((Serial)0x741D) { Quality = WeaponQuality.Exceptional };
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
        character.Backpack.AddItem(new HeavyCrossbow((Serial)0x741E));
        var mace = new Mace((Serial)0x741F);
        character.Backpack.AddItem(mace);

        Assert.Same(mace, GearEquip.BestPackWeapon(character, allowRanged: false));
    }

    [Fact]
    public void CanArm_WeaponInThePack_LeavesTheToolInHand()
    {
        var character = Character((Serial)0x7320);
        var pole = new FishingPole((Serial)0x7420) { Layer = Layer.TwoHanded };
        Wear(character, pole);
        character.Backpack.AddItem(new Mace((Serial)0x7421));

        Assert.True(GearEquip.CanArm(character));
        Assert.Same(pole, character.FindItemOnLayer(Layer.TwoHanded));
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
