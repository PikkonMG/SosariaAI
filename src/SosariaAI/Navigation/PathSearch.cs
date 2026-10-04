using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Server;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Navigation;

/// <summary>
/// Dijkstra on a frozen nav graph. Workers never touch Mobile, Item, Map or timers.
/// The world thread still walks the returned node names.
/// </summary>
public static class PathSearch
{
    public const int MinWorkers = 1;
    public const int MaxWorkers = 8;
    public const int QueueCapacity = 256;

    private static readonly ILogger logger = SosariaLog.For(typeof(PathSearch));
    private static Channel<Job> _queue;
    private static CancellationTokenSource _cts;
    private static Task[] _workers;

    public static bool Enabled { get; private set; }

    public static int WorkerCount { get; private set; }

    public static void Start()
    {
        if (Enabled)
        {
            return;
        }

        WorkerCount = Math.Clamp(Environment.ProcessorCount - 1, MinWorkers, MaxWorkers);
        _cts = new CancellationTokenSource();
        _queue = Channel.CreateBounded<Job>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            }
        );
        _workers = new Task[WorkerCount];

        for (var i = 0; i < WorkerCount; i++)
        {
            _workers[i] = Task.Run(() => Drain(_cts.Token));
        }

        Enabled = true;
        logger.Information("Path search workers: {Count}", WorkerCount);
    }

    public static void Stop()
    {
        if (!Enabled)
        {
            return;
        }

        Enabled = false;
        _cts.Cancel();
        _queue.Writer.TryComplete();

        try
        {
            Task.WaitAll(_workers, TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // Shutting down. Drop leftover jobs.
        }

        _cts.Dispose();
        _cts = null;
        _queue = null;
        _workers = null;
    }

    public static bool TryEnqueue(Job job)
    {
        if (!Enabled || job?.Graph == null || job.OnComplete == null)
        {
            return false;
        }

        return _queue.Writer.TryWrite(job);
    }

    private static void Drain(CancellationToken token)
    {
        try
        {
            while (_queue.Reader.WaitToReadAsync(token).AsTask().GetAwaiter().GetResult())
            {
                while (_queue.Reader.TryRead(out var job))
                {
                    Run(job);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void Run(Job job)
    {
        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        NavSearch.Explore(
            job.Graph,
            job.Source,
            job.StopAt,
            job.GateCost,
            job.Avoid,
            job.IndoorKeep,
            cost,
            prev,
            job.Bars
        );
        Deliver(() => job.OnComplete(prev, cost, job.Epoch));
    }

    private static void Deliver(Action action)
    {
        if (Core.LoopContext != null)
        {
            Core.LoopContext.Post(action);
            return;
        }

        action();
    }

    public sealed class Job
    {
        public NavGraph Graph { get; init; }

        public string Source { get; init; }

        public string StopAt { get; init; }

        public double GateCost { get; init; } = NavSearch.DefaultGateCost;

        public IReadOnlyList<Point3D> Avoid { get; init; }

        public ISet<string> IndoorKeep { get; init; }

        /// <summary>Nodes and gates this traveler will not use, or null.</summary>
        public PathSearchBars Bars { get; init; }

        public int Epoch { get; init; }

        public Action<Dictionary<string, string>, Dictionary<string, double>, int> OnComplete { get; init; }
    }
}
