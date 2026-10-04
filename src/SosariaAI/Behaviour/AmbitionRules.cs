using System;
using Server;

namespace SosariaAI.Behaviour;

public enum AmbitionKind
{
    None,
    Gold,
    Skill,
    Gear,
    Kills,
    Place
}

public readonly record struct Ambition(
    AmbitionKind Kind,
    string Target,
    int Goal,
    int Progress)
{
    /// <summary>The creature a kill ambition hunts, which fits best wherever it lives; null for any other ambition.</summary>
    public string Prey => Kind == AmbitionKind.Kills ? Target : null;
}

/// <summary>
/// One visible ambition per character. Progress is public to other characters.
/// </summary>
public static class AmbitionRules
{
    public const int DefaultBoatGold = 2000;
    public const int DefaultSkillGoal = 100;
    public const int DefaultKillGoal = 10;
    public const int DefaultNestEggGold = 1000;
    public const int DefaultGearScore = 80;
    public const int PlaceGoal = 1;
    public const double NearDoneFraction = 0.75;

    public const string NestEggTarget = "a nest egg";
    public const string SkillMining = "Mining";
    public const string SkillLumberjacking = "Lumberjacking";
    public const string PlaceMinoc = "Minoc";
    public const string CreatureTroll = "troll";
    public const string GearPlate = "plate";

    public const string SavingForMarker = "saving for ";
    public const string SkillFishing = "Fishing";
    public const string SkillArchery = "Archery";
    public const string SkillMagery = "Magery";
    public const string SkillSwords = "Swords";
    public const string CreatureSkeleton = "skeleton";

    private const string VeteranMarker = "veteran";
    private const string DespiseMarker = "despise";

    // The trade a background names, and the skill that trade trains. First match wins,
    // so a more specific phrase must come before a shorter one it contains, and a
    // smith's sword must be read as smithing before "sword" is read as swordplay.
    private static readonly (string Word, string Skill)[] TradeSkills =
    [
        ("cuts wood", SkillLumberjacking),
        ("woodcutter", SkillLumberjacking),
        ("lumber", SkillLumberjacking),
        ("miner", SkillMining),
        ("mining", SkillMining),
        ("mine", SkillMining),
        ("vein", SkillMining),
        ("ore", SkillMining),
        ("fish", SkillFishing),
        ("blacksmith", nameof(SkillName.Blacksmith)),
        ("smith", nameof(SkillName.Blacksmith)),
        ("forge", nameof(SkillName.Blacksmith)),
        ("smelt", nameof(SkillName.Blacksmith)),
        ("anvil", nameof(SkillName.Blacksmith)),
        ("ingot", nameof(SkillName.Blacksmith)),
        ("armor", nameof(SkillName.Blacksmith)),
        ("plate", nameof(SkillName.Blacksmith)),
        ("chain", nameof(SkillName.Blacksmith)),
        ("shield", nameof(SkillName.Blacksmith)),
        ("tailor", nameof(SkillName.Tailoring)),
        ("leather", nameof(SkillName.Tailoring)),
        ("sew", nameof(SkillName.Tailoring)),
        ("weave", nameof(SkillName.Tailoring)),
        ("cloak", nameof(SkillName.Tailoring)),
        ("boots", nameof(SkillName.Tailoring)),
        ("dye", nameof(SkillName.Tailoring)),
        ("carpenter", nameof(SkillName.Carpentry)),
        ("carpentry", nameof(SkillName.Carpentry)),
        ("furniture", nameof(SkillName.Carpentry)),
        ("chair", nameof(SkillName.Carpentry)),
        ("masonry", nameof(SkillName.Carpentry)),
        ("tinker", nameof(SkillName.Tinkering)),
        ("clock", nameof(SkillName.Tinkering)),
        ("cook", nameof(SkillName.Cooking)),
        ("bake", nameof(SkillName.Cooking)),
        ("bread", nameof(SkillName.Cooking)),
        ("recipe", nameof(SkillName.Cooking)),
        ("kitchen", nameof(SkillName.Cooking)),
        ("feast", nameof(SkillName.Cooking)),
        ("alchem", nameof(SkillName.Alchemy)),
        ("potion", nameof(SkillName.Alchemy)),
        ("scribe", nameof(SkillName.Inscribe)),
        ("write", nameof(SkillName.Inscribe)),
        ("cartograph", nameof(SkillName.Cartography)),
        ("bowyer", nameof(SkillName.Fletching)),
        ("fletch", nameof(SkillName.Fletching)),
        ("bows", nameof(SkillName.Fletching)),
        ("archer", SkillArchery),
        ("tame", nameof(SkillName.AnimalTaming)),
        ("taming", nameof(SkillName.AnimalTaming)),
        ("beast", nameof(SkillName.AnimalTaming)),
        ("animal", nameof(SkillName.AnimalTaming)),
        ("horse", nameof(SkillName.AnimalTaming)),
        ("ostard", nameof(SkillName.AnimalTaming)),
        ("riding", nameof(SkillName.AnimalTaming)),
        ("breed", nameof(SkillName.AnimalTaming)),
        ("bond", nameof(SkillName.AnimalTaming)),
        ("pet", nameof(SkillName.AnimalTaming)),
        ("stray", nameof(SkillName.AnimalTaming)),
        ("bird", nameof(SkillName.AnimalTaming)),
        ("command", nameof(SkillName.AnimalTaming)),
        ("veterinar", nameof(SkillName.Veterinary)),
        ("healer", nameof(SkillName.Healing)),
        ("thief", nameof(SkillName.Stealing)),
        ("steal", nameof(SkillName.Stealing)),
        ("stolen", nameof(SkillName.Stealing)),
        ("rob", nameof(SkillName.Stealing)),
        ("heist", nameof(SkillName.Stealing)),
        ("purse", nameof(SkillName.Stealing)),
        ("fence", nameof(SkillName.Stealing)),
        ("lockpick", nameof(SkillName.Lockpicking)),
        ("lock", nameof(SkillName.Lockpicking)),
        ("snoop", nameof(SkillName.Snooping)),
        ("stealth", nameof(SkillName.Stealth)),
        ("hiding", nameof(SkillName.Hiding)),
        ("bard", nameof(SkillName.Musicianship)),
        ("music", nameof(SkillName.Musicianship)),
        ("paladin", nameof(SkillName.Chivalry)),
        ("necromancy", nameof(SkillName.Necromancy)),
        ("spellweaving", nameof(SkillName.Spellweaving)),
        ("mage", SkillMagery),
        ("wizard", SkillMagery),
        ("magery", SkillMagery),
        ("spell", SkillMagery),
        ("word of power", SkillMagery),
        ("meteor", SkillMagery),
        ("tactics", nameof(SkillName.Tactics)),
        ("anatomy", nameof(SkillName.Anatomy)),
        ("parry", nameof(SkillName.Parry)),
        ("wrestl", nameof(SkillName.Wrestling)),
        ("red", nameof(SkillName.Tactics)),
        ("fighting", nameof(SkillName.Tactics)),
        ("sparring", nameof(SkillName.Tactics)),
        ("pk", nameof(SkillName.Tactics)),
        ("militia", nameof(SkillName.Tactics)),
        ("siege", nameof(SkillName.Tactics)),
        ("protect", nameof(SkillName.Tactics)),
        ("defend", nameof(SkillName.Tactics)),
        ("fencing", nameof(SkillName.Fencing)),
        ("mace", nameof(SkillName.Macing)),
        ("dagger", nameof(SkillName.Fencing)),
        ("duel", SkillSwords),
        ("swordsman", SkillSwords),
        ("sword", SkillSwords)
    ];

    // Words that say a character knows its own streets and could want to see further.
    private static readonly string[] WanderWords =
    [
        "errands", "every street", "every town", "every market", "every mountain", "every shrine",
        "every dungeon", "every corner", "see the", "travel", "visit", "sail", "reach"
    ];

    // Words that say a want is a hunt. A hunt counts only when it names a creature below.
    private static readonly string[] HuntWords = ["hunt", "kill", "slay", "defeat", "take", "bring down", "fight", "clear"];

    // Creatures a want can name, and the spawn role the hunt catalog files them under.
    // A longer name comes before a shorter one it contains.
    private static readonly (string Word, string Role)[] PreyNames =
    [
        ("giant spider", "GiantSpider"),
        ("sea serpent", "SeaSerpent"),
        ("dragon", "Dragon"),
        ("drake", "Drake"),
        ("wyvern", "Wyvern"),
        ("lich", "Lich"),
        ("troll", CreatureTroll),
        ("ogre", "Ogre"),
        ("orc", "Orc"),
        ("balron", "Balron"),
        ("daemon", "Daemon"),
        ("ettin", "Ettin"),
        ("cyclop", "Cyclops"),
        ("titan", "Titan"),
        ("skeleton", CreatureSkeleton),
        ("zombie", "Zombie"),
        ("wraith", "Wraith"),
        ("gargoyle", "Gargoyle"),
        ("harpy", "Harpy"),
        ("harpies", "Harpy"),
        ("ratmen", "Ratman"),
        ("ratman", "Ratman"),
        ("lizardm", "Lizardman"),
        ("gazer", "Gazer"),
        ("ophidian", "OphidianWarrior"),
        ("terathan", "TerathanWarrior"),
        ("elemental", "EarthElemental")
    ];

    // A want that names a thing to have: "Wants a vendor", "Dreams of a keep", "Hopes to
    // buy back the family farm". The thing starts at an article a few words after the lead.
    private static readonly string[] WantLeads =
    [
        "wants to ", "hopes to ", "means to ", "dreams of ", "hopes for ", "longs for ", "wants "
    ];

    private static readonly string[] Articles = ["a ", "an ", "the ", "one "];

    /// <summary>Words a want may put between its lead and the thing: "to buy back the farm".</summary>
    private const int MaxWordsBeforeThing = 3;

    // Towns the navigation graph reaches on foot or by moongate. A finished ambition picks
    // the next place from here by the character's own seed, so they do not all head to one.
    private static readonly string[] Towns =
    [
        PlaceMinoc, "Moonglow", "Skara Brae", "Yew", "Trinsic", "Jhelom", "Magincia"
    ];

    public const string MoodJustStarted = "just started";
    public const string MoodWellAlong = "well along";
    public const string MoodAlmostThere = "almost there";
    public const string MoodDone = "done";
    public const string DescribeNone = "no ambition";

    /// <summary>
    /// The first ambition comes from what the persona's own background says. A character
    /// saving for something saves for it. A veteran already has its skill, so a skill goal
    /// would finish the moment it was set; it gets a hunt instead. A wanderer travels, and
    /// anyone else trains the trade its background names, hunts the creature it names, or
    /// saves for the thing it wants. Only a background that says none of these falls back
    /// to a plain sum of gold.
    /// </summary>
    public static Ambition SeedFromBackground(string background)
    {
        var text = background ?? string.Empty;

        if (TryParseSavingFor(text, out var target))
        {
            return new Ambition(AmbitionKind.Gold, target, DefaultBoatGold, 0);
        }

        if (HasWord(text, VeteranMarker))
        {
            var prey = HasWord(text, CreatureTroll) || HasWord(text, DespiseMarker)
                ? CreatureTroll
                : CreatureSkeleton;
            return new Ambition(AmbitionKind.Kills, prey, DefaultKillGoal, 0);
        }

        // Errands name the shops on the way, so the wander words go before the trades.
        if (HasAnyWord(text, WanderWords))
        {
            return new Ambition(AmbitionKind.Place, NamedTown(text) ?? PlaceMinoc, PlaceGoal, 0);
        }

        if (TradeSkillOf(text) is { } skill)
        {
            return new Ambition(AmbitionKind.Skill, skill, DefaultSkillGoal, 0);
        }

        if (HasAnyWord(text, HuntWords) && NamedPrey(text) is { } quarry)
        {
            return new Ambition(AmbitionKind.Kills, quarry, DefaultKillGoal, 0);
        }

        if (TryParseWantedThing(text, out var thing))
        {
            return new Ambition(AmbitionKind.Gold, thing, DefaultBoatGold, 0);
        }

        return new Ambition(AmbitionKind.Gold, NestEggTarget, DefaultNestEggGold, 0);
    }

    /// <summary>
    /// Fill in live progress. If the goal is already met, pick the next fitting want
    /// instead of celebrating a seed that finished the moment it was written.
    /// </summary>
    public static Ambition FinalizeSeed(Ambition seed, int skillValue, string background, int nextSeed)
    {
        if (seed.Kind != AmbitionKind.Skill)
        {
            return seed;
        }

        var withSkill = WithProgress(seed, skillValue);

        if (!IsComplete(withSkill))
        {
            return withSkill;
        }

        return NextAfterComplete(withSkill, nextSeed, background);
    }

    /// <summary>
    /// True when a saved ambition is the plain fallback nobody has worked on yet, and the
    /// character's own background now yields something real. An older build seeded most
    /// characters with that fallback, and it was saved; this lets them pick up the goal
    /// their persona actually describes. Real progress is never thrown away.
    /// </summary>
    public static bool ShouldReseed(Ambition saved, Ambition fromBackground) =>
        saved.Kind == AmbitionKind.Gold &&
        string.Equals(saved.Target, NestEggTarget, StringComparison.OrdinalIgnoreCase) &&
        saved.Progress == 0 &&
        !string.Equals(fromBackground.Target, NestEggTarget, StringComparison.OrdinalIgnoreCase);

    public static double Fraction(Ambition a)
    {
        if (a.Goal <= 0)
        {
            return 0;
        }

        var raw = (double)a.Progress / a.Goal;
        if (raw < 0)
        {
            return 0;
        }

        if (raw > 1)
        {
            return 1;
        }

        return raw;
    }

    public static bool IsComplete(Ambition a) => a.Goal > 0 && a.Progress >= a.Goal;

    public static bool IsNearDone(Ambition a) => Fraction(a) >= NearDoneFraction;

    public static Ambition WithProgress(Ambition a, int progress)
    {
        var max = Math.Max(0, a.Goal);
        var clamped = Math.Clamp(progress, 0, max);
        return a with { Progress = clamped };
    }

    /// <summary>
    /// What a character wants after it gets what it wanted. The seed is the character's
    /// own, so two characters finishing the same kind of goal go different ways.
    /// </summary>
    public static Ambition NextAfterComplete(Ambition done, int seed, string background)
    {
        var pick = seed & int.MaxValue;
        var town = Towns[pick % Towns.Length];
        var trade = TradeSkillOf(background);

        return done.Kind switch
        {
            AmbitionKind.Gold => pick % 2 == 0
                ? new Ambition(AmbitionKind.Gear, GearPlate, DefaultGearScore, 0)
                : new Ambition(AmbitionKind.Place, town, PlaceGoal, 0),
            AmbitionKind.Skill => WorkerNext(trade, town, pick),
            AmbitionKind.Place => pick % 2 == 0
                ? new Ambition(AmbitionKind.Kills, CreatureTroll, DefaultKillGoal, 0)
                : new Ambition(AmbitionKind.Gold, NestEggTarget, DefaultNestEggGold, 0),
            AmbitionKind.Kills => pick % 2 == 0
                ? new Ambition(AmbitionKind.Gear, GearPlate, DefaultGearScore, 0)
                : new Ambition(AmbitionKind.Place, town, PlaceGoal, 0),
            _ => new Ambition(AmbitionKind.Place, town, PlaceGoal, 0)
        };
    }

    private static Ambition WorkerNext(string trade, string town, int pick)
    {
        if (trade is SkillFishing or SkillLumberjacking or SkillMining)
        {
            return pick % 2 == 0
                ? new Ambition(AmbitionKind.Gold, NestEggTarget, DefaultNestEggGold, 0)
                : new Ambition(AmbitionKind.Place, town, PlaceGoal, 0);
        }

        return pick % 2 == 0
            ? new Ambition(AmbitionKind.Place, town, PlaceGoal, 0)
            : new Ambition(AmbitionKind.Kills, CreatureSkeleton, DefaultKillGoal, 0);
    }

    private static string TradeSkillOf(string background)
    {
        var text = background ?? string.Empty;

        for (var i = 0; i < TradeSkills.Length; i++)
        {
            if (HasWord(text, TradeSkills[i].Word))
            {
                return TradeSkills[i].Skill;
            }
        }

        return null;
    }

    public static string Describe(Ambition a) =>
        a.Kind switch
        {
            AmbitionKind.Gold => $"saving {a.Progress} of {a.Goal} gold for {a.Target}",
            AmbitionKind.Skill => $"training {a.Target} to {a.Goal} (at {a.Progress})",
            AmbitionKind.Gear => $"gathering gear toward {a.Target} ({a.Progress} of {a.Goal})",
            AmbitionKind.Kills => $"hunting {a.Target} ({a.Progress} of {a.Goal})",
            AmbitionKind.Place => $"traveling to {a.Target} ({a.Progress} of {a.Goal})",
            _ => DescribeNone
        };

    public static string MoodHint(Ambition a)
    {
        if (IsComplete(a))
        {
            return MoodDone;
        }

        var fraction = Fraction(a);
        if (fraction >= NearDoneFraction)
        {
            return MoodAlmostThere;
        }

        if (fraction <= 0)
        {
            return MoodJustStarted;
        }

        return MoodWellAlong;
    }

    public static bool PrefersWork(Ambition a) =>
        a.Kind is AmbitionKind.Gold or AmbitionKind.Gear ||
        (a.Kind is AmbitionKind.Skill && !IsFightSkill(a.Target));

    public static bool PrefersHunt(Ambition a) =>
        a.Kind is AmbitionKind.Kills ||
        (a.Kind is AmbitionKind.Skill && IsFightSkill(a.Target));

    /// <summary>A weapon skill rises in a fight, not at a work site.</summary>
    public static bool IsFightSkill(string skill) => skill is SkillSwords or SkillArchery;

    /// <summary>The skill a Skill ambition names, or null for any other ambition.</summary>
    public static string SkillOf(Ambition a) => a.Kind is AmbitionKind.Skill ? a.Target : null;

    public static bool PrefersTravel(Ambition a) => a.Kind is AmbitionKind.Place;

    /// <summary>
    /// What a character says out loud about its ambition. This is dialogue, so it must read
    /// like speech. <see cref="Describe"/> keeps the exact numbers for logs and the model.
    /// </summary>
    public static string TalkLine(Ambition a)
    {
        var left = Math.Max(0, a.Goal - a.Progress);
        var done = IsComplete(a);
        var near = !done && IsNearDone(a);

        return a.Kind switch
        {
            AmbitionKind.Gold => done ? $"I finally have enough for {a.Target}."
                : near ? $"Almost enough for {a.Target} now."
                : $"Still saving for {a.Target}. {left} gold to go.",
            AmbitionKind.Skill => done ? $"I have got my {SpokenSkill(a.Target)} where I wanted it."
                : near ? $"My {SpokenSkill(a.Target)} is nearly there."
                : $"Working on my {SpokenSkill(a.Target)} every day.",
            AmbitionKind.Kills => done ? $"That is {a.Goal} {a.Target}s down."
                : $"Out after {a.Target}s. {left} still to go.",
            AmbitionKind.Place => done ? $"I finally made it to {a.Target}."
                : $"One day I want to see {a.Target}.",
            AmbitionKind.Gear => done ? $"I finally have my {a.Target}."
                : $"Saving up for {a.Target}.",
            _ => string.Empty
        };
    }

    // Skill names are the game's own identifiers. "my Swords" is not how anyone talks.
    private static string SpokenSkill(string skill) =>
        skill switch
        {
            SkillSwords => "swordwork",
            SkillLumberjacking => "woodcutting",
            SkillMining => "mining",
            SkillFishing => "fishing",
            SkillArchery => "archery",
            SkillMagery => "magic",
            _ => skill?.ToLowerInvariant() ?? string.Empty
        };

    private static bool TryParseSavingFor(string background, out string target)
    {
        target = string.Empty;
        var index = background.IndexOf(SavingForMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var start = index + SavingForMarker.Length;
        if (start >= background.Length)
        {
            return false;
        }

        var end = background.IndexOf('.', start);
        var raw = end < 0 ? background[start..] : background[start..end];
        target = raw.Trim();
        return target.Length > 0;
    }

    private static bool TryParseWantedThing(string background, out string target)
    {
        target = string.Empty;

        for (var i = 0; i < WantLeads.Length; i++)
        {
            var index = background.IndexOf(WantLeads[i], StringComparison.OrdinalIgnoreCase);

            if (index < 0 || (index > 0 && char.IsLetter(background[index - 1])))
            {
                continue;
            }

            var end = background.IndexOf('.', index);
            var sentenceEnd = end < 0 ? background.Length : end;

            if (ThingStart(background, index + WantLeads[i].Length, sentenceEnd) is not { } start)
            {
                continue;
            }

            target = background[start..sentenceEnd].Trim();
            return target.Length > 0;
        }

        return false;
    }

    // Where the wanted thing starts: the first article within a few words, in the sentence.
    private static int? ThingStart(string text, int from, int sentenceEnd)
    {
        var at = from;

        for (var words = 0; words <= MaxWordsBeforeThing && at < sentenceEnd; words++)
        {
            if (StartsWithArticle(text, at))
            {
                return at;
            }

            var space = text.IndexOf(' ', at);

            if (space < 0 || space >= sentenceEnd)
            {
                return null;
            }

            at = space + 1;
        }

        return null;
    }

    private static bool StartsWithArticle(string text, int start)
    {
        for (var i = 0; i < Articles.Length; i++)
        {
            if (string.Compare(text, start, Articles[i], 0, Articles[i].Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string NamedTown(string text)
    {
        for (var i = 0; i < Towns.Length; i++)
        {
            if (HasWord(text, Towns[i]))
            {
                return Towns[i];
            }
        }

        return null;
    }

    private static string NamedPrey(string text)
    {
        for (var i = 0; i < PreyNames.Length; i++)
        {
            if (HasWord(text, PreyNames[i].Word))
            {
                return PreyNames[i].Role;
            }
        }

        return null;
    }

    private static bool HasAnyWord(string text, string[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            if (HasWord(text, words[i]))
            {
                return true;
            }
        }

        return false;
    }

    // A word counts only at the start of a word, so "mage" does not match inside "image".
    private static bool HasWord(string text, string word)
    {
        var index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            if (index == 0 || !char.IsLetter(text[index - 1]))
            {
                return true;
            }

            index = text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
