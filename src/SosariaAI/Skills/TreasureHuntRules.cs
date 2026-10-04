using System;

namespace SosariaAI.Skills;

/// <summary>What a cartographer does with this work step.</summary>
public enum CartographyTask
{
    /// <summary>Decode, walk to the chest, dig, fight, open and loot.</summary>
    Hunt,

    /// <summary>Hold up a map the character cannot work at the bank.</summary>
    Sell,

    /// <summary>Look for a map for sale that the character can work.</summary>
    Shop,

    /// <summary>Draw maps at the mapmaker through the engine craft system.</summary>
    Craft
}

/// <summary>How a treasure chest trap is dealt with.</summary>
public enum ChestTrapPlan
{
    None,

    /// <summary>Third circle Telekinesis from outside the blast: the trap goes off on nobody.</summary>
    Telekinesis,

    /// <summary>The Remove Trap skill at the chest.</summary>
    Disarm,

    /// <summary>Open it and take the blast.</summary>
    Accept,

    /// <summary>Too hurt to take the blast: heal first.</summary>
    Heal
}

/// <summary>How a locked treasure chest is opened.</summary>
public enum ChestOpening
{
    None,
    MagicUnlock,
    Lockpick
}

/// <summary>
/// T2A treasure hunting, as the engine plays it. The numbers mirror the engine's own checks
/// (TreasureMap decode and dig, TreasureMapChest fill, the Unlock spell, the Remove Trap
/// skill, the explosion trap) so a character only takes on a map it could really finish.
/// Pure. No world objects.
/// </summary>
public static class TreasureHuntRules
{
    public const int MinHuntLevel = 1;
    public const int MaxHuntLevel = 6;

    // TreasureMap.GetMinSkillLevel: the Cartography a level needs to decode or dig.
    public const double LevelOneDecodeSkill = -3.0;
    public const double LevelTwoDecodeSkill = 41.0;
    public const double LevelThreeDecodeSkill = 51.0;
    public const double LevelFourDecodeSkill = 61.0;
    public const double TopLevelDecodeSkill = 70.0;
    public const double NoDecodeSkill = 0.0;

    /// <summary>TreasureMap.Decode rolls Cartography from the level's minimum to this much above it.</summary>
    public const double DecodeSkillSpan = 60.0;

    // TreasureMapChest.Fill: the Lockpicking a level's lock needs.
    public const int LevelOneLockSkill = 36;
    public const int LevelTwoLockSkill = 76;
    public const int LevelThreeLockSkill = 84;
    public const int LevelFourLockSkill = 92;
    public const int TopLevelLockSkill = 100;

    // UnlockSpell: (int)(Magery * 0.8) - 4 must reach the lock, and never above level 2.
    public const double UnlockMageryShare = 0.8;
    public const int UnlockMageryOffset = 4;
    public const int MagicUnlockMaxLevel = 2;

    // Remove Trap skill: both Lockpicking and Detect Hidden at 50 before the cursor comes up.
    public const double DisarmMinLockpicking = 50.0;
    public const double DisarmMinDetectHidden = 50.0;

    /// <summary>Telekinesis is third circle; about half the casts take at this Magery.</summary>
    public const double TelekinesisMinMagery = 40.0;

    // The explosion trap: 10 to 30 damage times the trap level to anyone within 3 tiles.
    public const int TrapDamagePerLevelMax = 30;
    public const int TrapBlastRange = 3;

    /// <summary>Hit points kept in hand above the worst blast before a character opens a trapped chest.</summary>
    public const int TrapHitsMargin = 10;

    /// <summary>Telekinesis is aimed from this far: outside the blast, well inside spell range.</summary>
    public const int TelekinesisStandOff = TrapBlastRange + 2;

    /// <summary>Out of a hundred, how often a mapless cartographer with coin looks for a map before drawing one.</summary>
    public const int ShopPercent = 35;

    /// <summary>The least pack coin worth a trip to look at maps for sale.</summary>
    public const int MinShopPurse = 100;

    public static bool IsHuntLevel(int level) => level is >= MinHuntLevel and <= MaxHuntLevel;

    public static double DecodeMinSkill(int level) =>
        level switch
        {
            1 => LevelOneDecodeSkill,
            2 => LevelTwoDecodeSkill,
            3 => LevelThreeDecodeSkill,
            4 => LevelFourDecodeSkill,
            5 or 6 => TopLevelDecodeSkill,
            _ => NoDecodeSkill
        };

    /// <summary>The engine's decode roll fails outright below the level's minimum.</summary>
    public static bool CanDecode(int level, double cartography) => cartography >= DecodeMinSkill(level);

    /// <summary>The engine lets the decoder dig, or anyone with the level's Cartography.</summary>
    public static bool MayDig(int level, bool decodedBySelf, double cartography) =>
        decodedBySelf || CanDecode(level, cartography);

    public static int LockSkill(int level) =>
        level switch
        {
            1 => LevelOneLockSkill,
            2 => LevelTwoLockSkill,
            3 => LevelThreeLockSkill,
            4 => LevelFourLockSkill,
            _ => TopLevelLockSkill
        };

    public static bool CanPick(int level, double lockpicking) => lockpicking >= LockSkill(level);

    public static bool CanMagicUnlock(int level, double magery) =>
        level <= MagicUnlockMaxLevel && (int)(magery * UnlockMageryShare) - UnlockMageryOffset >= LockSkill(level);

    /// <summary>The spell first: it burns reagents, not lockpicks.</summary>
    public static ChestOpening OpenBy(int level, double lockpicking, double magery) =>
        CanMagicUnlock(level, magery) ? ChestOpening.MagicUnlock :
        CanPick(level, lockpicking) ? ChestOpening.Lockpick :
        ChestOpening.None;

    public static bool CanDisarm(double lockpicking, double detectHidden) =>
        lockpicking >= DisarmMinLockpicking && detectHidden >= DisarmMinDetectHidden;

    public static int MaxTrapDamage(int trapLevel) => TrapDamagePerLevelMax * Math.Max(0, trapLevel);

    public static bool CanTakeTrap(int hits, int trapLevel) => hits > MaxTrapDamage(trapLevel) + TrapHitsMargin;

    public static ChestTrapPlan TrapPlan(bool trapped, bool canTelekinesis, bool canDisarm, int hits, int trapLevel)
    {
        if (!trapped)
        {
            return ChestTrapPlan.None;
        }

        if (canTelekinesis)
        {
            return ChestTrapPlan.Telekinesis;
        }

        if (canDisarm)
        {
            return ChestTrapPlan.Disarm;
        }

        return CanTakeTrap(hits, trapLevel) ? ChestTrapPlan.Accept : ChestTrapPlan.Heal;
    }

    /// <summary>
    /// A map worth taking on: a level characters may dig, on the facet the character lives
    /// and stands on, one it may dig, and a chest it can open once it is up.
    /// </summary>
    public static bool MayHunt(
        int level,
        bool rightFacet,
        bool decodedBySelf,
        double cartography,
        double lockpicking,
        double magery
    ) =>
        IsHuntLevel(level) && rightFacet && MayDig(level, decodedBySelf, cartography) &&
        OpenBy(level, lockpicking, magery) != ChestOpening.None;

    public static CartographyTask Choose(bool hasMap, bool mayHunt, bool offerNearby, int purse, int roll)
    {
        if (hasMap)
        {
            return mayHunt ? CartographyTask.Hunt : CartographyTask.Sell;
        }

        if (offerNearby)
        {
            return CartographyTask.Shop;
        }

        return purse >= MinShopPurse && PercentRoll.Under(roll, ShopPercent)
            ? CartographyTask.Shop
            : CartographyTask.Craft;
    }
}
