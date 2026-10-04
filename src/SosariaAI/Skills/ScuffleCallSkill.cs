using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A fighter called to a town scuffle (<see cref="TownScuffles"/>) walks to the street spot and
/// waits there. The step ends when the call no longer holds the fighter: the scuffle started
/// (the fight takes over), the call lapsed, or the fighter was dropped from it. A walk that
/// fails takes the fighter off the call.
/// </summary>
public sealed class ScuffleCallSkill : Skill
{
    public const string SkillName = "ScuffleCall";

    public const string NoWalkWhy = "no walk to the street spot";

    private SosariaCharacter _character;
    private TravelSkill _walk;

    public ScuffleCallSkill(Point3D spot) => Spot = spot;

    public Point3D Spot { get; }

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = new TravelSkill(Spot, TownScuffleRules.SpotTiles);

        if (_walk.Begin(character))
        {
            return true;
        }

        _walk = null;
        TownScuffles.LeaveCall(character);
        return CannotStart(NoWalkWhy);
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true } || !TownScuffles.OnCall(_character))
        {
            Abort();
            return SkillStatus.Done;
        }

        if (_walk == null)
        {
            _character.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return status;
        }

        _walk = null;

        if (status == SkillStatus.Failed)
        {
            TownScuffles.LeaveCall(_character);
            return Fail(NoWalkWhy);
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);
}
