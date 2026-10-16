using System;
using Server;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Works one practice skill for a few minutes where the person stands: a warrior swinging
/// at the bank, a mage casting while idling, a thief hiding in a corner. A try begins every
/// <see cref="PracticeRules.RepInterval"/> after the last one ended; between tries the person
/// mills about. A try that takes more than one tick (a spell's words then its aim, a bandage
/// and its wait, a meditation's trance) is ticked every tick until it ends, and only then
/// counts. Ticked once per interval and begun again each time, a cast never reached its aim
/// and a tending never finished. The session ends only between tries; a try that runs past
/// <see cref="RepLimit"/> is stopped as a failed try. The session carries the skill's own
/// name, so plans and logs see the skill.
/// </summary>
public sealed class PracticeSession : Skill
{
    /// <summary>The longest try is a meditation's sitting; a try gets that and one more interval to end on its own.</summary>
    private static readonly TimeSpan RepLimit = MeditateRules.SitLimit + PracticeRules.RepInterval;

    private readonly Skill _practice;
    private SosariaCharacter _character;
    private DateTime _started;
    private DateTime _lastRep;
    private DateTime _repStarted;
    private TimeSpan _length;
    private bool _practiceReady;
    private bool _repRunning;
    private int _successes;
    private int _failuresInRow;

    public PracticeSession(Skill practice) =>
        _practice = practice ?? throw new ArgumentNullException(nameof(practice));

    public override string Name => _practice.Name;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _repRunning = false;

        if (character == null || !BeginPractice())
        {
            return CannotStart(PracticeWhy());
        }

        _practiceReady = true;
        _started = Core.Now;
        _lastRep = default;
        _length = PracticeRules.SessionLength(Utility.Random(int.MaxValue));
        _successes = 0;
        _failuresInRow = 0;
        character.Home = character.Location;
        character.RangeHome = PracticeRules.MillRadius;
        return true;
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || _character.Map == null || _character.Map == Map.Internal)
        {
            return SkillStatus.Failed;
        }

        var now = Core.Now;

        if (_repRunning)
        {
            NoteRep(now - _repStarted >= RepLimit ? EndOverrun() : TickRep(), now);
        }
        else if (TimeRules.Rested(_lastRep, now, PracticeRules.RepInterval))
        {
            _repStarted = now;
            NoteRep(StartRep(), now);
        }
        else if (!PracticeRules.StandsStill(Name))
        {
            _character.Motor.LoiterInHome(PracticeRules.MillStayChance);
        }

        if (_repRunning)
        {
            return SkillStatus.Running;
        }

        var outcome = PracticeRules.Outcome(_successes, _failuresInRow, now - _started, _length);
        return outcome == SkillStatus.Failed ? Fail(PracticeWhy()) : outcome;
    }

    public override void Abort()
    {
        _practice.Abort();
        _repRunning = false;
        _character = null;
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _lastRep = SkillClock.Shift(_lastRep, held);

        if (_repRunning)
        {
            _repStarted = SkillClock.Shift(_repStarted, held);
            _practice.Resume(held);
        }
    }

    // The first try reuses the check Begin already passed; each later try begins again,
    // because the skill's own rules (mana, a target, being hidden) change between tries.
    private SkillStatus StartRep()
    {
        var ready = _practiceReady || BeginPractice();
        _practiceReady = false;
        return ready ? TickRep() : SkillStatus.Failed;
    }

    private SkillStatus TickRep()
    {
        var status = _practice.Tick();
        _repRunning = status == SkillStatus.Running;

        if (status == SkillStatus.Failed)
        {
            _practice.Abort();
        }

        return status;
    }

    /// <summary>A try that never ended on its own is stopped and counts as a failure.</summary>
    private SkillStatus EndOverrun()
    {
        _practice.Abort();
        _repRunning = false;
        return SkillStatus.Failed;
    }

    /// <summary>Each try starts with no reason of its own, so a failure names the last try's cause.</summary>
    private bool BeginPractice()
    {
        _practice.ClearFailReason();
        return _practice.Begin(_character);
    }

    private string PracticeWhy() => _practice.FailReason ?? PracticeRules.NothingToWorkOnWhy;

    /// <summary>A try that ended counts, and the wait for the next one runs from its end.</summary>
    private void NoteRep(SkillStatus status, DateTime now)
    {
        if (status == SkillStatus.Running)
        {
            return;
        }

        _lastRep = now;

        if (status == SkillStatus.Done)
        {
            _successes++;
            _failuresInRow = 0;
        }
        else
        {
            _failuresInRow++;
        }
    }
}
