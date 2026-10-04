using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Mobiles;
using EngineParty = Server.Engines.PartySystem.Party;
using Timer = Server.Timer;

namespace SosariaAI.Memory;

/// <summary>
/// Makes adventures and hands them to a <see cref="MemoryStore"/>. A party outing opens when a
/// party with at least one SosariaAI character in it, real players included, enters a dungeon
/// or starts a fight. It counts the foes that die to the party and the members who fall, and
/// closes when the party leaves the dungeon, splits, or has no fight for
/// <see cref="AdventureRules.OutingIdleLimit"/>. The big moments (a death, a red put down, a
/// rescue, a duel, a house, a first kill) and the solo outings are written at once. A closed
/// adventure is written once; single steps are never stored. Open outings live in RAM only.
/// Main game thread only.
/// </summary>
public sealed partial class AdventureTracker
{
    /// <summary>How often open outings are checked and real players' deaths are looked for.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    public static AdventureTracker Shared { get; } = new(MemoryStore.Shared);

    private readonly MemoryStore _store;
    private readonly Dictionary<object, PartyOuting> _outings = new();
    private Timer _timer;

    public AdventureTracker(MemoryStore store) => _store = store;

    /// <summary>Starts watching fights and the clock. Call once the world is loaded.</summary>
    public void Start()
    {
        Stop();
        EventSink.AggressiveAction += OnAggressiveAction;
        _timer = Timer.DelayCall(TickInterval, TickInterval, TickNow);
    }

    /// <summary>Stops watching and writes every outing still under way, as on a shutdown.</summary>
    public void Stop()
    {
        EventSink.AggressiveAction -= OnAggressiveAction;
        _timer?.Stop();
        _timer = null;
        CloseAll(Core.Now);
    }

    /// <summary>A party member walked into or out of a region: a party that entered a dungeon starts a run.</summary>
    public void Moved(Mobile member) => See(PartyOf(member), false, Core.Now);

    /// <summary>A party is done (an LFG run finished): its outing closes now, with everyone who came.</summary>
    public void PartyFinished(EngineParty party, DateTime now)
    {
        if (party == null || !_outings.TryGetValue(party, out var outing))
        {
            return;
        }

        outing.Join(PeopleIn(party));
        Close(party, now);
    }

    /// <summary>Checks every open outing: kills, a split, a party that left, a party gone quiet. Then real players' deaths.</summary>
    public void Tick(DateTime now)
    {
        var parties = new List<object>(_outings.Keys);

        for (var i = 0; i < parties.Count; i++)
        {
            if (parties[i] is EngineParty party && _outings.TryGetValue(party, out var running))
            {
                if (!party.Active || !HasCharacter(party))
                {
                    Close(party, now);
                    continue;
                }

                CountKills(running, now);
                See(party, false, now);
            }

            if (_outings.TryGetValue(parties[i], out var outing) && now - outing.LastActiveAt >= AdventureRules.OutingIdleLimit)
            {
                Close(parties[i], now);
            }
        }

        WatchPlayers(now);
        ForgetOldRescues(now);
    }

    /// <summary>Writes every outing still under way.</summary>
    public void CloseAll(DateTime now)
    {
        var parties = new List<object>(_outings.Keys);

        for (var i = 0; i < parties.Count; i++)
        {
            Close(parties[i], now);
        }
    }

    /// <summary>The people in a party: SosariaAI characters and real players, creatures left out.</summary>
    public static List<PersonRef> PeopleIn(EngineParty party)
    {
        var people = new List<PersonRef>();

        for (var i = 0; party != null && i < party.Members.Count; i++)
        {
            if (PersonRef.Of(party.Members[i].Mobile) is { } person)
            {
                people.Add(person);
            }
        }

        return people;
    }

    /// <summary>
    /// A party was seen: <paramref name="area"/> is the dungeon region it stands in, or null
    /// above ground. Opens an outing when the party is in a dungeon or fighting and has a
    /// SosariaAI character in it; closes the open one when the party left its dungeon or a hunt
    /// went underground. The spot is named only for a new outing: naming reads regions and items.
    /// </summary>
    internal void SeeParty(object party, IReadOnlyList<PersonRef> people, Func<AdventureSpot> spotOf, object area, bool fighting, DateTime now)
    {
        if (!GoesOn(party, people, area, fighting, now) && Opens(area, fighting))
        {
            Open(party, people, spotOf(), area, now);
        }
    }

    // The open outing goes on while the party stays in its dungeon, or hunts above ground; it
    // closes when the party left the dungeon or a hunt went underground.
    private bool GoesOn(object party, IReadOnlyList<PersonRef> people, object area, bool fighting, DateTime now)
    {
        if (!_outings.TryGetValue(party, out var outing))
        {
            return false;
        }

        if (outing.Dungeon ? !Equals(outing.Area, area) : area != null)
        {
            Close(party, now);
            return false;
        }

        outing.Join(people);

        if (fighting)
        {
            outing.Fought(now);
        }

        return true;
    }

    // A party in a dungeon, or in a fight, starts an outing.
    private static bool Opens(object area, bool fighting) => area != null || fighting;

    private void Open(object party, IReadOnlyList<PersonRef> people, AdventureSpot spot, object area, DateTime now)
    {
        if (!HasBot(people))
        {
            return;
        }

        var outing = new PartyOuting(spot, area, now);
        outing.Join(people);
        _outings[party] = outing;
    }

    /// <summary>A member of the party died.</summary>
    internal void NoteFell(object party, PersonRef person, DateTime now)
    {
        if (party != null && _outings.TryGetValue(party, out var outing))
        {
            outing.Fell(person, now);
        }
    }

    /// <summary>Ends the party's outing and keeps it when it is worth keeping.</summary>
    private void Close(object party, DateTime now)
    {
        if (!_outings.Remove(party, out var outing))
        {
            return;
        }

        if (AdventureRules.OutingWorthKeeping(outing.Dungeon, outing.Kills, outing.Deaths))
        {
            _store.Record(outing.ToAdventure(now));
        }
    }

    private void TickNow() => Tick(Core.Now);

    private void OnAggressiveAction(AggressiveActionEventArgs e)
    {
        var now = Core.Now;
        Fight(e.Aggressor, e.Aggressed, now);
        Fight(e.Aggressed, e.Aggressor, now);
    }

    /// <summary>One side of a fight: the side's party fought the foe. A pet fights for its master's party.</summary>
    internal void Fight(Mobile side, Mobile foe, DateTime now)
    {
        var party = PartyOf(side is BaseCreature creature ? creature.GetMaster() ?? side : side);

        if (party == null || foe == null || party.Contains(foe))
        {
            return;
        }

        See(party, true, now);

        if (_outings.TryGetValue(party, out var outing))
        {
            outing.Foes.Add(foe);
        }
    }

    // Where the party is: a member inside the open run's dungeon, else any member in a dungeon,
    // else the leader.
    private void See(EngineParty party, bool fighting, DateTime now)
    {
        if (party == null)
        {
            return;
        }

        var open = _outings.TryGetValue(party, out var outing) ? outing.Area : null;
        Mobile anchor = null;
        DungeonRegion area = null;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;

            if (member is not { Deleted: false } || !People.InWorld(member))
            {
                continue;
            }

            var dungeon = member.Region?.GetRegion<DungeonRegion>();

            if (dungeon != null && Equals(dungeon, open))
            {
                anchor = member;
                area = dungeon;
                break;
            }

            if (dungeon != null && area == null)
            {
                anchor = member;
                area = dungeon;
            }
            else if (area == null && (anchor == null || member == party.Leader))
            {
                anchor = member;
            }
        }

        if (anchor == null)
        {
            return;
        }

        SeeParty(party, PeopleIn(party), () => AdventureSpot.Of(anchor), area, fighting, now);
    }

    // A foe that is gone or dead since the last look: a kill when one of the outing's people, or
    // a pet of one, landed it.
    private static void CountKills(PartyOuting outing, DateTime now)
    {
        List<Mobile> gone = null;

        foreach (var foe in outing.Foes)
        {
            if (!foe.Deleted && foe.Alive)
            {
                continue;
            }

            (gone ??= []).Add(foe);

            if (PersonRef.Of(Behind(foe.LastKiller, foe)) is { } killer && outing.Has(killer.Id))
            {
                outing.Killed(now);
            }
        }

        for (var i = 0; gone != null && i < gone.Count; i++)
        {
            outing.Foes.Remove(gone[i]);
        }
    }

    // An active party with a SosariaAI character in it, else null.
    private static EngineParty PartyOf(Mobile member) =>
        EngineParty.Get(member) is { Active: true } party && HasCharacter(party) ? party : null;

    private static bool HasCharacter(EngineParty party)
    {
        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is SosariaCharacter)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBot(IReadOnlyList<PersonRef> people)
    {
        for (var i = 0; i < people.Count; i++)
        {
            if (people[i].IsBot)
            {
                return true;
            }
        }

        return false;
    }

    // The one a blow counts for: a pet's or a summon's master, else the mobile itself.
    private static Mobile Behind(Mobile mobile, Mobile damagee) => mobile?.GetDamageMaster(damagee) ?? mobile;
}
