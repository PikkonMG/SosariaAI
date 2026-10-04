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
/// A crafter still dry after its wait at the station tries the bank (<see cref="BankStockRules"/>),
/// when its purse, bank balance counted, pays for a unit there: it walks to the bank in reach
/// where stock is held up, else the nearest, shouts WTB for its stock and posts the want on the
/// board, so a gatherer on its way to the shop comes by and a person at a keyboard can answer it.
/// It looks over the floor for a gatherer holding the stock up, cuts the lot it can store and
/// pay for, and haggles for it out loud (<see cref="DealVisit"/>): the gold and the stack change
/// hands for real. Done once stock came into the pack. A wait with no seller walks on, once, to
/// another bank in reach where stock is held up; with none, it fails and the station falls back
/// on the shops.
/// </summary>
public sealed class BankStockBuySkill : Skill
{
    private const string NoStockRowWhy = "the bank does not deal in this stock";
    private const string NoBankWhy = "no bank in reach";
    private const string NoWalkWhy = "no walk to the bank";
    private const string WalkFailedWhy = "the walk to the bank failed";
    private const string NoSellerWhy = "no gatherer at the bank sold stock";
    private const string CannotWorkWhy = "cannot work now";
    public const string NoCoinWhy = "no coin for stock at the bank";

    private static readonly ILogger logger = SosariaLog.For(typeof(BankStockBuySkill));

    private readonly CraftTrade _trade;
    private SosariaCharacter _character;
    private Skill _walk;
    private DealVisit _visit;
    private DateTime _since;
    private DateTime _nextLook;
    private DateTime _nextShout;
    private int _stockBefore;
    private Point3D _bankSpot;
    private bool _movedOn;

    public BankStockBuySkill(CraftTrade trade) =>
        _trade = trade ?? throw new ArgumentNullException(nameof(trade));

    public override string Name => _trade.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _visit = null;
        _since = default;
        _movedOn = false;

        if (!CraftStationSkill.MayWork(character))
        {
            return CannotStart(CannotWorkWhy);
        }

        if (BankStock.ClaimOf(_trade) is not { } claim)
        {
            return CannotStart(NoStockRowWhy);
        }

        var purse = TradeHandOff.PurseAtBank(character);

        if (!BankStockRules.PaysForAUnit(purse, claim.Row))
        {
            return CannotStart(NoCoinWhy);
        }

        if (BankStock.BankFor(character, _trade, purse) is not { } bank)
        {
            return CannotStart(NoBankWhy);
        }

        return WalkTo(bank) || CannotStart(NoWalkWhy);
    }

    public override SkillStatus Tick()
    {
        if (!CraftStationSkill.MayWork(_character))
        {
            return Finish(Fail(CannotWorkWhy));
        }

        var now = Core.Now;

        if (_walk != null)
        {
            var walk = _walk.Tick();

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;
            return walk == SkillStatus.Done ? Arrive(now) : Finish(Fail(WalkFailedWhy));
        }

        if (_visit != null)
        {
            var visit = _visit;
            var status = visit.Tick(now);

            if (status == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _visit = null;

            if (status == SkillStatus.Done)
            {
                return Bought(visit);
            }

            // No deal with this gatherer: the crafter does not walk up to it again soon.
            TradeMarket.NoteRefusal(_character, visit.Seller, default);
        }

        // A person who answered the WTB handed stock over in a haggle of its own.
        if (StockCarried() > _stockBefore)
        {
            return Finish(SkillStatus.Done);
        }

        if (TradeSessions.IsBusy(_character))
        {
            return SkillStatus.Running;
        }

        if (TreasureMarketRules.DwellOver(now, _since, BankStockRules.WantDwell))
        {
            return MoveOn() ? SkillStatus.Running : Finish(Fail(NoSellerWhy));
        }

        if (now >= _nextShout)
        {
            Shout(now);
        }

        if (now >= _nextLook)
        {
            _nextLook = now + BankStockRules.LookGap;
            _visit = BankStock.StartBuy(_character, _trade);
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        Finish(SkillStatus.Failed);
    }

    public override void Resume(TimeSpan held)
    {
        _since = SkillClock.Shift(_since, held);
        _walk?.Resume(held);
        _visit?.Resume(held);
    }

    private bool WalkTo(Destination bank)
    {
        _bankSpot = bank.Arrival;
        _walk = new TravelSkill(_bankSpot, NavLimits.BankArrivalRange);

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    // Nobody sold here: the want comes down and the crafter walks on to a bank in reach where
    // stock is held up now, once a trip.
    private bool MoveOn()
    {
        if (_movedOn ||
            BankStock.BankWithOffer(_character, _trade, TradeHandOff.PurseAtBank(_character), _bankSpot) is not { } next)
        {
            return false;
        }

        _movedOn = true;
        BankStock.DropWant(_character);
        return WalkTo(next);
    }

    private SkillStatus Arrive(DateTime now)
    {
        _since = now;
        _nextLook = now;
        _stockBefore = StockCarried();
        BankStock.PostWant(_character, _trade, now + BankStockRules.WantDwell);
        Shout(now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} waits at the bank for {Stock} for the {Trade} trade at {Location}",
                _character.Name,
                _trade.StockNoun,
                _trade.Kind,
                _character.Location
            );
        }

        return SkillStatus.Running;
    }

    private void Shout(DateTime now)
    {
        _nextShout = now + BankCrowdRules.ShoutGap(Utility.Random(int.MaxValue));
        ChatLines.Shout(_character, selling: false, _trade.StockNoun, null);
    }

    private SkillStatus Bought(DealVisit visit)
    {
        var stock = visit.Goods;
        CraftTally.NoteStockDeal(visit.Amount, visit.Price);
        Talk.Maybe(
            _character,
            TalkCategory.CraftBuyStock,
            TalkOdds.CraftDonePercent,
            new TalkSlots { Name = visit.Seller.Name, Item = Appraisal.RowOf(stock).Noun }
        );

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Buyer} bought {Units} {Stock} from the gatherer {Seller} at the bank for {Gold} gold for the {Trade} trade at {Location}",
                _character.Name,
                visit.Amount,
                stock.GetType().Name,
                visit.Seller.Name,
                visit.Price,
                _trade.Kind,
                _character.Location
            );
        }

        return Finish(SkillStatus.Done);
    }

    private int StockCarried() => CraftStations.StockCarried(_character, _trade);

    private SkillStatus Finish(SkillStatus status)
    {
        _visit?.Abort();
        _visit = null;
        BankStock.DropWant(_character);
        return status;
    }
}
