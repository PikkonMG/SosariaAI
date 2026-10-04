using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Navigation.Generation;

public sealed class DestinationDraft
{
    public string Name { get; set; }
    public string Kind { get; set; }
    public string Role { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int? Difficulty { get; set; }
}

public static class CatalogBuilder
{
    /// <summary>
    /// Nearby nodes whose arrival walk may be proved by a tile route when the straight
    /// walk fails. Each proof is an A*, so only the nearest few try.
    /// </summary>
    private const int ArrivalRouteTries = 2;

    /// <summary>
    /// Town stops (inn, bank, shop) must snap to a nearby street node. A longer snap sent a
    /// Britain inn to a mine hill 19 tiles east, and every Britain tavern walk failed.
    /// </summary>
    private const int MaxTownAttachTiles = 16;

    /// <summary>Stands for any graph piece where a component is asked for.</summary>
    private const int AnyComponent = -1;

    /// <summary>
    /// An alias and the word that earns it are one constant: a word is matched without regard
    /// to case, and the alias is written as the constant spells it.
    /// </summary>
    private const string TavernAlias = "tavern";

    private const string PubAlias = "pub";
    private const string BritainToken = "Britain";

    /// <summary>
    /// A destination for each draft the walker can arrive at. No walker, no catalog:
    /// nothing can be walked.
    /// </summary>
    /// <param name="walker">How a walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    public static DestinationCatalog Build(
        IReadOnlyList<DestinationDraft> drafts,
        NavGraph graph,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor = null
    )
    {
        if (drafts == null || graph == null || graph.NodeCount == 0 || walker == null)
        {
            return new DestinationCatalog([]);
        }

        var destinations = new List<Destination>();
        var mainComponent = graph.LargestComponent();

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];

            if (draft == null || string.IsNullOrWhiteSpace(draft.Name))
            {
                continue;
            }

            var attachLimit = IsTownArrival(draft) ? MaxTownAttachTiles : NavLimits.MaxLegDistance;

            // A walk plan can only end at a node in the main component, and the arrival
            // leg from that node to the seed must itself walk. A seed whose neighbourhood
            // sits on unreachable islands, on a floor no stair reaches, or more than a
            // pathfinder leg from any node a plan reaches fails every trip it is offered.
            // A shrine raises the ghosts who reach it from wherever they fell, so it may
            // stand on a piece of its own: the Valor shrine's island has no land or pad
            // way off it, and a ghost there has no other ankh. The ghost judges its own
            // reach before it sets out.
            var arrival = NearestArrival(graph, draft, mainComponent, attachLimit, walker, isIndoor) ??
                          (KindIs(draft, DestinationKind.Shrine)
                              ? NearestArrival(graph, draft, AnyComponent, attachLimit, walker, isIndoor)
                              : null);

            if (arrival is not ({ } nearest, var at))
            {
                continue;
            }

            destinations.Add(
                new Destination
                {
                    Name = draft.Name,
                    Kind = draft.Kind,
                    Role = draft.Role,
                    X = draft.X,
                    Y = draft.Y,
                    Z = at.Z,
                    Node = nearest.Name,
                    Aliases = AliasesOf(draft),
                    Difficulty = draft.Difficulty
                }
            );
        }

        return new DestinationCatalog(destinations);
    }

    /// <summary>
    /// The closest node in <paramref name="component"/> (or in any piece, for
    /// <see cref="AnyComponent"/>) from which the arrival leg walks to the seed, and the
    /// floor the leg ends on. Seeds snapped onto islanded nodes (a bank
    /// interior with no edges) skip past those to the nearest node in the main component;
    /// the seed tile stays the arrival leg. A vendor behind a counter still counts: the
    /// tile route aims at the nearest floor beside the counter.
    /// </summary>
    private static (NavNode Node, Point3D At)? NearestArrival(
        NavGraph graph,
        DestinationDraft draft,
        int component,
        int attachLimit,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        // A seed's height is a guess; the floor found at its tile is where a walk ends.
        var seedFloor = walker.FloorNear(draft.X, draft.Y, draft.Z);
        var candidates = graph.FindNearest(new Point3D(draft.X, draft.Y, seedFloor ?? draft.Z), Traveler.StartCandidates);
        var routeTries = 0;

        for (var i = 0; i < candidates.Count; i++)
        {
            var node = candidates[i];

            if ((component != AnyComponent && graph.ComponentOf(node.Name) != component) ||
                NavMetric.Chebyshev(new Point3D(draft.X, draft.Y, 0), node.Location) > attachLimit)
            {
                continue;
            }

            // A location's height can be far off. When no floor stands near the height it
            // gives, the arrival leg ends on the floor level with the node: Hythloth's gate
            // chamber stands 64 up, and its entrance location reads 0.
            var at = new Point3D(draft.X, draft.Y, seedFloor ?? walker.FloorNear(draft.X, draft.Y, node.Z) ?? draft.Z);
            var atIndoor = isIndoor?.Invoke(at.X, at.Y, at.Z) == true;

            if (WalkLine.Reaches(walker, node.Location, at, WalkLine.OutdoorKeep(node.Indoor, atIndoor, isIndoor)))
            {
                return (node, at);
            }

            if (routeTries < ArrivalRouteTries)
            {
                routeTries++;

                if (TileRoute.FindForBuild(node.Location, at, walker, isIndoor).Count > 0)
                {
                    return (node, at);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The extra words a destination answers to. The graveyard and despise aliases are the
    /// words a routine names these places by; inns and taverns answer to "tavern" and "pub".
    /// </summary>
    private static List<string> AliasesOf(DestinationDraft draft)
    {
        var aliases = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (IsGraveyard(draft))
        {
            AddAlias(aliases, seen, DestinationCatalog.GraveyardToken);
        }

        if (IsDespiseDungeon(draft))
        {
            AddAlias(aliases, seen, DestinationCatalog.DespiseToken);
        }

        if (IsTavern(draft))
        {
            AddAlias(aliases, seen, TavernAlias);
            AddAlias(aliases, seen, PubAlias);
        }

        if (IsVendorNamed(draft, ShopFinder.CarpenterToken))
        {
            AddAlias(aliases, seen, ShopFinder.CarpenterToken);
        }

        if (IsVendorNamed(draft, DestinationCatalog.FishermanToken))
        {
            AddAlias(aliases, seen, DestinationCatalog.FishermanToken);
        }

        return aliases;
    }

    private static void AddAlias(List<string> aliases, HashSet<string> seen, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || !seen.Add(alias))
        {
            return;
        }

        aliases.Add(alias);
    }

    /// <summary>Britain's graveyard: the one a routine's "graveyard" names.</summary>
    private static bool IsGraveyard(DestinationDraft draft) =>
        KindIs(draft, DestinationKind.Hunt) &&
        ContainsToken(draft.Name, BritainToken) &&
        (EqualsToken(draft.Role, DestinationCatalog.GraveyardToken) || ContainsToken(draft.Name, DestinationCatalog.GraveyardToken));

    private static bool IsDespiseDungeon(DestinationDraft draft) =>
        KindIs(draft, DestinationKind.Dungeon) &&
        (EqualsToken(draft.Role, DestinationCatalog.DespiseToken) || ContainsToken(draft.Name, DestinationCatalog.DespiseToken));

    /// <summary>A shop whose role is the word, or whose name holds it.</summary>
    private static bool IsVendorNamed(DestinationDraft draft, string word) =>
        KindIs(draft, DestinationKind.Vendor) && (EqualsToken(draft.Role, word) || ContainsToken(draft.Name, word));

    private static bool IsTownArrival(DestinationDraft draft) =>
        KindIs(draft, DestinationKind.Bank) ||
        KindIs(draft, DestinationKind.Vendor) ||
        KindIs(draft, DestinationKind.Healer) ||
        IsTavern(draft);

    // Inns are often Kind Healer; still alias them so the "tavern" token resolves.
    // "Pub" is a whole word: a "Public Library" holds the letters pub.
    private static bool IsTavern(DestinationDraft draft) =>
        ContainsToken(draft.Name, TavernAlias) ||
        ContainsWord(draft.Name, PubAlias) ||
        ContainsToken(draft.Name, KindFromGroup.InnRole) ||
        ContainsToken(draft.Role, TavernAlias) ||
        ContainsWord(draft.Role, PubAlias) ||
        ContainsToken(draft.Role, KindFromGroup.InnRole);

    private static bool KindIs(DestinationDraft draft, DestinationKind kind) =>
        string.Equals(draft.Kind, kind.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool EqualsToken(string value, string token) =>
        string.Equals(value, token, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsToken(string value, string token) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsWord(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var start = 0;

        while (start <= value.Length - token.Length)
        {
            var index = value.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return false;
            }

            var beforeOk = index == 0 || !char.IsLetter(value[index - 1]);
            var afterOk = index + token.Length == value.Length ||
                          !char.IsLetter(value[index + token.Length]);

            if (beforeOk && afterOk)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }
}
