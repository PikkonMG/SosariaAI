using System;
using Server;
using SosariaAI.Logging;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// How often a person warns about reds. The same warning, from one speaker to one listener
/// about one red, goes out once in <see cref="SameWarningRest"/>, and a speaker says at most
/// one warning line in <see cref="SpeakerRest"/>. Every red warning and red scream passes this
/// gate. One anti-PK in Buccaneer's Den warned 223 times in an hour, a new red on every scan.
/// World thread only.
/// </summary>
public sealed class WarningGate
{
    /// <summary>The same warning to the same listener about the same red once in this time.</summary>
    public static readonly TimeSpan SameWarningRest = TimeSpan.FromMinutes(5);

    /// <summary>One warning line per speaker in this time, whoever it is for.</summary>
    public static readonly TimeSpan SpeakerRest = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The same red called out in the same map cell once in this time. A sighting is told once:
    /// before this gate a red through a crowd drew the same warn line from every lawful watcher.
    /// </summary>
    public static readonly TimeSpan ToldRest = TimeSpan.FromMinutes(2);

    private readonly LogGate<(Serial Speaker, Serial Listener, Serial Red)> _warnings = new(SameWarningRest);
    private readonly LogGate<Serial> _speakers = new(SpeakerRest);
    private readonly LogGate<(Serial Red, int Map, int CellX, int CellY)> _told = new(ToldRest);

    /// <summary>The gate the shard's speakers share.</summary>
    public static WarningGate Shared { get; } = new();

    /// <summary>True when the speaker may say a warning line now, before the listener is looked for.</summary>
    public bool SpeakerReady(Serial speaker, DateTime now) => _speakers.IsOpen(speaker, now);

    /// <summary>
    /// True when this warning may go out now; it then counts as said. <see cref="Serial.Zero"/>
    /// as the listener is a warning to anyone near.
    /// </summary>
    public bool TryWarn(Serial speaker, Serial listener, Serial red, DateTime now)
    {
        var warning = (speaker, listener, red);

        if (!_speakers.IsOpen(speaker, now) || !_warnings.IsOpen(warning, now))
        {
            return false;
        }

        _speakers.Note(speaker, now);
        _warnings.Note(warning, now);
        return true;
    }

    /// <summary>
    /// True when this red was already warned about in this cell inside <see cref="ToldRest"/>.
    /// A look only: call <see cref="NoteTold"/> when the warning actually goes out, so a crowd
    /// that cannot speak yet does not spend the one tell.
    /// </summary>
    public bool ToldLately(Serial red, int map, int x, int y, DateTime now) =>
        !_told.IsOpen(ToldKey(red, map, x, y), now);

    /// <summary>Marks this red as warned about in this cell.</summary>
    public void NoteTold(Serial red, int map, int x, int y, DateTime now) =>
        _told.Note(ToldKey(red, map, x, y), now);

    private static (Serial, int, int, int) ToldKey(Serial red, int map, int x, int y) =>
        (red, map, HeardLineRules.CellOf(x), HeardLineRules.CellOf(y));
}
