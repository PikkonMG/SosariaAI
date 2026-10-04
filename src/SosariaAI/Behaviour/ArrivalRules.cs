using System;

namespace SosariaAI.Behaviour;

public enum ArrivalStyle
{
    /// <summary>Stand about for a minute or two.</summary>
    Linger,

    /// <summary>Stay a good while, as if waiting for someone.</summary>
    Wait,

    /// <summary>Move on at once to the next thing the place offers.</summary>
    Wander
}

/// <summary>
/// How a person spends the first minutes at a place a job took them to, before the next
/// thing the place offers: four in ten linger a minute or two, four in ten wait a few
/// minutes, and the rest walk straight on. A wait with no end held people at the place
/// for hours, so every stay ends. Pure. No world objects.
/// </summary>
public static class ArrivalRules
{
    public const int LingerPercent = 40;
    public const int WaitPercent = 40;
    public const int PercentScale = 100;

    public const int LingerMinSeconds = 60;
    public const int LingerMaxSeconds = 120;
    public const int WaitMinSeconds = 180;
    public const int WaitMaxSeconds = 360;

    public const int StandRadius = 3;

    /// <summary>Stay weight of each stand (<see cref="SosariaAI.Skills.LoiterPaceRules.Dwell"/>): a lingerer stands a minute.</summary>
    public const int StandStayChance = 8;

    private const int InclusiveSpanPad = 1;

    public static ArrivalStyle Style(int roll)
    {
        var percent = Math.Abs(roll % PercentScale);

        if (percent < LingerPercent)
        {
            return ArrivalStyle.Linger;
        }

        return percent < LingerPercent + WaitPercent ? ArrivalStyle.Wait : ArrivalStyle.Wander;
    }

    /// <summary>False for a person who walks straight on.</summary>
    public static bool Stays(ArrivalStyle style) => style != ArrivalStyle.Wander;

    public static TimeSpan Length(ArrivalStyle style, int roll) =>
        style switch
        {
            ArrivalStyle.Wait => Seconds(WaitMinSeconds, WaitMaxSeconds, roll),
            ArrivalStyle.Wander => TimeSpan.Zero,
            _ => Seconds(LingerMinSeconds, LingerMaxSeconds, roll)
        };

    /// <summary>A span of whole seconds between <paramref name="min"/> and <paramref name="max"/>, both in, that a roll picks.</summary>
    public static TimeSpan Seconds(int min, int max, int roll) =>
        TimeSpan.FromSeconds(min + Math.Abs(roll % (max - min + InclusiveSpanPad)));
}
