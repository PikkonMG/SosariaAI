using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>Where a group goes: a dungeon hall or a hunt ground, and the word people use for it.</summary>
public sealed record LfgTarget(bool Dungeon, Point3D At, string Place);

/// <summary>One open group call: who leads, where it goes, how many it wants, and who joined.</summary>
public sealed class LfgGroup
{
    public LfgGroup(Serial leader, LfgTarget target, Map map, Point3D meetAt, DateTime shoutedAt, int size, TimeSpan window)
    {
        Leader = leader;
        Target = target;
        Map = map;
        MeetAt = meetAt;
        ShoutedAt = shoutedAt;
        Size = size;
        Window = window;
    }

    /// <summary>The one who called; a member who takes over after the leader dies leads from then on.</summary>
    public Serial Leader { get; set; }

    /// <summary>Where the group goes; a crew too light for the called place goes where it fits at the muster.</summary>
    public LfgTarget Target { get; set; }

    public string Place => Target.Place;

    public Map Map { get; }

    public Point3D MeetAt { get; }

    public DateTime ShoutedAt { get; }

    /// <summary>The whole group wanted, leader included.</summary>
    public int Size { get; }

    /// <summary>How long the leader waits for people to answer.</summary>
    public TimeSpan Window { get; }

    /// <summary>Default while recruiting; set when enough joined and the group gathers round the leader.</summary>
    public DateTime MusterSince { get; set; }

    public bool Mustering => MusterSince != default && !Running;

    /// <summary>Default until the group sets out.</summary>
    public DateTime RunningSince { get; set; }

    public bool Running => RunningSince != default;

    /// <summary>The run the leader walks; members follow it.</summary>
    public Skills.Skill Trip { get; set; }

    /// <summary>The last time the leader was seen on its run.</summary>
    public DateTime LastOnTrip { get; set; }

    /// <summary>How often the leader was put back on the run after something pulled it away.</summary>
    public int Restarts { get; set; }

    /// <summary>When the leader was first seen dead on the run; default while it lives.</summary>
    public DateTime LeaderDownAt { get; set; }

    /// <summary>The dungeon floor the leader last stood on, for the "entered" line.</summary>
    public (string Dungeon, int Level)? Floor { get; set; }

    /// <summary>The run reached a dungeon floor or lasted long enough to count.</summary>
    public bool Fought { get; set; }

    /// <summary>Someone in the group can open a gate toward the run: the leader at the call, or one who joined.</summary>
    public bool HasGater { get; set; }

    /// <summary>A nearby player was already asked to come on this call.</summary>
    public bool AskedPlayer { get; set; }

    /// <summary>Characters and players who joined, leader not included.</summary>
    public List<Serial> Joined { get; } = [];

    /// <summary>Characters who already rolled on this call, joined or not.</summary>
    public HashSet<Serial> Answered { get; } = [];

    /// <summary>Guildmates who said they are coming: they join on arrival without another roll.</summary>
    public HashSet<Serial> Promised { get; } = [];
}
