using System;
using System.Collections.Generic;
using System.Text.Json;
using SosariaAI.Behaviour;

namespace SosariaAI.Deliberation;

public static class ReplyParser
{
    private const string SayProperty = "say";
    private const string ChooseProperty = "choose";
    private const string ActProperty = "act";
    private const string GoalProperty = "goal";
    private const string ReasonProperty = "reason";
    private const string SuccessProperty = "success";
    private const string StepsProperty = "steps";
    private const string DoProperty = "do";
    private const string WhyProperty = "why";
    private const string RefProperty = "ref";
    private const string Fence = "```";
    private const char ObjectStart = '{';
    private const char ObjectEnd = '}';
    private const char Space = ' ';

    /// <summary>
    /// True when the model returned well-formed JSON and simply chose to say nothing.
    /// That is a valid answer, not a parse failure, and must not be logged as one.
    /// </summary>
    public static bool IsDeliberateSilence(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(SayProperty, out var say) &&
                   say.ValueKind == JsonValueKind.String &&
                   string.IsNullOrWhiteSpace(say.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The spoken line, the chosen routine, and the immediate act of a chat reply. Any other
    /// key, such as the "mood" older prompts asked for, is ignored.
    /// </summary>
    public static (string Say, string Choose, string Act) ParseDecide(string raw, int maxReplyCharacters)
    {
        var json = ExtractJson(raw);

        if (json == null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var say = ReadString(root, SayProperty);
            var choose = ReadString(root, ChooseProperty).Trim();
            var act = ImmediateActs.Normalize(ReadString(root, ActProperty)) ?? string.Empty;
            return (TrimToWordBoundary(say, maxReplyCharacters), choose, act);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    public static PlanProposal ParsePlan(string raw, string characterId, int expectedRevision, long requestId)
    {
        var json = ExtractJson(raw);

        if (json == null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var steps = ReadSteps(root);

            if (steps.Count == 0)
            {
                var choose = ReadString(root, ChooseProperty).Trim();

                if (!string.IsNullOrWhiteSpace(choose))
                {
                    steps.Add(new ModelPlanStep(choose, "chosen work", string.Empty));
                }
            }

            if (steps.Count == 0)
            {
                return null;
            }

            return new PlanProposal(
                characterId,
                expectedRevision,
                ModelPlan.ClampText(ReadString(root, GoalProperty)),
                ModelPlan.ClampText(ReadString(root, ReasonProperty)),
                ModelPlan.ClampText(ReadString(root, SuccessProperty)),
                steps,
                requestId
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<ModelPlanStep> ReadSteps(JsonElement root)
    {
        var steps = new List<ModelPlanStep>();

        if (!root.TryGetProperty(StepsProperty, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return steps;
        }

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var skill = ReadString(element, DoProperty).Trim();

            if (string.IsNullOrWhiteSpace(skill))
            {
                continue;
            }

            steps.Add(new ModelPlanStep(
                skill,
                ModelPlan.ClampText(ReadString(element, WhyProperty)),
                PlanRefs.Normalize(ReadString(element, RefProperty))
            ));

            if (steps.Count >= ModelPlan.MaxSteps)
            {
                break;
            }
        }

        return steps;
    }

    internal static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = StripFence(raw.Trim());
        var start = text.IndexOf(ObjectStart);
        var end = text.LastIndexOf(ObjectEnd);

        if (start < 0 || end <= start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }

    internal static string TrimToWordBoundary(string say, int maxReplyCharacters)
    {
        if (string.IsNullOrEmpty(say))
        {
            return string.Empty;
        }

        var trimmed = say.Trim();

        if (maxReplyCharacters <= 0 || trimmed.Length <= maxReplyCharacters)
        {
            return trimmed;
        }

        var slice = trimmed[..maxReplyCharacters];
        var lastSpace = slice.LastIndexOf(Space);

        if (lastSpace > 0)
        {
            slice = slice[..lastSpace];
        }

        return slice.Trim();
    }

    private static string StripFence(string text)
    {
        if (!text.StartsWith(Fence, StringComparison.Ordinal))
        {
            return text;
        }

        var firstBreak = text.IndexOf('\n');

        if (firstBreak < 0)
        {
            return text;
        }

        var inner = text[(firstBreak + 1)..];
        var close = inner.LastIndexOf(Fence, StringComparison.Ordinal);

        if (close >= 0)
        {
            inner = inner[..close];
        }

        return inner.Trim();
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => property.ToString()
        };
    }
}
