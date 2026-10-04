using System;
using System.Collections.Generic;
using System.Globalization;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// One clock hour of Jev traffic: requests sent and tokens the API reported, in all and by
/// <see cref="JevKind"/>, so every request is counted, logged or not.
/// </summary>
public readonly record struct JevUsageHour(
    int Requests,
    long InputTokens,
    IReadOnlyList<int> KindRequests,
    IReadOnlyList<long> KindTokens
)
{
    /// <summary>Input tokens are the only billed part of a Jev call.</summary>
    public double Dollars => InputTokens * BrainProviders.JevDollarsPerMillionInputTokens / BrainProviders.TokensPerMillion;

    /// <summary>The operator's cost line, one per hour and one at shutdown. Filled by <see cref="LogArgs"/>.</summary>
    public const string LogTemplate =
        "jev usage: {Requests} requests, {InputTokens} input tokens, ~${Dollars:0.00} this hour ({Split})";

    public const string NoSplit = "none";

    public object[] LogArgs => [Requests, InputTokens, Dollars, Split];

    /// <summary>Requests and input tokens per kind, the kinds with no request left out.</summary>
    public string Split
    {
        get
        {
            var parts = new List<string>();

            foreach (var kind in Enum.GetValues<JevKind>())
            {
                var index = (int)kind;
                var requests = KindRequests?[index] ?? 0;

                if (requests > 0)
                {
                    parts.Add(
                        $"{JevBudgetRules.Names[kind]} {requests.ToString(CultureInfo.InvariantCulture)} req/" +
                        $"{(KindTokens?[index] ?? 0).ToString(CultureInfo.InvariantCulture)} tok"
                    );
                }
            }

            return parts.Count == 0 ? NoSplit : string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Jev requests and tokens per clock hour, shared by every Jev call on the one transport: the
/// one budget all kinds spend. The worker records from its own threads and the game loop reads
/// the share used, so every member takes the lock. When the hour turns, the finished hour waits
/// for the loop to log it.
/// </summary>
public sealed class JevUsage
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly object _gate = new();
    private readonly long _maxInputTokensPerHour;
    private readonly int[] _kindRequests = new int[JevBudgetRules.KindCount];
    private readonly long[] _kindTokens = new long[JevBudgetRules.KindCount];
    private DateTime _hourStart;
    private int _requests;
    private long _inputTokens;
    private JevUsageHour? _finished;

    public JevUsage(long maxInputTokensPerHour) => _maxInputTokensPerHour = Math.Max(0, maxInputTokensPerHour);

    /// <summary>One HTTP request went out, whatever came back.</summary>
    public void RecordRequest(DateTime now, JevKind kind)
    {
        lock (_gate)
        {
            Advance(now);
            _requests++;
            _kindRequests[(int)kind]++;
        }
    }

    /// <summary>The input token count from one reply's usage field.</summary>
    public void RecordTokens(DateTime now, JevKind kind, int inputTokens)
    {
        lock (_gate)
        {
            Advance(now);
            var input = Math.Max(0, inputTokens);
            _inputTokens += input;
            _kindTokens[(int)kind] += input;
        }
    }

    /// <summary>The share of this hour's cap used so far; the next hour starts at zero.</summary>
    public double UsedShare(DateTime now)
    {
        lock (_gate)
        {
            Advance(now);
            return JevBudgetRules.UsedShare(_inputTokens, _maxInputTokensPerHour);
        }
    }

    /// <summary>The hour so far, for the shutdown line.</summary>
    public JevUsageHour Current(DateTime now)
    {
        lock (_gate)
        {
            Advance(now);
            return Snapshot();
        }
    }

    /// <summary>The last finished hour, once; null until the clock hour turns again.</summary>
    public JevUsageHour? TakeFinished(DateTime now)
    {
        lock (_gate)
        {
            Advance(now);
            var finished = _finished;
            _finished = null;
            return finished;
        }
    }

    internal static DateTime HourOf(DateTime now) => new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);

    private JevUsageHour Snapshot() =>
        new(_requests, _inputTokens, (int[])_kindRequests.Clone(), (long[])_kindTokens.Clone());

    private void Advance(DateTime now)
    {
        var hour = HourOf(now);

        if (_hourStart == default)
        {
            _hourStart = hour;
            return;
        }

        if (hour < _hourStart + Hour)
        {
            return;
        }

        _finished = Snapshot();
        _hourStart = hour;
        _requests = 0;
        _inputTokens = 0;
        Array.Clear(_kindRequests);
        Array.Clear(_kindTokens);
    }
}
