using System;
using System.Collections;
using System.Collections.Generic;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A seller's trip to a counter that really buys its goods: a live vendor whose own buy list
/// takes something in the pack, nearest first, this town before others. Iron ore is smelted
/// at a forge first, placed or part of the map, since no shop buys raw ore. Raw stock goes to
/// people before the counter: a crafter waiting dry at the bank for it (<see cref="BankStock"/>)
/// first, once a trip, then a crafter working at the shop (<see cref="CraftMarket"/>); only the
/// rest goes over the counter. The smiths at the forge took every ingot of the first live run
/// while smiths waited at five banks. A smith who mined smelts its ore the same
/// way, keeps the ingots its own forge burns and offers only the surplus to other smiths. A
/// counter that takes nothing more sends the rest to the next buyer, and the trip ends when none
/// is left.
/// </summary>
public sealed class VendorSellSkill : Skill
{
    private enum Phase
    {
        WalkForge,
        Smelt,
        WalkVendor,
        Sell,
        Bank
    }

    private const int MaxSmeltAttempts = 8;

    /// <summary>How many buyers to try, nearest first, before giving up.</summary>
    public const int MaxShopTries = 3;

    /// <summary>How many markers to check for one inside the town when a stall opens.</summary>
    private const int StallSpotCandidates = 8;

    private const string NoBuyerWhy = "no shop in reach buys the goods";
    private const string RefusedWhy = "no shop in reach would take the rest";
    private const string NoGoodsLeftWhy = "no goods left to sell";
    private const string OreStuckWhy = "the ore would not smelt";
    private const string NoForgeWalkWhy = "no walk to the forge";
    private const string NoOreTripWhy = "no forge or shop in reach for the ore";

    private static readonly ILogger logger = SosariaLog.For(typeof(VendorSellSkill));

    private readonly string _forcedDestination;
    private readonly HashSet<Serial> _triedVendors = [];
    private int _shopTries;
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Phase _phase;
    private int _smeltAttempts;
    private BaseVendor _vendor;
    private bool _missingVendor;
    private int _vendorApproaches;
    private int _soldToCrafters;
    private BankStockSellSkill _bankSale;
    private bool _bankTried;
    private int _soldAtBank;
    private int _smelted;
    private readonly HashSet<Point3D> _forgesWalkedTo = [];

    public VendorSellSkill(string destination = null) =>
        _forcedDestination = string.IsNullOrWhiteSpace(destination) ? null : destination;

    public override string Name => SkillKinds.VendorSell;

    public int ItemsSold { get; private set; }

    public int GoldTaken { get; private set; }

    public string VendorName { get; private set; }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        ItemsSold = 0;
        GoldTaken = 0;
        VendorName = null;
        _smeltAttempts = 0;
        _shopTries = 0;
        _triedVendors.Clear();
        _walk = null;
        _vendor = null;
        _missingVendor = false;
        _vendorApproaches = 0;
        _soldToCrafters = 0;
        _bankSale = null;
        _bankTried = false;
        _soldAtBank = 0;
        _smelted = 0;
        _forgesWalkedTo.Clear();

        if (character?.Backpack == null || !People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        PackAnimals.Unload(character);

        // The good piece goes on before the rest goes on the counter: a seller does not
        // pawn the katana it would rather swing.
        GearEquip.DressForBuild(character);
        var goods = Goods();

        if (!goods.Any)
        {
            return CannotStart("no goods to sell");
        }

        // A crafter who waits at a bank in reach for the load hears of it before the shop walk.
        if (BeginBankSale())
        {
            return true;
        }

        if (!TownStayRules.MaySell(
                character.Build?.Role ?? CharacterRole.Worker,
                TownAt(character.Map, character.Location) != null))
        {
            return BeginWalkToVendor() || CannotStart(NoBuyerWhy);
        }

        if (goods.Ore)
        {
            return BeginWalkToForge() || CannotStart(NoOreTripWhy);
        }

        return BeginWalkToVendor() || CannotStart(NoBuyerWhy);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        if (_phase == Phase.Bank)
        {
            return TickBank();
        }

        if (_phase is Phase.WalkForge or Phase.WalkVendor)
        {
            var walk = _walk?.Tick() ?? SkillStatus.Failed;

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            if (walk == SkillStatus.Failed)
            {
                // A buyer the walk could not reach is not the last buyer in reach.
                return _phase == Phase.WalkVendor && BeginWalkToVendor()
                    ? SkillStatus.Running
                    : Finish(_phase == Phase.WalkForge ? "the walk to the forge failed" : "the walk to the shop failed");
            }

            if (_phase == Phase.WalkForge)
            {
                var forge = FindForge();

                if (forge == Point3D.Zero)
                {
                    return BeginWalkToVendor() ? SkillStatus.Running : Finish(NoBuyerWhy);
                }

                if (!TryApproachForge(forge))
                {
                    return Finish(NoForgeWalkWhy);
                }

                if (_phase == Phase.WalkForge)
                {
                    return SkillStatus.Running;
                }
            }
            else
            {
                _phase = Phase.Sell;
            }
        }

        return _phase == Phase.Smelt ? TickSmelt() : TickSell();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _bankSale?.Abort();
        _bankSale = null;
        _vendor = null;
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _bankSale?.Resume(held);
    }

    /// <summary>
    /// Where a missing harvest stall opens, when the town has no shop for the harvest: the
    /// town's own marker for the shop kind, or the seller's spot when the town has none.
    /// Resolving the token across the whole facet parked the stall in a town hundreds of
    /// tiles away.
    /// </summary>
    internal static Point3D StallSpot(
        IReadOnlyList<Destination> candidates,
        Func<Destination, bool> inTown,
        Point3D fallback
    )
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (inTown(candidates[i]))
            {
                return candidates[i].Arrival;
            }
        }

        return fallback;
    }

    private SkillStatus TickSell()
    {
        _missingVendor = false;
        var soldBefore = ItemsSold;
        PackAnimals.Unload(_character);

        // Raw ore alone has nothing for the counter yet: the forge comes first.
        if (Goods() is { Ore: true, Counter: false } && _smeltAttempts < MaxSmeltAttempts)
        {
            return TickSmelt();
        }

        // Stock a crafter waits for at the bank goes there before anyone else gets it, a mining
        // smith's surplus ingots too, though no counter ever gets those.
        if (BeginBankSale())
        {
            return SkillStatus.Running;
        }

        // A crafter working at the shop buys its stock off the load before the vendor does.
        var craftedBefore = _soldToCrafters;
        _soldToCrafters += CraftMarket.SellToCrafters(_character);
        var before = Goods();

        if (!before.Any)
        {
            return Finish(NoGoodsLeftWhy);
        }

        SellHere();

        if (_missingVendor)
        {
            return ApproachVendorAgain() || BeginWalkToVendor() ? SkillStatus.Running : Finish(NoBuyerWhy);
        }

        // The pack beast's load comes into the pack a pack at a time and sells in turn.
        PackAnimals.Unload(_character);
        var goods = Goods();

        if (goods.Ore && _smeltAttempts < MaxSmeltAttempts)
        {
            return TickSmelt();
        }

        var sold = ItemsSold > soldBefore || _soldToCrafters > craftedBefore;

        return VendorSellRules.AfterCounter(sold, goods.Counter) switch
        {
            CounterNext.KeepSelling => SkillStatus.Running,
            CounterNext.NextShop => BeginWalkToVendor() ? SkillStatus.Running : Finish(RefusedWhy),
            _ => Finish(goods.Ore ? OreStuckWhy : NoGoodsLeftWhy)
        };
    }

    /// <summary>
    /// A trip that sold anything, or smelted ore a smith keeps for its own forge, is done; one that
    /// did neither fails for <paramref name="why"/>.
    /// </summary>
    private SkillStatus Finish(string why) =>
        ItemsSold > 0 || _soldToCrafters > 0 || _soldAtBank > 0 || _smelted > 0 ? SkillStatus.Done : Fail(why);

    /// <summary>
    /// Walks the load to the bank where a crafter in reach waits for it, once a trip: the board
    /// is read as the trip starts and again at the shop. At the bank the seller holds up only
    /// what it spares for whoever waits there then (<see cref="BankStockSellSkill"/>). False when
    /// no crafter waits for anything carried, or the walk cannot start.
    /// </summary>
    private bool BeginBankSale()
    {
        if (_bankTried || BankStock.WantFor(_character, BankStockRules.BankReach) is not { } want)
        {
            return false;
        }

        _bankTried = true;
        var sale = new BankStockSellSkill(want.Bank);

        if (!sale.Begin(_character))
        {
            return false;
        }

        _bankSale = sale;
        _walk = null;
        _phase = Phase.Bank;
        Talk.Maybe(
            _character,
            TalkCategory.StockToBank,
            TalkOdds.CraftDonePercent,
            new TalkSlots { Name = want.Crafter.Name, Item = Appraisal.RowOf(want.Stock).Noun }
        );
        return true;
    }

    // Back from the bank: what the crafter did not take goes to the shop, the one passed on
    // the way there included.
    private SkillStatus TickBank()
    {
        var status = _bankSale.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _bankSale = null;

        if (status == SkillStatus.Done)
        {
            _soldAtBank++;
        }

        if (!Goods().Any)
        {
            return Finish(NoGoodsLeftWhy);
        }

        _triedVendors.Clear();
        _shopTries = 0;
        return BeginWalkToVendor() ? SkillStatus.Running : Finish(NoBuyerWhy);
    }

    private string Destination()
    {
        if (!string.IsNullOrWhiteSpace(_forcedDestination))
        {
            return _forcedDestination;
        }

        var goods = Goods();
        return VendorSellRules.DestinationFor(goods.Ore, goods.Ingots, goods.Logs, goods.Fish);
    }

    private PackGoods Goods()
    {
        var goods = new PackGoods();
        var forSale = SaleGoods.ForSale(_character);

        for (var i = 0; i < forSale.Count; i++)
        {
            var item = forSale[i];

            if (!HarvestPack.IsSellable(item))
            {
                goods.Loot = true;
                continue;
            }

            goods.Ore |= item is BaseOre;
            goods.Ingots |= item is BaseIngot;
            goods.Logs |= item is Log;
            goods.Fish |= item is Fish;
        }

        return goods;
    }

    private bool HasOre() => Goods().Ore;

    private void TrySmelt(Point3D forge)
    {
        var pack = _character.Backpack;

        if (pack == null || forge == Point3D.Zero)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} found no forge to smelt at {Location}", _character.Name, _character.Location);
            }

            return;
        }

        var mining = _character.Skills[SkillName.Mining]?.Value ?? 0;
        var moving = new List<BaseOre>();

        foreach (var item in pack.Items)
        {
            if (item is BaseOre ore && !ore.Deleted)
            {
                moving.Add(ore);
            }
        }

        for (var i = 0; i < moving.Count; i++)
        {
            if (moving[i] == null || moving[i].Deleted)
            {
                continue;
            }

            SmeltOne(moving[i], forge, mining);
        }
    }

    private void SmeltOne(BaseOre ore, Point3D forge, double mining)
    {
        if (!_character.InRange(forge, SmeltRules.ForgeRange) ||
            !_character.InRange(ore.GetWorldLocation(), SmeltRules.ForgeRange))
        {
            return;
        }

        if (!SmeltRules.KnowsHow(mining, SmeltRules.IronDifficulty))
        {
            return;
        }

        var take = SmeltRules.ConsumeAmount(ore.ItemID, ore.Amount);

        if (take <= 0)
        {
            return;
        }

        var min = SmeltRules.MinSkill(SmeltRules.IronDifficulty);
        var max = SmeltRules.MaxSkill(SmeltRules.IronDifficulty);

        if (!_character.CheckSkill(SkillName.Mining, min, max))
        {
            ore.Amount = Math.Max(1, SmeltRules.AfterFailure(ore.Amount));
            return;
        }

        var ingots = SmeltRules.IngotsFrom(ore.ItemID, take);

        if (ingots <= 0)
        {
            return;
        }

        var bar = ore.GetIngot();
        bar.Amount = ingots;
        ore.Consume(take);
        _character.AddToBackpack(bar);
        _smelted += ingots;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} smelted {Count} ingots at a forge",
                _character.Name,
                ingots
            );
        }
    }

    private Point3D FindForge() => SmeltRules.FindNearestForge(_character.Map, _character.Location);

    /// <summary>
    /// Smelts the ore at the forge in reach, a few tries, then sells the ingots at this
    /// smithy: a seller who walked to the next buyer after smelting passed the smith it had
    /// come to, since that smith was already on its tried list.
    /// </summary>
    private SkillStatus TickSmelt()
    {
        var forge = FindForge();

        if (forge == Point3D.Zero)
        {
            return BeginWalkToVendor() ? SkillStatus.Running : Finish(NoBuyerWhy);
        }

        if (!TryApproachForge(forge))
        {
            return Finish(NoForgeWalkWhy);
        }

        if (_phase == Phase.WalkForge)
        {
            return SkillStatus.Running;
        }

        TrySmelt(forge);
        _smeltAttempts++;

        if (HasOre() && _smeltAttempts < MaxSmeltAttempts)
        {
            return SkillStatus.Running;
        }

        _phase = Phase.Sell;
        return SkillStatus.Running;
    }

    private bool BeginWalkToForge()
    {
        var forge = FindForge();

        if (forge != Point3D.Zero)
        {
            return TryApproachForge(forge);
        }

        _phase = Phase.WalkForge;
        var forges = SmeltRules.ForgeSpots(
            _character.Location,
            SmeltRules.ForgesNear(_character.Map, _character.Location, VendorSellRules.TownVendorScanRange),
            MaxShopTries
        );

        for (var i = 0; i < forges.Count; i++)
        {
            _walk = new TravelSkill(forges[i], SmeltRules.ForgeRange);

            if (_walk.Begin(_character))
            {
                _forgesWalkedTo.Add(forges[i]);
                return true;
            }
        }

        // A smith who buys the ingots works by a forge: the forge search on arrival finds it.
        return BeginWalkToVendor();
    }

    /// <summary>
    /// Stands in smelting reach of the forge, or starts the one walk to it this trip. A walk
    /// that ended and left the seller out of reach of the forge it went to cannot close on it
    /// (a counter or a wall between them), and false ends the smelting: walking to it again
    /// ended at once every tick, and three Minoc sellers stood at the smithy for ten minutes.
    /// </summary>
    private bool TryApproachForge(Point3D forge)
    {
        if (_character.InRange(forge, SmeltRules.ForgeRange))
        {
            _phase = Phase.Smelt;
            _walk = null;
            return true;
        }

        if (!VendorSellRules.MayWalkToForge(forge, _forgesWalkedTo))
        {
            return false;
        }

        _forgesWalkedTo.Add(forge);
        _phase = Phase.WalkForge;
        _walk = new TravelSkill(forge, SmeltRules.ForgeRange);
        return _walk.Begin(_character);
    }

    /// <summary>
    /// Walks to the nearest buyer not tried this trip. Nearest first, but only one the route
    /// planner can reach: the nearest smith in Britain stands behind a wall, and a walk to it
    /// failed every trip.
    /// </summary>
    private bool BeginWalkToVendor()
    {
        var stall = StallRange();
        var vendors = BuyersInReach();

        for (var i = 0; i < vendors.Count && _shopTries < MaxShopTries; i++)
        {
            var vendor = vendors[i];

            // A vendor already approached once this trip gave nothing: the walk never
            // closed on the stall, or the counter refused the goods. Do not pick it again.
            if (!_triedVendors.Add(vendor.Serial))
            {
                continue;
            }

            _shopTries++;
            _vendor = vendor;
            _vendorApproaches = 0;

            if (WalkToStall(vendor, stall))
            {
                return true;
            }
        }

        _vendor = null;
        _walk = null;
        return false;
    }

    /// <summary>
    /// The vendor walked to stepped off its stall while the seller came: it is walked to
    /// again where it stands now, once, before the next buyer.
    /// </summary>
    private bool ApproachVendorAgain() =>
        _vendor is { Deleted: false } vendor &&
        ShopBuyers.Serves(vendor, _character) &&
        VendorSellRules.MayApproachAgain(_vendorApproaches) &&
        WalkToStall(vendor, StallRange());

    /// <summary>
    /// Starts the walk into the stall of <paramref name="vendor"/>, or the sale at once when
    /// the seller already stands in it. The walk ends well inside the stall
    /// (<see cref="VendorSellRules.ShopWalkRange"/>), on the vendor's floor.
    /// </summary>
    private bool WalkToStall(BaseVendor vendor, int stall)
    {
        _vendorApproaches++;

        // InRange ignores height: a vendor on the floor below reads as a stall
        // the seller is standing at, then the 3D counter scan finds nothing.
        if (_character.InRange(vendor, stall) &&
            NavMetric.SameFloor(_character.Location, vendor.Location))
        {
            _phase = Phase.Sell;
            _walk = null;
            return true;
        }

        _phase = Phase.WalkVendor;
        _walk = new TravelSkill(vendor.Location, VendorSellRules.ShopWalkRange(stall), arrivalFloor: true);
        return _walk.Begin(_character);
    }

    /// <summary>
    /// Buyers of the goods: this town's first, where a missing harvest stall opens, then the
    /// rest of the leash by the census of live vendors. A shop kind's marker is never the
    /// goal: Magincia's smith marker had nobody standing at it.
    /// </summary>
    private List<BaseVendor> BuyersInReach()
    {
        var local = BuyingVendors(VendorSellRules.TownVendorScanRange);
        var others = ShopBuyers.For(_character.Map).NearestBuying(
            SaleGoods.TypesOf(SaleGoods.ForSale(_character)),
            _character.Location,
            HomeLeash.ConfiguredRadius(),
            vendor => ShopBuyers.Serves(vendor, _character) && !local.Contains(vendor)
        );

        local.AddRange(others);
        return local;
    }

    private int StallRange()
    {
        var range = SosariaSettings.Characters?.Career?.VendorSearchRange ?? 0;
        return range > 0 ? range : CareerSettings.DefaultVendorSearchRange;
    }

    private void SellHere()
    {
        var stall = StallRange();
        var vendor = _vendor is { Deleted: false } && InStall(_vendor, stall) ? _vendor : FindVendor(stall);

        if (vendor == null)
        {
            _missingVendor = true;

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} has no vendor within {Range} tiles at {Location}", _character.Name, stall, _character.Location);
            }

            return;
        }

        _vendor = vendor;
        _triedVendors.Add(vendor.Serial);

        if (!TradeRules.MayBeCounterpart(vendor.Player))
        {
            return;
        }

        var list = SellList(vendor, SaleGoods.ForSale(_character));

        if (list.Count == 0)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} found no goods {Vendor} will buy at {Location}", _character.Name, vendor.Name, _character.Location);
            }

            return;
        }

        var gold = VendorDeal.Sell(_character, vendor, list);

        if (gold <= 0)
        {
            return;
        }

        GoldTaken += gold;
        ItemsSold += list.Count;
        VendorName = vendor.Name;

        if (ItemsSold > 0)
        {
            _character.NoteMusingEvent(MusingRules.Sold(VendorName ?? "a vendor", GoldTaken));
            VendorBuySkill.BuyMissingWorkerTools(_character, VendorDeal.VendorsNear(_character, VendorDeal.CounterRange));
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} sold {Count} stacks to {Vendor} for {Gold} gold",
                _character.Name,
                ItemsSold,
                VendorName ?? "a vendor",
                GoldTaken
            );
        }
    }

    // Chebyshev, the same measure InRange uses to end the walk. The diagonal measure
    // called 8 tiles across and 8 down "11 away", and the seller walked, arrived, and
    // walked again all day.
    private bool InStall(BaseVendor vendor, int stall) =>
        VendorSellRules.MaySellAt(NavMetric.Chebyshev(_character.Location, vendor.Location), stall);

    private static List<Item> SellList(BaseVendor vendor, IReadOnlyList<Item> goods)
    {
        var list = new List<Item>();
        var info = vendor.GetSellInfo();

        if (info == null)
        {
            return list;
        }

        for (var i = 0; i < goods.Count && list.Count < VendorSellRules.MaxStacks; i++)
        {
            var item = goods[i];

            // No shop buys raw ore: the forge turns it into ingots first.
            if (item is BaseOre || !SaleGoods.CounterTakes(item) || !VendorDeal.IsSellable(info, item))
            {
                continue;
            }

            list.Add(item);
        }

        return list;
    }

    private BaseVendor FindVendor(int range)
    {
        var vendors = BuyingVendors(range);

        for (var i = 0; i < vendors.Count; i++)
        {
            if (InStall(vendors[i], range))
            {
                return vendors[i];
            }
        }

        return null;
    }

    /// <summary>Vendors in this town that buy something in the pack, nearest first.</summary>
    private List<BaseVendor> BuyingVendors(int range)
    {
        var found = new List<(BaseVendor Vendor, int Distance)>();

        if (range <= 0 || !People.InWorld(_character))
        {
            return [];
        }

        // The town where the trip began is not the town the seller is standing in. A
        // seller who started in Vesper and walked to a Minoc shop had every Minoc
        // vendor filtered out, so resolve the town at the current spot every scan.
        var town = TownAt(_character.Map, _character.Location);

        if (town == null)
        {
            return [];
        }

        _character.Map.ActivateSectors(
            _character.X >> Map.SectorShift,
            _character.Y >> Map.SectorShift
        );
        EnsureHarvestShops(town);
        var goods = SaleGoods.ForSale(_character);

        foreach (var mobile in _character.GetMobilesInRange(range))
        {
            if (mobile is not BaseVendor vendor || vendor.Deleted || vendor.Player)
            {
                continue;
            }

            var distance = (int)_character.GetDistanceToSqrt(vendor);

            if (distance > range ||
                !VendorSellRules.VendorInSameTown(true, TownContains(town, vendor.Location)) ||
                !ShopBuyers.Serves(vendor, _character) ||
                SellList(vendor, goods).Count == 0)
            {
                continue;
            }

            found.Add((vendor, distance));
        }

        found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return found.ConvertAll(entry => entry.Vendor);
    }

    /// <summary>Which goods are left in the pack for the counter.</summary>
    private struct PackGoods
    {
        public bool Ore;
        public bool Ingots;
        public bool Logs;
        public bool Fish;
        public bool Loot;

        /// <summary>Goods a counter takes as they are: everything but raw ore.</summary>
        public readonly bool Counter => Ingots || Logs || Fish || Loot;

        public readonly bool Any => Counter || Ore;
    }

    private static TownRegion TownAt(Map map, Point3D location)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        var fromRegion = RootTown(Region.Find(location, map)?.GetRegion<TownRegion>());

        if (fromRegion != null)
        {
            return fromRegion;
        }

        foreach (var region in map.Regions.Values)
        {
            if (region is TownRegion town && TownContains(town, location))
            {
                return RootTown(town);
            }
        }

        return null;
    }

    private static bool TownContains(TownRegion town, Point3D location)
    {
        if (town == null)
        {
            return false;
        }

        if (town.Contains(location))
        {
            return true;
        }

        var tile = new Point2D(location.X, location.Y);
        var area = town.Area;

        for (var i = 0; i < area.Length; i++)
        {
            if (area[i].Contains(tile))
            {
                return true;
            }
        }

        return false;
    }

    private static TownRegion RootTown(TownRegion town)
    {
        while (town?.Parent is TownRegion parent)
        {
            town = parent;
        }

        return town;
    }

    /// <summary>
    /// A town with no shop for the harvest wakes its harvest shop spawners, then opens a
    /// stall when that brought nobody. Loot has no stall: it goes to a buyer that exists.
    /// </summary>
    private void EnsureHarvestShops(TownRegion town)
    {
        var token = Destination();

        if (string.IsNullOrWhiteSpace(token) || HasHarvestShop(town, token))
        {
            return;
        }

        var spawners = new List<BaseSpawner>();

        foreach (var item in _character.GetItemsInRange(VendorSellRules.TownVendorScanRange))
        {
            if (item is not BaseSpawner spawner ||
                spawner.Deleted ||
                !TownContains(town, spawner.Location) ||
                !SpawnerSellsHarvest(spawner))
            {
                continue;
            }

            spawners.Add(spawner);
        }

        for (var i = 0; i < spawners.Count; i++)
        {
            spawners[i].Respawn();
        }

        if (!HasHarvestShop(town, token))
        {
            PlaceHarvestShop(town, token);
        }
    }

    private void PlaceHarvestShop(TownRegion town, string token)
    {
        var spot = StallSpot(
            NavWorld.DestinationsFor(_character.HomeFacet)
                ?.NearestFirst(token, _character.Location, StallSpotCandidates) ?? [],
            dest => TownContains(town, dest.Arrival),
            _character.Location
        );

        if (ShopStandsAt(spot, token))
        {
            return;
        }

        BaseVendor shop = null;

        if (token.Equals(ShopFinder.CarpenterToken, StringComparison.OrdinalIgnoreCase))
        {
            shop = new Carpenter();
        }
        else if (token.Equals(VendorSellRules.FishermanToken, StringComparison.OrdinalIgnoreCase))
        {
            shop = new Fisherman();
        }
        else if (token.Equals(ShopFinder.SmithToken, StringComparison.OrdinalIgnoreCase))
        {
            shop = new Blacksmith();
        }

        if (shop == null)
        {
            return;
        }

        shop.MoveToWorld(spot, _character.Map);
        ShopBuyers.Note(shop);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} opened a {Shop} stall at {Location}",
                _character.Name,
                shop.GetType().Name,
                spot
            );
        }
    }

    /// <summary>
    /// A shop already placed at the spot. The town check looks round the seller, and the
    /// spot can be further off than that: fourteen smiths ended up on one Minoc tile.
    /// </summary>
    private bool ShopStandsAt(Point3D spot, string token)
    {
        foreach (var mobile in _character.Map.GetMobilesInRange(spot, StallRange()))
        {
            if (mobile is BaseVendor { Deleted: false } vendor &&
                VendorSellRules.MatchesNeededShop(vendor.GetType().Name, token))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasHarvestShop(TownRegion town, string token)
    {
        foreach (var mobile in _character.GetMobilesInRange(VendorSellRules.TownVendorScanRange))
        {
            // A guildmaster's name matches the shop family, but its counter is for
            // guild joins, not goods. Counting it left towns looking served while
            // nothing there bought the harvest.
            if (mobile is BaseVendor { Deleted: false } vendor &&
                vendor is not BaseGuildmaster &&
                TownContains(town, vendor.Location) &&
                VendorSellRules.MatchesNeededShop(vendor.GetType().Name, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SpawnerSellsHarvest(BaseSpawner spawner)
    {
        // ModernUO changed this property's concrete return type while keeping its
        // contents the same. Read it by name so a saved plugin cannot crash the
        // shard when the server and plugin were built at different times.
        var entries = spawner?.GetType().GetProperty("Entries")?.GetValue(spawner) as IEnumerable;

        if (entries == null)
        {
            return false;
        }

        foreach (var entry in entries)
        {
            var spawnedName = entry?.GetType().GetProperty("SpawnedName")?.GetValue(entry) as string;

            if (VendorSellRules.IsHarvestShopName(spawnedName))
            {
                return true;
            }
        }

        return false;
    }
}
