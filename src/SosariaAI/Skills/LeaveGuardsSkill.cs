using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The way out for a red caught under the guards, and then home. Out first: a recall home when
/// it can cast one now, else a run to the nearest open ground a straight walk reaches
/// (<see cref="EscapeRoute.WayOutOfGuards"/>), which ends the moment the red stands clear.
/// Only then the walk home (<see cref="GoHomeSkill"/>), whose road out of a town, when the red
/// is still in one, takes the least guarded way. A plain walk home from under the guards led
/// reds through the town: "is red under the guards and heads for the Den", then dead one or two
/// seconds later. The bail-out looks the eight ways a few tiles out and runs for the open one.
/// </summary>
public sealed class LeaveGuardsSkill : Skill
{
    /// <summary>The run ends on the open tile itself: the tile beside it may still be under the guards.</summary>
    public const int OnTheTile = 0;

    /// <summary>A red caught under the guards again soon after is told once, not on every scan that starts the way out.</summary>
    public static readonly TimeSpan StartLineQuiet = TimeSpan.FromMinutes(1);

    private static readonly LogGate<Serial> StartLines = new(StartLineQuiet);

    private SosariaCharacter _character;
    private TravelCastSkill _recall;
    private GoToSkill _run;
    private GoHomeSkill _home;

    /// <summary>A way home, so the danger check and the fight hook leave it running as they leave a walk home.</summary>
    public override string Name => SkillKinds.GoHome;

    /// <summary>Hands <paramref name="red"/> the way out unless it is on it already.</summary>
    public static void StartFor(SosariaCharacter red)
    {
        if (red.Routine?.CurrentSkill is LeaveGuardsSkill)
        {
            return;
        }

        WorldPlay.StartWork(red, new LeaveGuardsSkill());

        if (StartLines.Opens(red.Serial, Core.Now))
        {
            WorldPlay.Log($"{red.Name} is red under the guards and gets out before it heads for the Den");
        }
    }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _recall = null;
        _run = null;
        _home = null;
        character.SetRunPace();
        return BeginRecall() || BeginRun() || BeginHome() || CannotStart(_home.FailReason);
    }

    public override SkillStatus Tick()
    {
        if (_character?.Deleted != false)
        {
            return Fail(LeftWorldReason);
        }

        if (_recall != null)
        {
            return TickRecall();
        }

        return _run != null ? TickRun() : TickHome();
    }

    public override void Abort()
    {
        _recall?.Abort();
        _recall = null;
        _run?.Abort();
        _run = null;
        _home?.Abort();
        _home = null;
    }

    public override void Resume(TimeSpan held)
    {
        _recall?.Resume(held);
        _run?.Resume(held);
        _home?.Resume(held);
    }

    /// <summary>A recall that landed took the red home; one that would not take leaves the run and the walk.</summary>
    private SkillStatus TickRecall()
    {
        var status = _recall.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _recall = null;

        if (status == SkillStatus.Done)
        {
            return SkillStatus.Done;
        }

        return BeginRun() || BeginHome() ? SkillStatus.Running : Fail(_home.FailReason);
    }

    /// <summary>The run ends where the guards end, or where it stalls; the walk home goes on from there.</summary>
    private SkillStatus TickRun()
    {
        if (SosariaCharacter.UnderGuards(_character))
        {
            var status = _run.Tick();

            if (status == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }
        }
        else
        {
            _run.Abort();
            WorldPlay.Log($"{_character.Name} stands clear of the guards at {_character.Location}");
        }

        _run = null;
        return BeginHome() ? SkillStatus.Running : Fail(_home.FailReason);
    }

    private SkillStatus TickHome()
    {
        var status = _home?.Tick() ?? SkillStatus.Failed;
        return status == SkillStatus.Failed ? Fail(_home?.FailReason) : status;
    }

    private bool BeginRecall()
    {
        var recall = new RecallSkill(_character.HomeSpot, RecallOutRules.MinTripTiles);

        if (!recall.Begin(_character))
        {
            return false;
        }

        _recall = recall;
        WorldPlay.Log($"{_character.Name} recalls home out of the guards from {_character.Location}");
        return true;
    }

    private bool BeginRun()
    {
        if (EscapeRoute.WayOutOfGuards(_character) is not { } open)
        {
            return false;
        }

        var run = new GoToSkill(open, OnTheTile);

        if (!run.Begin(_character))
        {
            return false;
        }

        _run = run;
        WorldPlay.Log($"{_character.Name} runs out of the guards from {_character.Location} to {open}");
        return true;
    }

    private bool BeginHome()
    {
        _home = new GoHomeSkill();
        return _home.Begin(_character);
    }
}
