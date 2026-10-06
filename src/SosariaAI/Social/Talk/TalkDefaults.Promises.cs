using System;
using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Social;

public static partial class TalkDefaults
{
    /// <summary>
    /// Categories said where no group, trip or meeting stands behind the line, so no line of theirs
    /// may promise one (<see cref="PromiseLines.IsPromise"/>): a bot at the bank asked "anyone going
    /// to despise?", a player said "me", and nothing happened. Every social, fighting, trade and
    /// street category is here unless its speaker really does what its promise says at that moment,
    /// so a new category starts promise-free.
    /// </summary>
    public static readonly IReadOnlySet<string> PromiseFree = PromiseFreeOf([.. Social(), .. Fighting(), .. Trade(), .. Street()]);

    /// <summary>
    /// Rows the shipped defaults once held that promise a trip, group or meeting no code keeps, in
    /// words <see cref="PromiseLines"/> does not catch ("u in?", "again sometime?"). A talk file is
    /// only written when missing, so a shard that booted before still has them in its files.
    /// </summary>
    private static readonly Dictionary<string, string[]> Retired = new(StringComparer.Ordinal)
    {
        [TalkCategory.SmallTalk] = ["{name} u still on for later?"],
        [TalkCategory.GreetOldFriend] = ["yo {friend}! {place} again sometime?", "{friend} o/ we should go back to {place}"],
        [TalkCategory.RecallAdventure] = ["we should hit {place} again {friend}", "thinking bout {place} again, u in?"],
        [TalkCategory.GoingAsk] = ["anyone going to {place}?", "anyone headed to {place}?", "anyone going {place} later?"],
        [TalkCategory.GoingReply] = ["i might go later"],
        [TalkCategory.GuardStandDown] = ["take it outside {name}", "outside town, {name}"]
    };

    /// <summary>
    /// True when a row of <paramref name="category"/> may be said: not a retired default, not a
    /// promise in a promise-free category, no repair offer, and not an order line outside the one
    /// category the order desk backs (<see cref="TalkCategory.CraftTakingOrders"/>). An operator's
    /// file is never rewritten, so the talk library asks this of every row it loads.
    /// </summary>
    public static bool Keeps(string category, string line) =>
        !IsRetired(category, line) && !(PromiseFree.Contains(category) && PromiseLines.IsPromise(line)) &&
        !OrderLines.ClaimsRepairs(line) && !(OrderLines.IsOrderTalk(line) && category != TalkCategory.CraftTakingOrders);

    private static bool IsRetired(string category, string line)
    {
        if (category == null || line == null || !Retired.TryGetValue(category, out var rows))
        {
            return false;
        }

        var said = line.Trim();

        foreach (var row in rows)
        {
            if (string.Equals(row, said, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // The categories whose speaker keeps the promise as it speaks: the crafter's bank trip and the
    // gatherer's walk to the waiting crafter have begun, the decoded map's walk starts that tick,
    // the duel and the res are real, and the assist and the anti-pk answer come from one already
    // fighting.
    private static HashSet<string> PromiseFreeOf(TalkTopic[] topics)
    {
        string[] backed =
        [
            TalkCategory.CraftBankStock, TalkCategory.StockToBank, TalkCategory.TreasureDecoded, TalkCategory.DuelChallenge,
            TalkCategory.DuelAccept, TalkCategory.ResOffer, TalkCategory.CombatAssist, TalkCategory.RedOnMyWay
        ];
        var free = new HashSet<string>(StringComparer.Ordinal);

        foreach (var topic in topics)
        {
            if (Array.IndexOf(backed, topic.Name) < 0)
            {
                free.Add(topic.Name);
            }
        }

        return free;
    }
}
