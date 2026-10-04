using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Memory;

/// <summary>Where an adventure happened: the place as people name it, the facet, and the tile.</summary>
public readonly record struct AdventureSpot(string Place, string Map, int X, int Y)
{
    /// <summary>Where this mobile stands now. Main game thread: it reads regions and items.</summary>
    public static AdventureSpot Of(Mobile mobile) =>
        new(
            PlaceNames.Of(mobile),
            People.InWorld(mobile) ? mobile.Map.Name : string.Empty,
            mobile?.X ?? 0,
            mobile?.Y ?? 0
        );

    /// <summary>An adventure here with these fields; the store sets its id and weight.</summary>
    public Adventure Make(string kind, DateTime startedAt, DateTime endedAt, string summary, AdventureMember[] members) =>
        new()
        {
            Kind = kind,
            Place = Place,
            Map = Map,
            X = X,
            Y = Y,
            StartedAt = startedAt,
            EndedAt = endedAt,
            Summary = summary,
            Members = members
        };
}
