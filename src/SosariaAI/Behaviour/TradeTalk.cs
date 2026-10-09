using System.Collections.Generic;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Trade talk from people at a keyboard. Every spoken line passes through the engine's speech
/// event once, before any listener hears it, so a line is read once and routed once: into the
/// haggle the person is already in; to the hawker a "how much" or "what u got" is aimed at; to
/// the buyer who crosses the floor for a WTS shouted at a bank; to the character whose WTB an
/// "i have one" answers; to the hawker whose goods an "ill take it" buys; a WTB to a crafter
/// with the piece in stock; a supply asked for to a person near who spares it
/// (<see cref="SupplyMarket.SellerForPlayer"/>); a request for work or a question after an order to the order desk
/// (<see cref="OrderDesk"/>). The cheap parser reads it; only an unsure line inside a live haggle goes
/// to Jev. World thread only.
/// </summary>
public static class TradeTalk
{
    private const string WaresSeparator = ", ";

    private static bool _hooked;

    public static void Configure()
    {
        if (_hooked)
        {
            return;
        }

        EventSink.Speech += OnSpeech;
        _hooked = true;
    }

    private static void OnSpeech(SpeechEventArgs e) => Hear(e?.Mobile, e?.Speech);

    /// <summary>Reads one line from a person and hands it to the trade it belongs to.</summary>
    public static void Hear(Mobile speaker, string text)
    {
        if (!People.IsHuman(speaker) || !speaker.Alive || speaker.Map == null || speaker.Map == Map.Internal ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var session = TradeSessions.FindFor(speaker);

        if (session != null)
        {
            // A person who hid mid-haggle is not heard; the session ends on its next tick.
            if (People.Perceives(session.Character, speaker))
            {
                Continue(session, speaker, text);
            }

            return;
        }

        // A quote waiting for a yes makes "ok" and "yes" count, as inside a haggle.
        var intent = TradeParser.Read(text, listenerName: null, engaged: OrderDesk.HasQuote(speaker), standing: 0);

        switch (intent.Kind)
        {
            case TradeIntentKind.Accept when OrderDesk.HasQuote(speaker):
                OrderDesk.Accept(speaker);
                break;
            case TradeIntentKind.Decline when OrderDesk.HasQuote(speaker):
                OrderDesk.Decline(speaker);
                break;
            case TradeIntentKind.AskStock:
            case TradeIntentKind.AskPrice:
            case TradeIntentKind.Offer:
            case TradeIntentKind.Accept:
                AskSeller(speaker, text, intent);
                break;
            case TradeIntentKind.Sell when AtBank(speaker):
                CallBuyer(speaker, intent);
                break;
            case TradeIntentKind.HaveOne:
                AnswerWant(speaker, intent);
                break;
            case TradeIntentKind.Want:
                OfferStock(speaker, text, intent);
                break;
            case TradeIntentKind.Order:
                OrderDesk.Ask(speaker, text, intent);
                break;
            case TradeIntentKind.OrderStatus:
                OrderDesk.Status(speaker);
                break;
        }
    }

    private static void Continue(TradeSession session, Mobile speaker, string text)
    {
        var intent = TradeParser.Read(text, session.Character.Name, engaged: true, session.Standing);

        // "how much for the legs" while the smith quotes its katana: it quotes the legs instead.
        if (intent.Goods is { } named && session.Phase == TradePhase.Talk && session.Side == HaggleSide.Sells &&
            !session.Sells(named) && ShopStock.Named(session.Character, named) != null)
        {
            session.End(null);
            AskSeller(speaker, text, intent);
            return;
        }

        if (intent.Sure)
        {
            session.Hear(intent);
            return;
        }

        TradeIntentJev.TryAsk(
            session.Character,
            speaker,
            text,
            session.Noun,
            session.Standing,
            session.Side,
            heard =>
            {
                if (!session.Ended)
                {
                    session.Hear(heard);
                }
            }
        );
    }

    // "how much", "what u got", "3k for the hally", "ill take it": the hawker it is aimed at answers.
    // A crafter asked what it has names its best few pieces with their prices.
    private static void AskSeller(Mobile speaker, string text, TradeIntent intent)
    {
        if (TradeMarket.SellerFor(speaker, text, intent.Goods) is not { } found)
        {
            SellSupply(speaker, text, intent);
            return;
        }

        var session = TradeSession.Selling(found.Seller, speaker, found.Goods, found.Offer.Asking);
        TradeSessions.Open(session);

        if (intent.Kind == TradeIntentKind.AskStock &&
            ShopStock.Best(found.Seller, CraftShopRules.ListedPieces) is { Count: > 1 } wares)
        {
            TradeVoice.Say(
                found.Seller,
                speaker,
                TradeLines.For(TradeLineKind.StockList, Utility.Random(int.MaxValue), WaresText(wares), 0, 0)
            );
            return;
        }

        session.Hear(TradeParser.Read(text, found.Seller.Name, engaged: true, session.Standing));
    }

    // "wtb 10 bandages", "10 bandages for 50": a person near who spares that supply cuts the count
    // off its stack and haggles it in a real deal, the way it sells to another character.
    private static void SellSupply(Mobile speaker, string text, TradeIntent intent)
    {
        if (intent.Goods is not { } wanted ||
            SupplyMarket.SellerForPlayer(speaker, text, wanted, TradeRanges.TalkRange) is not { } offer ||
            SupplyMarket.CutLot(offer) is not { } lot)
        {
            return;
        }

        var session = TradeSession.Selling(
            offer.Seller,
            speaker,
            lot,
            SupplyMarket.Asking(lot, lot.Amount, Utility.Random(int.MaxValue))
        );
        TradeSessions.Open(session);
        session.Hear(TradeParser.Read(text, offer.Seller.Name, engaged: true, session.Standing));
    }

    // "GM plate chest 3.5k, GM katana 1.2k".
    private static string WaresText(List<Item> wares) =>
        string.Join(WaresSeparator, wares.ConvertAll(piece => $"{Appraisal.NounOf(piece)} {GoldWords.Spoken(ShopStock.AskingOf(piece))}"));

    // "wtb gm plate chest": a crafter in walking range with one in stock answers with its price.
    private static void OfferStock(Mobile speaker, string text, TradeIntent intent)
    {
        if (intent.Goods is not { } wanted || TradeMarket.StockFor(speaker, wanted) is not { } found)
        {
            SellSupply(speaker, text, intent);
            return;
        }

        var asking = ShopStock.AskingOf(found.Piece);
        TradeSessions.Open(TradeSession.Selling(found.Crafter, speaker, found.Piece, asking));
        TradeVoice.Say(
            found.Crafter,
            speaker,
            TradeLines.For(TradeLineKind.HaveOne, Utility.Random(int.MaxValue), Appraisal.NounOf(found.Piece), asking, 0)
        );
    }

    // "WTS GM halberd 5k" at a bank: somebody who wants it walks over.
    private static void CallBuyer(Mobile speaker, TradeIntent intent)
    {
        if (intent.Goods is not { } goods || TradeMarket.BuyerFor(speaker, goods, intent.Price) is not { } buyer)
        {
            return;
        }

        var session = TradeSession.Buying(buyer, speaker, goods, intent.Price);
        TradeSessions.Open(session);
        TradeVoice.Say(buyer, speaker, TradeLines.For(TradeLineKind.Interested, Utility.Random(int.MaxValue), session.Noun, 0, 0));
    }

    // "i have one", "i have one 5k": the character that shouted WTB comes over.
    private static void AnswerWant(Mobile speaker, TradeIntent intent)
    {
        if (TradeMarket.WantAnswered(speaker, intent.Goods) is not { } found)
        {
            return;
        }

        TradeMarket.DropWant(found.Buyer);
        var session = TradeSession.Buying(found.Buyer, speaker, found.Want.Claim, intent.Price);
        TradeSessions.Open(session);
        TradeVoice.Say(found.Buyer, speaker, TradeLines.For(TradeLineKind.AnswerWant, Utility.Random(int.MaxValue), session.Noun, 0, 0));
    }

    private static bool AtBank(Mobile speaker) =>
        BankTeller.FindBanker(speaker, BankTellerRules.BankerSearchRange) != null;
}
