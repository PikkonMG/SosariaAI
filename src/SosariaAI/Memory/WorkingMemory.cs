using System;
using System.Collections.Generic;

namespace SosariaAI.Memory;

/// <summary>
/// The transient half of a character's memory: its recent thoughts, the lines it just said,
/// the skills it just ran or failed, and the conversation it is holding. None of this is
/// saved; a restart starts with an empty desk. What matters long term is an adventure or a
/// bond in <see cref="MemoryStore"/>. Main game thread only.
/// </summary>
public sealed class WorkingMemory
{
    /// <summary>How many recent thoughts a character keeps; the oldest drops first.</summary>
    public const int ThoughtCapacity = 12;

    private readonly List<string> _thoughts = [];

    /// <summary>Lines this character said lately, for repeat checks.</summary>
    public List<string> RecentSpeech { get; } = [];

    /// <summary>Skill kinds this character ran lately, so one kind does not win every pick.</summary>
    public List<string> RecentSkillKinds { get; } = [];

    /// <summary>Per-skill failure streaks and cooldowns.</summary>
    public Dictionary<string, SkillFailures> SkillFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Skills resting after a finished outing.</summary>
    public Dictionary<string, DateTime> SkillRestUntil { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The routine that failed last, and how many times in a row.</summary>
    public string FailedRoutine;
    public int FailedCount;

    /// <summary>The pending and last spoken musing, and when musings were tried or written.</summary>
    public string PendingMusingEvent;
    public string LastMusingEvent;
    public DateTime LastMusingAttempt;
    public DateTime LastWrittenMusingAt;

    /// <summary>The last living player noticed nearby.</summary>
    public string LastNearbyName;

    /// <summary>The person this character is talking with, while the window lasts.</summary>
    public ConversationHold Conversation { get; } = new();

    /// <summary>Adds a recent thought, dropping a blank line and a back-to-back repeat.</summary>
    public void Think(string line)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            _thoughts.Count > 0 && string.Equals(_thoughts[^1], line, StringComparison.Ordinal))
        {
            return;
        }

        _thoughts.Add(line);

        while (_thoughts.Count > ThoughtCapacity)
        {
            _thoughts.RemoveAt(0);
        }
    }

    /// <summary>The recent thoughts, oldest first.</summary>
    public IReadOnlyList<string> Thoughts() => _thoughts.ToArray();
}
