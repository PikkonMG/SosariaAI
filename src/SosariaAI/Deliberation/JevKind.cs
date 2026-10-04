using System;
using System.Collections.Generic;

namespace SosariaAI.Deliberation;

/// <summary>What a Jev call is for. The hourly usage line splits by it, and the budget keeps only some kinds once the hour runs short.</summary>
public enum JevKind
{
    /// <summary>A routine next job, asked only when jevScope is "all".</summary>
    Decision,

    /// <summary>A job or event decision at a big moment, or a player's own words to read.</summary>
    BigMoment,

    /// <summary>A fight stance against a person.</summary>
    PersonFight,

    /// <summary>A fight stance against a monster.</summary>
    MonsterFight,

    /// <summary>A haggle line from a player that fits no trade rule.</summary>
    Trade,

    /// <summary>One character hearing another: is the line worth a reply.</summary>
    SpeechGate
}

public static class SystemOneRoute
{
    public static string Key(JevKind kind) => kind switch
    {
        JevKind.Decision => "decision",
        JevKind.BigMoment => "bigMoment",
        JevKind.PersonFight => "personFight",
        JevKind.MonsterFight => "monsterFight",
        JevKind.Trade => "trade",
        JevKind.SpeechGate => "speechGate",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>
/// One budget for every Jev call. All kinds count against the hourly input token cap
/// (brain.json jevInputTokensPerHour); none of them spends the paid call budget. Past
/// <see cref="TightShare"/> of the hour's tokens only the calls that matter most go out: a
/// fight with a person, a big moment, and a player's haggle words. At the cap nothing goes
/// out until the next clock hour. Pure.
/// </summary>
public static class JevBudgetRules
{
    /// <summary>Past this share of the hour's tokens the budget keeps only the kinds that matter most.</summary>
    public const double TightShare = 0.8;

    public const double FullShare = 1.0;

    /// <summary>The words the usage line gives each kind.</summary>
    public static readonly IReadOnlyDictionary<JevKind, string> Names = new Dictionary<JevKind, string>
    {
        [JevKind.Decision] = "decision",
        [JevKind.BigMoment] = "big moment",
        [JevKind.PersonFight] = "stance vs person",
        [JevKind.MonsterFight] = "stance vs monster",
        [JevKind.Trade] = "trade",
        [JevKind.SpeechGate] = "speech gate"
    };

    public static int KindCount => Enum.GetValues<JevKind>().Length;

    /// <summary>True when a call of this kind may go out with this share of the hour's tokens used.</summary>
    public static bool Allows(JevKind kind, double usedShare) =>
        usedShare < TightShare || usedShare < FullShare && MattersMost(kind);

    public static bool MattersMost(JevKind kind) => kind is JevKind.PersonFight or JevKind.BigMoment or JevKind.Trade;

    /// <summary>The share of the hour's cap used; a cap of zero or less reads as spent.</summary>
    public static double UsedShare(long inputTokens, long cap) => cap <= 0 ? FullShare : (double)inputTokens / cap;

    /// <summary>The stance kind for a fight against this foe.</summary>
    public static JevKind FightKind(bool foeIsPerson) => foeIsPerson ? JevKind.PersonFight : JevKind.MonsterFight;
}
