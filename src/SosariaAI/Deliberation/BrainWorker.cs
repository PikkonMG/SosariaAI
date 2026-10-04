using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Deliberation;

/// <summary>
/// Off-loop worker for a chat provider: one OpenAI-style chat completion per request. Each
/// queue is bounded at <see cref="QueueCapacity"/>.
/// </summary>
public sealed class BrainWorker : ProviderWorker
{
    public const int QueueCapacity = 64;
    public const int RawReplyLogLength = 200;

    private const int HttpBadRequest = 400;
    private const int HttpUnprocessable = 422;
    private const int ThinkingOffSent = 0;
    private const int ThinkingOffRefused = 1;

    private static readonly ILogger logger = SosariaLog.For(typeof(BrainWorker));

    private readonly Uri _uri;
    private readonly string _apiKey;
    private readonly bool _thinking;
    private readonly string _reasoningEffort;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxReplyCharacters;

    // Several drain tasks share this. Only Interlocked flips it, so exactly one thread writes
    // the warning when the endpoint refuses the thinking-off switch.
    private int _thinkingOffState = ThinkingOffSent;

    public BrainWorker(
        string baseUrl,
        string apiKey,
        string model,
        double temperature,
        int maxReplyCharacters,
        TimeSpan timeout,
        Action<BrainResult> deliver,
        int maxConcurrentRequests = 1,
        bool thinking = false,
        string reasoningEffort = null,
        HttpClient http = null
    ) : base(timeout, http, deliver, maxConcurrentRequests, QueueCapacity, logger, "Brain worker failed a request")
    {
        _uri = ChatCompletions.CompletionsUri(baseUrl);
        _apiKey = apiKey;
        _thinking = thinking;
        _reasoningEffort = string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort.Trim();
        _model = model;
        _temperature = temperature;
        _maxReplyCharacters = maxReplyCharacters;
    }

    protected override async Task<BrainResult> CompleteAsync(BrainRequest request, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        var personaWrite = PersonaPrompt.IsPersonaWrite(request.Kind);
        var planShape = PromptBuilder.UsesPlanShape(request.Kind);
        var maxTokens = personaWrite
            ? PersonaPrompt.MaxReplyTokens
            : ChatCompletions.MaxTokensForReply(
                _maxReplyCharacters,
                PromptBuilder.UsesDecideShape(request.Kind),
                planShape
            );
        Exception lastError = null;
        var retriedPlain = false;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var effort = retriedPlain ? null : EffortToSend();

            try
            {
                using var message = ChatCompletions.CreateRequest(
                    _uri,
                    _apiKey,
                    _model,
                    [new ChatMessage { Role = ChatMessage.SystemRole, Content = request.SystemMessage },
                        new ChatMessage { Role = ChatMessage.UserRole, Content = request.UserMessage }],
                    _temperature,
                    maxTokens,
                    effort
                );

                using var response = await Http.SendAsync(message, token).ConfigureAwait(false);
                var code = (int)response.StatusCode;

                if (code >= ChatCompletions.ServerErrorStart)
                {
                    if (attempt == 0)
                    {
                        await Task.Delay(RetryDelay, token).ConfigureAwait(false);
                        continue;
                    }

                    return Fail(request, ElapsedMs(clock), $"HTTP {code}");
                }

                if (code >= ChatCompletions.ClientErrorStart)
                {
                    if (attempt == 0 && MayRetryPlain(effort, code))
                    {
                        retriedPlain = true;
                        continue;
                    }

                    return Fail(request, ElapsedMs(clock), $"HTTP {code}");
                }

                if (retriedPlain)
                {
                    // The bad request went away without the field: the endpoint refuses it.
                    NoteThinkingOffRefused();
                }

                var (content, reasoningLength) = await ChatCompletions.ReadAssistantContentAsync(response, token).ConfigureAwait(false);
                var (say, choose, act) = ReplyParser.ParseDecide(content, _maxReplyCharacters);
                // A persona is its own JSON shape: any text is handed back for the writer to check.
                // The planning path parses a plan itself; here a plan is read only when nothing
                // else came back, so a reply with no usable part is named in the log.
                var nothingUsable = personaWrite
                    ? string.IsNullOrWhiteSpace(content)
                    : string.IsNullOrEmpty(say) && string.IsNullOrEmpty(choose) && string.IsNullOrEmpty(act) &&
                      !(planShape && HasPlanSteps(content, request));
                if (nothingUsable && SosariaSettings.LogActivity)
                {
                    LogEmptyReply(request, content, reasoningLength, effort);
                }

                return new BrainResult(
                    request.RequestId,
                    request.CharacterSerial,
                    request.SpeakerSerial,
                    request.CharacterName,
                    request.Kind,
                    say,
                    ElapsedMs(clock),
                    null,
                    choose,
                    act,
                    request.FromParty,
                    request.CharacterId,
                    request.PlanRevision,
                    content,
                    ProviderName: request.ProviderName
                );
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastError = null;

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
                lastError = e;
                break;
            }
        }

        if (lastError != null)
        {
            logger.Warning(lastError, "Brain request failed");
        }

        return Fail(request, ElapsedMs(clock), RequestFailedError);
    }

    private static bool HasPlanSteps(string content, BrainRequest request) =>
        ReplyParser.ParsePlan(content, request.CharacterId, request.PlanRevision, request.RequestId) is { Steps.Count: > 0 };

    // "thinking": false (the default) asks the model not to think, unless the endpoint refused
    // that once. "thinking": true sends the operator's reasoningEffort level, or nothing.
    private string EffortToSend() =>
        _thinking ? _reasoningEffort :
        Volatile.Read(ref _thinkingOffState) == ThinkingOffRefused ? null : BrainProviders.ReasoningOff;

    /// <summary>
    /// Only a bad-request answer can mean the endpoint refused the thinking-off field. A rate
    /// limit, an auth error, or a missing model says nothing about it and must not turn it off.
    /// </summary>
    public static bool MayBeFieldRefusal(int code) => code is HttpBadRequest or HttpUnprocessable;

    // A bad request with the thinking-off field is tried once more without it. Only a plain
    // retry that then succeeds proves the field was the problem; a prompt that is too long,
    // or any other bad request, fails both times and keeps the field.
    private bool MayRetryPlain(string effortSent, int code) =>
        !_thinking && effortSent == BrainProviders.ReasoningOff && MayBeFieldRefusal(code);

    // An endpoint that refuses the thinking-off switch gets plain requests from then on.
    private void NoteThinkingOffRefused()
    {
        if (Interlocked.Exchange(ref _thinkingOffState, ThinkingOffRefused) == ThinkingOffRefused)
        {
            return;
        }

        logger.Warning(
            "{Model} refused the thinking-off switch; it now gets plain requests. Pick a model that can answer without thinking",
            _model
        );
    }

    // An empty answer is the one failure a player sees, so say why. A thinking model that
    // spent its budget on reasoning shows up here as empty content with reasoning present.
    private static void LogEmptyReply(BrainRequest request, string content, int reasoningLength, string effortSent)
    {
        if (string.IsNullOrEmpty(content) && reasoningLength > 0)
        {
            if (effortSent == BrainProviders.ReasoningOff)
            {
                logger.Information(
                    "{Name} got no answer: the model returned {ReasoningLength} characters of reasoning and no text even with thinking off. Pick a model that does not think.",
                    request.CharacterName,
                    reasoningLength
                );
                return;
            }

            logger.Information(
                "{Name} got no answer: the model returned {ReasoningLength} characters of reasoning and no text. Set \"thinking\": false on that provider.",
                request.CharacterName,
                reasoningLength
            );
            return;
        }

        var raw = content ?? string.Empty;

        if (ReplyParser.IsDeliberateSilence(raw))
        {
            logger.Information("{Name} had nothing to say", request.CharacterName);
            return;
        }

        var shown = raw.Length <= RawReplyLogLength ? raw : raw[..RawReplyLogLength];
        logger.Information("{Name} reply could not be parsed. Raw: {Raw}", request.CharacterName, shown);
    }
}
