using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// One facet's towns and dungeons as <see cref="PlaceRules"/> judged them: the places that
/// stay, with their doors, the regions dropped from the world, and the name people give a
/// spot inside a region. The staff panel lists its places, the destination catalog loses
/// what lies in a dropped region, and gossip names a spot by it. Pure.
/// </summary>
public sealed class PlaceBook
{
    /// <summary>The article a region name may open with ("The Painted Caves"), which a destination name may leave off.</summary>
    private const string Article = "The ";

    private readonly Dictionary<string, List<Place>> _placesByRegion = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _judged = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PlaceRegion> _dropped = [];
    private readonly List<Place> _places = [];

    /// <param name="regions">Every town and dungeon region the rule judged.</param>
    /// <param name="places">The places that stay (<see cref="PlaceRules.Book"/>).</param>
    public PlaceBook(IReadOnlyList<PlaceRegion> regions, IReadOnlyList<Place> places)
    {
        foreach (var place in places ?? [])
        {
            _places.Add(place);

            if (!_placesByRegion.TryGetValue(place.Region, out var list))
            {
                list = [];
                _placesByRegion[place.Region] = list;
            }

            list.Add(place);
        }

        foreach (var region in regions ?? [])
        {
            if (_judged.Add(region.Name) && !_placesByRegion.ContainsKey(region.Name))
            {
                _dropped.Add(region);
            }
        }
    }

    public static PlaceBook Empty { get; } = new([], []);

    public IReadOnlyList<Place> Places => _places;

    /// <summary>The regions dropped: a later era's, or with no working way in, or no live spawns.</summary>
    public IReadOnlyList<PlaceRegion> Dropped => _dropped;

    /// <summary>
    /// The dropped region a spot lies in, or null. No plan goes there, but a person the save
    /// or an older build left there still has to walk, pad or recall out of it.
    /// </summary>
    public PlaceRegion DroppedAt(Point3D at)
    {
        foreach (var region in _dropped)
        {
            if (region.Contains(at))
            {
                return region;
            }
        }

        return null;
    }

    public List<Place> Towns => Of(isTown: true);

    public List<Place> Dungeons => Of(isTown: false);

    /// <summary>
    /// The name people give a spot inside the named region: the place's own name, the part's
    /// name nearest the spot in a shared region, or null for a region dropped from the world.
    /// A region the rule does not judge, such as a town's field, keeps its own name.
    /// </summary>
    public string Spoken(string regionName, Point3D at)
    {
        if (string.IsNullOrWhiteSpace(regionName) || !_judged.Contains(regionName))
        {
            return regionName;
        }

        if (!_placesByRegion.TryGetValue(regionName, out var places))
        {
            return null;
        }

        var nearest = places[0];
        var best = int.MaxValue;

        foreach (var place in places)
        {
            var tiles = NavMetric.NearestOf(at, place.Spawners);

            if (tiles < best)
            {
                best = tiles;
                nearest = place;
            }
        }

        return nearest.Name;
    }

    /// <summary>
    /// True when a destination belongs to a dropped place: it lies inside the place's area,
    /// or names the place, or is a dungeon door at the place's recorded entrance.
    /// </summary>
    public bool IsDropped(Destination destination)
    {
        if (destination == null)
        {
            return false;
        }

        foreach (var region in _dropped)
        {
            if (region.Contains(destination.Location) ||
                Names(destination.Name, region.Name) || Names(destination.Role, region.Name) ||
                destination.ParsedKind == DestinationKind.Dungeon && region.Entrance != Point3D.Zero &&
                NavMetric.Chebyshev(destination.Location, region.Entrance) <= PlaceRules.DoorReach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The catalog without the destinations of dropped places; the same catalog when none is dropped.</summary>
    public DestinationCatalog Filter(DestinationCatalog catalog)
    {
        if (catalog == null || _dropped.Count == 0)
        {
            return catalog;
        }

        var kept = new List<Destination>(catalog.All.Count);

        foreach (var destination in catalog.All)
        {
            if (!IsDropped(destination))
            {
                kept.Add(destination);
            }
        }

        return kept.Count == catalog.All.Count ? catalog : new DestinationCatalog(kept);
    }

    private List<Place> Of(bool isTown)
    {
        var found = new List<Place>();

        foreach (var place in _places)
        {
            if (place.IsTown == isTown)
            {
                found.Add(place);
            }
        }

        return found;
    }

    /// <summary>True when a text names the region, with or without its leading article.</summary>
    private static bool Names(string text, string regionName)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(regionName))
        {
            return false;
        }

        var name = regionName.StartsWith(Article, StringComparison.OrdinalIgnoreCase)
            ? regionName[Article.Length..]
            : regionName;

        return text.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
