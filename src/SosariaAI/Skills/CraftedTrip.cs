using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A fighter's trip for GM gear (<see cref="CraftedGear"/>): to the bank first when the pack is
/// short of the coin, then to the crafter's station. There it buys the piece in a haggle
/// (<see cref="DealVisit"/>), places its order with half paid down, or picks its order up and
/// pays the rest (<see cref="OrderDesk"/>). World thread only.
/// </summary>
public sealed class CraftedTrip
{
    private readonly SosariaCharacter _buyer;
    private readonly GearOffer _offer;
    private TravelSkill _walk;
    private DealVisit _visit;
    private bool _banked;

    private CraftedTrip(SosariaCharacter buyer, GearOffer offer, SosariaCharacter crafter)
    {
        _buyer = buyer;
        _offer = offer;
        Crafter = crafter;
    }

    public SosariaCharacter Crafter { get; }

    /// <summary>What the trip brought home: the bought piece, or the order's work.</summary>
    public IReadOnlyList<Item> Bought { get; private set; } = [];

    /// <summary>The gold paid at the crafter.</summary>
    public int Paid { get; private set; }

    /// <summary>The trip, or null when the crafter is gone or no walk starts.</summary>
    public static CraftedTrip Start(SosariaCharacter buyer, GearOffer offer)
    {
        if (World.FindMobile((Serial)offer.CrafterSerial) is not SosariaCharacter { Deleted: false } crafter)
        {
            return null;
        }

        var trip = new CraftedTrip(buyer, offer, crafter);
        return trip.WalkOn() ? trip : null;
    }

    public SkillStatus Tick(DateTime now)
    {
        if (_visit != null)
        {
            return TickVisit(now);
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk != SkillStatus.Done)
        {
            return walk;
        }

        _walk = null;

        if (!_banked)
        {
            _banked = true;
            BankTeller.Withdraw(_buyer, _offer.Price - PackGold());
            return WalkOn() ? SkillStatus.Running : SkillStatus.Failed;
        }

        return Act();
    }

    public void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _visit?.Abort();
        _visit = null;
    }

    public void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _visit?.Resume(held);
    }

    // The bank while the pack is short, then the crafter's station.
    private bool WalkOn()
    {
        _banked |= PackGold() >= _offer.Price;
        _walk = _banked
            ? new TravelSkill(Crafter.Location, TradeRanges.TalkRange)
            : new TravelSkill(BankTeller.BankToken, NavLimits.BankArrivalRange);
        return _walk.Begin(_buyer);
    }

    private SkillStatus Act()
    {
        switch (_offer.Kind)
        {
            case GearBuyKind.Pickup:
            {
                var rest = Crafter.OrderFor(_buyer.Serial.Value)?.Rest ?? 0;
                Bought = OrderDesk.HandOverToBot(Crafter, _buyer);
                Paid = Bought.Count > 0 ? rest : 0;
                return Bought.Count > 0 ? SkillStatus.Done : SkillStatus.Failed;
            }
            case GearBuyKind.Order:
            {
                Paid = _offer.Price;
                return OrderDesk.PlaceForBot(Crafter, _buyer, _offer.ItemTypeName) != null ? SkillStatus.Done : SkillStatus.Failed;
            }
            default:
            {
                return StartVisit() ? SkillStatus.Running : SkillStatus.Failed;
            }
        }
    }

    // The piece is still in the crafter's stock and still betters the buyer: the haggle starts.
    private bool StartVisit()
    {
        var piece = ShopStock.InPack(Crafter).Find(p => p.GetType().Name == _offer.ItemTypeName && CraftedGear.Betters(_buyer, p));

        if (piece == null)
        {
            return false;
        }

        _visit = DealVisit.Start(_buyer, Crafter, piece, ShopStock.AskingOf(piece));
        return _visit != null;
    }

    private SkillStatus TickVisit(DateTime now)
    {
        var status = _visit.Tick(now);

        if (status == SkillStatus.Done)
        {
            Bought = [_visit.Goods];
            Paid = _visit.Price;
        }

        if (status != SkillStatus.Running)
        {
            _visit = null;
        }

        return status;
    }

    private int PackGold() => _buyer.Backpack?.GetAmount(typeof(Gold)) ?? 0;
}
