using System;
using System.Collections.Generic;
using System.Threading;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PathSearchTests
{
    [Fact]
    public void TryEnqueue_WhenStopped_ReturnsFalse()
    {
        PathSearch.Stop();
        Assert.False(
            PathSearch.TryEnqueue(
                new PathSearch.Job
                {
                    Graph = LineGraph(),
                    Source = "C",
                    OnComplete = (_, _, _) => { }
                }
            )
        );
    }

    [Fact]
    public void Enqueue_ExploreCompletesOffThread()
    {
        PathSearch.Start();

        try
        {
            var graph = LineGraph();
            var done = new ManualResetEventSlim(false);
            Dictionary<string, string> prev = null;
            Assert.True(
                PathSearch.TryEnqueue(
                    new PathSearch.Job
                    {
                        Graph = graph,
                        Source = "C",
                        OnComplete = (p, _, _) =>
                        {
                            prev = p;
                            done.Set();
                        }
                    }
                )
            );
            Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
            var path = Traveler.FinishPlan(
                graph,
                new Point3D(0, 0, 0),
                "C",
                prev,
                TestWalkers.Flat,
                isIndoor: null,
                avoid: null,
                out _
            );
            Assert.NotEmpty(path);
            Assert.Equal("A", path[0]);
            Assert.Equal("C", path[^1]);
        }
        finally
        {
            PathSearch.Stop();
        }
    }

    private static NavGraph LineGraph() =>
        new(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0, "C"),
                Node("C", 20, 0, 0)
            ]
        );

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
