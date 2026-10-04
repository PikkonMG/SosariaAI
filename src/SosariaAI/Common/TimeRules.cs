using System;

namespace SosariaAI.Common;

/// <summary>Rest and wait checks on a remembered time, where an unset time means "never". Pure.</summary>
public static class TimeRules
{
    /// <summary>True when the rest since <paramref name="last"/> is over, or nothing happened yet.</summary>
    public static bool Rested(DateTime last, DateTime now, TimeSpan rest) => last == default || now - last >= rest;

    /// <summary>True when something started at <paramref name="since"/> and <paramref name="span"/> has gone by.</summary>
    public static bool Passed(DateTime since, DateTime now, TimeSpan span) => since != default && now - since >= span;
}
