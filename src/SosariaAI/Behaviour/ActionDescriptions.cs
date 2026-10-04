using System;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// Short plain phrases for log lines, memory, and speech. Ids and coordinates
/// never appear in those places.
/// </summary>
public static class ActionDescriptions
{
    public const string Idle = "idle";
    public const string Unknown = "go about my day";

    public static string Phrase(string skillKind, string actionId = null)
    {
        var kind = string.IsNullOrWhiteSpace(skillKind)
            ? ActionId.SkillKindOf(actionId)
            : skillKind;

        return kind switch
        {
            SkillKinds.Lumberjack => "cut wood",
            SkillKinds.Mine => "mine ore",
            SkillKinds.Fish => "fish",
            SkillKinds.VendorSell => "sell at the vendor",
            SkillKinds.VendorBuy => "buy from a vendor",
            SkillKinds.BankDeposit => "walk to the bank",
            SkillKinds.BankShop => "trade at the bank",
            SkillKinds.UpgradeGear => "buy gear",
            SkillKinds.Tavern => "rest at the inn",
            SkillKinds.Visit => "visit a friend",
            SkillKinds.Sightsee => "look around town",
            SkillKinds.Loiter => "loiter",
            SkillKinds.IdleWander => "wander",
            SkillKinds.Rest => "rest",
            SkillKinds.Hunt => "hunt",
            SkillKinds.Dungeon => "enter a dungeon",
            SkillKinds.Follow => "follow the party",
            SkillKinds.Patrol => "patrol",
            SkillKinds.House => "look at a house",
            SkillKinds.GoTo => WalkPhrase(actionId),
            SkillKinds.Flee => "flee",
            SkillKinds.GoHome => "go home",
            SkillKinds.Smith => "work the forge",
            SkillKinds.Boat => "place a boat",
            SkillKinds.Steal => "steal",
            SkillKinds.Hide => "hide",
            SkillKinds.Lockpick => "pick a lock",
            SkillKinds.DetectHidden => "search for hidden folk",
            SkillKinds.Snoop => "peek into a pack",
            SkillKinds.Heal => "bandage a wound",
            SkillKinds.Alchemy => "brew a potion",
            SkillKinds.Inscription => "copy a scroll",
            SkillKinds.Peace => "play peace",
            SkillKinds.Track => "track",
            SkillKinds.Tailor => "sew",
            SkillKinds.Poison => "poison a blade",
            SkillKinds.Carpentry => "work wood",
            SkillKinds.Cook => "cook a meal",
            SkillKinds.Fletch => "fletch arrows",
            SkillKinds.Tinker => "tinker",
            SkillKinds.Meditate => "meditate",
            SkillKinds.Cartography => "draw a map",
            SkillKinds.Lore => "study a beast",
            SkillKinds.Vet => "tend a beast",
            SkillKinds.Spirit => "speak with spirits",
            SkillKinds.Anatomy => "study anatomy",
            SkillKinds.EvalInt => "evaluate intellect",
            SkillKinds.ArmsLore => "study arms",
            SkillKinds.ItemId => "identify loot",
            SkillKinds.Discord => "play discord",
            SkillKinds.Provoke => "provoke a fight",
            SkillKinds.Forensic => "examine a corpse",
            SkillKinds.Beg => "beg",
            SkillKinds.Camp => "make camp",
            SkillKinds.RemoveTrap => "disarm a trap",
            SkillKinds.Stealth => "move in stealth",
            SkillKinds.Taste => "taste food",
            SkillKinds.Wrestle => "wrestle",
            SkillKinds.Tactics => "drill tactics",
            SkillKinds.Parry => "practice parry",
            SkillKinds.Mage => "practice magery",
            SkillKinds.Resist => "resist spells",
            SkillKinds.Sword => "practice swords",
            SkillKinds.Fence => "practice fencing",
            SkillKinds.Archery => "practice archery",
            SkillKinds.Mace => "practice mace",
            SkillKinds.Herd => "herd animals",
            SkillKinds.Music => "play music",
            SkillKinds.Gate => "open a gate",
            SkillKinds.Necro => "practice necromancy",
            SkillKinds.Tame => "tame an animal",
            SkillKinds.Mount => "mount a steed",
            SkillKinds.BuyMount => "buy a mount",
            SkillKinds.Mark => "mark a rune",
            SkillKinds.Recall => "recall",
            SkillKinds.PlayerVendor => "tend a vendor",
            SkillKinds.Conflict => "seek player conflict",
            SkillKinds.Travel => "travel to another town",
            SkillKinds.Arrive => "stay a while",
            SkillKinds.Browse => "browse the shops",
            _ => Unknown
        };
    }

    public static string Started(string skillKind, string actionId = null) =>
        $"I set off to {Phrase(skillKind, actionId)}.";

    private static string WalkPhrase(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return "walk";
        }

        if (actionId.Contains("bank", StringComparison.OrdinalIgnoreCase))
        {
            return "walk to the bank";
        }

        if (actionId.Contains("graveyard", StringComparison.OrdinalIgnoreCase))
        {
            return "walk to the graveyard";
        }

        if (actionId.Contains("forest", StringComparison.OrdinalIgnoreCase) ||
            actionId.Contains("wood", StringComparison.OrdinalIgnoreCase))
        {
            return "walk to the forest";
        }

        return "walk";
    }
}
