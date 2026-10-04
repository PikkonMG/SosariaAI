using SosariaAI.Configuration;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// What a character's work looks like to a 1999 player, for the "what are you doing" answer
/// and the "gave up" mutter. The wording lives in the talk library
/// (<see cref="TalkCategory.RespondDoing"/>, <see cref="TalkCategory.PlanFailed"/>).
/// </summary>
public static class SpeechLines
{
    /// <summary>A "what are you doing" answer from the skill the character really runs and where.</summary>
    public static string DoingAnswer(string skillKind, string place, int seed)
    {
        var doing = DoingPhrase(skillKind);
        var spot = string.IsNullOrWhiteSpace(place) ? null : place;

        if (spot != null && ShowsPlace(skillKind))
        {
            doing = $"{doing} at {spot}";
        }

        return Talk.Line(TalkCategory.RespondDoing, seed, new TalkSlots { Doing = doing });
    }

    /// <summary>
    /// A failed plan said like a player, never in planner words: "seek player conflict" is a
    /// scorer's name, and nobody at a keyboard says it. A kind whose work phrase reads as a
    /// gerund fills {doing}; the rest take a plain line. Null when the file has nothing.
    /// </summary>
    public static string FailedLine(string failedId, int seed)
    {
        var kind = ActionId.SkillKindOf(failedId);
        var slots = kind != null && SaysGerund(kind)
            ? new TalkSlots { Doing = DoingPhrase(kind) }
            : default;

        return Talk.Line(TalkCategory.PlanFailed, seed, slots);
    }

    /// <summary>What the skill looks like to a 1999 player. A thief does not say it steals.</summary>
    public static string DoingPhrase(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Lumberjack => "chopping wood",
            SkillKinds.Mine => "mining",
            SkillKinds.Fish => "fishing",
            SkillKinds.VendorSell => "selling loot",
            SkillKinds.VendorBuy or SkillKinds.UpgradeGear => "shopping",
            SkillKinds.BankDeposit or SkillKinds.BankShop or SkillKinds.BankCrowd => "at the bank",
            SkillKinds.Tavern => "chilling at the inn",
            SkillKinds.Visit => "visiting a friend",
            SkillKinds.Rest or SkillKinds.Camp => "resting",
            SkillKinds.Hunt => "hunting",
            SkillKinds.Dungeon => "doing a dungeon",
            SkillKinds.Follow => "with my group",
            SkillKinds.Patrol => "patrolling",
            SkillKinds.House => "looking at houses",
            SkillKinds.GoTo or SkillKinds.Travel or SkillKinds.Recall or SkillKinds.Gate => "running somewhere",
            SkillKinds.Flee => "running lol",
            SkillKinds.GoHome => "heading home",
            SkillKinds.Smith => "smithing",
            SkillKinds.Tailor => "tailoring",
            SkillKinds.Carpentry => "doing carpentry",
            SkillKinds.Tinker => "tinkering",
            SkillKinds.Alchemy => "making pots",
            SkillKinds.Inscription => "scribing",
            SkillKinds.Fletch => "fletching",
            SkillKinds.Cook => "cooking",
            SkillKinds.Tame or SkillKinds.Herd => "taming",
            SkillKinds.Meditate => "medding",
            SkillKinds.Music or SkillKinds.Peace or SkillKinds.Provoke or SkillKinds.Discord => "playing music",
            SkillKinds.Beg => "begging lol",
            SkillKinds.Practice => "training skills",
            _ => "nothing much"
        };

    private static bool ShowsPlace(string skillKind) =>
        skillKind is SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish or SkillKinds.Hunt
            or SkillKinds.Dungeon or SkillKinds.BankDeposit or SkillKinds.BankShop or SkillKinds.BankCrowd
            or SkillKinds.Tavern or SkillKinds.Smith;

    /// <summary>
    /// The kinds whose <see cref="DoingPhrase"/> reads as a gerund a "{doing}" line can carry.
    /// Phrases shaped like clauses ("at the bank", "with my group", "running somewhere") do not
    /// fit those lines, so those kinds get a slot-free complaint instead.
    /// </summary>
    private static bool SaysGerund(string skillKind) =>
        skillKind is SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish or SkillKinds.VendorSell
            or SkillKinds.VendorBuy or SkillKinds.UpgradeGear or SkillKinds.Rest or SkillKinds.Camp
            or SkillKinds.Hunt or SkillKinds.Dungeon or SkillKinds.Patrol or SkillKinds.House
            or SkillKinds.GoHome or SkillKinds.Smith or SkillKinds.Tailor or SkillKinds.Carpentry
            or SkillKinds.Tinker or SkillKinds.Alchemy or SkillKinds.Inscription or SkillKinds.Fletch
            or SkillKinds.Cook or SkillKinds.Tame or SkillKinds.Herd or SkillKinds.Meditate
            or SkillKinds.Music or SkillKinds.Peace or SkillKinds.Provoke or SkillKinds.Discord
            or SkillKinds.Practice or SkillKinds.Visit;
}
