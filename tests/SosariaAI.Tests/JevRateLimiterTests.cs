using System;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevRateLimiterTests
{
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryAcquire_AllowsTheDocumentedLimitInAMinute()
    {
        var limiter = new JevRateLimiter();

        for (var i = 0; i < JevRateLimiter.MaxRequestsPerMinute; i++)
        {
            Assert.True(limiter.TryAcquire(Start.AddMilliseconds(i), out _));
        }

        Assert.False(limiter.TryAcquire(Start.AddSeconds(30), out var wait));
        Assert.Equal(TimeSpan.FromSeconds(30), wait);
        Assert.False(limiter.Allows(Start.AddSeconds(30)));
    }

    [Fact]
    public void TryAcquire_FreesASlotWhenTheOldestSendLeavesTheWindow()
    {
        var limiter = new JevRateLimiter(2);

        Assert.True(limiter.TryAcquire(Start, out _));
        Assert.True(limiter.TryAcquire(Start.AddSeconds(10), out _));
        Assert.False(limiter.TryAcquire(Start.AddSeconds(59), out _));
        Assert.True(limiter.TryAcquire(Start.AddSeconds(60), out _));
    }

    [Fact]
    public void Throttle_HoldsEverySendAndDoublesInARow()
    {
        var limiter = new JevRateLimiter();

        var first = limiter.Throttle(Start);
        var second = limiter.Throttle(Start);

        Assert.Equal(TimeSpan.FromMilliseconds(JevRateLimiter.FirstBackoffMilliseconds), first);
        Assert.Equal(first * JevRateLimiter.BackoffFactor, second);
        Assert.False(limiter.TryAcquire(Start.AddMilliseconds(500), out var wait));
        Assert.Equal(TimeSpan.FromMilliseconds(500), wait);
        Assert.True(limiter.TryAcquire(Start + second, out _));
    }

    [Fact]
    public void Succeeded_StartsTheNextThrottleFromTheFirstStep()
    {
        var limiter = new JevRateLimiter();
        limiter.Throttle(Start);
        limiter.Throttle(Start);
        limiter.Succeeded();

        Assert.Equal(TimeSpan.FromMilliseconds(JevRateLimiter.FirstBackoffMilliseconds), limiter.Throttle(Start));
    }

    [Fact]
    public void Backoff_StopsAtTheCeiling() =>
        Assert.Equal(TimeSpan.FromMilliseconds(JevRateLimiter.MaxBackoffMilliseconds), JevRateLimiter.Backoff(50));
}
