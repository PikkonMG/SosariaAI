using System;

namespace SosariaAI.Behaviour;

/// <summary>
/// Idle speech uses a random delay around the configured interval so the town
/// does not speak in lockstep.
/// </summary>
public static class SpeechTiming
{
    public const int MinScalePercent = 70;
    public const int MaxScalePercent = 180;
    public const int PercentRange = MaxScalePercent - MinScalePercent;

    public static TimeSpan NextDelay(TimeSpan configured, int rollPercent)
    {
        var clamped = rollPercent < 0 ? 0 : rollPercent > PercentRange ? PercentRange : rollPercent;
        var scale = (MinScalePercent + clamped) / 100.0;
        var ms = Math.Max(1, configured.TotalMilliseconds * scale);
        return TimeSpan.FromMilliseconds(ms);
    }
}
