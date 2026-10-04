using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(BankStockTests.CollectionName, DisableParallelization = true)]
public class BankStockCollection;

/// <summary>
/// The player market at the bank: a dry smith's want draws a miner carrying ingots, the smith
/// finds the miner holding them up, a fighter finds the plate it needs in a smith's pack, and a
/// crafter holds its best pieces back from the shop to hawk. The offers and wants are read, never
/// the map, so only what was posted is found. The people stand on the shared test land, so the
/// class runs alone: another class moving items on that map at the same time races the sectors.
/// </summary>
[Collection(CollectionName)]
public class BankStockTests
{
    public const string CollectionName = "Bank stock market";

    // Ranges no other test class uses: tests that share serials collide in the one engine world.
    private const uint FirstMobileSerial = 0xB401;
    private const uint BackpackSerialOffset = 0x100;
    private const uint FirstItemSerial = 0x4000B601;
    private const int ArtTileCount = 0x4000;
    private const int IngotLoad = 50;
    private const int LogLoad = 20;
    private const int IngotAsking = 400;
    private const int RichPurse = 1000;
    private const int PoorPurse = 10;
    private const int FarTiles = 60;
    private const int NearTiles = 4;
    private const int ShortReach = 2;
    private const int CutUnits = 20;
    private const int LogStack = 50;
    private const int LeftTiles = 90;
    private const int PackGold = 100;
    private const int NextTile = 1;
    private const int GearPurse = 5000;
    private const int MaxBeats = 20;
    private const int AnyGearScore = 0;
    private const string PlateChestType = "PlateChest";
    private const string PlateLegsType = "PlateLegs";
    private const string IngotsKey = "ingots";
    private const string WoodKey = "wood";

    private static readonly Point3D Bank = new(20, 20, 0);
    private static readonly TimeSpan WantFor = TimeSpan.FromMinutes(1);

    private readonly List<IEntity> _made = [];
    private uint _nextMobile = FirstMobileSerial;
    private uint _nextItem = FirstItemSerial;

    static BankStockTests() => Timer.Init(0);

    public BankStockTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        EnsureItemBounds();

        // Goods for sale are told from kit pieces by type name, looked up in the content.
        AssemblyHandler.Assemblies ??= [typeof(Item).Assembly, typeof(Katana).Assembly];
    }

    [Fact]
    public void HawkersNear_ReadsEveryOfferHeldUp_NotOnlyTheCrowdsSeats()
    {
        var near = Character(PersonClass.Miner, Beside(NearTiles));
        var far = Character(PersonClass.Miner, Beside(FarTiles));
        var ingots = InPack(near, Ingots(IngotLoad));
        var logs = InPack(far, Logs(LogLoad));

        try
        {
            BankCrowd.SetHawkerOffer(near, new HawkerOffer(ingots.Serial, IngotAsking, Appraisal.NounOf(ingots)));
            BankCrowd.SetHawkerOffer(far, new HawkerOffer(logs.Serial, IngotAsking, Appraisal.NounOf(logs)));

            var found = BankCrowd.HawkersNear(near.Map, Bank, TradeRanges.WalkOverRange);

            Assert.Contains(found, entry => entry.Hawker == near);
            Assert.DoesNotContain(found, entry => entry.Hawker == far);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void WantFor_AMinerWithIngotsHearsTheSmithWaitingAtTheBank()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var miner = Character(PersonClass.Miner, Beside(NearTiles));
        var ingots = InPack(miner, Ingots(IngotLoad));
        InPack(miner, Logs(LogLoad));

        try
        {
            BankStock.PostWant(smith, SmithRules.Trade, Core.Now + WantFor);

            var want = BankStock.WantFor(miner, BankStockRules.BankReach);

            Assert.NotNull(want);
            Assert.Same(smith, want.Value.Crafter);
            Assert.Same(SmithRules.Trade, want.Value.Trade);
            Assert.Same(ingots, want.Value.Stock);
            Assert.Null(BankStock.WantFor(miner, ShortReach));

            BankStock.DropWant(smith);

            Assert.Null(BankStock.WantFor(miner, BankStockRules.BankReach));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void WantFor_LeadsToTheBankSpot_AndDropsAWantWhoseCrafterLeftTheBank()
    {
        var fletcher = Character(PersonClass.Bowyer, Bank, SkillKinds.Fletch);
        var lumberjack = Character(PersonClass.Lumberjack, Beside(FarTiles));
        InPack(lumberjack, Logs(LogLoad));

        try
        {
            BankStock.PostWant(fletcher, FletchRules.Trade, Core.Now + WantFor);

            var want = BankStock.WantFor(lumberjack, BankStockRules.BankReach);

            Assert.NotNull(want);
            Assert.Equal(Bank, want.Value.Bank);

            // The Britain fletcher went out to cut wood while a lumberjack walked 150 logs to it.
            fletcher.MoveToWorld(Beside(LeftTiles), fletcher.Map);

            Assert.Null(BankStock.WantFor(lumberjack, BankStockRules.BankReach));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void WaiterFor_NamesTheCrafterWaitingBesideTheSeller_NotOneWaitingElsewhere()
    {
        var lumberjack = Character(PersonClass.Lumberjack, Bank);
        var far = Character(PersonClass.Carpenter, Beside(FarTiles), SkillKinds.Carpentry);
        var near = Character(PersonClass.Bowyer, Beside(NearTiles), SkillKinds.Fletch);
        var logs = InPack(lumberjack, Logs(LogLoad));

        try
        {
            BankStock.PostWant(far, CarpentryRules.Trade, Core.Now + WantFor);

            Assert.Null(BankStock.WaiterFor(lumberjack));

            BankStock.PostWant(near, FletchRules.Trade, Core.Now + WantFor);
            var waiter = BankStock.WaiterFor(lumberjack);

            Assert.NotNull(waiter);
            Assert.Same(near, waiter.Value.Crafter);
            Assert.Same(FletchRules.Trade, waiter.Value.Trade);
            Assert.Same(logs, waiter.Value.Stock);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void BankStockSell_HoldsTheLotUpForWhoWaitsNow_ThenTheRestForTheNext()
    {
        var lumberjack = Character(PersonClass.Lumberjack, Bank);
        var fletcher = Character(PersonClass.Bowyer, Beside(NearTiles), SkillKinds.Fletch);
        var carpenter = Character(PersonClass.Carpenter, Beside(NearTiles + NextTile), SkillKinds.Carpentry);
        var logs = InPack(lumberjack, Logs(LogStack));

        try
        {
            var nobody = new BankStockSellSkill(Bank);

            Assert.False(nobody.Begin(lumberjack));
            Assert.Equal(BankStockSellSkill.NoWaiterWhy, nobody.FailReason);

            BankStock.PostWant(fletcher, FletchRules.Trade, Core.Now + WantFor);
            var sale = new BankStockSellSkill(Bank);

            Assert.True(sale.Begin(lumberjack));
            Assert.True(BankCrowd.TryGetHawkerOffer(lumberjack, out var first));
            Assert.Equal(logs.Serial, first.Item);

            // The fletcher cuts its lot and takes it; the carpenter waits for the rest.
            Assert.True(BankStock.CutLot(logs, CutUnits));
            fletcher.Backpack.AddItem(logs);
            BankStock.DropWant(fletcher);
            BankStock.PostWant(carpenter, CarpentryRules.Trade, Core.Now + WantFor);

            Assert.Equal(SkillStatus.Running, sale.Tick());
            Assert.True(BankCrowd.TryGetHawkerOffer(lumberjack, out var rest));
            Assert.NotEqual(logs.Serial, rest.Item);
            Assert.Equal(LogStack - CutUnits, lumberjack.Backpack.GetAmount(typeof(Log)));

            var restStack = World.FindItem(rest.Item);
            _made.Add(restStack);
            carpenter.Backpack.AddItem(restStack);
            BankStock.DropWant(carpenter);

            Assert.Equal(SkillStatus.Done, sale.Tick());
            Assert.False(BankCrowd.TryGetHawkerOffer(lumberjack, out _));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void OfferFor_ACarpenterAndABowyerTakeTheLumberjacksLogs_TheEngineBurnsLogsAndBoards()
    {
        // Sellers held up logs while carpenters and bowyers asked for boards: the engine's
        // craft lists take logs, and its type table lets boards stand in for them.
        var carpenter = Character(PersonClass.Carpenter, Bank, SkillKinds.Carpentry);
        var bowyer = Character(PersonClass.Bowyer, Beside(NextTile), SkillKinds.Fletch);
        var lumberjack = Character(PersonClass.Lumberjack, Beside(NearTiles));
        var logs = InPack(lumberjack, Logs(LogLoad));
        InPack(carpenter, Registered(new Board((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = CutUnits }));
        InPack(carpenter, Logs(CutUnits));

        try
        {
            BankCrowd.SetHawkerOffer(lumberjack, new HawkerOffer(logs.Serial, IngotAsking, Appraisal.NounOf(logs)));

            Assert.Same(logs, BankStock.OfferFor(carpenter, CarpentryRules.Trade)?.Stock);
            Assert.Same(logs, BankStock.OfferFor(bowyer, FletchRules.Trade)?.Stock);
            Assert.Equal(CutUnits + CutUnits, CraftStations.StockCarried(carpenter, CarpentryRules.Trade));
            Assert.Equal(Appraisal.RowByKey(WoodKey), BankStock.ClaimOf(FletchRules.Trade)?.Row);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void BankStockBuy_ACrafterWithNoCoinForAUnitDoesNotWaitAtTheBank()
    {
        // Every bank buy of the first live run was cut short by the buyer's purse, and a
        // carpenter stood out its whole wait seven tiles from a lumberjack holding logs up.
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var trip = new BankStockBuySkill(SmithRules.Trade);

        try
        {
            Assert.False(trip.Begin(smith));
            Assert.Equal(BankStockBuySkill.NoCoinWhy, trip.FailReason);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void PurseAtBank_CountsThePackWithNoBankerInEarshot()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        InPack(smith, Registered(new Gold((Serial)_nextItem++) { Amount = PackGold }));

        try
        {
            Assert.Equal(PackGold, TradeHandOff.PurseAtBank(smith));
            Assert.Equal(PackGold, TradeHandOff.Purse(smith));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void KeptToHawk_TheCraftersBestPiecesStayOffTheBankFloor_TheRestIsTossed()
    {
        // Crafters tossed 212 of their own pieces on the bank floor in one evening, just before
        // their bank trade, and no fighter ever bought gear from one.
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var fighter = Character(PersonClass.Warrior, Beside(NearTiles));
        var chest = InPack(smith, Piece(new PlateChest((Serial)_nextItem++) { Quality = ArmorQuality.Exceptional }));
        var legs = InPack(smith, Piece(new PlateLegs((Serial)_nextItem++)));
        var gorget = InPack(smith, Piece(new StuddedGorget((Serial)_nextItem++)));
        InPack(fighter, Piece(new PlateChest((Serial)_nextItem++)));

        try
        {
            var hawked = HawkerGoods.KeptToHawk(smith, CraftMarket.TradeOf(smith));

            Assert.Same(chest, HawkerGoods.BestKept(smith));
            Assert.Null(HawkerGoods.BestKept(fighter));
            Assert.False(BankDepositSkill.IsTossable(smith, chest, hawked));
            Assert.False(BankDepositSkill.IsTossable(smith, legs, hawked));
            Assert.True(BankDepositSkill.IsTossable(smith, gorget, hawked));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void Unmet_ACartographerWithNoMapWorkAndNoMapmakerInReachDrawsNothing()
    {
        // Mapmakers out of blank maps failed 142 times in one evening: the job never checked
        // for a map to work or the makings of one.
        var cartographer = Character(PersonClass.Scribe, Bank, SkillKinds.Cartography);

        try
        {
            Assert.Contains(SkillKinds.Cartography, SkillReadiness.Unmet(cartographer));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void WantFor_NoLoadTheSmithBurns_NoTrip()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var lumberjack = Character(PersonClass.Lumberjack, Beside(NearTiles));
        InPack(lumberjack, Logs(LogLoad));

        try
        {
            BankStock.PostWant(smith, SmithRules.Trade, Core.Now + WantFor);

            Assert.Null(BankStock.WantFor(lumberjack, BankStockRules.BankReach));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void StockIn_NeverTheStockTheSellersOwnTradeBurns()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var miner = Character(PersonClass.Miner, Beside(NearTiles));
        InPack(smith, Ingots(IngotLoad));
        var ingots = InPack(miner, Ingots(IngotLoad));

        try
        {
            Assert.Null(BankStock.StockIn(smith, SmithRules.Trade));
            Assert.Same(ingots, BankStock.StockIn(miner, SmithRules.Trade));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void SpareUnits_ASmithWhoMinesSparesOnlyWhatItCarriesPastItsKeep()
    {
        var miningSmith = Character(PersonClass.Smith, Bank, SkillKinds.Smith, SkillKinds.Mine);
        var buyingSmith = Character(PersonClass.Smith, Beside(NearTiles), SkillKinds.Smith);
        var miner = Character(PersonClass.Miner, Beside(NearTiles + 1));
        var own = InPack(miningSmith, Ingots(CraftMarketRules.OwnStockKeep + CutUnits));
        var bought = InPack(buyingSmith, Ingots(CraftMarketRules.OwnStockKeep + CutUnits));
        var dug = InPack(miner, Ingots(IngotLoad));

        try
        {
            Assert.Equal(CutUnits, CraftMarket.SpareUnits(miningSmith, own));
            Assert.Equal(0, CraftMarket.SpareUnits(buyingSmith, bought));
            Assert.Equal(IngotLoad, CraftMarket.SpareUnits(miner, dug));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void SpareUnits_ACookKeepsTheMeatForItsOwnFire_AHunterSellsIt()
    {
        // Cooks live by no career, so each sold the ribs on its fire to the next cook every two
        // seconds for three hours: 14135 of the 14201 station sales, and every try burnt nothing.
        var cook = Character(PersonClass.Warrior, Bank, SkillKinds.Cook);
        var hunter = Character(PersonClass.Warrior, Beside(NearTiles));
        var cooking = InPack(cook, Ribs(LogLoad));
        var hunted = InPack(hunter, Ribs(LogLoad));

        try
        {
            Assert.Same(CookRules.Trade, CraftMarket.WorkedTradeBurning(cook, typeof(RawRibs)));
            Assert.Equal(0, CraftMarket.SpareUnits(cook, cooking));
            Assert.Null(CraftMarket.WorkedTradeBurning(hunter, typeof(RawRibs)));
            Assert.Equal(LogLoad, CraftMarket.SpareUnits(hunter, hunted));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void StockIn_ASmithWhoMinesOffersItsSurplusToOtherSmiths_NeverItsKeep()
    {
        var miningSmith = Character(PersonClass.Smith, Bank, SkillKinds.Smith, SkillKinds.Mine);
        var keep = InPack(miningSmith, Ingots(CraftMarketRules.OwnStockKeep));

        try
        {
            Assert.Null(BankStock.StockIn(miningSmith, SmithRules.Trade));

            var surplus = InPack(miningSmith, Ingots(CutUnits));

            Assert.Contains(BankStock.StockIn(miningSmith, SmithRules.Trade), new Item[] { keep, surplus });
            Assert.Equal(CutUnits, CraftMarket.SpareUnits(miningSmith, BankStock.StockIn(miningSmith, SmithRules.Trade)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void WantFor_ASmithWhoMinesHearsAnotherSmithWaitingForIngots()
    {
        var waiting = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var miningSmith = Character(PersonClass.Smith, Beside(NearTiles), SkillKinds.Smith, SkillKinds.Mine);
        var ingots = InPack(miningSmith, Ingots(CraftMarketRules.OwnStockKeep + CutUnits));

        try
        {
            BankStock.PostWant(waiting, SmithRules.Trade, Core.Now + WantFor);

            var want = BankStock.WantFor(miningSmith, BankStockRules.BankReach);

            Assert.NotNull(want);
            Assert.Same(waiting, want.Value.Crafter);
            Assert.Same(ingots, want.Value.Stock);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void SpareUnits_AGathererWhoseRoutineCraftsNowAndThenSparesItsWholeHarvest()
    {
        // The roster's lumberjacks fletch and its miners smith a dagger: each kept its first
        // hundred units from the crafters, and 825 of 841 lumber loads of a night spared none.
        var lumberjack = Character(PersonClass.Lumberjack, Bank, SkillKinds.Lumberjack, SkillKinds.Fletch);
        var miner = Character(PersonClass.Miner, Beside(NearTiles), SkillKinds.Mine, SkillKinds.Smith);
        var logs = InPack(lumberjack, Logs(LogLoad));
        var ingots = InPack(miner, Ingots(IngotLoad));

        try
        {
            Assert.True(CraftMarket.LivesByHarvest(lumberjack, FletchRules.Trade));
            Assert.Equal(LogLoad, CraftMarket.SpareUnits(lumberjack, logs));
            Assert.Equal(IngotLoad, CraftMarket.SpareUnits(miner, ingots));
            Assert.Same(logs, BankStock.StockIn(lumberjack, FletchRules.Trade));
            Assert.Same(ingots, BankStock.StockIn(miner, SmithRules.Trade));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void BankStockTrade_ALumberjackAndABowyerWaitingAtTheSameBankCompleteTheSale()
    {
        var lumberjack = Character(PersonClass.Lumberjack, Bank, PersonTrait.Generous, SkillKinds.Lumberjack, SkillKinds.Fletch);
        var bowyer = Character(PersonClass.Bowyer, Beside(NextTile), PersonTrait.Generous, SkillKinds.Fletch);
        var logs = InPack(lumberjack, Logs(LogStack));
        InPack(bowyer, Registered(new Gold((Serial)_nextItem++) { Amount = RichPurse }));

        try
        {
            BankStock.PostWant(bowyer, FletchRules.Trade, Core.Now + WantFor);
            var sale = new BankStockSellSkill(Bank);

            Assert.True(sale.Begin(lumberjack));

            var visit = BankStock.StartBuy(bowyer, FletchRules.Trade);

            Assert.NotNull(visit);
            Assert.Same(lumberjack, visit.Seller);
            Assert.Equal(SkillStatus.Done, Settle(visit));
            Assert.True(logs.IsChildOf(bowyer.Backpack));
            Assert.Equal(LogStack, bowyer.Backpack.GetAmount(typeof(Log)));
            Assert.Equal(visit.Price, lumberjack.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(RichPurse - visit.Price, bowyer.Backpack.GetAmount(typeof(Gold)));

            // The bowyer's want comes down with the buy; nothing is left to hold up.
            BankStock.DropWant(bowyer);

            Assert.Equal(SkillStatus.Done, sale.Tick());
            Assert.False(BankCrowd.TryGetHawkerOffer(lumberjack, out _));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void CrafterVisit_AFighterBuysThePieceASmithHoldsUpAtTheBank()
    {
        var smith = Character(PersonClass.Smith, Bank, PersonTrait.Generous, SkillKinds.Smith);
        var fighter = Character(PersonClass.Warrior, Beside(NextTile), PersonTrait.Generous);
        var chest = InPack(smith, Piece(new PlateChest((Serial)_nextItem++) { Quality = ArmorQuality.Exceptional }));
        InPack(fighter, Registered(new Gold((Serial)_nextItem++) { Amount = GearPurse }));
        var wanted = new GearOffer(
            PlateChestType, null, IngotAsking, AnyGearScore, ShopFinder.SmithToken, null, GearBuyKind.Armor, GearSlot.Chest, true
        );

        try
        {
            BankCrowd.SetHawkerOffer(smith, new HawkerOffer(chest.Serial, Appraisal.Value(chest, Appraisal.MidRoll), Appraisal.NounOf(chest)));

            var visit = UpgradeGearSkill.CrafterVisit(fighter, wanted);

            Assert.NotNull(visit);
            Assert.Same(smith, visit.Seller);
            Assert.Equal(SkillStatus.Done, Settle(visit));
            Assert.True(chest.IsChildOf(fighter.Backpack));
            Assert.Equal(visit.Price, smith.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(GearPurse - visit.Price, fighter.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void Unmet_ASmithDigsOnlyOnceItRunsLowOnIngots_AndCannotBuyAny()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith, SkillKinds.Mine);
        InPack(smith, Registered(new Pickaxe((Serial)_nextItem++) { Movable = true }));

        try
        {
            Assert.DoesNotContain(SkillKinds.Mine, SkillReadiness.Unmet(smith));

            InPack(smith, Ingots(CraftMarketRules.OwnStockLow));

            Assert.Contains(SkillKinds.Mine, SkillReadiness.Unmet(smith));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void OfferedInReach_ASmithCountsAGathererHoldingIngotsUpAtTheBank()
    {
        var smith = Character(PersonClass.Smith, Beside(NearTiles), SkillKinds.Smith);
        var miner = Character(PersonClass.Miner, Bank);
        var ingots = InPack(miner, Ingots(IngotLoad));

        try
        {
            Assert.False(BankStock.OfferedNear(smith, SmithRules.Trade, Bank, RichPurse));

            BankCrowd.SetHawkerOffer(miner, new HawkerOffer(ingots.Serial, IngotAsking, Appraisal.NounOf(ingots)));

            Assert.True(BankStock.OfferedNear(smith, SmithRules.Trade, Bank, RichPurse));
            Assert.False(BankStock.OfferedNear(smith, SmithRules.Trade, Bank, PoorPurse));
            Assert.False(BankStock.OfferedNear(smith, CarpentryRules.Trade, Bank, RichPurse));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void OfferFor_TheSmithFindsTheMinerHoldingIngotsUp()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var miner = Character(PersonClass.Miner, Beside(NearTiles));
        var lumberjack = Character(PersonClass.Lumberjack, Beside(NearTiles + 1));
        var ingots = InPack(miner, Ingots(IngotLoad));
        var logs = InPack(lumberjack, Logs(LogLoad));

        try
        {
            BankCrowd.SetHawkerOffer(lumberjack, new HawkerOffer(logs.Serial, IngotAsking, Appraisal.NounOf(logs)));

            Assert.Null(BankStock.OfferFor(smith, SmithRules.Trade));

            BankCrowd.SetHawkerOffer(miner, new HawkerOffer(ingots.Serial, IngotAsking, Appraisal.NounOf(ingots)));
            var offer = BankStock.OfferFor(smith, SmithRules.Trade);

            Assert.NotNull(offer);
            Assert.Same(miner, offer.Value.Seller);
            Assert.Same(ingots, offer.Value.Stock);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void CutLot_TheLotKeepsTheStackAndTheRestStaysInThePack()
    {
        var miner = Character(PersonClass.Miner, Bank);
        var ingots = InPack(miner, Ingots(IngotLoad));

        try
        {
            Assert.False(BankStock.CutLot(ingots, 0));
            Assert.True(BankStock.CutLot(ingots, CutUnits));
            Assert.Equal(CutUnits, ingots.Amount);
            Assert.Equal(IngotLoad, miner.Backpack.GetAmount(typeof(IronIngot)));
            Assert.True(BankStock.CutLot(ingots, IngotLoad));
            Assert.Equal(CutUnits, ingots.Amount);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void ClaimOf_TheTradesOwnWordsNameItsStockRow()
    {
        Assert.Equal(Appraisal.RowByKey(IngotsKey), BankStock.ClaimOf(SmithRules.Trade)?.Row);
        Assert.Equal(Appraisal.RowByKey(WoodKey), BankStock.ClaimOf(CarpentryRules.Trade)?.Row);
        Assert.Null(BankStock.ClaimOf(CookRules.Trade));
        Assert.Null(BankStock.ClaimOf(InscriptionRules.Trade));
    }

    [Fact]
    public void CrafterPieceFor_TheFighterFindsThePieceInTheSmithsPack()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var fighter = Character(PersonClass.Warrior, Beside(NearTiles));
        var held = InPack(smith, Piece(new Katana((Serial)_nextItem++)));
        var chest = InPack(smith, Piece(new PlateChest((Serial)_nextItem++)));

        try
        {
            BankCrowd.SetHawkerOffer(smith, new HawkerOffer(held.Serial, IngotAsking, Appraisal.NounOf(held)));

            var pick = TradeMarket.CrafterPieceFor(fighter, IsA(PlateChestType), BankStockRules.BankReach, RichPurse);

            Assert.NotNull(pick);
            Assert.Same(smith, pick.Value.Crafter);
            Assert.Same(chest, pick.Value.Piece);
            Assert.Null(TradeMarket.CrafterPieceFor(fighter, IsA(PlateLegsType), BankStockRules.BankReach, RichPurse));
            Assert.Null(TradeMarket.CrafterPieceFor(fighter, IsA(PlateChestType), BankStockRules.BankReach, PoorPurse));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void CrafterPieceFor_OnlyACrafterDigsInItsPack()
    {
        var fighter = Character(PersonClass.Warrior, Bank);
        var buyer = Character(PersonClass.Warrior, Beside(NearTiles));
        var held = InPack(fighter, Piece(new Katana((Serial)_nextItem++)));
        InPack(fighter, Piece(new PlateChest((Serial)_nextItem++)));

        try
        {
            BankCrowd.SetHawkerOffer(fighter, new HawkerOffer(held.Serial, IngotAsking, Appraisal.NounOf(held)));

            Assert.Null(TradeMarket.CrafterPieceFor(buyer, IsA(PlateChestType), BankStockRules.BankReach, RichPurse));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void KeptToHawk_ACrafterHoldsBackItsBestPieces_OthersNone()
    {
        var smith = Character(PersonClass.Smith, Bank, SkillKinds.Smith);
        var fighter = Character(PersonClass.Warrior, Beside(NearTiles));
        var dagger = InPack(smith, Piece(new Dagger((Serial)_nextItem++)));
        var chest = InPack(smith, Piece(new PlateChest((Serial)_nextItem++)));
        var katana = InPack(smith, Piece(new Katana((Serial)_nextItem++) { Quality = WeaponQuality.Exceptional }));
        InPack(fighter, Piece(new PlateChest((Serial)_nextItem++)));

        try
        {
            var kept = HawkerGoods.KeptToHawk(smith, CraftMarket.TradeOf(smith));

            Assert.Equal(CraftTradeRules.HawkerStock, kept.Count);
            Assert.Contains(katana, kept);
            Assert.Contains(chest, kept);
            Assert.DoesNotContain(dagger, kept);
            Assert.Empty(HawkerGoods.KeptToHawk(fighter, CraftMarket.TradeOf(fighter)));
        }
        finally
        {
            Clean();
        }
    }

    private static Func<Item, bool> IsA(string typeName) => item => item.GetType().Name == typeName;

    // The haggle runs a line a beat until the goods and gold changed hands or the deal fell through.
    private static SkillStatus Settle(DealVisit visit)
    {
        var now = Core.Now;
        var status = SkillStatus.Running;

        for (var beat = 0; beat < MaxBeats && status == SkillStatus.Running; beat++)
        {
            status = visit.Tick(now);
            now += TradeDeal.Beat;
        }

        return status;
    }

    private static Point3D Beside(int tiles) => new(Bank.X + tiles, Bank.Y, Bank.Z);

    private SosariaCharacter Character(PersonClass personClass, Point3D at, params string[] routineSkills) =>
        Character(personClass, at, PersonTrait.None, routineSkills);

    private SosariaCharacter Character(PersonClass personClass, Point3D at, PersonTrait traits, params string[] routineSkills)
    {
        var serial = _nextMobile++;
        var character = new SosariaCharacter((Serial)serial) { Name = $"{personClass}{serial}" };
        character.DefaultMobileInit();
        character.BindProfile(
            new PersonProfile(
                personClass,
                PersonProfile.Default.Tier,
                traits,
                PersonWealth.Modest,
                ActivityTendencies.Even,
                PersonProfile.NeutralPhaseLength,
                female: false
            )
        );

        if (routineSkills.Length > 0)
        {
            character.Definition = new CharacterDefinition
            {
                Routine = [.. Array.ConvertAll(routineSkills, skill => new SkillStepDefinition { Skill = skill })]
            };
        }

        var pack = new Backpack((Serial)(serial + BackpackSerialOffset))
        {
            Layer = Layer.Backpack, Movable = true, MaxItems = Container.GlobalMaxItems
        };
        pack.Parent = character;
        character.Items.Add(pack);
        character.MoveToWorld(at, TestMap.EnsureLand());
        _made.Add(character);
        return character;
    }

    private IronIngot Ingots(int amount) =>
        Registered(new IronIngot((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = amount });

    private RawRibs Ribs(int amount) =>
        Registered(new RawRibs((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = amount });

    private Log Logs(int amount) =>
        Registered(new Log((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = amount });

    private T Piece<T>(T piece) where T : Item
    {
        piece.Movable = true;
        piece.Amount = 1;
        return Registered(piece);
    }

    // An offer names its goods by serial, and the market looks the item up in the world.
    private T Registered<T>(T item) where T : Item
    {
        World.AddEntity(item);
        _made.Add(item);
        return item;
    }

    private static T InPack<T>(Mobile owner, T item) where T : Item
    {
        owner.Backpack.AddItem(item);
        return item;
    }

    private void Clean()
    {
        foreach (var entity in _made)
        {
            if (entity is SosariaCharacter character)
            {
                BankCrowd.ClearHawkerOffer(character);
                BankStock.DropWant(character);
            }
        }

        foreach (var entity in _made)
        {
            entity.Delete();
        }

        _made.Clear();
    }

    // A container places a dropped item inside the art bounds, which the test run never loads.
    private static void EnsureItemBounds()
    {
        if (ItemBounds.Bounds == null)
        {
            typeof(ItemBounds).GetProperty(nameof(ItemBounds.Bounds))!.SetValue(null, new Rectangle2D[ArtTileCount]);
        }
    }
}
