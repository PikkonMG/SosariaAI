using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class EdgeHealthTests
{
    private const long Now = 1_000_000;

    [Fact]
    public void Key_IsTheSameBothWays() =>
        Assert.Equal(EdgeHealthRules.Key(3, 9), EdgeHealthRules.Key(9, 3));

    [Fact]
    public void Penalty_GrowsPerFailureUpToTheCap()
    {
        Assert.Equal(0, EdgeHealthRules.PenaltyTiles(0));
        Assert.Equal(EdgeHealthRules.PenaltyTilesPerFailure, EdgeHealthRules.PenaltyTiles(1));
        Assert.Equal(
            EdgeHealthRules.MaxCountedFailures * EdgeHealthRules.PenaltyTilesPerFailure,
            EdgeHealthRules.PenaltyTiles(EdgeHealthRules.MaxCountedFailures + 1)
        );
    }

    [Fact]
    public void Mark_FadesAfterItsLifetime()
    {
        var mark = EdgeHealthRules.After(failuresSoFar: 0, Now);

        Assert.Equal(1, mark.Failures);
        Assert.True(EdgeHealthRules.Active(Now, mark.Until));
        Assert.False(EdgeHealthRules.Active(Now + EdgeHealthRules.MarkLifetimeMs, mark.Until));
        Assert.Equal(EdgeHealthRules.MaxCountedFailures, EdgeHealthRules.After(EdgeHealthRules.MaxCountedFailures, Now).Failures);
    }

    [Fact]
    public void Search_PaysForAMarkedEdgeAndTakesTheDetour()
    {
        // Two ways from A to D: the short one through B, a longer one through C. Walks
        // that fail on B to D send later searches round by C.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B", "C"),
                Node("B", 10, 0, 0, "D"),
                Node("C", 10, 30, 0, "D"),
                Node("D", 20, 0, 0)
            ]
        );

        Assert.Equal(["A", "B", "D"], NavSearch.FindPath(graph, "A", "D"));

        EdgeHealth.NoteFailure(graph, "B", "D");

        Assert.Equal(["A", "C", "D"], NavSearch.FindPath(graph, "A", "D"));
        Assert.NotNull(EdgeHealth.For(graph));
    }

    [Fact]
    public void NoteFailure_UnknownNodes_MarksNothing()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0, 0)]);

        EdgeHealth.NoteFailure(graph, "A", "missing");

        Assert.Null(EdgeHealth.For(graph));
    }

    private static NavNode Node(string name, int x, int y, int z, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Connects = [.. connects]
        };
}
