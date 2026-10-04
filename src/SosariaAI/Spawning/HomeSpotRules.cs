using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Spawning;

/// <summary>
/// A character's home spot is the bank nearest its own spawn when that bank is
/// inside the leash; otherwise the spawn itself. Only an unbound character uses the
/// Britain bank.
/// </summary>
public static class HomeSpotRules
{
    /// <summary>A copy's own corner of town, this far from the shared bank anchor.</summary>
    public const int CornerRadius = 24;

    /// <summary>How many scatter picks round the bank to try when the town has no places.</summary>
    public const int CornerTries = 6;

    /// <summary>
    /// How many place and scatter picks to try. A Britain street tile a few steps from a shop
    /// passes about one time in three: the rest are walls, roofs and shop floors.
    /// </summary>
    public const int PlaceTries = 12;

    /// <summary>A shop, inn, healer or dock this close to the home bank belongs to the same town.</summary>
    public const int TownPlaceRadius = 64;

    /// <summary>A corner is a doorstep near its place, not the place's own tile.</summary>
    public const int PlaceScatterRadius = 6;

    /// <summary>
    /// Full route proofs per anchor verdict and per resolve call. A town bank is
    /// judged once for the whole boot; a standable tile nearby only needs a clear
    /// walk line to it, so no copy path-finds every ring tile.
    /// </summary>
    private static readonly Dictionary<(Map Map, Point3D Anchor), bool> RoutableAnchors = new();

    /// <summary>A rebuilt nav graph or a fresh boot can change a verdict; clear between them.</summary>
    public static void ResetRoutableCache() => RoutableAnchors.Clear();

    public static Point3D Resolve(Point3D nearestBank, Point3D homeSpawn, int leashRadius)
    {
        if (homeSpawn == Point3D.Zero)
        {
            return CharactersFile.DefaultBankSpot;
        }

        if (nearestBank != Point3D.Zero && !HomeLeash.BeyondLeash(nearestBank, homeSpawn, leashRadius))
        {
            return nearestBank;
        }

        return homeSpawn;
    }

    /// <summary>
    /// The places a copy may call its corner of town: the shops, inns, healers and docks
    /// within <see cref="TownPlaceRadius"/> of its home bank, off the bank plaza, once
    /// each, in a fixed order so a reboot picks the same one. Pure.
    /// </summary>
    public static List<Point3D> TownPlaces(IEnumerable<Point3D> places, Point3D bank)
    {
        var found = new List<Point3D>();

        if (places == null || bank == Point3D.Zero)
        {
            return found;
        }

        foreach (var place in places)
        {
            if (place != Point3D.Zero && NavMetric.Chebyshev(place, bank) <= TownPlaceRadius &&
                !BankPlaza.Contains(place, bank) && !found.Contains(place))
            {
                found.Add(place);
            }
        }

        found.Sort(static (a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return found;
    }

    /// <summary>
    /// The town places of the catalog around a home bank: the doorstep of every shop and
    /// healer, which is the street node its marker is bound to. A shop marker sits on the
    /// counter or the roof; the node is where a person can stand outside.
    /// </summary>
    public static List<Point3D> TownPlaces(DestinationCatalog catalog, NavGraph graph, Point3D bank)
    {
        var doorsteps = new List<Point3D>();

        foreach (var dest in catalog?.All ?? [])
        {
            if (dest.ParsedKind is DestinationKind.Vendor or DestinationKind.Healer)
            {
                doorsteps.Add(graph?.TryGetNode(dest.Node, out var node) == true ? node.Location : dest.Arrival);
            }
        }

        return TownPlaces(doorsteps, bank);
    }

    /// <summary>The town place a copy's corner stands by on one try: a stable pick per id and try.</summary>
    public static Point3D PlaceFor(IReadOnlyList<Point3D> places, string uniqueId, int attempt) =>
        places is { Count: > 0 } ? places[WorkSites.StableRoll(uniqueId, attempt) % places.Count] : Point3D.Zero;

    /// <summary>
    /// The spot a copy treats as its own doorstep: a seeded tile a few steps from one of
    /// its town's shops, inns or docks, on that place's floor and never on the bank plaza.
    /// Every copy once anchored to the bank tile; a scatter round the bank that needed a
    /// straight walk back to the bank failed for 192 of 200 Britain copies, so the whole
    /// town idled inside the bank. Errands to the bank still use the real home spot.
    /// </summary>
    public static Point3D CornerFor(Point3D homeSpot, string uniqueId, Map map, IReadOnlyList<Point3D> townPlaces)
    {
        if (homeSpot == Point3D.Zero || map == null || map == Map.Internal)
        {
            return homeSpot;
        }

        if (!WorkSites.IsCopy(uniqueId))
        {
            return OpenSpot(map, homeSpot);
        }

        for (var attempt = 0; attempt < PlaceTries; attempt++)
        {
            var place = PlaceFor(townPlaces, uniqueId, attempt);

            if (place == Point3D.Zero)
            {
                break;
            }

            var candidate = WorkSites.Scatter(place, $"{uniqueId}:corner{attempt}", PlaceScatterRadius);

            if (TryOpenNearby(map, candidate, SiteAnchor(map, place), out var onFloor) && IsFreeCorner(onFloor, homeSpot))
            {
                return onFloor;
            }
        }

        // Every scatter hit a wall: the doorstep itself, or the road beside it.
        var doorstep = PlaceFor(townPlaces, uniqueId, 0);

        if (doorstep != Point3D.Zero && OpenSpot(map, doorstep) is var open && IsFreeCorner(open, homeSpot))
        {
            return open;
        }

        var anchor = SiteAnchor(map, homeSpot);
        var radius = SpawnSpread.RadiusFor(SpawnSpread.CrowdPerSite, CornerRadius, SpawnSpread.CornerMaxRadius);

        for (var attempt = 0; attempt < CornerTries; attempt++)
        {
            var corner = WorkSites.Scatter(homeSpot, $"{uniqueId}:corner{attempt}", radius);

            if (TryOpenNearby(map, corner, anchor, out var onFloor) && IsFreeCorner(onFloor, homeSpot))
            {
                return onFloor;
            }
        }

        return OpenSpot(map, homeSpot);
    }

    /// <summary>
    /// A spot a walk home can end on: the spot itself when a person can stand there and walk
    /// out to the road, else the nearest outdoor road node. A bank marker sits behind the
    /// counter in some towns and a shop doorstep inside a closed room in others; every walk
    /// home to such a spot ended in "no standable tile" after minutes of trying.
    /// </summary>
    public static Point3D OpenSpot(Map map, Point3D spot)
    {
        if (spot == Point3D.Zero || map == null || map == Map.Internal)
        {
            return spot;
        }

        if (FloorSpot(Standable.Walker(map), spot.X, spot.Y, spot) is { } floor && map.CanSpawnMobile(floor) &&
            Routable(map, floor))
        {
            return floor;
        }

        return NavWorld.GraphFor(map.Name)?.FindNearest(spot) is { Indoor: false } node ? node.Location : spot;
    }

    /// <summary>
    /// Where a person logs in and comes back after death: a copy at its own corner of town,
    /// a fixture or a red at its spawn. Every copy once logged in on its town's bank tile;
    /// 126 people stood on the Britain banker's tile a minute after boot.
    /// </summary>
    public static Point3D LoginSpot(bool isCopy, bool isRed, Point3D homeCorner, Point3D homeSpawn) =>
        isCopy && !isRed && homeCorner != Point3D.Zero ? homeCorner : homeSpawn;

    /// <summary>A corner is neither on the bank plaza nor inside a harvest box, where a walk turns into a work trip.</summary>
    public static bool IsFreeCorner(Point3D corner, Point3D bank) =>
        !BankPlaza.Contains(corner, bank) && !WorkSites.IsWorkSpot(corner);

    /// <summary>
    /// The anchor's written height is not always its ground; Magincia's bank sits on a
    /// plateau. The ground is the surface the walker finds nearest the land there.
    /// </summary>
    public static Point3D GroundedAt(Map map, Point3D anchor) =>
        Standable.TryFindGround(map, anchor.X, anchor.Y, out var ground) ? new Point3D(anchor.X, anchor.Y, ground) : anchor;

    /// <summary>
    /// The spot a site's spawn tiles are proven from: the site's ground when a route leaves
    /// it, else the ground of the first tile beside it that a route leaves. The server's
    /// decorations stand a bookcase on the Moonglow banker's tile, so the ground there is the
    /// bookcase top at z 20: no route left it, 102 copies were refused and 28 woke on top of
    /// the bookcases. The test world loads no items, so the same site passed there. When no
    /// tile beside it is proven either, the site's ground stays the anchor and each tile
    /// needs its own full route proof.
    /// </summary>
    public static Point3D SiteAnchor(Map map, Point3D site)
    {
        var grounded = GroundedAt(map, site);

        if (AnchorRoutable(map, grounded))
        {
            return grounded;
        }

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = site.X + dx;
            var y = site.Y + dy;

            if (Standable.TryFindGround(map, x, y, out var ground) && AnchorRoutable(map, new Point3D(x, y, ground)))
            {
                return new Point3D(x, y, ground);
            }
        }

        return grounded;
    }

    /// <summary>
    /// The spot at a tile on the anchor's floor: the floor the walker finds there nearest
    /// the anchor's height. Null when no floor at that tile is the anchor's floor.
    /// </summary>
    public static Point3D? FloorSpot(TileWalker walker, int x, int y, Point3D anchor) =>
        walker?.FloorNear(x, y, anchor.Z) is { } floor && NavMetric.SameFloor(floor, anchor.Z)
            ? new Point3D(x, y, floor)
            : null;

    /// <summary>
    /// The anchor can leave for the world, judged once and kept. Per tile the boot
    /// then only needs a straight walk line to it — the line is itself a way out.
    /// </summary>
    public static bool AnchorRoutable(Map map, Point3D anchor)
    {
        var key = (map, anchor);

        if (RoutableAnchors.TryGetValue(key, out var routable))
        {
            return routable;
        }

        routable = Routable(map, anchor);
        RoutableAnchors[key] = routable;
        return routable;
    }

    /// <summary>
    /// A spot on the anchor's floor where a character may spawn and from which a straight
    /// walk reaches a proven anchor. When the anchor is unproven, the full route check
    /// decides so a walled yard still cannot strand a spawn.
    /// </summary>
    public static bool TryOpenNearby(Map map, Point3D candidate, Point3D anchor, out Point3D location) =>
        OpenNearby(map, candidate, anchor, out location) == null;

    /// <summary>
    /// The test of <see cref="TryOpenNearby"/> a candidate fails, in the order they run, or
    /// null when it passes all of them.
    /// </summary>
    public static SpawnReject? OpenNearby(Map map, Point3D candidate, Point3D anchor, out Point3D location)
    {
        var walker = Standable.Walker(map);
        location = candidate;

        if (FloorSpot(walker, candidate.X, candidate.Y, anchor) is not { } spot)
        {
            return SpawnReject.NoFloor;
        }

        if (!map.CanSpawnMobile(spot))
        {
            return SpawnReject.Blocked;
        }

        location = spot;

        if (AnchorRoutable(map, anchor))
        {
            return WalkLine.Reaches(walker, spot, anchor) ? null : SpawnReject.NoWalkLine;
        }

        return Routable(map, spot) ? null : SpawnReject.Sealed;
    }

    /// <summary>
    /// A tile a few steps from a bank a person can log in on:
    /// on the bank's floor, free, and a straight walk from a bank anchor a route leaves. The
    /// bank's own spot is the last try. A bank no route leaves seats no one: with no proof, 28
    /// Moonglow copies woke on the bank's bookcase tops and never walked off. Each tile turned
    /// down is counted in <paramref name="rejects"/>.
    /// </summary>
    public static bool TryBankTile(Map map, Point3D bank, string uniqueId, Dictionary<SpawnReject, int> rejects, out Point3D location)
    {
        var anchor = SiteAnchor(map, bank);
        location = bank;

        if (!AnchorRoutable(map, anchor))
        {
            Count(rejects, SpawnReject.Sealed);
            return false;
        }

        for (var attempt = 0; attempt <= SpawnPlacementRules.BankTries; attempt++)
        {
            var tile = attempt < SpawnPlacementRules.BankTries ? SpawnPlacementRules.BankTile(anchor, uniqueId, attempt) : anchor;

            if (OpenNearby(map, tile, anchor, out var spot) is { } reject)
            {
                Count(rejects, reject);
            }
            else
            {
                location = spot;
                return true;
            }
        }

        location = bank;
        return false;
    }

    /// <summary>Counts one tile turned down by a test.</summary>
    public static void Count(Dictionary<SpawnReject, int> rejects, SpawnReject reject) =>
        rejects[reject] = rejects.GetValueOrDefault(reject) + 1;

    /// <summary>
    /// A doorstep must reach the world: a standable tile inside a walled yard passes
    /// CanSpawnMobile but strands the character for every trip after. Without a
    /// graph there is nothing to check against, so the tile is allowed.
    /// </summary>
    public static bool Routable(Map map, Point3D point)
    {
        var graph = NavWorld.GraphFor(map?.Name);
        return graph == null ||
               Traveler.HasClearStart(
                   graph,
                   point,
                   Standable.Walker(map),
                   (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z));
    }
}
