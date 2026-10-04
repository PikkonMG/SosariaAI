using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Skills;

/// <summary>
/// Work patches found with nothing to harvest, shared by every worker. The engine's harvest
/// bank refills an emptied spot no sooner than its definition's respawn time, so a patch
/// rests that long before anyone walks to it again. Seventeen woodcutters walked into the
/// cut-bare Britain wood one after another and each failed "nothing to work at the patch".
/// World thread only.
/// </summary>
public static class HarvestPatches
{
    /// <summary>Patches a job walks on to after the one it started on runs dry.</summary>
    public const int MaxPatchMoves = 2;

    private static readonly Dictionary<(string Kind, int MapId, Rectangle2D Patch), DateTime> Resting = new();
    private static readonly Dictionary<(uint Worker, string Kind), DateTime> DryUntil = new();

    public static void Rest(string kind, int mapId, Rectangle2D patch, DateTime until) =>
        Resting[(kind, mapId, patch)] = until;

    /// <summary>True while a patch found empty of this harvest still rests.</summary>
    public static bool IsResting(string kind, int mapId, Rectangle2D patch, DateTime now)
    {
        var key = (kind, mapId, patch);

        if (!Resting.TryGetValue(key, out var until))
        {
            return false;
        }

        if (now < until)
        {
            return true;
        }

        Resting.Remove(key);
        return false;
    }

    /// <summary>
    /// The worker found no patch with work on any site in its reach: until the engine's banks
    /// refill, the harvest is not its job, and the planner gives it other work.
    /// </summary>
    public static void NoteDry(uint worker, string kind, DateTime until) => DryUntil[(worker, kind)] = until;

    /// <summary>True while every site of the harvest in the worker's reach was found bare.</summary>
    public static bool IsDry(uint worker, string kind, DateTime now)
    {
        var key = (worker, kind);

        if (!DryUntil.TryGetValue(key, out var until))
        {
            return false;
        }

        if (now < until)
        {
            return true;
        }

        DryUntil.Remove(key);
        return false;
    }

    /// <summary>True when a job that already moved <paramref name="moves"/> times may walk on to another patch.</summary>
    public static bool MayMoveOn(int moves) => moves < MaxPatchMoves;

    /// <summary>
    /// The patch to work: the first of <paramref name="cells"/> that is not resting and has work
    /// now, preferring one the person can reach, else the first with work to try anyway. A
    /// cell found without work goes to <paramref name="noWork"/> so it rests. False when no
    /// cell has work.
    /// </summary>
    public static bool Pick(
        IReadOnlyList<Rectangle2D> cells,
        Func<Rectangle2D, bool> resting,
        Func<Rectangle2D, bool> hasWork,
        Func<Rectangle2D, bool> reachable,
        Action<Rectangle2D> noWork,
        out Rectangle2D patch
    )
    {
        var found = false;
        patch = default;

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];

            if (resting(cell))
            {
                continue;
            }

            if (!hasWork(cell))
            {
                noWork(cell);
                continue;
            }

            if (reachable(cell))
            {
                patch = cell;
                return true;
            }

            if (!found)
            {
                patch = cell;
                found = true;
            }
        }

        return found;
    }
}
