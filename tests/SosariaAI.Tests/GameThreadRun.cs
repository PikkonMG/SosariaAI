using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

// The engine and the plugin run on the one game thread. The world's entity tables, the map
// sectors, the movement check and the plugin's caches are plain statics with no lock, and every
// test class shares them in this process. Test classes run one at a time, as the game thread
// would run them: two in parallel corrupt the tables and read each other's state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: TestCollectionOrderer(
    "SosariaAI.Tests." + nameof(SosariaAI.Tests.LoneCollectionsLast),
    "SosariaAI.Tests"
)]

namespace SosariaAI.Tests;

/// <summary>
/// Runs the collections that run alone after the rest, as xUnit runs them when classes run in
/// parallel. They set process state for good: the real map registers every facet, and the
/// engine's moongate lists then name live gates, so a class that ran after them met a world
/// that is no longer offline.
/// </summary>
public sealed class LoneCollectionsLast : ITestCollectionOrderer
{
    private const string RunsAloneArgument = nameof(CollectionDefinitionAttribute.DisableParallelization);

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections.OrderBy(RunsAlone);

    private static bool RunsAlone(ITestCollection collection) =>
        collection.CollectionDefinition?
            .GetCustomAttributes(typeof(CollectionDefinitionAttribute))
            .FirstOrDefault()?
            .GetNamedArgument<bool>(RunsAloneArgument) == true;
}
