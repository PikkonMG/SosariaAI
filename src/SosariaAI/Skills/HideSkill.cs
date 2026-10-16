using System;
using Server;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Hide in place with the engine's Hiding skill: the skill check, the ten second delay, the
/// "You have hidden yourself well" and the refusal while someone fights the hider.
/// </summary>
public sealed class HideSkill : Skill
{
    private const string AlreadyHiddenReason = "already hidden";

    /// <summary>The engine would not let the hider use the skill: a region that forbids it, or a spell in its hands.</summary>
    public const string RefusedReason = "could not use the hiding skill";

    /// <summary>Seconds a hider waits for the engine to take the skill: its ten second delay with room to spare.</summary>
    public const int TryLimitSeconds = 15;

    /// <summary>How long a hider waits for the engine to take the skill before it gives up.</summary>
    public static readonly TimeSpan TryLimit = TimeSpan.FromSeconds(TryLimitSeconds);

    private SosariaCharacter _thief;
    private DateTime _started;

    public override string Name => SkillKinds.Hide;

    public override bool Begin(SosariaCharacter character)
    {
        _thief = character;
        _started = Core.Now;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        return !character.Hidden || CannotStart(AlreadyHiddenReason);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_thief) || _thief.Deleted)
        {
            return Fail(NotInWorldReason);
        }

        if (_thief.Hidden)
        {
            return Fail(AlreadyHiddenReason);
        }

        // The skill is still on its delay: this try waits for the next one, but not past the
        // delay, which a region that forbids the skill or a spell in the hands never ends.
        if (!Server.Skills.UseSkill(_thief, SkillName.Hiding))
        {
            return GivesUp(_started, Core.Now) ? Fail(RefusedReason) : SkillStatus.Running;
        }

        return _thief.Hidden ? SkillStatus.Done : Fail(HideRules.NotHiddenReason);
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);

    public override void Abort() => _thief = null;

    /// <summary>True once a hider started at <paramref name="started"/> has waited out <see cref="TryLimit"/>.</summary>
    public static bool GivesUp(DateTime started, DateTime now) => TimeRules.Passed(started, now, TryLimit);
}
