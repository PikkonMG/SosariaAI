using System;
using Server;

namespace SosariaAI.Mobiles;

/// <summary>
/// The heartbeat of one character. A PlayerMobile has no AI timer, so this one-shot timer
/// re-arms itself: it wakes at the next think, or sooner when a walk wants its next step.
/// Steps run on the move clock; decisions run on the think clock.
/// </summary>
public sealed class CharacterPulse
{
    public const int MinWakeMs = 25;

    private readonly SosariaCharacter _character;
    private TimerExecutionToken _timer;
    private long _nextThinkAt;

    public CharacterPulse(SosariaCharacter character) => _character = character;

    public void Start()
    {
        if (_timer.Running || !InWorld)
        {
            return;
        }

        _nextThinkAt = Core.TickCount + StaggerMs();
        Arm();
    }

    public void Stop() => _timer.Cancel();

    /// <summary>Brings the next think forward, for a hit or a word that needs an answer now.</summary>
    public void WakeNow()
    {
        if (!InWorld)
        {
            return;
        }

        _nextThinkAt = Core.TickCount;
        _timer.Cancel();
        Arm();
    }

    private bool InWorld =>
        !_character.Deleted && People.InWorld(_character);

    // Every character loads in the same tick. A per-serial offset keeps them from
    // thinking in lockstep.
    private long StaggerMs() =>
        _character.Serial.Value % Math.Max(1, (long)_character.ThinkDelay.TotalMilliseconds);

    private void Arm()
    {
        var now = Core.TickCount;
        var wakeAt = _nextThinkAt;
        var motor = _character.Motor;

        if (motor.HasMoveIntent && motor.NextMoveAt < wakeAt)
        {
            wakeAt = motor.NextMoveAt;
        }

        var delay = Math.Max(MinWakeMs, wakeAt - now);
        Timer.StartTimer(TimeSpan.FromMilliseconds(delay), Beat, out _timer);
    }

    private void Beat()
    {
        if (!InWorld)
        {
            return;
        }

        var now = Core.TickCount;

        if (now - _nextThinkAt >= 0)
        {
            _nextThinkAt = now + (long)_character.ThinkDelay.TotalMilliseconds;
            RoutineDriver.Think(_character);
        }
        else
        {
            _character.Motor.ContinueMove();
        }

        if (InWorld)
        {
            Arm();
        }
    }
}
