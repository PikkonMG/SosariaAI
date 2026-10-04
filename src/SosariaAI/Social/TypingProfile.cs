using SosariaAI.Spawning;

namespace SosariaAI.Social;

/// <summary>How a person's hands treat letter case.</summary>
public enum TypingCase
{
    /// <summary>Everything in lower case.</summary>
    Lower,

    /// <summary>Case as the line was written.</summary>
    Normal,

    /// <summary>Lower case, with a word now and then in capitals for weight.</summary>
    RareCaps
}

/// <summary>What a person does at the end of a line.</summary>
public enum TypingEnd
{
    Keep,

    /// <summary>No full stop; one ! or ? at most.</summary>
    Drop,

    /// <summary>!! and ??, and no full stop.</summary>
    Double,

    /// <summary>A full stop trails off as "...".</summary>
    Trail
}

/// <summary>
/// One person's typing habits, rolled from the character id, so the same person types the
/// same way after every reboot. <see cref="TypingStyle"/> applies it to each line.
/// </summary>
public sealed record TypingProfile(
    TypingCase Case,
    int AbbreviationLevel,
    string ThanksWord,
    TypingEnd End,
    int TypoPercent,
    string Tail,
    int TailPercent,
    int EmphasisPercent
)
{
    public const int NoAbbreviations = 0;
    public const int HeavyAbbreviations = 2;

    public const string ThanksShort = "ty";
    public const string ThanksClipped = "thx";
    public const string TailLol = "lol";
    public const string TailHeh = "heh";
    public const string TailLmao = "lmao";

    public const int LowSloppyTypoPercent = 3;
    public const int SloppyTypoPercent = 6;
    public const int MinTailPercent = 8;
    public const int TailPercentSpan = 12;
    public const int EmphasisHabitPercent = 15;

    private const int CaseSalt = 401;
    private const int AbbreviationSalt = 409;
    private const int ThanksSalt = 419;
    private const int EndSalt = 421;
    private const int TypoSalt = 431;
    private const int TailSalt = 433;
    private const int TailRateSalt = 439;
    private const int EmphasisSalt = 443;

    // Weights in enum or table order. Most 1999 players typed lower case and left off the full stop.
    private static readonly int[] CaseWeights = [55, 30, 15];
    private static readonly int[] AbbreviationWeights = [35, 40, 25];
    private static readonly int[] ThanksWeights = [60, 40];
    private static readonly int[] EndWeights = [25, 45, 15, 15];
    private static readonly int[] TypoWeights = [50, 35, 15];
    private static readonly int[] TailWeights = [45, 30, 15, 10];
    private static readonly int[] EmphasisWeights = [60, 40];

    private static readonly string[] ThanksWords = [ThanksShort, ThanksClipped];
    private static readonly int[] TypoRates = [0, LowSloppyTypoPercent, SloppyTypoPercent];
    private static readonly string[] Tails = [null, TailLol, TailHeh, TailLmao];
    private static readonly int[] EmphasisRates = [0, EmphasisHabitPercent];

    /// <summary>Lines exactly as written. Used before a character has an id.</summary>
    public static TypingProfile Plain { get; } = new(TypingCase.Normal, NoAbbreviations, ThanksShort, TypingEnd.Keep, 0, null, 0, 0);

    public static TypingProfile For(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return Plain;
        }

        var tail = Tails[PersonDice.Weighted(characterId, TailSalt, TailWeights)];

        return new TypingProfile(
            (TypingCase)PersonDice.Weighted(characterId, CaseSalt, CaseWeights),
            PersonDice.Weighted(characterId, AbbreviationSalt, AbbreviationWeights),
            ThanksWords[PersonDice.Weighted(characterId, ThanksSalt, ThanksWeights)],
            (TypingEnd)PersonDice.Weighted(characterId, EndSalt, EndWeights),
            TypoRates[PersonDice.Weighted(characterId, TypoSalt, TypoWeights)],
            tail,
            tail == null ? 0 : MinTailPercent + PersonDice.Roll(characterId, TailRateSalt, TailPercentSpan),
            EmphasisRates[PersonDice.Weighted(characterId, EmphasisSalt, EmphasisWeights)]
        );
    }
}
