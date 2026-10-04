using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SosariaAI.Navigation.Generation;

public readonly record struct TeleporterLink(
    string SrcMap,
    int Sx,
    int Sy,
    int Sz,
    string DstMap,
    int Dx,
    int Dy,
    int Dz,
    bool Back
);

public readonly record struct RegionArea(int X, int Y, int Width, int Height);

public readonly record struct RegionSeed(
    string Type,
    string Map,
    string Name,
    int X,
    int Y,
    int Z,
    IReadOnlyList<RegionArea> Areas = null
);

/// <summary>
/// Parses ModernUO teleporters.json and regions.json into seed records.
/// A true Back flag stays on the one record; the graph builder adds the reverse.
/// </summary>
public static class WorldDataSeeds
{
    public const string TownRegionToken = "TownRegion";
    public const string DungeonRegionToken = "DungeonRegion";

    /// <summary>A location in the server's data files is [x, y, z]: its length and each part's index.</summary>
    public const int LocationLength = 3;

    public const int XIndex = 0;
    public const int YIndex = 1;
    public const int ZIndex = 2;
    public const int AreaCentreZ = 0;

    private static readonly JsonSerializerOptions JsonOptions = new();

    public static IReadOnlyList<TeleporterLink> ParseTeleporters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var rows = JsonSerializer.Deserialize<List<TeleporterDto>>(json, JsonOptions);

        if (rows == null || rows.Count == 0)
        {
            return [];
        }

        var links = new List<TeleporterLink>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            if (TryMap(rows[i], out var link))
            {
                links.Add(link);
            }
        }

        return links;
    }

    public static IReadOnlyList<RegionSeed> ParseRegions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var rows = JsonSerializer.Deserialize<List<RegionDto>>(json, JsonOptions);

        if (rows == null || rows.Count == 0)
        {
            return [];
        }

        var seeds = new List<RegionSeed>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            if (TryMap(rows[i], out var seed))
            {
                seeds.Add(seed);
            }
        }

        return seeds;
    }

    /// <summary>True when a data record's map names the facet. A record with no map is on none.</summary>
    public static bool OnFacet(string map, string facet) =>
        !string.IsNullOrWhiteSpace(map) && map.Equals(facet, StringComparison.OrdinalIgnoreCase);

    public static bool IsTown(string type) =>
        !string.IsNullOrEmpty(type) && type.Contains(TownRegionToken, StringComparison.Ordinal);

    public static bool IsDungeon(string type) =>
        !string.IsNullOrEmpty(type) && type.Contains(DungeonRegionToken, StringComparison.Ordinal);

    private static bool TryMap(TeleporterDto dto, out TeleporterLink link)
    {
        link = default;

        if (dto?.Src == null || dto.Dst == null)
        {
            return false;
        }

        if (!TryReadLocation(dto.Src.Loc, out var sx, out var sy, out var sz))
        {
            return false;
        }

        if (!TryReadLocation(dto.Dst.Loc, out var dx, out var dy, out var dz))
        {
            return false;
        }

        link = new TeleporterLink(
            dto.Src.Map ?? string.Empty,
            sx,
            sy,
            sz,
            dto.Dst.Map ?? string.Empty,
            dx,
            dy,
            dz,
            dto.Back
        );
        return true;
    }

    private static bool TryMap(RegionDto dto, out RegionSeed seed)
    {
        seed = default;

        if (dto == null || !TryReadRegionLocation(dto, out var x, out var y, out var z))
        {
            return false;
        }

        seed = new RegionSeed(
            dto.Type ?? string.Empty,
            dto.Map ?? string.Empty,
            dto.Name ?? string.Empty,
            x,
            y,
            z,
            Areas(dto.Area)
        );
        return true;
    }

    private static bool TryReadRegionLocation(RegionDto dto, out int x, out int y, out int z)
    {
        if (dto.GoLocation != null)
        {
            x = dto.GoLocation.X;
            y = dto.GoLocation.Y;
            z = dto.GoLocation.Z;
            return true;
        }

        if (dto.Entrance != null)
        {
            x = dto.Entrance.X;
            y = dto.Entrance.Y;
            z = dto.Entrance.Z;
            return true;
        }

        if (dto.Area is { Count: > 0 } && dto.Area[0] != null)
        {
            var area = dto.Area[0];
            x = (area.X1 + area.X2) / 2;
            y = (area.Y1 + area.Y2) / 2;
            z = AreaCentreZ;
            return true;
        }

        x = 0;
        y = 0;
        z = 0;
        return false;
    }

    private static bool TryReadLocation(int[] loc, out int x, out int y, out int z)
    {
        if (loc == null || loc.Length < LocationLength)
        {
            x = 0;
            y = 0;
            z = 0;
            return false;
        }

        x = loc[XIndex];
        y = loc[YIndex];
        z = loc[ZIndex];
        return true;
    }

    private sealed class TeleporterDto
    {
        [JsonPropertyName("src")]
        public EndpointDto Src { get; set; }

        [JsonPropertyName("dst")]
        public EndpointDto Dst { get; set; }

        [JsonPropertyName("back")]
        public bool Back { get; set; }
    }

    private sealed class EndpointDto
    {
        [JsonPropertyName("map")]
        public string Map { get; set; }

        [JsonPropertyName("loc")]
        public int[] Loc { get; set; }
    }

    private sealed class RegionDto
    {
        [JsonPropertyName("$type")]
        public string Type { get; set; }

        [JsonPropertyName("Map")]
        public string Map { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("Area")]
        public List<AreaDto> Area { get; set; }

        [JsonPropertyName("GoLocation")]
        public PointDto GoLocation { get; set; }

        [JsonPropertyName("Entrance")]
        public PointDto Entrance { get; set; }
    }

    private static IReadOnlyList<RegionArea> Areas(List<AreaDto> areas)
    {
        var found = new List<RegionArea>();

        if (areas == null)
        {
            return found;
        }

        for (var i = 0; i < areas.Count; i++)
        {
            var area = areas[i];

            if (area == null)
            {
                continue;
            }

            var x = Math.Min(area.X1, area.X2);
            var y = Math.Min(area.Y1, area.Y2);
            found.Add(new RegionArea(x, y, Math.Abs(area.X2 - area.X1), Math.Abs(area.Y2 - area.Y1)));
        }

        return found;
    }

    private sealed class AreaDto
    {
        [JsonPropertyName("x1")]
        public int X1 { get; set; }

        [JsonPropertyName("y1")]
        public int Y1 { get; set; }

        [JsonPropertyName("x2")]
        public int X2 { get; set; }

        [JsonPropertyName("y2")]
        public int Y2 { get; set; }
    }

    private sealed class PointDto
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("z")]
        public int Z { get; set; }
    }
}
