using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

public static class ActivityLogLimit
{
    public static readonly TimeSpan FailureLogWindow = TimeSpan.FromMinutes(1);

    public static bool AllowFailureLog(DateTime lastLog, DateTime now) =>
        TimeRules.Rested(lastLog, now, FailureLogWindow);

    /// <summary>
    /// True while a failing step is still inside its quiet window, so the lines around it
    /// (the start and the routine restart) stay out of the log as well.
    /// </summary>
    public static bool InQuietWindow(DateTime lastFailureLog, DateTime now) =>
        !TimeRules.Rested(lastFailureLog, now, FailureLogWindow);
}
