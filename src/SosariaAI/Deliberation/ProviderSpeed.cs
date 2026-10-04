using System;
using System.Collections.Generic;

namespace SosariaAI.Deliberation;

/// <summary>
/// How fast each provider answers lately: a moving average of its reply times, failures and
/// timeouts included. A provider with no replies yet counts as fast, so it gets a first try.
/// World thread only. Pure.
/// </summary>
public sealed class ProviderSpeed
{
    /// <summary>How much one new reply moves the average. Higher follows recent replies faster.</summary>
    public const double NewReplyWeight = 0.3;

    private readonly Dictionary<string, double> _averageMs = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string providerName, long latencyMs)
    {
        if (string.IsNullOrWhiteSpace(providerName) || latencyMs < 0)
        {
            return;
        }

        _averageMs[providerName] = _averageMs.TryGetValue(providerName, out var average)
            ? average + NewReplyWeight * (latencyMs - average)
            : latencyMs;
    }

    /// <summary>The average reply time, or null before the first reply.</summary>
    public double? AverageMs(string providerName) =>
        !string.IsNullOrWhiteSpace(providerName) && _averageMs.TryGetValue(providerName, out var average) ? average : null;

    /// <summary>True when the provider answers inside the limit on average, or has not answered yet.</summary>
    public bool IsFastEnough(string providerName, double maxMs) => (AverageMs(providerName) ?? 0) <= maxMs;
}
