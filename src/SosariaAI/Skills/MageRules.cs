using SosariaAI.Combat;
using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>
/// T2A magery: cast spells. Classic mage skill. Mark and Recall are dedicated spells.
/// </summary>
public static class MageRules
{
    public const string Kind = SkillKinds.Mage;
    public const int ReachTiles = 8;
    public const double MinMagery = 1;
    public const int HealMana = 4;
    public const int ArrowMana = 4;

    public static bool MayCastHeal(int hits, int hitsMax, int mana, double magery) =>
        hits < hitsMax && mana >= HealMana && magery >= MinMagery;

    public static bool MayCastArrow(int mana, double magery) => mana >= ArrowMana && magery >= MinMagery;

    /// <summary>True when a living mobile in sight is fair game for a Magic Arrow.</summary>
    public static bool IsMageTarget(
        bool isSelf,
        bool isPlayer,
        bool isSosaria,
        bool isVendor,
        bool invulnerable,
        bool isControlled,
        bool isSummoned,
        int karma,
        bool alwaysMurderer,
        bool attackerIsPk
    )
    {
        if (isSelf || isVendor || invulnerable)
        {
            return false;
        }

        if (isPlayer || isSosaria)
        {
            return attackerIsPk;
        }

        return EnemyRules.IsEnemy(
            karma,
            alwaysMurderer,
            isPlayer,
            isSosaria,
            isControlled,
            isSummoned,
            isVendor,
            invulnerable
        );
    }
}
