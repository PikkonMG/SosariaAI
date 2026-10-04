using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>A weak curse a resist trainer casts on itself.</summary>
public enum ResistSpell
{
    Clumsy,
    Weaken,
    Feeblemind,
    Curse
}

/// <summary>
/// Training Resisting Spells the way every bank had someone doing it: casting weak curses
/// on oneself over and over. Clumsy, Weaken and Feeblemind are first circle; Curse is fourth
/// circle and joins the rotation once the caster can land it. The target's resist is
/// checked by the engine each time a curse lands. Pure.
/// </summary>
public static class ResistRules
{
    public const string Kind = SkillKinds.Resist;

    /// <summary>Below this Magery a first-circle self-cast fizzles more than it lands.</summary>
    public const double MinMagery = 30;

    public const double CurseMinMagery = 60;
    public const int FirstCircleMana = 4;
    public const int CurseMana = 11;

    private static readonly ResistSpell[] FirstCircle = [ResistSpell.Clumsy, ResistSpell.Weaken, ResistSpell.Feeblemind];

    private static readonly ResistSpell[] WithCurse =
        [ResistSpell.Clumsy, ResistSpell.Weaken, ResistSpell.Feeblemind, ResistSpell.Curse];

    public static bool MayTrain(double magery) => magery >= MinMagery;

    public static IReadOnlyList<ResistSpell> Options(double magery) =>
        magery >= CurseMinMagery ? WithCurse : FirstCircle;

    public static int ManaOf(ResistSpell spell) => spell == ResistSpell.Curse ? CurseMana : FirstCircleMana;

    /// <summary>
    /// The next curse in the rotation the caster can pay for in mana and reagents, or null
    /// when it can pay for none.
    /// </summary>
    public static ResistSpell? Pick(int rotation, double magery, int mana, Func<ResistSpell, bool> hasReagents)
    {
        if (!MayTrain(magery) || hasReagents == null)
        {
            return null;
        }

        var options = Options(magery);

        for (var i = 0; i < options.Count; i++)
        {
            var spell = options[(Math.Abs(rotation) + i) % options.Count];

            if (mana >= ManaOf(spell) && hasReagents(spell))
            {
                return spell;
            }
        }

        return null;
    }
}
