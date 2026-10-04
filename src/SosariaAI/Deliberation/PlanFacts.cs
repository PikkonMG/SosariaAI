using System;
using System.Collections.Generic;
using System.Text;

namespace SosariaAI.Deliberation;

/// <summary>
/// Bounded facts for one planning call. Not the whole world and not the whole history.
/// </summary>
public sealed class PlanFacts
{
    public const int MaxList = 8;
    public const int MaxMemory = 8;

    /// <summary>Recent thoughts keep at least this much of <see cref="MaxMemory"/> when the adventures would fill it.</summary>
    public const int MinThoughts = MaxMemory / 2;

    /// <summary>Adventures shared with the person present that a prompt carries, biggest first.</summary>
    public const int MaxShared = 3;
    public const int MaxFailures = 4;
    public const int MaxActions = 24;

    public string Role { get; init; }

    public string Drives { get; init; }

    public IReadOnlyList<string> SupportedActions { get; init; } = [];

    public int BankGold { get; init; }

    public int Goods { get; init; }

    public int Tools { get; init; }

    public bool ArmorEquipped { get; init; }

    public bool InDanger { get; init; }

    public bool IsGhost { get; init; }

    public bool InCombat { get; init; }

    public string Place { get; init; }

    public string Facet { get; init; }

    public string Expansion { get; init; }

    public IReadOnlyList<string> Destinations { get; init; } = [];

    public IReadOnlyList<string> People { get; init; } = [];

    public IReadOnlyList<string> Items { get; init; } = [];

    public IReadOnlyList<string> Memories { get; init; } = [];

    public IReadOnlyList<string> RecentFailures { get; init; } = [];

    public IReadOnlyList<string> Promises { get; init; } = [];

    public string CurrentPlan { get; init; }

    public string CurrentStep { get; init; }

    public string Trigger { get; init; }

    public static IReadOnlyList<string> Bound(IReadOnlyList<string> values, int cap)
    {
        if (values == null || values.Count == 0 || cap <= 0)
        {
            return [];
        }

        if (values.Count <= cap)
        {
            return values;
        }

        var start = values.Count - cap;
        var slice = new string[cap];

        for (var i = 0; i < cap; i++)
        {
            slice[i] = values[start + i];
        }

        return slice;
    }

    /// <summary>
    /// The memory a prompt carries: the newest adventures (oldest first), then the newest recent
    /// thoughts, at most <see cref="MaxMemory"/> in all. Thoughts keep up to
    /// <see cref="MinThoughts"/> places; adventures take the rest.
    /// </summary>
    public static IReadOnlyList<string> MemoriesFrom(IReadOnlyList<string> adventures, IReadOnlyList<string> thoughts)
    {
        var thoughtRoom = Math.Min(thoughts?.Count ?? 0, MinThoughts);
        var keptAdventures = Bound(adventures, MaxMemory - thoughtRoom);
        var keptThoughts = Bound(thoughts, MaxMemory - keptAdventures.Count);
        return [.. keptAdventures, .. keptThoughts];
    }

    /// <summary>
    /// The plan-only facts. The name, the long goal, hits and gold on hand are in the life lines
    /// every prompt carries (<see cref="PromptBuilder.BuildSystem"/>), so this block does not repeat them.
    /// </summary>
    public string ToPromptBlock()
    {
        var builder = new StringBuilder();
        builder.Append("Role: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(Role) ? "worker" : Role);

        if (!string.IsNullOrWhiteSpace(Drives))
        {
            builder.Append("Drives: ");
            builder.AppendLine(Drives);
        }

        builder.Append("Gold in bank: ");
        builder.Append(BankGold);
        builder.AppendLine();
        builder.Append("Work goods: ");
        builder.Append(Goods);
        builder.AppendLine();
        builder.Append("Tools: ");
        builder.Append(Tools);
        builder.AppendLine();
        builder.Append("Armor on: ");
        builder.AppendLine(ArmorEquipped ? "yes" : "no");
        builder.Append("Place: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(Place) ? "unknown" : Place);
        builder.Append("Facet: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(Facet) ? "unknown" : Facet);
        builder.Append("Era: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(Expansion) ? "unknown" : Expansion);

        if (IsGhost)
        {
            builder.AppendLine("You are a ghost.");
        }

        if (InDanger || InCombat)
        {
            builder.AppendLine("You are in immediate danger.");
        }

        AppendList(builder, "Actions you can take", SupportedActions);
        AppendList(builder, "Known places", Destinations);
        AppendList(builder, "People you know", People);
        AppendList(builder, "Useful goods", Items);
        AppendList(builder, "Promises", Promises);
        AppendList(builder, "Recent failures", RecentFailures);
        AppendList(builder, "Memory", Memories);

        if (!string.IsNullOrWhiteSpace(CurrentPlan))
        {
            builder.Append("Current plan: ");
            builder.AppendLine(CurrentPlan);
        }

        if (!string.IsNullOrWhiteSpace(CurrentStep))
        {
            builder.Append("Current step: ");
            builder.AppendLine(CurrentStep);
        }

        if (!string.IsNullOrWhiteSpace(Trigger))
        {
            builder.Append("Why you must plan now: ");
            builder.AppendLine(Trigger);
        }

        builder.AppendLine(
            "Pick a goal that fits this character. Name steps from the action list only. " +
            "Use ref dest:<place>, person:<id>, or item:<kind> from the lists. " +
            "Do not invent coordinates, items, or server commands. " +
            "Earlier steps may earn gold or goods that later steps spend."
        );
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendJoin(", ", values);
        builder.AppendLine();
    }
}
