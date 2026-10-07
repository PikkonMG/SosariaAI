using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// What a character puts in its bank box, read through the kit it keeps: kit pieces are
/// found by type name in the loaded content, so this runs in the real-map collection with
/// the world <see cref="KitWorld"/> sets up, and skips without the client data.
/// </summary>
[Collection(RealMapCollection.Name)]
public class BankDepositBankableTests
{
    public BankDepositBankableTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void IsBankable_PlainHarvestWaitsForAShopInReach_ElseGoesInTheBox()
    {
        var owner = Owner("Felucca:BankableHarvest#1");
        var logs = new Log((Serial)0x7F61) { Movable = true };
        var ore = new IronOre((Serial)0x7F62) { Movable = true };

        Assert.False(BankDepositSkill.IsBankable(owner, logs, _ => true, trade: null));
        Assert.False(BankDepositSkill.IsBankable(owner, ore, _ => true, trade: null));
        Assert.True(BankDepositSkill.IsBankable(owner, logs, _ => false, trade: null));
        Assert.True(BankDepositSkill.IsBankable(owner, ore, _ => false, trade: null));
    }

    [RealMapFact]
    public void IsBankable_HarvestNoShopBuysGoesInTheBox_AndToolsStayInThePack()
    {
        var owner = Owner("Felucca:BankableHarvest#2");

        Assert.True(BankDepositSkill.IsBankable(owner, new OakLog((Serial)0x7F63) { Movable = true }, _ => true, trade: null));
        Assert.False(BankDepositSkill.IsBankable(owner, new Pickaxe((Serial)0x7F64) { Movable = true }, _ => false, trade: null));
        Assert.False(BankDepositSkill.IsBankable(owner, new Gold((Serial)0x7F65) { Movable = true }, _ => false, trade: null));
    }

    [RealMapFact]
    public void BuyerInReach_OffTheMapNobodyBuys()
    {
        var owner = Owner("Felucca:BankableHarvest#3");

        Assert.False(SaleGoods.BuyerInReach(owner)(new Log((Serial)0x7F66) { Movable = true }));
    }

    [RealMapFact]
    public void IsBankable_TheStockTheOwnTradeBurnsStaysInThePack()
    {
        // A tailor banked its cloth and then stood at the station out of cloth.
        var owner = Owner("Felucca:BankableHarvest#4");
        var cloth = new Cloth((Serial)0x7F67) { Movable = true };

        Assert.False(BankDepositSkill.IsBankable(owner, cloth, _ => false, TailorRules.Trade));
        Assert.True(BankDepositSkill.IsBankable(owner, cloth, _ => false, trade: null));
    }

    [RealMapFact]
    public void IsBankable_RunesAndTheRunebookStayInThePack()
    {
        // The box takes the rune in, the supply draw takes it back out for the next Mark, and
        // the count of what is left failed the trip: "the bank box would not take everything"
        // on every visit of the scribes and the marking warriors.
        var owner = Owner("Felucca:BankableHarvest#5");

        Assert.False(BankDepositSkill.IsBankable(owner, new RecallRune((Serial)0x7F68) { Movable = true }, _ => false, trade: null));
        Assert.False(BankDepositSkill.IsBankable(owner, new Runebook((Serial)0x7F69) { Movable = true }, _ => false, trade: null));
    }

    [RealMapFact]
    public void IsBankable_LockpicksStayInThePack()
    {
        // Valka banked her twenty lockpicks, the supply draw took them straight back out, and
        // the stack left in the pack sent her to the Den's counter every two seconds.
        var owner = Owner("Felucca:BankableHarvest#6");

        Assert.False(BankDepositSkill.IsBankable(owner, new Lockpick((Serial)0x7F77) { Movable = true }, _ => false, trade: null));
    }

    [RealMapFact]
    public void HasErrand_LockpicksAloneAreNoErrand()
    {
        var owner = Carrier("Felucca:BankErrand#1");
        owner.AddToBackpack(new Lockpick(LockpickStack));

        Assert.False(BankDepositSkill.HasErrand(owner));
    }

    [RealMapFact]
    public void HasErrand_AFullBoxIsNoErrand_RoomInItIsOne()
    {
        var owner = Carrier("Felucca:BankErrand#2");
        var box = owner.BankBox;
        box.DropItem(new Katana());
        box.MaxItems = box.TotalItems;
        owner.AddToBackpack(new Amber());

        Assert.False(BankDepositSkill.HasErrand(owner));

        box.MaxItems = box.TotalItems + OneSlot;

        Assert.True(BankDepositSkill.HasErrand(owner));
    }

    [RealMapFact]
    public void BoxTakes_AFullBoxTakesOnlyWhatStacksOntoItsPile()
    {
        var owner = Carrier("Felucca:BankErrand#3");
        var box = owner.BankBox;
        box.DropItem(new Amber(AmberPile));
        box.MaxItems = box.TotalItems;

        Assert.True(BankDepositSkill.BoxTakes(box, owner, new Amber()));
        Assert.False(BankDepositSkill.BoxTakes(box, owner, new Katana()));
        Assert.False(BankDepositSkill.BoxTakes(null, owner, new Amber()));
    }

    [RealMapFact]
    public void BoxTakes_ABagCountsWithEverythingInIt()
    {
        // A box with one slot left has no room for a bag of three: the bag counts as one.
        var owner = Carrier("Felucca:BankErrand#4");
        var box = owner.BankBox;
        box.MaxItems = box.TotalItems + OneSlot;
        var fullBag = new Bag();
        fullBag.DropItem(new Katana());
        var emptyBag = new Bag();

        Assert.False(BankDepositSkill.BoxTakes(box, owner, fullBag));
        Assert.True(BankDepositSkill.BoxTakes(box, owner, emptyBag));
    }

    [RealMapFact]
    public void HasErrand_AHeavyPurseIsNoErrandWhenTheBoxRefusesGold()
    {
        var owner = Carrier("Felucca:BankErrand#5");
        var box = owner.BankBox;
        box.DropItem(new Katana());
        box.MaxItems = box.TotalItems;
        owner.AddToBackpack(new Gold(HeavyPurse));

        Assert.False(BankTeller.BoxTakesGold(box));
        Assert.False(BankDepositSkill.HasErrand(owner));

        box.MaxItems = box.TotalItems + OneSlot;

        Assert.True(BankTeller.BoxTakesGold(box));
        Assert.True(BankDepositSkill.HasErrand(owner));
    }

    [RealMapFact]
    public void BoxTakesGold_APileWithRoomTakesItInAFullBox()
    {
        var owner = Carrier("Felucca:BankErrand#6");
        var box = owner.BankBox;
        box.DropItem(new Gold(SmallPile));
        box.MaxItems = box.TotalItems;

        Assert.True(BankTeller.BoxTakesGold(box));
        Assert.False(BankTeller.BoxTakesGold(null));
    }

    /// <summary>
    /// An armed fighter with gold above its pocket money and an empty box goes to fill its
    /// rebuy fund; stripped of its tunic it keeps its gold for the rebuy and makes no trip.
    /// </summary>
    [RealMapFact]
    public void HasErrand_AnArmedFighterFillsItsRebuyFund_AStrippedOneKeepsItsGold()
    {
        var owner = Carrier("Felucca:BankErrand#7");
        owner.Build = BuildPresets.SwordsmanNovice();
        TestArms.Arm(owner);
        owner.AddToBackpack(new Gold(BankTellerRules.PocketMoney + BankTellerRules.MinTransaction));
        _ = owner.BankBox;

        Assert.True(SpareKit.Armed(owner));
        Assert.True(BankDepositSkill.HasErrand(owner));

        owner.FindItemOnLayer(Layer.InnerTorso).Delete();

        Assert.False(SpareKit.Armed(owner));
        Assert.False(BankDepositSkill.HasErrand(owner));
    }

    private const int HeavyPurse = BankTellerRules.WalkingMoney * BankTellerRules.HeavyPurseMultiple + 1;
    private const int SmallPile = 100;
    private const int LockpickStack = 20;
    private const int AmberPile = 3;
    private const int OneSlot = 1;

    private static SosariaCharacter Owner(string id) => KitWorld.Blank(id, female: false);

    /// <summary>A blank copy with an empty pack, and an empty bank box once it is asked for.</summary>
    private static SosariaCharacter Carrier(string id)
    {
        var owner = Owner(id);
        owner.AddItem(new Backpack());
        return owner;
    }
}
