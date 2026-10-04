using SosariaAI.Behaviour;

namespace SosariaAI.Population;

/// <summary>
/// Each person's own waking hours. An authored persona keeps its hours; anyone else gets a
/// window seeded by who they are: early risers, evening players and night owls. Nobody leaves
/// the world for them: the hours set the parts of a person's day (<see cref="DayShapeRules.Part"/>,
/// read for musing, the model prompts and the staff watch), and a fixture outside them stands about where it is
/// (<see cref="LifecycleClock"/>).
/// </summary>
public static class SessionHours
{
    /// <summary>The earliest hour a seeded day starts.</summary>
    public const int EarliestStartHour = 5;

    /// <summary>Seeded days start somewhere in this many hours after the earliest start.</summary>
    public const int StartSpreadHours = 14;

    public const int MinActiveHours = 7;
    public const int MaxActiveHours = 15;

    private const int InclusiveSpanPad = 1;
    private const uint StartSalt = 0x51A7;
    private const uint LengthSalt = 0x1E57;

    /// <summary>
    /// The person's active window. A missing start or end is seeded from
    /// <paramref name="serial"/>; an authored hour is kept.
    /// </summary>
    public static (int StartHour, int EndHour) Resolve(uint serial, int? start, int? end)
    {
        var seededStart = EarliestStartHour + Spread(serial ^ StartSalt, StartSpreadHours);
        var seededLength = MinActiveHours + Spread(serial ^ LengthSalt, MaxActiveHours - MinActiveHours + InclusiveSpanPad);
        var startHour = start ?? (end is { } authoredEnd ? authoredEnd - seededLength : seededStart);
        var endHour = end ?? startHour + seededLength;
        return (DayShapeRules.NormalizeHour(startHour), DayShapeRules.NormalizeHour(endHour));
    }

    /// <summary>True when <paramref name="hour"/> falls inside the window; one that starts and ends on the same hour is always on.</summary>
    public static bool IsActive(int hour, int startHour, int endHour) =>
        DayShapeRules.Part(hour, startHour, endHour) != DayPart.Night;

    private static int Spread(uint seed, int span) => (int)(ChoiceSeed.Mix(seed) % (uint)span);
}
