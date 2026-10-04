using System;

namespace SosariaAI.Deliberation;

public static class ReplyDelay
{
    public const int BaseMilliseconds = 900;
    public const int MillisecondsPerCharacter = 45;
    public const int MaxMilliseconds = 5000;

    public static TimeSpan For(string say)
    {
        var length = say?.Length ?? 0;
        var milliseconds = BaseMilliseconds + MillisecondsPerCharacter * length;

        if (milliseconds > MaxMilliseconds)
        {
            milliseconds = MaxMilliseconds;
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
