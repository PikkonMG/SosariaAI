using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class GhostSeekTests
{
    private static readonly Point3D From = new(1425, 1695, 0);
    private static readonly Point3D FarShrine = new(1850, 875, 0);
    private static readonly Point3D NearHealer = new(1430, 1700, 0);

    private static readonly ResSource[] Sources =
    [
        new(GhostSeek.KindShrine, FarShrine, GhostRules.AnkhRange),
        new(GhostSeek.KindHealer, NearHealer, GhostRules.HealerRange)
    ];

    private const double ShortTrip = 20;
    private const double LongTrip = 4000;

    [Fact]
    public void ShortestTrip_OfEqualTrips_PicksTheNearerSite()
    {
        var pick = GhostSeek.ShortestTrip(From, Sources, static _ => ShortTrip);

        Assert.NotNull(pick);
        Assert.Equal(GhostSeek.KindHealer, pick.Value.Kind);
        Assert.Equal(GhostRules.HealerRange, pick.Value.Range);
    }

    [Fact]
    public void ShortestTrip_SkipsASourceNoRoadReaches()
    {
        // The Honesty shrine loop: the nearest site sat where no node could route.
        var pick = GhostSeek.ShortestTrip(From, Sources, site => site.Location != NearHealer ? LongTrip : null);

        Assert.NotNull(pick);
        Assert.Equal(FarShrine, pick.Value.Location);
    }

    [Fact]
    public void ShortestTrip_PicksAFarSiteOverANearOneAtTheEndOfALongWayRound()
    {
        // Red ghosts at Magincia took the nearer Honesty ankh through three dungeons, while
        // the Justice shrine lay a short walk from the Yew gate.
        var pick = GhostSeek.ShortestTrip(From, Sources, site => site.Location == NearHealer ? LongTrip : ShortTrip);

        Assert.NotNull(pick);
        Assert.Equal(FarShrine, pick.Value.Location);
    }

    [Fact]
    public void ShortestTrip_NothingReachable_IsNull() =>
        Assert.Null(GhostSeek.ShortestTrip(From, Sources, static _ => null));

    [Fact]
    public void ShortestTrip_NoSources_IsNull()
    {
        Assert.Null(GhostSeek.ShortestTrip(From, [], static _ => ShortTrip));
        Assert.Null(GhostSeek.ShortestTrip(From, null, static _ => ShortTrip));
    }

    [Fact]
    public void ShortestTrip_AsksEverySite_NearestFirst()
    {
        // The few tile proofs a look may run go to the sites asked first.
        var asked = new List<Point3D>();

        GhostSeek.ShortestTrip(From, Sources, site =>
        {
            asked.Add(site.Location);
            return ShortTrip;
        });

        Assert.Equal([NearHealer, FarShrine], asked);
    }

    [Fact]
    public void RouteTiles_SumsTheLegsFromTheStart()
    {
        // A 3-4-5 leg, then a straight leg south.
        var corner = new Point3D(From.X + TriangleDx, From.Y + TriangleDy, From.Z);
        var route = new[] { corner, new Point3D(corner.X, corner.Y + StraightLeg, From.Z) };

        Assert.Equal(TriangleLeg + StraightLeg, GhostSeek.RouteTiles(From, route), TilePrecision);
        Assert.Equal(0, GhostSeek.RouteTiles(From, []));
        Assert.Equal(0, GhostSeek.RouteTiles(From, null));
    }

    private const int TriangleDx = 3;
    private const int TriangleDy = 4;
    private const double TriangleLeg = 5;
    private const int StraightLeg = 6;
    private const int TilePrecision = 6;

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(GhostSeek.TileProofTries - 1, TileRoute.MaxTripTiles, true)]
    [InlineData(GhostSeek.TileProofTries, 0, false)]
    [InlineData(0, TileRoute.MaxTripTiles + 1, false)]
    public void MayTryTiles_OnlyAFewNearSitesALook(int tries, int distance, bool may) =>
        Assert.Equal(may, GhostSeek.MayTryTiles(tries, distance));

    [Fact]
    public void RaiseSpot_TheNearestTileAGhostCanStandOn()
    {
        var ankh = new Point3D(1458, 843, 0);
        var open = new Point3D(ankh.X + 1, ankh.Y, 0);

        // The ankh's own tile holds the statue; the tile east of it is open ground.
        var spot = GhostSeek.RaiseSpot(ankh, GhostRules.AnkhRange, (x, y) => x == open.X && y == open.Y ? open : null);

        Assert.Equal(open, spot);
    }

    [Fact]
    public void RaiseSpot_AnAnkhRingedByPads_RaisesNobody()
    {
        // Spirituality (1592,2489): every tile within reach is a pad or the statue.
        Assert.Null(GhostSeek.RaiseSpot(new Point3D(1592, 2489, 20), GhostRules.AnkhRange, static (_, _) => null));
        Assert.Null(GhostSeek.RaiseSpot(new Point3D(1592, 2489, 20), GhostRules.AnkhRange, null));
    }

    [Fact]
    public void RaiseSpot_LooksNoFurtherThanTheAnkhsReach()
    {
        var ankh = new Point3D(100, 100, 0);
        var far = new Point3D(ankh.X + GhostRules.AnkhRange + 1, ankh.Y, 0);

        Assert.Null(GhostSeek.RaiseSpot(ankh, GhostRules.AnkhRange, (x, y) => x == far.X && y == far.Y ? far : null));
    }
}
