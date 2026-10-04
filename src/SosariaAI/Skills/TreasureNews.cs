using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// True treasure stories for the shard's event journal, so gossip at the bank can retell a
/// chest someone really dug up or a bottle someone really reeled in.
/// </summary>
public static class TreasureNews
{
    /// <summary>A treasure chest came up out of the ground: the digger is the actor.</summary>
    public static void ChestDug(SosariaCharacter digger, Point3D chestAt) =>
        Record(digger, ShardEventType.Treasure, chestAt);

    /// <summary>A fisherman reeled in a bottle or a sodden map: the fisherman is the actor.</summary>
    public static void SeaFind(SosariaCharacter fisherman) =>
        Record(fisherman, ShardEventType.SeaFind, fisherman?.Location ?? Point3D.Zero);

    /// <summary>The journal entry for one story. Pure.</summary>
    public static ShardEvent EventFor(string type, string actor, string place, string facet, Point3D at, DateTime now) =>
        new()
        {
            At = now,
            Type = type,
            Actor = actor,
            Place = place,
            Facet = facet,
            X = at.X,
            Y = at.Y,
            Z = at.Z
        };

    private static void Record(SosariaCharacter actor, string type, Point3D at)
    {
        if (actor == null)
        {
            return;
        }

        SosariaSettings.Journal?.Record(
            EventFor(type, actor.Name, PlaceNames.Of(at, actor.Map), actor.HomeFacet, at, Core.Now)
        );
    }
}
