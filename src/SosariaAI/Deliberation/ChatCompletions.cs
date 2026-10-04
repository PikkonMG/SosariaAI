using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SosariaAI.Deliberation;

internal static class ChatCompletions
{
    internal const string CompletionsPath = "/chat/completions";
    internal const string UserAgent = "SosariaAI";
    internal const string UserAgentHeader = "User-Agent";
    internal const string BearerScheme = "Bearer";
    internal const string OpenRouterHost = "openrouter.ai";
    internal const int CharsPerToken = 4;
    internal const int JsonOverheadTokens = 24;

    // The decide shape adds "choose" and "act" with their values on top of the spoken line.
    internal const int DecideOverheadTokens = 64;
    // A plan lists a goal, a reason, and up to six steps. That is larger than one spoken line.
    internal const int PlanOverheadTokens = 256;
    internal const int PlanMinContentTokens = 256;
    internal const int EscapeHeadroomTokens = 8;
    internal const int MinMaxTokens = 32;
    internal const int ClientErrorStart = 400;
    internal const int ServerErrorStart = 500;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static Uri CompletionsUri(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return new Uri(baseUrl.TrimEnd('/') + CompletionsPath, UriKind.Absolute);
    }

    internal static int MaxTokensForReply(int maxReplyCharacters, bool decideShape, bool planShape)
    {
        var contentTokens = (maxReplyCharacters + CharsPerToken - 1) / CharsPerToken;

        if (planShape)
        {
            contentTokens = Math.Max(contentTokens, PlanMinContentTokens);
        }

        var overhead = planShape
            ? PlanOverheadTokens
            : decideShape
                ? DecideOverheadTokens
                : JsonOverheadTokens;
        return Math.Max(MinMaxTokens, contentTokens + overhead + EscapeHeadroomTokens);
    }

    /// <summary>OpenRouter takes the thinking level as a reasoning object, not reasoning_effort.</summary>
    internal static bool IsOpenRouter(Uri uri) =>
        uri != null && uri.Host.EndsWith(OpenRouterHost, StringComparison.OrdinalIgnoreCase);

    internal static HttpRequestMessage CreateRequest(
        Uri uri,
        string apiKey,
        string model,
        ChatMessage[] messages,
        double temperature,
        int maxTokens,
        string reasoningEffort
    )
    {
        var effort = string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort.Trim();
        var openRouter = IsOpenRouter(uri);
        var body = new ChatCompletionRequest
        {
            Model = model,
            Messages = messages,
            Temperature = temperature,
            MaxTokens = maxTokens,
            ReasoningEffort = openRouter ? null : effort,
            Reasoning = openRouter && effort != null ? new ReasoningSettings { Effort = effort } : null
        };

        return CreatePost(uri, body, apiKey);
    }

    /// <summary>
    /// One JSON POST to a provider: the body, the bearer key when there is one, and the
    /// plugin's user agent. Chat and System One requests both go out through here.
    /// </summary>
    internal static HttpRequestMessage CreatePost<TBody>(Uri uri, TBody body, string apiKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, apiKey);
        }

        message.Headers.TryAddWithoutValidation(UserAgentHeader, UserAgent);
        return message;
    }

    internal static async Task<(string Content, int ReasoningLength)> ReadAssistantContentAsync(HttpResponseMessage response, CancellationToken token)
    {
        var parsed = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, token)
            .ConfigureAwait(false);
        var choice = parsed?.Choices is { Length: > 0 } ? parsed.Choices[0] : null;

        if (choice?.Message == null)
        {
            return (null, 0);
        }

        return (GetContentText(choice.Message.Content), choice.Message.Reasoning?.Length ?? 0);
    }

    internal static string GetContentText(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => content.GetRawText(),
            _ => null
        };
    }

    internal sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public ChatMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        // OpenAI, Ollama, and most compatible servers read this field.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("reasoning_effort")]
        public string ReasoningEffort { get; set; }

        // OpenRouter reads its own reasoning object instead.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("reasoning")]
        public ReasoningSettings Reasoning { get; set; }
    }

    internal sealed class ReasoningSettings
    {
        [JsonPropertyName("effort")]
        public string Effort { get; set; }
    }

    internal sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[] Choices { get; set; }
    }

    internal sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatAssistantMessage Message { get; set; }
    }

    internal sealed class ChatAssistantMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public JsonElement Content { get; set; }

        // Ollama and some providers return a thinking model's reasoning here. Logged only.
        [JsonPropertyName("reasoning")]
        public string Reasoning { get; set; }
    }
}
