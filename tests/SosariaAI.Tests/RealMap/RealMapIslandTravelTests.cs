using System.Linq;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Jhelom and Skara Brae are islands a player reaches by public moongate. On the real
/// Felucca tiles and the live nav graph, a person plans from a Britain street to each
/// island's bank and back, and the plan steps through a moongate.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapIslandTravelTests(ITestOutputHelper output)
{
    /// <summary>A street tile west of the Britain bank, where the town's trips start.</summary>
    private static readonly Point3D BritainStreet = new(1415, 1690, 0);

    [RealMapFact]
    public void Britain_ToJhelom_AndBack_ByMoongate() => AssertRoundTrip(WorkSites.JhelomTown, "Jhelom");

    [RealMapFact]
    public void Britain_ToSkaraBrae_AndBack_ByMoongate() => AssertRoundTrip(WorkSites.SkaraTown, "Skara Brae");

    private void AssertRoundTrip(Point3D bank, string island)
    {
        var walker = RealMapWorld.Walker();
        var graph = RealMapWorld.LiveGraph();

        AssertMoongateTrip(graph, walker, BritainStreet, bank, $"Britain to {island}");
        AssertMoongateTrip(graph, walker, bank, BritainStreet, $"{island} to Britain");
    }

    private void AssertMoongateTrip(NavGraph graph, TileWalker walker, Point3D from, Point3D to, string trip)
    {
        var goal = Traveler.PreferredGoal(graph, from, to);
        Assert.NotNull(goal);

        var path = Traveler.PlanNames(graph, from, goal.Name, walker, RealMapWorld.IsIndoor, avoid: null, out var why);
        var moongates = path.Zip(path.Skip(1)).Count(hop => graph.GateKind(hop.First, hop.Second) == NavGateKind.Moongate);
        output.WriteLine($"{trip}: {path.Count} nodes, {moongates} moongate hops {why}");

        Assert.True(path.Count > 0, $"{trip}: {why}");
        Assert.True(moongates > 0, $"{trip} takes no moongate");
    }
}
