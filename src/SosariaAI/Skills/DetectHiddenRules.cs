namespace SosariaAI.Skills;

/// <summary>
/// T2A detect hidden: skill check, then reveal Hidden mobiles in range.
/// </summary>
public static class DetectHiddenRules
{
    public const int ReachTiles = 8;
    public const double PracticeMin = 0;
    public const double PracticeMax = 100;

    public static bool IsHiddenTarget(bool hidden, bool isSelf) => hidden && !isSelf;
}
