using System;
using System.Collections.Generic;

namespace SosariaAI.Deliberation;

/// <summary>Pauses only the provider whose requests keep failing.</summary>
public sealed class ProviderFailureGate
{
    private readonly Dictionary<string, (int Count, DateTime PausedUntil)> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsPaused(string providerName, DateTime now) =>
        !string.IsNullOrWhiteSpace(providerName) &&
        _states.TryGetValue(providerName, out var state) && now < state.PausedUntil;

    public bool Failed(string providerName, DateTime now, int failuresBeforePause, int pauseSeconds)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        _states.TryGetValue(providerName, out var state);
        var count = state.Count + 1;

        if (count < Math.Max(1, failuresBeforePause))
        {
            _states[providerName] = (count, state.PausedUntil);
            return false;
        }

        _states[providerName] = (0, now + TimeSpan.FromSeconds(Math.Max(0, pauseSeconds)));
        return true;
    }

    public void Succeeded(string providerName, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(providerName) || !_states.TryGetValue(providerName, out var state))
        {
            return;
        }

        if (now < state.PausedUntil)
        {
            _states[providerName] = (0, state.PausedUntil);
            return;
        }

        _states.Remove(providerName);
    }

    public void Clear() => _states.Clear();
}
