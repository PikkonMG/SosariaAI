using System;

namespace SosariaAI.Combat;

public enum SpellKind
{
    MagicArrow,
    Harm,
    Fireball,
    Lightning,
    MindBlast,
    EnergyBolt,
    Explosion,
    FlameStrike,
    Poison,
    Paralyze,
    Heal,
    Cure,
    GreaterHeal,
    ReactiveArmor,
    Protection
}

/// <summary>
/// One spell a character may cast. <see cref="MinMagery"/> is the skill where a cast holds
/// about half the time. Whether it has room is a matter of time, not tiles: see <see cref="CastTiming"/>.
/// </summary>
public readonly record struct SpellEntry(
    SpellKind Kind,
    int SpellId,
    int Circle,
    double MinMagery,
    int Mana,
    int Weight
);

/// <summary>
/// The fighting mage's book, first to seventh circle, and how a 1999 mage picked from it.
/// Pre-AOS any blow ruins a cast above the first circle, so a spell is picked only when its
/// words finish before the next blow can land (the safe circle); bigger spells wait for the
/// footwork to open the gap. A mage throws its best few spells, not the whole book, with the
/// odd Paralyze on a closing foe and Poison on a clean one.
/// </summary>
public static class SpellBook
{
    /// <summary>Target reach of a spell after T2A (ITargetingSpell.TargetRange).</summary>
    public const int Reach = 10;

    /// <summary>How many of the strongest castable attacks stay in the pick.</summary>
    public const int PoolDepth = 3;

    /// <summary>A foe inside this many tiles is closing on the mage.</summary>
    public const int ClosingTiles = 4;
    public const double ParalyzeChance = 0.35;
    public const double PoisonChance = 0.15;

    public static readonly SpellEntry MagicArrow = new(SpellKind.MagicArrow, 4, 1, 20, 4, 2);
    public static readonly SpellEntry Harm = new(SpellKind.Harm, 11, 2, 30, 6, 3);
    public static readonly SpellEntry Fireball = new(SpellKind.Fireball, 17, 3, 40, 9, 4);
    public static readonly SpellEntry Lightning = new(SpellKind.Lightning, 29, 4, 50, 11, 5);
    public static readonly SpellEntry MindBlast = new(SpellKind.MindBlast, 36, 5, 60, 14, 3);
    public static readonly SpellEntry EnergyBolt = new(SpellKind.EnergyBolt, 41, 6, 70, 20, 6);
    public static readonly SpellEntry Explosion = new(SpellKind.Explosion, 42, 6, 70, 20, 5);
    public static readonly SpellEntry FlameStrike = new(SpellKind.FlameStrike, 50, 7, 80, 40, 6);
    public static readonly SpellEntry Poison = new(SpellKind.Poison, 19, 3, 40, 9, 0);
    public static readonly SpellEntry Paralyze = new(SpellKind.Paralyze, 37, 5, 60, 14, 0);
    public static readonly SpellEntry Heal = new(SpellKind.Heal, 3, 1, 20, 4, 0);
    public static readonly SpellEntry Cure = new(SpellKind.Cure, 10, 2, 30, 6, 0);
    public static readonly SpellEntry GreaterHeal = new(SpellKind.GreaterHeal, 28, 4, 50, 11, 0);
    public static readonly SpellEntry ReactiveArmor = new(SpellKind.ReactiveArmor, 6, 1, 20, 4, 0);
    public static readonly SpellEntry Protection = new(SpellKind.Protection, 14, 2, 30, 6, 0);

    /// <summary>Damage spells, weakest first.</summary>
    public static readonly SpellEntry[] Attacks =
        [MagicArrow, Harm, Fireball, Lightning, MindBlast, EnergyBolt, Explosion, FlameStrike];

    public static SpellEntry EntryOf(SpellKind kind) =>
        kind switch
        {
            SpellKind.MagicArrow => MagicArrow,
            SpellKind.Harm => Harm,
            SpellKind.Fireball => Fireball,
            SpellKind.Lightning => Lightning,
            SpellKind.MindBlast => MindBlast,
            SpellKind.EnergyBolt => EnergyBolt,
            SpellKind.Explosion => Explosion,
            SpellKind.FlameStrike => FlameStrike,
            SpellKind.Poison => Poison,
            SpellKind.Paralyze => Paralyze,
            SpellKind.Heal => Heal,
            SpellKind.Cure => Cure,
            SpellKind.GreaterHeal => GreaterHeal,
            SpellKind.ReactiveArmor => ReactiveArmor,
            SpellKind.Protection => Protection,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    /// <summary>A character fights with spells once its magery holds the first attack.</summary>
    public static bool IsCaster(double magery) => magery >= MagicArrow.MinMagery;

    public static bool CanCast(SpellEntry entry, double magery, int mana) =>
        magery >= entry.MinMagery && mana >= entry.Mana;

    /// <summary>The words finish before the next blow: the circle is at or under the safe circle.</summary>
    public static bool Fits(SpellEntry entry, int safeCircle) => entry.Circle <= safeCircle;

    /// <summary>
    /// The ward of the era. From UOR on, Protection guards the words against blows (Protection.cs);
    /// before that it only adds armor, and the answer to a melee foe is Reactive Armor, a first
    /// circle spell that lands through the blows and throws part of each one back.
    /// </summary>
    public static SpellKind WardOf(bool protectionGuardsCasts) =>
        protectionGuardsCasts ? SpellKind.Protection : SpellKind.ReactiveArmor;

    /// <summary>
    /// A spell that raises a target cursor when its words end. A cast of one that ends with no
    /// cursor was broken. From UOR on, Protection works at once on the caster, with no cursor.
    /// </summary>
    public static bool RaisesCursor(SpellKind kind, bool protectionGuardsCasts) =>
        kind != SpellKind.Protection || !protectionGuardsCasts;

    /// <summary>Paralyze on a foe closing in, or Poison on a clean one, by chance. Null when neither fits.</summary>
    public static SpellKind? PickUtility(
        double magery,
        int mana,
        int distance,
        int safeCircle,
        bool foeHeld,
        bool foePoisoned,
        double paralyzeRoll,
        double poisonRoll
    )
    {
        if (!foeHeld && distance <= ClosingTiles && Fits(Paralyze, safeCircle) &&
            CanCast(Paralyze, magery, mana) && paralyzeRoll < ParalyzeChance)
        {
            return SpellKind.Paralyze;
        }

        if (!foePoisoned && Fits(Poison, safeCircle) &&
            CanCast(Poison, magery, mana) && poisonRoll < PoisonChance)
        {
            return SpellKind.Poison;
        }

        return null;
    }

    /// <summary>
    /// A weighted pick among the strongest <see cref="PoolDepth"/> attacks at or under
    /// <paramref name="circleCap"/>. Magic Arrow is the floor: it leaves the pool once anything
    /// better can be cast. <paramref name="roll"/> is in [0, 1). Null when nothing can be cast.
    /// </summary>
    public static SpellKind? PickAttack(double magery, int mana, int distance, int circleCap, double roll)
    {
        Span<int> pool = stackalloc int[PoolDepth];
        var pooled = 0;
        var totalWeight = 0;

        for (var i = Attacks.Length - 1; i >= 0 && pooled < PoolDepth; i--)
        {
            var floorWithBetter = i == 0 && pooled > 0;

            if (floorWithBetter || !Castable(Attacks[i], magery, mana, distance, circleCap))
            {
                continue;
            }

            pool[pooled++] = i;
            totalWeight += Attacks[i].Weight;
        }

        if (pooled == 0)
        {
            return null;
        }

        var pick = Math.Min(totalWeight - 1, (int)(Math.Clamp(roll, 0, 1) * totalWeight));

        for (var i = 0; i < pooled; i++)
        {
            pick -= Attacks[pool[i]].Weight;

            if (pick < 0)
            {
                return Attacks[pool[i]].Kind;
            }
        }

        return Attacks[pool[pooled - 1]].Kind;
    }

    private static bool Castable(SpellEntry entry, double magery, int mana, int distance, int circleCap) =>
        CanCast(entry, magery, mana) && Fits(entry, circleCap) && distance <= Reach;
}
