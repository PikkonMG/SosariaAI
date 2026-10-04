using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class WorkerQueuePolicyTests
{
    [Fact]
    public void Next_PrefersHighUntilTheStarveLimit()
    {
        Assert.Equal(WorkerQueuePolicy.Pick.High, WorkerQueuePolicy.Next(true, true, 0));
        Assert.Equal(
            WorkerQueuePolicy.Pick.High,
            WorkerQueuePolicy.Next(true, true, WorkerQueuePolicy.MaxHighBeforeNormal - 1)
        );
        Assert.Equal(
            WorkerQueuePolicy.Pick.Normal,
            WorkerQueuePolicy.Next(true, true, WorkerQueuePolicy.MaxHighBeforeNormal)
        );
    }

    [Fact]
    public void Next_TakesHighWhenNormalIsEmpty()
    {
        Assert.Equal(
            WorkerQueuePolicy.Pick.High,
            WorkerQueuePolicy.Next(true, false, WorkerQueuePolicy.MaxHighBeforeNormal)
        );
        Assert.Equal(WorkerQueuePolicy.Pick.Normal, WorkerQueuePolicy.Next(false, true, 0));
        Assert.Equal(WorkerQueuePolicy.Pick.None, WorkerQueuePolicy.Next(false, false, 0));
    }
}
