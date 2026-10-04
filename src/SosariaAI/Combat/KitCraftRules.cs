using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// Who made the kit. The common 1999 look is armor and weapons bought from a player
/// crafter, not from an NPC shelf: a grandmaster smith's mark on the plate, a tailor's
/// on the studded leather, a bowyer's on the bow. A few crafters on each shard made most
/// of what people wore, so every trade has a small pool of makers and one person buys
/// its whole suit from one of them. Veterans could pay for the best; novices wore more
/// shop work. Pure: rolled from the person's id, so a reboot brings back the same marks.
/// </summary>
public static class KitCraftRules
{
    /// <summary>Percent of each piece, by tier, novice first, that is exceptional crafted work.</summary>
    public static readonly int[] ExceptionalPercentByTier = [35, 50, 65, 75, 80, 85, 90];

    /// <summary>How many known makers each trade has on one shard.</summary>
    public const int MakersPerTrade = 12;

    public const string MakerIdPrefix = "maker:";
    private const char MakerIdSeparator = ':';

    private const int ExceptionalSalt = 1301;
    private const int MakerSalt = 1307;
    private const int MakerGenderSalt = 1311;

    public static int ExceptionalPercent(SkillTier tier) => ExceptionalPercentByTier[(int)tier];

    /// <summary>True when the piece on <paramref name="piece"/> (a wear layer) is exceptional work.</summary>
    public static bool IsExceptional(SkillTier tier, string uniqueId, int piece) =>
        PersonDice.Chance(uniqueId, ExceptionalSalt + piece, ExceptionalPercent(tier));

    /// <summary>The id of the maker of this trade that the person bought from.</summary>
    public static string MakerId(string tradeKind, string uniqueId) =>
        MakerIdPrefix + tradeKind + MakerIdSeparator + PersonDice.Roll(uniqueId, MakerSalt, MakersPerTrade);

    /// <summary>The maker's mark: a player-style name, the same for everyone who bought from that maker.</summary>
    public static string MakerName(string tradeKind, string uniqueId)
    {
        var makerId = MakerId(tradeKind, uniqueId);
        var female = PersonDice.Chance(makerId, MakerGenderSalt, PersonProfileRules.FemalePercent);
        return PlayerNameRules.Pick(makerId, female, null);
    }
}
