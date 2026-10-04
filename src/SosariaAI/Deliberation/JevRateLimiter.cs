using System;
using System.Collections.Generic;

namespace SosariaAI.Deliberation;

/// <summary>
/// Client-side pacing for the one Jev transport: at most <see cref="MaxRequestsPerMinute"/>
/// sends in any sliding minute, and a shared hold after a 429 or 529 that doubles on each
/// throttle in a row. A good reply clears the doubling. The worker's drain tasks share one
/// instance, so every member takes the lock.
/// </summary>
public sealed class JevRateLimiter
{
    /// <summary>TypeSafe's documented request limit.</summary>
    public const int MaxRequestsPerMinute = 1200;

    public const int FirstBackoffMilliseconds = 500;
    public const int BackoffFactor = 2;
    public const int MaxBackoffMilliseconds = 30_000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Queue<DateTime> _sent = new();
    private readonly int _maxPerMinute;
    private DateTime _holdUntil;
    private int _throttles;

    public JevRateLimiter(int maxPerMinute = MaxRequestsPerMinute) => _maxPerMinute = Math.Max(1, maxPerMinute);

    /// <summary>
    /// Takes one send slot. False gives the wait until a slot frees: the end of a throttle
    /// hold, or the moment the oldest send in the window leaves it.
    /// </summary>
    public bool TryAcquire(DateTime now, out TimeSpan wait)
    {
        lock (_gate)
        {
            wait = WaitFor(now);

            if (wait > TimeSpan.Zero)
            {
                return false;
            }

            _sent.Enqueue(now);
            return true;
        }
    }

    /// <summary>A send would go out now. The game loop asks this before it queues a decision.</summary>
    public bool Allows(DateTime now)
    {
        lock (_gate)
        {
            return WaitFor(now) <= TimeSpan.Zero;
        }
    }

    /// <summary>The API said 429 or 529: hold every send for the next backoff step.</summary>
    public TimeSpan Throttle(DateTime now)
    {
        lock (_gate)
        {
            var hold = Backoff(++_throttles);
            var until = now + hold;

            if (until > _holdUntil)
            {
                _holdUntil = until;
            }

            return hold;
        }
    }

    /// <summary>A reply came back; the next throttle starts from the first step again.</summary>
    public void Succeeded()
    {
        lock (_gate)
        {
            _throttles = 0;
        }
    }

    public static TimeSpan Backoff(int throttlesInARow)
    {
        var milliseconds = FirstBackoffMilliseconds;

        for (var i = 1; i < throttlesInARow && milliseconds < MaxBackoffMilliseconds; i++)
        {
            milliseconds *= BackoffFactor;
        }

        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaxBackoffMilliseconds));
    }

    private TimeSpan WaitFor(DateTime now)
    {
        if (now < _holdUntil)
        {
            return _holdUntil - now;
        }

        var windowStart = now - Window;

        while (_sent.Count > 0 && _sent.Peek() <= windowStart)
        {
            _sent.Dequeue();
        }

        return _sent.Count < _maxPerMinute ? TimeSpan.Zero : _sent.Peek() + Window - now;
    }
}
