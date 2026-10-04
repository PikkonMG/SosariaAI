using System;

namespace SosariaAI.Combat;

/// <summary>Which way the trade of blows is going.</summary>
public enum FightOutlook
{
    /// <summary>Too little of the fight seen yet to tell.</summary>
    Unknown,

    /// <summary>The foes fall well before this character would.</summary>
    Winning,

    /// <summary>Neither side is clearly ahead.</summary>
    Even,

    /// <summary>This character falls before the foes do.</summary>
    Losing
}

/// <summary>
/// The trade of blows over the last few seconds: hit points this character lost per second
/// (after its own healing), hit points the foes lost per second, and what they have left.
/// </summary>
public readonly record struct FightTrend(double LostPerSecond, double DealtPerSecond, int FoeHitsLeft, long SampleMs);

/// <summary>
/// The fight as it is going, sampled once per danger scan: this character's hit points and a
/// running count of the hit points its foes lost. Reused between fights. World thread only.
/// </summary>
public sealed class FightLedger
{
    /// <summary>Samples kept: at one scan a second, more than the trend window.</summary>
    public const int Capacity = 12;

    private readonly long[] _at = new long[Capacity];
    private readonly int[] _hits = new int[Capacity];
    private readonly long[] _dealt = new long[Capacity];
    private int _next;
    private int _stored;
    private long _dealtTotal;

    /// <summary>Foes in this fight lost this many hit points since the last sample.</summary>
    public void Dealt(int amount) => _dealtTotal += Math.Max(0, amount);

    public void Note(long now, int hits)
    {
        _at[_next] = now;
        _hits[_next] = hits;
        _dealt[_next] = _dealtTotal;
        _next = (_next + 1) % Capacity;
        _stored = Math.Min(Capacity, _stored + 1);
    }

    /// <summary>The trend from the oldest sample inside <see cref="FightTrendRules.WindowMs"/> to the newest.</summary>
    public FightTrend Read(long now, int foeHitsLeft)
    {
        if (_stored == 0)
        {
            return new FightTrend(0, 0, foeHitsLeft, 0);
        }

        var newest = (_next - 1 + Capacity) % Capacity;
        var oldest = newest;

        for (var back = 1; back < _stored; back++)
        {
            var i = (newest - back + Capacity) % Capacity;

            if (now - _at[i] > FightTrendRules.WindowMs)
            {
                break;
            }

            oldest = i;
        }

        var spanMs = _at[newest] - _at[oldest];

        if (spanMs <= 0)
        {
            return new FightTrend(0, 0, foeHitsLeft, 0);
        }

        var seconds = spanMs / (double)CombatBrain.MillisecondsPerSecond;
        return new FightTrend(
            (_hits[oldest] - _hits[newest]) / seconds,
            (_dealt[newest] - _dealt[oldest]) / seconds,
            foeHitsLeft,
            spanMs
        );
    }

    public void Clear()
    {
        _next = 0;
        _stored = 0;
        _dealtTotal = 0;
    }
}

/// <summary>
/// Judges a fight from what is happening in it, not from how many foes there are or what they
/// are on paper. Three orcs that barely scratch a fighter who kills one every few seconds are a
/// fight he wins; standing is right. A trend that says he falls first is the reason to leave.
/// </summary>
public static class FightTrendRules
{
    /// <summary>The trend reads this far back.</summary>
    public const int WindowMs = 6000;

    /// <summary>Less of the fight seen than this is no trend yet.</summary>
    public const int MinSampleMs = 3000;

    /// <summary>Winning means the foes fall this much sooner than this character would.</summary>
    public const double WinMargin = 1.5;

    /// <summary>Light damage loses under this share of full hits over the horizon.</summary>
    public const double LightDamageShare = 0.25;

    public const int HorizonSeconds = 10;

    /// <summary>Below this share of hits even a winning trade is left: one bad swing ends it.</summary>
    public const double WinningFloor = 0.25;

    public static FightOutlook Outlook(FightTrend trend, int hits)
    {
        if (trend.SampleMs < MinSampleMs)
        {
            return FightOutlook.Unknown;
        }

        var dealing = trend.DealtPerSecond > 0;

        if (trend.LostPerSecond <= 0)
        {
            return dealing ? FightOutlook.Winning : FightOutlook.Even;
        }

        var secondsToFall = Math.Max(0, hits) / trend.LostPerSecond;

        if (!dealing)
        {
            return FightOutlook.Losing;
        }

        var secondsToWin = trend.FoeHitsLeft / trend.DealtPerSecond;

        if (secondsToWin * WinMargin <= secondsToFall)
        {
            return FightOutlook.Winning;
        }

        return secondsToFall < secondsToWin ? FightOutlook.Losing : FightOutlook.Even;
    }

    /// <summary>The blows coming in would take less than a quarter of full hits over the next ten seconds.</summary>
    public static bool LightDamage(FightTrend trend, int hitsMax) =>
        trend.SampleMs >= MinSampleMs && trend.LostPerSecond * HorizonSeconds < hitsMax * LightDamageShare;
}
