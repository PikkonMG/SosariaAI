using System;
using System.Linq;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The shard's save held people in their death robes with their armor gone, leg armor
/// hidden under cloth pants and kilts over plate. A living person puts that right the way
/// a player does, and a stripped one goes shopping for its kit.
/// </summary>
[Collection(RealMapCollection.Name)]
public class SavedKitRepairTests
{
    private const int WalkingMoney = 1000;

    public SavedKitRepairTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void Resurrected_ShedsTheDeathRobe_AndWearsItsOwnArmorFromThePack()
    {
        var character = Warrior("Felucca:robe#1");
        var chest = character.FindItemOnLayer(Layer.InnerTorso);
        character.AddToBackpack(chest);
        var robe = new DeathRobe();
        character.AddItem(robe);

        character.EnsureFightingForm();

        Assert.True(robe.Deleted);
        Assert.Same(chest, character.FindItemOnLayer(Layer.InnerTorso));
    }

    [RealMapFact]
    public void ClothPantsUnderLegArmor_AndAKiltOverIt_GoToThePack()
    {
        var character = Warrior("Felucca:legs#1");
        var legs = character.FindItemOnLayer(Layer.Pants);
        Assert.IsAssignableFrom<BaseArmor>(legs);

        // The old kit pass added armor without a layer check, beside the starter pants.
        var pants = new LongPants();
        character.Items.Insert(0, pants);
        pants.Parent = character;
        var kilt = new Kilt();
        character.AddItem(kilt);

        character.EnsureFightingForm();

        Assert.Same(legs, character.FindItemOnLayer(Layer.Pants));
        Assert.True(pants.IsChildOf(character.Backpack));
        Assert.True(kilt.IsChildOf(character.Backpack));
        Assert.Single(character.Items, item => item.Layer == Layer.Pants);
    }

    [RealMapFact]
    public void PureMage_NeverPutsOnPackedPlate()
    {
        var mage = PureMage();
        var plate = new PlateChest();
        mage.AddToBackpack(plate);

        mage.EnsureFightingForm();

        Assert.True(plate.IsChildOf(mage.Backpack));
        Assert.False(GearEquip.WearsBodyArmor(mage));
    }

    [RealMapFact]
    public void MageSavedInPlate_TakesItOffIntoThePack()
    {
        // A saved mage in plate from an older kit: plate stops meditation, so it comes off.
        var mage = PureMage();
        var plate = new PlateChest();
        mage.AddItem(plate);

        mage.EnsureFightingForm();

        Assert.True(plate.IsChildOf(mage.Backpack));
        Assert.False(GearEquip.WearsBodyArmor(mage));
    }

    [RealMapFact]
    public void TankMage_KeepsStudded_ButTakesOffAPlateHelm()
    {
        var mage = FirstDressed(
            "Felucca:tank#1",
            KitWorld.Profile(PersonClass.Mage, SkillTier.Expert, PersonWealth.Modest, false),
            character => character.FindItemOnLayer(Layer.TwoHanded) is BaseWeapon && character.FindItemOnLayer(Layer.InnerTorso) is StuddedChest
        );
        mage.FindItemOnLayer(Layer.Helm)?.Delete();
        var helm = new PlateHelm();
        mage.AddItem(helm);

        mage.EnsureFightingForm();

        Assert.True(helm.IsChildOf(mage.Backpack));
        Assert.IsType<StuddedChest>(mage.FindItemOnLayer(Layer.InnerTorso));
    }

    [RealMapFact]
    public void TamerOutOfPlate_ShopsForLeather()
    {
        var tamer = KitWorld.Dress(KitWorld.Profile(PersonClass.Tamer, SkillTier.Expert, PersonWealth.Modest, false), "Felucca:osric#1");
        tamer.FindItemOnLayer(Layer.InnerTorso)?.Delete();
        var plate = new PlateChest();
        tamer.AddItem(plate);
        tamer.Backpack.DropItem(new Gold(WalkingMoney));

        tamer.EnsureFightingForm();
        var offer = UpgradeGearSkill.OfferFor(tamer);

        Assert.True(plate.IsChildOf(tamer.Backpack));
        Assert.NotNull(offer);
        Assert.Equal(GearSlot.Chest, offer.Value.Slot);
        Assert.Equal(nameof(LeatherChest), offer.Value.ItemTypeName);
    }

    [RealMapFact]
    public void Warrior_NeverPutsARobeOverItsArmor()
    {
        var character = Warrior("Felucca:robe#2");
        var robe = new Robe();
        character.AddToBackpack(robe);

        character.EnsureFightingForm();

        Assert.True(robe.IsChildOf(character.Backpack));
    }

    [RealMapFact]
    public void StrippedFighter_ShopsForItsWeaponThenItsChest()
    {
        var character = Warrior("Felucca:strip#1");
        Strip(character);
        character.Backpack.DropItem(new Gold(WalkingMoney));

        var first = UpgradeGearSkill.OfferFor(character);

        Assert.NotNull(first);
        Assert.Equal(GearBuyKind.Weapon, first.Value.Kind);

        character.AddItem(new Katana());
        var second = UpgradeGearSkill.OfferFor(character);

        Assert.NotNull(second);
        Assert.Equal(GearBuyKind.Armor, second.Value.Kind);
        Assert.Equal(GearSlot.Chest, second.Value.Slot);
    }

    [RealMapFact]
    public void FullyKittedVeteran_HasNothingToBuy()
    {
        var character = FirstDressed(
            "Felucca:full#1",
            KitWorld.Profile(PersonClass.Warrior, SkillTier.Grandmaster, PersonWealth.Poor, false),
            dressed => dressed.FindItemOnLayer(Layer.TwoHanded) is BaseShield && dressed.FindItemOnLayer(Layer.Helm) is PlateHelm &&
                       dressed.FindItemOnLayer(Layer.InnerTorso) is PlateChest
        );
        character.Backpack.DropItem(new Gold(WalkingMoney));

        Assert.Null(UpgradeGearSkill.OfferFor(character));
    }

    /// <summary>A warrior with a one-handed weapon and a shield in a metal suit, so every hand and body layer is full.</summary>
    private static SosariaCharacter Warrior(string id, SkillTier tier = SkillTier.Expert, PersonWealth wealth = PersonWealth.Modest) =>
        FirstDressed(id, KitWorld.Profile(PersonClass.Warrior, tier, wealth, false), character =>
            character.FindItemOnLayer(Layer.TwoHanded) is BaseShield && character.FindItemOnLayer(Layer.InnerTorso) is not BoneChest);

    private static SosariaCharacter FirstDressed(string id, PersonProfile profile, Func<SosariaCharacter, bool> wanted)
    {
        for (var i = 0; ; i++)
        {
            var character = KitWorld.Dress(profile, $"{id}.{i}");

            if (wanted(character))
            {
                return character;
            }
        }
    }

    private static SosariaCharacter PureMage() =>
        FirstDressed("Felucca:mage#1", KitWorld.Profile(PersonClass.Mage, SkillTier.Expert, PersonWealth.Modest, false), character =>
            character.FindItemOnLayer(Layer.OneHanded) is Spellbook);

    private static void Strip(SosariaCharacter character)
    {
        foreach (var item in character.Items.Where(item => item is BaseArmor or BaseWeapon).ToList())
        {
            item.Delete();
        }

        foreach (var item in character.Backpack.Items.Where(item => item is BaseArmor or BaseWeapon).ToList())
        {
            item.Delete();
        }
    }
}
