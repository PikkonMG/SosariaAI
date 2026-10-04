using System;
using Server;
using SosariaAI.Economy;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// A hawker: holds one real item from its pack (or fetches one from its bank box) and
/// shouts a WTS line built from that item and its asking price. Between shouts a bystander
/// idling on the floor may answer and come over to deal (<see cref="FloorDeal"/>). When the item
/// is gone it fetches the next, and when there is nothing left to sell it leaves the crowd.
/// </summary>
public sealed class BankCrowdHawker : BankCrowdAct
{
    private Item _goods;
    private HawkerOffer _offer;
    private DateTime _nextShout;
    private DateTime _lookedAt;

    public override bool Tick(DateTime now)
    {
        if (!Holds(_goods) && !TakeGoods())
        {
            return false;
        }

        if (FloorTradeRules.LookDue(now, _lookedAt))
        {
            _lookedAt = now;
            FloorDeal.AnswerSale(Member);
        }

        if (now < _nextShout)
        {
            return true;
        }

        _nextShout = now + BankCrowdRules.ShoutGap(Roll());
        FaceNearest();
        ChatLines.Shout(Member, selling: true, _offer.Noun, GoldWords.Spoken(_offer.Asking));
        return true;
    }

    public override void End() => BankCrowd.ClearHawkerOffer(Member);

    protected override bool OnStart() => TakeGoods();

    private bool Holds(Item goods) => goods is { Deleted: false } && goods.IsChildOf(Member.Backpack);

    private bool TakeGoods()
    {
        _goods = HawkerGoods.BestInPack(Member);

        if (_goods == null && BankTeller.OpenBox(Member) && BankCrowd.BankGoods(Member) is { } stored)
        {
            Member.Backpack.DropItem(stored);
            _goods = stored;
        }

        if (_goods == null)
        {
            BankCrowd.ClearHawkerOffer(Member);
            return false;
        }

        _offer = new HawkerOffer(_goods.Serial, Appraisal.Value(_goods, Roll() % Appraisal.PercentScale), Appraisal.NounOf(_goods));
        BankCrowd.SetHawkerOffer(Member, _offer);
        TradeTally.NoteHeldUp();
        _nextShout = Core.Now;
        _lookedAt = Core.Now;
        return true;
    }
}
