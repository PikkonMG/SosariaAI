using System;
using System.Collections.Generic;
using System.Globalization;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using SosariaAI.Memory;

namespace SosariaAI.Mobiles;

/// <summary>
/// Pure text for staff commands. No world objects.
/// </summary>
public static class CharacterCommandText
{
    public const int NameColumn = 12;
    public const int WatchMemoryLines = 6;
    public const string NoName = "(unnamed)";
    public const string GhostMark = "ghost";
    public const string AnotherFacet = "another facet";
    public const string TilesSuffix = " tiles";
    public const string NoOpinion = "no opinion of you";
    public const string NoMemory = "nothing remembered";
    public const string MemoryPrefix = "Memory:";
    public const string MemoryItemPrefix = "- ";
    public const string DoingPrefix = "Doing: ";
    public const string BackgroundPrefix = "Background: ";
    public const string VoicePrefix = "Voice: ";
    public const string IdlePrefix = "Idle: ";
    public const string AmbitionPrefix = "Ambition: ";
    public const string DayPrefix = "Day: ";
    public const string OpinionPrefix = "Opinion of you: ";
    public const string GoalPrefix = "Goal: ";
    public const string DispositionPrefix = "Disposition: ";
    public const string PartyPrefix = "Party: ";
    public const string NoParty = "none";
    public const string ChoicesPrefix = "Choices: ";
    public const string ChosePrefix = "Chose ";
    public const string HomeDistancePrefix = "Home: ";
    public const string NotAFighter = "not a fighter";
    public const string ListFileName = "sosaria-list.txt";
    public const int MemoryTopBonds = 8;
    public const int MemoryLastAdventures = 8;
    public const string MemoryTitlePrefix = "Long-term memory of ";
    public const string BondsPrefix = "Bonds: ";
    public const string AdventuresPrefix = "Adventures: ";
    public const string NoBonds = "nobody known";
    public const string NoAdventures = "nothing kept";
    public const string NeverMetPlace = "somewhere";
    public const string MemoryClosed = "The memory file is not open; nothing is kept.";
    public const string WhenFormat = "yyyy-MM-dd HH:mm";
    private const string PeopleSeparator = "; ";
    private const string NameSeparator = ", ";
    public static string CountLine(int count) => $"{count} characters.";

    public static string WroteFile(string path, int count) =>
        $"Wrote {count} characters to {path}.";

    public static string ListReport(IReadOnlyList<string> lines, int count)
    {
        var body = lines == null || lines.Count == 0
            ? string.Empty
            : string.Join("\n", lines);
        var total = CountLine(count);
        return string.IsNullOrEmpty(body) ? total : body + "\n" + total;
    }

    public static string PadName(string name)
    {
        var text = string.IsNullOrWhiteSpace(name) ? NoName : name;
        return text.Length >= NameColumn ? text : text.PadRight(NameColumn);
    }

    public static string DistanceLabel(bool sameMap, int tiles) =>
        sameMap ? $"{tiles}{TilesSuffix}" : AnotherFacet;

    public static string AmbitionBrief(Ambition ambition)
    {
        if (ambition.Kind == AmbitionKind.None)
        {
            return AmbitionRules.DescribeNone;
        }

        var target = string.IsNullOrWhiteSpace(ambition.Target) ? AmbitionRules.DescribeNone : ambition.Target;
        return $"{ambition.Kind} {target} {ambition.Progress}/{ambition.Goal}";
    }

    public static string ListLine(
        string name,
        string activity,
        string location,
        string map,
        int power,
        string distance,
        bool ghost,
        Ambition ambition,
        string personaKey = null
    )
    {
        var ghostBit = ghost ? $", {GhostMark}" : string.Empty;
        var key = string.IsNullOrWhiteSpace(personaKey) ? string.Empty : $" ({personaKey})";
        return $"{PadName(name)}{key} {activity} at {location} on {map} " +
               $"[power {power}, {distance}{ghostBit}] {AmbitionBrief(ambition)}";
    }

    public static string DayLine(int hour, DayPart part, int? startHour, int? endHour)
    {
        var start = startHour ?? DayShapeRules.DefaultStartHour;
        var end = endHour ?? DayShapeRules.DefaultEndHour;
        return $"{DayPrefix}{hour} {part} (active {start}-{end})";
    }

    public static string OpinionLine(Bond bond)
    {
        if (bond == null)
        {
            return $"{OpinionPrefix}{NoOpinion}";
        }

        return $"{OpinionPrefix}{Recall.Tone(bond.Score)} {bond.Score} ({bond.LastReason})";
    }

    /// <summary>
    /// The [SosariaMemory report: the warmest bonds (name, tone, score, shared adventures, where
    /// and when they first met) and the newest adventures (when, kind, summary, place, who).
    /// </summary>
    public static List<string> MemoryReport(MemoryStore store, string name, string personId, DateTime now)
    {
        var lines = new List<string> { $"{MemoryTitlePrefix}{(string.IsNullOrWhiteSpace(name) ? NoName : name)}" };

        if (store is not { IsOpen: true })
        {
            lines.Add(MemoryClosed);
        }

        var bonds = Recall.BondsOf(store, personId);
        lines.Add(bonds.Count == 0 ? $"{BondsPrefix}{NoBonds}" : $"{BondsPrefix}{Math.Min(bonds.Count, MemoryTopBonds)} of {bonds.Count}");

        for (var i = 0; i < bonds.Count && i < MemoryTopBonds; i++)
        {
            lines.Add(MemoryItemPrefix + BondLine(bonds[i], Recall.NameOf(store, bonds[i].OtherId)));
        }

        var adventures = Recall.AdventuresOf(store, personId);
        lines.Add(adventures.Count == 0 ? $"{AdventuresPrefix}{NoAdventures}" : $"{AdventuresPrefix}{Math.Min(adventures.Count, MemoryLastAdventures)} of {adventures.Count}");

        for (var i = 0; i < adventures.Count && i < MemoryLastAdventures; i++)
        {
            lines.Add(MemoryItemPrefix + AdventureLine(adventures[i], personId, now));
        }

        return lines;
    }

    /// <summary>"Tamsin: warm 26, 3 shared, met at Britain 2026-10-01 14:05 (healed me)".</summary>
    public static string BondLine(Bond bond, string otherName)
    {
        var place = string.IsNullOrWhiteSpace(bond.FirstMetPlace) ? NeverMetPlace : bond.FirstMetPlace;
        var reason = string.IsNullOrWhiteSpace(bond.LastReason) ? string.Empty : $" ({bond.LastReason})";
        return $"{Shown(otherName, bond.OtherId)}: {Recall.Tone(bond.Score)} {bond.Score}, " +
               $"{bond.SharedCount} shared, met at {place} {Stamp(bond.FirstMetAt)}{reason}";
    }

    /// <summary>"2026-10-03 20:10 (today) dungeon: cleared Despise at Despise; Tamsin with, Grim against".</summary>
    public static string AdventureLine(Adventure adventure, string readerId, DateTime now)
    {
        var who = new List<string>(adventure.Members.Count);

        for (var i = 0; i < adventure.Members.Count; i++)
        {
            var member = adventure.Members[i];

            if (member.Person.Id != readerId)
            {
                who.Add($"{Shown(member.Person.Name, member.Person.Id)} {member.Role}");
            }
        }

        var people = who.Count == 0 ? string.Empty : PeopleSeparator + string.Join(NameSeparator, who);
        return $"{Stamp(adventure.EndedAt)} ({Recall.WhenWord(adventure.EndedAt, now)}) {adventure.Kind}: {adventure.Headline} at {adventure.Place}{people}";
    }

    private static string Stamp(DateTime at) => at.ToString(WhenFormat, CultureInfo.InvariantCulture);

    private static string Shown(string name, string personId) => string.IsNullOrWhiteSpace(name) ? personId : name;

    public static List<string> LastMemory(IReadOnlyList<string> memory, int count)
    {
        var result = new List<string>();

        if (memory == null || memory.Count == 0 || count <= 0)
        {
            return result;
        }

        var start = memory.Count > count ? memory.Count - count : 0;

        for (var i = start; i < memory.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(memory[i]))
            {
                result.Add(memory[i]);
            }
        }

        return result;
    }

    public static string DispositionLine(string kind) =>
        $"{DispositionPrefix}{(string.IsNullOrWhiteSpace(kind) ? DispositionRules.NeutralName : kind)}";

    public static string PartyLine(string names) =>
        $"{PartyPrefix}{(string.IsNullOrWhiteSpace(names) ? NoParty : names)}";

    public static List<string> WatchLines(
        string name,
        string activity,
        string location,
        string map,
        Ambition ambition,
        int hour,
        DayPart dayPart,
        int? startHour,
        int? endHour,
        Bond opinion,
        IReadOnlyList<string> memory,
        string disposition = null,
        string party = null,
        string background = null,
        string voice = null,
        string idleLine = null
    )
    {
        var lines = new List<string>
        {
            $"{(string.IsNullOrWhiteSpace(name) ? NoName : name)} at {location} on {map}",
            $"{DoingPrefix}{activity}",
            DispositionLine(disposition),
            PartyLine(party),
            $"{AmbitionPrefix}{AmbitionBrief(ambition)} ({AmbitionRules.MoodHint(ambition)})",
            DayLine(hour, dayPart, startHour, endHour),
            OpinionLine(opinion)
        };

        if (!string.IsNullOrWhiteSpace(background))
        {
            lines.Add($"{BackgroundPrefix}{background}");
        }

        if (!string.IsNullOrWhiteSpace(voice))
        {
            lines.Add($"{VoicePrefix}{voice}");
        }

        if (!string.IsNullOrWhiteSpace(idleLine))
        {
            lines.Add($"{IdlePrefix}{idleLine}");
        }

        AppendMemory(lines, memory);
        return lines;
    }

    public static List<string> WatchLines(
        string name,
        string activity,
        string location,
        string map,
        Ambition ambition,
        int hour,
        DayPart dayPart,
        int? startHour,
        int? endHour,
        Bond opinion,
        IReadOnlyList<string> memory,
        ScoreResult score,
        string disposition = null,
        string party = null,
        string background = null,
        string voice = null,
        string idleLine = null
    )
    {
        var lines = WatchLines(
            name,
            activity,
            location,
            map,
            ambition,
            hour,
            dayPart,
            startHour,
            endHour,
            opinion,
            memory,
            disposition,
            party,
            background,
            voice,
            idleLine
        );

        var memoryStart = lines.FindIndex(line => line.StartsWith(MemoryPrefix, StringComparison.Ordinal));

        if (memoryStart < 0)
        {
            memoryStart = lines.Count;
        }

        var extra = DecisionLines(score);
        lines.InsertRange(memoryStart, extra);
        return lines;
    }

    public static List<string> PlanLines(PlanDiagnostics diag)
    {
        if (diag == null)
        {
            return ["Control: fallback"];
        }

        return [.. diag.WatchLines()];
    }

    public static List<string> DecisionLines(ScoreResult score)
    {
        var lines = new List<string>();

        if (score == null || string.IsNullOrWhiteSpace(score.Winner.Id.Value))
        {
            lines.Add($"{GoalPrefix}none");
            return lines;
        }

        lines.Add($"{GoalPrefix}{GoalRules.Describe(score.Goal)}");
        var top = score.Top3;
        var parts = new string[top.Count];

        for (var i = 0; i < top.Count; i++)
        {
            parts[i] = $"{top[i].Id.Value} {top[i].Score:0.00}";
        }

        lines.Add($"{ChoicesPrefix}{string.Join(", ", parts)}");
        lines.Add($"{ChosePrefix}{score.WinnerReason}");
        return lines;
    }

    private static void AppendMemory(List<string> lines, IReadOnlyList<string> memory)
    {
        var remembered = LastMemory(memory, WatchMemoryLines);

        if (remembered.Count == 0)
        {
            lines.Add($"{MemoryPrefix} {NoMemory}");
            return;
        }

        lines.Add(MemoryPrefix);

        for (var i = 0; i < remembered.Count; i++)
        {
            lines.Add($"{MemoryItemPrefix}{remembered[i]}");
        }
    }

    public static string TextAfterName(string argString)
    {
        if (string.IsNullOrWhiteSpace(argString))
        {
            return null;
        }

        var trimmed = argString.Trim();
        var space = trimmed.IndexOf(' ');

        if (space < 0)
        {
            return null;
        }

        var rest = trimmed[(space + 1)..].Trim();
        return rest.Length == 0 ? null : rest;
    }
}
