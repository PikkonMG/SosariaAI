using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Memory;

/// <summary>
/// One party outing still under way, held in RAM by <see cref="AdventureTracker"/>: where it
/// began, who came along, what each one did, and the kills and deaths so far. It becomes one
/// adventure when it closes. Main game thread only.
/// </summary>
internal sealed class PartyOuting
{
    private readonly List<PersonRef> _people = [];
    private readonly Dictionary<string, string> _roles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _falls = new(StringComparer.Ordinal);

    public PartyOuting(AdventureSpot spot, object area, DateTime now)
    {
        Spot = spot;
        Area = area;
        StartedAt = now;
        LastActiveAt = now;
    }

    public AdventureSpot Spot { get; }

    /// <summary>The dungeon region the run is in, or null for a hunt above ground.</summary>
    public object Area { get; }

    public bool Dungeon => Area != null;

    public DateTime StartedAt { get; }

    /// <summary>The last fight or kill.</summary>
    public DateTime LastActiveAt { get; private set; }

    public int Kills { get; private set; }

    public int Deaths { get; private set; }

    /// <summary>What the party fought; a foe that dies to the party is a kill.</summary>
    public HashSet<Mobile> Foes { get; } = [];

    public bool Has(string personId) => personId != null && _roles.ContainsKey(personId);

    /// <summary>These people are in the party now; anyone who came along stays in the outing.</summary>
    public void Join(IReadOnlyList<PersonRef> people)
    {
        for (var i = 0; i < people.Count; i++)
        {
            if (_roles.TryAdd(people[i].Id, AdventureRoles.With))
            {
                _people.Add(people[i]);
            }
        }
    }

    public void Fought(DateTime now) => LastActiveAt = now;

    public void Killed(DateTime now)
    {
        Kills++;
        LastActiveAt = now;
    }

    /// <summary>A member died in the outing: it counts as a death, and the member stands as fallen.</summary>
    public void Fell(PersonRef person, DateTime now)
    {
        Join([person]);
        Deaths++;
        _falls[person.Id] = _falls.GetValueOrDefault(person.Id) + AdventureRules.OneDeath;
        _roles[person.Id] = AdventureRoles.Fallen;
        LastActiveAt = now;
    }

    /// <summary>A member raised or healed another in the outing; a fallen member stays fallen.</summary>
    public void Healed(PersonRef person)
    {
        if (_roles.TryGetValue(person.Id, out var role) && role == AdventureRoles.With)
        {
            _roles[person.Id] = AdventureRoles.Healer;
        }
    }

    /// <summary>The closed outing as one adventure, each member with what it did.</summary>
    public Adventure ToAdventure(DateTime endedAt)
    {
        var members = new AdventureMember[_people.Count];
        var fallen = new List<(string Name, int Times)>();

        for (var i = 0; i < _people.Count; i++)
        {
            members[i] = new AdventureMember(_people[i], _roles[_people[i].Id]);

            if (_falls.TryGetValue(_people[i].Id, out var times))
            {
                fallen.Add((_people[i].Name, times));
            }
        }

        return Spot.Make(
                AdventureRules.OutingKind(Dungeon),
                StartedAt,
                endedAt,
                AdventureRules.PartySummary(Spot.Place, Kills, fallen),
                members
            ) with
            {
                Kills = Kills,
                Deaths = Deaths
            };
    }
}
