using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SosariaAI.Deliberation;

/// <summary>
/// The two bounded queues one provider's drain tasks share: high (player chat and fight
/// stances) and normal. One count of queued requests wakes one drain task per request, and a
/// waiting drain task holds nothing but its place in that count. Two channels waited on
/// together left the quiet channel's wait behind on every pass: Von's stance calls kept about
/// 300 bytes per idle drain task per call, 0.8 GB over one night.
/// </summary>
public sealed class WorkerQueues
{
    private readonly Lock _gate = new();
    private readonly Queue<BrainRequest> _high = new();
    private readonly Queue<BrainRequest> _normal = new();
    private readonly SemaphoreSlim _queued = new(0);

    public WorkerQueues(int capacity) => Capacity = Math.Max(1, capacity);

    /// <summary>The most requests each queue holds; a request past it is dropped, not waited for.</summary>
    public int Capacity { get; }

    /// <summary>Queues the request by its priority. False when that queue is full or the request is null.</summary>
    public bool TryWrite(BrainRequest request)
    {
        if (request == null)
        {
            return false;
        }

        lock (_gate)
        {
            var queue = request.HighPriority ? _high : _normal;

            if (queue.Count >= Capacity)
            {
                return false;
            }

            queue.Enqueue(request);
        }

        _queued.Release();
        return true;
    }

    /// <summary>
    /// Waits for the next request in <see cref="WorkerQueuePolicy"/> order, and says whether it
    /// came from the high queue. A cancelled token ends the wait with
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<(BrainRequest Request, bool High)> ReadNextAsync(int highStreak, CancellationToken token)
    {
        await _queued.WaitAsync(token).ConfigureAwait(false);

        lock (_gate)
        {
            // Each count stands for one queued request and only a counted reader takes one,
            // so a request waits in one of the queues here.
            return WorkerQueuePolicy.Next(_high.Count > 0, _normal.Count > 0, highStreak) == WorkerQueuePolicy.Pick.High
                ? (_high.Dequeue(), true)
                : (_normal.Dequeue(), false);
        }
    }
}
