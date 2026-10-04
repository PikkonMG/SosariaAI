using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Server.Logging;

namespace SosariaAI.Deliberation;

/// <summary>
/// The off-loop part every brain provider worker shares: the HTTP client, the two bounded
/// queues, and the drain tasks that take one request at a time and hand each result to the
/// deliver callback. A subclass only answers one request (<see cref="CompleteAsync"/>). Policy
/// stays on the game loop. Every await uses ConfigureAwait(false).
/// </summary>
public abstract class ProviderWorker : IBrainWorker
{
    public const int ShutdownWaitSeconds = 2;
    public const int MaxAttempts = 2;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    protected const string RequestFailedError = "request failed";
    protected const string TimeoutError = "timeout";
    private const string CancelledError = "cancelled";

    private readonly Action<BrainResult> _deliver;
    private readonly bool _ownsHttp;
    private readonly int _parallelism;
    private readonly WorkerQueues _queues;
    private readonly ILogger _logger;
    private readonly string _drainFailedMessage;
    private CancellationTokenSource _cts;
    private Task[] _drainTasks;

    protected ProviderWorker(
        TimeSpan timeout,
        HttpClient http,
        Action<BrainResult> deliver,
        int maxConcurrentRequests,
        int queueCapacity,
        ILogger logger,
        string drainFailedMessage
    )
    {
        _deliver = deliver;
        _parallelism = Math.Max(1, maxConcurrentRequests);
        _queues = new WorkerQueues(queueCapacity);
        _logger = logger;
        _drainFailedMessage = drainFailedMessage;
        _ownsHttp = http == null;
        Http = http ?? ProviderHttp.Create(timeout);
    }

    protected HttpClient Http { get; }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _drainTasks = new Task[_parallelism];

        for (var i = 0; i < _parallelism; i++)
        {
            _drainTasks[i] = Task.Run(() => DrainLoop(_cts.Token), _cts.Token);
        }
    }

    public virtual bool TryEnqueue(BrainRequest request) => _queues.TryWrite(request);

    public void Dispose()
    {
        Stop();

        if (_ownsHttp)
        {
            Http.Dispose();
        }
    }

    /// <summary>Answers one request. Throws only on cancel or a fault the drain loop reports.</summary>
    protected abstract Task<BrainResult> CompleteAsync(BrainRequest request, CancellationToken token);

    protected static int ElapsedMs(Stopwatch clock) => (int)clock.ElapsedMilliseconds;

    protected static BrainResult Fail(BrainRequest request, int latencyMilliseconds, string error) =>
        new(
            request.RequestId,
            request.CharacterSerial,
            request.SpeakerSerial,
            request.CharacterName,
            request.Kind,
            string.Empty,
            latencyMilliseconds,
            error,
            FromParty: request.FromParty,
            Ask: request.Ask,
            ProviderName: request.ProviderName
        );

    private void Stop()
    {
        _cts?.Cancel();

        try
        {
            if (_drainTasks is { Length: > 0 })
            {
                _ = Task.WaitAll(_drainTasks, TimeSpan.FromSeconds(ShutdownWaitSeconds));
            }
        }
        catch
        {
            // Drain exited by cancel or fault. The queues are free.
        }

        _cts?.Dispose();
        _cts = null;
        _drainTasks = null;
    }

    private async Task DrainLoop(CancellationToken token)
    {
        var highStreak = 0;

        while (!token.IsCancellationRequested)
        {
            BrainRequest request = null;

            try
            {
                var (next, fromHigh) = await _queues.ReadNextAsync(highStreak, token).ConfigureAwait(false);
                request = next;
                highStreak = fromHigh ? highStreak + 1 : 0;
                var result = await CompleteAsync(request, token).ConfigureAwait(false);
                _deliver(result);
            }
            catch (OperationCanceledException)
            {
                if (request != null)
                {
                    _deliver(Fail(request, 0, CancelledError));
                }

                return;
            }
            catch (Exception e)
            {
                _logger.Warning(e, _drainFailedMessage);

                if (request != null)
                {
                    _deliver(Fail(request, 0, RequestFailedError));
                }
            }
        }
    }
}
