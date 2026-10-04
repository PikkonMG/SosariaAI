using System;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Memory;

namespace SosariaAI.Behaviour;

/// <summary>
/// Ask out loud first. Only send a real party invite after a spoken yes. A player is asked
/// rarely: only by a group leader calling near an idle player of like power, never twice in
/// a short while by anyone, and never again by one the player turned down.
/// </summary>
public static class InviteAskRules
{
    public const string AskLog = "asked";
    public const string AnswerLog = "answered";
    public const string InviteLog = "invited";

    /// <summary>Rolled once per group call with a fitting player close by.</summary>
    public const int AskPlayerPercent = 35;

    /// <summary>A player stood still this long, out of a fight, is free to be asked.</summary>
    public static readonly TimeSpan PlayerIdleFor = TimeSpan.FromSeconds(5);

    /// <summary>Nobody asks the same player again sooner than this.</summary>
    public static readonly TimeSpan PlayerAskGap = TimeSpan.FromMinutes(20);

    // Multi-word entries match as a phrase. Both spellings of let's are listed.
    private static readonly string[] YesWords =
    [
        "yes", "yeah", "yep", "y", "yea", "ya", "sure", "ok", "okay", "aye", "yup",
        "alright", "fine", "of course", "lets", "let's"
    ];

    private static readonly string[] NoWords =
    [
        "no", "nope", "nah", "pass", "busy"
    ];

    /// <summary>A line with a no-word is never a yes, so "no thanks" declines.</summary>
    public static bool IsYes(string text) => !IsNo(text) && SpeechIntent.ContainsPhrase(text, YesWords);

    public static bool IsNo(string text) => SpeechIntent.ContainsPhrase(text, NoWords);

    public static bool MayInviteFighter(bool hasWeapon, int power, int huntDifficulty) =>
        hasWeapon && huntDifficulty > 0 && power >= huntDifficulty;

    public static bool AnswerTimedOut(DateTime askedAt, DateTime now, int answerSeconds)
    {
        if (askedAt == default)
        {
            return false;
        }

        var seconds = answerSeconds > 0 ? answerSeconds : CareerSettings.DefaultInviteAnswerSeconds;
        return now - askedAt >= TimeSpan.FromSeconds(seconds);
    }

    /// <summary>A player standing about: not fighting, not in war mode, not walking.</summary>
    public static bool PlayerIdle(bool fighting, bool warmode, TimeSpan sinceMoved) =>
        !fighting && !warmode && sinceMoved >= PlayerIdleFor;

    /// <summary>Anyone's last ask of this player is far enough back.</summary>
    public static bool PlayerAskRested(DateTime lastAsked, DateTime now) =>
        TimeRules.Rested(lastAsked, now, PlayerAskGap);

    /// <summary>Whether a group leader asks a nearby player along: rare and only when it fits.</summary>
    public static bool MayAskPlayer(bool playerIdle, bool powerFits, bool declinedBefore, bool rested, int roll100) =>
        playerIdle && powerFits && !declinedBefore && rested && roll100 >= 0 && roll100 < AskPlayerPercent;

    /// <summary>
    /// What a leader keeps of a player who said no: the reason on its bond to that player. The
    /// bond outlives a restart; a later deed between the two (a run together, a heal) replaces
    /// the reason, and the leader may ask again.
    /// </summary>
    public const string DeclineReason = "turned down my group";

    /// <summary>A no leaves the leader's feeling for the player as it was; only the reason is kept.</summary>
    public const int DeclineBondShift = 0;

    /// <summary>True when the leader's bond to the player still holds the no.</summary>
    public static bool RemembersDecline(Bond bond) =>
        bond != null && string.Equals(bond.LastReason, DeclineReason, StringComparison.Ordinal);

    public static string LogAsk(string name, string playerName) =>
        $"{name} {AskLog} {playerName} to party";

    public static string LogAnswer(string playerName, bool yes) =>
        $"{playerName} {AnswerLog} {(yes ? "yes" : "no")}";

    /// <summary>An ask that ran out unanswered. It is not a no, and it is not remembered as one.</summary>
    public static string LogNoAnswer(string name, string playerName) =>
        $"{playerName} gave {name} no answer";

    public static string LogInvite(string name, string playerName) =>
        $"{name} {InviteLog} {playerName} to party";
}
