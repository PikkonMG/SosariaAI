namespace SosariaAI.Combat;

/// <summary>
/// After a fight a character must recover before the routine continues.
/// Self-defence is not this class.
/// </summary>
public static class RecoveryRules
{
    public const double RecoverBelowHitsFraction = 0.60;
    public const double FitHitsFraction = 0.95;

    public static bool NeedsRecovery(double hitsFraction, bool inCombat) =>
        !inCombat && hitsFraction < RecoverBelowHitsFraction;

    public static bool IsFit(double hitsFraction) => hitsFraction >= FitHitsFraction;

    public static bool ShouldSeekHealer(double hitsFraction, int bandageCount, int potionCount) =>
        NeedsRecovery(hitsFraction, false) && bandageCount <= 0 && potionCount <= 0;
}
