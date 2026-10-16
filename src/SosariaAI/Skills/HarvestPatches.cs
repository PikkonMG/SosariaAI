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

    /// <summary>Minutes a worker that came to its harvest still loaded is left off every harvest.</summary>
    public const int LoadedRestMinutes = 15;

    /// <summary>
    /// How long a worker that came to its harvest still loaded is left off every harvest, unless
    /// an unload trip ends it sooner (<see cref="NoteUnloaded"/>).
    /// </summary>
    public static readonly TimeSpan LoadedRest = TimeSpan.FromMinutes(LoadedRestMinutes);

    /// <summary>Minutes a worker whose trip found no forge or counter for its haul banks the haul.</summary>
    public const int NoOutletMinutes = 30;

    /// <summary>How long a worker whose trip found no forge or counter for its haul banks the haul.</summary>
    public static readonly TimeSpan NoOutletRest = TimeSpan.FromMinutes(NoOutletMinutes);

    private static readonly Dictionary<(string Kind, int MapId, Rectangle2D Patch), DateTime> Resting = new();
    private static readonly Dictionary<(uint Worker, string Kind), DateTime> DryUntil = new();
    private static readonly Dictionary<uint, DateTime> LoadedUntil = new();
    private static readonly Dictionary<uint, DateTime> NoOutletUntil = new();

    public static void Rest(string kind, int mapId, Rectangle2D patch, DateTime until) =>
        Resting[(kind, mapId, patch)] = until;

    /// <summary>True while a patch found empty of this harvest still rests.</summary>
    public static bool IsResting(string kind, int mapId, Rectangle2D patch, DateTime now) =>
        Holds(Resting, (kind, mapId, patch), now);

    /// <summary>
    /// The worker found no patch with work on any site in its reach: until the engine's banks
    /// refill, the harvest is not its job, and the planner gives it other work.
    /// </summary>
    public static void NoteDry(uint worker, string kind, DateTime until) => DryUntil[(worker, kind)] = until;

    /// <summary>
    /// The worker came to a harvest carrying a full load: no harvest is its job until an unload
    /// trip empties the pack or the rest ends. Tilda Hallow came to her mine with 27 ore three
    /// times in one night and failed "carries a full load" each time, between unload trips that
    /// took nothing off her.
    /// </summary>
    public static void NoteLoaded(uint worker, DateTime until) => LoadedUntil[worker] = until;

    /// <summary>An unload trip put haul in the bank, on a counter or through a forge: the worker may harvest again.</summary>
    public static void NoteUnloaded(uint worker) => LoadedUntil.Remove(worker);

    /// <summary>
    /// True while the harvest is not the worker's job: every site of it in the worker's reach
    /// was found bare (<see cref="NoteDry"/>), or the worker came to a harvest still loaded
    /// (<see cref="NoteLoaded"/>).
    /// </summary>
    public static bool IsDry(uint worker, string kind, DateTime now) =>
        Holds(DryUntil, (worker, kind), now) || Holds(LoadedUntil, worker, now);

    /// <summary>
    /// The worker's trip found no forge or counter that took its haul: until
    /// <paramref name="until"/> the bank box takes the haul a shop in reach would buy
    /// (<see cref="SmeltOrBank"/>).
    /// </summary>
    public static void NoteNoOutlet(uint worker, DateTime until) => NoOutletUntil[worker] = until;

    /// <summary>True while the worker's last trip found no forge or counter for its haul.</summary>
    public static bool HasNoOutlet(uint worker, DateTime now) => Holds(NoOutletUntil, worker, now);

    /// <summary>
    /// Drops every note of a worker that left the world for good: its bare harvests, its load
    /// rest and its haul with no outlet. A lapsed note is only dropped when read again, and a
    /// gone worker is never read again.
    /// </summary>
    public static void Forget(uint worker)
    {
        LoadedUntil.Remove(worker);
        NoOutletUntil.Remove(worker);

        var dry = new List<(uint Worker, string Kind)>();

        foreach (var key in DryUntil.Keys)
        {
            if (key.Worker == worker)
            {
                dry.Add(key);
            }
        }

        for (var i = 0; i < dry.Count; i++)
        {
            DryUntil.Remove(dry[i]);
        }
    }

    /// <summary>True while the note under <paramref name="key"/> lasts; a lapsed note is dropped.</summary>
    private static bool Holds<TKey>(Dictionary<TKey, DateTime> notes, TKey key, DateTime now)
    {
        if (!notes.TryGetValue(key, out var until))
        {
            return false;
        }

        if (now < until)
        {
            return true;
        }

        notes.Remove(key);
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
