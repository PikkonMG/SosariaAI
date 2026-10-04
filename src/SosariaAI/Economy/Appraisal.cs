using System;
using System.Collections.Generic;
using System.Text;
using Server;
using Server.Items;

namespace SosariaAI.Economy;

/// <summary>
/// The one market table. A hawker's ask, a buyer's offer and a WTB shout all price goods here,
/// so the number a character names in words is the number it holds to when the item is on
/// the table. A plain weapon off a vendor shelf was near worthless at a 1999 bank; a smith's
/// exceptional piece sold for hundreds; a vanquishing weapon or an invulnerability suit sold
/// for thousands. Reagents, bandages and ammunition went by the hundred. Our own numbers.
/// </summary>
public static class Appraisal
{
    public const int PercentScale = 100;

    /// <summary>The middle of a band, for comparing goods and for a buyer who has not seen the item.</summary>
    public const int MidRoll = PercentScale / 2;

    public const int ExceptionalMultiplier = 8;
    public const int NoMagic = 0;
    public const int MaxMagicLevel = 5;

    // Level 1 (ruin, defense) through 5 (vanquishing, invulnerability).
    private static readonly int[] MagicMultipliers = [1, 4, 8, 16, 30, 60];

    private static readonly string[] ExceptionalWords = ["gm", "exceptional", "exc", "gmd", "gm made"];

    private static readonly string[] WeaponMagicWords = ["", "ruin", "might", "force", "power", "vanq"];
    private static readonly string[] ArmorMagicWords = ["", "defense", "guarding", "hardening", "fort", "invul"];

    // Every typed spelling of a magic level, with the level it means.
    private static readonly (string Word, int Level)[] WeaponMagicSpellings =
    [
        ("ruin", 1), ("might", 2), ("force", 3), ("power", 4), ("vanq", 5), ("vanquishing", 5)
    ];

    private static readonly (string Word, int Level)[] ArmorMagicSpellings =
    [
        ("defense", 1), ("guarding", 2), ("hardening", 3), ("fort", 4), ("fortification", 4),
        ("invul", 5), ("invuln", 5), ("invulnerability", 5)
    ];

    public const string ReagentsKey = "reagents";
    public const string RecallKey = "recall";
    public const string BandagesKey = "bandages";
    public const string ArrowsKey = "arrows";
    public const string BoltsKey = "bolts";
    public const string RunebookKey = "runebooks";
    public const string ToolsKey = "tools";

    private const char Space = ' ';
    private const string MakerMark = "GM";

    public static readonly GoodsRow Other = new(
        "goods", "goods", GoodsKind.Stack, 5, 40, 1, TradeAppetite.None, 10, [], _ => true
    );

    /// <summary>The rows, most specific first: a kryss is a sword to the engine but fencing to a buyer.</summary>
    public static readonly IReadOnlyList<GoodsRow> Rows =
    [
        Gear("polearm", "halberd", GoodsKind.Weapon, 40, 110, TradeAppetite.Melee, 30,
            ["halberd", "halberds", "hally", "halb", "bardiche", "bardiches", "scythe"], i => i is BasePoleArm),
        Gear("fencing", "kryss", GoodsKind.Weapon, 35, 95, TradeAppetite.Fencing, 30,
            ["kryss", "spear", "spears", "short spear", "war fork", "warfork", "pitchfork"],
            i => i is Kryss or BaseSpear),
        Gear("axe", "axe", GoodsKind.Weapon, 35, 100, TradeAppetite.Melee, 30,
            ["axe", "axes", "battle axe", "double axe", "exe axe", "executioners axe", "large battle axe", "lba",
             "two handed axe", "war axe"], i => i is BaseAxe),
        Gear("sword", "katana", GoodsKind.Weapon, 35, 100, TradeAppetite.Melee, 30,
            ["sword", "swords", "katana", "katanas", "broadsword", "broadswords", "longsword", "long sword",
             "viking sword", "vsword", "cutlass", "scimitar"], i => i is BaseSword),
        Gear("mace", "war mace", GoodsKind.Weapon, 35, 100, TradeAppetite.Melee, 30,
            ["mace", "maces", "war mace", "war hammer", "warhammer", "hammer pick", "maul", "club"],
            i => i is BaseBashing),
        Gear("staff", "quarterstaff", GoodsKind.Weapon, 20, 60, TradeAppetite.Caster | TradeAppetite.Melee, 20,
            ["staff", "quarterstaff", "qstaff", "black staff", "bstaff", "gnarled staff"], i => i is BaseStaff),
        Gear("knife", "dagger", GoodsKind.Weapon, 8, 25, TradeAppetite.Everyone, 15,
            ["dagger", "daggers", "knife", "cleaver", "butcher knife", "skinning knife"], i => i is BaseKnife),
        Gear("bow", "bow", GoodsKind.Weapon, 40, 110, TradeAppetite.Archery, 30,
            ["bow", "bows", "crossbow", "xbow", "heavy crossbow", "heavy xbow", "hxbow"], i => i is BaseRanged),
        Gear("shield", "heater", GoodsKind.Armor, 35, 95, TradeAppetite.Melee, 30,
            ["shield", "shields", "heater", "heater shield", "kite", "kite shield", "metal kite", "buckler",
             "bronze shield", "metal shield", "wooden shield", "tear kite"], i => i is BaseShield),
        Gear("plate", "plate", GoodsKind.Armor, 60, 140, TradeAppetite.Melee, 35,
            ["plate", "platemail", "plate mail", "plate tunic", "plate chest", "plate legs", "plate arms",
             "plate gloves", "gorget", "plate gorget", "plate helm", "full plate", "plate set", "helm", "helmet",
             "norse helm", "close helm", "bascinet"], i => Material(i, ArmorMaterialType.Plate)),
        Gear("chain", "chainmail", GoodsKind.Armor, 45, 110, TradeAppetite.Melee, 30,
            ["chain", "chainmail", "chain mail", "chain tunic", "chain legs", "chain coif", "coif"],
            i => Material(i, ArmorMaterialType.Chainmail)),
        Gear("ring", "ringmail", GoodsKind.Armor, 35, 85, TradeAppetite.Melee | TradeAppetite.Fencing, 25,
            ["ringmail", "ring mail", "ring tunic", "ring legs", "ring arms", "ring gloves"],
            i => Material(i, ArmorMaterialType.Ringmail)),
        Gear("studded", "studded", GoodsKind.Armor, 25, 70, TradeAppetite.Archery | TradeAppetite.Fencing, 25,
            ["studded", "studded leather", "studded tunic", "studded set"],
            i => Material(i, ArmorMaterialType.Studded)),
        Gear("leather", "leather armor", GoodsKind.Armor, 15, 45, TradeAppetite.Archery | TradeAppetite.Caster, 20,
            ["leather armor", "leather tunic", "leather legs", "leather cap", "leather gloves", "leather set"],
            i => Material(i, ArmorMaterialType.Leather)),
        Gear("bone", "bone armor", GoodsKind.Armor, 30, 75, TradeAppetite.Melee | TradeAppetite.Archery, 20,
            ["bone armor", "bone set", "bone helm", "bone chest", "bone legs", "bone arms"],
            i => Material(i, ArmorMaterialType.Bone)),
        Stack(ReagentsKey, "regs", 3, 7, 100, TradeAppetite.Caster, 90,
            ["regs", "reags", "reagents", "reagent", "black pearl", "pearl", "bp", "blood moss", "bm",
             "sulfurous ash", "sulf ash", "ash", "sa", "mandrake", "mandrake root", "mr", "garlic", "ginseng",
             "gins", "nightshade", "ns", "spiders silk", "spider silk", "silk", "ss"], i => i is BaseReagent),
        Stack(RecallKey, "recall scrolls", 12, 25, 10, TradeAppetite.Caster, 80,
            ["recall", "recalls", "recall scroll", "recall scrolls"], i => i is RecallScroll),
        Stack("scrolls", "scrolls", 15, 60, 5, TradeAppetite.Caster, 45,
            ["scroll", "scrolls", "gate", "gates", "gate scrolls", "mark scrolls", "ebolt", "energy bolt",
             "flamestrike", "fs"], i => i is SpellScroll),
        // A scribe's book of sixteen marks: bought by one who recalls and has no book yet.
        Stack(RunebookKey, "runebook", 100, 200, 1, TradeAppetite.Travel, 50,
            ["runebook", "runebooks", "rune book", "rbook"], i => i is Runebook),
        Stack("heal potions", "heal pots", 18, 35, 10, TradeAppetite.Everyone, 65,
            ["heal pots", "heal potions", "gheal", "gh", "greater heal", "heals"], i => i is BaseHealPotion),
        Stack("cure potions", "cure pots", 15, 30, 10, TradeAppetite.Everyone, 55,
            ["cure pots", "cure potions", "gcure", "greater cure", "cures"], i => i is BaseCurePotion),
        Stack("potions", "pots", 12, 30, 10, TradeAppetite.Everyone, 35,
            ["pots", "potions", "potion", "refresh", "refresh pots", "str pots", "agi pots", "explo",
             "explosion pots", "nightsight"], i => i is BasePotion),
        Stack(BandagesKey, "bandages", 1, 3, 100,
            TradeAppetite.Healing | TradeAppetite.Melee | TradeAppetite.Fencing | TradeAppetite.Archery, 80,
            ["bandages", "bandage", "bandies", "bands", "aids"], i => i is Bandage),
        Stack(ArrowsKey, "arrows", 1, 3, 200, TradeAppetite.Archery, 75, ["arrows", "arrow"], i => i is Arrow),
        Stack(BoltsKey, "bolts", 1, 3, 200, TradeAppetite.Archery, 70, ["bolts", "bolt"], i => i is Bolt),
        Stack("ingots", "ingots", 6, 12, 100, TradeAppetite.Smithing, 60,
            ["ingots", "ingot", "iron ingots", "iron"], i => i is BaseIngot),
        Stack("ore", "ore", 4, 9, 100, TradeAppetite.Smithing, 40, ["ore", "iron ore"], i => i is BaseOre),
        Stack("wood", "boards", 3, 6, 100, TradeAppetite.Carpentry, 50,
            ["logs", "log", "boards", "board", "wood", "lumber"], i => i is Log or Board),
        Stack("cloth", "cloth", 2, 5, 100, TradeAppetite.Tailoring, 45,
            ["cloth", "bolt of cloth", "bolts of cloth", "uncut cloth"], i => i is Cloth or UncutCloth or BoltOfCloth),
        Stack("hides", "leather", 4, 8, 100, TradeAppetite.Tailoring, 45,
            ["hides", "hide", "leather", "cut leather"], i => i is BaseLeather or BaseHides),
        // Only a character who can decode a map buys one; nobody else has a use for it.
        Stack("treasure maps", "tmap", 150, 600, 1, TradeAppetite.None, 30,
            ["tmap", "tmaps", "treasure map", "treasure maps"], i => i is TreasureMap { Completed: false }),
        Stack("sos", "sos", 80, 250, 1, TradeAppetite.None, 25,
            ["sos", "message in a bottle", "mib"], i => i is SOS or MessageInABottle),
        Stack("gems", "gems", 40, 120, 5, TradeAppetite.Everyone, 30,
            ["gems", "gem", "diamond", "diamonds", "ruby", "rubies", "sapphire", "sapphires", "star sapphire",
             "emerald", "emeralds", "amethyst", "citrine", "tourmaline", "amber"], IsGem),
        Stack("jewelry", "jewelry", 50, 180, 1, TradeAppetite.Everyone, 20,
            ["jewelry", "jewels", "bracelet", "necklace", "earrings", "gold ring", "silver ring", "gold bracelet"],
            i => i is BaseJewel),
        Stack("clothing", "clothes", 8, 40, 1, TradeAppetite.Everyone, 15,
            ["robe", "robes", "cloak", "cloaks", "sash", "shirt", "pants", "boots", "thigh boots", "shoes",
             "sandals", "hat", "skirt", "kilt", "surcoat"], i => i is BaseClothing),
        Stack("food", "food", 3, 8, 10, TradeAppetite.Everyone, 20,
            ["food", "fish", "meat", "bread", "ham", "ribs", "cheese", "apples"], i => i is Food),
        // A tinker's make: the smith, tailor and carpenter at work buy a spare off the bank floor.
        Stack(ToolsKey, "tools", 8, 30, 1, TradeAppetite.Smithing | TradeAppetite.Tailoring | TradeAppetite.Carpentry, 30,
            ["tools", "tool", "tongs", "tinker tools", "tinker kit", "sewing kit", "saw", "smith hammer", "froe",
             "draw knife"], i => i is BaseTool)
    ];

    public static int MagicMultiplier(int magicLevel) =>
        MagicMultipliers[Math.Clamp(magicLevel, NoMagic, MaxMagicLevel)];

    /// <summary>
    /// What <paramref name="amount"/> units are worth. The roll places the unit inside the band,
    /// so two sellers ask a little differently for the same goods. Said as a round number.
    /// </summary>
    public static int Value(GoodsRow row, int amount, bool exceptional, int magicLevel, int roll)
    {
        row ??= Other;
        var unit = row.UnitLow + (row.UnitHigh - row.UnitLow) * Math.Clamp(roll, 0, PercentScale - 1) / PercentScale;
        long total = (long)unit * Math.Max(1, amount);

        if (row.IsGear)
        {
            total *= MagicMultiplier(magicLevel) * (exceptional ? ExceptionalMultiplier : 1);
        }

        return GoldWords.RoundSpoken((int)Math.Min(int.MaxValue, total));
    }

    /// <summary>What a real item is worth, the whole stack.</summary>
    public static int Value(Item item, int roll) =>
        Value(RowOf(item), item?.Amount ?? 1, IsExceptional(item), MagicLevelOf(item), roll);

    public static GoodsRow RowOf(Item item)
    {
        if (item == null)
        {
            return Other;
        }

        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Holds(item))
            {
                return Rows[i];
            }
        }

        return Other;
    }

    public static GoodsRow RowByKey(string key)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (string.Equals(Rows[i].Key, key, StringComparison.Ordinal))
            {
                return Rows[i];
            }
        }

        return null;
    }

    /// <summary>What a real item is, as a claim: its row, count, mark and magic.</summary>
    public static GoodsClaim ClaimOf(Item item) =>
        new(RowOf(item), item?.Amount ?? 1, IsExceptional(item), MagicLevelOf(item));

    public static bool IsExceptional(Item item) =>
        item is BaseWeapon { Quality: WeaponQuality.Exceptional } or BaseArmor { Quality: ArmorQuality.Exceptional }
            or BaseClothing { Quality: ClothingQuality.Exceptional };

    public static int MagicLevelOf(Item item) =>
        item switch
        {
            BaseWeapon weapon => (int)weapon.DamageLevel,
            BaseArmor armor => (int)armor.ProtectionLevel,
            _ => NoMagic
        };

    /// <summary>
    /// Reads goods out of words: "wts gm hally 5k", "wtb 100 regs", "vanq katana". The longest
    /// phrase wins, so "heavy xbow" is not a plain bow and "bolt of cloth" is not ammunition.
    /// False when nothing in the table is named.
    /// </summary>
    public static bool TryRead(IReadOnlyList<string> words, out GoodsClaim claim, out int nounAt)
    {
        claim = default;
        nounAt = -1;

        if (words is not { Count: > 0 })
        {
            return false;
        }

        var padded = $"{Space}{string.Join(Space, words)}{Space}";
        GoodsRow best = null;
        var bestLength = 0;
        var bestIndex = -1;

        for (var r = 0; r < Rows.Count; r++)
        {
            foreach (var phrase in Rows[r].Words)
            {
                var index = padded.IndexOf($"{Space}{phrase}{Space}", StringComparison.Ordinal);

                if (index >= 0 && phrase.Length > bestLength)
                {
                    best = Rows[r];
                    bestLength = phrase.Length;
                    bestIndex = index;
                }
            }
        }

        if (best == null)
        {
            return false;
        }

        nounAt = WordIndexAt(padded, bestIndex);
        claim = new GoodsClaim(
            best,
            best.IsGear ? 1 : CountBefore(words, nounAt),
            best.IsGear && HasAny(padded, ExceptionalWords),
            best.IsGear ? MagicHeard(padded, best.Kind) : NoMagic
        );
        return true;
    }

    /// <summary>"GM halberd", "vanq halberd", "100 regs", "regs".</summary>
    public static string ClaimNoun(GoodsClaim claim)
    {
        var row = claim.Row ?? Other;
        return Named(row.IsGear ? 0 : claim.Amount, claim.Exceptional, row.Kind, claim.MagicLevel, row.Noun);
    }

    /// <summary>What a real item is called aloud: "GM halberd", "vanq katana", "20 iron ingots".</summary>
    public static string NounOf(Item item)
    {
        if (item == null)
        {
            return null;
        }

        var name = string.IsNullOrEmpty(item.Name) ? SplitWords(item.GetType().Name) : item.Name.ToLowerInvariant();
        var count = item.Amount > 1 ? item.Amount : 0;
        return Named(count, IsExceptional(item), RowOf(item).Kind, MagicLevelOf(item), name);
    }

    // The count when one was said, the maker's mark, the magic word, then the name.
    private static string Named(int count, bool exceptional, GoodsKind kind, int magicLevel, string name)
    {
        var builder = new StringBuilder();

        if (count > 0)
        {
            builder.Append(count).Append(Space);
        }

        if (exceptional)
        {
            builder.Append(MakerMark).Append(Space);
        }

        var magic = MagicWord(kind, magicLevel);

        if (magic.Length > 0)
        {
            builder.Append(magic).Append(Space);
        }

        return builder.Append(name).ToString();
    }

    private static string MagicWord(GoodsKind kind, int level)
    {
        var index = Math.Clamp(level, NoMagic, MaxMagicLevel);

        return kind switch
        {
            GoodsKind.Weapon => WeaponMagicWords[index],
            GoodsKind.Armor => ArmorMagicWords[index],
            _ => string.Empty
        };
    }

    private static int MagicHeard(string padded, GoodsKind kind)
    {
        var spellings = kind == GoodsKind.Weapon ? WeaponMagicSpellings : ArmorMagicSpellings;
        var level = NoMagic;

        foreach (var (word, value) in spellings)
        {
            if (value > level && padded.Contains($"{Space}{word}{Space}", StringComparison.Ordinal))
            {
                level = value;
            }
        }

        return level;
    }

    private static bool HasAny(string padded, string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (padded.Contains($"{Space}{phrase}{Space}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // "200 mandrake": a plain number right before the goods counts them. "5k" is money, never a count.
    private static int CountBefore(IReadOnlyList<string> words, int nounAt)
    {
        if (nounAt <= 0 || !GoldWords.TryRead(words[nounAt - 1], out var value, out var money) || money)
        {
            return 0;
        }

        return value;
    }

    private static int WordIndexAt(string padded, int charIndex)
    {
        var index = 0;

        // The padded line starts with a space; each space before the match starts one more word.
        for (var i = 1; i <= charIndex; i++)
        {
            if (padded[i] == Space)
            {
                index++;
            }
        }

        return index;
    }

    private static bool Material(Item item, ArmorMaterialType material) =>
        item is BaseArmor armor and not BaseShield && armor.MaterialType == material;

    private static bool IsGem(Item item) =>
        item is Amber or Amethyst or Citrine or Diamond or Emerald or Ruby or Sapphire or StarSapphire or Tourmaline;

    private static GoodsRow Gear(
        string key, string noun, GoodsKind kind, int low, int high, TradeAppetite appetite, int demand, string[] words,
        Func<Item, bool> holds
    ) => new(key, noun, kind, low, high, 1, appetite, demand, words, holds);

    private static GoodsRow Stack(
        string key, string noun, int low, int high, int lot, TradeAppetite appetite, int demand, string[] words,
        Func<Item, bool> holds
    ) => new(key, noun, GoodsKind.Stack, low, high, lot, appetite, demand, words, holds);

    /// <summary>A type name as words people say: "IronIngot" is "iron ingot".</summary>
    public static string SplitWords(string typeName)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < typeName.Length; i++)
        {
            var c = typeName[i];

            if (i > 0 && char.IsUpper(c))
            {
                builder.Append(Space);
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
