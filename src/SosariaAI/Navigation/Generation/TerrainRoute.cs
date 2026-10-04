using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Joins graph pieces by following the ground, not by drawing a straight line across it.
///
/// A straight line between two places almost never works in a built world. Measured on
/// Felucca: the line from the Britain bank to the Britain forest fails after 33 tiles
/// because a building stands in the way, and every longer pair fails as well. A walk over
/// the real tiles from that same bank reaches the forest, the graveyard, the Despise
/// door, Minoc and Trinsic among many others.
///
/// So this class walks the tiles. It starts from every node in the largest piece of the
/// graph at once and spreads out cell by cell. Each move into the next cell is a real
/// walk of single tile steps with the walker's step rule, out and back again, so a wall
/// seam, a cliff or a drop stops it. It remembers the tile and height it reached in each
/// cell, so any node it reaches can trace the way back. Road nodes are then laid on that
/// way so that every road edge is a straight walk both ways with the walker's step rule,
/// the walk travel proves, with a node at each bend. A road that meets an earlier road
/// joins it there.
///
/// Both ways matter. A road is laid from the stranded end back toward the source, the
/// opposite way to the spread, and a straight line walked back does not cross the same
/// tiles as the line walked out. Measured on Felucca with one-way moves: the spread came
/// within two tiles of the Despise, Destard, Shame and Wrong doors, and every road back
/// failed on a move that only walked outward, so those doors stayed off the main graph.
///
/// Last, every piece walks out at once and pieces whose walks meet are joined there. The
/// passes above only join a piece to the ground that travel from the largest piece already
/// reaches, so ground that nothing leads into stayed in pieces. Measured on the Felucca
/// tiles: the Deceit island is one walk of 83,479 tiles that holds the Deceit door, the
/// Honesty shrine and three graph pieces; the Valor island is one walk of 15,612 tiles
/// with two pieces; the Fire dungeon's exit pads stand on the same walk as its upper level
/// yet were a piece of their own; the lower Trinsic passage, the way into the Lost Lands,
/// was three pieces on one walk. Each gate kind counts as a way between pieces there, so a
/// pad or a one-way pad that leads between them hid the gap.
/// </summary>
public static class TerrainRoute
{
    /// <summary>Tiles between cells. Each move between cells is walked tile by tile.</summary>
    public const int GridStep = 4;

    /// <summary>Upper bound on cells visited, so a huge facet cannot stall the boot.</summary>
    public const int MaxCells = 600_000;

    /// <summary>Longest road edge. A road node stands at least this often.</summary>
    public const int RoadSpacing = NavLimits.SoftLegDistance;

    /// <summary>How far from a node we look for a cell the walk reached, and how far apart two walks may stop and still be joined.</summary>
    public const int AttachRadiusCells = 3;

    /// <summary>
    /// Target tiles tried in the next cell. The same spot one cell over comes first, then
    /// the tiles beside it, so a gate or a doorway off the line is still found.
    /// </summary>
    public const int MaxEntryTries = 4;

    /// <summary>
    /// Each pass joins pieces to the largest one, which makes the largest one bigger and
    /// opens ground the next pass can start from. Teleporters are the reason: joining a
    /// dungeon gate brings its far side in, and the next walk begins inside the dungeon.
    /// </summary>
    public const int MaxPasses = 8;

    public const string RoadNamePrefix = "road";

    /// <summary>
    /// Furthest a piece the spread missed may lie from the joined ground for a tile walk to
    /// join it: one pathfinder leg. Further pieces are islands or dungeon floors.
    /// </summary>
    public const int TileTrailReach = NavLimits.MaxLegDistance;

    /// <summary>Nearest node pairs a missed piece tries on tiles, once per build.</summary>
    public const int TileTrailTries = 2;

    /// <summary>Tile walks two pieces try across the spots where their walks meet and no straight walk crosses.</summary>
    public const int MeetTrailTries = 2;

    /// <summary>Tiles per side of a square of the source index the tile walk looks up its nearest node in.</summary>
    private const int SourceSector = 64;

    /// <summary>Sideways shifts from the straight target, nearest first.</summary>
    private static readonly int[] EntryShifts = [0, 1, -1, 2, -2, 3, -3];

    /// <summary>
    /// Lays roads from the largest piece of the graph to every other piece the ground
    /// actually connects to, then between the pieces left whose walks meet. Returns how many
    /// pieces were joined.
    /// </summary>
    /// <param name="walker">How the walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    /// <param name="keep">
    /// Where a road may run, such as <see cref="WalkLine.Outdoors"/>, or null for anywhere
    /// the walker steps.
    /// </param>
    public static int ConnectComponents(
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep
    )
    {
        if (nodes == null || nodes.Count == 0 || walker == null)
        {
            return 0;
        }

        var joined = 0;
        var tileTried = new HashSet<(string From, string To)>();
        var pass = 0;

        for (; pass < MaxPasses; pass++)
        {
            var added = ConnectOnce(nodes, walker, keep, pass, tileTried);

            if (added == 0)
            {
                break;
            }

            joined += added;
        }

        return joined + JoinWhereWalksMeet(nodes, walker, keep, pass);
    }

    private static int ConnectOnce(
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        int pass,
        HashSet<(string From, string To)> tileTried
    )
    {
        var componentOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var largest = LargestGroundComponent(nodes, componentOf);

        if (largest < 0)
        {
            return 0;
        }

        // A gate pad that travel already reaches is open ground for the walk: an island
        // moongate stands in its own piece yet must seed the flood, or the town around
        // it stays stranded on the far side of water the walk cannot cross.
        var reached = GateReach(nodes, componentOf, largest);
        var sources = new List<NavNode>();
        var stranded = new List<NavNode>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            // A road must not start or end inside a shop. Search never passes through a
            // shop, so a road anchored on one would lead nowhere and leave its piece
            // stranded.
            if (string.IsNullOrWhiteSpace(node.Name) || node.Indoor)
            {
                continue;
            }

            (reached.Contains(node.Name) ? sources : stranded).Add(node);
        }

        if (sources.Count == 0 || stranded.Count == 0)
        {
            return 0;
        }

        var visits = new Dictionary<Cell, Visit>();
        Spread(visits, sources, walker, keep);
        var roadAt = new Dictionary<(int X, int Y), NavNode>();
        // One road joins a whole piece, so each piece is only worth laying once.
        var done = new HashSet<int> { largest };
        var joined = 0;

        for (var i = 0; i < stranded.Count; i++)
        {
            var node = stranded[i];
            var piece = componentOf[node.Name];

            // A piece is only finished once a road actually reached it. Marking it done on
            // a node that could not attach would strand the whole piece: a dungeon gate and
            // its far side are one piece, and only the gate end stands on open ground.
            if (done.Contains(piece) || !TryLayRoad(node, visits, sources, roadAt, nodes, walker, keep, pass))
            {
                continue;
            }

            done.Add(piece);
            joined++;
        }

        return joined + JoinByTileTrail(stranded, sources, componentOf, done, roadAt, nodes, walker, keep, pass, tileTried);
    }

    /// <summary>
    /// Pieces the spread did not reach, joined along a tile walk to the nearest node of the
    /// joined ground. The spread moves four tiles at a time on near-straight lines, and a
    /// town gate that bends between two walls turns it back: Cove stood apart from the
    /// world, and every walk home there had no route. A tile walk threads the bend. Each
    /// piece tries its nearest few pairs, each pair once per build.
    /// </summary>
    private static int JoinByTileTrail(
        List<NavNode> stranded,
        List<NavNode> sources,
        Dictionary<string, int> componentOf,
        HashSet<int> done,
        Dictionary<(int X, int Y), NavNode> roadAt,
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        int pass,
        HashSet<(string From, string To)> tileTried
    )
    {
        var index = IndexSources(sources);
        var pairsByPiece = new Dictionary<int, List<(int Distance, NavNode From, NavNode To)>>();

        for (var i = 0; i < stranded.Count; i++)
        {
            var node = stranded[i];
            var piece = componentOf[node.Name];

            if (done.Contains(piece) || NearestSource(node, index) is not { } source)
            {
                continue;
            }

            if (!pairsByPiece.TryGetValue(piece, out var pairs))
            {
                pairsByPiece[piece] = pairs = [];
            }

            pairs.Add((NavMetric.Chebyshev(node.Location, source.Location), node, source));
        }

        var isIndoor = Roofed(keep);
        var joined = 0;

        foreach (var (piece, pairs) in pairsByPiece)
        {
            pairs.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
            var tries = 0;

            for (var i = 0; i < pairs.Count && tries < TileTrailTries; i++)
            {
                var (_, from, to) = pairs[i];

                if (!tileTried.Add((from.Name, to.Name)))
                {
                    continue;
                }

                tries++;
                var trail = TileRoute.Trail(from.Location, to.Location, walker, isIndoor);
                var stops = PlanStops(from.Location, trail, at => roadAt.ContainsKey((at.X, at.Y)), walker, keep, out var end);

                if (stops == null)
                {
                    continue;
                }

                LayRoad(from, trail, stops, end, to, roadAt, nodes, pass);
                done.Add(piece);
                joined++;
                break;
            }
        }

        return joined;
    }

    private static Dictionary<(int X, int Y), List<NavNode>> IndexSources(List<NavNode> sources)
    {
        var index = new Dictionary<(int X, int Y), List<NavNode>>();

        for (var i = 0; i < sources.Count; i++)
        {
            var key = (sources[i].X / SourceSector, sources[i].Y / SourceSector);

            if (!index.TryGetValue(key, out var list))
            {
                index[key] = list = [];
            }

            list.Add(sources[i]);
        }

        return index;
    }

    /// <summary>The joined-ground node nearest to <paramref name="node"/> within <see cref="TileTrailReach"/>, or null.</summary>
    private static NavNode NearestSource(NavNode node, Dictionary<(int X, int Y), List<NavNode>> index)
    {
        NavNode best = null;
        var bestDistance = TileTrailReach + 1;
        var sectorX = node.X / SourceSector;
        var sectorY = node.Y / SourceSector;

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!index.TryGetValue((sectorX + dx, sectorY + dy), out var list))
                {
                    continue;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    var distance = NavMetric.Chebyshev(node.Location, list[i].Location);

                    if (distance < bestDistance)
                    {
                        best = list[i];
                        bestDistance = distance;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Walks outward from every source at once, one cell at a time, into
    /// <paramref name="visits"/>. The first source in a cell holds it.
    /// </summary>
    private static void Spread(
        Dictionary<Cell, Visit> visits,
        IReadOnlyList<NavNode> sources,
        TileWalker walker,
        Func<int, int, int, bool> keep
    )
    {
        var queue = new Queue<Cell>();

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var cell = ToCell(source.X, source.Y);

            if (visits.ContainsKey(cell))
            {
                continue;
            }

            // Remember the real node here. The road must end on it, not beside it.
            var z = walker.FloorNear(source.X, source.Y, source.Z) ?? source.Z;
            visits[cell] = new Visit(cell, new Point3D(source.X, source.Y, z), i);
            queue.Enqueue(cell);
        }

        while (queue.Count > 0 && visits.Count < MaxCells)
        {
            var current = queue.Dequeue();
            var visit = visits[current];

            foreach (var (dx, dy) in TileGrid.Neighbours)
            {
                var next = new Cell(current.X + dx * GridStep, current.Y + dy * GridStep);

                if (next.X < 0 || next.Y < 0 || visits.ContainsKey(next))
                {
                    continue;
                }

                if (TryEnter(visit.Tile, dx, dy, next, walker, keep, out var entered))
                {
                    visits[next] = new Visit(current, entered, visit.Source);
                    queue.Enqueue(next);
                }
            }
        }
    }

    /// <summary>
    /// Joins the pieces that share ground, wherever their walks meet or come close. Every
    /// outdoor node walks out at once, each cell held by the first walk to reach it. Where a
    /// cell of one piece's walk lies within <see cref="AttachRadiusCells"/> of a cell of
    /// another's, nearest tiles first, a road runs from the one piece's node out along its
    /// walk, across to the other walk, and back to the other piece's node. The two tiles are
    /// crossed by a straight walk both ways, or else by a tile walk while the pair has
    /// <see cref="MeetTrailTries"/> left. A tile walk threads what the four-tile moves of the
    /// walk cannot: the Fire dungeon's exit pads stand in a room whose door bends off every
    /// straight line, and the Honesty shrine stands on a hill whose paths are two tiles wide.
    /// Only walking links make a piece here, so a gate between two pieces that share ground
    /// does not hide the missing road. Two pieces whose first nodes share a cell are joined by
    /// one edge when it walks both ways.
    /// </summary>
    private static int JoinWhereWalksMeet(
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        int pass
    )
    {
        var pieceOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pieces = GroundPieces(nodes, pieceOf, WalksBetween).Count;
        var joinedTo = new int[pieces];

        for (var i = 0; i < pieces; i++)
        {
            joinedTo[i] = i;
        }

        var joined = 0;
        var seeds = new List<NavNode>();
        var seedAt = new Dictionary<Cell, NavNode>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (string.IsNullOrWhiteSpace(node.Name) || node.Indoor)
            {
                continue;
            }

            var cell = ToCell(node.X, node.Y);

            if (!seedAt.TryGetValue(cell, out var first))
            {
                seedAt[cell] = node;
                seeds.Add(node);
                continue;
            }

            var a = GraphConnect.FindRoot(joinedTo, pieceOf[first.Name]);
            var b = GraphConnect.FindRoot(joinedTo, pieceOf[node.Name]);

            if (a != b && InLine(first.Location, node.Location, walker, keep))
            {
                GraphConnect.LinkForRoad(first, node);
                GraphConnect.Union(joinedTo, a, b);
                joined++;
            }
        }

        var seedPiece = new int[seeds.Count];

        for (var i = 0; i < seeds.Count; i++)
        {
            seedPiece[i] = pieceOf[seeds[i].Name];
        }

        var visits = new Dictionary<Cell, Visit>();
        Spread(visits, seeds, walker, keep);

        var roadAt = new Dictionary<(int X, int Y), NavNode>();
        var trailTries = new Dictionary<(int, int), int>();
        var isIndoor = Roofed(keep);

        foreach (var (_, here, there) in Meetings(visits, seedPiece, joinedTo))
        {
            var from = seeds[visits[here].Source];
            var to = seeds[visits[there].Source];
            var a = GraphConnect.FindRoot(joinedTo, seedPiece[visits[here].Source]);
            var b = GraphConnect.FindRoot(joinedTo, seedPiece[visits[there].Source]);

            if (a == b ||
                Crossing(visits[here].Tile, visits[there].Tile, (Math.Min(a, b), Math.Max(a, b)), trailTries, walker, keep, isIndoor) is not { } crossing ||
                !TryLayMeetingRoad(from, here, crossing, to, there, visits, roadAt, nodes, walker, keep, pass))
            {
                continue;
            }

            GraphConnect.Union(joinedTo, a, b);
            joined++;
        }

        return joined;
    }

    /// <summary>
    /// Pairs of reached cells within <see cref="AttachRadiusCells"/> of each other whose walks
    /// belong to pieces still apart, nearest tiles first. Only a cell at the edge of its own
    /// piece's walk can lie near another's, so the cells inside are skipped.
    /// </summary>
    private static List<(int Distance, Cell Here, Cell There)> Meetings(
        Dictionary<Cell, Visit> visits,
        int[] seedPiece,
        int[] joinedTo
    )
    {
        var meetings = new List<(int Distance, Cell Here, Cell There)>();

        foreach (var (cell, visit) in visits)
        {
            var piece = GraphConnect.FindRoot(joinedTo, seedPiece[visit.Source]);

            if (!AtEdge(cell, piece, visits, seedPiece, joinedTo))
            {
                continue;
            }

            for (var dx = -AttachRadiusCells; dx <= AttachRadiusCells; dx++)
            {
                for (var dy = -AttachRadiusCells; dy <= AttachRadiusCells; dy++)
                {
                    var near = new Cell(cell.X + dx * GridStep, cell.Y + dy * GridStep);

                    if (visits.TryGetValue(near, out var other) &&
                        GraphConnect.FindRoot(joinedTo, seedPiece[other.Source]) != piece)
                    {
                        meetings.Add((NavMetric.Chebyshev(visit.Tile, other.Tile), cell, near));
                    }
                }
            }
        }

        meetings.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        return meetings;
    }

    /// <summary>True when a cell next to <paramref name="cell"/> is not held by a walk of <paramref name="piece"/>.</summary>
    private static bool AtEdge(Cell cell, int piece, Dictionary<Cell, Visit> visits, int[] seedPiece, int[] joinedTo)
    {
        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            if (!visits.TryGetValue(new Cell(cell.X + dx * GridStep, cell.Y + dy * GridStep), out var next) ||
                GraphConnect.FindRoot(joinedTo, seedPiece[next.Source]) != piece)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The tiles from one meeting tile to the other, without the first: the other alone when
    /// a straight walk crosses both ways, else a tile walk while the pair has tries left.
    /// Null when neither crosses.
    /// </summary>
    private static IReadOnlyList<Point3D> Crossing(
        Point3D here,
        Point3D there,
        (int, int) pair,
        Dictionary<(int, int), int> trailTries,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        Func<int, int, int, bool> isIndoor
    )
    {
        if (InLine(here, there, walker, keep))
        {
            return [there];
        }

        var tries = trailTries.GetValueOrDefault(pair);

        if (tries >= MeetTrailTries)
        {
            return null;
        }

        trailTries[pair] = tries + 1;
        var trail = TileRoute.Trail(here, there, walker, isIndoor);
        return trail.Count > 0 ? trail : null;
    }

    /// <summary>
    /// The road from <paramref name="from"/> along its walk out to <paramref name="here"/>,
    /// over <paramref name="crossing"/>, and back along the other walk from
    /// <paramref name="there"/> to <paramref name="to"/>. Nothing is added unless the whole
    /// road walks both ways.
    /// </summary>
    private static bool TryLayMeetingRoad(
        NavNode from,
        Cell here,
        IReadOnlyList<Point3D> crossing,
        NavNode to,
        Cell there,
        Dictionary<Cell, Visit> visits,
        Dictionary<(int X, int Y), NavNode> roadAt,
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        int pass
    )
    {
        // The walk out, turned round to run from the node, without the node's own tile.
        var trail = Trail(here, visits);
        trail.RemoveAt(trail.Count - 1);
        trail.Reverse();
        trail.AddRange(crossing);
        // The crossing ends on the other walk's first tile, or the road plan refuses it.
        var back = Trail(there, visits);
        trail.AddRange(back.GetRange(1, back.Count - 1));
        var stops = PlanStops(from.Location, trail, joinsRoad: null, walker, keep, out var end);

        if (stops == null)
        {
            return false;
        }

        LayRoad(from, trail, stops, end, to, roadAt, nodes, pass);
        return true;
    }

    /// <summary>The roof test a keep test stands for: true where the keep test refuses a tile. Null for no keep test.</summary>
    private static Func<int, int, int, bool> Roofed(Func<int, int, int, bool> keep) =>
        keep == null ? null : (x, y, z) => !keep(x, y, z);

    /// <summary>
    /// Walks from a tile into the next cell over, tile by tile, trying the same spot one
    /// cell over first and then the tiles beside it. The tile entered must walk back to
    /// the tile it came from.
    /// </summary>
    private static bool TryEnter(
        Point3D from,
        int dx,
        int dy,
        Cell target,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        out Point3D entered
    )
    {
        var aimX = from.X + dx * GridStep;
        var aimY = from.Y + dy * GridStep;
        // Sideways to the move: across a straight move, along the other diagonal of a
        // diagonal one.
        var (sideX, sideY) = dx == 0 ? (1, 0) : dy == 0 ? (0, 1) : (dx, -dy);
        var tries = 0;

        for (var i = 0; i < EntryShifts.Length && tries < MaxEntryTries; i++)
        {
            var x = aimX + sideX * EntryShifts[i];
            var y = aimY + sideY * EntryShifts[i];

            if (x < 0 || y < 0 || ToCell(x, y) != target)
            {
                continue;
            }

            tries++;

            // The road laid on this move is walked back toward the source, and travel
            // uses it both ways, so the move must walk back as well as out.
            if (WalkLine.TryWalk(walker, from.X, from.Y, from.Z, x, y, keep, out var z) &&
                WalkLine.Reaches(walker, new Point3D(x, y, z), from, keep))
            {
                entered = new Point3D(x, y, z);
                return true;
            }
        }

        entered = default;
        return false;
    }

    /// <summary>
    /// Lays the road from a stranded node back to the walk's source, from the nearest
    /// reached cell that works. Nothing is added unless the whole road walks.
    /// </summary>
    private static bool TryLayRoad(
        NavNode stranded,
        Dictionary<Cell, Visit> visits,
        List<NavNode> sources,
        Dictionary<(int X, int Y), NavNode> roadAt,
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        int pass
    )
    {
        foreach (var cell in AttachCells(stranded, visits))
        {
            var trail = Trail(cell, visits);
            var stops = PlanStops(stranded.Location, trail, at => roadAt.ContainsKey((at.X, at.Y)), walker, keep, out var end);

            if (stops == null)
            {
                continue;
            }

            LayRoad(stranded, trail, stops, end, sources[visits[cell].Source], roadAt, nodes, pass);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Road nodes on the planned stops, linked from the stranded node to the trail's end: an
    /// earlier road's node where the road met one, else <paramref name="source"/>.
    /// </summary>
    private static void LayRoad(
        NavNode stranded,
        IReadOnlyList<Point3D> trail,
        List<int> stops,
        int end,
        NavNode source,
        Dictionary<(int X, int Y), NavNode> roadAt,
        List<NavNode> nodes,
        int pass
    )
    {
        var previous = stranded;

        for (var i = 0; i < stops.Count; i++)
        {
            var road = RoadNode(trail[stops[i]], roadAt, nodes, pass);
            GraphConnect.LinkForRoad(previous, road);
            previous = road;
        }

        var last = trail[end];
        GraphConnect.LinkForRoad(previous, end < trail.Count - 1 ? roadAt[(last.X, last.Y)] : source);
    }

    /// <summary>
    /// Where road nodes stand along a walked trail, as indexes into it. The road runs
    /// from <paramref name="start"/> through each stop to the trail tile at
    /// <paramref name="end"/>, and every edge on it is a straight walk both ways with the
    /// walker's step rule, within <see cref="RoadSpacing"/>. A stop goes on the last tile
    /// still such a walk from the previous one, so a bend gets its own node. The road ends
    /// early on a tile where an earlier road already has a node. Null when some hop has no
    /// straight walk both ways at all.
    /// </summary>
    /// <param name="trail">Tiles walked, each in reach of the one before and back, ending at the source.</param>
    /// <param name="joinsRoad">True on a tile that already carries a road node.</param>
    /// <param name="walker">How the walker stands and steps.</param>
    /// <param name="keep">Where a road may run, or null for anywhere the walker steps.</param>
    /// <param name="end">Index of the trail tile the road ends on.</param>
    public static List<int> PlanStops(
        Point3D start,
        IReadOnlyList<Point3D> trail,
        Func<Point3D, bool> joinsRoad,
        TileWalker walker,
        Func<int, int, int, bool> keep,
        out int end
    )
    {
        end = -1;

        if (trail is not { Count: > 0 } || walker == null)
        {
            return null;
        }

        var stops = new List<int>();
        // Index -1 is the start, before the first trail tile.
        var anchorIndex = -1;
        var lastInLine = -1;
        var i = 0;

        while (i < trail.Count)
        {
            var anchor = anchorIndex < 0 ? start : trail[anchorIndex];

            if (InLine(anchor, trail[i], walker, keep))
            {
                lastInLine = i;

                if (joinsRoad?.Invoke(trail[i]) == true)
                {
                    end = i;
                    return stops;
                }

                i++;
                continue;
            }

            if (lastInLine == anchorIndex)
            {
                return null;
            }

            stops.Add(lastInLine);
            anchorIndex = lastInLine;
        }

        end = trail.Count - 1;
        return stops;
    }

    private static bool InLine(Point3D from, Point3D to, TileWalker walker, Func<int, int, int, bool> keep) =>
        NavMetric.Chebyshev(from, to) <= RoadSpacing &&
        WalkLine.Reaches(walker, from, to, keep) &&
        WalkLine.Reaches(walker, to, from, keep);

    /// <summary>Cells the walk reached at or near a node, nearest first.</summary>
    private static IEnumerable<Cell> AttachCells(NavNode node, Dictionary<Cell, Visit> visits)
    {
        for (var radius = 0; radius <= AttachRadiusCells; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var x = node.X + dx * GridStep;
                    var y = node.Y + dy * GridStep;

                    if (x >= 0 && y >= 0 && visits.ContainsKey(ToCell(x, y)))
                    {
                        yield return ToCell(x, y);
                    }
                }
            }
        }
    }

    /// <summary>The tiles the walk passed, from a cell back to the source it came from.</summary>
    private static List<Point3D> Trail(Cell from, Dictionary<Cell, Visit> visits)
    {
        var trail = new List<Point3D>();
        var cell = from;

        while (true)
        {
            var visit = visits[cell];
            trail.Add(visit.Tile);

            if (visit.Parent == cell)
            {
                return trail;
            }

            cell = visit.Parent;
        }
    }

    private static NavNode RoadNode(
        Point3D tile,
        Dictionary<(int X, int Y), NavNode> roadAt,
        List<NavNode> nodes,
        int pass
    )
    {
        if (roadAt.TryGetValue((tile.X, tile.Y), out var existing))
        {
            return existing;
        }

        var node = new NavNode
        {
            // The list only grows, so its length is a name no earlier node holds, even
            // when the graph is walked again after it was loaded or repaired.
            Name = $"{RoadNamePrefix}{pass}-{nodes.Count}",
            X = tile.X,
            Y = tile.Y,
            Z = tile.Z,
            ArrivalRange = NavLimits.DefaultArrivalRange
        };

        nodes.Add(node);
        roadAt[(tile.X, tile.Y)] = node;
        return node;
    }

    /// <summary>
    /// Every node a traveller can already stand on when starting in the largest piece:
    /// its members plus anything an edge of any kind reaches from them, gates included.
    /// Gate edges still do not count toward the pieces themselves.
    /// </summary>
    private static HashSet<string> GateReach(
        List<NavNode> nodes,
        Dictionary<string, int> componentOf,
        int largest
    )
    {
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<NavNode>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            byName[node.Name] = node;

            if (componentOf.TryGetValue(node.Name, out var piece) && piece == largest)
            {
                reached.Add(node.Name);
                queue.Enqueue(node);
            }
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            foreach (var otherName in node.Connects ?? [])
            {
                if (reached.Contains(otherName) ||
                    !byName.TryGetValue(otherName, out var other) ||
                    other.Indoor)
                {
                    continue;
                }

                reached.Add(otherName);
                queue.Enqueue(other);
            }
        }

        return reached;
    }

    private static int LargestGroundComponent(
        List<NavNode> nodes,
        Dictionary<string, int> componentOf
    )
    {
        var best = -1;
        var bestSize = 0;

        foreach (var (id, size) in GroundPieces(nodes, componentOf, NotAMoongate))
        {
            if (size > bestSize)
            {
                best = id;
                bestSize = size;
            }
        }

        return best;
    }

    /// <summary>
    /// A public moongate joins towns for travel. It must not make a gate look locally
    /// connected to its town road.
    /// </summary>
    private static bool NotAMoongate(NavNode node, NavNode other) =>
        NavGates.KindBetween(node, other.Name) != NavGateKind.Moongate;

    /// <summary>Only a walking link joins two nodes: no gate of any kind leads between them.</summary>
    private static bool WalksBetween(NavNode node, NavNode other) =>
        NavGates.KindEitherWay(node, other) == NavGateKind.None;

    /// <summary>
    /// Numbers the pieces of the graph from 0 into <paramref name="componentOf"/> and returns
    /// each piece's size. Two linked nodes share a piece when <paramref name="joins"/> says
    /// the link joins them. An indoor destination must not join two outdoor street pieces.
    /// </summary>
    private static Dictionary<int, int> GroundPieces(
        List<NavNode> nodes,
        Dictionary<string, int> componentOf,
        Func<NavNode, NavNode, bool> joins
    )
    {
        var sizes = new Dictionary<int, int>();
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                byName[node.Name] = node;
            }
        }

        var component = 0;

        foreach (var start in byName.Values)
        {
            if (componentOf.ContainsKey(start.Name))
            {
                continue;
            }

            var queue = new Queue<NavNode>();
            queue.Enqueue(start);
            componentOf[start.Name] = component;
            var size = 0;

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                size++;

                foreach (var otherName in node.Connects ?? [])
                {
                    if (node.Indoor ||
                        componentOf.ContainsKey(otherName) ||
                        !byName.TryGetValue(otherName, out var other) ||
                        other.Indoor ||
                        !joins(node, other))
                    {
                        continue;
                    }

                    componentOf[otherName] = component;
                    queue.Enqueue(other);
                }
            }

            sizes[component] = size;
            component++;
        }

        return sizes;
    }

    private static Cell ToCell(int x, int y) => new(x / GridStep * GridStep, y / GridStep * GridStep);

    private readonly record struct Cell(int X, int Y);

    /// <summary>
    /// A cell the walk reached: the cell it came from, the tile it stands on, and the index
    /// of the source node whose walk reached it.
    /// </summary>
    private readonly record struct Visit(Cell Parent, Point3D Tile, int Source);
}
