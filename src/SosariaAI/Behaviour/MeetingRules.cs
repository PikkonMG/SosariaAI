using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Bank steps packed twenty Sosaria characters into greeting range. Each unique
/// pair said hello once, so the chat filled with Morning/Evening in one minute.
/// </summary>
public static class MeetingRules
{
    // Two tiles matched the old bank piles. With home corners spread out, neighbours
    // standing near enough to notice each other sit a few tiles apart.
    public const int GreetingRange = 4;
    public const int CrowdQuietCount = 3;
    public const int CrowdGreetChancePercent = 10;
    public const int BankQuietRange = 16;
    public const int PadQuietRange = 8;
    public const int BankGreetChancePercent = 5;
    public const string BankArea = "bank";
    public static readonly TimeSpan SpeakerQuiet = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan BankGap = TimeSpan.FromSeconds(20);

    // Small talk while lingering next to someone: a visit with a friend or a spell
    // at the tavern. Scripted lines only; these cadences bound the chatter per stay.
    public const int ChatRange = 6;
    public const int ChatMaxTurns = 4;
    public const int ChatReplyChancePercent = 65;
    public const int ChatGossipChancePercent = 35;
    public const int GreetReplyChancePercent = 70;

    /// <summary>Old friends bring up an adventure they shared this often when they meet.</summary>
    public const int OldFriendRecallPercent = 60;

    /// <summary>Small talk between old friends turns to an adventure they shared this often.</summary>
    public const int ChatRecallPercent = 25;

    /// <summary>
    /// How often an answer comes from the listener's own written lines instead of the shared
    /// pools: the same stock acks from every mouth read as one script.
    /// </summary>
    public const int PersonaReplyPercent = 45;

    /// <summary>Someone else standing by answers a told story this often.</summary>
    public const int BystanderReplyPercent = 30;
    public static readonly TimeSpan FirstChatDelay = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan ChatGap = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan ChatPairQuiet = TimeSpan.FromSeconds(45);

    public static bool InBankQuiet(Point3D at, Point3D bank) =>
        NavMetric.Chebyshev(at, bank) <= BankQuietRange;

    public static bool NearPad(Point3D at, Point3D pad) =>
        NavMetric.Chebyshev(at, pad) <= PadQuietRange;

    public static bool InQuietPlace(Point3D at, Point3D bank, IReadOnlyList<Point3D> pads)
    {
        if (InBankQuiet(at, bank))
        {
            return true;
        }

        if (pads == null)
        {
            return false;
        }

        for (var i = 0; i < pads.Count; i++)
        {
            if (NearPad(at, pads[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static bool InChatRange(Point3D a, Point3D b) =>
        NavMetric.Chebyshev(a, b) <= ChatRange;

    public static bool SameWornName(string speaker, string other) =>
        !string.IsNullOrWhiteSpace(speaker) &&
        speaker.Equals(other, StringComparison.OrdinalIgnoreCase);

    public static bool MayGreetCrowd(int nearbyCount, bool atQuiet, int roll100)
    {
        if (atQuiet)
        {
            return roll100 >= 0 && roll100 < BankGreetChancePercent;
        }

        if (nearbyCount >= CrowdQuietCount)
        {
            return roll100 >= 0 && roll100 < CrowdGreetChancePercent;
        }

        return true;
    }

    public static int ChanceRoll(int seed)
    {
        var value = seed % 100;
        return value < 0 ? value + 100 : value;
    }
}
