using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>The step lines the activity log writes for one character.</summary>
public enum StepLine
{
    Started,
    Ended,
    Failed
}

public static class ActivityLogLimit
{
    public static readonly TimeSpan FailureLogWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether one step line is written, and the failure time kept after it. A logged failure
    /// opens a quiet window: the starts and failures of a step that fails over and over stay out
    /// of the log until the window runs out. A step that ends without failing is always written
    /// and closes the window, so an isolated failure never hides a character's real work.
    /// </summary>
    public static (bool Write, DateTime LastFailure) Next(StepLine line, DateTime lastFailure, DateTime now) =>
        line switch
        {
            StepLine.Ended => (true, default(DateTime)),
            _ when InQuietWindow(lastFailure, now) => (false, lastFailure),
            StepLine.Failed => (true, now),
            _ => (true, lastFailure)
        };

    /// <summary>True while a failing step is still inside its quiet window.</summary>
    public static bool InQuietWindow(DateTime lastFailureLog, DateTime now) =>
        !TimeRules.Rested(lastFailureLog, now, FailureLogWindow);
}
