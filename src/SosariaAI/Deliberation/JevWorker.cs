using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Deliberation;

/// <summary>
/// Off-loop HTTP worker for a System One provider (Jev, Von, or Laya). Each queue holds
/// <see cref="JevDecisionRules.QueueLimit"/> of its parallel sends, so a burst waits a
/// moment instead of being refused. Policy stays on the game loop. Every
/// await uses ConfigureAwait(false). Results go back only through the deliver
/// callback. Jev answers typed questions, so a decision comes back as a routine
/// choice instead of text to parse. Every Jev call shares this one transport, so the
/// rate limiter paces all of them and the usage meter counts all of them, by kind.
/// </summary>
public sealed class JevWorker : ProviderWorker
{
    public const int TooManyRequests = 429;
    public const int Overloaded = 529;

    private static readonly ILogger logger = SosariaLog.For(typeof(JevWorker));

    private readonly Uri _uri;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly double _minConfidence;
    private readonly JevRateLimiter _limiter;
    private readonly JevUsage _usage;

    public JevWorker(
        string baseUrl,
        string apiKey,
        string model,
        TimeSpan timeout,
        Action<BrainResult> deliver,
        int maxConcurrentRequests = 1,
        double minConfidence = 0,
        HttpClient http = null,
        JevRateLimiter limiter = null,
        JevUsage usage = null
    ) : base(
        timeout,
        http,
        deliver,
        maxConcurrentRequests,
        JevDecisionRules.QueueLimit(maxConcurrentRequests),
        logger,
        "System One worker failed a request"
    )
    {
        _uri = SystemOneApi.SystemOneUri(baseUrl);
        _apiKey = apiKey;
        _model = model;
        _minConfidence = minConfidence;
        _limiter = limiter ?? new JevRateLimiter();
        _usage = usage;
    }

    public override bool TryEnqueue(BrainRequest request) => request?.Jev != null && base.TryEnqueue(request);

    protected override async Task<BrainResult> CompleteAsync(BrainRequest request, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                await PaceAsync(token).ConfigureAwait(false);

                using var message = SystemOneApi.CreateRequest(
                    _uri,
                    _apiKey,
                    _model,
                    request.Jev.State,
                    request.Jev.Questions
                );

                _usage?.RecordRequest(DateTime.UtcNow, request.JevKind);
                using var response = await Http.SendAsync(message, token).ConfigureAwait(false);
                var code = (int)response.StatusCode;

                // The next attempt waits out the shared hold in PaceAsync.
                if (code is TooManyRequests or Overloaded)
                {
                    _limiter.Throttle(DateTime.UtcNow);

                    if (attempt + 1 < MaxAttempts)
                    {
                        continue;
                    }

                    return Fail(request, ElapsedMs(clock), $"HTTP {code}");
                }

                if (code >= ChatCompletions.ServerErrorStart)
                {
                    if (attempt == 0)
                    {
                        await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                        continue;
                    }

                    return Fail(request, ElapsedMs(clock), $"HTTP {code}{await Detail(response, token).ConfigureAwait(false)}");
                }

                if (code >= ChatCompletions.ClientErrorStart)
                {
                    return Fail(request, ElapsedMs(clock), $"HTTP {code}{await Detail(response, token).ConfigureAwait(false)}");
                }

                var reply = await SystemOneApi.ReadAnswersAsync(response, token).ConfigureAwait(false);
                _limiter.Succeeded();
                _usage?.RecordTokens(DateTime.UtcNow, request.JevKind, reply.InputTokens);

                reply.Answers.TryGetValue(JevPrompt.NextQuestion, out var next);
                reply.Answers.TryGetValue(JevPrompt.ActQuestion, out var act);
                reply.Answers.TryGetValue(JevPrompt.ReplyQuestion, out var gate);
                reply.Answers.TryGetValue(JevPrompt.IntentQuestion, out var intent);

                // A gate call exists only for its noul, an intent call only for its intent,
                // a decide call only for its routine choice. Any missing is a malformed reply.
                // A caller's own questions come back as answered; the caller reads them.
                var missing = request.Ask switch
                {
                    JevAsk.Gate when gate?.Noul == null => "no gate answer in reply",
                    JevAsk.Intent when string.IsNullOrWhiteSpace(intent?.Choice) => "no intent in reply",
                    JevAsk.Plain when next == null || string.IsNullOrWhiteSpace(next.Choice) => "no choice in reply",
                    _ => null
                };

                if (missing != null)
                {
                    return Fail(request, ElapsedMs(clock), missing);
                }

                // Below the operator's floor the needs brain picks instead: a null
                // Choose sends ApplyDecide down the same fallback as a rejected one.
                // The act fan-out follows the same floor; a shaky act is no act. A shaky
                // intent is no intent, and the line gets an ordinary reply.
                var picked = request.Ask == JevAsk.Intent ? intent : next;
                var choose = picked != null && picked.Confidence >= _minConfidence ? picked.Choice : null;
                var actName = act != null && act.Confidence >= _minConfidence ? act.Choice : null;

                return new BrainResult(
                    request.RequestId,
                    request.CharacterSerial,
                    request.SpeakerSerial,
                    request.CharacterName,
                    request.Kind,
                    Say: null,
                    ElapsedMs(clock),
                    null,
                    Choose: choose,
                    Act: actName,
                    request.FromParty,
                    request.CharacterId,
                    request.PlanRevision,
                    RawOf(request, next, act, gate, intent),
                    reply.InputTokens,
                    request.Ask,
                    gate?.Noul ?? 0,
                    request.Ask == JevAsk.Answers ? reply.Answers : null,
                    request.ProviderName
                );
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                if (attempt == 0)
                {
                    await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                    continue;
                }

                return Fail(request, ElapsedMs(clock), TimeoutError);
            }
            catch (HttpRequestException e)
            {
                var reason = ProviderHttp.Reason(e);

                if (attempt == 0)
                {
                    logger.Information("{Provider} request failed ({Reason}); trying once more", request.ProviderName, reason);
                    await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                    continue;
                }

                return Fail(request, ElapsedMs(clock), $"request failed ({reason})");
            }
            catch (Exception e)
            {
                logger.Warning(e, "{Provider} request failed", request.ProviderName);
                return Fail(request, ElapsedMs(clock), RequestFailedError);
            }
        }

        return Fail(request, ElapsedMs(clock), RequestFailedError);
    }

    /// <summary>Waits for a send slot under the per-minute limit and any throttle hold.</summary>
    private async Task PaceAsync(CancellationToken token)
    {
        while (!_limiter.TryAcquire(DateTime.UtcNow, out var wait))
        {
            await Task.Delay(wait, token).ConfigureAwait(false);
        }
    }

    /// <summary>The body's first line on a refused call: providers name the rejected field there.</summary>
    private static async Task<string> Detail(HttpResponseMessage response, CancellationToken token)
    {
        var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var flat = body.Replace('\n', ' ').Replace('\r', ' ').Trim();
        const int detailMaxChars = 240;
        return $" ({(flat.Length <= detailMaxChars ? flat : flat[..detailMaxChars])})";
    }

    private static string RawOf(
        BrainRequest request,
        SystemOneAnswer next,
        SystemOneAnswer act,
        SystemOneAnswer gate,
        SystemOneAnswer intent
    )
    {
        switch (request.Ask)
        {
            case JevAsk.Gate:
                return gate?.Noul == null ? "gate missing" : $"gate {gate.Noul:0.00}";
            case JevAsk.Intent:
                return intent == null ? "intent missing" : $"intent {intent.Choice} confidence {intent.Confidence:0.00}";
            case JevAsk.Answers:
                return "answers";
        }

        var raw = next == null ? "choice missing" : $"choice {next.Choice} confidence {next.Confidence:0.00}";

        return act == null ? raw : $"{raw} act {act.Choice} confidence {act.Confidence:0.00}";
    }
}
