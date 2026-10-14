using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>
/// The words of a party order or a help call, read the way a 1999 player skims party chat, and
/// the limits an order runs under. No model: a word list places every line, and a line the
/// lists do not place is ordinary talk. Free, so every member reads every line.
/// </summary>
public static class PartyOrderRules
{
    /// <summary>A member told to hold stands for this long at most, then follows again.</summary>
    public static readonly TimeSpan HoldLimit = TimeSpan.FromMinutes(5);

    /// <summary>A member sent back stays out of the fight for this long, then follows again.</summary>
    public static readonly TimeSpan BackHold = TimeSpan.FromSeconds(30);

    /// <summary>A leader waits for one member this long at most.</summary>
    public static readonly TimeSpan HelpWaitLimit = TimeSpan.FromMinutes(2);

    /// <summary>A bot leader gives the same order about the same person once in this gap.</summary>
    public static readonly TimeSpan LeaderQuiet = TimeSpan.FromSeconds(20);

    /// <summary>A follower that has not moved for this long while far from its leader is stuck.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromSeconds(30);

    /// <summary>Farther than this behind the leader is lagging: the leader waits, a walk to a place pauses.</summary>
    public const int LagTiles = PartyWaitRules.LagTiles;

    /// <summary>A member in a fight below this share of its hits is sent back.</summary>
    public const double BackBelowHitsFraction = 0.40;

    /// <summary>How close a member keeps to the leader under Follow, Regroup and Back.</summary>
    public const int CloseRange = SosariaCombat.FollowRangeMin;

    /// <summary>A leader that walks back for a stuck member stops this close to it.</summary>
    public const int WalkBackRange = LagTiles / 2;

    /// <summary>
    /// A wait ends once the member is this close: half the lag line, so a member walking at
    /// the line does not end and reopen the wait every few seconds.
    /// </summary>
    public const int CaughtUpTiles = LagTiles / 2;

    /// <summary>How often a member answers an order out loud in party chat.</summary>
    public const int AckPercent = 40;

    private static readonly string[] GoToLeads =
    [
        "lets go to", "let's go to", "go to", "head to", "take me to", "lead the way to", "lead me to", "come to", "goto"
    ];

    private static readonly string[] FollowWords = ["follow me", "on me", "come", "with me", "lets go", "let's go", "come on"];

    private static readonly string[] HoldWords = ["wait here", "hold here", "stay here", "hold", "stay", "stop", "wait"];

    private static readonly string[] AttackWords = ["attack", "kill it", "get it", "kill", "get him", "get her", "get them"];

    private static readonly string[] BackWords = ["fall back", "back off", "retreat", "pull back", "back up", "run away", "get back"];

    private static readonly string[] RegroupWords = ["regroup", "to me", "gather", "gather up", "form up"];

    private static readonly string[] HealWords = ["heal me", "need heal", "need a heal", "healz", "heals", "heal pls", "heal plz"];

    private static readonly string[] WaitForMeWords = ["wait for me", "wait up", "hold on", "wait for me pls"];

    private static readonly string[] StuckWords =
    [
        "stuck", "im stuck", "i'm stuck", "cant get there", "can't get there", "cant reach", "can't reach", "cant get through"
    ];

    /// <summary>The leading words a place is said with: "the britain bank", "to despise".</summary>
    private static readonly string[] PlaceLeads = ["to the ", "the ", "to "];

    private static readonly char[] PlaceTrim = [' ', '.', '!', '?', ',', ';', ':'];

    /// <summary>A place word shorter than this is noise: "go to it".</summary>
    private const int MinPlaceWordLength = 3;

    /// <summary>
    /// The order a line gives, and for a place or a target the words after the order. A go-to
    /// is read first: "lets go to despise" is a trip, not a follow.
    /// </summary>
    public static PartyOrderKind ReadOrder(string text, out string remainder)
    {
        remainder = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return PartyOrderKind.None;
        }

        if (SpeechIntent.After(text, GoToLeads) is { } place)
        {
            remainder = place;
            return PartyOrderKind.GoTo;
        }

        if (SpeechIntent.ContainsPhrase(text, RegroupWords))
        {
            return PartyOrderKind.Regroup;
        }

        if (SpeechIntent.ContainsPhrase(text, BackWords))
        {
            return PartyOrderKind.Back;
        }

        if (SpeechIntent.After(text, AttackWords) is { } target)
        {
            remainder = target;
            return PartyOrderKind.Attack;
        }

        if (SpeechIntent.ContainsPhrase(text, FollowWords))
        {
            return PartyOrderKind.Follow;
        }

        return SpeechIntent.ContainsPhrase(text, HoldWords) ? PartyOrderKind.Hold : PartyOrderKind.None;
    }

    /// <summary>The help a line asks for. Read before the orders: "wait for me" is a call, "wait" an order.</summary>
    public static PartyHelpKind ReadHelp(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return PartyHelpKind.None;
        }

        if (SpeechIntent.ContainsPhrase(text, HealWords))
        {
            return PartyHelpKind.HealMe;
        }

        if (SpeechIntent.ContainsPhrase(text, StuckWords))
        {
            return PartyHelpKind.Stuck;
        }

        return SpeechIntent.ContainsPhrase(text, WaitForMeWords) ? PartyHelpKind.WaitForMe : PartyHelpKind.None;
    }

    /// <summary>
    /// The names a place remainder may be looked up as, first the whole, then each word of it:
    /// "the britain bank" tries "britain bank", "britain" and "bank".
    /// </summary>
    public static IReadOnlyList<string> PlaceTokens(string remainder)
    {
        var tokens = new List<string>();
        var whole = PlaceToken(remainder);

        if (whole == null)
        {
            return tokens;
        }

        tokens.Add(whole);

        foreach (var word in whole.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length >= MinPlaceWordLength && !tokens.Contains(word))
            {
                tokens.Add(word);
            }
        }

        return tokens;
    }

    /// <summary>The place a remainder names, without its lead words and punctuation; null for none.</summary>
    public static string PlaceToken(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        var token = remainder.Trim(PlaceTrim).ToLowerInvariant();

        foreach (var lead in PlaceLeads)
        {
            if (token.StartsWith(lead, StringComparison.Ordinal))
            {
                token = token[lead.Length..].Trim(PlaceTrim);
                break;
            }
        }

        return token.Length == 0 ? null : token;
    }

    /// <summary>Only the party leader gives orders, and only a person: a bot leader orders in code.</summary>
    public static bool MayOrder(bool speakerLeads, bool speakerIsHuman) => speakerLeads && speakerIsHuman;

    /// <summary>Any person in the party may call for help.</summary>
    public static bool MayCallHelp(bool speakerInParty, bool speakerIsHuman) => speakerInParty && speakerIsHuman;

    /// <summary>A hold or a step back runs out; every other order ends on its own terms.</summary>
    public static bool Expired(PartyOrderKind kind, DateTime at, DateTime now) =>
        kind switch
        {
            PartyOrderKind.Hold => TimeRules.Passed(at, now, HoldLimit),
            PartyOrderKind.Back => TimeRules.Passed(at, now, BackHold),
            _ => false
        };

    /// <summary>Every order settles back to following the leader.</summary>
    public static PartyOrderKind AfterDone(PartyOrderKind kind) =>
        kind == PartyOrderKind.None ? PartyOrderKind.None : PartyOrderKind.Follow;

    /// <summary>A member on another map is not behind, it is gone; one on this map too far back lags.</summary>
    public static bool LagsBehind(bool sameMap, int distance) => sameMap && distance > LagTiles;

    /// <summary>A member sent on its own errand, a walk to a place or a hold, is not lagging where it is.</summary>
    public static bool CountsForLag(PartyOrderKind kind) => kind is not (PartyOrderKind.GoTo or PartyOrderKind.Hold);

    /// <summary>A member in a fight and badly hurt is sent back.</summary>
    public static bool NeedsBack(bool inFight, double hitsFraction) => inFight && hitsFraction < BackBelowHitsFraction;

    /// <summary>
    /// A member leaves for a heal errand only when it can heal, is out of a fight, and has no
    /// threat in sight it must run from: a hurt healer sent to a mate fled home at once, and the
    /// next scan sent it back, every two seconds for minutes.
    /// </summary>
    public static bool FreeToHeal(bool canHeal, bool inFight, bool mustRun) => canHeal && !inFight && !mustRun;

    /// <summary>A leader gives up waiting for one member after the limit.</summary>
    public static bool HelpWaitOver(DateTime since, DateTime now) => TimeRules.Passed(since, now, HelpWaitLimit);

    /// <summary>
    /// A leader that gave up on a member does not wait for the same one again until this rest
    /// passed: a wait that timed out opened again the same second, for good.
    /// </summary>
    public static bool WaitRested(DateTime gaveUpAt, DateTime now) => TimeRules.Rested(gaveUpAt, now, HelpWaitLimit);

    /// <summary>The wait for a member ends when it has caught up, is gone, or the limit passed.</summary>
    public static bool WaitEnds(bool callerPresent, bool sameMap, int distance, DateTime since, DateTime now) =>
        !callerPresent || (sameMap && distance <= CaughtUpTiles) || HelpWaitOver(since, now);

    /// <summary>A follower standing still this long, far from its leader, is stuck.</summary>
    public static bool IsStuck(DateTime stillSince, DateTime now) => TimeRules.Passed(stillSince, now, StuckAfter);

    /// <summary>A walk to a place pauses while the leader is far behind on the same map.</summary>
    public static bool PausesWalk(bool leaderPresent, bool sameMap, int distance) =>
        leaderPresent && LagsBehind(sameMap, distance);
}
