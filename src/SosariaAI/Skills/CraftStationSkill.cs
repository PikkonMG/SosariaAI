using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Engines.Craft.T2A;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A crafter's long session at the station of its trade: between an anvil and a forge for a
/// smith, beside the heat for a cook, across the counter from the shop's vendor for every
/// other trade. It walks there, and when it lacks a tool or stock it buys them first: off a
/// gatherer standing near, from a vendor at the counter, or on a trip to the nearest shop that
/// really sells them, then back to the station. It keeps a spare tool, bought at the counter
/// or, for a tinker, made at the bench. Several crafters share a station, each on a free tile
/// in the engine's reach. Out of stock with no shop left to try, it holds its station a while,
/// asks aloud for stock and buys from any gatherer who comes by; still dry, it tries the bank,
/// where it shouts WTB and buys a gatherer's load (<see cref="BankStockBuySkill"/>), then walks
/// back to work. No seller at the bank sends it to one more shop, past the trip cap: the NPC
/// vendor is the fallback. Stock only a shop sells (<see cref="CraftTrade.PeopleBringStock"/>)
/// is not waited for: nobody comes by the mapmaker with blank maps, so it goes straight to that
/// one more shop. It makes goods someone buys, weighted by the gold a try earns
/// (<see cref="CraftBuyers"/>), answers the T2A maker's-mark question a grandmaster is asked for
/// an exceptional piece, and rests after batches that make nothing. It crafts through the engine's
/// craft system, so the engine plays the craft sound, burns the stock and wears the tool, and
/// between two tries it pauses as a player at the menu did (<see cref="CraftTradeRules.TryPause"/>). Each
/// batch ends with the goods sold to a vendor in reach, a few pieces kept back to hawk at the
/// bank, and one activity line; a grandmaster calls out an exceptional piece. Goods no vendor
/// in reach took are sold on the next supply trip or on a walk to the shop at the end of the
/// session. A crafter holds its station for most of
/// an evening, burns real stock it bought for real coin, and waits out a dry spell asking for
/// more.
/// </summary>
public abstract class CraftStationSkill : Skill
{
    private enum Phase
    {
        WalkShop,
        WalkStation,
        Prepare,
        WalkSupply,
        WalkBack,
        Craft,
        Sell,
        WalkSell,
        Dry,
        BankStock
    }

    private const string ToolShort = "a tool";
    private const string StockShort = "materials";
    private const string MadeNothingWhy = "made nothing";

    /// <summary>CraftSystem.CanCraft answers 0 when the item may be attempted.</summary>
    private const int CanCraftOk = 0;

    private static readonly ILogger logger = SosariaLog.For(typeof(CraftStationSkill));

    // Materials the engine accepts for one another (CraftItem's type table).
    private static readonly Type[][] MaterialFamilies =
    [
        [typeof(Log), typeof(Board)],
        [typeof(Leather), typeof(Hides)],
        [typeof(Cloth), typeof(UncutCloth)]
    ];

    private readonly CraftTrade _trade;
    private readonly HashSet<Type> _rejected = [];
    private readonly HashSet<Type> _unstocked = [];
    private readonly HashSet<Type> _madeTypes = [];
    private readonly HashSet<Point3D> _triedSupply = [];
    private readonly HashSet<Serial> _productBefore = [];
    private readonly HashSet<Point3D> _triedStands = [];
    private readonly Dictionary<Type, int> _emptyBatches = [];
    private SosariaCharacter _character;
    private Skill _walk;
    private BankStockBuySkill _bankStock;
    private Phase _phase;
    private Point3D _shop;
    private DateTime _started;
    private TimeSpan _length;
    private CraftItem _product;
    private BaseTool _tool;
    private int _batchLeft;
    private int _batchMade;
    private int _batchTries;
    private int _batchSpent;
    private int _countBefore;
    private int _materialsBefore;
    private int _emptyInRow;
    private int _rejectsInRow;
    private int _madeTotal;
    private int _supplyTrips;
    private int _standTries;
    private DateTime _dryStarted;
    private DateTime _dryChecked;
    private DateTime _nextTry;
    private bool _crafting;
    private bool _sellTripDone;
    private bool _dried;
    private bool _spareBatch;
    private string _shortOf;

    protected CraftStationSkill(CraftTrade trade) =>
        _trade = trade ?? throw new ArgumentNullException(nameof(trade));

    public override string Name => _trade.Kind;

    /// <summary>True for a living person in the world: it may work a trade or draw maps.</summary>
    public static bool MayWork(SosariaCharacter character) =>
        character is { Deleted: false, Alive: true } && People.InWorld(character);

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        Reset();

        if (!MayWork(character) || _trade.System == null)
        {
            return CannotStart("cannot work now");
        }

        _started = Core.Now;
        _length = CraftTradeRules.SessionLength(Utility.Random(int.MaxValue));

        if (CraftStations.ShopFor(character, _trade) is { } place)
        {
            _shop = place.Shop;
        }
        else if (_trade.Station == CraftStation.Heat && CraftStations.CanLightFire(character))
        {
            // No shop in reach can heat a pan: cook over a campfire where it stands.
            _shop = character.Location;
            CraftTally.NoteSession();
            return BeginStation() == SkillStatus.Running || CannotStart(_shortOf ?? "no heat to cook at");
        }
        else
        {
            return CannotStart($"no {_trade.Kind} shop in reach");
        }

        CraftTally.NoteSession();
        _phase = Phase.WalkShop;
        _walk = new TravelSkill(_shop, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(character) || CannotStart("no walk to the shop");
    }

    public override SkillStatus Tick()
    {
        if (!MayWork(_character))
        {
            return Fail("cannot work now");
        }

        return _phase switch
        {
            Phase.WalkShop => AfterWalk(ArriveAtShop, WalkFailed),
            Phase.WalkStation => AfterWalk(StartPrepare, StartPrepare),
            Phase.Prepare => Prepare(),
            Phase.WalkSupply => AfterWalk(AtSupply, NextSupplyOrEnd),
            Phase.WalkBack => AfterWalk(BeginStation, WalkFailed),
            Phase.Craft => CraftStep(),
            Phase.Sell => SellStep(),
            Phase.Dry => DryStep(),
            Phase.BankStock => BankStockStep(),
            _ => AfterWalk(SellAndEnd, EndSession)
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _bankStock?.Abort();
        _bankStock = null;
        _character = null;
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _dryStarted = SkillClock.Shift(_dryStarted, held);
        _dryChecked = SkillClock.Shift(_dryChecked, held);
        _nextTry = SkillClock.Shift(_nextTry, held);
        _walk?.Resume(held);
        _bankStock?.Resume(held);
    }

    private void Reset()
    {
        _rejected.Clear();
        _unstocked.Clear();
        _madeTypes.Clear();
        _triedSupply.Clear();
        _productBefore.Clear();
        _triedStands.Clear();
        _emptyBatches.Clear();
        _emptyInRow = 0;
        _walk = null;
        _bankStock = null;
        _product = null;
        _tool = null;
        _batchLeft = 0;
        _madeTotal = 0;
        _rejectsInRow = 0;
        _supplyTrips = 0;
        _standTries = 0;
        _crafting = false;
        _nextTry = DateTime.MinValue;
        _sellTripDone = false;
        _dried = false;
        _spareBatch = false;
        _shortOf = null;
        ResetBatch();
    }

    private void ResetBatch()
    {
        _batchMade = 0;
        _batchTries = 0;
        _batchSpent = 0;
    }

    private SkillStatus AfterWalk(Func<SkillStatus> onArrival, Func<SkillStatus> onFailure)
    {
        var status = _walk?.Tick() ?? SkillStatus.Failed;

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;
        return status == SkillStatus.Done ? onArrival() : onFailure();
    }

    private SkillStatus WalkFailed()
    {
        _shortOf ??= "the walk failed";
        return EndSession();
    }

    private SkillStatus ArriveAtShop()
    {
        if (AmbientTalk.WorkCategory(_trade.Kind) is { } shopTalk)
        {
            Talk.Maybe(_character, shopTalk, TalkOdds.CraftStartPercent);
        }

        return BeginStation();
    }

    private SkillStatus BeginStation()
    {
        if (_trade.Station == CraftStation.None)
        {
            return WalkToCounter();
        }

        // The station belongs to the shop, not to wherever the walk stopped.
        var spot = CraftStations.Find(_character.Map, _shop, _trade.Station) ?? LitFire();

        if (spot is not { } stand)
        {
            _shortOf = $"no {_trade.Station} at the shop";

            if (SosariaSettings.LogActivity)
            {
                logger.Information(
                    "{Name} found no {Station} for {Trade} near {Shop} standing at {Location}",
                    _character.Name,
                    _trade.Station,
                    _trade.Kind,
                    _shop,
                    _character.Location
                );
            }

            return EndSession();
        }

        return AtStation() ? StartPrepare() : WalkToWorkTile(stand);
    }

    // The nearest free tile the station is worked from, else the stand spot itself.
    private SkillStatus WalkToWorkTile(StandSpot stand)
    {
        _standTries++;

        if (CraftStations.FreeWorkTile(_character, stand, _trade.Station, _triedStands) is not { } tile)
        {
            return WalkTo(stand.Spot, stand.Range);
        }

        _triedStands.Add(tile);
        return WalkTo(tile, CraftStationRules.OnTheTile);
    }

    // A trade with no station works inside its shop, across the counter from the vendor
    // that sells its stock, not out on the street where the shop marker stands.
    private SkillStatus WalkToCounter()
    {
        var vendor = CraftStations.CounterVendor(_character.Map, _shop, _trade);

        return vendor == null || NavMetric.Chebyshev(_character.Location, vendor.Location) <= CraftStationRules.CounterStand
            ? StartPrepare()
            : WalkTo(vendor.Location, CraftStationRules.CounterStand);
    }

    private SkillStatus WalkTo(Point3D spot, int range)
    {
        _walk = new GoToSkill(spot, range);

        if (!_walk.Begin(_character))
        {
            _walk = null;
            return StartPrepare();
        }

        _phase = Phase.WalkStation;
        return SkillStatus.Running;
    }

    /// <summary>A cook with kindling lights a campfire beside it; the spot of that fire, or null.</summary>
    private StandSpot? LitFire()
    {
        if (_trade.Station != CraftStation.Heat || !CraftStations.CanLightFire(_character))
        {
            return null;
        }

        CraftStations.LightFire(_character);
        return CraftStations.Find(_character.Map, _character.Location, _trade.Station);
    }

    private SkillStatus StartPrepare()
    {
        _phase = Phase.Prepare;
        return SkillStatus.Running;
    }

    private SkillStatus Prepare()
    {
        if (!AtStation())
        {
            // Another crafter took the tile: the next free one is a step away.
            if (_standTries < CraftStationRules.MaxStandTries &&
                CraftStations.Find(_character.Map, _shop, _trade.Station) is { } stand)
            {
                return WalkToWorkTile(stand);
            }

            _shortOf = $"no free place at the {_trade.Station}";
            return FinishSession();
        }

        var system = _trade.System;
        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);
        _tool = FindTool(_character, system) ?? BuyTool(system, vendors);

        if (_tool == null)
        {
            return StartSupplyTrip(needTool: true) ? SkillStatus.Running : EndWithoutTool();
        }

        KeepSpareTool(system, vendors);
        var spare = SpareToolProduct(system);
        _spareBatch = spare != null;

        if (_spareBatch)
        {
            _product = spare;
            _batchLeft = CraftTradeRules.SpareToolBatch;
            _phase = Phase.Craft;
            return SkillStatus.Running;
        }

        _product = ChooseProduct(system, vendors, atStation: true);

        // A gatherer standing at the shop sells its load before the crafter walks off for stock.
        if (_product == null && CraftMarket.BuyFromGatherers(_character, _trade) > 0)
        {
            _product = ChooseProduct(system, vendors, atStation: true);
        }

        if (_product == null)
        {
            return StartSupplyTrip(needTool: false) ? SkillStatus.Running : BeginDry();
        }

        _batchLeft = StockBatch(_product, vendors, CraftTradeRules.BatchSize(Utility.Random(int.MaxValue)));

        if (_batchLeft <= 0)
        {
            _unstocked.Add(_product.ItemType);
            return SkillStatus.Running;
        }

        _phase = Phase.Craft;
        return SkillStatus.Running;
    }

    /// <summary>
    /// True when the station is inside the engine's reach where the crafter stands: another
    /// smith on the only tile of a narrow smithy leaves it standing one step off.
    /// </summary>
    private bool AtStation() => CraftStations.WorksHere(_character, _trade.Station);

    /// <summary>
    /// Out of stock at the station with no shop left to try: hold the station and ask aloud,
    /// once a stretch, or try one more shop for stock nobody brings. A crafter that digs or cuts
    /// its own stock tries the bank's sellers and else ends the session for its harvest
    /// (<see cref="CraftTradeRules.WaitsForSeller"/>). A stretch that already waited gives up.
    /// </summary>
    private SkillStatus BeginDry()
    {
        if (_dried)
        {
            return EndShort(StockShort);
        }

        _dried = true;

        // Nobody ever comes by with stock only a shop sells: the next shop is the one hope.
        if (!_trade.PeopleBringStock)
        {
            return StartFallbackTrip() ? SkillStatus.Running : EndShort(StockShort);
        }

        if (!CraftTradeRules.WaitsForSeller(_trade.PeopleBringStock, CraftMarket.GathersOwnStock(_character, _trade)))
        {
            return BeginBankStock() ? SkillStatus.Running : EndShort(StockShort);
        }

        _phase = Phase.Dry;
        _dryStarted = Core.Now;
        _dryChecked = _dryStarted;
        SayNeed(_trade.StockNoun, TalkOdds.CraftNeedPercent);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} is out of {Stock} at the {Trade} station and waits for a seller at {Location}",
                _character.Name,
                _trade.StockNoun,
                _trade.Kind,
                _character.Location
            );
        }

        return SkillStatus.Running;
    }

    // Now and then: a gatherer who came by sells its load, or a shelf restocked, and the work goes on.
    private SkillStatus DryStep()
    {
        var now = Core.Now;

        if (!CraftTradeRules.DryCheckDue(now - _dryChecked))
        {
            return SkillStatus.Running;
        }

        _dryChecked = now;

        if (CraftMarket.BuyFromGatherers(_character, _trade) > 0 ||
            ChooseProduct(_trade.System, VendorDeal.VendorsNear(_character, VendorDeal.CounterRange), atStation: true) != null)
        {
            _phase = Phase.Prepare;
            return SkillStatus.Running;
        }

        if (CraftTradeRules.DryWaitOver(now - _dryStarted))
        {
            return BeginBankStock() || StartFallbackTrip() ? SkillStatus.Running : EndShort(StockShort);
        }

        SayNeed(_trade.StockNoun, TalkOdds.CraftNeedRepeatPercent);
        return SkillStatus.Running;
    }

    // Nobody came by the station: the bank is the other market for stock, once a stretch.
    private bool BeginBankStock()
    {
        var trip = new BankStockBuySkill(_trade);

        if (!trip.Begin(_character))
        {
            return false;
        }

        _bankStock = trip;
        _phase = Phase.BankStock;
        Talk.Maybe(_character, TalkCategory.CraftBankStock, TalkOdds.CraftNeedPercent, new TalkSlots { Item = _trade.StockNoun });
        return true;
    }

    private SkillStatus BankStockStep()
    {
        var status = _bankStock.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _bankStock = null;

        if (status == SkillStatus.Done)
        {
            return WalkBackToStation();
        }

        return StartFallbackTrip() ? SkillStatus.Running : EndShort(StockShort);
    }

    // No seller at the bank, or no bank to try: the next shop that shelves the stock is the fallback.
    private bool StartFallbackTrip() => StartSupplyTrip(needTool: false, pastCap: true);

    private void SayNeed(string noun, int percent) =>
        Talk.Maybe(_character, TalkCategory.CraftNeed, percent, new TalkSlots { Item = noun });

    // No tool in reach: ask the room for one, as a player would, and give the stretch up.
    private SkillStatus EndWithoutTool()
    {
        SayNeed(_trade.ToolNoun, TalkOdds.CraftNeedPercent);
        return EndShort(ToolShort);
    }

    /// <summary>A spare from the counter while it sells one, so a broken tool never ends the trade.</summary>
    private void KeepSpareTool(CraftSystem system, IReadOnlyList<BaseVendor> vendors)
    {
        if (CraftTradeRules.ToolsToBuy(CountTools(_character, system)) > 0)
        {
            BuyTool(system, vendors);
        }
    }

    /// <summary>
    /// The trade's own tool to make as a spare, when its craft list holds it, the crafter has
    /// a tool to make it with but no spare, and the stock for it is carried. Else null.
    /// </summary>
    private CraftItem SpareToolProduct(CraftSystem system) =>
        CraftTradeRules.MakesSpareTool(_trade.OwnToolType != null, CountTools(_character, system)) &&
        system.CraftItems.SearchFor(_trade.OwnToolType) is { } item &&
        MayTry(system, item, atStation: true) && HasMaterialsForOne(item) &&
        WorksAtUsefulOdds(_character, system, item)
            ? item
            : null;

    /// <summary>
    /// True when the crafter's skills cover <paramref name="item"/> at even odds or better
    /// (<see cref="CraftTradeRules.MinUsefulChance"/>): below them it burns more than it makes.
    /// </summary>
    public static bool WorksAtUsefulOdds(Mobile crafter, CraftSystem system, CraftItem item)
    {
        var chance = item.GetSuccessChance(crafter, item.Resources[0].ItemType, system, false, out var allSkills);
        return allSkills && chance >= CraftTradeRules.MinUsefulChance;
    }

    /// <summary>
    /// Walks to the nearest place not yet tried that sells what the crafter lacks at a price
    /// its pack pays. False when the trips for this stretch are used up, unless
    /// <paramref name="pastCap"/> (the fallback after the bank), or nothing is left to try.
    /// </summary>
    private bool StartSupplyTrip(bool needTool, bool pastCap = false)
    {
        if (!pastCap && _supplyTrips >= CraftTradeRules.MaxSupplyTrips ||
            CraftStations.SupplyFor(_character, _trade, _shop, needTool, _triedSupply, CraftStations.PackGold(_character))
                is not { } shop)
        {
            return false;
        }

        _triedSupply.Add(shop);
        _supplyTrips++;
        _walk = new TravelSkill(shop, NavLimits.ShopArrivalRange, arrivalFloor: true);

        if (!_walk.Begin(_character))
        {
            _walk = null;
            return false;
        }

        _phase = Phase.WalkSupply;
        return true;
    }

    // No shop left sells the stock: back to the station to wait for a gatherer, once a stretch.
    private SkillStatus NextSupplyOrEnd()
    {
        if (StartSupplyTrip(_tool == null))
        {
            return SkillStatus.Running;
        }

        if (_tool == null)
        {
            return EndWithoutTool();
        }

        return _dried ? EndShort(StockShort) : WalkBackToStation();
    }

    // At the counter of a supply shop: sell what the station made, buy the tool and its spare,
    // and lay in stock for a long stretch, then walk back. Still short, it tries the next shop.
    private SkillStatus AtSupply()
    {
        var system = _trade.System;
        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);
        SellMade(vendors, out _, out _);
        _tool = FindTool(_character, system) ?? BuyTool(system, vendors);
        _unstocked.Clear();

        if (_tool != null)
        {
            KeepSpareTool(system, vendors);
        }

        if (_tool != null && ChooseProduct(system, vendors, atStation: false) is { } product)
        {
            StockBatch(product, vendors, CraftTradeRules.SupplyCrafts);
        }

        if (_tool != null && ChooseProduct(system, [], atStation: false) != null)
        {
            return WalkBackToStation();
        }

        return NextSupplyOrEnd();
    }

    private SkillStatus WalkBackToStation()
    {
        _walk = new TravelSkill(_shop, NavLimits.ShopArrivalRange, arrivalFloor: true);

        if (!_walk.Begin(_character))
        {
            _walk = null;
            _shortOf ??= "no walk back to the station";
            return FinishSession();
        }

        _phase = Phase.WalkBack;
        return SkillStatus.Running;
    }

    private SkillStatus CraftStep()
    {
        if (_crafting)
        {
            if (!_character.CanBeginAction<CraftSystem>())
            {
                return SkillStatus.Running;
            }

            _crafting = false;
            AnswerMakersMark();
            var made = NoteNewPieces();
            _batchMade += made;
            _batchTries++;
            _batchLeft--;
            _nextTry = Core.Now + CraftTradeRules.TryPause(Utility.Random(int.MaxValue));
        }

        if (_batchLeft <= 0 || !HasMaterialsForOne(_product))
        {
            _phase = Phase.Sell;
            return SkillStatus.Running;
        }

        // A person reads the result and clicks again: the next try waits out the pause, and the
        // mana, stamina or hits it costs.
        if (Core.Now < _nextTry || WaitsToRecover(_product))
        {
            return SkillStatus.Running;
        }

        var system = _trade.System;

        if (_tool is not { Deleted: false, UsesRemaining: > 0 })
        {
            _tool = FindTool(_character, system) ?? BuyTool(system, VendorDeal.VendorsNear(_character, VendorDeal.CounterRange));

            if (_tool == null)
            {
                _phase = Phase.Sell;
                return SkillStatus.Running;
            }
        }

        NoteProductBefore();
        system.CreateItem(_character, _product.ItemType, _product.Resources[0].ItemType, _tool, _product);

        // The engine starts a craft timer and holds the craft action until it ends. An
        // item it turned away never took the action.
        if (_character.CanBeginAction<CraftSystem>())
        {
            _rejected.Add(_product.ItemType);
            _rejectsInRow++;
            _phase = _batchTries > 0 ? Phase.Sell : Phase.Prepare;
            return _rejectsInRow >= CraftTradeRules.MaxRejects ? EndSession() : SkillStatus.Running;
        }

        _rejectsInRow = 0;
        _crafting = true;

        // A spare tool stays in the pack: it is not goods for the counter.
        if (!_spareBatch)
        {
            _madeTypes.Add(_product.ItemType);
        }

        return SkillStatus.Running;
    }

    private bool WaitsToRecover(CraftItem item) =>
        CraftTradeRules.WaitsToRecover(_character.Mana, item.Mana, _character.ManaMax) ||
        CraftTradeRules.WaitsToRecover(_character.Stam, item.Stam, _character.StamMax) ||
        CraftTradeRules.WaitsToRecover(_character.Hits, item.Hits, _character.HitsMax);

    /// <summary>
    /// Answers the engine's maker's-mark question the way a grandmaster does, with its mark: the
    /// T2A menus ask it for every exceptional piece of a markable item and make nothing until the
    /// answer comes, and a person with no client never gave one.
    /// </summary>
    private void AnswerMakersMark()
    {
        if (!CraftTradeRules.AnswersMakersMark(
                T2ACraftSystem.Enabled,
                _character.Skills[_trade.Skill].Base,
                _product.IsMarkable(_product.ItemType),
                CountProduct() == _countBefore,
                MaterialsCarried(_product) == _materialsBefore
            ))
        {
            return;
        }

        _product.CompleteCraft(
            CraftTradeRules.ExceptionalQuality,
            makersMark: true,
            _character,
            _trade.System,
            _product.Resources[0].ItemType,
            _tool,
            null
        );
    }

    private void NoteProductBefore()
    {
        _productBefore.Clear();
        _countBefore = CountProduct();
        _materialsBefore = MaterialsCarried(_product);

        foreach (var item in _character.Backpack?.Items ?? [])
        {
            if (item.GetType() == _product.ItemType)
            {
                _productBefore.Add(item.Serial);
            }
        }
    }

    // Counts what the last craft made (a stackable piece joins its stack, so the pack's count
    // tells) and calls out a grandmaster's exceptional new piece.
    private int NoteNewPieces()
    {
        var made = Math.Max(0, CountProduct() - _countBefore);

        foreach (var item in _character.Backpack?.Items ?? [])
        {
            if (item is { Deleted: false } && item.GetType() == _product.ItemType && !_productBefore.Contains(item.Serial) &&
                CraftTradeRules.AnnouncesMasterwork(_character.Skills[_trade.Skill].Value, Appraisal.IsExceptional(item)))
            {
                CallOutMasterwork(item);
            }
        }

        CraftTally.NoteMade(_trade.Kind, made);
        return made;
    }

    private void CallOutMasterwork(Item piece)
    {
        var noun = Appraisal.NounOf(piece);
        CraftTally.NoteMasterwork();
        Talk.Maybe(_character, TalkCategory.CraftMasterwork, CraftTradeRules.MasterworkTalkPercent, new TalkSlots { Item = noun });

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} made an exceptional {Piece} at the {Trade} station at {Location}",
                _character.Name,
                noun,
                _trade.Kind,
                _character.Location
            );
        }
    }

    private SkillStatus SellStep()
    {
        var sold = SellMade(VendorDeal.VendorsNear(_character, VendorDeal.CounterRange), out var gold, out var keptNow);
        _madeTotal += _batchMade;

        if (!_spareBatch)
        {
            TalkAboutBatch(sold, gold, keptNow);
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} made {Made} {Product} in {Tries} tries at the {Trade} station, spent {Spent} gold on materials, sold {Sold} for {Gold} gold, kept {Kept}",
                _character.Name,
                _batchMade,
                _product?.ItemType.Name ?? _trade.GoodsNoun,
                _batchTries,
                _trade.Kind,
                _batchSpent,
                sold,
                gold,
                keptNow
            );
        }

        // A batch made is a stretch at the station done: the next one may send it for stock again.
        if (_batchMade > 0)
        {
            _supplyTrips = 0;
            _triedSupply.Clear();
            _dried = false;
        }

        if (NoteEmptyBatch())
        {
            ResetBatch();
            return FinishSession();
        }

        ResetBatch();

        if (CraftTradeRules.SessionOver(Core.Now - _started, _length))
        {
            return FinishSession();
        }

        _phase = Phase.Prepare;
        return SkillStatus.Running;
    }

    /// <summary>
    /// Counts a batch that made nothing: its item is dropped for the session after
    /// <see cref="CraftTradeRules.EmptyBatchesPerItem"/> such batches, and the session ends to
    /// rest after <see cref="CraftTradeRules.MaxEmptyBatches"/> in a row. Cooks tried the same
    /// ribs every two seconds for three hours. True when the crafter rests.
    /// </summary>
    private bool NoteEmptyBatch()
    {
        var item = _product?.ItemType;

        if (_batchMade > 0 || item == null)
        {
            _emptyInRow = 0;

            if (item != null)
            {
                _emptyBatches.Remove(item);
            }

            return false;
        }

        _emptyInRow++;
        _emptyBatches[item] = _emptyBatches.GetValueOrDefault(item) + 1;

        if (CraftTradeRules.DropsItem(_emptyBatches[item]))
        {
            _rejected.Add(item);
        }

        if (!CraftTradeRules.RestsAfterEmpty(_emptyInRow))
        {
            return false;
        }

        _shortOf ??= MadeNothingWhy;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} made nothing in {Batches} batches in a row at the {Trade} station and rests at {Location}",
                _character.Name,
                _emptyInRow,
                _trade.Kind,
                _character.Location
            );
        }

        return true;
    }

    // Goods no vendor at the station took go to the shop on the way out, once.
    private SkillStatus FinishSession()
    {
        if (_sellTripDone || MadeGoods(out _).Count == 0 ||
            VendorDeal.VendorsNear(_character, VendorDeal.CounterRange).Count > 0 ||
            CraftStations.SupplyFor(_character, _trade, _shop, needTool: false, new HashSet<Point3D>(), CraftStations.AnyGold)
                is not { } shop)
        {
            return EndSession();
        }

        _sellTripDone = true;
        _walk = new TravelSkill(shop, NavLimits.ShopArrivalRange, arrivalFloor: true);

        if (!_walk.Begin(_character))
        {
            _walk = null;
            return EndSession();
        }

        _phase = Phase.WalkSell;
        return SkillStatus.Running;
    }

    private SkillStatus SellAndEnd()
    {
        var sold = SellMade(VendorDeal.VendorsNear(_character, VendorDeal.CounterRange), out var gold, out _);

        if (SosariaSettings.LogActivity && sold > 0)
        {
            logger.Information(
                "{Name} sold {Sold} {Goods} for {Gold} gold at the shop after the {Trade} session",
                _character.Name,
                sold,
                _trade.GoodsNoun,
                gold,
                _trade.Kind
            );
        }

        return EndSession();
    }

    private SkillStatus EndSession() => _madeTotal > 0 ? SkillStatus.Done : Fail(_shortOf ?? MadeNothingWhy);

    private SkillStatus EndShort(string what)
    {
        LogShort(what);
        return FinishSession();
    }

    // A finished batch now and then gets a word, and a shopper in the shop may ask about it:
    // the price is what the shop paid a piece; nothing kept back means it all sold.
    private void TalkAboutBatch(int sold, int gold, int kept)
    {
        if (_batchMade <= 0)
        {
            return;
        }

        Talk.Maybe(
            _character,
            TalkCategory.CraftDone,
            TalkOdds.CraftDonePercent,
            new TalkSlots { Item = _trade.GoodsNoun, Count = _batchMade }
        );

        if (sold > 0)
        {
            Scenes.CraftCustomer(_character, _trade.GoodsNoun, kept == 0 ? null : GoldWords.Spoken(gold / sold));
        }
    }

    private void LogShort(string what)
    {
        _shortOf = $"could not get {what}";

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} could not get {What} for {Trade} at {Location}",
                _character.Name,
                what,
                _trade.Kind,
                _character.Location
            );
        }
    }

    /// <summary>A tool of the craft system with uses left, in the pack or in hand, or null.</summary>
    public static BaseTool FindTool(Mobile crafter, CraftSystem system)
    {
        if (crafter == null || system == null)
        {
            return null;
        }

        foreach (var item in crafter.Backpack?.Items ?? [])
        {
            if (IsWorkingTool(item, system))
            {
                return (BaseTool)item;
            }
        }

        var held = crafter.FindItemOnLayer(Layer.OneHanded);
        return IsWorkingTool(held, system) ? (BaseTool)held : null;
    }

    /// <summary>Tools of the craft system with uses left, in the pack and in hand.</summary>
    public static int CountTools(Mobile crafter, CraftSystem system)
    {
        var count = 0;

        foreach (var item in crafter?.Backpack?.Items ?? [])
        {
            if (IsWorkingTool(item, system))
            {
                count++;
            }
        }

        return IsWorkingTool(crafter?.FindItemOnLayer(Layer.OneHanded), system) ? count + 1 : count;
    }

    private static bool IsWorkingTool(Item item, CraftSystem system) =>
        system != null && item is BaseTool { Deleted: false, UsesRemaining: > 0 } tool && tool.CraftSystem == system;

    private BaseTool BuyTool(CraftSystem system, IReadOnlyList<BaseVendor> vendors)
    {
        for (var v = 0; v < vendors.Count; v++)
        {
            var vendor = vendors[v];

            foreach (var info in vendor.GetBuyInfo())
            {
                if (info is not GenericBuyInfo { Type: not null } line ||
                    line.GetDisplayEntity() is not BaseTool shown ||
                    shown.CraftSystem != system)
                {
                    continue;
                }

                // Gold taken is a purchase even when the tool never reached the pack: the
                // next line or counter would only take gold for another.
                var bought = VendorDeal.Buy(_character, [vendor], [line.Type], 1, line.Price, out var spent);
                _batchSpent += spent;

                if (bought > 0 || spent > 0)
                {
                    return FindTool(_character, system);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The item to make: one someone buys, weighted by the gold a try earns
    /// (<see cref="CraftTradeRules.Pick"/>). The buyers are the vendors round the station and the
    /// people who want the goods (<see cref="CraftBuyers"/>); materials are priced at what the
    /// crafter pays under those shelves (<see cref="CraftMarketRules.MaterialUnitPrice"/>). At the
    /// station the engine's own check must pass (the anvil and forge in reach, the spell in the
    /// scribe's book); at a supply counter the pick only says what stock to buy, so that check
    /// waits for the station.
    /// </summary>
    private CraftItem ChooseProduct(CraftSystem system, IReadOnlyList<BaseVendor> vendors, bool atStation)
    {
        var candidates = new List<CraftItem>();
        var options = new List<CraftOption>();
        var gold = _character.Backpack?.GetAmount(typeof(Gold)) ?? 0;
        var buyers = VendorDeal.VendorsNear(_character.Map, _shop, CraftStations.SupplySearchRadius);
        var tables = CraftBuyers.TablesOf(buyers);

        for (var i = 0; i < system.CraftItems.Count; i++)
        {
            var item = system.CraftItems[i];

            if (!MayTry(system, item, atStation))
            {
                continue;
            }

            var chance = item.GetSuccessChance(_character, item.Resources[0].ItemType, system, false, out var allSkills);

            if (!allSkills)
            {
                continue;
            }

            var worth = CraftBuyers.WorthOf(item.ItemType, tables);
            candidates.Add(item);
            options.Add(
                new CraftOption(
                    i,
                    chance,
                    MaterialsReachable(item, vendors, gold),
                    CraftTradeRules.ExpectedProfit(chance, worth, MaterialCost(item, buyers)),
                    worth > 0
                )
            );
        }

        var pick = CraftTradeRules.Pick(options, Utility.Random(int.MaxValue));
        return pick == CraftTradeRules.NoPick ? null : candidates[pick];
    }

    private bool MayTry(CraftSystem system, CraftItem item, bool atStation) =>
        IsCraftable(item) && !_rejected.Contains(item.ItemType) && !_unstocked.Contains(item.ItemType) &&
        (!atStation || system.CanCraft(_character, _tool, item.ItemType) == CanCraftOk);

    /// <summary>
    /// True when <paramref name="item"/> is on the craft list for a crafter of this era: it takes
    /// materials, needs no recipe or later expansion, and is not one of its own materials (a
    /// board is not made from boards).
    /// </summary>
    public static bool IsCraftable(CraftItem item)
    {
        if (item?.ItemType == null || item.Resources.Count == 0 || item.Recipe != null ||
            item.RequiredExpansion != Expansion.None)
        {
            return false;
        }

        for (var r = 0; r < item.Resources.Count; r++)
        {
            if (Array.IndexOf(MaterialFamily(item.Resources[r].ItemType), item.ItemType) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    // The gold one piece's materials cost the crafter: the units it carries at what a gatherer
    // asks for the stock people bring it, the rest at the shelf in reach (CraftMarketRules.PieceMaterialCost).
    private int MaterialCost(CraftItem item, IReadOnlyList<BaseVendor> shelves)
    {
        var cost = 0;

        for (var r = 0; r < item.Resources.Count; r++)
        {
            var res = item.Resources[r];
            var family = MaterialFamily(res.ItemType);
            cost += CraftMarketRules.PieceMaterialCost(
                res.Amount,
                Carried(family),
                VendorDeal.CheapestPrice(shelves, family),
                GathererSells(family)
            );
        }

        return cost;
    }

    // Gatherers deal a crafter only its trade's raw stock (CraftMarket.DealAll), and nobody brings
    // stock only a shop sells (CraftTrade.PeopleBringStock).
    private bool GathererSells(Type[] family) => _trade.PeopleBringStock && Array.Exists(family, _trade.BurnsStock);

    private bool MaterialsReachable(CraftItem item, IReadOnlyList<BaseVendor> vendors, int gold)
    {
        var cost = 0;

        for (var r = 0; r < item.Resources.Count; r++)
        {
            var res = item.Resources[r];
            var family = MaterialFamily(res.ItemType);
            var shortfall = CraftTradeRules.MaterialsToBuy(res.Amount, 1, Carried(family));

            if (shortfall == 0)
            {
                continue;
            }

            if (VendorDeal.OnShelves(vendors, family) < shortfall)
            {
                return false;
            }

            cost += shortfall * VendorDeal.CheapestPrice(vendors, family);
        }

        return cost <= CraftTradeRules.StockBudget(gold, CountTools(_character, _trade.System));
    }

    /// <summary>Buys what a run of <paramref name="crafts"/> pieces needs and returns how many pieces the carried materials now cover.</summary>
    private int StockBatch(CraftItem item, IReadOnlyList<BaseVendor> vendors, int crafts)
    {
        var covered = crafts;

        for (var r = 0; r < item.Resources.Count; r++)
        {
            var res = item.Resources[r];
            var family = MaterialFamily(res.ItemType);
            var shortfall = CraftTradeRules.MaterialsToBuy(res.Amount, crafts, Carried(family));

            if (shortfall > 0)
            {
                var gold = _character.Backpack?.GetAmount(typeof(Gold)) ?? 0;
                var budget = CraftTradeRules.StockBudget(gold, CountTools(_character, _trade.System));
                VendorDeal.Buy(_character, vendors, family, shortfall, budget, out var spent);
                _batchSpent += spent;
            }

            covered = Math.Min(covered, CraftTradeRules.CraftsCovered(res.Amount, Carried(family)));
        }

        return covered;
    }

    private bool HasMaterialsForOne(CraftItem item) => CoversOnePiece(_character, item);

    /// <summary>True when the crafter's pack holds every material of one piece of <paramref name="item"/>.</summary>
    public static bool CoversOnePiece(Mobile crafter, CraftItem item)
    {
        var pack = crafter?.Backpack;

        if (item == null || pack == null)
        {
            return false;
        }

        for (var r = 0; r < item.Resources.Count; r++)
        {
            var res = item.Resources[r];

            if (CraftTradeRules.CraftsCovered(res.Amount, pack.GetAmount(MaterialFamily(res.ItemType))) < 1)
            {
                return false;
            }
        }

        return true;
    }

    private int Carried(Type[] family) => _character.Backpack?.GetAmount(family) ?? 0;

    private int CountProduct() => _product == null ? 0 : _character.Backpack?.GetAmount(_product.ItemType) ?? 0;

    // Units of every material the item takes that the pack carries: a try that burnt none left this unchanged.
    private int MaterialsCarried(CraftItem item)
    {
        var carried = 0;

        for (var r = 0; r < (item?.Resources.Count ?? 0); r++)
        {
            carried += Carried(MaterialFamily(item.Resources[r].ItemType));
        }

        return carried;
    }

    /// <summary>The materials the engine accepts for <paramref name="material"/>, itself included.</summary>
    public static Type[] MaterialFamily(Type material)
    {
        for (var i = 0; i < MaterialFamilies.Length; i++)
        {
            if (Array.IndexOf(MaterialFamilies[i], material) >= 0)
            {
                return MaterialFamilies[i];
            }
        }

        return [material];
    }

    /// <summary>
    /// This session's goods in the pack, less the pieces held back to hawk at the bank
    /// (<see cref="HawkerGoods.KeptToHawk"/>), counted in <paramref name="kept"/>, and the
    /// working tool and spare of a tinker that sells the tools it makes.
    /// </summary>
    private List<Item> MadeGoods(out int kept)
    {
        var goods = new List<Item>();
        var hawked = HawkerGoods.KeptToHawk(_character, CraftMarket.TradeOf(_character));
        var toolsKept = 0;
        kept = 0;

        foreach (var item in _character.Backpack?.Items ?? [])
        {
            if (item is not { Deleted: false } || !_madeTypes.Contains(item.GetType()))
            {
                continue;
            }

            if (hawked.Contains(item))
            {
                kept++;
                continue;
            }

            if (IsWorkingTool(item, _trade.System) && toolsKept < CraftTradeRules.ToolsKept)
            {
                toolsKept++;
                continue;
            }

            goods.Add(item);
        }

        return goods;
    }

    /// <summary>
    /// Sells this session's goods to the vendors across the counter, holding back a few
    /// finished pieces to hawk (<paramref name="keptNow"/>). Returns the stacks sold.
    /// </summary>
    private int SellMade(IReadOnlyList<BaseVendor> vendors, out int gold, out int keptNow)
    {
        gold = 0;
        var goods = MadeGoods(out keptNow);
        var sold = 0;

        for (var v = 0; v < vendors.Count; v++)
        {
            var vendor = vendors[v];
            var buys = goods.FindAll(item => !item.Deleted && item.IsChildOf(_character.Backpack) && VendorDeal.Buys(vendor, item));

            if (buys.Count == 0)
            {
                continue;
            }

            gold += VendorDeal.Sell(_character, vendor, buys);
            sold += buys.FindAll(item => item.Deleted || !item.IsChildOf(_character.Backpack)).Count;
        }

        CraftTally.NoteSold(sold, gold);
        return sold;
    }
}
