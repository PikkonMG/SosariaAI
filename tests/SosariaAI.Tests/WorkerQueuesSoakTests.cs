using System;
using System.Threading;
using System.Threading.Tasks;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkerQueuesSoakCollection
{
    public const string Name = "Worker queues soak";
}

/// <summary>
/// Von's worker ran 35 drain tasks on a busy high queue and a quiet normal one. Each pass of
/// every idle drain task left a wait on the quiet queue behind, about 300 bytes, and the heap
/// grew 0.8 GB in a night. The same traffic through the queues keeps nothing. The class runs
/// alone, so the heap it weighs is its own.
/// </summary>
[Collection(WorkerQueuesSoakCollection.Name)]
public class WorkerQueuesSoakTests
{
    /// <summary>Von's drain tasks on the night of the leak.</summary>
    private const int DrainTasks = 35;

    private const int Requests = 2_000;

    /// <summary>A pause after each request that lets every drain task go back to waiting, as between Von's calls.</summary>
    private const int SettleMilliseconds = 2;

    /// <summary>The old queues kept about 10 KB per request here, 20 MB in all.</summary>
    private const long LeakBudgetBytes = 4 * 1024 * 1024;

    private const int WaitSeconds = 60;

    [Fact]
    public async Task IdleDrainTasks_KeepNothing_WhileOnlyTheHighQueueIsFed()
    {
        var queues = new WorkerQueues(Requests);
        using var stop = new CancellationTokenSource();
        using var served = new SemaphoreSlim(0);
        var drains = new Task[DrainTasks];

        for (var i = 0; i < DrainTasks; i++)
        {
            drains[i] = Task.Run(() => Drain(queues, served, stop.Token));
        }

        // One request at a time, so every other drain task is idle and waiting each time.
        await Feed(queues, served, DrainTasks);
        var before = GC.GetTotalMemory(forceFullCollection: true);
        await Feed(queues, served, Requests);
        var after = GC.GetTotalMemory(forceFullCollection: true);

        stop.Cancel();
        await Task.WhenAll(drains).WaitAsync(TimeSpan.FromSeconds(WaitSeconds));

        Assert.True(after - before < LeakBudgetBytes, $"the heap grew {after - before} bytes over {Requests} requests");
    }

    private static async Task Feed(WorkerQueues queues, SemaphoreSlim served, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.True(queues.TryWrite(WorkerQueuesTests.Request(i, high: true)));
            Assert.True(await served.WaitAsync(TimeSpan.FromSeconds(WaitSeconds)));
            await Task.Delay(SettleMilliseconds);
        }
    }

    private static async Task Drain(WorkerQueues queues, SemaphoreSlim served, CancellationToken token)
    {
        var streak = 0;

        while (true)
        {
            try
            {
                var (_, high) = await queues.ReadNextAsync(streak, token);
                streak = high ? streak + 1 : 0;
                served.Release();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
