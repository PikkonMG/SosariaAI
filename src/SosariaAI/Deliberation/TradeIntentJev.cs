using System;
using System.Collections.Generic;
using System.Globalization;
using Server;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>
/// Layer three of trade talk. The cheap parser reads nearly every line; only a line from a person
/// at a keyboard, inside a live haggle, that smells of trade but fits no rule is put to Jev as two
/// typed choices: the intent, and which heard number is the price. Jev never writes words. A shaky
/// answer is no answer. One call per character per <see cref="Cooldown"/>.
/// </summary>
public static class TradeIntentJev
{
    public const string Instructions =
        "A person at a market is haggling with you. Which of these did their last words mean?";

    public const string PriceInstructions =
        "If they named a price, which of these amounts of gold did they mean?";

    public const double MinConfidence = 0.6;
    public const int CooldownSeconds = 15;
    public const string NoPrice = "none";

    internal const string IntentQuestion = "trade_intent";
    internal const string PriceQuestion = "trade_price";

    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(CooldownSeconds);

    private static readonly CharacterCooldown _asked = new();

    private static readonly IReadOnlyDictionary<string, string> IntentOptions = new Dictionary<string, string>
    {
        ["none"] = "small talk, not about this deal",
        ["ask_price"] = "asks what it costs",
        ["offer"] = "puts a number of gold on the table",
        ["accept"] = "agrees to the last price you said",
        ["decline"] = "backs out of the deal"
    };

    private static readonly IReadOnlyDictionary<string, TradeIntentKind> IntentKinds =
        new Dictionary<string, TradeIntentKind>
        {
            ["ask_price"] = TradeIntentKind.AskPrice,
            ["offer"] = TradeIntentKind.Offer,
            ["accept"] = TradeIntentKind.Accept,
            ["decline"] = TradeIntentKind.Decline
        };

    /// <summary>Set by the Brain when a System One provider is routed. Returns true when it took the call.</summary>
    internal static Func<JevCall, bool> Asker { get; set; }

    /// <summary>
    /// The prices a line could mean: each number as said, and a small bare number read as thousands
    /// ("would you do 3" in a haggle over 3500).
    /// </summary>
    public static List<int> PriceOptions(string heard)
    {
        var options = new List<int>();

        foreach (var number in GoldWords.NumbersIn(TradeParser.Words(heard)))
        {
            AddOnce(options, number.Value);

            if (!number.IsMoney && number.Value < GoldWords.ShortThousandsBelow)
            {
                AddOnce(options, number.Value * GoldWords.Thousand);
            }
        }

        return options;
    }

    public static JevDecision Build(string heard, string noun, int standing, HaggleSide side, IReadOnlyList<int> prices)
    {
        var state = new Dictionary<string, object>
        {
            ["you_are"] = side == HaggleSide.Sells ? "the seller" : "the buyer",
            ["goods"] = noun ?? "goods",
            ["your_last_price"] = standing,
            ["their_words"] = heard ?? string.Empty
        };

        var questions = new Dictionary<string, JevQuestion>
        {
            [IntentQuestion] = new(SystemOneApi.ChoiceType, Instructions, IntentOptions)
        };

        if (prices is { Count: > 0 })
        {
            var options = new Dictionary<string, string> { [NoPrice] = "no price named" };

            foreach (var price in prices)
            {
                options[Key(price)] = $"{Key(price)} gold";
            }

            questions[PriceQuestion] = new(SystemOneApi.ChoiceType, PriceInstructions, options);
        }

        return new JevDecision(state, questions);
    }

    /// <summary>Jev's answers as an intent. Below the confidence floor, or malformed, it is nothing.</summary>
    internal static TradeIntent Read(IReadOnlyDictionary<string, SystemOneAnswer> answers, int standing)
    {
        if (answers == null || !answers.TryGetValue(IntentQuestion, out var intent) ||
            intent.Confidence < MinConfidence || intent.Choice == null ||
            !IntentKinds.TryGetValue(intent.Choice, out var kind))
        {
            return TradeIntent.Nothing;
        }

        var price = 0;

        if (answers.TryGetValue(PriceQuestion, out var named) && named.Confidence >= MinConfidence &&
            int.TryParse(named.Choice, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            price = value;
        }

        return kind switch
        {
            TradeIntentKind.Offer when price <= 0 => TradeIntent.Nothing,
            TradeIntentKind.Accept => new TradeIntent(kind, standing, null, true),
            _ => new TradeIntent(kind, price, null, true)
        };
    }

    /// <summary>
    /// Puts an unsure line to Jev. False when no provider is routed or the character asked too
    /// recently; the line then passes as small talk.
    /// </summary>
    public static bool TryAsk(
        SosariaCharacter character,
        Mobile speaker,
        string heard,
        string noun,
        int standing,
        HaggleSide side,
        Action<TradeIntent> onIntent
    )
    {
        var asker = Asker;
        var now = Core.Now;

        if (asker == null || character == null || !People.IsHuman(speaker) ||
            _asked.IsCooling(character.Serial, now, Cooldown))
        {
            return false;
        }

        var decision = Build(heard, noun, standing, side, PriceOptions(heard));

        if (!asker(new JevCall(character, speaker, BrainEventKind.Spoken, JevKind.Trade, decision, (answers, _) => onIntent(Read(answers, standing)))))
        {
            return false;
        }

        _asked.Mark(character.Serial, now);
        return true;
    }

    private static string Key(int price) => price.ToString(CultureInfo.InvariantCulture);

    private static void AddOnce(List<int> options, int value)
    {
        if (!options.Contains(value))
        {
            options.Add(value);
        }
    }
}
