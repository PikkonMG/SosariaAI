using System;
using System.Collections.Generic;
using System.Threading;
using Server.Logging;

namespace SosariaAI.Memory;

/// <summary>
/// The <see cref="MemoryStore"/> changes waiting for <c>memory.db</c>. Any thread adds without
/// waiting for the disk. The writer thread takes the whole queue, and on a failed write puts
/// the batch back in front, waits <c>retryMs</c> before the next timed try, and warns once per
/// run of failures. While the disk refuses writes the queue keeps only its newest
/// <c>maxPending</c> entries.
/// </summary>
internal sealed class PendingWrites
{
    private readonly Lock _gate = new();
    private readonly int _maxPending;
    private readonly int _retryMs;
    private readonly ILogger _logger;
    private Queue<MemoryWrite> _queue = new();
    private bool _failing;
    private long _nextTryAt;

    /// <param name="maxPending">The most writes kept; older ones are dropped.</param>
    /// <param name="retryMs">After a failed write, timed tries wait this long.</param>
    /// <param name="logger">Gets the one warning per run of failures.</param>
    public PendingWrites(int maxPending, int retryMs, ILogger logger)
    {
        _maxPending = maxPending;
        _retryMs = retryMs;
        _logger = logger;
    }

    /// <summary>False while a failed write still waits out its retry time.</summary>
    public bool RetryDue => Environment.TickCount64 >= Interlocked.Read(ref _nextTryAt);

    public void Add(MemoryWrite item)
    {
        lock (_gate)
        {
            _queue.Enqueue(item);
            TrimOldest();
        }
    }

    /// <summary>Takes every waiting write, oldest first.</summary>
    public List<MemoryWrite> TakeAll()
    {
        lock (_gate)
        {
            var batch = new List<MemoryWrite>(_queue);
            _queue.Clear();
            return batch;
        }
    }

    /// <summary>
    /// A write failed: the batch goes back in front of newer writes, timed tries wait the
    /// retry time, and the first failure of a run is logged with the target and the reason.
    /// </summary>
    public void NoteFailure(List<MemoryWrite> batch, string target, Exception error)
    {
        lock (_gate)
        {
            var merged = new Queue<MemoryWrite>(batch.Count + _queue.Count);

            for (var i = 0; i < batch.Count; i++)
            {
                merged.Enqueue(batch[i]);
            }

            while (_queue.TryDequeue(out var newer))
            {
                merged.Enqueue(newer);
            }

            _queue = merged;
            TrimOldest();

            Interlocked.Exchange(ref _nextTryAt, Environment.TickCount64 + _retryMs);

            if (_failing)
            {
                return;
            }

            _failing = true;
        }

        _logger.Warning(
            "Could not write {Path} ({Reason}); the memory store keeps its newest writes and tries again",
            target,
            error.Message
        );
    }

    /// <summary>A write went through: the next failure warns again.</summary>
    public void NoteSuccess()
    {
        lock (_gate)
        {
            _failing = false;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _queue.Clear();
        }
    }

    // A store that keeps refusing writes must not hold every write of the night.
    private void TrimOldest()
    {
        while (_queue.Count > _maxPending)
        {
            _queue.Dequeue();
        }
    }
}
