using System;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>What a pickpocket reaches for. The class of a light item that sets its worth.</summary>
public enum LootKind
{
    Other,
    Gold,
    Reagent,
    Scroll,
    Potion,
    Gem,
    Magic,
    Jewel,
    Gear
}

/// <summary>
/// The era's pickpocket: empty hands, a mark in a crowd standing clear of the town's NPCs, a
/// moment beside it, a peek into the pack, and the best light thing worth the risk. The lift
/// itself is the engine's Stealing skill, which reveals the thief and flags it when the mark
/// notices; a clean lift is followed by a hide and a few quiet steps away. A robbed person
/// needs a moment to shout, and the guards come only for a thief still in their reach: every
/// caught thief under the guards died in the same second as the lift. Pure except the pack scan.
/// </summary>
public static class StealRules
{
    public const int ReachTiles = 1;

    /// <summary>A thief looks for marks this far out.</summary>
    public const int MarkRange = 10;

    /// <summary>A thief works a crowd: this many people besides itself.</summary>
    public const int MarkCrowd = 2;

    /// <summary>A noticed thief runs this far from the mark before it tries to hide.</summary>
    public const int GapTiles = 8;

    /// <summary>
    /// With the engine's Hiding and this much Stealth a thief creeps up hidden; below, it walks
    /// up. A thief with less Hiding hid, took one step and stood revealed.
    /// </summary>
    public const double CreepStealth = 30;

    public const int GoldWorth = 1;
    public const int ReagentWorth = 3;
    public const int ScrollWorth = 20;
    public const int PotionWorth = 25;
    public const int GemWorth = 50;
    public const int MagicWorth = 300;
    public const int JewelWorth = 400;
    public const int OtherWorth = 5;

    /// <summary>Plain gear: a hunter pawns it, a thief leaves it alone for less than a gem.</summary>
    public const int GearWorth = 10;

    /// <summary>A real player is the audience: a thief would sooner rob one than a character.</summary>
    public const int HumanMarkFactor = 3;

    public const int CharacterMarkFactor = 1;

    /// <summary>The engine's guards come for a criminal this close to the one who calls them.</summary>
    public const int VictimCallReach = 14;

    /// <summary>An item without a stack count is one thing.</summary>
    public const int SingleItem = 1;

    /// <summary>Nothing worth less than this is lifted: nobody risked a guard whack for a torch.</summary>
    public const int WorthFloor = 15;

    /// <summary>
    /// A town NPC this close to the mark yells for the guards the moment the thief flags. A
    /// mark standing by the banker is a hot mark: most thieves leave it, the bold ones gamble.
    /// </summary>
    public const int NpcHeatRange = 8;

    /// <summary>Only a thief with this much Stealing works a hot mark, and only now and then.</summary>
    public const double BoldStealing = 70;

    public const int HotMarkPercent = 20;

    /// <summary>A thief that got no look into the pack makes a blind grab this often, else walks off.</summary>
    public const int BlindGrabPercent = 50;

    /// <summary>
    /// A thief peeks this many times at most before it grabs blind or walks off. A failed peek
    /// is no alarm by itself, so a player tried again: with one peek, 219 thieves in a night
    /// walked off "could not get a look in the pack".
    /// </summary>
    public const int MaxPeeks = 3;

    /// <summary>A thief that walked up in the open hides after a clean lift this often.</summary>
    public const int HideAfterPercent = 50;

    /// <summary>A caught thief's mark shouts it out (or calls the guards) this often.</summary>
    public const int VictimCallsPercent = 75;

    public const string FlaggedReason = "dead or flagged criminal";
    public const string DiedReason = "the thief died";
    public const string NoMarkReason = "no mark worth the risk in reach";
    public const string MarkGoneReason = "the mark got away";
    public const string ApproachTooLongReason = "could not get beside the mark in time";
    public const string NoLiftReason = "the lift never came in time";
    public const string NoLookReason = "could not get a look in the pack";
    public const string NothingWorthReason = "nothing in the pack worth the risk";
    public const string MissedReason = "the lift missed";

    // The tile names mark a plural as %plural% or %plural/singular%.
    private const char PluralMark = '%';
    private const char SingularSplit = '/';

    /// <summary>A Snooping skill this high always gets a look.</summary>
    public const double SureSnoop = 100;

    /// <summary>After a clean lift the thief slips this many tiles away before anything else.</summary>
    public const int SlipTiles = 5;

    public static readonly TimeSpan ApproachLimit = TimeSpan.FromSeconds(25);

    /// <summary>A failed peek is tried again after this pause, the time to click the pack once more.</summary>
    public static readonly TimeSpan PeekGap = TimeSpan.FromSeconds(2);

    /// <summary>A thief beside its mark stands a moment first: walking up and lifting in one breath gets noticed.</summary>
    public static readonly TimeSpan CaseMin = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan CaseMax = TimeSpan.FromSeconds(4);

    /// <summary>The quiet steps away after a clean lift end by this time, arrived or not.</summary>
    public static readonly TimeSpan SlipLimit = TimeSpan.FromSeconds(12);

    /// <summary>A mark tried, lifted from or not, is left alone this long by that thief.</summary>
    public static readonly TimeSpan MarkRest = TimeSpan.FromMinutes(4);
    public static readonly TimeSpan EscapeLimit = TimeSpan.FromSeconds(40);

    /// <summary>A criminal flag lasts two minutes; the thief lays low a little longer.</summary>
    public static readonly TimeSpan LayLowLimit = TimeSpan.FromSeconds(150);

    /// <summary>A thief works one crowd, then lets it forget its face.</summary>
    public static readonly TimeSpan Rest = TimeSpan.FromMinutes(6);

    /// <summary>
    /// A thief in a crowd with no mark worth the risk looks again this soon. A thief that
    /// found none rested six minutes: 56 of 97 starts in one run ended before a step.
    /// </summary>
    public static readonly TimeSpan LookGapMin = TimeSpan.FromSeconds(6);

    public static readonly TimeSpan LookGapMax = TimeSpan.FromSeconds(18);

    /// <summary>A robbed person shouts this long after the lift at the least, <see cref="VictimCallMax"/> at most.</summary>
    public static readonly TimeSpan VictimCallMin = TimeSpan.FromSeconds(1.5);

    public static readonly TimeSpan VictimCallMax = TimeSpan.FromSeconds(4);

    public static bool MayBegin(SosariaCharacter thief) => thief != null && thief.Alive && !thief.Criminal;

    /// <summary>A snoop begins in the world and alive; a criminal may still peek.</summary>
    public static bool MaySnoop(SosariaCharacter snooper) => People.InWorld(snooper) && snooper.Alive;

    /// <summary>
    /// A thief works no crowd under the guards: a caught lift turns it gray, and any townsperson
    /// near calls the guards at once. 50 of 53 thieves caught at the Britain bank died to them,
    /// 191 of 243 guard deaths on the shard. It works the Den, the roads and the dungeons.
    /// </summary>
    public static bool MayWorkCrowd(bool underGuards) => !underGuards;

    /// <summary>
    /// The engine lets only a Thieves Guild member lift from a person, and with the era's rule
    /// on it suspends a member who has murdered: such a thief would fail every lift it tried.
    /// </summary>
    public static bool MayLiftFromPeople(bool inThievesGuild, int kills, bool suspendOnMurder) =>
        inThievesGuild && (!suspendOnMurder || kills <= 0);

    /// <summary>A mark with a town NPC near: only a bold thief tries it, and only on some looks.</summary>
    public static bool TakesHotMark(double stealing, int roll100) =>
        stealing >= BoldStealing && roll100 >= 0 && roll100 < HotMarkPercent;

    /// <summary>The peek works as often as the Snooping skill says.</summary>
    public static bool GotALook(double snooping, int roll100) => snooping >= SureSnoop || roll100 < snooping;

    public static bool BlindGrab(int roll100) => roll100 >= 0 && roll100 < BlindGrabPercent;

    /// <summary>A thief that got no look peeks again while it has peeks left.</summary>
    public static bool PeeksAgain(int peeks) => peeks < MaxPeeks;

    /// <summary>
    /// A mark the thief can stand beside: a free tile next to it. A mark boxed in by the bank
    /// crowd could not be reached, and 294 thieves in a night gave up "could not get beside
    /// the mark in time" four tiles from where they began.
    /// </summary>
    public static bool HasRoomBeside(int freeNeighbours) => freeNeighbours > 0;

    public static bool HidesAfter(bool sneaked, int roll100) => sneaked || roll100 >= 0 && roll100 < HideAfterPercent;

    public static bool VictimCalls(int roll100) => roll100 >= 0 && roll100 < VictimCallsPercent;

    /// <summary>The guards reach a thief this many tiles from its caller; one that ran farther got away.</summary>
    public static bool VictimReaches(int distance) => distance >= 0 && distance <= VictimCallReach;

    /// <summary>The same mark again only after its rest.</summary>
    public static bool MarkRested(DateTime triedAt, DateTime now) => triedAt == default || now - triedAt >= MarkRest;

    /// <summary>Beside the mark: in reach of a lift, and of a peek into its pack.</summary>
    public static bool InReach(Point3D from, Point3D to) => NavMetric.Chebyshev(from, to) <= ReachTiles;

    public static bool ShouldCreep(double hidingBase, double stealth, double hidingRequirement, int armorRating) =>
        StealthRules.MayCreep(hidingBase, hidingRequirement, armorRating) && stealth >= CreepStealth;

    public static int Worth(LootKind kind, int amount) =>
        Math.Max(SingleItem, amount) * kind switch
        {
            LootKind.Gold => GoldWorth,
            LootKind.Reagent => ReagentWorth,
            LootKind.Scroll => ScrollWorth,
            LootKind.Potion => PotionWorth,
            LootKind.Gem => GemWorth,
            LootKind.Magic => MagicWorth,
            LootKind.Jewel => JewelWorth,
            LootKind.Gear => GearWorth,
            _ => OtherWorth
        };

    /// <summary>A mark is worth the walk by what it carries, less the tiles to reach it; a human mark counts more.</summary>
    public static double MarkScore(int worth, int distance, bool human) =>
        worth * (human ? HumanMarkFactor : CharacterMarkFactor) / (1.0 + Math.Max(0, distance));

    /// <summary>
    /// A tile name read for <paramref name="amount"/>: "Black Pearl%s%" is "Black Pearls" for
    /// many and "Black Pearl" for one; "loa%ves/f%" is "loaves" or "loaf". An unclosed mark is left as it is.
    /// </summary>
    public static string ItemWord(string tileName, int amount)
    {
        var open = tileName?.IndexOf(PluralMark) ?? -1;
        var close = open < 0 ? -1 : tileName.IndexOf(PluralMark, open + 1);

        if (close < 0)
        {
            return tileName;
        }

        var mark = tileName.Substring(open + 1, close - open - 1);
        var split = mark.IndexOf(SingularSplit);
        var plural = split < 0 ? mark : mark[..split];
        var singular = split < 0 ? string.Empty : mark[(split + 1)..];
        var word = tileName[..open] + (amount > SingleItem ? plural : singular) + tileName[(close + 1)..];
        return ItemWord(word, amount);
    }

    public static LootKind KindOf(Item item) =>
        item switch
        {
            Gold => LootKind.Gold,
            BaseJewel => LootKind.Jewel,
            BaseWeapon weapon when weapon.DamageLevel != WeaponDamageLevel.Regular ||
                                   weapon.AccuracyLevel != WeaponAccuracyLevel.Regular => LootKind.Magic,
            BaseArmor armor when armor.ProtectionLevel != ArmorProtectionLevel.Regular => LootKind.Magic,
            BaseWeapon or BaseArmor or BaseClothing or BaseInstrument or BaseHides => LootKind.Gear,
            BasePotion => LootKind.Potion,
            SpellScroll => LootKind.Scroll,
            BaseReagent => LootKind.Reagent,
            _ when IsGem(item) => LootKind.Gem,
            _ => LootKind.Other
        };

    /// <summary>
    /// The most valuable item the engine would let a thief lift from the top of this pack:
    /// movable, not blessed or newbied, not a container, light enough, and worth at least
    /// <see cref="WorthFloor"/>. Ties keep the first.
    /// </summary>
    public static Item PickLoot(Container pack, int maxWeight)
    {
        if (pack == null)
        {
            return null;
        }

        Item chosen = null;
        var best = WorthFloor - 1;

        foreach (var item in pack.Items)
        {
            if (item == null || item.Deleted || !item.Movable || item is Container ||
                item.LootType is LootType.Blessed or LootType.Newbied ||
                item.Weight + item.TotalWeight > maxWeight)
            {
                continue;
            }

            var worth = Worth(KindOf(item), item.Amount);

            if (worth > best)
            {
                best = worth;
                chosen = item;
            }
        }

        return chosen;
    }

    private static bool IsGem(Item item) =>
        item is Amber or Amethyst or Citrine or Diamond or Emerald or Ruby or Sapphire or StarSapphire or Tourmaline;
}
