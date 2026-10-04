using System;
using Server;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Works one practice skill for a few minutes where the person stands: a warrior swinging
/// at the bank, a mage casting while idling, a thief hiding in a corner. The skill is used
/// every <see cref="PracticeRules.RepInterval"/>; between tries the person mills about.
/// The session carries the skill's own name, so plans and logs see the skill.
/// </summary>
public sealed class PracticeSession : Skill
{
    private readonly Skill _practice;
    private SosariaCharacter _character;
    private DateTime _started;
    private DateTime _lastRep;
    private TimeSpan _length;
    private bool _practiceReady;
    private int _successes;
    private int _failuresInRow;

    public PracticeSession(Skill practice) =>
        _practice = practice ?? throw new ArgumentNullException(nameof(practice));

    public override string Name => _practice.Name;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;

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

        if (TimeRules.Rested(_lastRep, now, PracticeRules.RepInterval))
        {
            _lastRep = now;
            NoteRep(RunRep());
        }
        else if (!PracticeRules.StandsStill(Name))
        {
            _character.Motor.LoiterInHome(PracticeRules.MillStayChance);
        }

        var outcome = PracticeRules.Outcome(_successes, _failuresInRow, now - _started, _length);
        return outcome == SkillStatus.Failed ? Fail(PracticeWhy()) : outcome;
    }

    public override void Abort()
    {
        _practice.Abort();
        _character = null;
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);

        if (_lastRep != default)
        {
            _lastRep = SkillClock.Shift(_lastRep, held);
        }
    }

    // The first try reuses the check Begin already passed; each later try begins again,
    // because the skill's own rules (mana, a target, being hidden) change between tries.
    private SkillStatus RunRep()
    {
        var ready = _practiceReady || BeginPractice();
        _practiceReady = false;

        if (!ready)
        {
            return SkillStatus.Failed;
        }

        var status = _practice.Tick();

        if (status == SkillStatus.Failed)
        {
            _practice.Abort();
        }

        return status;
    }

    /// <summary>Each try starts with no reason of its own, so a failure names the last try's cause.</summary>
    private bool BeginPractice()
    {
        _practice.ClearFailReason();
        return _practice.Begin(_character);
    }

    private string PracticeWhy() => _practice.FailReason ?? PracticeRules.NothingToWorkOnWhy;

    private void NoteRep(SkillStatus status)
    {
        if (status == SkillStatus.Done)
        {
            _successes++;
            _failuresInRow = 0;
        }
        else if (status == SkillStatus.Failed)
        {
            _failuresInRow++;
        }
    }
}
