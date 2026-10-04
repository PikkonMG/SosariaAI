using System;
using System.Collections.Generic;
using Server;
using Server.Collections;
using Server.Network;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;
using EngineParty = Server.Engines.PartySystem.Party;

namespace SosariaAI.Memory;

/// <summary>The big moments and the solo outings: each one is written the moment it happens.</summary>
public sealed partial class AdventureTracker
{
    private readonly Dictionary<(string Helper, string Helped), DateTime> _rescuedAt = new();
    private Dictionary<Serial, bool> _playersAlive = new();

    /// <summary>
    /// A SosariaAI character or a real player died. The dead is <c>fallen</c>, the person
    /// behind the kill is <c>against</c>, and party members there stand <c>with</c>. A creature
    /// kill names the creature in the summary only. The death counts in the party's outing, and
    /// a red's death is also a red kill for the people who put it down.
    /// <paramref name="killerPerson"/> was taken when the killer was set, so a killer that has
    /// left the world since is still named.
    /// </summary>
    public void PersonDied(Mobile dead, Mobile killer, PersonRef? killerPerson, DateTime now)
    {
        if (PersonRef.Of(dead) is not { } fallen)
        {
            return;
        }

        var spot = AdventureSpot.Of(dead);
        var party = EngineParty.Get(dead);
        RecordDeath(fallen, killerPerson, killerPerson?.Name ?? killer?.Name, PartyThere(party, dead), spot, now);
        NoteFell(party, fallen, now);

        if (PkRules.IsRed(dead.Kills))
        {
            RedDied(dead, killer, spot, now);
        }
    }

    /// <summary>One <c>death</c> adventure. Returns its id, or <see cref="MemoryStore.NoAdventure"/>.</summary>
    internal long RecordDeath(
        PersonRef fallen,
        PersonRef? killer,
        string killerName,
        IReadOnlyList<PersonRef> there,
        AdventureSpot spot,
        DateTime now
    )
    {
        var members = new List<AdventureMember> { new(fallen, AdventureRoles.Fallen) };

        if (killer is { } against)
        {
            members.Add(new AdventureMember(against, AdventureRoles.Against));
        }

        for (var i = 0; i < there.Count; i++)
        {
            members.Add(new AdventureMember(there[i], AdventureRoles.With));
        }

        var adventure = spot.Make(
            AdventureKinds.Death,
            now,
            now,
            AdventureRules.DeathSummary(fallen.Name, killerName, spot.Place),
            [.. members]
        );
        return _store.Record(adventure with { Deaths = AdventureRules.OneDeath });
    }

    /// <summary>
    /// A red died: one shared <c>red-kill</c> for every person on its damage list who is not
    /// red. The red is <c>against</c>, the one who landed the kill is <c>killer</c>, the rest
    /// stand <c>with</c>. A pet's blows count for its master. The shard hears of it too
    /// (<see cref="ShardNews.RedKill"/>). Returns the id, or
    /// <see cref="MemoryStore.NoAdventure"/> when no such person hurt it.
    /// </summary>
    public long RedDied(Mobile red, Mobile killer, AdventureSpot spot, DateTime now)
    {
        if (PersonRef.Of(red) is not { } redPerson)
        {
            return MemoryStore.NoAdventure;
        }

        var landedBy = Behind(killer, red);
        var members = new List<AdventureMember> { new(redPerson, AdventureRoles.Against) };
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        string killerName = null;

        foreach (var entry in red.DamageEntries)
        {
            var attacker = Behind(entry.Damager, red);

            if (attacker == null || attacker == red || PkRules.IsRed(attacker.Kills) || PersonRef.Of(attacker) is not { } person)
            {
                continue;
            }

            var role = attacker == landedBy ? AdventureRoles.Killer : AdventureRoles.With;

            if (role == AdventureRoles.Killer)
            {
                killerName = person.Name;
            }

            if (index.TryGetValue(person.Id, out var at))
            {
                if (role == AdventureRoles.Killer)
                {
                    members[at] = members[at] with { Role = role };
                }

                continue;
            }

            index[person.Id] = members.Count;
            members.Add(new AdventureMember(person, role));
        }

        if (index.Count == 0)
        {
            return MemoryStore.NoAdventure;
        }

        var adventure = spot.Make(
            AdventureKinds.RedKill,
            now,
            now,
            AdventureRules.RedKillSummary(redPerson.Name, killerName, spot.Place),
            [.. members]
        );
        ShardNews.RedKill(red, SideNames(members), red.Location, now);
        return _store.Record(adventure with { Kills = AdventureRules.OneKill });
    }

    // The names of everyone on the side that put the red down, the red left out.
    private static List<string> SideNames(List<AdventureMember> members)
    {
        var names = new List<string>(members.Count);

        for (var i = 0; i < members.Count; i++)
        {
            if (!AdventureRoles.IsOpponent(members[i].Role))
            {
                names.Add(members[i].Person.Name);
            }
        }

        return names;
    }

    /// <summary>
    /// Another person raised the helped one from the dead, or healed it low in a fight: a
    /// <c>rescue</c>, the helper <c>healer</c> and the helped <c>with</c>. The helper also counts
    /// as a healer in the party's outing.
    /// </summary>
    public void Rescued(Mobile healer, Mobile helped, bool raised, DateTime now)
    {
        if (PersonRef.Of(healer) is not { } helper || PersonRef.Of(helped) is not { } person)
        {
            return;
        }

        if (RecordRescue(helper, person, raised, AdventureSpot.Of(helped), now) != MemoryStore.NoAdventure &&
            EngineParty.Get(helped) is { } party && _outings.TryGetValue(party, out var outing))
        {
            outing.Healed(helper);
        }
    }

    /// <summary>
    /// One <c>rescue</c> adventure. A heal in a fight between the same two people counts once
    /// per <see cref="AdventureRules.RescueRest"/>; a raise always counts.
    /// </summary>
    internal long RecordRescue(PersonRef healer, PersonRef helped, bool raised, AdventureSpot spot, DateTime now)
    {
        if (healer.Id == helped.Id)
        {
            return MemoryStore.NoAdventure;
        }

        var pair = (healer.Id, helped.Id);

        if (!raised && !AdventureRules.RescueRested(_rescuedAt.GetValueOrDefault(pair), now))
        {
            return MemoryStore.NoAdventure;
        }

        _rescuedAt[pair] = now;
        return _store.Record(
            spot.Make(
                AdventureKinds.Rescue,
                now,
                now,
                AdventureRules.RescueSummary(healer.Name, helped.Name, raised, spot.Place),
                [new AdventureMember(healer, AdventureRoles.Healer), new AdventureMember(helped, AdventureRoles.With)]
            )
        );
    }

    /// <summary>A duel ended with a winner: the winner landed it (<c>killer</c>), the beaten stood <c>against</c>.</summary>
    public long Duel(Mobile winner, Mobile beaten, DateTime now)
    {
        if (PersonRef.Of(winner) is not { } first || PersonRef.Of(beaten) is not { } second)
        {
            return MemoryStore.NoAdventure;
        }

        var spot = AdventureSpot.Of(winner);
        return _store.Record(
            spot.Make(
                AdventureKinds.Duel,
                now,
                now,
                AdventureRules.DuelSummary(first.Name, second.Name, spot.Place),
                [new AdventureMember(first, AdventureRoles.Killer), new AdventureMember(second, AdventureRoles.Against)]
            )
        );
    }

    /// <summary>The owner bought a house where it stands.</summary>
    public long House(Mobile owner, DateTime now) =>
        Solo(owner, AdventureKinds.House, spot => AdventureRules.HouseSummary(owner.Name, spot.Place), now);

    /// <summary>
    /// The killer murdered a person for the first time: <c>killer</c> and the victim
    /// <c>against</c>. A later murder is only the victim's death.
    /// </summary>
    public long FirstKill(Mobile killer, Mobile victim, DateTime now)
    {
        if (PersonRef.Of(killer) is not { } first || PersonRef.Of(victim) is not { } second)
        {
            return MemoryStore.NoAdventure;
        }

        var known = _store.AdventuresOf(first.Id);

        for (var i = 0; i < known.Count; i++)
        {
            if (known[i].Kind == AdventureKinds.FirstKill && known[i].MemberOf(first.Id)?.Role == AdventureRoles.Killer)
            {
                return MemoryStore.NoAdventure;
            }
        }

        var spot = AdventureSpot.Of(victim);
        return _store.Record(
            spot.Make(
                AdventureKinds.FirstKill,
                now,
                now,
                AdventureRules.FirstKillSummary(first.Name, second.Name, spot.Place),
                [new AdventureMember(first, AdventureRoles.Killer), new AdventureMember(second, AdventureRoles.Against)]
            ) with
            {
                Kills = AdventureRules.OneKill
            }
        );
    }

    /// <summary>A notable skill ended (<see cref="AdventureRules.IsNotableSkill"/>): a solo <c>outing</c>.</summary>
    public long SkillEnded(Mobile character, string skill, SkillStatus status, DateTime now) =>
        AdventureRules.IsNotableSkill(skill)
            ? Solo(character, AdventureKinds.Outing, spot => AdventureRules.OutingSummary(skill, status, spot.Place), now)
            : MemoryStore.NoAdventure;

    private long Solo(Mobile owner, string kind, Func<AdventureSpot, string> summary, DateTime now)
    {
        if (PersonRef.Of(owner) is not { } person)
        {
            return MemoryStore.NoAdventure;
        }

        var spot = AdventureSpot.Of(owner);
        return _store.Record(spot.Make(kind, now, now, summary(spot), [new AdventureMember(person, AdventureRoles.With)]));
    }

    // Party members near the dead, the dead left out.
    private static List<PersonRef> PartyThere(EngineParty party, Mobile dead)
    {
        var there = new List<PersonRef>();

        for (var i = 0; party != null && i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;

            if (member != dead && member is { Deleted: false } && member.Map == dead.Map &&
                member.InRange(dead.Location, AdventureRules.PresentTiles) && PersonRef.Of(member) is { } person)
            {
                there.Add(person);
            }
        }

        return there;
    }

    // A real player has no death hook the plugin can reach: each tick looks at who is connected,
    // and one alive at the last look and dead now died in between.
    private void WatchPlayers(DateTime now)
    {
        var alive = new Dictionary<Serial, bool>();

        foreach (var state in NetState.Instances)
        {
            if (state.Mobile is not { Deleted: false } player || !People.IsHuman(player))
            {
                continue;
            }

            alive[player.Serial] = player.Alive;

            if (!player.Alive && _playersAlive.GetValueOrDefault(player.Serial))
            {
                PersonDied(player, player.LastKiller, PersonRef.Of(Behind(player.LastKiller, player)), now);
            }
        }

        _playersAlive = alive;
    }

    private void ForgetOldRescues(DateTime now)
    {
        List<(string, string)> old = null;

        foreach (var (pair, at) in _rescuedAt)
        {
            if (AdventureRules.RescueRested(at, now))
            {
                (old ??= []).Add(pair);
            }
        }

        for (var i = 0; old != null && i < old.Count; i++)
        {
            _rescuedAt.Remove(old[i]);
        }
    }
}
