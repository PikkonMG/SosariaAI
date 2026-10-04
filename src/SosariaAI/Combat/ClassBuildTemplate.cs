using System;
using Server;

namespace SosariaAI.Combat;

/// <summary>
/// One era template: the skills a class trains, its full stat line and what it carries.
/// The primary skill leads, secondary skills follow it, utility skills sit at half.
/// </summary>
public sealed class ClassBuildTemplate
{
    public SkillName Primary { get; init; }

    public SkillName[] Secondary { get; init; } = [];

    public SkillName[] Utility { get; init; } = [];

    /// <summary>Stats at full training. The three sum to <see cref="EraBuildCaps.StatTotalCap"/>.</summary>
    public (int Strength, int Dexterity, int Intelligence) Stats { get; init; }

    public CombatStyle Style { get; init; }

    /// <summary>A <see cref="KitVariation"/> row key, or null for a caster with no weapon.</summary>
    public string Weapon { get; init; }

    /// <summary>True only beside a one-handed row.</summary>
    public bool Shield { get; init; }

    public KitArmor Armor { get; init; }

    /// <summary>Casts in a fight: carries a spellbook and every reagent.</summary>
    public bool Caster => Style == CombatStyle.Mage;

    /// <summary>Casts only to travel and cure: carries a spellbook and the recall reagents.</summary>
    public bool TravelMagic => !Caster && Array.IndexOf(Utility, SkillName.Magery) >= 0;

    /// <summary>Extra class pieces: the bard's instrument row, a paladin's book of chivalry, a necromancer's book and reagents.</summary>
    public string[] ClassPieces { get; init; } = [];

    /// <summary>Sits to meditate for its mana. Metal and bone armor stopped that in the Second Age.</summary>
    public bool Meditates => Trains(SkillName.Meditation);

    /// <summary>Fights with a trained weapon skill: a tank mage beside a pure one.</summary>
    public bool WeaponTrained =>
        Trains(SkillName.Swords) || Trains(SkillName.Macing) || Trains(SkillName.Fencing) || Trains(SkillName.Archery);

    /// <summary>True when the skill leads the template or follows the lead; a utility skill does not count.</summary>
    public bool Trains(SkillName skill) => Primary == skill || Array.IndexOf(Secondary, skill) >= 0;
}
