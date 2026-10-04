using System;

namespace SosariaAI.Navigation.Generation;

public enum ResourceHit
{
    None,
    Tree,
    Ore
}

public static class ResourceSampler
{
    public const int SampleRadius = 24;
    public const int SampleStep = 4;
    public const int MinHits = 3;

    public static string Classify(int x, int y, int z, Func<int, int, int, ResourceHit> probe)
    {
        if (probe == null)
        {
            return null;
        }

        var trees = 0;
        var ore = 0;

        for (var dy = -SampleRadius; dy <= SampleRadius; dy += SampleStep)
        {
            for (var dx = -SampleRadius; dx <= SampleRadius; dx += SampleStep)
            {
                var hit = probe(x + dx, y + dy, z);

                if (hit == ResourceHit.Tree)
                {
                    trees++;
                }
                else if (hit == ResourceHit.Ore)
                {
                    ore++;
                }
            }
        }

        if (trees >= MinHits && trees >= ore)
        {
            return ResourceKind.Lumber;
        }

        if (ore >= MinHits)
        {
            return ResourceKind.Mine;
        }

        return null;
    }
}
