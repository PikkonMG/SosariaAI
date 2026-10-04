using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SosariaAI.Behaviour;

public enum PlanState
{
    Waiting,
    Running,
    Blocked,
    Complete,
    Failed
}

public enum PlanTrigger
{
    None,
    NoPlan,
    GoalComplete,
    StepFailed,
    Died,
    Invitation,
    WorldBlocked,
    PlayerRequest,
    Retry
}

public enum StepResultKind
{
    GoalCompleted,
    StepCompleted,
    AttemptFailed,
    ActionUnavailable,
    Interrupted,
    NoUsefulProgress
}

public readonly record struct ModelPlanStep(string SkillKind, string Why, string TargetRef);

public readonly record struct PlanResultRecord(string SkillKind, StepResultKind Kind, string Detail);

/// <summary>
/// One model-authored job. Game code carries out each step. Scoring does not own it.
/// </summary>
public sealed class ModelPlan
{
    public const int MaxSteps = 6;
    public const int MaxText = 80;
    public const int MaxResults = 8;
    public const int CodecVersion = 1;
    public static readonly TimeSpan DefaultLife = TimeSpan.FromHours(4);

    /// <summary>A retry count an older save wrote. Nothing read it; a load skips the line.</summary>
    private const string OldRetriesKey = "retries";

    /// <summary>A creation time an older save wrote. Nothing read it; a load skips the line.</summary>
    private const string OldCreatedKey = "created";

    public string Id { get; init; }

    public int Revision { get; init; }

    public string CharacterId { get; init; }

    public string Goal { get; init; }

    public string Reason { get; init; }

    public string Success { get; init; }

    public IReadOnlyList<ModelPlanStep> Steps { get; init; } = [];

    public int Index { get; init; }

    public PlanState State { get; init; }

    public string FailureReason { get; init; }

    public int StepFailures { get; init; }

    public DateTime Expires { get; init; }

    public IReadOnlyList<PlanResultRecord> Results { get; init; } = [];

    public PlanTrigger Trigger { get; init; }

    public bool OwnsWork =>
        State is PlanState.Waiting or PlanState.Running &&
        Steps is { Count: > 0 } &&
        Index >= 0 &&
        Index < Steps.Count;

    public ModelPlanStep? Current => OwnsWork ? Steps[Index] : null;

    public string CurrentSkill => Current?.SkillKind;

    public string CurrentWhy => Current?.Why;

    public static string NewId(string characterId, DateTime created)
    {
        var who = string.IsNullOrWhiteSpace(characterId) ? "unknown" : characterId.Trim();
        return who + ":" + created.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
    }

    public static string ClampText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxText ? trimmed : trimmed[..MaxText].Trim();
    }

    public List<string> ToLines()
    {
        var lines = new List<string>
        {
            "v" + CodecVersion.ToString(CultureInfo.InvariantCulture),
            "id=" + Escape(Id),
            "rev=" + Revision.ToString(CultureInfo.InvariantCulture),
            "who=" + Escape(CharacterId),
            "goal=" + Escape(Goal),
            "reason=" + Escape(Reason),
            "success=" + Escape(Success),
            "state=" + State,
            "index=" + Index.ToString(CultureInfo.InvariantCulture),
            "fail=" + Escape(FailureReason),
            "stepFail=" + StepFailures.ToString(CultureInfo.InvariantCulture),
            "expires=" + Expires.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            "trigger=" + Trigger
        };

        var steps = Steps ?? [];

        for (var i = 0; i < steps.Count; i++)
        {
            lines.Add(
                "step=" +
                Escape(steps[i].SkillKind) + "|" +
                Escape(steps[i].Why) + "|" +
                Escape(steps[i].TargetRef)
            );
        }

        var results = Results ?? [];

        for (var i = 0; i < results.Count; i++)
        {
            lines.Add(
                "result=" +
                Escape(results[i].SkillKind) + "|" +
                results[i].Kind + "|" +
                Escape(results[i].Detail)
            );
        }

        return lines;
    }

    public static ModelPlan FromLines(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            return null;
        }

        if (!string.Equals(lines[0], "v" + CodecVersion, StringComparison.Ordinal))
        {
            return null;
        }

        var id = string.Empty;
        var revision = 0;
        var who = string.Empty;
        var goal = string.Empty;
        var reason = string.Empty;
        var success = string.Empty;
        var state = PlanState.Failed;
        var index = 0;
        var fail = string.Empty;
        var stepFail = 0;
        var expires = default(DateTime);
        var trigger = PlanTrigger.None;
        var steps = new List<ModelPlanStep>();
        var results = new List<PlanResultRecord>();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var split = line.IndexOf('=');

            if (split <= 0)
            {
                return null;
            }

            var key = line[..split];
            var value = line[(split + 1)..];

            switch (key)
            {
                case "id":
                    id = Unescape(value);
                    break;
                case "rev":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out revision))
                    {
                        return null;
                    }

                    break;
                case "who":
                    who = Unescape(value);
                    break;
                case "goal":
                    goal = Unescape(value);
                    break;
                case "reason":
                    reason = Unescape(value);
                    break;
                case "success":
                    success = Unescape(value);
                    break;
                case "state":
                    if (!Enum.TryParse(value, out state))
                    {
                        return null;
                    }

                    break;
                case "index":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                    {
                        return null;
                    }

                    break;
                case "fail":
                    fail = Unescape(value);
                    break;
                case "stepFail":
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out stepFail))
                    {
                        return null;
                    }

                    break;
                case OldRetriesKey:
                case OldCreatedKey:
                    break;
                case "expires":
                    if (!TryTicks(value, out expires))
                    {
                        return null;
                    }

                    break;
                case "trigger":
                    if (!Enum.TryParse(value, out trigger))
                    {
                        return null;
                    }

                    break;
                case "step":
                    if (!TryStep(value, out var step))
                    {
                        return null;
                    }

                    steps.Add(step);
                    break;
                case "result":
                    if (!TryResult(value, out var record))
                    {
                        return null;
                    }

                    results.Add(record);
                    break;
                default:
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(id) || steps.Count == 0)
        {
            return null;
        }

        return new ModelPlan
        {
            Id = id,
            Revision = revision,
            CharacterId = who,
            Goal = goal,
            Reason = reason,
            Success = success,
            Steps = steps,
            Index = index < 0 ? 0 : index,
            State = state,
            FailureReason = fail,
            StepFailures = stepFail < 0 ? 0 : stepFail,
            Expires = expires,
            Results = results,
            Trigger = trigger
        };
    }

    public ModelPlan With(
        int? index = null,
        PlanState? state = null,
        string failureReason = null,
        int? stepFailures = null,
        IReadOnlyList<PlanResultRecord> results = null,
        PlanTrigger? trigger = null
    ) =>
        new()
        {
            Id = Id,
            Revision = Revision,
            CharacterId = CharacterId,
            Goal = Goal,
            Reason = Reason,
            Success = Success,
            Steps = Steps,
            Index = index ?? Index,
            State = state ?? State,
            FailureReason = failureReason ?? FailureReason,
            StepFailures = stepFailures ?? StepFailures,
            Expires = Expires,
            Results = results ?? Results,
            Trigger = trigger ?? Trigger
        };

    public static IReadOnlyList<PlanResultRecord> AppendResult(
        IReadOnlyList<PlanResultRecord> current,
        PlanResultRecord next
    )
    {
        var list = new List<PlanResultRecord>();

        if (current != null)
        {
            list.AddRange(current);
        }

        list.Add(next);

        while (list.Count > MaxResults)
        {
            list.RemoveAt(0);
        }

        return list;
    }

    private static bool TryTicks(string value, out DateTime time)
    {
        time = default;

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < 0)
        {
            return false;
        }

        time = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    private static bool TryStep(string value, out ModelPlanStep step)
    {
        step = default;
        var parts = SplitPipe(value);

        if (parts.Count != 3 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return false;
        }

        step = new ModelPlanStep(Unescape(parts[0]), Unescape(parts[1]), Unescape(parts[2]));
        return true;
    }

    private static bool TryResult(string value, out PlanResultRecord record)
    {
        record = default;
        var parts = SplitPipe(value);

        if (parts.Count != 3 || !Enum.TryParse(parts[1], out StepResultKind kind))
        {
            return false;
        }

        record = new PlanResultRecord(Unescape(parts[0]), kind, Unescape(parts[2]));
        return true;
    }

    private static List<string> SplitPipe(string value)
    {
        var parts = new List<string>();
        var builder = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '|' && (i + 1 >= value.Length || value[i + 1] != '|'))
            {
                parts.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            if (value[i] == '|' && i + 1 < value.Length && value[i + 1] == '|')
            {
                builder.Append('|');
                i++;
                continue;
            }

            builder.Append(value[i]);
        }

        parts.Add(builder.ToString());
        return parts;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("|", "||", StringComparison.Ordinal);
    }

    private static string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("||", "|", StringComparison.Ordinal);
    }
}
