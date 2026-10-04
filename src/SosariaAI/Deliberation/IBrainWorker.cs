using System;

namespace SosariaAI.Deliberation;

/// <summary>
/// Off-loop HTTP worker behind a brain provider. Chat providers use
/// <see cref="BrainWorker"/>; System One providers use <see cref="JevWorker"/>. Dispose stops
/// the drain tasks.
/// </summary>
public interface IBrainWorker : IDisposable
{
    void Start();

    bool TryEnqueue(BrainRequest request);
}
