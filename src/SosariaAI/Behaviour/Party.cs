using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

public sealed class Party
{
    private static readonly ILogger logger = SosariaLog.For(typeof(Party));
    private static readonly Dictionary<string, Party> Registry = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where each crew member was last found. Care, lag waits and follows look members up
    /// every think, and this cache saves a walk over every mobile in the world for each one.
    /// </summary>
    private static readonly Dictionary<string, Serial> KnownSerials = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _present = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _restLogged = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _waitStarted;
    private bool _formedLogged;
    private bool _disbandLogged;

    private Party(PartyDefinition definition)
    {
        Id = definition.Id;
        LeaderId = definition.Leader;
        MemberIds = definition.Members ?? [];
        MeetAt = definition.MeetAt;
    }

    public string Id { get; }

    public string LeaderId { get; private set; }

    private DateTime _leaderDiedAt;

    public IReadOnlyList<string> MemberIds { get; }

    public Point3D MeetAt { get; }

    public bool TripActive { get; private set; }

    public bool Disbanded { get; private set; }

    public bool IsGathering => PartyWaitRules.IsGathering(TripActive, Disbanded, _waitStarted);

    public static void Configure(IEnumerable<PartyDefinition> definitions)
    {
        Registry.Clear();

        if (definitions == null)
        {
            return;
        }

        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            Registry[definition.Id] = new Party(definition);
        }
    }

    public static Party Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Registry.TryGetValue(id, out var party) ? party : null;
    }

    /// <summary>
    /// Power of the real party members, players included, standing near enough to help, and of
    /// the character's own pets at its side (<see cref="PetKeeper.PetsPower"/>).
    /// </summary>
    public static int AlliesPower(SosariaCharacter self)
    {
        var party = self == null ? null : GameParty.Of(self);
        var power = PetKeeper.PetsPower(self);

        if (party == null)
        {
            return power;
        }

        for (var i = 0; i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;

            if (member == null || member == self || member.Deleted || member is SosariaCharacter { IsGhost: true })
            {
                continue;
            }

            var sameMap = member.Map == self.Map;
            var distance = sameMap ? NavMetric.Chebyshev(self.Location, member.Location) : int.MaxValue;

            if (PartyInviteRules.CountsAsAlly(sameMap, member.Alive, distance, PartyInviteRules.InviteRange))
            {
                power += CharacterPower.For(member);
            }
        }

        return power;
    }

    public static Party FindByMember(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        foreach (var party in Registry.Values)
        {
            if (party.Lists(characterId))
            {
                return party;
            }
        }

        return null;
    }

    public static bool IsLeader(string partyId, string characterId)
    {
        var party = Find(partyId);
        return party != null && string.Equals(party.LeaderId, characterId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The leader or a listed member. A copy of a member ("sela#3") is not one: eight
    /// copies homed across the world followed the one Britain crew.
    /// </summary>
    public static bool IsMember(string partyId, string characterId)
    {
        var party = Find(partyId);
        return party != null && !string.IsNullOrWhiteSpace(characterId) && party.Lists(characterId);
    }

    /// <summary>This crew's leader or one of its listed members, by exact id.</summary>
    private bool Lists(string characterId)
    {
        if (string.Equals(LeaderId, characterId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var i = 0; i < MemberIds.Count; i++)
        {
            if (string.Equals(MemberIds[i], characterId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Two people in the same crew. The crew's leader and a member were in guilds at war and killed each other all evening.</summary>
    public static bool SharesParty(string firstId, string secondId)
    {
        var party = FindByMember(firstId);
        return party != null && IsMember(party.Id, secondId);
    }

    public static bool IsPresent(string partyId, string characterId)
    {
        var party = Find(partyId);
        return party != null && party._present.Contains(characterId);
    }

    public static void StartWait(SosariaCharacter leader)
    {
        var party = FindByMember(leader?.CharacterId);

        if (party == null || leader == null)
        {
            return;
        }

        party._waitStarted = Core.Now;
        party.TripActive = false;
        party.Disbanded = false;
        party._formedLogged = false;
        party._disbandLogged = false;
        party._present.Clear();
        party._restLogged.Clear();
    }

    public static PartyWaitResult TickWait(SosariaCharacter leader)
    {
        var party = FindByMember(leader?.CharacterId);

        if (party == null || leader == null)
        {
            return PartyWaitResult.Formed;
        }

        var members = new List<(string Id, int Distance, bool Alive, bool IsLeader)>(party.MemberIds.Count);

        for (var i = 0; i < party.MemberIds.Count; i++)
        {
            var id = party.MemberIds[i];
            var isLeader = string.Equals(id, party.LeaderId, StringComparison.OrdinalIgnoreCase);
            var mobile = FindMobile(id);
            var alive = mobile is { Deleted: false, IsGhost: false };
            var distance = int.MaxValue;

            if (alive && mobile.Map == leader.Map)
            {
                distance = NavMetric.Chebyshev(mobile.Location, party.MeetAt);
            }

            members.Add((id, distance, alive, isLeader));
        }

        var result = PartyWaitRules.WaitForMembers(
            Core.Now,
            party._waitStarted,
            members,
            PartyWaitRules.MeetRange,
            PartyWaitRules.WaitLimit
        );

        if (result == PartyWaitResult.Waiting)
        {
            return result;
        }

        party.TripActive = true;
        party._present.Clear();

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (member.IsLeader || (member.Alive && PartyWaitRules.AtMeet(member.Distance, PartyWaitRules.MeetRange)))
            {
                party._present.Add(member.Id);
                continue;
            }

            if (result == PartyWaitResult.TimedOut && member.Alive)
            {
                LogLeftBehind(party, member.Id);
            }
        }

        if (!party._formedLogged)
        {
            party._formedLogged = true;

            if (SosariaSettings.LogActivity)
            {
                logger.Information("Party {Id} formed with leader {Leader}", party.Id, party.LeaderId);
            }

            BindGameParty(leader, party);
        }

        return result;
    }

    private static void BindGameParty(SosariaCharacter leader, Party party)
    {
        // A leader already in a player's party keeps that party as it is.
        if (GameParty.InPlayerParty(leader))
        {
            return;
        }

        foreach (var id in party._present)
        {
            var member = FindMobile(id);

            if (member == null || member == leader || GameParty.InPlayerParty(member))
            {
                continue;
            }

            GameParty.TryForm(leader, member);
        }

        GameParty.Chat(leader, PartyInviteRules.PullLine());
    }

    public static void Disband(string id)
    {
        var party = Find(id);

        if (party == null || party._disbandLogged && party.Disbanded)
        {
            return;
        }

        party.TripActive = false;
        party.Disbanded = true;

        if (party._disbandLogged)
        {
            return;
        }

        party._disbandLogged = true;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("Party {Id} disbanded", party.Id);
        }

        // Members leave before the leader: the engine disbands a whole party when its leader
        // leaves. A leader still in a party with a player stays, so the player's party survives.
        for (var i = 0; i < party.MemberIds.Count; i++)
        {
            var member = FindMobile(party.MemberIds[i]);

            if (member != null && !string.Equals(party.MemberIds[i], party.LeaderId, StringComparison.OrdinalIgnoreCase))
            {
                GameParty.Leave(member, PartyInviteRules.GoodbyeLine());
            }
        }

        var leader = FindMobile(party.LeaderId);

        if (leader != null && !GameParty.InPlayerParty(leader))
        {
            GameParty.Leave(leader, PartyInviteRules.GoodbyeLine());
        }
    }

    public static bool ShouldRest(SosariaCharacter character)
    {
        if (character == null)
        {
            return false;
        }

        var rest = PartyWaitRules.ShouldRest(Vitals.HitsFraction(character), PartyWaitRules.RestHitsFraction);

        if (!rest)
        {
            return false;
        }

        var party = FindByMember(character.CharacterId);

        if (party != null && party._restLogged.Add(character.CharacterId) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} is resting from party {Id}", character.Name, party.Id);
        }

        return true;
    }

    public static void TickCare(SosariaCharacter actor)
    {
        var party = FindByMember(actor?.CharacterId);

        if (party == null || actor == null)
        {
            return;
        }

        var members = new List<(string Id, bool Alive)>(party.MemberIds.Count);

        for (var i = 0; i < party.MemberIds.Count; i++)
        {
            var id = party.MemberIds[i];
            var mobile = FindMobile(id);
            var living = mobile is { Deleted: false, IsGhost: false };
            members.Add((id, living));
        }

        var leaderMobile = FindMobile(party.LeaderId);

        if (leaderMobile == null || leaderMobile.Deleted || leaderMobile.IsGhost)
        {
            if (party._leaderDiedAt == default)
            {
                party._leaderDiedAt = Core.Now;
            }
        }
        else
        {
            party._leaderDiedAt = default;
        }

        party.LeaderId = PartyLead.Promote(
            party.LeaderId,
            members,
            Core.Now,
            party._leaderDiedAt,
            TimeSpan.FromMinutes(PartyScale.FallenGraceMinutes)
        );

        for (var i = 0; i < party.MemberIds.Count; i++)
        {
            var fallen = FindMobile(party.MemberIds[i]);

            if (fallen is not { Deleted: false, IsGhost: true } || fallen.Map == null || fallen.Map == Map.Internal)
            {
                continue;
            }

            TryRaiseFallen(party, fallen);
        }
    }

    private static void TryRaiseFallen(Party party, SosariaCharacter fallen)
    {
        foreach (var id in party.MemberIds)
        {
            var helper = FindMobile(id);

            if (helper == null || helper == fallen || helper.Deleted || helper.IsGhost ||
                !ResurrectAid.InPartyReach(helper.Map == fallen.Map, NavMetric.Chebyshev(helper.Location, fallen.Location)))
            {
                continue;
            }

            // A real Resurrection cast or bandage raise, walked to and paid for like any other.
            if (ResurrectOffer.TryStartAid(helper, fallen, asked: true))
            {
                return;
            }
        }
    }

    /// <summary>
    /// A member that keeps running from the danger leaves the trip. The leader waits for
    /// every member on the trip, so without this it stood still near the monsters for good.
    /// </summary>
    public static void LeaveTrip(SosariaCharacter member)
    {
        var party = FindByMember(member?.CharacterId);

        if (party is not { TripActive: true } ||
            string.Equals(member.CharacterId, party.LeaderId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (party._present.Remove(member.CharacterId) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} left party {Id} after running from danger too often", member.Name, party.Id);
        }
    }

    public static bool ShouldWaitForLag(SosariaCharacter leader)
    {
        var party = FindByMember(leader?.CharacterId);

        if (party is not { TripActive: true } || leader == null)
        {
            return false;
        }

        if (leader.Combatant != null)
        {
            return false;
        }

        var farthest = 0;

        foreach (var id in party._present)
        {
            if (string.Equals(id, party.LeaderId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var member = FindMobile(id);

            if (member is not { Deleted: false, IsGhost: false } || member.Map != leader.Map)
            {
                continue;
            }

            var distance = (int)leader.GetDistanceToSqrt(member);

            if (distance > farthest)
            {
                farthest = distance;
            }
        }

        return PartyWaitRules.ShouldWaitForLag(false, farthest, PartyWaitRules.LagTiles);
    }

    public static SosariaCharacter FindMobile(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        if (KnownSerials.TryGetValue(characterId, out var serial) &&
            World.FindMobile(serial) is SosariaCharacter { Deleted: false } known &&
            string.Equals(known.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
        {
            return known;
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character &&
                string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
            {
                KnownSerials[characterId] = character.Serial;
                return character;
            }
        }

        KnownSerials.Remove(characterId);
        return null;
    }

    private static void LogLeftBehind(Party party, string memberId)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("Party {Id} left {Member} behind", party.Id, memberId);
        }
    }
}
