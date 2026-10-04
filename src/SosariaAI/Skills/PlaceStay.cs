using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The stay at the end of a leisure walk (a tavern, a visit, a sight): the person makes the
/// spot it stands on its home for a while, loiters there, may say a few scripted lines (no
/// model call), then walks out of any building it stands in. The home it had before the step
/// comes back when the step ends.
/// </summary>
public sealed class PlaceStay
{
    private readonly TimeSpan _length;
    private readonly int _stayChance;
    private readonly Func<int, bool> _chat;
    private SosariaCharacter _character;
    private HomeRange? _homeBefore;
    private BuildingLeave _leave;
    private DateTime _start;
    private DateTime _chatAt;
    private int _chatTurns;

    /// <param name="length">How long the person stays before it walks out.</param>
    /// <param name="stayChance">How long the person stands still between strolls (<see cref="CharacterMotor.LoiterInHome"/>).</param>
    /// <param name="chat">
    /// One chat turn, given the turns said so far; true when a line was said. Null for a
    /// quiet stay.
    /// </param>
    public PlaceStay(TimeSpan length, int stayChance, Func<int, bool> chat)
    {
        _length = length;
        _stayChance = stayChance;
        _chat = chat;
    }

    /// <summary>True once the stay began, or once the person set out of a building instead of a walk.</summary>
    public bool Started { get; private set; }

    /// <summary>At the start of the step: keeps the person's home for its end and clears the last stay.</summary>
    public void Prepare(SosariaCharacter character)
    {
        _character = character;
        _homeBefore = HomeRange.Capture(character);
        _leave = null;
        Started = false;
        _chatAt = default;
        _chatTurns = 0;
    }

    /// <summary>
    /// On arrival: the spot the person stands on is its home within <paramref name="radius"/>
    /// tiles, and the stay and the first chat wait start.
    /// </summary>
    public void Settle(int radius)
    {
        Started = true;
        _start = Core.Now;
        _chatAt = Core.Now + MeetingRules.FirstChatDelay;
        _character.Home = _character.Location;
        _character.RangeHome = radius;
    }

    /// <summary>Walks out of the building now, with no stay. False when no way out begins.</summary>
    public bool LeaveNow()
    {
        _leave = new BuildingLeave();

        if (!_leave.Begin(_character))
        {
            _leave = null;
            return false;
        }

        Started = true;
        _start = Core.Now;
        return true;
    }

    /// <summary>Loiters and chats while the stay lasts, then walks out of any building.</summary>
    public SkillStatus Tick()
    {
        if (_leave != null)
        {
            return _leave.Tick();
        }

        _character.Motor.LoiterInHome(_stayChance);
        ConsiderChat();

        if (Core.Now - _start < _length)
        {
            return SkillStatus.Running;
        }

        _leave = new BuildingLeave();
        return _leave.Begin(_character) ? SkillStatus.Running : SkillStatus.Done;
    }

    /// <summary>Gives the person its home back once the step's <paramref name="status"/> is final.</summary>
    public SkillStatus Finish(SkillStatus status)
    {
        if (status != SkillStatus.Running)
        {
            RestoreHome();
        }

        return status;
    }

    public void Abort()
    {
        _leave?.Abort();
        _leave = null;
        Started = false;
        RestoreHome();
    }

    public void Resume(TimeSpan held)
    {
        _start = SkillClock.Shift(_start, held);
        _chatAt = SkillClock.Shift(_chatAt, held);
    }

    private void ConsiderChat()
    {
        if (_chat == null || _chatTurns >= MeetingRules.ChatMaxTurns || Core.Now < _chatAt)
        {
            return;
        }

        _chatAt = Core.Now + MeetingRules.ChatGap;

        if (_chat(_chatTurns))
        {
            _chatTurns++;
        }
    }

    private void RestoreHome()
    {
        _homeBefore?.Restore(_character);
        _homeBefore = null;
    }
}
