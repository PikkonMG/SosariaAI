using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation.Generation;

public readonly record struct GraphSeed(string Name, int X, int Y, int Z, string Kind);

/// <summary>
/// Seeds merged into one place. The first seed names it; every member gives a height
/// hint, because the seeds of one spot rarely agree on its floor.
/// </summary>
public sealed record SeedCluster(GraphSeed Head, int X, int Y, IReadOnlyList<GraphSeed> Members)
{
    /// <summary>A cluster of one seed, at that seed's own tile.</summary>
    public static SeedCluster Of(GraphSeed seed) => new(seed, seed.X, seed.Y, [seed]);
}

public static class SeedMerge
{
    public const int MergeChebyshev = 8;
    public const int SnapRadius = 4;

    public static IReadOnlyList<SeedCluster> Merge(IReadOnlyList<GraphSeed> seeds)
    {
        if (seeds == null || seeds.Count == 0)
        {
            return [];
        }

        var sorted = new GraphSeed[seeds.Count];

        for (var i = 0; i < seeds.Count; i++)
        {
            sorted[i] = seeds[i];
        }

        Array.Sort(sorted, CompareSeeds);

        var parent = new int[sorted.Length];

        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (var i = 0; i < sorted.Length; i++)
        {
            for (var j = i + 1; j < sorted.Length; j++)
            {
                if (sorted[j].X - sorted[i].X > MergeChebyshev)
                {
                    break;
                }

                if (NavMetric.Chebyshev(Flat(sorted[i].X, sorted[i].Y), Flat(sorted[j].X, sorted[j].Y)) <=
                    MergeChebyshev)
                {
                    Union(parent, i, j);
                }
            }
        }

        var groups = new List<int>[sorted.Length];

        for (var i = 0; i < sorted.Length; i++)
        {
            var root = Find(parent, i);
            groups[root] ??= [];
            groups[root].Add(i);
        }

        var merged = new List<SeedCluster>();

        for (var root = 0; root < groups.Length; root++)
        {
            var group = groups[root];

            if (group == null)
            {
                continue;
            }

            merged.Add(Cluster(sorted, group));
        }

        return merged;
    }

    /// <summary>
    /// Where a cluster's node stands: the standable tile nearest the averaged point, tried
    /// at each height hint in turn and last at the ground. Null when none of them stands.
    /// </summary>
    public static (int X, int Y, int Z)? Place(
        SeedCluster cluster,
        Func<int, int, int, int?> surfaceAt,
        Func<int, int, int> groundZ
    )
    {
        if (cluster == null)
        {
            return null;
        }

        var hints = HeightHints(cluster.Members);

        for (var i = 0; i < hints.Count; i++)
        {
            if (Snap(cluster.X, cluster.Y, hints[i], surfaceAt) is { } point)
            {
                return point;
            }
        }

        return groundZ == null ? null : Snap(cluster.X, cluster.Y, groundZ(cluster.X, cluster.Y), surfaceAt);
    }

    /// <summary>
    /// The standable tile nearest (x, y) within <see cref="SnapRadius"/>, at the surface
    /// found there near <paramref name="z"/>. The asked height is only where the search
    /// looks; the returned height is the floor a walker stands on.
    /// </summary>
    public static (int X, int Y, int Z)? Snap(int x, int y, int z, Func<int, int, int, int?> surfaceAt)
    {
        if (surfaceAt == null)
        {
            return null;
        }

        if (surfaceAt(x, y, z) is { } here)
        {
            return (x, y, here);
        }

        var centre = Flat(x, y);

        for (var radius = 1; radius <= SnapRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;

                    if (NavMetric.Chebyshev(centre, Flat(nx, ny)) != radius)
                    {
                        continue;
                    }

                    if (surfaceAt(nx, ny, z) is { } surface)
                    {
                        return (nx, ny, surface);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Heights to try for a cluster: the height most members give first, the lower one on
    /// a tie. The first seed alone is no guide: a location's height can be far off (Hythloth's
    /// entrance reads 0 under a gate chamber 64 up), and a keeper upstairs sorts before the
    /// shop floor under it.
    /// </summary>
    public static IReadOnlyList<int> HeightHints(IReadOnlyList<GraphSeed> members)
    {
        if (members == null || members.Count == 0)
        {
            return [];
        }

        var counts = new Dictionary<int, int>();

        for (var i = 0; i < members.Count; i++)
        {
            var z = members[i].Z;
            counts[z] = counts.GetValueOrDefault(z) + 1;
        }

        var hints = new List<int>(counts.Keys);
        hints.Sort((a, b) =>
        {
            var byCount = counts[b].CompareTo(counts[a]);
            return byCount != 0 ? byCount : a.CompareTo(b);
        });
        return hints;
    }

    private static SeedCluster Cluster(GraphSeed[] sorted, List<int> group)
    {
        var members = new GraphSeed[group.Count];
        var sumX = 0;
        var sumY = 0;

        for (var i = 0; i < group.Count; i++)
        {
            var seed = sorted[group[i]];
            members[i] = seed;
            sumX += seed.X;
            sumY += seed.Y;
        }

        var count = group.Count;
        return new SeedCluster(members[0], sumX / count, sumY / count, members);
    }

    private static Point3D Flat(int x, int y) => new(x, y, 0);

    private static int CompareSeeds(GraphSeed a, GraphSeed b)
    {
        var byX = a.X.CompareTo(b.X);

        if (byX != 0)
        {
            return byX;
        }

        var byY = a.Y.CompareTo(b.Y);

        if (byY != 0)
        {
            return byY;
        }

        return string.CompareOrdinal(a.Name, b.Name);
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        a = Find(parent, a);
        b = Find(parent, b);

        if (a == b)
        {
            return;
        }

        if (a < b)
        {
            parent[b] = a;
        }
        else
        {
            parent[a] = b;
        }
    }
}
