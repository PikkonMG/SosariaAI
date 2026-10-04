using System;

namespace SosariaAI.Memory;

/// <summary>How one person feels about another (the <c>bonds.score</c> column).</summary>
public static class BondRules
{
    public const int MinScore = -100;
    public const int MaxScore = 100;
    public const int WarmThreshold = 20;
    public const int ColdThreshold = -20;

    /// <summary>The score of a bond between people who just met.</summary>
    public const int NeutralScore = 0;

    /// <summary>Each adventure two people share on the same side warms the bond by this much.</summary>
    public const int SharedAdventureBonus = 3;

    /// <summary>Raised from the dead by the other.</summary>
    public const int HealBonus = 25;

    /// <summary>Killed by the other.</summary>
    public const int KillPenalty = 40;

    /// <summary>A haggle with the other fell through.</summary>
    public const int OutbidPenalty = 10;

    /// <summary>Greeted the other, or was greeted.</summary>
    public const int GreetBonus = 5;

    /// <summary>Chatted with the other.</summary>
    public const int ChatBonus = 1;

    public const string HealedReason = "healed me";
    public const string KilledReason = "killed me";
    public const string GreetedReason = "greeted";
    public const string ChattedReason = "chatted";

    public static int Clamp(int score) => Math.Clamp(score, MinScore, MaxScore);

    public static bool IsWarm(int score) => score >= WarmThreshold;

    public static bool IsCold(int score) => score <= ColdThreshold;
}
