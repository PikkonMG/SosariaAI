using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SosariaAI.Deliberation;

/// <summary>
/// One typed question in a System One request. Type is "choice" or "noul". Criteria
/// takes a different shape per type: a map of option to rubric for choice,
/// {"true": ..., "false": ...} for noul, or null when the question needs no rubric.
/// </summary>
public sealed record JevQuestion(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("instructions")] object Instructions,
    [property: JsonPropertyName("criteria"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object Criteria = null
);

/// <summary>
/// One typed answer in a System One response. The matching question decides which
/// fields carry a value: Choice and Confidence for choice, Noul for noul.
/// </summary>
public sealed record SystemOneAnswer(
    string Choice,
    double Confidence,
    double? Noul
);

/// <summary>Everything one System One call returned: an answer per question plus the billed input tokens.</summary>
internal sealed record SystemOneReply(
    IReadOnlyDictionary<string, SystemOneAnswer> Answers,
    int InputTokens
);

/// <summary>
/// Wire format for the TypeSafe System One API (Jev). One endpoint answers typed
/// questions against a state; it does not generate text. Used by <see cref="JevWorker"/>.
/// </summary>
internal static class SystemOneApi
{
    private const string VersionSuffix = "/v1";
    private const string EndpointPath = "/systemone";
    private const string SystemOnePath = VersionSuffix + EndpointPath;
    internal const string ChoiceType = "choice";
    internal const string NoulType = "noul";
    private const string TypeField = "type";
    private const string ChoiceField = "choice";
    private const string NoulField = "noul";
    private const string ConfidenceField = "confidence";
    private const string ProbabilitiesField = "probabilities";
    private const int MinOptionsForSpread = 2;

    internal static Uri SystemOneUri(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        var trimmed = baseUrl.TrimEnd('/');

        // The operator may write the base with or without the /v1 suffix.
        return new Uri(
            trimmed.EndsWith(VersionSuffix, StringComparison.OrdinalIgnoreCase)
                ? trimmed + EndpointPath
                : trimmed + SystemOnePath,
            UriKind.Absolute
        );
    }

    internal static HttpRequestMessage CreateRequest(
        Uri uri,
        string apiKey,
        string model,
        object state,
        IReadOnlyDictionary<string, JevQuestion> questions
    )
    {
        var body = new SystemOneRequest
        {
            Model = model,
            State = state,
            Questions = new Dictionary<string, JevQuestion>(questions)
        };

        return ChatCompletions.CreatePost(uri, body, apiKey);
    }

    internal static async Task<SystemOneReply> ReadAnswersAsync(
        HttpResponseMessage response,
        CancellationToken token
    )
    {
        var parsed = await response.Content
            .ReadFromJsonAsync<SystemOneResponse>(ChatCompletions.JsonOptions, token)
            .ConfigureAwait(false);

        var answers = new Dictionary<string, SystemOneAnswer>(StringComparer.OrdinalIgnoreCase);

        if (parsed?.Answers != null)
        {
            foreach (var (name, element) in parsed.Answers)
            {
                var answer = ReadAnswer(element);

                if (answer != null)
                {
                    answers[name] = answer;
                }
            }
        }

        return new SystemOneReply(answers, parsed?.Usage?.InputTokens ?? 0);
    }

    private static SystemOneAnswer ReadAnswer(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object ||
            !answer.TryGetProperty(TypeField, out var type) ||
            type.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var confidence = answer.TryGetProperty(ConfidenceField, out var conf) &&
                         conf.ValueKind == JsonValueKind.Number
            ? conf.GetDouble()
            : 0;

        string choice = null;
        double? noul = null;

        switch (type.GetString())
        {
            case ChoiceType
                when answer.TryGetProperty(ChoiceField, out var c) && c.ValueKind == JsonValueKind.String:
                choice = c.GetString();
                confidence = PickConfidence(answer, choice) ?? confidence;
                break;
            case NoulType
                when answer.TryGetProperty(NoulField, out var n) && n.ValueKind == JsonValueKind.Number:
                noul = n.GetDouble();
                break;
            default:
                return null;
        }

        return new SystemOneAnswer(choice, confidence, noul);
    }

    /// <summary>
    /// Jev's choice confidence: the pick's probability rescaled so an even spread over the
    /// options is 0 and a certain pick is 1. Von sends the gap between the top two
    /// probabilities and Laya sends 1 minus the normalised entropy, so the same floor would
    /// mean three different things. Every provider sends the probabilities, so the plugin
    /// computes this one measure itself and one floor fits all three.
    /// </summary>
    public static double ChoiceConfidence(double pickProbability, int optionCount) =>
        Math.Clamp((optionCount * pickProbability - 1) / (optionCount - 1), 0, 1);

    // Null when the answer carries no usable spread; the provider's own confidence then stands.
    private static double? PickConfidence(JsonElement answer, string choice)
    {
        if (!answer.TryGetProperty(ProbabilitiesField, out var spread) || spread.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? pick = null;
        var count = 0;

        foreach (var option in spread.EnumerateObject())
        {
            if (option.Value.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            count++;

            if (option.NameEquals(choice))
            {
                pick = option.Value.GetDouble();
            }
        }

        return pick is { } p && count >= MinOptionsForSpread ? ChoiceConfidence(p, count) : null;
    }

    internal sealed class SystemOneRequest
    {
        [JsonPropertyName("state")]
        public object State { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("questions")]
        public Dictionary<string, JevQuestion> Questions { get; set; }
    }

    internal sealed class SystemOneResponse
    {
        [JsonPropertyName("answers")]
        public Dictionary<string, JsonElement> Answers { get; set; }

        [JsonPropertyName("usage")]
        public SystemOneUsage Usage { get; set; }
    }

    internal sealed class SystemOneUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }
    }
}
