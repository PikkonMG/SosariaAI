using System;

namespace SosariaAI.Mobiles;

public static class ReturnSchedule
{
    public static bool IsScheduled(DateTime returnAt) => returnAt != default;

    public static TimeSpan DelayUntil(DateTime returnAt, DateTime now)
    {
        if (returnAt == default)
        {
            return TimeSpan.Zero;
        }

        var delay = returnAt - now;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    public static DateTime At(DateTime now, TimeSpan delay) => now + delay;
}
