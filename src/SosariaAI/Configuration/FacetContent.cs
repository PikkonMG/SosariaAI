using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

/// <summary>
/// One facet's authored content. An older file's "routes" block of hand-made waypoints is
/// skipped as the file is read: every trip travels by the graph.
/// </summary>
public sealed class FacetContent
{
    [JsonPropertyName("areas")]
    public Dictionary<string, AreaDefinition> Areas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("parties")]
    public List<PartyDefinition> Parties { get; set; } = [];

    [JsonPropertyName("roster")]
    public List<CharacterDefinition> Roster { get; set; } = [];

    [JsonIgnore]
    public bool HasRoster
    {
        get
        {
            if (Roster == null || Roster.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < Roster.Count; i++)
            {
                if (Roster[i] != null && !string.IsNullOrWhiteSpace(Roster[i].Id))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Normalize()
    {
        Areas = FacetNames.Copy(Areas);
        Parties ??= [];
        Roster ??= [];
    }

    public CharacterDefinition FindRoster(string templateId)
    {
        if (Roster == null || string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        for (var i = 0; i < Roster.Count; i++)
        {
            var item = Roster[i];

            if (item != null && string.Equals(item.Id, templateId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    public Rectangle2D ResolveArea(AreaDefinition area)
    {
        if (area == null)
        {
            return default;
        }

        if (area.IsNamed && Areas != null && Areas.TryGetValue(area.Name, out var named) && named != null)
        {
            return named.ToRectangle();
        }

        return area.ToRectangle();
    }
}
