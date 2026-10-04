using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Server.Json;
using SosariaAI.Configuration;

namespace SosariaAI.Navigation;

public static class NavStore
{
    private const string NavFolder = "nav";
    private const string DestinationsPrefix = "destinations-";

    public static string DefaultDirectory => ConfigFile.PathIn(NavFolder);

    public static void Save(NavGraph graph)
    {
        if (graph == null)
        {
            return;
        }

        WriteGraph(graph.Facet, graph.Nodes);
    }

    public static bool TryLoad(string facet, out NavGraph graph)
    {
        graph = null;

        if (string.IsNullOrWhiteSpace(facet))
        {
            return false;
        }

        var path = Path.Combine(DefaultDirectory, GraphFileName(facet));

        if (!File.Exists(path))
        {
            return false;
        }

        graph = FromFile(JsonConfig.Deserialize<NavFile>(path), facet);
        return graph != null;
    }

    /// <summary>
    /// The graph a nav file holds. A walking link longer than a leg becomes a teleporter
    /// (<see cref="NavGates.InferLongConnects"/>). A file with no facet takes
    /// <paramref name="facet"/>. Null when the file holds no node list.
    /// </summary>
    internal static NavGraph FromFile(NavFile file, string facet)
    {
        if (file?.Nodes == null)
        {
            return null;
        }

        NavGates.InferLongConnects(file.Nodes);

        var loadedFacet = string.IsNullOrWhiteSpace(file.Facet) ? facet ?? string.Empty : file.Facet;
        return new NavGraph(loadedFacet, file.Nodes);
    }

    public static bool TryLoadDestinations(string facet, out DestinationCatalog catalog)
    {
        catalog = null;

        if (string.IsNullOrWhiteSpace(facet))
        {
            return false;
        }

        var path = Path.Combine(DefaultDirectory, DestinationsFileName(facet));

        if (!File.Exists(path))
        {
            return false;
        }

        var file = JsonConfig.Deserialize<DestinationFile>(path);

        if (file?.Destinations == null)
        {
            return false;
        }

        catalog = new DestinationCatalog(file.Destinations);
        return true;
    }

    public static void SaveDestinations(string facet, DestinationCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(facet) || catalog == null)
        {
            return;
        }

        WriteDestinations(facet, catalog.All);
    }

    private static void WriteDestinations(string facet, IReadOnlyList<Destination> destinations)
    {
        Directory.CreateDirectory(DefaultDirectory);
        var file = new DestinationFile
        {
            Facet = facet,
            Destinations = destinations == null ? [] : [.. destinations]
        };
        JsonConfig.Serialize(Path.Combine(DefaultDirectory, DestinationsFileName(facet)), file);
    }

    private static void WriteGraph(string facet, IEnumerable<NavNode> nodes)
    {
        Directory.CreateDirectory(DefaultDirectory);
        var file = new NavFile
        {
            Format = NavFile.CurrentFormat,
            Facet = facet ?? string.Empty,
            Nodes = nodes == null ? [] : [.. nodes]
        };
        JsonConfig.Serialize(Path.Combine(DefaultDirectory, GraphFileName(facet)), file);
    }

    private static string GraphFileName(string facet) =>
        $"{NormalizeFacet(facet)}{ConfigFile.FileExtension}";

    private static string DestinationsFileName(string facet) =>
        $"{DestinationsPrefix}{NormalizeFacet(facet)}{ConfigFile.FileExtension}";

    private static string NormalizeFacet(string facet) =>
        string.IsNullOrWhiteSpace(facet) ? FacetNames.Felucca.ToLowerInvariant() : facet.Trim().ToLowerInvariant();
}

public sealed class DestinationFile
{
    [JsonPropertyName("facet")]
    public string Facet { get; set; }

    [JsonPropertyName("destinations")]
    public List<Destination> Destinations { get; set; } = [];
}
