using Server;
using Server.Items;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Disarms a trapped chest in reach with the engine's own check, the trap's power to 30
/// above it. The engine's cursor takes only a trapped container ("that doesn't appear to be
/// trapped" for any other), so a thief with no trapped chest in reach has no work
/// (<see cref="HasWork"/>): its step rolled a check on nothing and failed 256 times a night.
/// </summary>
public sealed class RemoveTrapSkill : Skill
{
    public const string NoTrapWhy = "no trapped chest in reach";
    public const string TrapHeldWhy = "the trap held";

    private SosariaCharacter _character;

    public override string Name => RemoveTrapRules.Kind;

    /// <summary>A trapped chest stands within the thief's reach.</summary>
    public static bool HasWork(SosariaCharacter character) =>
        People.InWorld(character) && FindTrapped(character) != null;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        return FindTrapped(character) != null || CannotStart(NoTrapWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character) || _character.Deleted)
        {
            return Fail(NotInWorldReason);
        }

        var container = FindTrapped(_character);

        if (container == null)
        {
            return Fail(NoTrapWhy);
        }

        var (min, max) = RemoveTrapRules.SkillWindow(container.TrapPower);

        if (!_character.CheckSkill(SkillName.RemoveTrap, min, max))
        {
            return Fail(TrapHeldWhy);
        }

        RemoveTrapRules.Disarm(container);
        return SkillStatus.Done;
    }

    public override void Abort() => _character = null;

    private static TrappableContainer FindTrapped(SosariaCharacter character)
    {
        foreach (var item in character.GetItemsInRange(RemoveTrapRules.ReachTiles))
        {
            if (item is TrappableContainer { Deleted: false } container &&
                RemoveTrapRules.IsTrapped(container.TrapType))
            {
                return container;
            }
        }

        return null;
    }
}
