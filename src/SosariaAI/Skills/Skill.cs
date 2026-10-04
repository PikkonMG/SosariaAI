using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

public abstract class Skill
{
    /// <summary>The reason a step gives when its person is not on a map.</summary>
    public const string NotInWorldReason = "not in the world";

    /// <summary>The reason a running step gives when its person left the map.</summary>
    public const string LeftWorldReason = "left the world";

    /// <summary>Starts the step. The character's <see cref="SosariaCharacter.Motor"/> walks it.</summary>
    public abstract bool Begin(SosariaCharacter character);

    public abstract SkillStatus Tick();

    public abstract void Abort();

    /// <summary>
    /// The routine was held for <paramref name="held"/> without ticks. A skill with a clock
    /// shifts its start so the held time does not count against it.
    /// </summary>
    public virtual void Resume(TimeSpan held)
    {
    }

    public abstract string Name { get; }

    /// <summary>Why the step failed, in plain words for the activity log; null when it gave no reason.</summary>
    public string FailReason { get; private set; }

    /// <summary>A step begun again starts with no reason of its own.</summary>
    public void ClearFailReason() => FailReason = null;

    /// <summary>Notes why the routine ends the step as a failure from outside it (<see cref="Routine.FailActive"/>).</summary>
    public void NoteFailReason(string reason) => FailReason = reason;

    /// <summary>
    /// What the step aims at, once it knows: a ghost, a mount, a friend, a dungeon, a stable or
    /// a walk's goal. Null for a step with no target of its own. Three failures the same way
    /// at one target rest the step there (<see cref="JobTargetRest"/>).
    /// </summary>
    public virtual JobTarget? AimedAt => null;

    /// <summary>
    /// True, with the reason noted, when this person rests the step for a target it knows
    /// before it begins: the routine does not begin it. A target found only as the step
    /// begins is passed over by the step's own search instead.
    /// </summary>
    public bool RefusesRestingTarget(SosariaCharacter character) =>
        character != null && AimedAt is { } target &&
        JobTargetRest.Rests(character, Name, target.Key, Core.Now) &&
        !CannotStart(JobTargetRest.RestingWhy);

    /// <summary>Notes why the step cannot start. Returns false for <see cref="Begin"/>.</summary>
    protected bool CannotStart(string reason)
    {
        FailReason = reason;
        return false;
    }

    /// <summary>Notes why the step failed. Returns <see cref="SkillStatus.Failed"/> for <see cref="Tick"/>.</summary>
    protected SkillStatus Fail(string reason)
    {
        FailReason = reason;
        return SkillStatus.Failed;
    }
}
