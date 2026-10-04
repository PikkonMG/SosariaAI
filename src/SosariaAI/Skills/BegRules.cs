namespace SosariaAI.Skills;

/// <summary>
/// Classic town begging: ask a nearby NPC for gold. Practice check only.
/// </summary>
public static class BegRules
{
    public const int ReachTiles = 2;
    public const double PracticeMin = 0;
    public const double PracticeMax = 100;

    /// <summary>
    /// The engine's begging target: not the beggar, not a player (characters are players
    /// too), and in a human body. A dog at the bank gives no gold.
    /// </summary>
    public static bool IsBegTarget(bool isSelf, bool isPlayer, bool humanBody) =>
        !isSelf && !isPlayer && humanBody;

    /// <summary>Before Mondain's Legacy nobody gives gold to a beggar on a horse.</summary>
    public static bool MayBegMounted(bool mounted, bool mondainsLegacy) =>
        !mounted || mondainsLegacy;
}
