using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// One job Jev may pick: the plain words it sees as the option name, the action it maps back
/// to, and a contrastive rubric: what the job is for and when it is the wrong pick.
/// </summary>
public sealed record JobOption(string Key, string ActionId, string What, string NotFor);

/// <summary>
/// Builds the next-job options from the scorer's ranking. Only jobs the rules allow now are
/// offered, best first, at most <see cref="MaxOptions"/>, so every option is a real choice.
/// Jev picks the kind of job by its plain name; the scorer has already put the best place
/// for each name first. Pure.
/// </summary>
public static class JevJobOptions
{
    /// <summary>More options spread Jev's answer thin and cost tokens; the scorer's top few are the real contest.</summary>
    public const int MaxOptions = 5;

    private enum JobGroup
    {
        Gather,
        Craft,
        Sell,
        Buy,
        Fight,
        Recover,
        Leisure,
        Travel,
        Group,
        Crime,
        Practice
    }

    // One short "for" and "not for" per group, so two kinds of job read as a contrast. The
    // group's "for" is the fallback for a kind with no words of its own in KindWhat.
    private static readonly Dictionary<JobGroup, (string What, string NotFor)> Rubrics = new()
    {
        [JobGroup.Gather] = ("gather goods to sell", "full pack or wounds"),
        [JobGroup.Craft] = ("make goods", "no materials or full pack"),
        [JobGroup.Sell] = ("sell or bank goods", "empty pack"),
        [JobGroup.Buy] = ("buy gear or property", "thin purse"),
        [JobGroup.Fight] = ("fight for loot and skill", "wounds, low mana, or danger"),
        [JobGroup.Recover] = ("recover hits and mana", "unhurt and rested"),
        [JobGroup.Leisure] = ("pass time with people", "work, wounds, or goods waiting"),
        [JobGroup.Travel] = ("go somewhere else", "already where the work is"),
        [JobGroup.Group] = ("stay with the group", "no group near"),
        [JobGroup.Crime] = ("sneak or steal", "guards or friends watching"),
        [JobGroup.Practice] = ("train a skill safely", "goods or wounds waiting")
    };

    // What each kind of job is for, in a player's words. Two jobs of one group must not read
    // the same: with "hunt" and "seek player conflict" both "fight for loot and skill", Von and
    // Laya split their answer evenly between them. A kind not listed uses its group's words.
    private static readonly Dictionary<string, string> KindWhat = new(StringComparer.Ordinal)
    {
        [SkillKinds.Lumberjack] = "chop logs to sell or craft",
        [SkillKinds.Mine] = "dig ore to smelt and sell",
        [SkillKinds.Fish] = "catch fish to eat or sell",
        [SkillKinds.Smith] = "forge weapons and armor",
        [SkillKinds.Tailor] = "sew clothes and leather",
        [SkillKinds.Carpentry] = "build wooden goods",
        [SkillKinds.Cook] = "cook food to eat or sell",
        [SkillKinds.Fletch] = "make bows and arrows",
        [SkillKinds.Tinker] = "make tools",
        [SkillKinds.Alchemy] = "brew potions",
        [SkillKinds.Inscription] = "write scrolls",
        [SkillKinds.Cartography] = "draw maps",
        [SkillKinds.VendorSell] = "sell goods to a shop",
        [SkillKinds.BankDeposit] = "put gold and goods in the bank",
        [SkillKinds.BankShop] = "trade goods with people at the bank",
        [SkillKinds.PlayerVendor] = "stock your own vendor",
        [SkillKinds.VendorBuy] = "buy supplies from a shop",
        [SkillKinds.UpgradeGear] = "buy better weapons or armor",
        [SkillKinds.BuyMount] = "buy a horse to ride",
        [SkillKinds.House] = "look for a house to own",
        [SkillKinds.Boat] = "put a boat on the water",
        [SkillKinds.Browse] = "look at what shops sell",
        [SkillKinds.Hunt] = "kill monsters for loot and skill",
        [SkillKinds.Dungeon] = "fight monsters in a dungeon for better loot",
        [SkillKinds.Conflict] = "attack other players for their loot",
        [SkillKinds.Tame] = "tame an animal to fight beside you",
        [SkillKinds.Track] = "find prey or players by their tracks",
        [SkillKinds.Provoke] = "turn monsters against each other",
        [SkillKinds.Discord] = "weaken a monster with music",
        [SkillKinds.Patrol] = "guard the roads against criminals",
        [SkillKinds.Rest] = "sit and get hits back",
        [SkillKinds.Tavern] = "rest and drink with people at the inn",
        [SkillKinds.Meditate] = "get mana back",
        [SkillKinds.Heal] = "bandage wounds",
        [SkillKinds.Camp] = "make a safe camp in the wild",
        [SkillKinds.Sightsee] = "walk about town and look around",
        [SkillKinds.Visit] = "spend time with a friend",
        [SkillKinds.Loiter] = "hang about and chat",
        [SkillKinds.IdleWander] = "wander without a goal",
        [SkillKinds.BankCrowd] = "stand with the crowd at the bank",
        [SkillKinds.Arrive] = "settle in after a trip",
        [SkillKinds.Music] = "play music for people",
        [SkillKinds.Beg] = "ask people for coins",
        [SkillKinds.GoTo] = "walk to another place",
        [SkillKinds.GoHome] = "go back home",
        [SkillKinds.Travel] = "go to another town",
        [SkillKinds.Recall] = "teleport by rune",
        [SkillKinds.Gate] = "open a magic gate",
        [SkillKinds.Mount] = "get on your horse",
        [SkillKinds.Mark] = "mark a rune for later travel",
        [SkillKinds.Flee] = "run from danger",
        [SkillKinds.Follow] = "stay with your party",
        [SkillKinds.Steal] = "steal from someone",
        [SkillKinds.Snoop] = "peek into someone's pack",
        [SkillKinds.Lockpick] = "open a locked box",
        [SkillKinds.Hide] = "hide from sight",
        [SkillKinds.Stealth] = "sneak unseen",
        [SkillKinds.Poison] = "poison a weapon",
        [SkillKinds.Forensic] = "search a body for clues"
    };

    public static IReadOnlyList<JobOption> From(ScoreResult result, int max = MaxOptions)
    {
        var options = new List<JobOption>();

        if (result?.Ranked == null)
        {
            return options;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ranked is sorted best first; the best place for each kind of job keeps its name.
        for (var i = 0; i < result.Ranked.Count && options.Count < max; i++)
        {
            var scored = result.Ranked[i];

            if (!ActionScorer.IsEligible(scored) || string.IsNullOrWhiteSpace(scored.Id.Value))
            {
                continue;
            }

            var key = ActionDescriptions.Phrase(scored.SkillKind, scored.Id.Value);

            if (!keys.Add(key))
            {
                continue;
            }

            var (groupWhat, notFor) = Rubrics[GroupOf(scored.SkillKind)];
            var what = KindWhat.TryGetValue(scored.SkillKind, out var own) ? own : groupWhat;
            options.Add(new JobOption(key, scored.Id.Value, what, notFor));
        }

        return options;
    }

    /// <summary>
    /// The Choice criteria: option name to its rubric as one line. Von and Laya take a
    /// string per option; a nested what / not_for object is refused with HTTP 422.
    /// </summary>
    public static Dictionary<string, string> Criteria(IReadOnlyList<JobOption> options)
    {
        var criteria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (options == null)
        {
            return criteria;
        }

        for (var i = 0; i < options.Count; i++)
        {
            criteria[options[i].Key] = Rubric(options[i].What, options[i].NotFor);
        }

        return criteria;
    }

    /// <summary>One contrastive Choice rubric: what the option is for, and when it is the wrong pick.</summary>
    public static string Rubric(string what, string notFor) => $"{what}; wrong when {notFor}";

    private static JobGroup GroupOf(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish => JobGroup.Gather,
            SkillKinds.Smith or SkillKinds.Tailor or SkillKinds.Carpentry or SkillKinds.Cook or SkillKinds.Fletch
                or SkillKinds.Tinker or SkillKinds.Alchemy or SkillKinds.Inscription or SkillKinds.Cartography => JobGroup.Craft,
            SkillKinds.VendorSell or SkillKinds.BankDeposit or SkillKinds.BankShop or SkillKinds.PlayerVendor => JobGroup.Sell,
            SkillKinds.VendorBuy or SkillKinds.UpgradeGear or SkillKinds.BuyMount or SkillKinds.House
                or SkillKinds.Boat or SkillKinds.Browse => JobGroup.Buy,
            SkillKinds.Hunt or SkillKinds.Dungeon or SkillKinds.Conflict or SkillKinds.Tame or SkillKinds.Track
                or SkillKinds.Provoke or SkillKinds.Discord or SkillKinds.Patrol => JobGroup.Fight,
            SkillKinds.Rest or SkillKinds.Tavern or SkillKinds.Meditate or SkillKinds.Heal or SkillKinds.Camp => JobGroup.Recover,
            SkillKinds.Sightsee or SkillKinds.Visit or SkillKinds.Loiter or SkillKinds.IdleWander or SkillKinds.BankCrowd
                or SkillKinds.Arrive or SkillKinds.Music or SkillKinds.Beg => JobGroup.Leisure,
            SkillKinds.GoTo or SkillKinds.GoHome or SkillKinds.Travel or SkillKinds.Recall or SkillKinds.Gate
                or SkillKinds.Mount or SkillKinds.Mark or SkillKinds.Flee => JobGroup.Travel,
            SkillKinds.Follow => JobGroup.Group,
            SkillKinds.Steal or SkillKinds.Snoop or SkillKinds.Lockpick or SkillKinds.Hide or SkillKinds.Stealth
                or SkillKinds.Poison => JobGroup.Crime,
            _ => JobGroup.Practice
        };
}
