using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using SosariaAI.Social;

namespace SosariaAI.Admin;

/// <summary>A character the watchdog sees standing still with no progress.</summary>
public readonly record struct StuckLine(string Name, string Activity, string Place, int Minutes, int Rescues);

/// <summary>Everything one status page shows. Built on the game thread each minute.</summary>
public sealed record FleetReport(
    DateTime At,
    FleetCounts Counts,
    IReadOnlyList<StuckLine> Stuck,
    int RepeatFailing,
    JournalTally.Result Journal,
    bool BrainEnabled,
    int RoadMoves,
    int Rescores
);

/// <summary>
/// The minute status page: plain HTML with no scripts or outside files. The browser
/// reloads it once a minute, when the plugin writes it again. Pure.
/// </summary>
public static class StatusPage
{
    public const string FileName = "status.html";
    public const int TopLimit = 12;
    public const int MurderLimit = 10;
    public const int RefreshSeconds = 60;
    public const string TimeFormat = "yyyy-MM-dd HH:mm:ss 'UTC'";
    public const string Title = "SosariaAI status";
    public const string NoneText = "none";
    public const string UnknownKiller = "someone";

    public static string Render(FleetReport report)
    {
        var counts = report.Counts ?? new FleetCounts();
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta http-equiv=\"refresh\" content=\"").Append(RefreshSeconds).Append("\">");
        html.Append("<title>").Append(Title).Append("</title>");
        html.Append(
            "<style>body{font-family:sans-serif;margin:16px;background:#fafafa;color:#222}" +
            "table{border-collapse:collapse;margin:0 0 16px}td,th{border:1px solid #ccc;padding:3px 8px;text-align:left}" +
            "th{background:#eee}h2{margin:18px 0 6px;font-size:1.1em}</style></head><body>\n"
        );
        html.Append("<h1>").Append(Title).Append("</h1>\n<p>Written ")
            .Append(Encode(report.At.ToString(TimeFormat, CultureInfo.InvariantCulture)))
            .Append(". This page is written again every minute.</p>\n");

        AppendTable(
            html,
            "Population",
            [
                Row("Live", counts.Live),
                Row("Reds", counts.Reds),
                Row("Ghosts", counts.Ghosts),
                Row("In combat", counts.InCombat),
                Row("In dungeons", counts.InDungeons),
                Row("Supplies low", counts.SuppliesLow),
                Row("Parties", counts.Parties)
            ]
        );
        AppendFacets(html, counts);
        AppendCounts(html, "Activities", FleetCounts.Top(counts.ByActivity, -1));
        AppendCounts(html, "Jobs", FleetCounts.Top(counts.ByJob, -1));
        AppendCounts(html, "Top places", FleetCounts.Top(counts.ByPlace, TopLimit));
        AppendJournal(html, report);
        AppendStuck(html, report);
        AppendTable(
            html,
            "Model calls since boot",
            [
                Row("Brain enabled", report.BrainEnabled ? "yes" : "no"),
                Row("Chat calls", counts.ChatCalls.ToString(CultureInfo.InvariantCulture)),
                Row("Planning calls", counts.PlanCalls.ToString(CultureInfo.InvariantCulture))
            ]
        );
        html.Append("</body></html>\n");
        return html.ToString();
    }

    public static string MurderLine(ShardEvent evt) =>
        $"{evt.At.ToString("HH:mm", CultureInfo.InvariantCulture)} " +
        $"{(string.IsNullOrWhiteSpace(evt.Other) ? UnknownKiller : evt.Other)} murdered {evt.Actor}" +
        (string.IsNullOrWhiteSpace(evt.Place) ? string.Empty : $" at {evt.Place}");

    private static void AppendFacets(StringBuilder html, FleetCounts counts)
    {
        html.Append("<h2>By facet</h2>\n<table><tr><th>Facet</th><th>Live</th></tr>\n");

        foreach (var (facet, live) in counts.LiveByFacet)
        {
            html.Append("<tr><td>").Append(Encode(facet)).Append("</td><td>")
                .Append(live).Append("</td></tr>\n");
        }

        html.Append("</table>\n");
    }

    private static void AppendCounts(StringBuilder html, string heading, List<KeyValuePair<string, int>> rows)
    {
        var cells = new List<(string, string)>(rows.Count);

        foreach (var row in rows)
        {
            cells.Add(Row(row.Key, row.Value));
        }

        AppendTable(html, heading, cells);
    }

    private static void AppendJournal(StringBuilder html, FleetReport report)
    {
        AppendTable(
            html,
            "Last hour",
            [
                Row("Murders", report.Journal.Murders),
                Row("Other deaths", report.Journal.Deaths)
            ]
        );

        var murders = report.Journal.RecentMurders;

        if (murders == null || murders.Count == 0)
        {
            return;
        }

        html.Append("<ul>\n");

        for (var i = 0; i < murders.Count; i++)
        {
            html.Append("<li>").Append(Encode(MurderLine(murders[i]))).Append("</li>\n");
        }

        html.Append("</ul>\n");
    }

    private static void AppendStuck(StringBuilder html, FleetReport report)
    {
        AppendTable(
            html,
            "Watchdog",
            [
                Row("Stuck now", report.Stuck?.Count ?? 0),
                Row("Moved back to a road", report.RoadMoves),
                Row("Plans dropped and re-scored", report.Rescores),
                Row("Repeating one failed plan", report.RepeatFailing)
            ]
        );

        if (report.Stuck == null || report.Stuck.Count == 0)
        {
            return;
        }

        html.Append(
            "<table><tr><th>Name</th><th>Doing</th><th>Place</th><th>Still (min)</th><th>Rescues</th></tr>\n"
        );

        foreach (var line in report.Stuck)
        {
            html.Append("<tr><td>").Append(Encode(line.Name)).Append("</td><td>")
                .Append(Encode(line.Activity)).Append("</td><td>")
                .Append(Encode(line.Place)).Append("</td><td>")
                .Append(line.Minutes).Append("</td><td>")
                .Append(line.Rescues).Append("</td></tr>\n");
        }

        html.Append("</table>\n");
    }

    private static void AppendTable(StringBuilder html, string heading, IReadOnlyList<(string Name, string Value)> rows)
    {
        html.Append("<h2>").Append(Encode(heading)).Append("</h2>\n");

        if (rows.Count == 0)
        {
            html.Append("<p>").Append(NoneText).Append("</p>\n");
            return;
        }

        html.Append("<table>\n");

        for (var i = 0; i < rows.Count; i++)
        {
            html.Append("<tr><td>").Append(Encode(rows[i].Name)).Append("</td><td>")
                .Append(Encode(rows[i].Value)).Append("</td></tr>\n");
        }

        html.Append("</table>\n");
    }

    private static (string, string) Row(string name, int value) =>
        (name, value.ToString(CultureInfo.InvariantCulture));

    private static (string, string) Row(string name, string value) => (name, value);

    private static string Encode(string text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
