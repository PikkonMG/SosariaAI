using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Study a nearby creature the engine would let this person lore (<see cref="LoreRules.MayLore"/>),
/// with the engine's own targeted skill check. Knowledge check; do not tame. With no such beast
/// in reach the study does not start: tamers stood at the Moonglow bank "studying" nothing.
/// </summary>
public sealed class LoreSkill : Skill
{
    private SosariaCharacter _character;

    public override string Name => LoreRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return People.InWorld(character) && FindSubject(character) != null || CannotStart(LoreRules.NoSubjectWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character) || _character.Deleted)
        {
            return Fail(LoreRules.NoSubjectWhy);
        }

        var target = FindSubject(_character);

        if (target == null)
        {
            return Fail(LoreRules.NoSubjectWhy);
        }

        _character.CheckTargetSkill(SkillName.AnimalLore, target, LoreRules.PracticeMin, LoreRules.PracticeMax);
        return SkillStatus.Done;
    }

    public override void Abort() => _character = null;

    /// <summary>A creature in reach this person may lore, or null. The scorer asks it before it offers the study.</summary>
    public static BaseCreature FindSubject(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return null;
        }

        var lore = character.Skills.AnimalLore.Value;

        foreach (var mobile in character.GetMobilesInRange(LoreRules.ReachTiles))
        {
            if (mobile is BaseCreature { Deleted: false, Alive: true } creature &&
                LoreRules.MayLore(
                    creature.Body.IsAnimal || creature.Body.IsMonster || creature.Body.IsSea,
                    creature.IsDeadPet,
                    creature.Controlled,
                    creature.Tamable,
                    lore
                ))
            {
                return creature;
            }
        }

        return null;
    }
}
