using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(FloorTradeTests.CollectionName, DisableParallelization = true)]
public class FloorTradeCollection;

/// <summary>
/// Trade between characters that the operator can see: a deal closes in the engine's secure
/// trade window, a bystander idling on the bank floor answers a WTB, and a player vendor sells at
/// the owner's appraised price and pays its owner. The people stand on the shared test land, so
/// the class runs alone.
/// </summary>
[Collection(CollectionName)]
public class FloorTradeTests
{
    public const string CollectionName = "Floor trade";

    // Ranges no other test class uses: tests that share serials collide in the one engine world.
    private const uint FirstMobileSerial = 0xC601;
    private const uint BackpackSerialOffset = 0x100;
    private const uint FirstItemSerial = 0x4000C601;
    private const int ArtTileCount = 0x4000;
    private const int Purse = 5000;
    private const int Price = 400;
    private const int NextTile = 1;
    private const int MaxBeats = 40;
    private const int HeldGold = 1000;
    private const int ArrowLoad = 200;
    private const int BandageSurplus = 30;
    private const double HealingSkill = 50;

    private static readonly Point3D Bank = new(40, 40, 0);
    private static readonly TimeSpan RestFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PastFreshStock = PlayerVendorRules.FreshStockWait + TimeSpan.FromSeconds(1);

    private readonly List<IEntity> _made = [];
    private uint _nextMobile = FirstMobileSerial;
    private uint _nextItem = FirstItemSerial;

    static FloorTradeTests() => Timer.Init(0);

    public FloorTradeTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
        EnsureItemBounds();
        AssemblyHandler.Assemblies ??= [typeof(Item).Assembly, typeof(Katana).Assembly];

        // A player vendor dresses from the races, as the server sets them up at boot.
        if (Race.Human == null)
        {
            Server.Misc.RaceDefinitions.Configure();
        }

        // A browser's wants read its skills: a recaller without a runebook wants one.
        TestSkills.EnsureTable();

        // What a bowyer makes comes off the engine's fletching list, which the server builds at boot.
        if (DefBowFletching.CraftSystem == null)
        {
            DefBowFletching.Initialize();
        }
    }

    [Fact]
    public void Swap_TheTradeWindowMovesTheGoodsOneWayAndTheCoinTheOther()
    {
        var smith = Character(PersonClass.Smith, Bank);
        var fighter = Character(PersonClass.Warrior, Beside(NextTile));
        var chest = InPack(smith, Chest());
        InPack(fighter, Coins(Purse));

        try
        {
            Assert.True(CharacterTrade.Swap(smith, chest, fighter, Price));
            Assert.True(chest.IsChildOf(fighter.Backpack));
            Assert.Equal(Price, smith.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(Purse - Price, fighter.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void Swap_ABuyerShortOfCoinBuysNothing_AndNothingMoves()
    {
        var smith = Character(PersonClass.Smith, Bank);
        var fighter = Character(PersonClass.Warrior, Beside(NextTile));
        var chest = InPack(smith, Chest());
        InPack(fighter, Coins(Price - 1));

        try
        {
            Assert.False(CharacterTrade.Swap(smith, chest, fighter, Price));
            Assert.True(chest.IsChildOf(smith.Backpack));
            Assert.Equal(0, smith.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(Price - 1, fighter.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void MayAnswer_OnlyAPersonIdlingOnTheFloorAnswers()
    {
        var idle = Character(PersonClass.Warrior, Bank);
        var walking = Character(PersonClass.Warrior, Beside(NextTile));

        try
        {
            TestRoutine.Give(idle, new Routine([new RestSkill(RestFor)]));
            TestRoutine.Give(walking, new Routine([new TravelSkill(Beside(NextTile + NextTile), NextTile)]));

            Assert.True(TradeMarket.MayAnswer(idle));
            Assert.False(TradeMarket.MayAnswer(walking));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void AnswerWant_ASmithIdlingOnTheFloorBringsThePieceTheFighterShoutedFor()
    {
        var fighter = Character(PersonClass.Warrior, Bank);
        var smith = Character(PersonClass.Smith, Beside(NextTile));
        var chest = InPack(smith, Chest());
        InPack(fighter, Coins(Purse));
        var want = new GoodsClaim(Appraisal.RowOf(chest), 1, true, Appraisal.NoMagic);

        try
        {
            TestRoutine.Give(smith, new Routine([new RestSkill(RestFor)]));

            var deal = FloorDeal.AnswerWant(fighter, want);

            Assert.NotNull(deal);
            Settle(deal);
            Assert.True(deal.Price > 0);
            Assert.True(chest.IsChildOf(fighter.Backpack));
            Assert.Equal(deal.Price, smith.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(Purse - deal.Price, fighter.Backpack.GetAmount(typeof(Gold)));
            Assert.False(smith.Conversation.IsActive(Core.Now));
            Assert.False(fighter.Conversation.IsActive(Core.Now));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void AnswerWant_NobodyIdlingWithTheGoods_NoDeal()
    {
        var fighter = Character(PersonClass.Warrior, Bank);
        var smith = Character(PersonClass.Smith, Beside(NextTile));
        var chest = InPack(smith, Chest());
        InPack(fighter, Coins(Purse));
        var want = new GoodsClaim(Appraisal.RowOf(chest), 1, true, Appraisal.NoMagic);

        try
        {
            TestRoutine.Give(smith, new Routine([new TravelSkill(Beside(NextTile + NextTile), NextTile)]));

            Assert.Null(FloorDeal.AnswerWant(fighter, want));
            Assert.True(chest.IsChildOf(smith.Backpack));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void PlayerVendor_SellsAtTheOwnersPrice_AndTheOwnerCollectsIntoItsBank()
    {
        var owner = Character(PersonClass.Smith, Bank);
        var fighter = Character(PersonClass.Warrior, Beside(NextTile));
        var chest = Chest();
        InPack(fighter, Coins(Purse));
        var vendor = Registered(new PlayerVendor(owner, null));
        vendor.MoveToWorld(Beside(NextTile + NextTile), TestMap.EnsureLand());

        try
        {
            Assert.True(PlayerVendorRules.Stock(vendor, chest));

            var price = vendor.GetVendorItem(chest).Price;
            var later = Core.Now + PastFreshStock;

            Assert.Equal(PlayerVendorRules.PriceFor(chest), price);
            Assert.False(PlayerVendorRules.Buy(fighter, vendor, chest, Core.Now));

            var piece = PlayerVendorMall.PieceFor(fighter, PlayerVendorMall.ShopReach, later);

            Assert.NotNull(piece);
            Assert.Same(chest, piece.Value.Piece);

            var held = vendor.HoldGold;

            Assert.True(PlayerVendorRules.Buy(fighter, vendor, chest, later));
            Assert.True(chest.IsChildOf(fighter.Backpack));
            Assert.Equal(Purse - price, fighter.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(held + price, vendor.HoldGold);

            vendor.HoldGold = HeldGold;
            Assert.NotNull(owner.BankBox);
            var takings = PlayerVendorRules.Earnings(HeldGold, vendor.ChargePerDay);

            Assert.Equal(takings, PlayerVendorRules.Collect(owner, vendor));
            Assert.Equal(takings, Banker.GetBalance(owner));
            Assert.Equal(HeldGold - takings, vendor.HoldGold);
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void IsForSale_ABowyerHawksTheArrowsItFletched_AnArcherKeepsItsOwn()
    {
        var bowyer = Character(PersonClass.Bowyer, Bank, SkillKinds.Fletch);
        var archer = Character(PersonClass.Archer, Beside(NextTile));
        var made = InPack(bowyer, Arrows());
        var carried = InPack(archer, Arrows());

        try
        {
            Assert.True(HawkerGoods.IsForSale(bowyer, made));
            Assert.Contains(made, HawkerGoods.KeptToHawk(bowyer, CraftMarket.TradeOf(bowyer)));
            Assert.False(HawkerGoods.IsForSale(archer, carried));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void AnswerSupply_AHealerSellsOnlyItsBandagesPastItsTarget_AndOnlyTheBuyersShortfall()
    {
        var fighter = Character(PersonClass.Warrior, Bank);
        var healer = Character(PersonClass.Healer, Beside(NextTile));
        InPack(fighter, Coins(Purse));
        InPack(healer, Bandages(SupplyRules.BandageTarget + BandageSurplus));
        fighter.Skills.Healing.Base = HealingSkill;
        healer.Skills.Healing.Base = HealingSkill;

        try
        {
            TestRoutine.Give(healer, new Routine([new RestSkill(RestFor)]));

            var deal = FloorDeal.AnswerSupply(fighter);

            Assert.NotNull(deal);
            Settle(deal);
            Assert.True(deal.Price > 0);
            Assert.Equal(BandageSurplus, fighter.Backpack.GetAmount(typeof(Bandage)));
            Assert.Equal(SupplyRules.BandageTarget, healer.Backpack.GetAmount(typeof(Bandage)));
            Assert.Equal(deal.Price, healer.Backpack.GetAmount(typeof(Gold)));

            // At its target the healer spares nothing more.
            Assert.Null(FloorDeal.AnswerSupply(fighter));
            Assert.Equal(0, SupplyMarket.SpareUnits(healer, typeof(Bandage)));
        }
        finally
        {
            Clean();
        }
    }

    // The haggle runs a line a beat until the goods and gold changed hands or the deal fell through.
    private static void Settle(FloorDeal deal)
    {
        var now = Core.Now;

        for (var beat = 0; beat < MaxBeats && deal.Tick(now); beat++)
        {
            now += TradeDeal.Beat;
        }
    }

    [Fact]
    public void Hawker_APersonWhoSaysIllTakeItBuysAtTheAskingPrice()
    {
        var hawker = Character(PersonClass.Smith, Bank);
        var chest = InPack(hawker, Chest());
        BankCrowd.SetHawkerOffer(hawker, new HawkerOffer(chest.Serial, Price, Appraisal.NounOf(chest)));
        var person = Person(Beside(NextTile));
        InPack(person, Coins(Purse));
        TradeSession session = null;

        try
        {
            TradeTalk.Hear(person, "ill take it");
            session = TradeSessions.FindFor(person);

            Assert.NotNull(session);
            Assert.Same(hawker, session.Character);
            Assert.Equal(Price, session.Standing);
            Assert.Equal(TradePhase.HandOff, session.Phase);
        }
        finally
        {
            if (session != null)
            {
                session.End(null);
                TradeSessions.Close(session);
            }

            Clean();
        }
    }

    private static Point3D Beside(int tiles) => new(Bank.X + tiles, Bank.Y, Bank.Z);

    private SosariaCharacter Character(PersonClass personClass, Point3D at, params string[] routineSkills)
    {
        var serial = _nextMobile++;
        var character = new SosariaCharacter((Serial)serial) { Name = $"{personClass}{serial}" };
        character.DefaultMobileInit();
        character.BindProfile(
            new PersonProfile(
                personClass,
                PersonProfile.Default.Tier,
                PersonTrait.Generous,
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

    // A person at a keyboard; refusing trades keeps the window away, so the deal waits in the hand-off.
    private PlayerMobile Person(Point3D at)
    {
        var serial = _nextMobile++;
        var person = new PlayerMobile((Serial)serial) { RefuseTrades = true };
        person.DefaultMobileInit();
        var pack = new Backpack((Serial)(serial + BackpackSerialOffset)) { Layer = Layer.Backpack, Movable = true };
        pack.Parent = person;
        person.Items.Add(pack);
        person.MoveToWorld(at, TestMap.EnsureLand());
        _made.Add(person);
        return person;
    }

    private PlateChest Chest()
    {
        var chest = new PlateChest((Serial)_nextItem++) { Quality = ArmorQuality.Exceptional, Movable = true, Amount = 1 };
        return Registered(chest);
    }

    private Arrow Arrows() =>
        Registered(new Arrow((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = ArrowLoad });

    private Bandage Bandages(int amount) =>
        Registered(new Bandage((Serial)_nextItem++) { Movable = true, Stackable = true, Amount = amount });

    private Gold Coins(int amount) => Registered(new Gold((Serial)_nextItem++) { Amount = amount });

    // A deal and a vendor listing find their goods by serial, and the market looks the item up in the world.
    private T Registered<T>(T entity) where T : IEntity
    {
        if (entity is Item item)
        {
            World.AddEntity(item);
        }

        _made.Add(entity);
        return entity;
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
                TradeMarket.DropWant(character);
            }
        }

        TestMap.Remove(_made);
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

    [Fact]
    public void Deposit_FoldedWindow_SavesNoOrder()
    {
        var smith = Character(PersonClass.Smith, Bank);
        var buyer = Character(PersonClass.Warrior, Beside(NextTile));
        InPack(buyer, Coins(Purse));
        var placed = false;

        try
        {
            var session = TradeSession.Fixed(smith, buyer, null, Price, "GM katana", TradeLineKind.SellerAccept, paid => placed = paid);
            TradeSessions.Open(session);

            session.End(TradeLineKind.Released);

            Assert.False(placed);
            Assert.Equal(0, smith.Backpack.GetAmount(typeof(Gold)));
            Assert.Equal(Purse, buyer.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void Deposit_DroppedCoinPaysAndPlaces()
    {
        var smith = Character(PersonClass.Smith, Bank);
        var buyer = Character(PersonClass.Warrior, Beside(NextTile));
        var coins = Coins(Price);
        var placed = false;

        try
        {
            var session = TradeSession.Fixed(smith, buyer, null, Price, "GM katana", TradeLineKind.SellerAccept, paid => placed = paid);
            TradeSessions.Open(session);

            Assert.True(session.Receive(coins));
            Assert.True(placed);
            Assert.Equal(Price, smith.Backpack.GetAmount(typeof(Gold)));
        }
        finally
        {
            Clean();
        }
    }
}
