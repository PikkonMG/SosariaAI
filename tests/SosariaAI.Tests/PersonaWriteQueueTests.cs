using Server;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaWriteQueueTests
{
    private const string First = "Felucca:connor#1";
    private const string Second = "Felucca:connor#2";
    private static readonly Serial FirstSerial = (Serial)11u;
    private static readonly Serial SecondSerial = (Serial)12u;

    [Fact]
    public void Add_QueuesEachCopyOnce_EvenAfterItIsTaken()
    {
        var queue = new PersonaWriteQueue();

        Assert.True(queue.Add(First, FirstSerial));
        Assert.False(queue.Add(First, FirstSerial));
        Assert.True(queue.TryTake(out var id, out var serial));
        Assert.Equal(First, id);
        Assert.Equal(FirstSerial, serial);
        Assert.False(queue.Add(First, FirstSerial));
        Assert.False(queue.Add("  ", FirstSerial));
        Assert.Equal(0, queue.Waiting);
    }

    [Fact]
    public void TryTake_OldestFirst_AndPutBackGoesToTheEnd()
    {
        var queue = new PersonaWriteQueue();
        queue.Add(First, FirstSerial);
        queue.Add(Second, SecondSerial);

        Assert.True(queue.TryTake(out var id, out var serial));
        Assert.Equal(First, id);
        queue.PutBack(id, serial);

        Assert.True(queue.TryTake(out id, out _));
        Assert.Equal(Second, id);
        Assert.True(queue.TryTake(out id, out _));
        Assert.Equal(First, id);
        Assert.False(queue.TryTake(out id, out _));
        Assert.Null(id);
    }
}
