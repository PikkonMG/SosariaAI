namespace SosariaAI.Combat;

public static class EnemyRules
{
    public static bool IsEnemy(
        int karma,
        bool alwaysMurderer,
        bool isPlayer,
        bool isSosaria,
        bool isControlled,
        bool isSummoned,
        bool isVendor,
        bool isInvulnerable
    )
    {
        if (isPlayer || isSosaria || isControlled || isSummoned || isVendor || isInvulnerable)
        {
            return false;
        }

        return alwaysMurderer || karma < 0;
    }

    public static bool IsGuardedFlag(bool hasRegion, bool disabled) => hasRegion && !disabled;
}
