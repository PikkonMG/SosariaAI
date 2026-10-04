using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Navigation;

/// <summary>
/// One walkable floor of a dungeon: the halls a person reaches on foot without a stair, its
/// real level as players number it, and how hard its spawn is (<see cref="AreaDifficulty.SpawnScore"/>).
/// </summary>
public readonly record struct DungeonFloor(string Dungeon, int Level, int Id, int Difficulty = 0);

/// <summary>A stair: the pad on this floor and the tile it lands on, on another floor of the same dungeon. A one-way pad counts only its own way.</summary>
public readonly record struct DungeonStair(NavNode Pad, NavNode Landing, int ToFloor);

/// <summary>A spot the world's own location list names "Level N" in a dungeon: the floor under it is level N.</summary>
public readonly record struct LevelMark(Point3D At, int Level);

/// <summary>
/// The dungeons of one facet, read from the generated navigation graph. A floor is a set of
/// dungeon nodes joined by walking edges; teleporter edges are the stairs between floors.
/// Rooms are the floor's plain nodes: pads are left out, since standing on one sends a player
/// through it. Nothing here is hand-made: the graph comes from the world's own regions and
/// teleporters, the levels from the world's own location list, the difficulty from its spawners.
///
/// A floor's level is the level players give it. The world's location list marks "Level 1" to
/// "Level 4" in most dungeons (<see cref="LevelMark"/>), and a marked floor takes that number.
/// A floor with no mark takes level 1 when a door or an exit joins it to the overland (the
/// Despise landing, the Terathan Keep gate house), else one level below the shallowest marked
/// floor a stair joins it to (the Shame gazer room, the Covetous lake). A dungeon with no
/// marks counts the stairs from the overland. Counting stairs alone was wrong: an exit pad made
/// Deceit's fourth level and Hythloth's third "level 1", every Covetous level has its own door,
/// and a crawl on Hythloth's balron floor went "down" to the easier second. A stair down lands
/// on a deeper level, a stair up on a shallower one; between two floors of one level, the one
/// more stairs from the overland is the deeper (the Despise landing above its first level, the
/// Shame gazer room above its fourth). Pure over the graph. No world objects.
/// </summary>
public sealed class DungeonMap
{
    /// <summary>A floor no stair or walk from the overland reaches. Nobody crawls it.</summary>
    public const int Unreached = 0;

    public const int FirstLevel = 1;

    /// <summary>A location named this and a number marks a dungeon level.</summary>
    public const string LevelMarkPrefix = "Level ";

    private readonly NavGraph _graph;
    private readonly Dictionary<string, int> _floorOf = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DungeonFloor> _floors = [];
    private readonly List<List<NavNode>> _rooms = [];
    private readonly List<List<DungeonStair>> _downs = [];
    private readonly List<List<DungeonStair>> _ups = [];
    private readonly List<DungeonStair> _drops = [];
    private int[] _hops = [];

    private DungeonMap(NavGraph graph) => _graph = graph;

    public static DungeonMap Empty { get; } = new(null);

    /// <summary>
    /// Builds the floors. <paramref name="dungeonOf"/> names the dungeon a node stands in, or
    /// null on the overland. <paramref name="marks"/> number the levels, and
    /// <paramref name="spawn"/> with <paramref name="lookup"/> rates each floor; without them a
    /// floor's level counts the stairs from the overland and its difficulty is zero.
    /// </summary>
    public static DungeonMap Build(
        NavGraph graph,
        Func<NavNode, string> dungeonOf,
        IReadOnlyList<LevelMark> marks = null,
        IReadOnlyList<SpawnPoint> spawn = null,
        Func<string, HostileStats?> lookup = null
    )
    {
        if (graph == null || dungeonOf == null)
        {
            return Empty;
        }

        var map = new DungeonMap(graph);
        var dungeonByNode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            var dungeon = dungeonOf(node);

            if (!string.IsNullOrWhiteSpace(dungeon))
            {
                dungeonByNode[node.Name] = dungeon;
            }
        }

        var floorDungeon = map.FloodFloors(dungeonByNode);
        var membersByFloor = map.MembersByFloor(floorDungeon.Count);
        var overland = map.OverlandFloors(dungeonByNode, floorDungeon.Count);
        var hops = map.HopLevels(floorDungeon, membersByFloor, overland);
        map._hops = hops;
        var levels = map.RealLevels(floorDungeon, membersByFloor, overland, hops, marks);
        var difficulty = map.FloorDifficulties(floorDungeon, hops, spawn, lookup);

        for (var id = 0; id < floorDungeon.Count; id++)
        {
            map._floors.Add(new DungeonFloor(floorDungeon[id], levels[id], id, difficulty[id]));
        }

        map.CollectRoomsAndStairs();
        map.CollectDrops();
        return map;
    }

    /// <summary>The level marks among the world's named locations: each "Level N" and where it stands. Pure.</summary>
    public static List<LevelMark> MarksIn(IReadOnlyList<NamedSeed> locations)
    {
        var marks = new List<LevelMark>();

        for (var i = 0; i < (locations?.Count ?? 0); i++)
        {
            var name = locations[i].Name;

            if (name != null && name.StartsWith(LevelMarkPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.AsSpan(LevelMarkPrefix.Length), out var level) && level >= FirstLevel)
            {
                marks.Add(new LevelMark(new Point3D(locations[i].X, locations[i].Y, locations[i].Z), level));
            }
        }

        return marks;
    }

    /// <summary>The floor under a node, or null off every reached floor.</summary>
    public DungeonFloor? FloorOfNode(string nodeName) =>
        nodeName != null && _floorOf.TryGetValue(nodeName, out var id) && _floors[id].Level != Unreached
            ? _floors[id]
            : null;

    /// <summary>The floor a point stands on: the floor of the nearest node within one leg.</summary>
    public DungeonFloor? FloorAt(Point3D at) => FloorOfNode(NodeAt(at));

    /// <summary>The floor with this id, or null for none.</summary>
    public DungeonFloor? Floor(int floorId) =>
        floorId >= 0 && floorId < _floors.Count && _floors[floorId].Level != Unreached ? _floors[floorId] : null;

    public IReadOnlyList<NavNode> Rooms(int floorId) =>
        floorId >= 0 && floorId < _rooms.Count ? _rooms[floorId] : [];

    /// <summary>The stairs from this floor to a deeper floor of its dungeon.</summary>
    public IReadOnlyList<DungeonStair> StairsDown(int floorId) =>
        floorId >= 0 && floorId < _downs.Count ? _downs[floorId] : [];

    /// <summary>The stairs from this floor to a shallower floor of its dungeon.</summary>
    public IReadOnlyList<DungeonStair> StairsUp(int floorId) =>
        floorId >= 0 && floorId < _ups.Count ? _ups[floorId] : [];

    /// <summary>
    /// The one-way pads that set a person down on a floor harder than the ground the pad stands
    /// on (the overland counts as none), other than a stair up its own dungeon: the pads of
    /// Despise's third level and of Fire onto Destard's third, the Lost Lands pads onto the
    /// Trinsic Passage. No pad leads straight back, so a walk that takes one crosses that
    /// floor to get anywhere. Sorted by pad name, then landing.
    /// </summary>
    public IReadOnlyList<DungeonStair> Drops => _drops;

    /// <summary>True when these floors were read off <paramref name="graph"/>, so its node indexes name their pads.</summary>
    public bool IsOf(NavGraph graph) => graph != null && ReferenceEquals(_graph, graph);

    /// <summary>Every reached floor, for reports.</summary>
    public IEnumerable<DungeonFloor> Floors
    {
        get
        {
            for (var i = 0; i < _floors.Count; i++)
            {
                if (_floors[i].Level != Unreached)
                {
                    yield return _floors[i];
                }
            }
        }
    }

    private string NodeAt(Point3D at)
    {
        var nearest = _graph?.FindNearest(at);

        return nearest != null && NavMetric.WithinLegCap(at, nearest.Location) && NavMetric.SameFloor(nearest.Location, at)
            ? nearest.Name
            : null;
    }

    private List<string> FloodFloors(Dictionary<string, string> dungeonByNode)
    {
        var floorDungeon = new List<string>();
        var stack = new Stack<string>();

        foreach (var (name, dungeon) in dungeonByNode)
        {
            if (_floorOf.ContainsKey(name))
            {
                continue;
            }

            var id = floorDungeon.Count;
            floorDungeon.Add(dungeon);
            stack.Push(name);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (!_floorOf.TryAdd(current, id))
                {
                    continue;
                }

                var next = _graph.Neighbors(current);

                for (var i = 0; i < next.Count; i++)
                {
                    var other = next[i];

                    if (!_floorOf.ContainsKey(other) &&
                        !_graph.IsGate(current, other) &&
                        dungeonByNode.TryGetValue(other, out var otherDungeon) &&
                        string.Equals(otherDungeon, dungeon, StringComparison.OrdinalIgnoreCase))
                    {
                        stack.Push(other);
                    }
                }
            }
        }

        return floorDungeon;
    }

    /// <summary>The floors with an edge, stair or walk, out to the overland: a door or an exit.</summary>
    private bool[] OverlandFloors(Dictionary<string, string> dungeonByNode, int count)
    {
        var overland = new bool[count];

        foreach (var (name, id) in _floorOf)
        {
            if (overland[id])
            {
                continue;
            }

            var next = _graph.Neighbors(name);

            for (var i = 0; i < next.Count && !overland[id]; i++)
            {
                overland[id] = !dungeonByNode.ContainsKey(next[i]);
            }
        }

        return overland;
    }

    /// <summary>
    /// Stairs from the overland: a floor with a door or an exit is 1, and each stair of the
    /// same dungeon from there adds one. A floor no stair reaches stays <see cref="Unreached"/>.
    /// </summary>
    private int[] HopLevels(List<string> floorDungeon, List<List<string>> membersByFloor, bool[] overland)
    {
        var levels = new int[floorDungeon.Count];
        var queue = new Queue<int>();

        for (var id = 0; id < levels.Length; id++)
        {
            if (overland[id])
            {
                levels[id] = FirstLevel;
                queue.Enqueue(id);
            }
        }

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();

            foreach (var otherId in StairNeighbors(id, floorDungeon, membersByFloor))
            {
                if (levels[otherId] == Unreached)
                {
                    levels[otherId] = levels[id] + 1;
                    queue.Enqueue(otherId);
                }
            }
        }

        return levels;
    }

    /// <summary>
    /// The level players give each reached floor: the lowest mark on it; for an unmarked floor of
    /// a marked dungeon, 1 with a door or an exit, else one below the shallowest numbered floor a
    /// stair joins it to; for a dungeon with no marks, its stairs from the overland.
    /// </summary>
    private int[] RealLevels(
        List<string> floorDungeon,
        List<List<string>> membersByFloor,
        bool[] overland,
        int[] hops,
        IReadOnlyList<LevelMark> marks
    )
    {
        var levels = new int[floorDungeon.Count];
        var marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < (marks?.Count ?? 0); i++)
        {
            if (!_floorOf.TryGetValue(NodeAt(marks[i].At) ?? string.Empty, out var id) || hops[id] == Unreached)
            {
                continue;
            }

            levels[id] = levels[id] == Unreached ? marks[i].Level : Math.Min(levels[id], marks[i].Level);
            marked.Add(floorDungeon[id]);
        }

        var numbered = new PriorityQueue<int, int>();

        for (var id = 0; id < levels.Length; id++)
        {
            if (hops[id] == Unreached || !marked.Contains(floorDungeon[id]))
            {
                levels[id] = hops[id];
                continue;
            }

            if (levels[id] == Unreached && overland[id])
            {
                levels[id] = FirstLevel;
            }

            if (levels[id] != Unreached)
            {
                numbered.Enqueue(id, levels[id]);
            }
        }

        // Shallowest first, so an unmarked floor between two marked ones hangs below the upper.
        while (numbered.TryDequeue(out var id, out _))
        {
            foreach (var otherId in StairNeighbors(id, floorDungeon, membersByFloor))
            {
                if (levels[otherId] == Unreached && hops[otherId] != Unreached)
                {
                    levels[otherId] = levels[id] + 1;
                    numbered.Enqueue(otherId, levels[otherId]);
                }
            }
        }

        // A marked dungeon's floor joined to no numbered floor keeps its stairs from the overland.
        for (var id = 0; id < levels.Length; id++)
        {
            if (levels[id] == Unreached)
            {
                levels[id] = hops[id];
            }
        }

        return levels;
    }

    /// <summary>The other floors of the same dungeon a stair joins this one to, either way.</summary>
    private IEnumerable<int> StairNeighbors(int id, List<string> floorDungeon, List<List<string>> membersByFloor)
    {
        var members = membersByFloor[id];

        for (var m = 0; m < members.Count; m++)
        {
            var next = _graph.Neighbors(members[m]);

            for (var i = 0; i < next.Count; i++)
            {
                if (_floorOf.TryGetValue(next[i], out var otherId) && otherId != id &&
                    string.Equals(floorDungeon[otherId], floorDungeon[id], StringComparison.OrdinalIgnoreCase))
                {
                    yield return otherId;
                }
            }
        }
    }

    /// <summary>Each reached floor scored by the spawn standing on it, spawn of its own dungeon only.</summary>
    private int[] FloorDifficulties(
        List<string> floorDungeon,
        int[] hops,
        IReadOnlyList<SpawnPoint> spawn,
        Func<string, HostileStats?> lookup
    )
    {
        var difficulty = new int[floorDungeon.Count];
        var byFloor = new Dictionary<int, List<SpawnCount>>();

        for (var i = 0; i < (spawn?.Count ?? 0); i++)
        {
            var point = spawn[i];

            if (point.Place == null || !_floorOf.TryGetValue(NodeAt(point.At) ?? string.Empty, out var id) ||
                hops[id] == Unreached || !string.Equals(floorDungeon[id], point.Place, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!byFloor.TryGetValue(id, out var counts))
            {
                counts = [];
                byFloor[id] = counts;
            }

            counts.Add(new SpawnCount(point.Creature, point.Count));
        }

        foreach (var (id, counts) in byFloor)
        {
            difficulty[id] = AreaDifficulty.SpawnScore(counts, lookup);
        }

        return difficulty;
    }

    /// <summary>Above zero when floor <paramref name="a"/> lies deeper than <paramref name="b"/>: by level, then by stairs from the overland.</summary>
    private int DepthOrder(int a, int b) =>
        _floors[a].Level != _floors[b].Level ? _floors[a].Level.CompareTo(_floors[b].Level) : _hops[a].CompareTo(_hops[b]);

    private List<List<string>> MembersByFloor(int count)
    {
        var members = new List<List<string>>(count);

        for (var i = 0; i < count; i++)
        {
            members.Add([]);
        }

        foreach (var (name, id) in _floorOf)
        {
            members[id].Add(name);
        }

        // Dictionary order is not stable across builds; the level walk must not depend on it.
        for (var i = 0; i < count; i++)
        {
            members[i].Sort(StringComparer.Ordinal);
        }

        return members;
    }

    /// <summary>See <see cref="Drops"/>. A pad onto its own floor is a shortcut across it, no drop.</summary>
    private void CollectDrops()
    {
        foreach (var pad in _graph.Nodes)
        {
            var next = _graph.Neighbors(pad.Name);

            for (var i = 0; i < next.Count; i++)
            {
                if (!_graph.IsGate(pad.Name, next[i]) || !_graph.Travels(pad.Name, next[i]) || _graph.Travels(next[i], pad.Name) ||
                    !_floorOf.TryGetValue(next[i], out var toId) || _floors[toId].Level == Unreached ||
                    !_graph.TryGetNode(next[i], out var landing))
                {
                    continue;
                }

                var onFloor = _floorOf.TryGetValue(pad.Name, out var fromId);

                if (onFloor && IsStairUpOrAcross(fromId, toId) ||
                    _floors[toId].Difficulty <= (onFloor ? _floors[fromId].Difficulty : 0))
                {
                    continue;
                }

                _drops.Add(new DungeonStair(pad, landing, toId));
            }
        }

        // Dictionary order is not stable across builds.
        _drops.Sort(static (a, b) =>
            {
                var byPad = string.CompareOrdinal(a.Pad.Name, b.Pad.Name);
                return byPad != 0 ? byPad : string.CompareOrdinal(a.Landing.Name, b.Landing.Name);
            }
        );
    }

    /// <summary>True when a pad on floor <paramref name="fromId"/> lands on the same floor, or on a shallower floor of its dungeon.</summary>
    private bool IsStairUpOrAcross(int fromId, int toId) =>
        fromId == toId ||
        string.Equals(_floors[fromId].Dungeon, _floors[toId].Dungeon, StringComparison.OrdinalIgnoreCase) &&
        _floors[fromId].Level != Unreached && DepthOrder(toId, fromId) < 0;

    private void CollectRoomsAndStairs()
    {
        for (var i = 0; i < _floors.Count; i++)
        {
            _rooms.Add([]);
            _downs.Add([]);
            _ups.Add([]);
        }

        foreach (var (name, id) in _floorOf)
        {
            if (_floors[id].Level == Unreached || !_graph.TryGetNode(name, out var node))
            {
                continue;
            }

            var isPad = false;
            var next = _graph.Neighbors(name);

            for (var i = 0; i < next.Count; i++)
            {
                if (!_graph.IsGate(name, next[i]))
                {
                    continue;
                }

                isPad = true;

                if (_graph.Travels(name, next[i]) &&
                    _floorOf.TryGetValue(next[i], out var toId) &&
                    toId != id &&
                    _floors[toId].Level != Unreached &&
                    string.Equals(_floors[toId].Dungeon, _floors[id].Dungeon, StringComparison.OrdinalIgnoreCase) &&
                    _graph.TryGetNode(next[i], out var landing) &&
                    DepthOrder(toId, id) is var order && order != 0)
                {
                    (order > 0 ? _downs : _ups)[id].Add(new DungeonStair(node, landing, toId));
                }
            }

            if (!isPad)
            {
                _rooms[id].Add(node);
            }
        }

        // Dictionary order is not stable across builds; seeded picks need a fixed order.
        for (var i = 0; i < _rooms.Count; i++)
        {
            _rooms[i].Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            _downs[i].Sort(static (a, b) => string.CompareOrdinal(a.Pad.Name, b.Pad.Name));
            _ups[i].Sort(static (a, b) => string.CompareOrdinal(a.Pad.Name, b.Pad.Name));
        }
    }
}
