using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Server;

namespace SosariaAI.Memory;

/// <summary>
/// What long-term memory says, read from the RAM of a <see cref="MemoryStore"/>: the bond
/// between two people, the shared adventure worth telling, and the plain facts a prompt
/// carries. Reads only. Main game thread.
/// </summary>
public static class Recall
{
    public const string WarmTone = "warm";
    public const string ColdTone = "cold";
    public const string PlainTone = "plain";

    /// <summary>Shared adventures looked at, heaviest first, for one to tell.</summary>
    public const int TellableLooked = 8;

    /// <summary>Today and the day before: what "what I did today and yesterday" covers.</summary>
    public const int RecentDays = 1;

    private const int SameDay = 0;

    private const string TodayWord = "today";
    private const string YesterdayWord = "yesterday";
    private const string DaysAgoFormat = "{0} days ago";
    private const string WhenSeparator = ": ";
    private const string AtWord = " at ";
    private const string WithWord = ", with ";
    private const string AgainstWord = ", against ";
    private const string NameSeparator = ", ";

    /// <summary>The long-term memory id of a mobile, or null for a creature.</summary>
    public static string IdOf(Mobile mobile) => PersonRef.Of(mobile)?.Id;

    /// <summary>How the owner feels about the other mobile, or null when they never met.</summary>
    public static Bond BondOf(MemoryStore store, Mobile owner, Mobile other) =>
        store?.BondOf(IdOf(owner), IdOf(other));

    /// <summary>The owner's bond score toward the other mobile; a stranger is neutral.</summary>
    public static int ScoreOf(MemoryStore store, Mobile owner, Mobile other) =>
        BondOf(store, owner, other)?.Score ?? BondRules.NeutralScore;

    /// <summary>The tone a bond score gives a greeting: warm, cold, or plain.</summary>
    public static string Tone(int score) =>
        BondRules.IsWarm(score) ? WarmTone
        : BondRules.IsCold(score) ? ColdTone
        : PlainTone;

    /// <summary>The last known name of a person, or null.</summary>
    public static string NameOf(MemoryStore store, string personId) => store?.PersonOf(personId)?.Name;

    /// <summary>
    /// The adventure the teller brings up with the listener: the heaviest, then newest, one they
    /// shared on the same side. Null when they share none.
    /// </summary>
    public static Adventure Tellable(MemoryStore store, string tellerId, string listenerId)
    {
        if (store == null || tellerId == null || listenerId == null)
        {
            return null;
        }

        var shared = store.SharedWith(tellerId, listenerId, TellableLooked);

        for (var i = 0; i < shared.Count; i++)
        {
            if (shared[i].MemberOf(tellerId) is { } teller &&
                shared[i].MemberOf(listenerId) is { } listener &&
                AdventureRoles.SameSide(teller.Role, listener.Role))
            {
                return shared[i];
            }
        }

        return null;
    }

    /// <summary>Every bond this person holds, warmest first.</summary>
    public static IReadOnlyList<Bond> BondsOf(MemoryStore store, string personId) => store?.BondsOf(personId) ?? [];

    /// <summary>The adventures RAM keeps for this person, newest first.</summary>
    public static IReadOnlyList<Adventure> AdventuresOf(MemoryStore store, string personId) => store?.AdventuresOf(personId) ?? [];

    /// <summary>
    /// What this person did today and yesterday, oldest first, at most <paramref name="max"/>
    /// of the newest.
    /// </summary>
    public static IReadOnlyList<string> Days(MemoryStore store, string personId, DateTime now, int max) =>
        Latest(store, personId, now, max, now.Date.AddDays(-RecentDays));

    /// <summary>
    /// This person's newest adventures that ended at or after <paramref name="since"/> (any
    /// time when left out), oldest first, at most <paramref name="max"/>.
    /// </summary>
    public static IReadOnlyList<string> Latest(MemoryStore store, string personId, DateTime now, int max, DateTime since = default)
    {
        var newest = AdventuresOf(store, personId);
        var facts = new List<string>();

        for (var i = 0; i < newest.Count && facts.Count < max && newest[i].EndedAt >= since; i++)
        {
            facts.Add(Fact(newest[i], personId, now));
        }

        facts.Reverse();
        return facts;
    }

    /// <summary>The biggest adventures this person shared with the other, biggest first, at most <paramref name="max"/>.</summary>
    public static IReadOnlyList<string> SharedFacts(MemoryStore store, string personId, string otherId, DateTime now, int max)
    {
        if (store == null || otherId == null || max <= 0)
        {
            return [];
        }

        var shared = store.SharedWith(personId, otherId, max);
        var facts = new List<string>(shared.Count);

        for (var i = 0; i < shared.Count; i++)
        {
            facts.Add(Fact(shared[i], personId, now));
        }

        return facts;
    }

    /// <summary>
    /// One adventure as a plain fact from the reader's side: when, what, where, who stood with
    /// the reader, and who stood against. Names and the place already in the summary are not
    /// said twice; the summary already tells the kills and deaths.
    /// </summary>
    public static string Fact(Adventure adventure, string readerId, DateTime now)
    {
        var what = adventure.Headline.Trim();
        var text = new StringBuilder();
        text.Append(WhenWord(adventure.EndedAt, now)).Append(WhenSeparator).Append(what);

        if (!string.IsNullOrWhiteSpace(adventure.Place) && !Mentions(what, adventure.Place))
        {
            text.Append(AtWord).Append(adventure.Place);
        }

        var readerRole = adventure.MemberOf(readerId)?.Role ?? AdventureRoles.With;
        var with = new List<string>();
        var against = new List<string>();

        for (var i = 0; i < adventure.Members.Count; i++)
        {
            var member = adventure.Members[i];
            var name = member.Person.Name;

            if (member.Person.Id == readerId || string.IsNullOrWhiteSpace(name) || Mentions(what, name))
            {
                continue;
            }

            (AdventureRoles.SameSide(readerRole, member.Role) ? with : against).Add(name);
        }

        if (with.Count > 0)
        {
            text.Append(WithWord).AppendJoin(NameSeparator, with);
        }

        if (against.Count > 0)
        {
            text.Append(AgainstWord).AppendJoin(NameSeparator, against);
        }

        return text.ToString();
    }

    /// <summary>"today", "yesterday", or "3 days ago", by calendar day.</summary>
    public static string WhenWord(DateTime at, DateTime now)
    {
        var days = Math.Max(SameDay, (int)(now.Date - at.Date).TotalDays);

        return days switch
        {
            SameDay => TodayWord,
            RecentDays => YesterdayWord,
            _ => string.Format(CultureInfo.InvariantCulture, DaysAgoFormat, days)
        };
    }

    private static bool Mentions(string text, string word) => text.Contains(word, StringComparison.OrdinalIgnoreCase);
}
