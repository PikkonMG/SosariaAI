using Server;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// What a person holds to fight, by its build. A caster build (mage, healer, bard, tamer,
/// treasure hunter, necromancer) holds its spellbook and never a weapon: the engine puts a
/// weapon in the pack at every cast, and mages drew a looted mace or their mining pick between
/// casts and swung it. Any other build holds only a weapon of a skill it trains: a swordsman
/// keeps its katana, not the mace off an orc, and carries its travel book in the pack. The
/// tank mage holds its weapon and casts from the book in its pack (<see cref="SwingsAndCasts"/>).
/// A mobile with no build holds what it finds. Pure.
/// </summary>
public static class WieldRules
{
    /// <summary>True when the build fights with spells: its hands hold the spellbook.</summary>
    public static bool HoldsSpellbook(ResolvedBuild build) => build?.Style == CombatStyle.Mage;

    /// <summary>True when the build may hold a weapon of <paramref name="weaponSkill"/>.</summary>
    public static bool MayWield(ResolvedBuild build, SkillName weaponSkill) =>
        build == null || !HoldsSpellbook(build) && build.Trains(weaponSkill);

    /// <summary>True when the build may hold a spellbook in its hands: a caster, or a mobile with no build.</summary>
    public static bool MayHoldSpellbook(ResolvedBuild build) => build == null || HoldsSpellbook(build);

    /// <summary>
    /// True for the tank mage (<see cref="ClassBuilds.TankMageShare"/>): a mage whose build
    /// fights with a weapon. It swings and casts the first circle where it stands.
    /// </summary>
    public static bool SwingsAndCasts(PersonClass personClass, ResolvedBuild build) =>
        personClass == PersonClass.Mage && build?.Style == CombatStyle.Melee;
}
