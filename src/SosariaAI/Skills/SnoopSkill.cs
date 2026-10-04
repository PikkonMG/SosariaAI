using System;
using Server;
using Server.SkillHandlers;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The era's snoop: walks up to a mark in the crowd and peeks into its pack. The peek is the
/// engine's own handler: its skill check, its notice to watchers and its karma. The reach and
/// the time to get beside the mark are the lift's (<see cref="StealRules"/>).
/// </summary>
public sealed class SnoopSkill : Skill
{
    private SosariaCharacter _snooper;
    private Mobile _mark;
    private DateTime _started;

    public override string Name => SkillKinds.Snoop;

    public override bool Begin(SosariaCharacter character)
    {
        _snooper = character;
        _started = Core.Now;

        if (!StealRules.MaySnoop(character))
        {
            return CannotStart(NotInWorldReason);
        }

        _mark = StealMarks.Find(character, out _);
        return _mark != null || CannotStart(StealRules.NoMarkReason);
    }

    public override SkillStatus Tick()
    {
        if (!StealRules.MaySnoop(_snooper) || _snooper.Deleted)
        {
            return Fail(NotInWorldReason);
        }

        if (_mark is not { Deleted: false, Alive: true } || _mark.Map != _snooper.Map)
        {
            return Fail(StealRules.MarkGoneReason);
        }

        if (Core.Now - _started >= StealRules.ApproachLimit)
        {
            return Fail(StealRules.ApproachTooLongReason);
        }

        if (!StealRules.InReach(_snooper.Location, _mark.Location))
        {
            _snooper.Motor.MoveTo(_mark, StealRules.ReachTiles);
            return SkillStatus.Running;
        }

        Snooping.Container_Snoop(_mark.Backpack, _snooper);
        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _snooper = null;
        _mark = null;
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);
}
