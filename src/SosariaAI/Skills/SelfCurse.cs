using Server;
using Server.Spells;
using Server.Spells.First;
using Server.Spells.Fourth;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The resist macro: a real Clumsy, Weaken, Feeblemind or Curse cast on oneself, paid for
/// in mana and reagents by the engine.
/// </summary>
public static class SelfCurse
{
    public static Spell Create(ResistSpell kind, Mobile caster) =>
        kind switch
        {
            ResistSpell.Weaken => new WeakenSpell(caster),
            ResistSpell.Feeblemind => new FeeblemindSpell(caster),
            ResistSpell.Curse => new CurseSpell(caster),
            _ => new ClumsySpell(caster)
        };

    /// <summary>True when the person has the Magery and a spellbook holding Clumsy.</summary>
    public static bool CanTrain(SosariaCharacter person) =>
        person != null && ResistRules.MayTrain(person.Skills.Magery.Value) &&
        SpellCasting.Knows(person, Create(ResistSpell.Clumsy, person));

    /// <summary>True when the pack holds reagents for at least one curse in the rotation.</summary>
    public static bool HasReagents(SosariaCharacter person) =>
        ResistRules.Pick(0, person.Skills.Magery.Value, int.MaxValue, kind => Affords(person, kind)) != null;

    /// <summary>Starts the next curse in the rotation. False when mana or reagents run short.</summary>
    public static bool TryCast(SosariaCharacter person, int rotation)
    {
        var kind = ResistRules.Pick(rotation, person.Skills.Magery.Value, person.Mana, spell => Affords(person, spell));
        return kind is { } picked && SpellCasting.TryBegin(person, Create(picked, person));
    }

    /// <summary>Aims a finished curse at oneself. True when it was aimed.</summary>
    public static bool TryAimSelf(SosariaCharacter person) => SpellCasting.TryAim(person, person);

    private static bool Affords(SosariaCharacter person, ResistSpell kind) =>
        SpellCasting.HasReagents(person.Backpack, Create(kind, person).Info);
}
