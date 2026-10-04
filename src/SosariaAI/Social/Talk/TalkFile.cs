using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SosariaAI.Behaviour;

namespace SosariaAI.Social;

/// <summary>
/// The operator's talk file format: one line per row, "#" notes, blank rows skipped. A row that
/// starts with "[eras: ml,modern]" keeps that line to those eras; a row holding only the tag
/// sets the eras for every row below it, and "[eras: all]" ends that. Pure.
/// </summary>
public static class TalkFile
{
    public const string Extension = ".txt";
    public const string CommentMark = "#";
    public const string EraTagOpen = "[eras:";
    public const char EraTagClose = ']';
    public const string AllEras = "all";

    private const char EraSeparator = ',';
    private const string NoSlots = "none";

    // Nothing stands behind these lines when they are said, so an invite or a trip claim is a lie.
    private const string PromiseFreeNote =
        " Nobody here sets out or opens a group: a line inviting people along or saying it is on its way is skipped and logged.";

    private static readonly string[] KnownEras = [EraBands.T2ATag, EraBands.MLTag, EraBands.ModernTag];

    public sealed record Result(List<TalkLine> Lines, List<string> Problems);

    public static Result Parse(IEnumerable<string> rows)
    {
        var lines = new List<TalkLine>();
        var problems = new List<string>();
        string[] sectionEras = [];
        var rowNumber = 0;

        foreach (var raw in rows)
        {
            rowNumber++;
            var row = raw?.Trim();

            if (string.IsNullOrEmpty(row) || row.StartsWith(CommentMark, StringComparison.Ordinal))
            {
                continue;
            }

            var eras = sectionEras;

            if (row.StartsWith(EraTagOpen, StringComparison.OrdinalIgnoreCase))
            {
                var close = row.IndexOf(EraTagClose);

                if (close < 0)
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "row {0}: an [eras: ...] tag has no closing ]", rowNumber));
                    continue;
                }

                eras = ReadEras(row[EraTagOpen.Length..close], rowNumber, problems);
                row = row[(close + 1)..].Trim();

                if (row.Length == 0)
                {
                    sectionEras = eras;
                    continue;
                }
            }

            var line = TalkLine.Of(row, eras);

            if ((line.Needs & TalkSlot.Unknown) != 0)
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture, "row {0}: an unknown {{slot}} in \"{1}\"; the line is never said", rowNumber, row));
            }

            lines.Add(line);
        }

        return new Result(lines, problems);
    }

    /// <summary>The file written when the operator has none yet: a short note, then the default lines.</summary>
    public static string Render(TalkTopic topic)
    {
        var text = new StringBuilder();
        text.Append(CommentMark).Append(' ').Append(topic.Name).Append(": ").AppendLine(topic.About);
        text.Append(CommentMark).Append(" Slots: ").AppendLine(SlotList(topic.Lines));
        text.Append(CommentMark).AppendLine(" One line per row. Rows starting with # are notes. Blank rows are skipped.");
        text.Append(CommentMark).AppendLine(" \"[eras: t2a] text\" keeps one line to those eras (t2a, ml, modern).");
        text.Append(CommentMark).AppendLine(" A row holding only \"[eras: ml,modern]\" sets the eras for the rows below it; \"[eras: all]\" ends that.");
        text.Append(CommentMark).AppendLine(" A line naming a slot the moment does not fill is skipped. Edits are kept: this file is only written when missing.");

        if (TalkDefaults.PromiseFree.Contains(topic.Name))
        {
            text.Append(CommentMark).AppendLine(PromiseFreeNote);
        }

        text.AppendLine();

        foreach (var line in topic.Lines)
        {
            text.AppendLine(line);
        }

        return text.ToString();
    }

    private static string[] ReadEras(string list, int rowNumber, List<string> problems)
    {
        var eras = new List<string>();

        foreach (var part in list.Split(EraSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals(AllEras, StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            if (Array.IndexOf(KnownEras, part.ToLowerInvariant()) < 0)
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture, "row {0}: unknown era \"{1}\" (use t2a, ml, modern or all)", rowNumber, part));
            }

            eras.Add(part.ToLowerInvariant());
        }

        return [.. eras];
    }

    private static string SlotList(string[] lines)
    {
        var needs = TalkSlot.None;

        foreach (var line in lines)
        {
            needs |= TalkSlots.Needs(line);
        }

        var names = new List<string>();

        foreach (var slot in Enum.GetValues<TalkSlot>())
        {
            if (slot is not (TalkSlot.None or TalkSlot.Unknown) && (needs & slot) != 0)
            {
                names.Add(TalkSlots.TokenOf(slot));
            }
        }

        return names.Count == 0 ? NoSlots : string.Join(' ', names);
    }
}
