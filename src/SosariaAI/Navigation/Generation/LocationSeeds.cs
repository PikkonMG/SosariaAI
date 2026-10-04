using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SosariaAI.Navigation.Generation;

public readonly record struct NamedSeed(string Name, string Group, string Facet, int X, int Y, int Z);

/// <summary>
/// Reads ModernUO location JSON. Categories nest. Each location is a name and [x, y, z].
/// Group is the category path joined by "/". The root name is the facet.
/// </summary>
public static class LocationSeeds
{
    private const string NameProperty = "name";
    private const string CategoriesProperty = "categories";
    private const string LocationsProperty = "locations";
    private const string LocationProperty = "location";
    private const string GroupSeparator = "/";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static IReadOnlyList<NamedSeed> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            return Collect(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<NamedSeed> LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        return Parse(File.ReadAllText(path));
    }

    private static IReadOnlyList<NamedSeed> Collect(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var seeds = new List<NamedSeed>();
        var facet = ReadString(root, NameProperty) ?? string.Empty;
        CollectCategory(root, facet, string.Empty, seeds);
        return seeds;
    }

    private static void CollectCategory(JsonElement category, string facet, string group, List<NamedSeed> seeds)
    {
        if (TryGetArray(category, LocationsProperty, out var locations))
        {
            foreach (var location in locations.EnumerateArray())
            {
                if (TryReadSeed(location, facet, group, out var seed))
                {
                    seeds.Add(seed);
                }
            }
        }

        if (!TryGetArray(category, CategoriesProperty, out var categories))
        {
            return;
        }

        foreach (var child in categories.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var childGroup = AppendGroup(group, ReadString(child, NameProperty));
            CollectCategory(child, facet, childGroup, seeds);
        }
    }

    private static bool TryReadSeed(JsonElement entry, string facet, string group, out NamedSeed seed)
    {
        seed = default;

        if (entry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var name = ReadString(entry, NameProperty);

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!entry.TryGetProperty(LocationProperty, out var location) ||
            location.ValueKind != JsonValueKind.Array ||
            location.GetArrayLength() != WorldDataSeeds.LocationLength)
        {
            return false;
        }

        if (!TryReadInt(location[WorldDataSeeds.XIndex], out var x) ||
            !TryReadInt(location[WorldDataSeeds.YIndex], out var y) ||
            !TryReadInt(location[WorldDataSeeds.ZIndex], out var z))
        {
            return false;
        }

        seed = new NamedSeed(name, group ?? string.Empty, facet ?? string.Empty, x, y, z);
        return true;
    }

    private static string AppendGroup(string group, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return group ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(group))
        {
            return name;
        }

        return string.Concat(group, GroupSeparator, name);
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool TryGetArray(JsonElement element, string property, out JsonElement array)
    {
        if (element.TryGetProperty(property, out array) && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
