using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SosariaAI.Skills;

namespace SosariaAI.Navigation;

/// <summary>
/// The one-way pads of a facet's dungeons that a walker does not take: each drops it onto a
/// floor harder than the ground it leaves (<see cref="DungeonMap.Drops"/>) and above its reach,
/// the bar the crawl and the hall pick take a floor by (<see cref="DungeonCrawlRules.FloorFits"/>).
/// No pad leads straight back, so the walk crosses the floor it lands on: tamers and walkers
/// home took the Despise and Fire pads onto Destard's third level, a floor asking power 570.
/// A trip into a dungeon was picked by the same bar, so it keeps the pads onto its own floor.
/// The barred pads of one reach are one set, made once and shared by every walker of that
/// reach and by path workers, and never changed once handed out. World thread only.
/// </summary>
public static class FloorDrops
{
    private static readonly ConditionalWeakTable<DungeonMap, Ladder> ByFloors = new();

    /// <summary>The keys (<see cref="EdgeHealthRules.Key"/>) of the drops above the reach of <paramref name="power"/>, or null for none.</summary>
    public static IReadOnlySet<long> AboveReach(DungeonMap floors, int power) => LadderOf(floors).Pads(power);

    /// <summary>Bars on those drops alone (<see cref="AboveReach"/>), one object per set, or null for none.</summary>
    public static PathSearchBars BarsAboveReach(DungeonMap floors, int power) => LadderOf(floors).Bars(power);

    private static Ladder LadderOf(DungeonMap floors) => ByFloors.GetValue(floors, static map => new Ladder(map));

    /// <summary>
    /// The drops, hardest landing first: the drops above a reach are a run from the top, so each
    /// run's set and bars are made once.
    /// </summary>
    private sealed class Ladder
    {
        private readonly int[] _difficulty;
        private readonly long[] _keys;
        private readonly HashSet<long>[] _pads;
        private readonly PathSearchBars[] _bars;

        public Ladder(DungeonMap floors)
        {
            var drops = new List<(int Difficulty, long Key)>();

            foreach (var drop in floors.Drops)
            {
                if (floors.Floor(drop.ToFloor) is { } landing)
                {
                    drops.Add((landing.Difficulty, EdgeHealthRules.Key(drop.Pad.Index, drop.Landing.Index)));
                }
            }

            drops.Sort(static (a, b) => b.Difficulty.CompareTo(a.Difficulty));
            _difficulty = new int[drops.Count];
            _keys = new long[drops.Count];

            for (var i = 0; i < drops.Count; i++)
            {
                (_difficulty[i], _keys[i]) = drops[i];
            }

            _pads = new HashSet<long>[drops.Count + 1];
            _bars = new PathSearchBars[drops.Count + 1];
        }

        public IReadOnlySet<long> Pads(int power)
        {
            var count = CountAbove(power);
            return count == 0 ? null : PadsOf(count);
        }

        public PathSearchBars Bars(int power)
        {
            var count = CountAbove(power);

            return count == 0
                ? null
                : _bars[count] ??= new PathSearchBars(null, default, noMoongates: false, openAroundStart: false, pads: PadsOf(count));
        }

        private HashSet<long> PadsOf(int count) => _pads[count] ??= new HashSet<long>(new ArraySegment<long>(_keys, 0, count));

        /// <summary>How many drops land on a floor that does not fit <paramref name="power"/>.</summary>
        private int CountAbove(int power)
        {
            var count = 0;

            while (count < _difficulty.Length && !DungeonCrawlRules.FloorFits(_difficulty[count], power))
            {
                count++;
            }

            return count;
        }
    }
}
