using System.Text;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Says why a walk from one tile to another finds no route: the tile router's reason,
/// then each nearby graph node and whether a straight walk reaches it. For the
/// route-explain staff command, so a stuck character can be understood on the spot.
/// </summary>
public static class RouteExplain
{
    public const int NodesShown = 6;

    public static string Describe(Map map, string facet, Point3D from, Point3D to)
    {
        var text = new StringBuilder();
        text.AppendLine($"Route {from} -> {to} on {map?.Name}");

        var walker = Standable.Walker(map);
        var tiles = TileRoute.Find(
            from,
            to,
            walker,
            (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
            NavLimits.DefaultArrivalRange,
            out var reason
        );

        text.AppendLine(tiles.Count > 0
            ? $"Tile route: {tiles.Count} legs, first {tiles[0]}, last {tiles[^1]}"
            : $"Tile route: none ({reason})");

        var graph = NavWorld.GraphFor(facet);

        if (graph == null)
        {
            text.AppendLine("Graph: none loaded for this facet");
            return text.ToString();
        }

        DescribeNodes(text, graph, walker, "start", from);
        DescribeNodes(text, graph, walker, "goal", to);

        var names = Traveler.PlanNames(
            graph,
            from,
            graph.FindNearest(to)?.Name,
            walker,
            (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
            avoid: null,
            out var whyNone
        );
        text.AppendLine(names.Count > 0 ? $"Graph route: {names.Count} nodes" : $"Graph route: none ({whyNone})");
        return text.ToString();
    }

    private static void DescribeNodes(StringBuilder text, NavGraph graph, TileWalker walker, string label, Point3D at)
    {
        var nodes = graph.FindNearest(at, NodesShown);
        text.AppendLine($"Nodes near {label} {at}:");

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var distance = NavMetric.Chebyshev(at, node.Location);
            var verdict = distance > NavLimits.MaxLegDistance
                ? "too far"
                : WalkLine.Reaches(walker, at, node.Location) ? "straight walk reaches" : "straight walk blocked";
            text.AppendLine($"  {node.Name} at {node.Location}, {distance} tiles, {(node.Indoor ? "indoor, " : "")}{verdict}");
        }
    }
}
