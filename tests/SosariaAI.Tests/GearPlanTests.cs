using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// One piece a trip: first what the person lost, then the next rung of its class's armor,
/// never above what its tier and purse allow and never a plain piece over better work.
/// </summary>
public class GearPlanTests
{
    private const int Reserve = CareerSettings.DefaultGoldReserve;
    private const int WalkingMoney = 1000;
    private const int Broke = Reserve;
    private const string Katana = "Katana";
    private const string Scimitar = "Scimitar";
    private const string Buckler = "Buckler";

    [Fact]
    public void Affordable_KeepsReserve()
    {
        Assert.True(GearPlan.Affordable(Reserve + GearPlan.WeaponBudget, GearPlan.WeaponBudget, Reserve));
        Assert.False(GearPlan.Affordable(Reserve + GearPlan.WeaponBudget - 1, GearPlan.WeaponBudget, Reserve));
    }

    [Fact]
    public void PackReserve_BankGoldCoversTheReserveFirst()
    {
        Assert.Equal(Reserve, GearPlan.PackReserve(bankGold: 0, Reserve));
        Assert.Equal(Reserve / 2, GearPlan.PackReserve(bankGold: Reserve / 2, Reserve));
        Assert.Equal(0, GearPlan.PackReserve(bankGold: Reserve, Reserve));
        Assert.Equal(0, GearPlan.PackReserve(bankGold: Reserve * 10, Reserve));
        Assert.Equal(Reserve, GearPlan.PackReserve(bankGold: -1, Reserve));
    }

    [Fact]
    public void Affordable_RejectsNonPositivePrice()
    {
        Assert.False(GearPlan.Affordable(Reserve, 0, Reserve));
        Assert.False(GearPlan.Affordable(Reserve, -1, Reserve));
    }

    [Fact]
    public void Fits_ThePlannedPieceOrItsSpareType()
    {
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Chainmail, armed: false), WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.True(GearPlan.Fits(buy.Value, Scimitar));
        Assert.True(GearPlan.Fits(buy.Value, Katana));
        Assert.False(GearPlan.Fits(buy.Value, Buckler));
        Assert.False(GearPlan.Fits(buy.Value, null));
    }

    [Fact]
    public void StrippedFighter_BuysItsOwnKitWeaponFirst()
    {
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Chainmail, armed: false), WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal(GearBuyKind.Weapon, buy.Value.Kind);
        Assert.Equal(Scimitar, buy.Value.ItemTypeName);
        Assert.Equal(Katana, buy.Value.AlternateTypeName);
        Assert.Equal(ShopFinder.SmithToken, buy.Value.VendorDestination);
    }

    [Fact]
    public void ArmedButBareFighter_BuysAChestAtItsTarget()
    {
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Chainmail, armed: true), WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal("ChainChest", buy.Value.ItemTypeName);
        Assert.Equal(GearSlot.Chest, buy.Value.Slot);
        Assert.Equal(GearLadder.ChainmailScore, buy.Value.GearScore);
    }

    [Fact]
    public void ThinPurse_BuysTheBestRungItCanPay()
    {
        var ringChest = GearLadder.Piece(GearSlot.Chest, GearMaterial.Ringmail, false)!.Value;
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Plate, armed: true), Reserve + ringChest.Price, Reserve);

        Assert.NotNull(buy);
        Assert.Equal(ringChest.TypeName, buy.Value.ItemTypeName);
    }

    [Fact]
    public void ExceptionalChest_IsNotSwappedForTheSameRungPlain()
    {
        var needs = Warrior(GearMaterial.Chainmail, armed: true, Suit(GearLadder.ChainmailScore + GearScore.ExceptionalBonus));

        Assert.Null(GearPlan.NextBuy(needs, WalkingMoney, Reserve));
    }

    [Fact]
    public void ExceptionalChain_StillClimbsToPlate()
    {
        var needs = Warrior(GearMaterial.Plate, armed: true, Suit(GearLadder.ChainmailScore + GearScore.ExceptionalBonus));
        var buy = GearPlan.NextBuy(needs, WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal("PlateChest", buy.Value.ItemTypeName);
        Assert.True(buy.Value.Upgrade);
    }

    [Fact]
    public void LostPieces_AreReplacedBeforeAnythingIsBettered()
    {
        var ranks = Suit(GearLadder.LeatherScore);
        ranks.Remove(GearSlot.Gloves);
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Plate, armed: true, ranks), WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal(GearSlot.Gloves, buy.Value.Slot);
        Assert.False(buy.Value.Upgrade);
    }

    [Fact]
    public void RecentUpgrade_WaitsItsTurn_ButALostPieceIsStillReplaced()
    {
        var suit = Suit(GearLadder.LeatherScore);
        var paced = new GearNeeds
        {
            Weight = KitArmor.Heavy, Target = GearMaterial.Plate, Armed = true, WornRanks = suit, MayUpgrade = false
        };

        Assert.Null(GearPlan.NextBuy(paced, WalkingMoney, Reserve));

        suit.Remove(GearSlot.Chest);
        var lost = GearPlan.NextBuy(paced, WalkingMoney, Reserve);

        Assert.NotNull(lost);
        Assert.Equal(GearSlot.Chest, lost.Value.Slot);
        Assert.True(GearPlan.UpgradeInterval > System.TimeSpan.Zero);
    }

    [Fact]
    public void ChestAndLegsDone_TheShieldComesBeforeTheLimbs()
    {
        var ranks = Suit(GearLadder.RingmailScore);
        ranks.Remove(GearSlot.Helm);
        var needs = Warrior(GearMaterial.Ringmail, armed: true, ranks, Buckler);

        var buy = GearPlan.NextBuy(needs, WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal(GearBuyKind.Shield, buy.Value.Kind);
        Assert.Equal(Buckler, buy.Value.ItemTypeName);
    }

    [Fact]
    public void FemaleBuyer_GetsTheFemaleCut()
    {
        var needs = new GearNeeds
        {
            Weight = KitArmor.Heavy, Target = GearMaterial.Plate, Female = true, Armed = true
        };

        Assert.Equal("FemalePlateChest", GearPlan.NextBuy(needs, WalkingMoney, Reserve)!.Value.ItemTypeName);
        Assert.Equal(ShopFinder.TannerToken, GearPlan.NextBuy(needs, WalkingMoney, Reserve)!.Value.VendorDestination);
    }

    [Fact]
    public void LeatherPieces_AreBoughtFromTheTanner()
    {
        var needs = new GearNeeds { Weight = KitArmor.Light, Target = GearMaterial.Studded, Armed = true };
        var buy = GearPlan.NextBuy(needs, WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal("StuddedChest", buy.Value.ItemTypeName);
        Assert.Equal(ShopFinder.TannerToken, buy.Value.VendorDestination);
    }

    [Fact]
    public void PureMage_BuysNoArmor_ButARobeWhenItsLookIsOne()
    {
        var mage = new GearNeeds { Weight = KitArmor.None, Target = GearMaterial.None, Armed = true, WantsRobe = true };

        var buy = GearPlan.NextBuy(mage, WalkingMoney, Reserve);

        Assert.NotNull(buy);
        Assert.Equal(GearPlan.RobeType, buy.Value.ItemTypeName);
        Assert.Equal(ShopFinder.TailorToken, buy.Value.VendorDestination);
        Assert.Null(GearPlan.NextBuy(new GearNeeds { Weight = KitArmor.None, Armed = true, RobeWorn = true }, WalkingMoney, Reserve));
    }

    [Fact]
    public void Worker_OnlyReplacesItsWorkGloves()
    {
        var worker = new GearNeeds { Weight = KitArmor.Work, Target = GearMaterial.Leather, Armed = true };

        Assert.Equal("LeatherGloves", GearPlan.NextBuy(worker, WalkingMoney, Reserve)!.Value.ItemTypeName);

        var gloved = new GearNeeds
        {
            Weight = KitArmor.Work,
            Target = GearMaterial.Leather,
            Armed = true,
            WornRanks = new Dictionary<GearSlot, int> { [GearSlot.Gloves] = GearLadder.LeatherScore }
        };

        Assert.Null(GearPlan.NextBuy(gloved, WalkingMoney, Reserve));
    }

    [Fact]
    public void ReserveBlocksEveryBuy_OfAnArmedPerson() =>
        Assert.Null(GearPlan.NextBuy(Warrior(GearMaterial.Plate, armed: true), Broke, Reserve));

    [Fact]
    public void ScoreOf_KnownPieces_RankAtTheirTier()
    {
        Assert.Equal(GearPlan.KatanaGearScore, GearPlan.ScoreOf(Katana));
        Assert.Equal(GearPlan.KatanaGearScore, GearPlan.ScoreOf("katana"));
        Assert.Equal(GearLadder.LeatherScore, GearPlan.ScoreOf("LeatherChest"));
        Assert.Equal(GearLadder.PlateScore, GearPlan.ScoreOf("FemalePlateChest"));
        Assert.Equal(GearLadder.BoneScore, GearPlan.ScoreOf("BoneLegs"));
        Assert.Equal(GearPlan.HeavyCrossbowGearScore, GearPlan.ScoreOf("HeavyCrossbow"));
    }

    [Fact]
    public void ScoreOf_BareSlotDeathRobeAndUnlistedGear()
    {
        Assert.Equal(GearPlan.NoGearScore, GearPlan.ScoreOf(null));
        Assert.Equal(GearPlan.NoGearScore, GearPlan.ScoreOf(""));
        // A death robe is no armor: a person in one still shops for a chest.
        Assert.Equal(GearPlan.NoGearScore, GearPlan.ScoreOf(GearPlan.DeathRobeType));
        // Exotic or looted gear counts as the best on the shelf: never "upgraded" down.
        Assert.Equal(GearPlan.UnlistedGearScore, GearPlan.ScoreOf("HalberdOfVanquishing"));
    }

    private static GearNeeds Warrior(GearMaterial target, bool armed, Dictionary<GearSlot, int> ranks = null, string shield = null) =>
        new()
        {
            Weight = KitArmor.Heavy,
            Target = target,
            Weapon = Scimitar,
            WeaponRow = Katana,
            Armed = armed,
            Shield = shield,
            WornRanks = ranks ?? new Dictionary<GearSlot, int>()
        };

    private static Dictionary<GearSlot, int> Suit(int rank)
    {
        var ranks = new Dictionary<GearSlot, int>();

        foreach (var slot in GearLadder.Slots(KitArmor.Heavy))
        {
            ranks[slot] = rank;
        }

        return ranks;
    }
    [Fact]
    public void SpareOffer_TheKitWeaponAtTheSmith_NeverWorn()
    {
        var offer = GearPlan.SpareOffer(Katana, Katana, Scimitar, WalkingMoney, Reserve);

        Assert.NotNull(offer);
        Assert.Equal(GearBuyKind.Spare, offer.Value.Kind);
        Assert.Equal(Katana, offer.Value.ItemTypeName);
        Assert.Equal(Scimitar, offer.Value.AlternateTypeName);
        Assert.Equal(GearPlan.WeaponBudget, offer.Value.Price);
        Assert.Equal(ShopFinder.SmithToken, offer.Value.VendorDestination);
        Assert.False(offer.Value.Upgrade);
    }

    [Fact]
    public void SpareOffer_ShieldAndShelfArmor_AtTheirShelfPrices()
    {
        var shelf = GearLadder.Pieces[0];

        var shield = GearPlan.SpareOffer(Buckler, Katana, Katana, WalkingMoney, Reserve);
        var armor = GearPlan.SpareOffer(shelf.TypeName, Katana, Katana, Reserve + shelf.Price, Reserve);

        Assert.NotNull(shield);
        Assert.NotNull(armor);
        Assert.Equal(GearBuyKind.Spare, shield.Value.Kind);
        Assert.Equal(Buckler, shield.Value.ItemTypeName);
        Assert.Equal(GearBuyKind.Spare, armor.Value.Kind);
        Assert.Equal(shelf.Price, armor.Value.Price);
        Assert.Equal(shelf.Vendor, armor.Value.VendorDestination);
        Assert.Equal(shelf.Slot, armor.Value.Slot.Value);
    }

    [Fact]
    public void SpareOffer_NoneWhenBrokeOrNoShopSellsIt()
    {
        Assert.Null(GearPlan.SpareOffer(Katana, Katana, Katana, Broke, Reserve));
        Assert.Null(GearPlan.SpareOffer(Buckler, Katana, Katana, Broke, Reserve));
        Assert.Null(GearPlan.SpareOffer(GearPlan.RobeType, Katana, Katana, WalkingMoney, Reserve));
        Assert.Null(GearPlan.SpareOffer(null, Katana, Katana, WalkingMoney, Reserve));
    }

    [Fact]
    public void IsShelfShield_KnowsTheSmithsShields()
    {
        Assert.True(GearPlan.IsShelfShield("HeaterShield"));
        Assert.True(GearPlan.IsShelfShield("buckler"));
        Assert.False(GearPlan.IsShelfShield("PlateChest"));
        Assert.False(GearPlan.IsShelfShield(null));
    }

    [Fact]
    public void NextBuy_Unarmed_SpendsTheReserveOnItsWeapon()
    {
        var buy = GearPlan.NextBuy(Warrior(GearMaterial.Chainmail, armed: false), GearPlan.WeaponBudget, Reserve);

        Assert.NotNull(buy);
        Assert.True(buy.Value.Price <= GearPlan.WeaponBudget);
    }
}
