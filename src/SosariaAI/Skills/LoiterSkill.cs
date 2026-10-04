using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Stands about at the person's spot for a while: its own corner of town for a copy, the
/// authored centre for a fixture, where it stands when it is far from home, or the next
/// place with room when that spot is full. Each stay is the authored one drawn short or
/// long (<see cref="LoiterPaceRules.StayLength"/>), so a corner's crowd does not leave on a beat.
/// </summary>
public sealed class LoiterSkill : Skill
{
    /// <summary>The stay weight of a loiterer: long stands, a short stroll now and then.</summary>
    public const int StayChance = 6;

    private const string NoSpotWhy = "no walk to a spot with room";
    private const string SpotWalkFailedWhy = "the walk to its spot failed";

    public const int DefaultLoiterMinutes = 3;
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(DefaultLoiterMinutes);

    private readonly Point3D _center;
    private readonly int _radius;
    private readonly TimeSpan _authored;
    private readonly LoiterStay _stay = new();
    private DateTime _started;
    private TimeSpan _duration;

    public LoiterSkill(Point3D center, int radius, TimeSpan duration)
    {
        _center = center;
        _radius = radius > 0 ? radius : CharactersFile.DefaultIdleRadius;
        _authored = duration <= TimeSpan.Zero ? DefaultDuration : duration;
    }

    public override string Name => SkillKinds.Loiter;

    public override bool Begin(SosariaCharacter character)
    {
        var leash = HomeLeash.ConfiguredRadius();
        var idle = HomeLeash.IdleCenter(_center, character.HomeCorner, leash, WorkSites.IsCopy(character.CharacterId));
        var center = HomeLeash.StandCenter(character.Location, idle, leash);

        if (!_stay.Begin(character, center, _radius, StayChance))
        {
            return CannotStart(NoSpotWhy);
        }

        _started = Core.Now;
        _duration = LoiterPaceRules.StayLength(_authored, Utility.Random(int.MaxValue));
        return true;
    }

    public override SkillStatus Tick()
    {
        if (_stay.Tick(out var settled) == SkillStatus.Failed)
        {
            return Fail(SpotWalkFailedWhy);
        }

        if (settled || _stay.Walking)
        {
            _started = Core.Now;
            return SkillStatus.Running;
        }

        return Core.Now - _started >= _duration ? SkillStatus.Done : SkillStatus.Running;
    }

    public override void Abort() => _stay.Abort();

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _stay.Resume(held);
    }
}
