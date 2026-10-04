using System;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A shout on the bank floor that somebody answers. A character holding goods up with a WTS
/// draws a bystander who wants them and can pay (<see cref="TradeMarket.FloorBuyerFor"/>); a
/// character's WTB draws a bystander who carries the goods for sale
/// (<see cref="TradeMarket.FloorSellerFor"/>), or who spares the supply it is short of
/// (<see cref="SupplyMarket"/>). The one who answers says so, crosses the floor to
/// arm's length, and the two haggle out loud (<see cref="TradeDeal"/>); a settled price closes
/// in the engine's trade window (<see cref="CharacterTrade"/>). Both sides stand for the deal:
/// their routines wait the way they wait for any talk, and go on when it is over. Neither
/// routine ticks the deal, so <see cref="TradeSessions"/> does. World thread only.
/// </summary>
public sealed class FloorDeal
{
    private readonly TradeDeal _deal;
    private readonly SosariaCharacter _walker;
    private readonly SosariaCharacter _host;

    private FloorDeal(TradeDeal deal, SosariaCharacter walker, SosariaCharacter host)
    {
        _deal = deal;
        _walker = walker;
        _host = host;
    }

    /// <summary>
    /// How far across the floor an answer comes from: the reach of a WTS walk-over, and never
    /// past the reach at which a character still holds still for talk.
    /// </summary>
    public static int Reach => Math.Min(TradeRanges.WalkOverRange, Brain.ReplyHearRange);

    /// <summary>
    /// The goods the seller holds up draw a buyer from the floor. Returns the deal opened; null
    /// when nothing is held up, nobody on the floor wants it at that price, or either side is
    /// already in a deal.
    /// </summary>
    public static FloorDeal AnswerSale(SosariaCharacter seller)
    {
        if (seller?.Backpack == null || !BankCrowd.TryGetHawkerOffer(seller, out var offer) ||
            World.FindItem(offer.Item) is not { } goods || !goods.IsChildOf(seller.Backpack) ||
            TradeMarket.FloorBuyerFor(seller, goods, offer.Asking, Reach) is not { } buyer)
        {
            return null;
        }

        return Open(buyer, seller, goods, offer.Asking, buyer, TradeLineKind.Interested);
    }

    /// <summary>
    /// The buyer's WTB draws a seller from the floor. Returns the deal opened; null when nobody on
    /// the floor carries the goods for sale at a price the buyer could meet, or either side is
    /// already in a deal.
    /// </summary>
    public static FloorDeal AnswerWant(SosariaCharacter buyer, GoodsClaim claim)
    {
        if (TradeMarket.FloorSellerFor(buyer, claim, Reach, Utility.Random(int.MaxValue)) is not { } found)
        {
            return null;
        }

        return Open(buyer, found.Seller, found.Goods, found.Asking, found.Seller, TradeLineKind.HaveOne);
    }

    /// <summary>
    /// The buyer, short of a supply the shops ran out of, draws a seller from the floor who
    /// carries that supply past its own restock target (<see cref="SupplyMarket"/>). The seller
    /// cuts no more than the buyer's shortfall off its stack and brings it over. Returns the deal
    /// opened; null when nobody on the floor spares what the buyer is short of.
    /// </summary>
    public static FloorDeal AnswerSupply(SosariaCharacter buyer)
    {
        var roll = Utility.Random(int.MaxValue);

        if (SupplyMarket.SellerFor(buyer, Reach, roll) is not { } offer || SupplyMarket.CutLot(offer) is not { } lot)
        {
            return null;
        }

        return Open(buyer, offer.Seller, lot, SupplyMarket.Asking(lot, lot.Amount, roll), offer.Seller, TradeLineKind.HaveOne);
    }

    /// <summary>The gold the goods went for, or 0 while no deal was settled.</summary>
    public int Price => _deal.Price;

    /// <summary>
    /// Holds both sides and walks the one who answered to arm's length while the haggle runs.
    /// False once the deal is over.
    /// </summary>
    public bool Tick(DateTime now)
    {
        if (!_deal.Done)
        {
            _walker.Conversation.Hold(_host.Serial, now, TradeRanges.Idle);
            _host.Conversation.Hold(_walker.Serial, now, TradeRanges.Idle);

            if (!_walker.InRange(_host, TradeRanges.DealRange))
            {
                _walker.Motor.MoveTo(_host, TradeRanges.DealRange);
            }
        }

        if (_deal.Tick(now))
        {
            return true;
        }

        Close();
        return false;
    }

    private static FloorDeal Open(
        SosariaCharacter buyer, SosariaCharacter seller, Item goods, int asking, SosariaCharacter walker, TradeLineKind answer
    )
    {
        if (TradeDeal.Start(buyer, seller, goods, asking) is not { } deal)
        {
            return null;
        }

        var host = walker == buyer ? seller : buyer;
        var floor = new FloorDeal(deal, walker, host);
        TradeVoice.Say(walker, host, TradeLines.For(answer, Utility.Random(int.MaxValue), deal.Noun, asking, 0));
        TradeSessions.OpenFloor(floor);
        return floor;
    }

    // Both sides go back to what they were doing; a closed deal counts as an answered shout.
    private void Close()
    {
        Release(_walker, _host);
        Release(_host, _walker);

        if (Price > 0)
        {
            TradeTally.NoteFloorAnswer();
        }
    }

    private static void Release(SosariaCharacter character, SosariaCharacter partner)
    {
        if (character.Conversation.Partner == partner.Serial)
        {
            character.Conversation.Clear();
        }
    }
}
