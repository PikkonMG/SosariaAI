using System;
using System.Collections.Generic;

namespace SosariaAI.Combat;

/// <summary>
/// Most reds ride in small gangs of two to four, camp one PvP hot spot together (see
/// <see cref="Behaviour.HotSpotRules"/>) and converge on one victim. The operator sets the share
/// that rides in these gangs (pkGangPercent); each red past them starts as a gang of one, and
/// the spawner pairs those loners off into gangs of their own (see
/// <see cref="Spawning.RedRosterRules.RideTogether"/>), so no red rides alone while another
/// could ride with it. The gang is the red's place in the selected order, so it is the same
/// after every boot. Pure.
/// </summary>
public static class PkGangRules
{
    public const int NoGang = -1;

    /// <summary>Two reds ride in one gang when both carry the same gang; a red of no gang has no mate.</summary>
    public static bool SameGang(int gang, int otherGang) => gang != NoGang && otherGang == gang;
    public const int MinGangSize = 2;
    public const int MaxGangSize = 4;

    /// <summary>The share of reds that ride in gangs when characters.json does not say.</summary>
    public const int DefaultGangPercent = 80;

    public const int MaxPercent = 100;

    /// <summary>Gang mates this close join the gang's fight.</summary>
    public const int CallRange = 16;

    /// <summary>Felucca's dungeon halls lie east of this column; a mouth stands on the surface west of it.</summary>
    public const int SurfaceWidth = 5120;

    public const string MouthSuffix = "Entrance";

    /// <summary>A gang lurks at its camp this long before it moves on.</summary>
    public static readonly TimeSpan LurkTime = TimeSpan.FromMinutes(8);

    /// <summary>A lurking red with this much Hiding waits hidden.</summary>
    public const double LurkHiding = 30;

    /// <summary>The sizes repeat in this order, so the shard holds pairs, threes and fours.</summary>
    private static readonly int[] SizePattern = [3, 2, 4, 3, 2];

    /// <summary>How many of the reds ride in gangs. A single would-be gang member is left a loner to pair off.</summary>
    public static int GangedCount(int redCount, int gangPercent)
    {
        var percent = Math.Clamp(gangPercent, 0, MaxPercent);
        var ganged = (int)Math.Round(redCount * percent / (double)MaxPercent, MidpointRounding.AwayFromZero);
        return ganged < MinGangSize ? 0 : Math.Min(ganged, redCount);
    }

    /// <summary>
    /// The gang of the red at <paramref name="slot"/> in the selected order. The first reds
    /// ride in gangs; every red after them is a gang of one.
    /// </summary>
    public static int GangOf(int slot, int redCount, int gangPercent)
    {
        if (slot < 0 || slot >= redCount)
        {
            return NoGang;
        }

        var ganged = GangedCount(redCount, gangPercent);

        if (slot < ganged)
        {
            return GroupOf(slot, ganged);
        }

        var gangsFormed = ganged == 0 ? 0 : GroupOf(ganged - 1, ganged) + 1;
        return gangsFormed + slot - ganged;
    }

    private static int GroupOf(int slot, int redCount)
    {
        var start = 0;
        var gang = 0;

        while (true)
        {
            var size = SizePattern[gang % SizePattern.Length];
            var left = redCount - (start + size);

            // A last gang of one is no gang: this gang takes the straggler in, or a full
            // gang hands one of its own over so the last pair can ride.
            if (left > 0 && left < MinGangSize)
            {
                size = size < MaxGangSize ? size + left : size - (MinGangSize - left);
            }

            if (slot < start + size)
            {
                return gang;
            }

            start += size;
            gang++;
        }
    }

    /// <summary>The gang of each selected red, in the selected order.</summary>
    public static int[] Gangs(IReadOnlyList<string> selected, int gangPercent)
    {
        var count = selected?.Count ?? 0;
        var gangs = new int[count];

        for (var i = 0; i < count; i++)
        {
            gangs[i] = GangOf(i, count, gangPercent);
        }

        return gangs;
    }

    /// <summary>A dungeon's surface entrance: where crews came out hurt and loaded, and reds waited for them.</summary>
    public static bool IsDungeonMouth(string kind, string name, int x) =>
        string.Equals(kind, nameof(Navigation.DestinationKind.Dungeon), StringComparison.OrdinalIgnoreCase) &&
        name?.EndsWith(MouthSuffix, StringComparison.OrdinalIgnoreCase) == true &&
        x < SurfaceWidth;
}
