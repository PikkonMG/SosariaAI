using System.Collections.Generic;
using System.Text;

namespace SosariaAI.Logging;

/// <summary>
/// The shared shape of the ten-minute census lines in the activity log: when one is due, and
/// "name count" pairs, biggest count first, so a run can be counted by eye. Pure.
/// </summary>
public static class CensusText
{
    /// <summary>Every this many minutes a census line is written.</summary>
    public const int CensusMinutes = 10;

    /// <summary>True on the minutes a census line is written.</summary>
    public static bool Due(int minute) => minute > 0 && minute % CensusMinutes == 0;

    /// <summary>Appends "name count" pairs, biggest count first, or "none".</summary>
    public static StringBuilder Counted(StringBuilder line, IReadOnlyDictionary<string, int> counts)
    {
        var rows = new List<KeyValuePair<string, int>>(counts ?? new Dictionary<string, int>());
        rows.Sort(static (a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));

        for (var i = 0; i < rows.Count; i++)
        {
            line.Append(i == 0 ? string.Empty : ", ").Append(rows[i].Key).Append(' ').Append(rows[i].Value);
        }

        return rows.Count == 0 ? line.Append("none") : line;
    }
}
