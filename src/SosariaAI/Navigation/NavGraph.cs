using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

public sealed class NavGraph
{
    private const string GateKeySeparator = "\n";

    /// <summary>
    /// Nearest-node lookups walk rings of sectors outward instead of sorting every node.
    /// A ring further out than the best distance found so far cannot hold a closer node.
    /// </summary>
    public const int SectorSize = 64;

    private readonly Dictionary<string, NavNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _neighbors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NavGateKind> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _gateEnds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _oneWay = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _padsOnly = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _component = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int X, int Y), List<NavNode>> _sectors = new();
    private NavNode[] _byIndex = [];
    private int[][] _neighborIds = [];
    private bool[][] _neighborGate = [];
    private bool[][] _closedOut = [];
    private bool[][] _closedIn = [];
    private int _minSectorX;
    private int _maxSectorX;
    private int _minSectorY;
    private int _maxSectorY;
    private bool _componentsReady;
    private int _largestComponent = -1;

    public NavGraph(string facet, IEnumerable<NavNode> nodes)
    {
        Facet = facet ?? string.Empty;

        if (nodes == null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            _nodes[node.Name] = node;
            _neighbors[node.Name] = [];
        }

        IndexSectors();

        foreach (var node in _nodes.Values)
        {
            var links = node.Connects;

            if (links == null)
            {
                continue;
            }

            for (var i = 0; i < links.Count; i++)
            {
                AddUndirected(node.Name, links[i]);
            }
        }

        foreach (var node in _nodes.Values)
        {
            var gates = node.Gates;

            if (gates == null)
            {
                continue;
            }

            for (var i = 0; i < gates.Count; i++)
            {
                var gate = gates[i];

                if (gate == null || string.IsNullOrWhiteSpace(gate.To))
                {
                    continue;
                }

                AddUndirected(node.Name, gate.To);
                IndexGate(node.Name, gate.To, gate.ParsedKind);
            }
        }

        IndexOneWayGates();
        IndexPadsOnly();
        BuildSearchIndex();
    }

    public string Facet { get; }

    public int NodeCount => _nodes.Count;

    public IReadOnlyCollection<NavNode> Nodes => _nodes.Values;

    public NavNode NodeAt(int index) => _byIndex[index];

    public int[] NeighborIds(int index) => _neighborIds[index];

    public bool[] NeighborGate(int index) => _neighborGate[index];

    /// <summary>
    /// Per neighbour of <paramref name="index"/>, true where the edge cannot be taken in the
    /// asked direction: out from this node, or (<paramref name="inbound"/>) in from the
    /// neighbour. A one-way gate closes an edge, and so does a walk off a pad no gate lands on.
    /// </summary>
    public bool[] NeighborClosed(int index, bool inbound) => inbound ? _closedIn[index] : _closedOut[index];

    /// <summary>
    /// True when a person can go from <paramref name="from"/> to <paramref name="to"/>: a
    /// walk, a two-way gate, or a one-way gate in its own direction. No walk leaves a pad that
    /// no gate lands on (<see cref="IndexPadsOnly"/>).
    /// </summary>
    public bool Travels(string from, string to) =>
        !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to) && !_oneWay.Contains(GateKey(to, from)) &&
        (!_padsOnly.Contains(from) || _gates.ContainsKey(GateKey(from, to)));

    public int SearchIndex(string name) =>
        TryGetNode(name, out var node) ? node.Index : -1;

    public bool TryGetNode(string name, out NavNode node)
    {
        node = null;
        return !string.IsNullOrWhiteSpace(name) && _nodes.TryGetValue(name, out node);
    }

    public IReadOnlyList<string> Neighbors(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_neighbors.TryGetValue(name, out var list))
        {
            return [];
        }

        return list;
    }

    public bool IsGate(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        return _gateEnds.TryGetValue(from, out var ends) && ends.Contains(to);
    }

    public NavGateKind GateKind(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return NavGateKind.None;
        }

        if (_gates.TryGetValue(GateKey(from, to), out var kind) ||
            _gates.TryGetValue(GateKey(to, from), out kind))
        {
            return kind;
        }

        return NavGateKind.None;
    }

    public int ComponentOf(string name)
    {
        EnsureComponents();
        return _component.TryGetValue(name, out var id) ? id : -1;
    }

    public bool SameComponent(string from, string to)
    {
        var a = ComponentOf(from);
        var b = ComponentOf(to);
        return a >= 0 && a == b;
    }

    /// <summary>
    /// The component holding the most nodes — the connected world a walk can reach.
    /// Tallied once inside <see cref="EnsureComponents"/>; spawn checks call this per
    /// tile, so a fresh scan per call priced a boot spawn in whole-graph walks.
    /// </summary>
    public int LargestComponent()
    {
        EnsureComponents();
        return _largestComponent;
    }

    public NavNode FindNearest(Point3D location)
    {
        var nearest = FindNearest(location, 1);
        return nearest.Count > 0 ? nearest[0] : null;
    }

    public IReadOnlyList<NavNode> FindNearest(Point3D location, int count)
    {
        if (count <= 0 || _nodes.Count == 0)
        {
            return [];
        }

        var scored = new List<(NavNode Node, double Cost)>();
        var centreX = SectorOf(location.X);
        var centreY = SectorOf(location.Y);
        var lastRing = LastRing(centreX, centreY);

        for (var ring = 0; ring <= lastRing; ring++)
        {
            scored.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));

            if (scored.Count >= count && RingFloor(ring) > scored[count - 1].Cost)
            {
                break;
            }

            ScoreRing(location, centreX, centreY, ring, scored);
        }

        scored.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));
        var take = Math.Min(count, scored.Count);
        var result = new NavNode[take];

        for (var i = 0; i < take; i++)
        {
            result[i] = scored[i].Node;
        }

        return result;
    }

    /// <summary>
    /// Components are cached on first use. Marking nodes indoor afterwards changes which
    /// nodes a search may pass through, so the cache must be dropped.
    /// </summary>
    public void InvalidateComponents()
    {
        _component.Clear();
        _componentsReady = false;
        _largestComponent = -1;
        EnsureComponents();
    }

    /// <summary>
    /// The walking links the boot log counts as notes: a link to the node itself, to a node the
    /// graph does not hold, or longer than an authored street leg (<see cref="NavLimits.SoftLegDistance"/>).
    /// Length is the leg cap's own tile measure (<see cref="NavMetric.WithinLegCap"/>), so every
    /// link past the cap is counted too. A gate is no walk and is never counted.
    /// </summary>
    public int CountValidationNotes()
    {
        var notes = 0;

        foreach (var node in _nodes.Values)
        {
            var links = node.Connects;

            if (links == null)
            {
                continue;
            }

            for (var i = 0; i < links.Count; i++)
            {
                var other = links[i];

                if (string.IsNullOrWhiteSpace(other))
                {
                    continue;
                }

                if (other.Equals(node.Name, StringComparison.OrdinalIgnoreCase) ||
                    !_nodes.TryGetValue(other, out var neighbor) ||
                    !IsGate(node.Name, other) &&
                    NavMetric.Chebyshev(node.Location, neighbor.Location) > NavLimits.SoftLegDistance)
                {
                    notes++;
                }
            }
        }

        return notes;
    }

    private void IndexSectors()
    {
        var first = true;

        foreach (var node in _nodes.Values)
        {
            var key = (SectorOf(node.X), SectorOf(node.Y));

            if (!_sectors.TryGetValue(key, out var list))
            {
                list = [];
                _sectors[key] = list;
            }

            list.Add(node);

            if (first)
            {
                _minSectorX = _maxSectorX = key.Item1;
                _minSectorY = _maxSectorY = key.Item2;
                first = false;
                continue;
            }

            _minSectorX = Math.Min(_minSectorX, key.Item1);
            _maxSectorX = Math.Max(_maxSectorX, key.Item1);
            _minSectorY = Math.Min(_minSectorY, key.Item2);
            _maxSectorY = Math.Max(_maxSectorY, key.Item2);
        }
    }

    /// <summary>The ring that reaches the furthest indexed sector from this centre.</summary>
    private int LastRing(int centreX, int centreY) =>
        Math.Max(
            Math.Max(Math.Abs(centreX - _minSectorX), Math.Abs(_maxSectorX - centreX)),
            Math.Max(Math.Abs(centreY - _minSectorY), Math.Abs(_maxSectorY - centreY))
        );

    /// <summary>
    /// The shortest planar distance from a point in the centre sector to any tile in a
    /// sector on this ring. Ring 0 is the centre sector itself. Every node cost is at
    /// least its planar distance, so a ring past this floor holds nothing closer.
    /// </summary>
    private static int RingFloor(int ring) => ring == 0 ? 0 : (ring - 1) * SectorSize + 1;

    private void ScoreRing(
        Point3D location,
        int centreX,
        int centreY,
        int ring,
        List<(NavNode Node, double Cost)> scored
    )
    {
        for (var dx = -ring; dx <= ring; dx++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                if (ring > 0 && Math.Abs(dx) != ring && Math.Abs(dy) != ring)
                {
                    continue;
                }

                if (!_sectors.TryGetValue((centreX + dx, centreY + dy), out var list))
                {
                    continue;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    scored.Add((list[i], NavMetric.Distance(location, list[i].Location)));
                }
            }
        }
    }

    private static int SectorOf(int value) => (int)Math.Floor((double)value / SectorSize);

    private void IndexGate(string from, string to, NavGateKind kind)
    {
        _gates[GateKey(from, to)] = kind;
        AddGateEnd(from, to);
        AddGateEnd(to, from);
    }

    private void AddGateEnd(string from, string to)
    {
        if (!_gateEnds.TryGetValue(from, out var ends))
        {
            ends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _gateEnds[from] = ends;
        }

        ends.Add(to);
    }

    private static string GateKey(string from, string to) => from + GateKeySeparator + to;

    /// <summary>
    /// The teleporter pads no gate sets a person down on. A person on such a pad walked onto
    /// it and was carried off at once, so no walk leaves it: a plan that walked across the pad
    /// at (1629,3320) to the street beyond was sent back into the Trinsic passage every time.
    /// A pad that is also a landing is left on foot as any landing is.
    /// </summary>
    private void IndexPadsOnly()
    {
        var landings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in _nodes.Values)
        {
            foreach (var gate in node.Gates ?? [])
            {
                if (!string.IsNullOrWhiteSpace(gate?.To))
                {
                    landings.Add(gate.To);
                }
            }
        }

        foreach (var node in _nodes.Values)
        {
            foreach (var gate in node.Gates ?? [])
            {
                if (gate?.ParsedKind == NavGateKind.Teleporter && !landings.Contains(node.Name))
                {
                    _padsOnly.Add(node.Name);
                    break;
                }
            }
        }
    }

    /// <summary>A gate listed on one end only carries a person that way alone.</summary>
    private void IndexOneWayGates()
    {
        foreach (var node in _nodes.Values)
        {
            var gates = node.Gates;

            for (var i = 0; i < (gates?.Count ?? 0); i++)
            {
                var to = gates[i]?.To;

                if (!string.IsNullOrWhiteSpace(to) && _nodes.TryGetValue(to, out var other) &&
                    NavGates.KindBetween(other, node.Name) == NavGateKind.None)
                {
                    _oneWay.Add(GateKey(node.Name, to));
                }
            }
        }
    }

    private void AddUndirected(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(to) ||
            from.Equals(to, StringComparison.OrdinalIgnoreCase) ||
            !_nodes.ContainsKey(to))
        {
            return;
        }

        AddOneWay(from, to);
        AddOneWay(to, from);
    }

    private void BuildSearchIndex()
    {
        _byIndex = new NavNode[_nodes.Count];
        var i = 0;

        foreach (var node in _nodes.Values)
        {
            node.Index = i;
            _byIndex[i] = node;
            i++;
        }

        _neighborIds = new int[_byIndex.Length][];
        _neighborGate = new bool[_byIndex.Length][];
        _closedOut = new bool[_byIndex.Length][];
        _closedIn = new bool[_byIndex.Length][];

        for (i = 0; i < _byIndex.Length; i++)
        {
            var name = _byIndex[i].Name;
            var names = Neighbors(name);
            var ids = new int[names.Count];
            var gates = new bool[names.Count];
            var closedOut = new bool[names.Count];
            var closedIn = new bool[names.Count];

            for (var n = 0; n < names.Count; n++)
            {
                ids[n] = SearchIndex(names[n]);
                gates[n] = IsGate(name, names[n]);
                closedOut[n] = !Travels(name, names[n]);
                closedIn[n] = !Travels(names[n], name);
            }

            _neighborIds[i] = ids;
            _neighborGate[i] = gates;
            _closedOut[i] = closedOut;
            _closedIn[i] = closedIn;
        }

        EnsureComponents();
    }

    private void AddOneWay(string from, string to)
    {
        var list = _neighbors[from];

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Equals(to, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(to);
    }

    /// <summary>
    /// Search never passes through an indoor node, so neither may a component. An indoor
    /// node joins the piece of the street it opens onto but does not carry that piece on
    /// to whatever else it links to. Otherwise a shop linked to two streets would report
    /// them as one piece while no route between them exists.
    /// </summary>
    private void EnsureComponents()
    {
        if (_componentsReady)
        {
            return;
        }

        var id = 0;

        foreach (var node in _nodes.Values)
        {
            if (!node.Indoor && Flood(node.Name, id, throughIndoor: false))
            {
                id++;
            }
        }

        foreach (var node in _nodes.Values)
        {
            if (Flood(node.Name, id, throughIndoor: true))
            {
                id++;
            }
        }

        var sizes = new Dictionary<int, int>();

        foreach (var node in _nodes.Values)
        {
            var nodeId = _component[node.Name];
            sizes[nodeId] = sizes.GetValueOrDefault(nodeId) + 1;
        }

        var main = -1;
        var mainSize = 0;

        foreach (var (componentId, size) in sizes)
        {
            if (size > mainSize)
            {
                mainSize = size;
                main = componentId;
            }
        }

        _largestComponent = main;
        _componentsReady = true;
    }

    private bool Flood(string root, int id, bool throughIndoor)
    {
        if (_component.ContainsKey(root))
        {
            return false;
        }

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (!_component.TryAdd(current, id))
            {
                continue;
            }

            if (!throughIndoor && _nodes.TryGetValue(current, out var here) && here.Indoor)
            {
                continue;
            }

            var next = Neighbors(current);

            for (var i = 0; i < next.Count; i++)
            {
                if (!_component.ContainsKey(next[i]))
                {
                    stack.Push(next[i]);
                }
            }
        }

        return true;
    }
}
