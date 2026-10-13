using System;
using System.Collections.Generic;

namespace SosariaAI.Navigation.Generation;

public enum ResourceHit
{
    None,
    Tree,
    Ore,
    Water
}

/// <summary>
/// The harvests a walkable place offers, read from the map around it. A place can be a wood
/// or a mine, never both; it can also be a shore, since a fisher works the water beside any
/// land. Gatherers work these places, so a shard's own map, not a list of Felucca boxes,
/// says where the trees, the ore and the fish are.
/// </summary>
public static class ResourceSampler
{
    public const int SampleRadius = 24;
    public const int SampleStep = 4;
    public const int MinHits = 3;

    private static readonly IReadOnlyList<string> NoRoles = [];

    public static IReadOnlyList<string> Roles(int x, int y, int z, Func<int, int, int, ResourceHit> probe)
    {
        if (probe == null)
        {
            return NoRoles;
        }

        var trees = 0;
        var ore = 0;
        var water = 0;

        for (var dy = -SampleRadius; dy <= SampleRadius; dy += SampleStep)
        {
            for (var dx = -SampleRadius; dx <= SampleRadius; dx += SampleStep)
            {
                switch (probe(x + dx, y + dy, z))
                {
                    case ResourceHit.Tree:
                        trees++;
                        break;
                    case ResourceHit.Ore:
                        ore++;
                        break;
                    case ResourceHit.Water:
                        water++;
                        break;
                }
            }
        }

        var roles = new List<string>();

        if (trees >= MinHits && trees >= ore)
        {
            roles.Add(ResourceKind.Lumber);
        }
        else if (ore >= MinHits)
        {
            roles.Add(ResourceKind.Mine);
        }

        if (water >= MinHits)
        {
            roles.Add(ResourceKind.Fish);
        }

        return roles;
    }
}
