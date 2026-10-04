using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// A foe as every threat read takes it (<see cref="ThreatRating"/>): the fight's focus pick, its
/// engage and run tests, the hunter's sight scan, the answer to a blow, and the floor ratings
/// (<see cref="Navigation.Generation.CreatureStatsLookup"/>). A creature counts its own blow and
/// its arts (<see cref="HostileArts"/>); a person counts the blow a hunter reckons with and no
/// arts. The fight read the blows alone while the floors counted arts too, so a fighter a floor
/// was fit for opened on the dread spider whose spells and lethal bite made it the floor's rating.
/// Main thread: it reads live mobiles.
/// </summary>
public static class HostileRead
{
    /// <summary>A live foe as it stands now: its hits left, its strength, its blow and its arts.</summary>
    public static HostileStats Of(Mobile mobile) =>
        mobile is BaseCreature creature
            ? new HostileStats(creature.Hits, creature.Str, AverageBlow(creature), ArtsOf(creature))
            : new HostileStats(mobile.Hits, mobile.Str, HuntSkill.HuntThreatAverageDamage);

    /// <summary>The mean of a creature's least and greatest blow.</summary>
    public static int AverageBlow(BaseCreature creature) => (creature.DamageMin + creature.DamageMax) / 2;

    /// <summary>
    /// What a creature brings beside its blows. Only a mage's brain casts, so the Magery of a
    /// fighter that merely has the skill counts for nothing. Cold and chaos breaths are fire
    /// breaths to the engine.
    /// </summary>
    public static HostileArts ArtsOf(BaseCreature creature) =>
        new(
            creature.AI == AIType.AI_Mage ? (int)creature.Skills.Magery.Value : 0,
            creature.GetAbility(MonsterAbilityType.FireBreath) != null,
            creature.HitPoison is { } poison ? poison.Level + 1 : ThreatRating.NoPoison,
            creature.AI == AIType.AI_Archer || creature.Weapon is BaseRanged
        );
}
