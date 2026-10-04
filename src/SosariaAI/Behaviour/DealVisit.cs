using System;
using Server;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// A buyer's visit to goods another character holds up: it walks to talking range, opens the
/// haggle (<see cref="TradeDeal"/>) and closes to arm's length while the numbers go back and
/// forth. The bank shopper, the map buyer, the crafter short of stock and the fighter after a
/// crafter's piece all visit this one way. The walk is the buyer's; the deal holds the seller.
/// World thread only.
/// </summary>
public sealed class DealVisit
{
    private readonly SosariaCharacter _buyer;
    private readonly int _asking;
    private Skill _walk;
    private TradeDeal _deal;

    private DealVisit(SosariaCharacter buyer, SosariaCharacter seller, Item goods, int asking)
    {
        _buyer = buyer;
        Seller = seller;
        Goods = goods;
        Amount = goods.Amount;
        _asking = asking;
    }

    public SosariaCharacter Seller { get; }

    public Item Goods { get; }

    /// <summary>How many the goods counted when the visit began: a bought stack may join the buyer's own.</summary>
    public int Amount { get; }

    /// <summary>The gold the goods went for, or 0 when no deal was settled.</summary>
    public int Price => _deal?.Price ?? 0;

    /// <summary>
    /// Starts the visit. Null when the goods left the seller's pack, either side is already in a
    /// deal, or the buyer cannot walk over.
    /// </summary>
    public static DealVisit Start(SosariaCharacter buyer, SosariaCharacter seller, Item goods, int asking)
    {
        if (buyer == null || seller == null || buyer == seller || goods is not { Deleted: false } ||
            !goods.IsChildOf(seller.Backpack) || TradeSessions.IsBusy(buyer) || TradeSessions.IsBusy(seller))
        {
            return null;
        }

        var visit = new DealVisit(buyer, seller, goods, asking);
        return visit.Approach() ? visit : null;
    }

    /// <summary>Running while the buyer walks or haggles; Done when the goods were paid for and handed over; Failed otherwise.</summary>
    public SkillStatus Tick(DateTime now) => _deal == null ? TickApproach() : TickDeal(now);

    /// <summary>Called off: the walk stops and both sides go back to what they were doing.</summary>
    public void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _deal?.Abort();
    }

    public void Resume(TimeSpan held) => _walk?.Resume(held);

    private bool Approach()
    {
        if (_buyer.InRange(Seller, TradeRanges.TalkRange))
        {
            return OpenDeal();
        }

        _walk = new TravelSkill(Seller.Location, TradeRanges.TalkRange);
        return _walk.Begin(_buyer);
    }

    // The deal only opens within a bank floor's walk of the seller: close in first.
    private SkillStatus TickApproach()
    {
        if (!Goods.IsChildOf(Seller.Backpack))
        {
            Abort();
            return SkillStatus.Failed;
        }

        if (!_buyer.InRange(Seller, TradeRanges.TalkRange))
        {
            var walk = _walk?.Tick() ?? SkillStatus.Failed;

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            if (walk != SkillStatus.Done)
            {
                return SkillStatus.Failed;
            }
        }

        _walk?.Abort();
        _walk = null;
        return OpenDeal() ? SkillStatus.Running : SkillStatus.Failed;
    }

    private bool OpenDeal()
    {
        _deal = TradeDeal.Start(_buyer, Seller, Goods, _asking);
        return _deal != null;
    }

    private SkillStatus TickDeal(DateTime now)
    {
        if (!_deal.Tick(now))
        {
            _walk?.Abort();
            _walk = null;
            return _deal.Price > 0 ? SkillStatus.Done : SkillStatus.Failed;
        }

        if (_buyer.InRange(Seller, TradeRanges.DealRange))
        {
            _walk?.Abort();
            _walk = null;
            return SkillStatus.Running;
        }

        if (_walk == null)
        {
            _walk = new GoToSkill(Seller.Location, TradeRanges.DealRange);

            if (!_walk.Begin(_buyer))
            {
                _walk = null;
                return SkillStatus.Running;
            }
        }

        if (_walk.Tick() != SkillStatus.Running)
        {
            _walk = null;
        }

        return SkillStatus.Running;
    }
}
