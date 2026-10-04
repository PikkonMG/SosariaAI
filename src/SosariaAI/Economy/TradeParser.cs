using System;
using System.Collections.Generic;
using System.Text;

namespace SosariaAI.Economy;

/// <summary>
/// The cheap layer: reads trade talk the way it was typed at a 1999 bank. Everyone runs it.
/// Vague words ("ok", "no", a bare number) only count from someone already haggling, or every
/// "sure" said near a bank would buy something. A line that smells of trade but fits no rule
/// comes back unsure, and only then may a typed model look at it. Pure.
/// </summary>
public static class TradeParser
{
    private const char Space = ' ';
    private const char Apostrophe = '\'';
    private const char RightQuote = '\u2019';
    private const char DecimalPoint = '.';
    private const char GroupSeparator = ',';

    // "s> GM hally" was shorthand for selling.
    private const char ShoutMark = '>';

    private static readonly string[] SellOpeners = ["wts", "selling", "s>"];

    private static readonly string[] HaveOnePhrases =
    [
        "i have one", "i have 1", "i have it", "i have some", "i got one", "i got 1", "i got some", "got one",
        "have one", "ive got one", "ive got some", "i have", "ive got", "i got"
    ];

    private static readonly string[] DeclinePhrases =
    [
        "nvm", "nevermind", "never mind", "too much", "too expensive", "too rich", "too high", "no thanks",
        "no thx", "no ty", "nah", "pass", "forget it", "no deal", "not interested", "im good", "no way"
    ];

    private static readonly string[] WeakDeclineWords = ["no", "nope", "n"];

    private static readonly string[] StrongAcceptPhrases =
    [
        "deal", "ill take it", "i will take it", "sold", "agreed", "done deal", "ill buy it", "its a deal",
        "you got a deal", "ill take", "ill buy", "i will buy", "i want it", "i want to buy", "id like to buy",
        "i would like to buy", "can i buy"
    ];

    private static readonly string[] WeakAcceptWords =
    [
        "ok", "k", "okay", "kk", "sure", "yes", "ya", "yeah", "yep", "fine", "alright", "done", "y"
    ];

    private static readonly string[] StockPhrases =
    [
        "what are you selling", "what are u selling", "what r u selling", "what you selling", "what u selling",
        "whatcha selling", "what u got", "what you got", "what ya got", "watcha got", "whatcha got",
        "what do you have", "what do u have", "what have you got", "whats for sale", "what are you hawking",
        "what u have", "wares", "what do you sell", "selling anything", "anything for sale", "what are you hawkin"
    ];

    private static readonly string[] PricePhrases =
    [
        "how much", "hm for", "price", "pc", "cost", "what do you want for", "what u want for", "for how much",
        "how many gold", "hm"
    ];

    private static readonly string[] OfferPhrases =
    [
        "ill give", "ill go", "ill pay", "i give", "i can give", "i can do", "how about", "would you take",
        "would you do", "will you take", "would u take", "would u do", "will u take", "take", "do", "for the",
        "for it", "offer", "best i can do", "can do", "meet me at", "ill do"
    ];

    private static readonly string[] TradeSmellWords =
    [
        "gold", "gp", "cheaper", "lower", "discount", "less", "more", "give", "pay", "sell", "buy", "price",
        "thousand", "hundred", "cheap", "expensive", "worth"
    ];

    /// <summary>
    /// Reads one line. <paramref name="listenerName"/> is stripped so "ulric how much" reads as
    /// "how much". <paramref name="engaged"/> is true when the speaker is already haggling with the
    /// listener; <paramref name="standing"/> is the last number the listener named (0 for none).
    /// </summary>
    public static TradeIntent Read(string text, string listenerName, bool engaged, int standing)
    {
        var words = Words(text, listenerName);

        if (words.Count == 0)
        {
            return TradeIntent.Nothing;
        }

        var padded = Padded(words);
        var numbers = GoldWords.NumbersIn(words);
        GoodsClaim? goods = Appraisal.TryRead(words, out var claim, out var nounAt) ? claim : null;
        var price = PriceIn(numbers, nounAt, standing);

        if (IsSellShout(words, padded))
        {
            return goods == null ? TradeIntent.Nothing : new TradeIntent(TradeIntentKind.Sell, price, goods, true);
        }

        if (HasAny(padded, DeclinePhrases) || engaged && words.Count <= 2 && IsAnyWord(words[0], WeakDeclineWords))
        {
            return new TradeIntent(TradeIntentKind.Decline, 0, goods, true);
        }

        if (HasAny(padded, HaveOnePhrases))
        {
            return new TradeIntent(TradeIntentKind.HaveOne, price, goods, true);
        }

        if (HasAny(padded, StrongAcceptPhrases) || engaged && StartsWithAny(words, WeakAcceptWords))
        {
            // "ok 3k?" is a number on the table, not a yes to the last one.
            return price > 0 && price != standing
                ? new TradeIntent(TradeIntentKind.Offer, price, goods, true)
                : new TradeIntent(TradeIntentKind.Accept, standing, goods, true);
        }

        if (HasAny(padded, StockPhrases))
        {
            return new TradeIntent(TradeIntentKind.AskStock, 0, goods, true);
        }

        if (price > 0 && (HasAny(padded, OfferPhrases) || engaged && IsOnlyNumber(words, numbers) ||
                          goods != null && engaged))
        {
            return new TradeIntent(TradeIntentKind.Offer, price, goods, true);
        }

        if (HasAny(padded, PricePhrases))
        {
            return new TradeIntent(TradeIntentKind.AskPrice, 0, goods, true);
        }

        return engaged && (numbers.Count > 0 || HasAnyWord(words, TradeSmellWords))
            ? TradeIntent.Unsure
            : TradeIntent.Nothing;
    }

    /// <summary>
    /// Lower-case words with punctuation gone. An apostrophe is dropped ("i'll" reads "ill"); a
    /// point or comma inside a number stays ("2.5k", "1,200"). The listener's name is left out.
    /// </summary>
    public static List<string> Words(string text, string listenerName = null)
    {
        var words = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        var builder = new StringBuilder(text.Length);
        var lower = text.ToLowerInvariant();

        for (var i = 0; i < lower.Length; i++)
        {
            var c = lower[i];

            if (c is Apostrophe or RightQuote)
            {
                continue;
            }

            var inNumber = c is DecimalPoint or GroupSeparator && i > 0 && i + 1 < lower.Length &&
                           char.IsDigit(lower[i - 1]) && char.IsDigit(lower[i + 1]);
            builder.Append(char.IsLetterOrDigit(c) || inNumber || c == ShoutMark ? c : Space);
        }

        var skip = NameWords(listenerName);

        foreach (var word in builder.ToString().Split(Space, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!skip.Contains(word))
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>
    /// The price in a line. Money-marked numbers win ("5k"); otherwise the last plain number that
    /// is not counting the goods ("wts 200 mandrake 900" is 900). A bare small number in a haggle
    /// over thousands means thousands.
    /// </summary>
    public static int PriceIn(IReadOnlyList<HeardNumber> numbers, int nounAt, int standing)
    {
        HeardNumber? pick = null;

        for (var i = 0; i < numbers.Count; i++)
        {
            var number = numbers[i];

            if (number.IsMoney)
            {
                pick = number;
            }
            else if (pick is not { IsMoney: true } && number.Position != nounAt - 1)
            {
                pick = number;
            }
        }

        return pick is { } found ? GoldWords.InContext(found, standing) : 0;
    }

    private static bool IsSellShout(List<string> words, string padded) =>
        IsAnyWord(words[0], SellOpeners) || padded.Contains(" wts ", StringComparison.Ordinal);

    private static bool IsOnlyNumber(List<string> words, List<HeardNumber> numbers) =>
        numbers.Count == 1 && words.Count == 1;

    private static string Padded(List<string> words) => $"{Space}{string.Join(Space, words)}{Space}";

    private static HashSet<string> NameWords(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? []
            : [..name.ToLowerInvariant().Split(Space, StringSplitOptions.RemoveEmptyEntries)];

    private static bool HasAny(string padded, string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (padded.Contains($"{Space}{phrase}{Space}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAny(List<string> words, string[] wanted) => IsAnyWord(words[0], wanted);

    private static bool HasAnyWord(List<string> words, string[] wanted)
    {
        foreach (var word in words)
        {
            if (IsAnyWord(word, wanted))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnyWord(string word, string[] wanted) => Array.IndexOf(wanted, word) >= 0;
}
