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
/// "i have one" answers; to the hawker whose goods an "ill take it" buys. The cheap parser reads it; only an unsure line inside a live haggle goes
/// to Jev. World thread only.
/// </summary>
public static class TradeTalk
{
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

        var intent = TradeParser.Read(text, listenerName: null, engaged: false, standing: 0);

        switch (intent.Kind)
        {
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
        }
    }

    private static void Continue(TradeSession session, Mobile speaker, string text)
    {
        var intent = TradeParser.Read(text, session.Character.Name, engaged: true, session.Standing);

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
    private static void AskSeller(Mobile speaker, string text, TradeIntent intent)
    {
        if (TradeMarket.SellerFor(speaker, text, intent.Goods) is not { } found)
        {
            return;
        }

        var session = TradeSession.Selling(found.Seller, speaker, found.Goods, found.Offer.Asking);
        TradeSessions.Open(session);
        session.Hear(TradeParser.Read(text, found.Seller.Name, engaged: true, session.Standing));
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
