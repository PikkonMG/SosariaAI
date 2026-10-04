using System;
using System.Globalization;

namespace SosariaAI.Social;

/// <summary>
/// Gossip retells a real journal event in 1999 chat: lowercase, short, no roleplay. Each
/// event kind has its own lines, told in the first person when the teller took part.
/// Original lines for SosariaAI, not imported.
/// </summary>
public static class GossipLines
{
    private const string PlaceSeparator = ",";
    private const string MonsterWord = "a monster";

    public enum Role
    {
        Bystander,
        Actor,
        Other
    }

    private static readonly string[] PkHeard =
    [
        "{actor} got pked at {place} {when}!!",
        "{other} just pked {actor} at {place}",
        "heads up, {other} killed {actor} near {place}",
        "rip {actor}, {other} got em at {place} {when}",
        "{actor} got ganked by {other} at {place}",
        "stay away from {place}, {other} pked {actor} {when}",
        "{other} is killing ppl at {place}, got {actor}",
        "omg {actor} got pked at {place}"
    ];

    private static readonly string[] PkVague =
    [
        "heard someone got pked out at {place}",
        "heard {actor} got killed by a red near {place}",
        "reds around {place} i heard",
        "somebody said {actor} got pked out by {place}"
    ];

    private static readonly string[] PkRepeat =
    [
        "{other} again!! thats {count} kills",
        "{other} is on a spree, {count} dead now",
        "watch for {other}, {count} pks so far",
        "{other} got {actor} too, thats {count}"
    ];

    private static readonly string[] PkOwnVictim =
    [
        "{other} pked me at {place} {when} :(",
        "got pked by {other} at {place}, lost everything",
        "{other} killed me at {place}, anyone seen em?",
        "just got ganked at {place} by {other}",
        "{other} got me at {place} {when}. watch out"
    ];

    private static readonly string[] PkOwnKiller =
    [
        "lol got {actor} at {place}",
        "{actor} dropped fast at {place}",
        "{actor} had nothing on em lol",
        "easy kill at {place} {when}"
    ];

    private static readonly string[] DeathHeard =
    [
        "{actor} died at {place} {when}",
        "rip {actor}, died at {place}",
        "{actor} went down at {place} {when}",
        "{actor} got killed at {place}, poor guy",
        "{place} got {actor} {when}"
    ];

    private static readonly string[] DeathVague =
    [
        "heard {actor} died out by {place}",
        "someone died at {place} i heard",
        "heard {place} is rough, {actor} died there"
    ];

    private static readonly string[] DeathOwn =
    [
        "died at {place} {when} ugh",
        "just got back from {place}, died",
        "{place} killed me {when} lol",
        "died at {place}, lost my stuff",
        "ugh {place}. died there {when}"
    ];

    private static readonly string[] DeathKiller =
    [
        "killed {actor} at {place} {when}",
        "{actor} fell at {place}, that was me",
        "got {actor} at {place} {when}"
    ];

    private static readonly string[] TheftHeard =
    [
        "{actor} stole from {other} at {place} {when}",
        "watch ur packs at {place}, {actor} is stealing",
        "some thief {actor} robbed {other} at {place}",
        "{actor} is a thief, got {other} at {place}",
        "thief at {place}! {actor}"
    ];

    private static readonly string[] TheftVague =
    [
        "heard theres a thief working {place}",
        "heard someone got robbed at {place}"
    ];

    private static readonly string[] TheftVictim =
    [
        "{actor} stole from me at {place}!!",
        "{actor} robbed me at {place} {when}",
        "watch {actor}, thief, got me at {place}",
        "some thief {actor} took my stuff at {place}"
    ];

    private static readonly string[] WarHeard =
    [
        "{actor} and {other} guilds at war now",
        "war on! {actor} hit {other} at {place}",
        "{actor}'s guild vs {other}'s guild now lol",
        "guild war started at {place} {when}"
    ];

    private static readonly string[] WarVague =
    [
        "heard theres a guild war on",
        "heard some guilds went to war near {place}"
    ];

    private static readonly string[] WarOwn =
    [
        "hit {other} at {place} and now its war lol",
        "we're at war with {other}'s guild now",
        "{other} wanted a fight at {place}, war now"
    ];

    private static readonly string[] WarDefender =
    [
        "{actor} jumped me at {place}, war now",
        "{actor}'s guild wants war, they got it"
    ];

    private static readonly string[] RedHeard =
    [
        "red at {place} {when}, {actor}",
        "watch out, {actor} is red and hanging at {place}",
        "{actor} (red) seen near {place}",
        "reds near {place}, {actor} i think"
    ];

    private static readonly string[] RedVague =
    [
        "heard theres a red around {place}",
        "someone saw a red near {place}"
    ];

    private static readonly string[] RedWitness =
    [
        "saw {actor} at {place} {when}, red",
        "{actor} was at {place}, red. careful",
        "ran from {actor} at {place} {when}"
    ];

    private static readonly string[] PartyHeard =
    [
        "{actor}'s group did {place} {when}",
        "{actor} ran {place} with a group {when}",
        "{actor} took a group to {place} {when}",
        "heard {actor} and {other} cleared {place} {when}",
        "{actor} and {other} did {place} {when}"
    ];

    private static readonly string[] PartyVague =
    [
        "heard a group went to {place}"
    ];

    private static readonly string[] PartyLeader =
    [
        "took a group to {place} {when}, good run",
        "took {other} to {place} {when}, gg",
        "we did {place} {when}, gg",
        "just got back from {place} with a group"
    ];

    private static readonly string[] PartyMember =
    [
        "went to {place} with {actor} {when}, fun",
        "{actor}'s group did {place}, i was in it",
        "did {place} with {actor} {when}"
    ];

    private static readonly string[] DuelHeard =
    [
        "{actor} beat {other} in a duel at {place} {when}",
        "{actor} and {other} dueled at {place}, {actor} won",
        "saw {actor} take {other} in a spar at {place}",
        "{other} lost a duel to {actor} at {place} lol"
    ];

    private static readonly string[] DuelVague =
    [
        "heard there was a duel at {place}",
        "someone won a duel at {place} i heard"
    ];

    private static readonly string[] DuelWinner =
    [
        "beat {other} in a duel at {place} {when}",
        "dueled {other} at {place}, won lol",
        "{other} wanted a rematch, took em again at {place}"
    ];

    private static readonly string[] DuelLoser =
    [
        "lost a duel to {actor} at {place}, lag",
        "{actor} beat me in a spar {when}, rematch soon",
        "dueled {actor} at {place}. next time"
    ];

    private static readonly string[] RedKillHeard =
    [
        "{other} put down {actor} at {place} {when}",
        "heard {other} killed the red {actor} near {place}",
        "{actor} finally got dropped at {place}, {other} did it",
        "{other} took out {actor} at {place} {when}"
    ];

    private static readonly string[] RedKillVague =
    [
        "heard a red got put down at {place}",
        "someone killed a red near {place} {when}"
    ];

    private static readonly string[] RedKillKiller =
    [
        "we put down {actor} at {place} {when}",
        "dropped {actor} at {place}, one less red",
        "{actor} came at us at {place}. didnt go well for him"
    ];

    private static readonly string[] RedKillReplies =
    [
        "good riddance",
        "about time someone got {actor}",
        "nice, one less red",
        "{actor} will be back tho"
    ];

    private static readonly string[] DuelReplies =
    [
        "gf",
        "rematch!",
        "i want a spar too",
        "{actor} is good",
        "nice"
    ];

    private static readonly string[] PkReplies =
    [
        "wow",
        "omg",
        "stay out of {place} then",
        "reds r nuts",
        "ty for the heads up",
        "damn reds",
        "rip",
        "someone should hunt {other}"
    ];

    private static readonly string[] DeathReplies =
    [
        "rip",
        "lol",
        "{place} is rough",
        "ouch",
        "rip {actor}",
        "happens",
        "been there"
    ];

    private static readonly string[] TheftReplies =
    [
        "thieves everywhere lol",
        "check ur pack",
        "guards never catch em",
        "ugh thieves",
        "lol"
    ];

    private static readonly string[] WarReplies =
    [
        "war lol",
        "here we go",
        "stay out of it",
        "more loot for someone"
    ];

    private static readonly string[] RedReplies =
    [
        "ty",
        "ty for the warning",
        "avoiding {place} then",
        "k",
        "thx"
    ];

    private static readonly string[] TreasureHeard =
    [
        "{actor} dug up a chest near {place} {when}",
        "{actor} found treasure out by {place}",
        "heard {actor} pulled a chest at {place}, lucky",
        "{actor} dug a tmap near {place}, guardians and all"
    ];

    private static readonly string[] TreasureVague =
    [
        "heard someone dug a chest out past {place}",
        "someone found treasure near {place}"
    ];

    private static readonly string[] TreasureOwn =
    [
        "dug up a chest near {place} {when}!!",
        "finally got my tmap at {place}, killed the guards too",
        "chest at {place}, not bad loot"
    ];

    private static readonly string[] SeaFindHeard =
    [
        "{actor} fished up a bottle at {place} {when}",
        "{actor} got an sos off {place}",
        "heard {actor} reeled in a map at {place}"
    ];

    private static readonly string[] SeaFindVague =
    [
        "heard a fisher got a bottle near {place}",
        "someone fished up a map near {place}"
    ];

    private static readonly string[] SeaFindOwn =
    [
        "reeled in a bottle at {place} {when}, selling it",
        "got an sos fishing at {place}, i fish i dont dig"
    ];

    private static readonly string[] TreasureReplies =
    [
        "grats",
        "what was in it",
        "lucky",
        "sell me the next one"
    ];

    private static readonly string[] PartyReplies =
    [
        "nice",
        "gj",
        "grats",
        "any good loot?",
        "next time invite me"
    ];

    public static Role RoleOf(ShardEvent news, string teller) =>
        news.IsActor(teller) ? Role.Actor : news.Involves(teller) ? Role.Other : Role.Bystander;

    /// <summary>
    /// The teller's line for this event, or null when this kind has no line for this teller.
    /// <paramref name="repeatKills"/> names a killer who keeps at it.
    /// </summary>
    public static string Tell(ShardEvent news, string teller, int detail, int repeatKills, DateTime now, int seed)
    {
        if (news == null || detail == GossipRules.FadedDetail)
        {
            return null;
        }

        var role = RoleOf(news, teller);
        var vague = role == Role.Bystander && detail == GossipRules.VagueDetail;
        var lines = news.Type switch
        {
            ShardEventType.Pk => role switch
            {
                Role.Actor => PkOwnVictim,
                Role.Other => PkOwnKiller,
                _ => vague ? PkVague : GossipRules.IsRepeatKiller(repeatKills) ? PkRepeat : PkHeard
            },
            ShardEventType.Death => role switch
            {
                Role.Actor => DeathOwn,
                Role.Other => DeathKiller,
                _ => vague ? DeathVague : DeathHeard
            },
            ShardEventType.Theft => role switch
            {
                Role.Actor => null,
                Role.Other => TheftVictim,
                _ => vague ? TheftVague : TheftHeard
            },
            ShardEventType.GuildWar => role switch
            {
                Role.Actor => WarOwn,
                Role.Other => WarDefender,
                _ => vague ? WarVague : WarHeard
            },
            ShardEventType.Red => role switch
            {
                Role.Actor => null,
                Role.Other => RedWitness,
                _ => vague ? RedVague : RedHeard
            },
            ShardEventType.Party => role switch
            {
                Role.Actor => PartyLeader,
                Role.Other => PartyMember,
                _ => vague ? PartyVague : PartyHeard
            },
            ShardEventType.Duel => role switch
            {
                Role.Actor => DuelWinner,
                Role.Other => DuelLoser,
                _ => vague ? DuelVague : DuelHeard
            },
            ShardEventType.RedKill => role switch
            {
                Role.Actor => null,
                Role.Other => RedKillKiller,
                _ => vague ? RedKillVague : RedKillHeard
            },
            ShardEventType.Treasure => role == Role.Actor ? TreasureOwn : vague ? TreasureVague : TreasureHeard,
            ShardEventType.SeaFind => role == Role.Actor ? SeaFindOwn : vague ? SeaFindVague : SeaFindHeard,
            _ => null
        };

        return lines == null ? null : Fill(Pick(lines, seed), news, now, repeatKills);
    }

    /// <summary>A listener's answer to the story, or null for an event kind with no answers.</summary>
    public static string Reply(ShardEvent news, int seed)
    {
        var lines = news?.Type switch
        {
            ShardEventType.Pk => PkReplies,
            ShardEventType.Death => DeathReplies,
            ShardEventType.Theft => TheftReplies,
            ShardEventType.GuildWar => WarReplies,
            ShardEventType.Red => RedReplies,
            ShardEventType.Party => PartyReplies,
            ShardEventType.Duel => DuelReplies,
            ShardEventType.RedKill => RedKillReplies,
            ShardEventType.Treasure or ShardEventType.SeaFind => TreasureReplies,
            _ => null
        };

        return lines == null ? null : Fill(Pick(lines, seed), news, news.At, 0);
    }

    /// <summary>
    /// The plain facts of the event from the teller's side, for a model to put in its own
    /// words near a player. Never more than the journal holds.
    /// </summary>
    public static string Fact(ShardEvent news, string teller, DateTime now)
    {
        if (news == null)
        {
            return null;
        }

        var role = RoleOf(news, teller);
        var actor = news.Actor ?? ShardEvent.SomeoneName;
        var other = news.Other ?? ShardEvent.SomeoneName;
        var place = PlaceWord(news.Place);
        var when = GossipRules.WhenWord(now - news.At);

        return news.Type switch
        {
            ShardEventType.Pk => role switch
            {
                Role.Actor => $"You were murdered by {other} at {place} {when}.",
                Role.Other => $"You killed {actor} at {place} {when}.",
                _ => $"{actor} was murdered by {other} at {place} {when}."
            },
            ShardEventType.Death => role switch
            {
                Role.Actor => $"You died at {place} {when}, killed by {news.Other ?? MonsterWord}.",
                Role.Other => $"You killed {actor} at {place} {when}.",
                _ => $"{actor} died at {place} {when}, killed by {news.Other ?? MonsterWord}."
            },
            ShardEventType.Theft => role == Role.Other
                ? $"{actor} stole from you at {place} {when}."
                : $"{actor} stole from {other} at {place} {when}.",
            ShardEventType.GuildWar => role switch
            {
                Role.Actor => $"You attacked {other} at {place} {when}; your guilds are now at war.",
                Role.Other => $"{actor} attacked you at {place} {when}; your guilds are now at war.",
                _ => $"{actor} attacked {other} at {place} {when}; their guilds are now at war."
            },
            ShardEventType.Red => role == Role.Other
                ? $"You saw {actor}, a murderer, at {place} {when}."
                : $"{actor}, a murderer, was seen at {place} {when}.",
            ShardEventType.Party => role switch
            {
                Role.Actor => $"You led {other} through {place} {when}.",
                Role.Other => $"You went through {place} with {actor}'s group {when}.",
                _ => $"{actor} led {other} through {place} {when}."
            },
            ShardEventType.Duel => role switch
            {
                Role.Actor => $"You beat {other} in a friendly duel at {place} {when}.",
                Role.Other => $"You lost a friendly duel to {actor} at {place} {when}.",
                _ => $"{actor} beat {other} in a friendly duel at {place} {when}."
            },
            ShardEventType.RedKill => role switch
            {
                Role.Actor => $"{other} killed you at {place} {when}.",
                Role.Other => $"You and your side ({other}) killed the murderer {actor} at {place} {when}.",
                _ => $"{other} killed the murderer {actor} at {place} {when}."
            },
            ShardEventType.Treasure => role == Role.Actor
                ? $"You dug up a treasure chest near {place} {when}."
                : $"{actor} dug up a treasure chest near {place} {when}.",
            ShardEventType.SeaFind => role == Role.Actor
                ? $"You fished up a message in a bottle at {place} {when}."
                : $"{actor} fished up a message in a bottle at {place} {when}.",
            _ => null
        };
    }

    /// <summary>"Despise, Felucca" becomes "despise": people name the spot, not the facet.</summary>
    public static string PlaceWord(string place)
    {
        if (string.IsNullOrWhiteSpace(place))
        {
            return PlaceNameRules.Wild;
        }

        var cut = place.IndexOf(PlaceSeparator, StringComparison.Ordinal);
        var spot = cut < 0 ? place : place[..cut];
        return spot.Trim().ToLowerInvariant();
    }

    private static string Pick(string[] lines, int seed) => lines[Math.Abs(seed % lines.Length)];

    private static string Fill(string template, ShardEvent news, DateTime now, int count) =>
        template
            .Replace("{actor}", news.Actor ?? ShardEvent.SomeoneName, StringComparison.Ordinal)
            .Replace("{other}", news.Other ?? ShardEvent.SomeoneName, StringComparison.Ordinal)
            .Replace("{place}", PlaceWord(news.Place), StringComparison.Ordinal)
            .Replace("{when}", GossipRules.WhenWord(now - news.At), StringComparison.Ordinal)
            .Replace("{count}", count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
