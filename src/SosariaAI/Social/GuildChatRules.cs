using System;

namespace SosariaAI.Social;

/// <summary>
/// Guild chat cadence. The lines live in the talk library. Characters talk in guild chat only while a player of that
/// guild is online to read it. Free. No model.
/// </summary>
public static class GuildChatRules
{
    public static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ChatterGapMin = TimeSpan.FromMinutes(4);
    public static readonly TimeSpan ChatterGapMax = TimeSpan.FromMinutes(9);

    /// <summary>A guildmate's answer comes after this, plus a step for each one before it.</summary>
    public static readonly TimeSpan AnswerDelay = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan AnswerStep = TimeSpan.FromSeconds(3);

    /// <summary>A player's "me" in guild chat answers a character's call this long after it.</summary>
    public static readonly TimeSpan OpenCallWindow = TimeSpan.FromMinutes(2);

    public const int DoingAnswers = 3;
    public const int RallyAnswers = 2;
    public const int GreetAnswers = 2;
    public const int GreetAnswerPercent = 60;
    public const int WelcomePercent = 70;
    public const int AskGroupPercent = 25;
    public const int GossipPercent = 35;
    public const int CantComePercent = 50;

    /// <summary>A guildmate travelling to the asker stops this close.</summary>
    public const int ArriveRange = 3;

    public static bool ChatterDue(DateTime next, DateTime now) => next == default || now >= next;

    /// <summary>The next chatter slot: somewhere in the gap, by the roll.</summary>
    public static DateTime NextChatter(DateTime now, int roll100)
    {
        var span = ChatterGapMax - ChatterGapMin;
        var share = Math.Clamp(roll100, 0, 100) / 100.0;
        return now + ChatterGapMin + TimeSpan.FromTicks((long)(span.Ticks * share));
    }

    public static TimeSpan DelayFor(int order) => AnswerDelay + AnswerStep * Math.Max(0, order);

    public static bool CallStillOpen(DateTime openedAt, DateTime now) =>
        openedAt != default && now - openedAt <= OpenCallWindow;
}
