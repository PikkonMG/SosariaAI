using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class BritainCoverageTests
{
    private static readonly Point3D Minoc = new(2471, 439, 15);
    private static readonly Point3D Trinsic = new(1914, 2717, 20);
    private static readonly Point3D DespiseGate = new(1298, 1080, 0);
    private const int SeedStride = NavLimits.SoftLegDistance;
    private const string SeedKind = "";

    [Fact]
    public void BuildGraph_BritainBankExists_AndPlanToMinocIsNonEmpty()
    {
        var graph = WorldGenerator.BuildGraph(FacetNames.Felucca, BritainSeeds(), [], TestWalkers.Flat);

        var bank = graph.FindNearest(CharactersFile.DefaultBankSpot);
        Assert.NotNull(bank);
        Assert.Equal(CharactersFile.DefaultBankSpot.X, bank.X);
        Assert.Equal(CharactersFile.DefaultBankSpot.Y, bank.Y);

        var minoc = graph.FindNearest(Minoc);
        Assert.NotNull(minoc);

        var path = NavSearch.FindPath(graph, bank.Name, minoc.Name);
        Assert.NotEmpty(path);
        Assert.True(graph.SameComponent(bank.Name, minoc.Name));
    }

    private static IReadOnlyList<GraphSeed> BritainSeeds()
    {
        var seeds = new List<GraphSeed>
        {
            Seed("britain-bank", CharactersFile.DefaultBankSpot),
            Seed("britain-graveyard", CharactersFile.GraveyardGoPoint),
            Seed("despise-gate", DespiseGate),
            Seed("britain-smith", CharactersFile.HalBlacksmith),
            Seed("britain-mine", CharactersFile.MiraMineApproach),
            Seed("britain-forest", CharactersFile.DefaultForestApproach),
            Seed("britain-shore", CharactersFile.TobinShore),
            Seed("minoc", Minoc),
            Seed("trinsic", Trinsic)
        };

        AddStride(seeds, CharactersFile.DefaultBankSpot, Minoc, SeedStride);
        return seeds;
    }

    private static void AddStride(List<GraphSeed> seeds, Point3D from, Point3D to, int stride)
    {
        var dist = NavMetric.Chebyshev(from, to);

        if (dist == 0)
        {
            return;
        }

        for (var step = 0; step <= dist; step += stride)
        {
            seeds.Add(Seed($"hop-{step}", Lerp(from, to, step, dist)));
        }

        if (dist % stride != 0)
        {
            seeds.Add(Seed("hop-end", to));
        }
    }

    private static Point3D Lerp(Point3D from, Point3D to, int step, int dist)
    {
        if (dist == 0)
        {
            return from;
        }

        return new Point3D(Lerp(from.X, to.X, step, dist), Lerp(from.Y, to.Y, step, dist), Lerp(from.Z, to.Z, step, dist));
    }

    private static int Lerp(int from, int to, int step, int dist) =>
        from + (int)((long)(to - from) * step / dist);

    private static GraphSeed Seed(string name, Point3D point) =>
        new(name, point.X, point.Y, point.Z, SeedKind);
}
