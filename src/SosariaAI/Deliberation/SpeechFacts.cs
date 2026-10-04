using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Deliberation;

/// <summary>
/// A reply that claims a state the facts contradict is discarded, like a repeat.
/// </summary>
public static class SpeechFacts
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    /// <summary>
    /// Words that set a group's pace: only a leader with people behind it says them. A red
    /// camping Destard told a player "keep up, i dont wait long" and never left.
    /// </summary>
    private static readonly Regex LeadingWords = new(
        string.Join(
            '|',
            @"(^|[,.!;]\s*)(keep up|stay close|stick close|keep pace)\b",
            @"\bi (dont|don't|wont|won't) wait\b"
        ),
        Options
    );

    /// <summary>The step kinds whose whole work is getting somewhere: a walk, a cast, a ride, a sail, a follow.</summary>
    private static readonly HashSet<string> TripKinds = new(StringComparer.Ordinal)
    {
        SkillKinds.GoTo, SkillKinds.Travel, SkillKinds.GoHome, SkillKinds.Recall, SkillKinds.Gate,
        SkillKinds.Follow, SkillKinds.Boat, CorpseRunSkill.SkillName, HouseRetreatSkill.SkillName
    };

    public sealed class Snapshot
    {
        public int PackCount { get; init; }

        public int Gold { get; init; }

        public int Hits { get; init; }

        public int HitsMax { get; init; }

        public bool IsAlive { get; init; }

        /// <summary>
        /// True when a real group stands behind an invite: the speaker is in an engine party, or
        /// leads or gathers a group call, a road group, a crew or a red gang's muster.
        /// </summary>
        public bool LeadsGroup { get; init; }

        /// <summary>True when the step running now really takes the speaker somewhere.</summary>
        public bool Travelling { get; init; }
    }

    public static bool Contradicts(string say, Snapshot facts)
    {
        if (string.IsNullOrWhiteSpace(say) || facts == null)
        {
            return false;
        }

        if (facts.PackCount <= 0 && ClaimsFullLoad(say))
        {
            return true;
        }

        if (facts.Gold <= 0 && ClaimsRich(say))
        {
            return true;
        }

        if (facts.HitsMax > 0 && facts.Hits >= facts.HitsMax && ClaimsDying(say))
        {
            return true;
        }

        if (facts.IsAlive && (ClaimsDead(say) || ClaimsNeedsRez(say)))
        {
            return true;
        }

        if (!facts.LeadsGroup && (PromiseLines.IsInvite(say) || ClaimsLeading(say)))
        {
            return true;
        }

        if (!facts.Travelling && PromiseLines.ClaimsTrip(say))
        {
            return true;
        }

        return false;
    }

    /// <summary>The speaker sets a group's pace: "keep up", "stay close", "i dont wait".</summary>
    public static bool ClaimsLeading(string say) =>
        !string.IsNullOrWhiteSpace(say) && LeadingWords.IsMatch(say);

    /// <summary>True for a step kind whose whole work is getting somewhere (<see cref="Snapshot.Travelling"/>).</summary>
    public static bool IsTripKind(string skillKind) =>
        !string.IsNullOrEmpty(skillKind) && TripKinds.Contains(skillKind);

    public static bool ClaimsFullLoad(string say) =>
        ContainsAny(say, "full load", "nearly a full", "near a full", "pack is full", "almost full");

    public static bool ClaimsRich(string say) =>
        ContainsAny(say, "pockets of gold", "heavy with gold", "rich today");

    public static bool ClaimsDying(string say) =>
        ContainsAny(say, "i am dying", "i'm dying", "bleeding out");

    /// <summary>The speaker claims to be dead, a ghost, or looted by death — only a ghost can say so.</summary>
    public static bool ClaimsDead(string say) =>
        ContainsAny(
            say,
            "im dead", "i am dead", "i'm dead", "i died", "died again", "im a ghost",
            "i am a ghost", "i'm a ghost", "my corpse", "lost my armor", "lost my stuff",
            "lost my gear", "lost my weps", "lost my sword", "im naked");

    /// <summary>The speaker asks to be resurrected — only the dead need one.</summary>
    public static bool ClaimsNeedsRez(string say) =>
        ContainsAny(
            say,
            "rez pl", "rez please", "res pl", "res please", "need a rez", "need a res",
            "need rez", "rez me", "res me", "anyone rez", "someone rez", "any rez",
            "can i get a rez", "get a rez", "ress me");

    /// <summary>Words that claim a state or a deal a line said out of context cannot back.</summary>
    private static readonly string[] UnbackedClaims =
        ["wts", "wtb", "selling ", "buying ", "afk", "brb", "rez", "dead", "died", "corpse", "ghost"];

    /// <summary>
    /// True when a written persona line claims nothing the speaker cannot back — no trade
    /// shout, no away flag, no death talk, no invite, no trip and no group pace: a written line
    /// has no group or trip behind it. Questions pass: a reply may ask them.
    /// </summary>
    public static bool ClaimsFree(string say) =>
        !string.IsNullOrWhiteSpace(say) && !ContainsAny(say, UnbackedClaims) &&
        !PromiseLines.IsPromise(say) && !ClaimsLeading(say);

    /// <summary>
    /// True when a line can be said unprovoked — no question for a room that will not answer,
    /// and nothing <see cref="ClaimsFree"/> rejects. Written persona lines carry situations
    /// on paper; an ambient mutter has no situation.
    /// </summary>
    public static bool AmbientSafe(string say) =>
        ClaimsFree(say) && !say.Contains('?');

    private static bool ContainsAny(string say, params string[] phrases)
    {
        for (var i = 0; i < phrases.Length; i++)
        {
            if (say.Contains(phrases[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
