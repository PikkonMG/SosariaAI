using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Social;

/// <summary>
/// Writes what really happened into the shard's event journal, so gossip retells only true
/// stories. Deaths, murders and guild wars are journaled where they happen on the character;
/// these are the other kinds: a lift, a red seen, a red put down, a party run and a duel.
/// </summary>
public static class ShardNews
{
    /// <summary>One red is journaled once per this time: a sighting is told once, not a stream.</summary>
    public static readonly TimeSpan RedSightingRest = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<Serial, DateTime> LastRedSighting = new();

    /// <summary>
    /// A lift from a person: the thief is the actor, the mark the other. A lift nobody noticed
    /// is told by the mark as the work of someone.
    /// </summary>
    public static void Theft(Mobile thief, Mobile mark, bool noticed)
    {
        if (thief == null || mark == null)
        {
            return;
        }

        Record(ShardEventType.Theft, noticed ? thief.Name : ShardEvent.SomeoneName, mark.Name, mark, Core.Now);
    }

    /// <summary>
    /// A red was seen: the red is the actor, the witness the other. The journal warms the danger
    /// map where it stood, so the caller needs no second note.
    /// </summary>
    public static void RedSeen(Mobile red, Mobile witness, DateTime now)
    {
        if (red == null || witness == null)
        {
            return;
        }

        if (!SightingRested(LastRedSighting.GetValueOrDefault(red.Serial), now))
        {
            return;
        }

        LastRedSighting[red.Serial] = now;
        Record(ShardEventType.Red, red.Name, witness.Name, red, now, FacetOf(witness));
    }

    /// <summary>
    /// A group ran a dungeon or hunt together: the leader is the actor, the members the others.
    /// The place is where the group went (<paramref name="ranAt"/>), not where it broke up.
    /// </summary>
    public static void PartyRun(Mobile leader, IReadOnlyList<string> memberNames, Point3D ranAt, DateTime now)
    {
        if (leader == null || memberNames is not { Count: > 0 })
        {
            return;
        }

        Record(
            ShardEventType.Party,
            leader.Name,
            string.Join(ShardEvent.NameSeparator, memberNames),
            ranAt,
            leader.Map,
            FacetOf(leader),
            now
        );
    }

    /// <summary>
    /// A red was put down: the red is the actor, every non-red person who hurt it the others.
    /// <paramref name="at"/> is where it fell, so a moongate fight is told at the gate.
    /// </summary>
    public static void RedKill(Mobile red, IReadOnlyList<string> killerNames, Point3D at, DateTime now)
    {
        if (red == null || killerNames is not { Count: > 0 })
        {
            return;
        }

        Record(ShardEventType.RedKill, red.Name, string.Join(ShardEvent.NameSeparator, killerNames), at, red.Map, FacetOf(red), now);
    }

    /// <summary>
    /// How a death is journaled: killed by a player or a PK it is a murder (<see cref="ShardEventType.Pk"/>),
    /// else a death. A red that dies is never murdered: killing a red is no crime.
    /// </summary>
    public static string DeathType(bool deadIsRed, bool killedByPlayerOrPk) =>
        killedByPlayerOrPk && !deadIsRed ? ShardEventType.Pk : ShardEventType.Death;

    /// <summary>A friendly duel ended on the floor: the winner is the actor, the loser the other.</summary>
    public static void Duel(Mobile winner, Mobile loser, DateTime now)
    {
        if (winner == null || loser == null)
        {
            return;
        }

        Record(ShardEventType.Duel, winner.Name, loser.Name, winner, now);
    }

    public static bool SightingRested(DateTime last, DateTime now) =>
        TimeRules.Rested(last, now, RedSightingRest);

    private static void Record(string type, string actor, string other, Mobile at, DateTime now, string facet = null) =>
        Record(type, actor, other, at.Location, at.Map, facet ?? FacetOf(at), now);

    private static void Record(string type, string actor, string other, Point3D at, Map map, string facet, DateTime now) =>
        SosariaSettings.Journal?.Record(
            new ShardEvent
            {
                At = now,
                Type = type,
                Actor = actor,
                Other = other,
                Place = PlaceNames.Of(at, map),
                Facet = facet,
                X = at.X,
                Y = at.Y,
                Z = at.Z
            }
        );

    private static string FacetOf(Mobile mobile) =>
        mobile is SosariaCharacter character ? character.HomeFacet : mobile.Map?.Name;
}
