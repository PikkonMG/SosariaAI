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
/// A gatherer brings its load to the bank where a crafter waits for it (<see cref="BankStock"/>):
/// it walks to the bank spot the want was posted from, names the crafter that waits there now
/// (<see cref="BankStock.WaiterFor"/>), holds that crafter's lot up with a WTS at the market
/// table's price and waits for it to walk up and haggle, or for a person who asks the price. When
/// the lot changed hands, the rest of the stack goes up for the next crafter waiting there. Done
/// when any lot sold; nobody waiting on arrival, or nobody buying in the dwell, fails, and the
/// load goes on to the shop.
/// </summary>
public sealed class BankStockSellSkill : Skill
{
    private const string NoBankWhy = "no bank spot to bring the stock to";
    private const string NoWalkWhy = "no walk to the bank";
    private const string WalkFailedWhy = "the walk to the bank failed";
    public const string NoWaiterWhy = "no crafter waits at the bank now";
    private const string NoBuyerWhy = "nobody at the bank bought the stock";

    private static readonly ILogger logger = SosariaLog.For(typeof(BankStockSellSkill));

    private readonly Point3D _bank;
    private SosariaCharacter _character;
    private Item _stock;
    private Skill _walk;
    private HawkerOffer _offer;
    private int _offeredAmount;
    private int _lotsSold;
    private DateTime _heldSince;
    private DateTime _nextShout;

    public BankStockSellSkill(Point3D bank) => _bank = bank;

    public override string Name => SkillKinds.VendorSell;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _stock = null;
        _lotsSold = 0;
        _heldSince = default;

        if (character == null || _bank == Point3D.Zero)
        {
            return CannotStart(NoBankWhy);
        }

        if (character.InRange(_bank, NavLimits.BankArrivalRange))
        {
            return NextLot(Core.Now) || CannotStart(NoWaiterWhy);
        }

        _walk = new TravelSkill(_bank, NavLimits.BankArrivalRange);
        return _walk.Begin(character) || CannotStart(NoWalkWhy);
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true } || !People.InWorld(_character))
        {
            return Finish(Fail(LeftWorldReason));
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

            if (walk != SkillStatus.Done)
            {
                return Finish(Fail(WalkFailedWhy));
            }

            return NextLot(now) ? SkillStatus.Running : Finish(Fail(NoWaiterWhy));
        }

        // The lot changed hands: the buyer's deal logged the price. The rest goes up for the
        // next crafter waiting here.
        if (!Carried())
        {
            _lotsSold++;
            return NextLot(now) ? SkillStatus.Running : Finish(SkillStatus.Done);
        }

        // A buyer cut its lot off the stack: the rest of the ask no longer fits it.
        if (_stock.Amount != _offeredAmount)
        {
            Price();
        }

        if (!TradeSessions.IsBusy(_character) && TreasureMarketRules.DwellOver(now, _heldSince, BankStockRules.OfferDwell))
        {
            return Finish(_lotsSold > 0 ? SkillStatus.Done : Fail(NoBuyerWhy));
        }

        if (now >= _nextShout)
        {
            Shout(now);
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
        _heldSince = SkillClock.Shift(_heldSince, held);
        _walk?.Resume(held);
    }

    /// <summary>
    /// Holds up the lot of the crafter waiting here now: only what the seller spares
    /// (<see cref="CraftMarket.SpareUnits"/>), so a mining smith's keep stays in its pack. False
    /// when nobody waits for anything carried.
    /// </summary>
    private bool NextLot(DateTime now)
    {
        if (BankStock.WaiterFor(_character) is not { } waiter ||
            !BankStock.CutLot(waiter.Stock, CraftMarket.SpareUnits(_character, waiter.Stock)))
        {
            LogNoWaiter();
            return false;
        }

        _stock = waiter.Stock;
        _heldSince = now;
        Price();
        Shout(now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} holds up {Units} {Stock} at the bank for the {Trade} crafter {Crafter} at {Location}",
                _character.Name,
                _stock.Amount,
                _stock.GetType().Name,
                waiter.Trade.Kind,
                waiter.Crafter.Name,
                _character.Location
            );
        }

        return true;
    }

    private void LogNoWaiter()
    {
        if (SosariaSettings.LogActivity && _lotsSold == 0)
        {
            logger.Information("{Name} found no crafter waiting at the bank for its stock at {Location}", _character.Name, _character.Location);
        }
    }

    private void Price()
    {
        _offeredAmount = _stock.Amount;
        _offer = new HawkerOffer(
            _stock.Serial,
            Appraisal.Value(_stock, Utility.Random(Appraisal.PercentScale)),
            Appraisal.NounOf(_stock)
        );
        BankCrowd.SetHawkerOffer(_character, _offer);
    }

    private void Shout(DateTime now)
    {
        _nextShout = now + BankCrowdRules.ShoutGap(Utility.Random(int.MaxValue));
        ChatLines.Shout(_character, selling: true, _offer.Noun, GoldWords.Spoken(_offer.Asking));
    }

    private bool Carried() => _stock is { Deleted: false } && _character != null && _stock.IsChildOf(_character.Backpack);

    private SkillStatus Finish(SkillStatus status)
    {
        if (_character != null)
        {
            BankCrowd.ClearHawkerOffer(_character);
        }

        return status;
    }
}
