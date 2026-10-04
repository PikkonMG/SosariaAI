using System;
using System.Threading;
using System.Threading.Tasks;
using Server;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class WorkerQueuesTests
{
    private const int Capacity = 2;
    private const int WaitSeconds = 5;

    [Fact]
    public void TryWrite_DropsTheNewestWhenItsQueueIsFull()
    {
        var queues = new WorkerQueues(Capacity);

        Assert.True(queues.TryWrite(Request(1, high: true)));
        Assert.True(queues.TryWrite(Request(2, high: true)));
        Assert.False(queues.TryWrite(Request(3, high: true)));
        Assert.True(queues.TryWrite(Request(4, high: false)));
        Assert.False(queues.TryWrite(null));
    }

    [Fact]
    public async Task ReadNextAsync_ServesHighFirst_ThenNormalAfterTheStreakLimit()
    {
        var queues = new WorkerQueues(WorkerQueuePolicy.MaxHighBeforeNormal + 1);
        Assert.True(queues.TryWrite(Request(100, high: false)));

        for (var i = 1; i <= WorkerQueuePolicy.MaxHighBeforeNormal + 1; i++)
        {
            Assert.True(queues.TryWrite(Request(i, high: true)));
        }

        var streak = 0;

        for (var i = 1; i <= WorkerQueuePolicy.MaxHighBeforeNormal; i++)
        {
            var (request, high) = await queues.ReadNextAsync(streak, CancellationToken.None);
            Assert.True(high);
            Assert.Equal(i, request.RequestId);
            streak++;
        }

        var (normal, fromHigh) = await queues.ReadNextAsync(streak, CancellationToken.None);
        Assert.False(fromHigh);
        Assert.Equal(100, normal.RequestId);
    }

    [Fact]
    public async Task ReadNextAsync_WaitsForAWrite()
    {
        var queues = new WorkerQueues(Capacity);
        var read = queues.ReadNextAsync(0, CancellationToken.None);

        Assert.False(read.IsCompleted);
        Assert.True(queues.TryWrite(Request(7, high: false)));

        var (request, high) = await read.WaitAsync(TimeSpan.FromSeconds(WaitSeconds));
        Assert.Equal(7, request.RequestId);
        Assert.False(high);
    }

    [Fact]
    public async Task ReadNextAsync_EndsWithCancel_WhenTheWorkerStops()
    {
        var queues = new WorkerQueues(Capacity);
        using var stop = new CancellationTokenSource();
        var read = queues.ReadNextAsync(0, stop.Token);

        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(WaitSeconds)));
    }

    internal static BrainRequest Request(long id, bool high) =>
        new(id, (Serial)1u, Serial.Zero, "Connor", BrainEventKind.Spoken, "system", "user", HighPriority: high);
}
