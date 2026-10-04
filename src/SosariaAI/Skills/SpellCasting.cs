using Server;
using Server.Items;
using Server.Spells;

namespace SosariaAI.Skills;

/// <summary>
/// Casting the way a player does: the spell speaks its words of power and plays the chant,
/// the engine takes mana and reagents, and once the words are done the target cursor is
/// answered. A shove during the words disturbs the cast like any other.
/// </summary>
public static class SpellCasting
{
    public static bool HasReagents(Container pack, SpellInfo info)
    {
        if (pack == null || info == null)
        {
            return false;
        }

        for (var i = 0; i < info.Reagents.Length; i++)
        {
            if (pack.GetAmount(info.Reagents[i]) < info.Amounts[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when a spellbook the caster carries holds the spell.</summary>
    public static bool Knows(Mobile caster, Spell spell)
    {
        var id = SpellRegistry.GetRegistryNumber(spell);
        return Spellbook.Find(caster, id)?.HasSpell(id) == true;
    }

    /// <summary>Starts the words of power. False when busy, the book lacks the spell, or the pack lacks reagents.</summary>
    public static bool TryBegin(Mobile caster, Spell spell) =>
        caster is { Spell: null, Target: null } && spell != null && Knows(caster, spell) &&
        HasReagents(caster.Backpack, spell.Info) && spell.Cast();

    /// <summary>
    /// Answers the target cursor of a spell whose words are done with <paramref name="target"/>,
    /// a mobile or an item. True when it was answered.
    /// </summary>
    public static bool TryAim(Mobile caster, object target)
    {
        if (caster?.Spell is not Spell { State: SpellState.Sequencing } || caster.Target is not { } cursor || target == null)
        {
            return false;
        }

        cursor.Invoke(caster, target);
        return true;
    }
}
