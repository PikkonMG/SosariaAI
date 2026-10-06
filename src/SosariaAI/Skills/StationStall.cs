using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// The shop a crafter keeps at its station between batches. It holds its best piece of shop
/// stock up, so a person's "how much" or "what do you have" finds it
/// (<see cref="TradeMarket.SellerFor"/>), and it stands on the board fighters shop from
/// (<see cref="CraftShopBoard"/>). Every few minutes, with somebody in sight, it shouts a WTS for
/// that piece and, when it can take one, says it takes orders. It hands finished orders over and
/// lets expired ones go (<see cref="OrderDesk"/>). World thread only.
/// </summary>
public sealed class StationStall
{
    private static readonly ILogger logger = SosariaLog.For(typeof(StationStall));

    private readonly SosariaCharacter _crafter;
    private readonly HashSet<uint> _pickupsOffered = [];
    private DateTime _nextRefresh;
    private DateTime _nextShout;
    private bool _open;

    public StationStall(SosariaCharacter crafter) => _crafter = crafter;

    public void Tick(DateTime now)
    {
        if (_crafter == null || TradeSessions.IsBusy(_crafter) || now < _nextRefresh)
        {
            return;
        }

        _nextRefresh = now + CraftShopRules.RefreshGap;

        if (!_open)
        {
            _open = true;
            _nextShout = now + CraftShopRules.ShoutGap(Utility.Random(int.MaxValue));
        }

        CraftShopBoard.Note(_crafter);
        OrderDesk.Sweep(_crafter, now);

        if (OrderDesk.OfferPickup(_crafter, _pickupsOffered))
        {
            return;
        }

        var best = ShopStock.Best(_crafter, 1);

        if (best.Count == 0)
        {
            BankCrowd.ClearHawkerOffer(_crafter);
            return;
        }

        var piece = best[0];
        var asking = ShopStock.AskingOf(piece);
        var noun = Appraisal.NounOf(piece);
        BankCrowd.SetHawkerOffer(_crafter, new HawkerOffer(piece.Serial, asking, noun));

        if (now >= _nextShout && SomeoneInSight())
        {
            _nextShout = now + CraftShopRules.ShoutGap(Utility.Random(int.MaxValue));
            Shout(noun, asking);
        }
    }

    /// <summary>The shop closes: the piece goes down and the crafter leaves the board.</summary>
    public void Close()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        BankCrowd.ClearHawkerOffer(_crafter);
        CraftShopBoard.Forget(_crafter);
    }

    private void Shout(string noun, int asking)
    {
        ChatLines.Shout(_crafter, selling: true, noun, GoldWords.Spoken(asking));

        if (OrderDesk.MayTakeOrder(_crafter))
        {
            Talk.Maybe(
                _crafter,
                TalkCategory.CraftTakingOrders,
                CraftShopRules.OrderTalkPercent,
                new TalkSlots { Item = CraftMarket.TradeOf(_crafter)?.GoodsNoun }
            );
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} holds up the {Piece} at its {Trade} station for {Gold} gold at {Location}",
                _crafter.Name,
                noun,
                CraftMarket.TradeOf(_crafter)?.Kind,
                asking,
                _crafter.Location
            );
        }
    }

    // A person at a keyboard, or a character free to shop, that the crafter sees.
    private bool SomeoneInSight()
    {
        foreach (var mobile in _crafter.GetMobilesInRange(TradeRanges.ShoutRange))
        {
            if (mobile != _crafter && People.Perceives(_crafter, mobile) &&
                (People.IsHuman(mobile) || mobile is SosariaCharacter character && TradeMarket.MayShop(character)))
            {
                return true;
            }
        }

        return false;
    }
}
