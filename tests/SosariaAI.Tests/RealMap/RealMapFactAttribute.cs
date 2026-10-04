using System;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Tests on the real map share global engine state (tile data, maps, the movement check),
/// so they run alone, after the parallel tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealMapCollection
{
    public const string Name = "Real map";
}

/// <summary>
/// A fact that runs on the real map, and skips when the UO data is not there. A full
/// facet build takes about five minutes, so it also waits for
/// <see cref="FullBuildEnvironmentVariable"/> set to 1.
/// </summary>
public sealed class RealMapFactAttribute : FactAttribute
{
    public const string FullBuildEnvironmentVariable = "SOSARIAAI_FULL_NAV";
    private const string FullBuildOn = "1";

    public RealMapFactAttribute(bool fullBuild = false)
    {
        if (!RealMapWorld.Available)
        {
            Skip = RealMapWorld.SkipReason;
        }
        else if (fullBuild && Environment.GetEnvironmentVariable(FullBuildEnvironmentVariable) != FullBuildOn)
        {
            Skip = $"Full facet build: set {FullBuildEnvironmentVariable}={FullBuildOn}.";
        }
    }
}
