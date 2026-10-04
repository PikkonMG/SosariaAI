using System;

namespace SosariaAI.Behaviour;

public enum DayPart
{
    Night,
    Work,
    Evening
}

public static class DayShapeRules
{
    public const int DefaultStartHour = 7;
    public const int DefaultEndHour = 21;
    public const int EveningHours = 4;
    public const int HoursPerDay = 24;

    public const double NeutralWeight = 1.0;
    public const double WorkActiveBoost = 1.4;
    public const double WorkSocialCut = 0.4;
    public const double EveningSocialBoost = 1.6;
    public const double EveningWorkCut = 0.5;
    public const double EveningHuntCut = 0.7;
    public const double NightRestBoost = 1.8;
    public const double NightHuntCut = 0.2;
    public const double NightWorkCut = 0.3;

    public static int LocalHour(DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).Hour;
    }

    public static DayPart Part(int hour, int? startHour, int? endHour)
    {
        var start = NormalizeHour(startHour ?? DefaultStartHour);
        var end = NormalizeHour(endHour ?? DefaultEndHour);
        hour = NormalizeHour(hour);

        if (!InActiveWindow(hour, start, end))
        {
            return DayPart.Night;
        }

        var length = ActiveLength(start, end);
        var offset = OffsetInWindow(hour, start);
        var eveningStartOffset = Math.Max(0, length - EveningHours);

        return offset >= eveningStartOffset ? DayPart.Evening : DayPart.Work;
    }

    /// <summary>
    /// How the hour of the person's own day weighs a family of activity: work, trade and
    /// outings by day, the inn and the street in the evening, rest at night. A night hunt or
    /// delve is cut hard. A job's own weights live in <see cref="JobRules"/>.
    /// </summary>
    public static double WeightMultiplier(DayPart part, RoutineFamily family) =>
        part switch
        {
            DayPart.Work => WorkWeight(family),
            DayPart.Evening => EveningWeight(family),
            DayPart.Night => NightWeight(family),
            _ => NeutralWeight
        };

    private static double WorkWeight(RoutineFamily family) =>
        family switch
        {
            RoutineFamily.Work or RoutineFamily.Hunt or RoutineFamily.Dungeon or RoutineFamily.Trade => WorkActiveBoost,
            RoutineFamily.Leisure => WorkSocialCut,
            _ => NeutralWeight
        };

    private static double EveningWeight(RoutineFamily family) =>
        family switch
        {
            RoutineFamily.Leisure => EveningSocialBoost,
            RoutineFamily.Work => EveningWorkCut,
            RoutineFamily.Hunt or RoutineFamily.Dungeon => EveningHuntCut,
            _ => NeutralWeight
        };

    private static double NightWeight(RoutineFamily family) =>
        family switch
        {
            RoutineFamily.Rest => NightRestBoost,
            RoutineFamily.Hunt or RoutineFamily.Dungeon => NightHuntCut,
            RoutineFamily.Work => NightWorkCut,
            _ => NeutralWeight
        };

    private static bool InActiveWindow(int hour, int start, int end)
    {
        if (start == end)
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }

    private static int ActiveLength(int start, int end)
    {
        if (start == end)
        {
            return HoursPerDay;
        }

        if (start < end)
        {
            return end - start;
        }

        return HoursPerDay - start + end;
    }

    private static int OffsetInWindow(int hour, int start) =>
        (hour - start + HoursPerDay) % HoursPerDay;

    internal static int NormalizeHour(int hour) => ((hour % HoursPerDay) + HoursPerDay) % HoursPerDay;
}
