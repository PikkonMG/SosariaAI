using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Combat;

/// <summary>
/// Meditation through the engine's own skill: the hands must be empty (a spellbook is
/// fine), the body stands still, and the trance makes mana come back faster until the next
/// step. A weapon or shield put away for it goes back on afterwards.
/// </summary>
public static partial class CombatBrain
{
    /// <summary>The engine answers each meditation try with a ten second skill wait.</summary>
    public const int MeditateTryGapMs = 10000;

    /// <summary>A body with no mana pool counts as rested.</summary>
    public const double FullMana = 1.0;

    /// <summary>
    /// Out of a fight, a caster low on mana puts its weapon away and meditates until it is
    /// rested, the way a mage sat down after a rough win. True while it meditates.
    /// </summary>
    public static bool TendMana(SosariaCharacter character)
    {
        if (character?.Deleted != false || !character.Alive)
        {
            return false;
        }

        var memory = MemoryOf(character);
        var caster = SpellBook.IsCaster(character.Skills.Magery.Value);

        if (!SelfCareRules.ShouldMeditate(memory.Meditating, caster, ManaFraction(character)))
        {
            EndMeditation(character);
            return false;
        }

        KeepMeditating(character);
        return true;
    }

    /// <summary>Stands still and keeps the trance going: a new try after each engine wait.</summary>
    public static void KeepMeditating(SosariaCharacter character)
    {
        var memory = MemoryOf(character);
        memory.Meditating = true;
        character.Motor.Stop();

        var now = Core.TickCount;

        if (character.Meditating || now - memory.NextMeditateAt < 0)
        {
            return;
        }

        memory.NextMeditateAt = now + MeditateTryGapMs;
        StashHands(character, memory);
        Server.Skills.UseSkill(character, SkillName.Meditation);
    }

    /// <summary>Gets up: the trance ends and what was put away for it is held again.</summary>
    public static void EndMeditation(SosariaCharacter character)
    {
        if (character == null || !Memories.TryGetValue(character, out var memory) || !memory.Meditating)
        {
            return;
        }

        memory.Meditating = false;
        character.Meditating = false;

        for (var i = 0; i < memory.StashedHands.Count; i++)
        {
            var item = memory.StashedHands[i];

            if (!item.Deleted && item.IsChildOf(character.Backpack))
            {
                GearEquip.EquipTool(character, item);
            }
        }

        memory.StashedHands.Clear();
    }

    /// <summary>Pre-AOS the trance needs empty hands; a spellbook or runebook may stay.</summary>
    private static void StashHands(SosariaCharacter character, Memory memory)
    {
        Stash(character, memory, character.FindItemOnLayer(Layer.OneHanded));
        Stash(character, memory, character.FindItemOnLayer(Layer.TwoHanded));
    }

    private static void Stash(SosariaCharacter character, Memory memory, Item held)
    {
        if (held == null || held is Spellbook or Runebook)
        {
            return;
        }

        character.AddToBackpack(held);
        memory.StashedHands.Add(held);
    }

    private static double ManaFraction(Mobile mobile) =>
        mobile.ManaMax <= 0 ? FullMana : (double)mobile.Mana / mobile.ManaMax;
}
