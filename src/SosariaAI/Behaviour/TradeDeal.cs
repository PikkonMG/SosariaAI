using System;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// One character buying from a hawker, out loud and for real. The buyer walks up (its skill or
/// the bank floor's answer does the walking), both sides haggle a line a beat over the hawker's
/// real ask and floor and the buyer's real purse, and on a deal the goods and the gold change
/// hands in the engine's trade window (<see cref="CharacterTrade"/>). Either side short, or too
/// far apart, and the deal falls through, which reads as one. World thread only.
/// </summary>
public sealed class TradeDeal
{
    public const int BeatSeconds = 4;
    public const string OutbidReason = "outbid";

    public static readonly TimeSpan Beat = TimeSpan.FromSeconds(BeatSeconds);

    private static readonly ILogger logger = SosariaLog.For(typeof(TradeDeal));

    private readonly Haggle _seller;
    private readonly Haggle _buyer;
    private readonly Item _goods;
    private readonly GoodsClaim _claim;
    private readonly DateTime _started;
    private DateTime _nextBeat;
    private bool _opened;
    private bool _buyerTurn;

    private TradeDeal(SosariaCharacter buyer, SosariaCharacter seller, Item goods, int asking)
    {
        Buyer = buyer;
        Seller = seller;
        _goods = goods;
        _claim = Appraisal.ClaimOf(goods);
        Noun = Appraisal.NounOf(goods);
        _seller = Haggle.Selling(asking, TradeSession.TemperOf(seller));
        _buyer = Haggle.Buying(_claim.Value(Appraisal.MidRoll), TradeHandOff.Purse(buyer), TradeSession.TemperOf(buyer));
        _started = Core.Now;
    }

    public SosariaCharacter Buyer { get; }

    public SosariaCharacter Seller { get; }

    public string Noun { get; }

    public bool Done { get; private set; }

    /// <summary>The gold that changed hands, or 0 while no deal was settled.</summary>
    public int Price { get; private set; }

    public static TradeDeal Start(SosariaCharacter buyer, SosariaCharacter seller, Item goods, int asking)
    {
        if (buyer == null || seller == null || goods == null || TradeSessions.IsBusy(buyer) || TradeSessions.IsBusy(seller))
        {
            return null;
        }

        TradeSessions.JoinDeal(buyer);
        TradeSessions.JoinDeal(seller);
        return new TradeDeal(buyer, seller, goods, asking);
    }

    /// <summary>
    /// Holds the hawker for its customer and, once the buyer stands at arm's length, says the next
    /// line on the beat. False once the deal is over.
    /// </summary>
    public bool Tick(DateTime now)
    {
        if (Done)
        {
            return false;
        }

        if (!BothHere() || !_goods.IsChildOf(Seller.Backpack))
        {
            Finish();
            return false;
        }

        Seller.Conversation.Hold(Buyer.Serial, now, TradeRanges.Idle);

        if (!Buyer.InRange(Seller, TradeRanges.DealRange))
        {
            if (now - _started > TradeRanges.Approach)
            {
                Finish();
            }

            return !Done;
        }

        if (now < _nextBeat)
        {
            return true;
        }

        _nextBeat = now + Beat;
        Buyer.Direction = Buyer.GetDirectionTo(Seller);
        Seller.Direction = Seller.GetDirectionTo(Buyer);

        if (!_opened)
        {
            _opened = true;
            Say(Buyer, Seller, TradeLineKind.BuyerOpen, _buyer.Standing, 0);
            return true;
        }

        var roll = Utility.Random(Haggle.PercentScale);

        if (_buyerTurn)
        {
            BuyerAnswers(roll);
        }
        else
        {
            SellerAnswers(roll);
        }

        _buyerTurn = !_buyerTurn;
        return !Done;
    }

    /// <summary>Called off by the buyer's routine: both sides go back to what they were doing.</summary>
    public void Abort() => Finish();

    private void SellerAnswers(int roll)
    {
        var bid = _buyer.Standing;
        var step = _seller.Hear(bid, roll);

        switch (step.Move)
        {
            case HaggleMove.Accept:
                Say(Seller, Buyer, TradeLineKind.SellerAccept, step.Price, 0);
                Settle(step.Price);
                break;
            case HaggleMove.Counter:
                Say(Seller, Buyer, TradeLineKind.SellerCounter, step.Price, bid);
                break;
            case HaggleMove.Firm:
                Say(Seller, Buyer, TradeLineKind.SellerFirm, step.Price, bid);
                break;
            default:
                Say(Seller, Buyer, step.Move == HaggleMove.WalkAway ? TradeLineKind.SellerWalk : TradeLineKind.Insulted, step.Price, bid);
                FallThrough();
                break;
        }
    }

    private void BuyerAnswers(int roll)
    {
        var ask = _seller.Standing;
        var step = _buyer.Hear(ask, roll);

        switch (step.Move)
        {
            case HaggleMove.Accept:
                Say(Buyer, Seller, TradeLineKind.BuyerAccept, step.Price, 0);
                Settle(step.Price);
                break;
            case HaggleMove.Counter:
                Say(Buyer, Seller, TradeLineKind.BuyerCounter, step.Price, ask);
                break;
            case HaggleMove.Firm:
                Say(Buyer, Seller, TradeLineKind.BuyerFirm, step.Price, ask);
                break;
            default:
                Say(Buyer, Seller, step.Move == HaggleMove.WalkAway ? TradeLineKind.BuyerWalk : TradeLineKind.Insulted, step.Price, ask);
                FallThrough();
                break;
        }
    }

    // Too far apart: the buyer will not come back soon, and neither thinks better of the other.
    private void FallThrough()
    {
        TradeMarket.NoteRefusal(Buyer, Seller, _claim);
        Buyer.Memory.ShiftBond(Seller, -BondRules.OutbidPenalty, OutbidReason);
        Seller.Memory.ShiftBond(Buyer, -BondRules.OutbidPenalty, OutbidReason);
        Finish();
    }

    private void Settle(int price)
    {
        // A buyer whose coin fell short does not come back for the same goods at once.
        if (!CharacterTrade.Swap(Seller, _goods, Buyer, price))
        {
            Say(Buyer, Seller, TradeLineKind.ShortOfGold, price, 0);
            TradeMarket.NoteRefusal(Buyer, Seller, _claim);
            Finish();
            return;
        }

        Price = price;
        Say(Seller, Buyer, TradeLineKind.Thanks, price, 0);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Buyer} bought {Noun} from {Seller} for {Price} gold", Buyer.Name, Noun, Seller.Name, price);
        }

        Finish();
    }

    private void Finish()
    {
        if (Done)
        {
            return;
        }

        Done = true;
        TradeSessions.LeaveDeal(Buyer);
        TradeSessions.LeaveDeal(Seller);

        if (Seller.Conversation.Partner == Buyer.Serial)
        {
            Seller.Conversation.Clear();
        }
    }

    private bool BothHere() =>
        Buyer is { Deleted: false, Alive: true } && Seller is { Deleted: false, Alive: true } &&
        Buyer.Map == Seller.Map && Buyer.Map != null && Buyer.Map != Map.Internal &&
        Buyer.Motor.Action == CharacterAction.Wander && Seller.Motor.Action == CharacterAction.Wander &&
        Buyer.InRange(Seller, TradeRanges.WalkOverRange);

    private void Say(SosariaCharacter speaker, SosariaCharacter listener, TradeLineKind kind, int price, int theirs) =>
        TradeVoice.Say(speaker, listener, TradeLines.For(kind, Utility.Random(int.MaxValue), Noun, price, theirs));
}
