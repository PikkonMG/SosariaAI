using System;
using SosariaAI.Deliberation;

namespace SosariaAI.Behaviour;

/// <summary>What a spoken line wants from whoever hears it.</summary>
public enum SpeechIntentKind
{
    /// <summary>Nothing the word lists recognise. A model may still classify it.</summary>
    Other,

    /// <summary>Just the listener's name: turn and answer.</summary>
    NameCall,
    Greeting,
    Goodbye,
    Question,

    /// <summary>Looking for a group: "lfg", "wanna group?".</summary>
    Party,

    /// <summary>A short yes to a group call: "me", "im in".</summary>
    Join,

    /// <summary>Buying, selling, prices. Trade speech has its own handler.</summary>
    Trade,
    Insult
}

/// <summary>
/// Free word-list reading of a line, the way a 1999 player skims chat. Runs for everyone;
/// the model only sees lines these lists cannot place.
/// </summary>
public static class SpeechIntent
{
    public const string WordGap = " ";

    /// <summary>A join answer is a word or three; a longer line is a sentence about something else.</summary>
    public const int JoinMaxWords = 3;

    private static readonly char[] WordBreaks = [' ', ',', '.', '!', '?', ';', ':', '(', ')', '"'];

    private static readonly string[] TradeWords =
    [
        "wts", "wtb", "wtt", "sell", "selling", "buy", "buying", "price", "how much", "pc", "gp", "trade", "trading"
    ];

    private static readonly string[] PartyWords =
    [
        "lfg", "lfm", "wanna group", "want to group", "wanna party", "want to party", "anyone hunt",
        "anyone wanna hunt", "anyone want to hunt", "need a group", "looking for group", "group up", "party up",
        "join me", "who wants to hunt"
    ];

    private static readonly string[] JoinWords =
    [
        "me", "me too", "im in", "i'm in", "count me in", "i'll go", "ill go", "i will go", "sure", "yes", "ya",
        "yeah", "ok", "k", "inv", "inv me", "invite me"
    ];

    private static readonly string[] InsultWords =
    [
        "noob", "newb", "n00b", "idiot", "stupid", "loser", "moron", "shut up", "stfu", "fu", "f u", "suck",
        "sucks", "ugly", "dumb", "lame"
    ];

    private static readonly string[] GoodbyeWords =
    [
        "bye", "bye all", "cya", "see ya", "see you", "later", "laters", "gtg", "g2g", "gn", "good night",
        "night all", "farewell", "take care"
    ];

    private static readonly string[] GreetingWords =
    [
        "hi", "hello", "hey", "heya", "hiya", "hail", "yo", "sup", "greetings", "howdy", "o/", "good morning",
        "good evening", "good day", "hi all", "hey all", "hello all", "wb"
    ];

    private static readonly string[] QuestionWords =
    [
        "what", "where", "who", "why", "how", "when", "which", "wat", "wut", "wher", "anyone know", "does anyone",
        "can someone", "can u", "can you", "do you", "do u", "is there"
    ];

    private static readonly string[] DoingWords =
    [
        "what are you doing", "what r u doing", "wat r u doing", "what u doing", "wat u doing", "wyd",
        "whatcha doing", "what are u doing", "what u up to", "what are you up to", "what is everyone doing",
        "what's everyone doing", "whats everyone doing", "wat is everyone doing", "what everyone doing",
        "where is everyone", "where's everyone", "wheres everyone", "what are you all doing", "what u guys doing",
        "what are you guys doing"
    ];

    /// <summary>
    /// The words a player asks the way with, longest first: the place follows them, as in
    /// "how do i get to the britain bank" or "anyone know where trinsic is".
    /// </summary>
    private static readonly string[] WayLeads =
    [
        "how do i get to", "how do u get to", "how do you get to", "how can i get to", "how would i get to",
        "how to get to", "how i get to", "how get to", "which way to", "which way is", "what way to",
        "which direction is", "what direction is", "how far is", "how far to", "where can i find",
        "where do i find", "where would i find", "anyone know where", "you know where", "u know where",
        "ya know where", "where is", "where's", "wheres"
    ];

    /// <summary>
    /// Way words that ask only in a question: "whats the way to yew?" asks, "on the way to yew"
    /// does not.
    /// </summary>
    private static readonly string[] WayLeadsInQuestion = ["the way to", "directions to", "direction to"];

    /// <summary>
    /// A way lead followed by one of these words asks after a person or a thing, not a place:
    /// "where is he", "where is my horse". The answer is never a direction.
    /// </summary>
    private static readonly string[] NotPlaceWords =
    [
        "he", "she", "it", "they", "you", "u", "ya", "ur", "your", "my", "his", "her", "their", "our",
        "this", "that", "everyone", "everybody", "everyone's", "every1"
    ];

    public static SpeechIntentKind Classify(string text, string listenerName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SpeechIntentKind.Other;
        }

        if (AttentionGate.IsOnlyName(text, listenerName))
        {
            return SpeechIntentKind.NameCall;
        }

        if (ContainsPhrase(text, TradeWords))
        {
            return SpeechIntentKind.Trade;
        }

        if (ContainsPhrase(text, PartyWords))
        {
            return SpeechIntentKind.Party;
        }

        if (IsJoin(text))
        {
            return SpeechIntentKind.Join;
        }

        if (ContainsPhrase(text, InsultWords))
        {
            return SpeechIntentKind.Insult;
        }

        // "hi bob, where is trinsic" asks the way; the greeting is the smaller part.
        if (AskedPlace(text) != null)
        {
            return SpeechIntentKind.Question;
        }

        if (ContainsPhrase(text, GoodbyeWords))
        {
            return SpeechIntentKind.Goodbye;
        }

        if (ContainsPhrase(text, GreetingWords))
        {
            return SpeechIntentKind.Greeting;
        }

        if (AsksSomething(text))
        {
            return SpeechIntentKind.Question;
        }

        return SpeechIntentKind.Other;
    }

    /// <summary>The line is a question: it ends in "?" or holds a question word.</summary>
    public static bool AsksSomething(string text) =>
        !string.IsNullOrWhiteSpace(text) && (text.TrimEnd().EndsWith('?') || ContainsPhrase(text, QuestionWords));

    /// <summary>"what are you doing" and its kin: answered from what the listener really does.</summary>
    public static bool AsksWhatDoing(string text) => ContainsPhrase(text, DoingWords);

    /// <summary>
    /// The words naming the place a line asks the way to, or null when it asks no way: "where is
    /// trinsic?" gives "trinsic", "how do i get to the britain bank" gives "the britain bank".
    /// "where is everyone" asks what people do, and "where is he" asks after a person.
    /// </summary>
    public static string AskedPlace(string text)
    {
        var after = After(text, WayLeads) ?? (AsksSomething(text) ? After(text, WayLeadsInQuestion) : null);

        if (AsksWhatDoing(text) || after is not { Length: > 0 } place)
        {
            return null;
        }

        var space = place.IndexOf(WordGap, StringComparison.Ordinal);
        var first = space < 0 ? place : place[..space];
        return Array.IndexOf(NotPlaceWords, first) >= 0 ? null : place;
    }

    /// <summary>
    /// The spoken words after the first of <paramref name="phrases"/> the line holds, in list
    /// order, or null when it holds none. Whole words only, lower case.
    /// </summary>
    public static string After(string text, string[] phrases)
    {
        var spoken = Spoken(text);

        foreach (var phrase in phrases)
        {
            var key = WordGap + phrase + WordGap;
            var at = spoken.IndexOf(key, StringComparison.Ordinal);

            if (at >= 0)
            {
                return spoken[(at + key.Length)..].Trim();
            }
        }

        return null;
    }

    /// <summary>A short yes that answers a group call.</summary>
    public static bool IsJoin(string text) =>
        WordCount(text) <= JoinMaxWords && !InviteAskRules.IsNo(text) && ContainsPhrase(text, JoinWords);

    public static bool IsPartyCall(string text) => ContainsPhrase(text, PartyWords);

    /// <summary>
    /// True when any phrase appears as whole words. Multi-word entries match as a phrase and
    /// punctuation between words does not matter.
    /// </summary>
    public static bool ContainsPhrase(string text, string[] phrases)
    {
        if (string.IsNullOrWhiteSpace(text) || phrases == null)
        {
            return false;
        }

        var spoken = Spoken(text);

        for (var i = 0; i < phrases.Length; i++)
        {
            if (spoken.Contains(WordGap + phrases[i] + WordGap, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The line as spoken words, lower case, one gap between them and one at each end.</summary>
    public static string Spoken(string text) =>
        string.IsNullOrWhiteSpace(text) ? WordGap : WordGap + string.Join(WordGap, Words(text)).ToLowerInvariant() + WordGap;

    private static int WordCount(string text) => string.IsNullOrWhiteSpace(text) ? 0 : Words(text).Length;

    private static string[] Words(string text) => text.Split(WordBreaks, StringSplitOptions.RemoveEmptyEntries);
}
