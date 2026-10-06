using System;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// Trading at the bank. On arrival the person looks over the hawkers first: one holding goods it
/// can use and pay for gets a visit and a haggle out loud, and the goods and gold change hands for
/// real. A crafter with pieces it held back from the shop (<see cref="HawkerGoods.KeptToHawk"/>)
/// holds the best of them up with a WTS, so a fighter shopping for gear finds the smith's plate:
/// crafters hawked the heal potions they carried instead, and no fighter bought a crafted piece.
/// Anyone else with nothing worth buying shouts WTB for what it really wants and can afford, pack
/// and bank counted, and listens for an "i have one"; with nothing wanted it holds up its best
/// goods with a WTS and answers anyone who asks the price. While a shout stands, a bystander
/// idling on the floor may answer it and come over to deal (<see cref="FloorDeal"/>); goods sold
/// end the wait. The dwell starts at the shout; a crafter holds its own make up longer
/// (<see cref="CrafterHawkDwell"/>).
/// </summary>
public sealed class BankShopSkill : Skill
{
    public const int ShopDwellSeconds = 60;
    public static readonly TimeSpan ShopDwell = TimeSpan.FromSeconds(ShopDwellSeconds);

    /// <summary>
    /// How long a crafter holds its own make up at the bank. A minute's WTS met no fighter on a
    /// gear trip: none of the 326 shop upgrades of a night came within two minutes of a crafter
    /// holding that piece up.
    /// </summary>
    public const int CrafterHawkSeconds = 180;
    public static readonly TimeSpan CrafterHawkDwell = TimeSpan.FromSeconds(CrafterHawkSeconds);

    private static readonly ILogger logger = SosariaLog.For(typeof(BankShopSkill));

    private readonly Point3D _bankSpot;
    private SosariaCharacter _character;
    private Skill _walk;
    private ShopStep _step;
    private DateTime _arrivedAt;
    private DateTime _shoutedAt;
    private TimeSpan _dwell;
    private DealVisit _visit;
    private bool _holdsStall;
    private readonly bool _suppliesOnly;
    private GoodsClaim? _want;
    private bool _wantsSupplies;
    private DateTime _lookedAt;

    public BankShopSkill(Point3D bankSpot) : this(bankSpot, suppliesOnly: false)
    {
    }

    private BankShopSkill(Point3D bankSpot, bool suppliesOnly)
    {
        _bankSpot = bankSpot == Point3D.Zero ? CharactersFile.DefaultBankSpot : bankSpot;
        _suppliesOnly = suppliesOnly;
    }

    /// <summary>
    /// A trip to the nearest bank for the supplies the shop shelves ran out of: a WTB for them and
    /// a wait for somebody on the floor who spares them (<see cref="FloorDeal.AnswerSupply"/>).
    /// No hawker browsing, no goods held up.
    /// </summary>
    public static BankShopSkill ForSupplies() => new(Point3D.Zero, suppliesOnly: true);

    private enum ShopStep
    {
        Walking,
        Pausing,
        Visiting,
        Waiting
    }

    public override string Name => SkillKinds.BankShop;

    public override bool Begin(SosariaCharacter character)
    {
        Leave();
        _character = character;
        _step = ShopStep.Walking;
        _shoutedAt = default;
        _walk = _bankSpot == CharactersFile.DefaultBankSpot
            ? new TravelSkill(BankTeller.BankToken, NavLimits.BankArrivalRange)
            : new TravelSkill(_bankSpot, NavLimits.BankArrivalRange);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            Leave();
            return SkillStatus.Failed;
        }

        var now = Core.Now;

        switch (_step)
        {
            case ShopStep.Walking:
                return TickWalk(now);
            case ShopStep.Pausing:
                return now - _arrivedAt < BankDepositSkill.BankPause ? SkillStatus.Running : ChooseErrand(now);
            case ShopStep.Visiting:
                return TickVisit(now);
            default:
                return TickWaiting(now);
        }
    }

    public override void Abort()
    {
        _walk?.Abort();
        Leave();
    }

    public override void Resume(TimeSpan held)
    {
        _shoutedAt = SkillClock.Shift(_shoutedAt, held);
        _arrivedAt = SkillClock.Shift(_arrivedAt, held);
        _walk?.Resume(held);
        _visit?.Resume(held);
    }

    private SkillStatus TickWalk(DateTime now)
    {
        var status = _walk.Tick();

        if (status != SkillStatus.Done)
        {
            return status;
        }

        _walk = null;
        _arrivedAt = now;
        _step = ShopStep.Pausing;
        return SkillStatus.Running;
    }

    // Browse the hawkers, else want something aloud, else hold goods up.
    private SkillStatus ChooseErrand(DateTime now)
    {
        var roll = Utility.Random(int.MaxValue);
        var purse = TradeHandOff.Purse(_character);
        var need = WantedSupply(_character);

        if (_suppliesOnly)
        {
            return need == null
                ? SkillStatus.Done
                : WantAloud(now, TradeDemandRules.WantFor(need, TradeAppetite.None, purse, roll), suppliesWanted: true);
        }

        if (TradeMarket.HawkerFor(_character) is { } pick &&
            DealVisit.Start(_character, pick.Hawker, pick.Goods, pick.Offer.Asking) is { } visit)
        {
            _visit = visit;
            _step = ShopStep.Visiting;
            return SkillStatus.Running;
        }

        if (HawkerGoods.BestKept(_character) is { } piece)
        {
            var asking = HoldUp(piece, roll);

            if (SosariaSettings.LogActivity)
            {
                logger.Information(
                    "{Name} holds up the {Piece} it made at the bank for {Gold} gold at {Location}",
                    _character.Name,
                    Appraisal.NounOf(piece),
                    asking,
                    _character.Location
                );
            }

            return Wait(now, CrafterHawkDwell);
        }

        var want = TradeDemandRules.WantFor(need, TradeMarket.AppetiteOf(_character), purse, roll);

        if (want is { } claim)
        {
            return WantAloud(now, claim, need is { } supply && claim.Row == TradeDemandRules.RowFor(supply.Kind));
        }

        if (HawkerGoods.BestInPack(_character) is not { } goods)
        {
            return SkillStatus.Done;
        }

        HoldUp(goods, roll);
        return Wait(now, ShopDwell);
    }

    // A WTB aloud and a wait for an answer. A supply WTB with no market-table words names the
    // first supply type short: "wtb lockpick".
    private SkillStatus WantAloud(DateTime now, GoodsClaim? want, bool suppliesWanted)
    {
        var noun = want?.Noun ?? (SupplyMarket.Wanted(_character) is { Count: > 0 } lines
            ? Appraisal.SplitWords(lines[0].Type.Name).ToLowerInvariant()
            : null);

        if (noun == null)
        {
            return SkillStatus.Done;
        }

        ChatLines.Shout(_character, selling: false, noun, null);

        if (want is { } claim)
        {
            TradeMarket.PostWant(_character, claim, now + ShopDwell);
        }

        _want = want;
        _wantsSupplies = suppliesWanted;
        return Wait(now, ShopDwell);
    }

    // A WTS for the goods at the market table's price, and the offer buyers read. Returns the asking price.
    private int HoldUp(Item goods, int roll)
    {
        var asking = ShopStock.IsStock(_character, goods, CraftMarket.TradeOf(_character))
            ? ShopStock.AskingOf(goods)
            : Appraisal.Value(goods, roll % Appraisal.PercentScale);
        var noun = Appraisal.NounOf(goods);
        ChatLines.Shout(_character, selling: true, noun, GoldWords.Spoken(asking));
        BankCrowd.SetHawkerOffer(_character, new HawkerOffer(goods.Serial, asking, noun));
        TradeTally.NoteHeldUp();
        _holdsStall = true;
        return asking;
    }

    // While the shout stands, somebody on the floor may answer it now and then. Goods that were
    // sold end the wait at the bank.
    private SkillStatus TickWaiting(DateTime now)
    {
        if (_holdsStall && !HoldsOffer())
        {
            return Finish(SkillStatus.Done);
        }

        if (FloorTradeRules.LookDue(now, _lookedAt))
        {
            _lookedAt = now;
            LookForAnswer();
        }

        // The dwell starts at the shout, not at the start of the walk to the bank.
        return TreasureMarketRules.DwellOver(now, _shoutedAt, _dwell) ? Finish(SkillStatus.Done) : SkillStatus.Running;
    }

    private void LookForAnswer()
    {
        if (_holdsStall)
        {
            FloorDeal.AnswerSale(_character);
        }
        else if (_wantsSupplies)
        {
            FloorDeal.AnswerSupply(_character);
        }
        else if (_want is { } claim && FloorDeal.AnswerWant(_character, claim) != null)
        {
            TradeMarket.DropWant(_character);
            _want = null;
        }
    }

    private bool HoldsOffer() =>
        BankCrowd.TryGetHawkerOffer(_character, out var offer) &&
        World.FindItem(offer.Item) is { } goods && goods.IsChildOf(_character.Backpack);

    private SkillStatus TickVisit(DateTime now) =>
        _visit.Tick(now) == SkillStatus.Running ? SkillStatus.Running : Finish(SkillStatus.Done);

    private SkillStatus Wait(DateTime now, TimeSpan dwell)
    {
        _shoutedAt = now;
        _lookedAt = now;
        _dwell = dwell;
        _step = ShopStep.Waiting;
        return SkillStatus.Running;
    }

    private SkillStatus Finish(SkillStatus status)
    {
        Leave();
        return status;
    }

    // Whatever the visit left standing goes: a half-done deal, a WTB still listening, goods held up.
    private void Leave()
    {
        _visit?.Abort();
        _visit = null;

        if (_character == null)
        {
            return;
        }

        TradeMarket.DropWant(_character);
        _want = null;
        _wantsSupplies = false;

        if (_holdsStall)
        {
            BankCrowd.ClearHawkerOffer(_character);
            _holdsStall = false;
        }
    }

    // The supply the person burns that is furthest short of its target, or null.
    private static SupplyNeed? WantedSupply(SosariaCharacter character)
    {
        var needs = SupplyRules.Shortfalls(SupplyCheck.ProfileOf(character));
        return needs.Count > 0 ? needs[0] : null;
    }
}
