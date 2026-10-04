using System;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A skill that is one travel spell: cast it, wait for the words and the cursor, cast again
/// after a fizzle a few times, and fail when it will not take so the walk takes over. A cast
/// that did not take is tried again as soon as the next one can begin, within
/// <see cref="RecallRules.RecastWait"/>: the recovery after the words, or a person on the landing.
/// </summary>
public abstract class TravelCastSkill : Skill
{
    private int _casts;
    private bool _took;
    private DateTime _recastEnds;

    protected SosariaCharacter Character { get; private set; }

    public override bool Begin(SosariaCharacter character)
    {
        Character = character;
        _casts = 0;
        _took = false;
        _recastEnds = default;
        return Cast();
    }

    public override SkillStatus Tick()
    {
        if (_took)
        {
            return AfterCast();
        }

        switch (TravelSpells.TakeOutcome(Character))
        {
            case TravelCastOutcome.Casting:
                return SkillStatus.Running;
            case TravelCastOutcome.Succeeded:
                _took = true;
                return AfterCast();
            default:
                return CastAgain();
        }
    }

    /// <summary>A cast still under way is broken off, so it neither lands nor leaves an outcome for the next.</summary>
    public override void Abort()
    {
        TravelSpells.Cancel(Character);
        Character = null;
    }

    public override void Resume(TimeSpan held) => _recastEnds = SkillClock.Shift(_recastEnds, held);

    /// <summary>Begins one cast. False when it could not start.</summary>
    protected abstract bool BeginCast(SosariaCharacter character);

    /// <summary>What follows a cast that took. Most travel spells are done then.</summary>
    protected virtual SkillStatus AfterCast() => SkillStatus.Done;

    /// <summary>
    /// The last cast did not take: cast again while tries are left, waiting out a refusal
    /// that passes. The tries count casts that began, so a wait costs none.
    /// </summary>
    private SkillStatus CastAgain()
    {
        if (!RecallRules.ShouldCastAgain(_casts))
        {
            return Fail(RecallRules.CastsSpentWhy);
        }

        if (_recastEnds == default)
        {
            _recastEnds = Core.Now + RecallRules.RecastWait;
        }

        if (Cast())
        {
            _recastEnds = default;
            return SkillStatus.Running;
        }

        return RecallRules.MayRecast(_casts, _recastEnds, Core.Now) ? SkillStatus.Running : Fail(RecallRules.RecastRefusedWhy);
    }

    private bool Cast()
    {
        if (!BeginCast(Character))
        {
            return false;
        }

        _casts++;
        return true;
    }
}
