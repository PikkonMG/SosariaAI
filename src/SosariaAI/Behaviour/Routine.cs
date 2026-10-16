using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Runs one skill until it ends. The goal loop then picks the next skill.
/// </summary>
public sealed class Routine
{
    public const int FailedPauseTicks = 2;

    private Skill _skill;
    private SosariaCharacter _character;
    private bool _begun;
    private int _failedPauseRemaining;
    private int _doneHoldRemaining;
    private int _ticksRun;
    private bool _aborted;
    private DateTime _pausedAt;
    private DateTime _startedAt;

    public Routine(Skill skill) => _skill = skill ?? throw new ArgumentNullException(nameof(skill));

    public event Action<SosariaCharacter, Skill> SkillStarted;

    public event Action<SosariaCharacter, Skill, SkillStatus> SkillEnded;

    public Skill CurrentSkill => _skill;

    public bool NeedsNext { get; private set; }

    public bool WasAborted => _aborted;

    public bool HasSkill(string name) => !string.IsNullOrEmpty(name) && _skill.Name == name;

    public void Restart()
    {
        AbortActive();
        _failedPauseRemaining = 0;
        _doneHoldRemaining = 0;
        _aborted = false;
        NeedsNext = false;
    }

    public void Bind(SosariaCharacter character)
    {
        _character = character;
    }

    public void Replace(Skill skill) => Replace(skill, keepFailPause: false);

    public void Replace(Skill skill, bool keepFailPause)
    {
        ArgumentNullException.ThrowIfNull(skill);
        AbortActive();
        _skill = skill;
        _begun = false;
        _aborted = false;
        _doneHoldRemaining = 0;
        NeedsNext = false;

        if (!keepFailPause)
        {
            _failedPauseRemaining = 0;
        }
    }

    public void Tick()
    {
        if (NeedsNext)
        {
            return;
        }

        if (_doneHoldRemaining > 0)
        {
            if (--_doneHoldRemaining == 0)
            {
                NeedsNext = true;
            }

            return;
        }

        if (_failedPauseRemaining > 0)
        {
            _failedPauseRemaining--;
            return;
        }

        var skill = _skill;

        if (!_begun)
        {
            // Begin may replace the skill mid-call: a Decide step resolves to a new skill while
            // the brain is off. The new skill must run its own Begin on the next tick, so do
            // not mark this stale one begun.
            skill.ClearFailReason();
            var started = !skill.RefusesRestingTarget(_character) && skill.Begin(_character);

            if (!ReferenceEquals(skill, _skill))
            {
                return;
            }

            if (!started)
            {
                EndSkill(skill, SkillStatus.Failed);
                return;
            }

            _begun = true;
            _startedAt = Core.Now;
            _ticksRun = 0;
            RunRules.ApplyPace(_character, skill.Name);
            SkillStarted?.Invoke(_character, skill);
        }

        _ticksRun++;
        var status = skill.Tick();

        if (status != SkillStatus.Running)
        {
            EndSkill(skill, status);
        }
    }

    /// <summary>
    /// The routine is held (a conversation, for one) and its skill is not ticked. The
    /// held time is given back to the skill's clocks on <see cref="Resume"/>.
    /// </summary>
    public void Pause(DateTime now)
    {
        if (_pausedAt == default)
        {
            _pausedAt = now;
        }
    }

    public void Resume(DateTime now)
    {
        if (_pausedAt == default)
        {
            return;
        }

        var held = now - _pausedAt;
        _pausedAt = default;

        if (_begun && held > TimeSpan.Zero)
        {
            _startedAt = SkillClock.Shift(_startedAt, held);
            _skill.Resume(held);
        }
    }

    public void AbortActive()
    {
        if (_begun)
        {
            _skill.Abort();
            _begun = false;
            _aborted = true;
        }

        _doneHoldRemaining = 0;
        NeedsNext = true;
    }

    /// <summary>
    /// Ends the running skill as a failure for <paramref name="why"/>, as the skill's own
    /// failure would: the end is logged and noted, so the spot bars the skill a while and the
    /// plan counts a failed step. The fleet watchdog's plain abort left no mark, and three
    /// Minoc sellers began the same stalled sale again the next second.
    /// </summary>
    public void FailActive(string why)
    {
        if (!_begun)
        {
            AbortActive();
            return;
        }

        _skill.NoteFailReason(why);
        EndSkill(_skill, SkillStatus.Failed);
    }

    /// <summary>
    /// Keep the current skill after an abort. Idle fallback uses this so a
    /// wander does not Begin again every tick.
    /// </summary>
    public bool ContinueAfterAbort()
    {
        if (!_aborted)
        {
            return false;
        }

        NeedsNext = false;
        _begun = true;
        _aborted = false;
        return true;
    }

    private void EndSkill(Skill skill, SkillStatus status)
    {
        var ran = _begun ? Core.Now - _startedAt : TimeSpan.Zero;

        if (_begun)
        {
            if (status == SkillStatus.Failed)
            {
                skill.Abort();
            }

            _begun = false;
        }

        _aborted = false;
        SkillEnded?.Invoke(_character, skill, status);

        _character?.NoteJobTarget(skill, status);
        _character?.NoteRoutineOutcome(skill.Name, status, ran);

        if (_character != null)
        {
            // Each newly selected skill checks its own route and arrival.
            _character.LastWalkFailed = status == SkillStatus.Failed && WalkArrival.IsWalk(skill.Name);
        }

        _doneHoldRemaining = RoutineDwell.HoldTicks(status, _ticksRun);
        NeedsNext = _doneHoldRemaining == 0;

        if (status == SkillStatus.Failed)
        {
            _failedPauseRemaining = FailedPauseTicks;
        }
    }
}
