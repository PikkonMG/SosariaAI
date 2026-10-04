using System.Text.RegularExpressions;

namespace SosariaAI.Configuration;

/// <summary>
/// Lines that promise something only a group, a trip or a meeting can keep: an invite to come
/// along, follow, join or meet, and a claim that the speaker (or the listener with it) is on the
/// way somewhere. A persona line said at random, or a model reply, keeps none of these: a red
/// camping Destard asked a player along to Minoc and never left, and a banker standing at the
/// Britain bank asked a player there whether they were heading to Britain too. Pure.
/// </summary>
public static class PromiseLines
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex Invite = new(
        string.Join(
            '|',
            @"(?<!hear )(?<!heard )\b(you|u|ya) (coming|comin)\b(?! from)",
            @"\bcoming with\b",
            @"\bcome (with|along|hunt|farm)\b",
            @"\btag along\b",
            @"\bjoin (me|us)\b",
            @"\b(walk|ride|hunt|run|go|travel|sail|farm|spar) with (me|us)\b",
            @"\bwith me to\b",
            @"(^|[,.!;]\s*)(with me|on me)\b",
            @"\b(stick|stay) with me\b",
            @"\bmeet me\b(?! (halfway|in the middle|at (\{price\}|\d)))",
            @"\bmeet (you|u) (at|in|by|there|later)\b",
            @"(^|[,.!;]\s*)follow me\b",
            @"\b(wanna|want to) come\b",
            @"\bwant in\b",
            @"\bneed company\b",
            @"\banyone (coming|joining|joins?|up for)\b",
            @"\banyone (wanna|want to) (hunt|come|go|join|farm|run|spar|help|walk|ride|group|party)\b",
            @"\bwho(s|'s| is)? coming\b",
            @"\bwho wants to (hunt|come|go|join|farm|run|spar)\b",
            @"\blet'?s (go (to|hunt|farm|kill)|hunt|head|ride|walk)\b"
        ),
        Options
    );

    // The speaker's or the listener's own trip: at the start of a clause or after a first or
    // second person word, so "pk heading to the bank" (someone else's) is no claim.
    private static readonly Regex Trip = new(
        string.Join(
            '|',
            @"(^|[,.!;]\s*|\{name\}\s*|\b(i|im|i'm|i am|i'll|ill|you|u|ya|we|then|and|now|just|me)\s+)(heading|headed|off to|on my way)\b",
            @"\bon my way to\b",
            @"\b(heading|headed|going)\b[^?.!,]*\btoo\b"
        ),
        Options
    );

    /// <summary>True for an invite to come along, follow, join or meet.</summary>
    public static bool IsInvite(string line) => !string.IsNullOrWhiteSpace(line) && Invite.IsMatch(line);

    /// <summary>True for a claim that the speaker, or the listener with it, is on the way somewhere.</summary>
    public static bool ClaimsTrip(string line) => !string.IsNullOrWhiteSpace(line) && Trip.IsMatch(line);

    /// <summary>True for a line that promises a group, a trip or a meeting.</summary>
    public static bool IsPromise(string line) => IsInvite(line) || ClaimsTrip(line);
}
