using System;
using System.Collections.Generic;

namespace SosariaAI.Combat;

/// <summary>
/// Which spawn ids become outlaws. The model never decides this.
/// </summary>
public static class PkSlotRules
{
    /// <summary>
    /// <paramref name="pkCount"/> is the facet's red count from <see cref="OutlawRules.RedCount"/>.
    /// A red stays red: every id in <paramref name="savedReds"/> (a person the world save holds
    /// with a red count) comes first, in roster order, even past the count. The picks were
    /// made again at every boot, and a plan grown from 1200 to 2000 people handed most red
    /// slots to the new people at its end: the old reds woke blue in the Den. The slots left
    /// go to veteran non-leader fighters from the end of the roster, so later veterans are
    /// chosen first, then to the remaining non-leader fighters (novices), also from the end.
    /// Workers and party leaders never take a free slot.
    /// </summary>
    public static IReadOnlyList<string> Select(
        IReadOnlyList<string> ids,
        IReadOnlyList<bool> isVeteran,
        IReadOnlyList<bool> isFighter,
        IReadOnlyList<bool> isPartyLeader,
        IReadOnlyCollection<string> savedReds,
        int pkCount
    )
    {
        if (pkCount <= 0 ||
            ids == null ||
            isVeteran == null ||
            isFighter == null ||
            isPartyLeader == null ||
            ids.Count == 0)
        {
            return [];
        }

        var length = ids.Count;

        if (isVeteran.Count < length)
        {
            length = isVeteran.Count;
        }

        if (isFighter.Count < length)
        {
            length = isFighter.Count;
        }

        if (isPartyLeader.Count < length)
        {
            length = isPartyLeader.Count;
        }

        if (length <= 0)
        {
            return [];
        }

        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AppendSaved(ids, savedReds, length, selected, seen);
        var count = Math.Max(selected.Count, Math.Min(pkCount, length));

        AppendEligible(ids, isVeteran, isFighter, isPartyLeader, length, requireVeteran: true, selected, seen, count);
        AppendEligible(ids, isVeteran, isFighter, isPartyLeader, length, requireVeteran: false, selected, seen, count);

        return selected;
    }

    private static void AppendSaved(
        IReadOnlyList<string> ids,
        IReadOnlyCollection<string> savedReds,
        int length,
        List<string> selected,
        HashSet<string> seen
    )
    {
        if (savedReds == null || savedReds.Count == 0)
        {
            return;
        }

        var saved = new HashSet<string>(savedReds, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < length; i++)
        {
            var id = ids[i];

            if (!string.IsNullOrWhiteSpace(id) && saved.Contains(id) && seen.Add(id))
            {
                selected.Add(id);
            }
        }
    }

    private static void AppendEligible(
        IReadOnlyList<string> ids,
        IReadOnlyList<bool> isVeteran,
        IReadOnlyList<bool> isFighter,
        IReadOnlyList<bool> isPartyLeader,
        int length,
        bool requireVeteran,
        List<string> selected,
        HashSet<string> seen,
        int count
    )
    {
        for (var i = length - 1; i >= 0 && selected.Count < count; i--)
        {
            if (!isFighter[i] || isPartyLeader[i])
            {
                continue;
            }

            if (requireVeteran && !isVeteran[i])
            {
                continue;
            }

            var id = ids[i];

            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            selected.Add(id);
        }
    }
}
