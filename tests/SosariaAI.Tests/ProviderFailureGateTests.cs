using System;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ProviderFailureGateTests
{
    [Fact]
    public void Failed_PausesOnlyTheFailingProvider()
    {
        var gate = new ProviderFailureGate();
        var now = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(gate.Failed("von", now, 3, 60));
        Assert.False(gate.Failed("von", now, 3, 60));
        Assert.True(gate.Failed("von", now, 3, 60));

        Assert.True(gate.IsPaused("von", now.AddSeconds(59)));
        Assert.False(gate.IsPaused("jev", now.AddSeconds(59)));
        Assert.False(gate.IsPaused("von", now.AddSeconds(60)));
    }

    [Fact]
    public void Succeeded_ClearsOnlyThatProvidersFailureStreak()
    {
        var gate = new ProviderFailureGate();
        var now = DateTime.UtcNow;

        Assert.False(gate.Failed("von", now, 2, 60));
        Assert.False(gate.Failed("jev", now, 2, 60));
        gate.Succeeded("von", now);

        Assert.False(gate.Failed("von", now, 2, 60));
        Assert.True(gate.Failed("jev", now, 2, 60));
    }

    [Fact]
    public void Succeeded_DoesNotEndAnActivePause()
    {
        var gate = new ProviderFailureGate();
        var now = DateTime.UtcNow;

        Assert.True(gate.Failed("laya", now, 1, 60));
        gate.Succeeded("laya", now.AddSeconds(10));

        Assert.True(gate.IsPaused("laya", now.AddSeconds(30)));
    }
}
