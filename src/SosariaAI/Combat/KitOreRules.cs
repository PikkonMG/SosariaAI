using Server.Items;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// Coloured ore on a crafted metal suit: a veteran with money paid its smith for dull
/// copper, shadow or copper plate, and the few rich grandmasters for gold, agapite or
/// verite; valorite was a legend. A novice's suit is iron. One ore for the whole suit,
/// since one smith made it. Pure: rolled from the person's id.
/// </summary>
public static class KitOreRules
{
    /// <summary>Percent of each tier, novice first, whose crafted metal suit is coloured ore.</summary>
    public static readonly int[] OrePercentByTier = [0, 0, 5, 10, 20, 30, 40];

    /// <summary>Points the purse adds to the tier's odds, poor first.</summary>
    public static readonly int[] WealthBonus = [0, 0, 10, 20];

    /// <summary>Weights of the ores from dull copper up to valorite: the rarer, the scarcer.</summary>
    public static readonly int[] OreWeights = [300, 250, 200, 120, 70, 40, 15, 5];

    private static readonly CraftResource[] Ores =
    [
        CraftResource.DullCopper,
        CraftResource.ShadowIron,
        CraftResource.Copper,
        CraftResource.Bronze,
        CraftResource.Gold,
        CraftResource.Agapite,
        CraftResource.Verite,
        CraftResource.Valorite
    ];

    private const int OreSalt = 1409;
    private const int KindSalt = 1413;

    public static int Percent(SkillTier tier, PersonWealth wealth) =>
        OrePercentByTier[(int)tier] == 0 ? 0 : OrePercentByTier[(int)tier] + WealthBonus[(int)wealth];

    /// <summary>The ore of this person's crafted metal suit: iron, or one coloured ore.</summary>
    public static CraftResource OreFor(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        PersonDice.Chance(uniqueId, OreSalt, Percent(tier, wealth))
            ? Ores[PersonDice.Weighted(uniqueId, KindSalt, OreWeights)]
            : CraftResource.Iron;
}
