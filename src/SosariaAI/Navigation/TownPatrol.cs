using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;

namespace SosariaAI.Navigation;

/// <summary>
/// A watch loop round a character's own town: bank, smith or armorer, healer, inn, and
/// back to the bank. The authored patrol is a Britain loop; a watchman homed in
/// Trinsic walked for Britain and reached nothing.
/// </summary>
public static class TownPatrol
{
    public const int TownRadius = 120;
    public const int MinPoints = 2;

    public static bool NeedsLocalLoop(IReadOnlyList<Point3D> authored, Point3D homeSpot, int leashRadius) =>
        authored is { Count: > 0 } && HomeLeash.BeyondLeash(authored[0], homeSpot, leashRadius);

    public static List<Point3D> Build(DestinationCatalog catalog, Point3D homeSpot)
    {
        var points = new List<Point3D>();

        if (catalog == null || homeSpot == Point3D.Zero)
        {
            return points;
        }

        Add(points, catalog.Nearest(homeSpot, DestinationKind.Bank), homeSpot);
        Add(points, catalog.Nearest(homeSpot, DestinationKind.Vendor, "Smith") ??
                    catalog.Nearest(homeSpot, DestinationKind.Vendor, "Armorer"), homeSpot);
        Add(points, catalog.Nearest(homeSpot, DestinationKind.Healer), homeSpot);
        Add(points, catalog.Nearest(homeSpot, DestinationKind.Vendor, "Provisioner"), homeSpot);
        Add(points, catalog.Nearest(homeSpot, DestinationKind.Vendor, "Tailor"), homeSpot);

        if (points.Count >= MinPoints && points[0] != points[^1])
        {
            points.Add(points[0]);
        }

        return points.Count >= MinPoints ? points : [];
    }

    private static void Add(List<Point3D> points, Destination dest, Point3D homeSpot)
    {
        if (dest == null || dest.Arrival == Point3D.Zero ||
            NavMetric.Chebyshev(dest.Arrival, homeSpot) > TownRadius ||
            points.Contains(dest.Arrival))
        {
            return;
        }

        points.Add(dest.Arrival);
    }
}
