using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>One group on the road: who leads, who walks along, and the trip it walks.</summary>
public sealed class RoadGroup
{
    public RoadGroup(RoadGroupKind kind, Skill trip, string place, GuildType side, FactionSpot? spot, DateTime formedAt)
    {
        Kind = kind;
        Trip = trip;
        Place = place;
        Side = side;
        Spot = spot;
        FormedAt = formedAt;
    }

    public RoadGroupKind Kind { get; }

    /// <summary>The leader's trip; the group lasts while the leader walks it.</summary>
    public Skill Trip { get; }

    /// <summary>Where the group goes, as people name it.</summary>
    public string Place { get; }

    /// <summary>The side of a war band; <see cref="GuildType.Regular"/> for a convoy.</summary>
    public GuildType Side { get; }

    /// <summary>The meeting spot a war band rides for.</summary>
    public FactionSpot? Spot { get; }

    public DateTime FormedAt { get; }

    /// <summary>True once a Den raid rode out: a band too small for the Den never left.</summary>
    public bool RodeOut { get; set; }

    /// <summary>The killer a posse rides after.</summary>
    public Serial Quarry { get; set; }

    /// <summary>True once the call went out in guild chat too, so a guildmate there may answer it.</summary>
    public bool CalledInChat { get; set; }

    public List<Serial> Mates { get; } = [];
}

/// <summary>
/// Guild convoys, faction war bands, anti-PK sweeps, Den raids and posses on the roads (see
/// <see cref="PartyRoadRules"/>). The call reaches the free mates over a town and its roads:
/// guildmates through the guild's roster, faction-mates through the rosters of every guild of
/// the side, lawful fighters in sight of the call. The group is a real engine party: the mates
/// walk over and follow the leader with <see cref="FollowSkill"/>, the leader waits for them
/// (<see cref="PartyRoadTrip"/>), all take up each other's fights, and the mates are let go
/// when the leader's trip ends, the time runs out, or a band's first clash takes the leader
/// off its ride. A player of the group's side who answers the call with "me", near the leader
/// or in the guild chat the call went out in, gets a real invite and walks as a mate (see
/// <see cref="JoinPlayer"/>). The groups are in memory only, as the engine's parties end with a restart.
/// Each fighter's rest after leading a band (<see cref="RuleClock.BandLed"/>) and after riding
/// a Den raid (<see cref="RuleClock.DenRaid"/>) is its own clock, which the save keeps.
/// </summary>
public static class PartyRoads
{
    /// <summary>A mate on another map counts as nowhere near the muster.</summary>
    private const int OffMap = -1;

    /// <summary>The leader, counted as one rider of its band.</summary>
    private const int OneRider = 1;

    private static readonly Dictionary<Serial, RoadGroup> Groups = new();

    /// <summary>The one go-or-hold question out to Jev for a Den raid, and the leader who asked it.</summary>
    private static readonly JevWait DenRaidAsk = new();

    private static Serial _denRaidAsker;

    /// <summary>How the last Den raid that rode out ended, for the next band's go-or-hold.</summary>
    private static DenRaidOutcome _lastDenRaid;

    private static readonly Dictionary<Serial, DateTime> NextLook = new();
    private static readonly List<Serial> _gone = [];
    private static readonly List<SosariaCharacter> _pool = [];
    private static readonly List<SosariaCharacter> _mates = [];
    private static readonly List<(string Id, int Distance, bool Alive, bool IsLeader)> _muster = [];
    private static DateTime _nextConvoy;
    private static DateTime _nextWarBand;
    private static DateTime _nextSweep;
    private static DateTime _nextDenRaid;

    /// <summary>
    /// True for a blue that rides against the reds in Buccaneer's Den: the leader or a mate of
    /// a Den raid or a posse, from its muster to its break-up, or a PK hunter on its run there
    /// (<see cref="ConflictSkill.HuntsDen"/>).
    /// </summary>
    public static bool RidesAgainstDen(Mobile mobile) =>
        InGroup(mobile, RoadGroupKind.DenRaid) || InGroup(mobile, RoadGroupKind.Posse) ||
        mobile is SosariaCharacter { Routine.CurrentSkill: ConflictSkill { HuntsDen: true } };

    /// <summary>
    /// True while <paramref name="leader"/> leads a road group, from the call to the break-up:
    /// its call stands behind an invite even with no mate yet, as a lone posse leader has no
    /// engine party and still asks who rides with it.
    /// </summary>
    public static bool Leads(Mobile leader) => leader != null && Groups.ContainsKey(leader.Serial);

    /// <summary>
    /// The anti-PK band out now, a sweep or a Den raid, that rides for a spot within
    /// <see cref="HotSpots.SightingTiles"/> of this red or of the camp its run takes, or null:
    /// what a red hears of the blues before it picks its next job.
    /// </summary>
    public static RoadGroupKind? BandFor(SosariaCharacter red, DateTime now)
    {
        if (red?.Map == null || Count(RoadGroupKind.Sweep) + Count(RoadGroupKind.DenRaid) == 0)
        {
            return null;
        }

        return BandNear(red.Map, red.Location) ??
               (HotSpots.CampFor(red, now) is { } camp ? BandNear(red.Map, camp.Camp) : null);
    }

    private static RoadGroupKind? BandNear(Map map, Point3D at)
    {
        foreach (var (leader, group) in Groups)
        {
            if (group.Kind is RoadGroupKind.Sweep or RoadGroupKind.DenRaid && group.Spot is { } spot &&
                World.FindMobile(leader)?.Map == map && NavMetric.Chebyshev(spot.Location, at) <= HotSpots.SightingTiles)
            {
                return group.Kind;
            }
        }

        return null;
    }

    private static bool InGroup(Mobile mobile, RoadGroupKind kind)
    {
        if (mobile == null)
        {
            return false;
        }

        foreach (var (leader, group) in Groups)
        {
            if (group.Kind == kind && (leader == mobile.Serial || group.Mates.Contains(mobile.Serial)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where the leader's mates stand against the muster: waiting while one is still on its
    /// way, formed once all living ones stand round the leader, timed out after
    /// <see cref="PartyRoadRules.MusterLimit"/>. A leader with no group has nothing to wait for.
    /// A player whose invite is still pending is not waited for: it may never click.
    /// </summary>
    public static PartyWaitResult Muster(SosariaCharacter leader, DateTime since, DateTime now)
    {
        if (leader == null || !Groups.TryGetValue(leader.Serial, out var group))
        {
            return PartyWaitResult.Formed;
        }

        _muster.Clear();
        var party = GameParty.Of(leader);

        for (var i = 0; i < group.Mates.Count; i++)
        {
            if (World.FindMobile(group.Mates[i]) is { Deleted: false } mate && party?.Contains(mate) == true)
            {
                var distance = mate.Map == leader.Map ? NavMetric.Chebyshev(leader.Location, mate.Location) : OffMap;
                _muster.Add((mate.Serial.ToString(), distance, mate.Alive, false));
            }
        }

        return PartyWaitRules.WaitForMembers(now, since, _muster, PartyWaitRules.MeetRange, PartyRoadRules.MusterLimit);
    }

    /// <summary>One world scan: a leader keeps its group or breaks it up; anyone else may set out with one.</summary>
    public static void Consider(SosariaCharacter character)
    {
        if (character?.Map == null || character.Map == Map.Internal || !character.Alive)
        {
            return;
        }

        var now = Core.Now;
        DisbandGone();

        if (Groups.TryGetValue(character.Serial, out var group))
        {
            TickLeader(character, group, now);
            return;
        }

        if (!PartyRoadRules.LookDue(NextLook.GetValueOrDefault(character.Serial), now))
        {
            return;
        }

        NextLook.Remove(character.Serial);

        if (!TryWarBand(character, now) && !TryDenRaid(character, now) && !TrySweep(character, now))
        {
            TryConvoy(character, now);
        }
    }

    /// <summary>
    /// Breaks up the groups whose leader left the world. A leader on the internal map thinks
    /// no more, so its own scan never ends the group; anyone else's scan does, and the mates
    /// are let go instead of following a leader on no map.
    /// </summary>
    private static void DisbandGone()
    {
        foreach (var serial in Groups.Keys)
        {
            var leader = World.FindMobile(serial);

            if (PartyRoadRules.LeaderGone(leader is { Deleted: false }, leader?.Map == null || leader.Map == Map.Internal))
            {
                _gone.Add(serial);
            }
        }

        for (var i = 0; i < _gone.Count; i++)
        {
            Disband(_gone[i], Groups[_gone[i]], "the leader left the world");
        }

        _gone.Clear();
    }

    private static void TickLeader(SosariaCharacter leader, RoadGroup group, DateTime now)
    {
        var reason = !ReferenceEquals(leader.Routine?.CurrentSkill, group.Trip)
            ? group.Kind == RoadGroupKind.Convoy ? "the trip is over" : "the band met its fight or rode home"
            : PartyRoadRules.Expired(group.Kind, group.FormedAt, now)
                ? "it ran out of time"
                : StillTogether(leader, group) == 0 && PartyRoadRules.MinMatesOf(group.Kind) > 0
                    ? "everyone else left"
                    : null;

        if (reason != null)
        {
            Disband(leader.Serial, group, reason);
        }
    }

    private static int StillTogether(SosariaCharacter leader, RoadGroup group)
    {
        var party = GameParty.Of(leader);

        // A player whose invite is still pending stays a mate until it answers.
        group.Mates.RemoveAll(serial =>
            World.FindMobile(serial) is not { Deleted: false } mate ||
            party?.Contains(mate) != true && party?.Candidates.Contains(mate) != true);
        return group.Mates.Count;
    }

    /// <summary>
    /// A player's "me" to a road group's call: the player joins the engine party and the group's
    /// mates, and walks with it as a mate does. Heard aloud, the leader stands within speech of
    /// the player; heard in guild chat (<paramref name="viaGuild"/>), the call went out there.
    /// Only while the call is fresh, and only a player of the group's side. The group's leader
    /// when the player is in the group, else null; <paramref name="invited"/> is true when the
    /// invite went out just now. The calls asked "anyone coming?" and a player's yes got nothing.
    /// </summary>
    public static SosariaCharacter JoinPlayer(Mobile player, Guild viaGuild, out bool invited)
    {
        invited = false;

        if (player is not { Deleted: false, Alive: true })
        {
            return null;
        }

        var now = Core.Now;

        foreach (var (serial, group) in Groups)
        {
            if (World.FindMobile(serial) is not SosariaCharacter { Deleted: false, Alive: true } leader ||
                !PartyRoadRules.PlayerJoinOpen(group.FormedAt, now) || !HeardCall(leader, group, player, viaGuild) ||
                !PlayerFits(leader, group, player))
            {
                continue;
            }

            if (group.Mates.Contains(player.Serial))
            {
                return leader;
            }

            if (!GameParty.TryForm(leader, player))
            {
                continue;
            }

            group.Mates.Add(player.Serial);
            invited = true;
            WorldPlay.Log($"{player.Name} answered {leader.Name}'s {KindWord(group.Kind)} for {group.Place}");
            return leader;
        }

        return null;
    }

    // Aloud within speech of the leader, or in the guild chat the call went out in.
    private static bool HeardCall(SosariaCharacter leader, RoadGroup group, Mobile player, Guild viaGuild) =>
        viaGuild == null
            ? player.Map == leader.Map && GameParty.IsApproachable(leader, player) &&
              PartyRoadRules.WithinSpeech(NavMetric.Chebyshev(leader.Location, player.Location))
            : group.CalledInChat && leader.Guild == viaGuild;

    // The group's side, and never the killer a posse rides after.
    private static bool PlayerFits(SosariaCharacter leader, RoadGroup group, Mobile player) =>
        player.Serial != group.Quarry &&
        PartyRoadRules.PlayerFitsSide(
            group.Kind,
            player.Guild != null && player.Guild == leader.Guild,
            group.Side != GuildType.Regular && EngineGuilds.AlignmentOf(player) == group.Side,
            !PkRules.IsRed(player.Kills) && !player.Criminal
        );

    // Read by serial: a deleted leader is found no more, and its mates still stand in its party.
    private static void Disband(Serial leaderSerial, RoadGroup group, string reason)
    {
        Groups.Remove(leaderSerial);
        var leader = World.FindMobile(leaderSerial);

        // Only a raid that rode out tells the next band how it went; one too small never left.
        if (group.Kind == RoadGroupKind.DenRaid && group.RodeOut)
        {
            _lastDenRaid = PartyRoadRules.RaidOutcome(Fallen(leader, group));
        }

        for (var i = 0; i < group.Mates.Count; i++)
        {
            if (World.FindMobile(group.Mates[i]) is { Deleted: false } mate &&
                GameParty.Of(mate)?.Leader?.Serial == leaderSerial)
            {
                GameParty.Leave(mate, null);
            }
        }

        if (leader != null && GameParty.Of(leader) is { } left && left.Leader == leader)
        {
            GameParty.Leave(leader, null);
        }

        WorldPlay.Log($"{leader?.Name ?? "A gone leader"}'s {KindWord(group.Kind)} for {group.Place} broke up: {reason}");
    }

    /// <summary>The riders of a group dead at its break-up, the leader counted.</summary>
    private static int Fallen(Mobile leader, RoadGroup group)
    {
        var fallen = leader is { Alive: false } ? OneRider : 0;

        for (var i = 0; i < group.Mates.Count; i++)
        {
            if (World.FindMobile(group.Mates[i]) is { Alive: false })
            {
                fallen++;
            }
        }

        return fallen;
    }

    /// <summary>
    /// An idle Order or Chaos fighter calls free faction-mates of every guild of its side over
    /// a town and its roads, and sets out with them for a meeting spot, most often the one an
    /// enemy band already rides for.
    /// </summary>
    private static bool TryWarBand(SosariaCharacter leader, DateTime now)
    {
        var side = EngineGuilds.AlignmentOf(leader);

        if (side == GuildType.Regular || now < _nextWarBand || !FreeToSetOut(leader, now) ||
            !WorldPlay.OpenPvp(leader.Map) || leader.Hits < leader.HitsMax ||
            !TimeRules.Rested(leader.ClockAt(RuleClock.BandLed), now, FactionRules.PatrolRest) ||
            !PartyRoadRules.MayForm(RoadGroupKind.WarBand, Count(RoadGroupKind.WarBand), LfgBoard.Population(now)))
        {
            return false;
        }

        SideRoster(side);

        if (!FindMates(leader, RoadGroupKind.WarBand, mate => EngineGuilds.AlignmentOf(mate) == side, now))
        {
            return LookedInVain(leader, now);
        }

        var enemySpot = EnemyBandSpot(side);
        var spot = PartyRoadRules.Intercepts(enemySpot != null, Utility.Random(PartyRoadRules.PercentScale))
            ? enemySpot
            : FactionSpots.Pick(
                FactionSpots.ForMap(leader.Map),
                now,
                leader.Location,
                leader.HomeSpot,
                HomeLeash.ConfiguredRadius()
            );

        if (spot is not { } chosen || SetOut(leader, new FactionPatrolSkill(chosen, side)) is not { } trip)
        {
            return LookedInVain(leader, now);
        }

        _nextWarBand = now + PartyRoadRules.Gap(RoadGroupKind.WarBand, Utility.RandomDouble());
        leader.StartClock(RuleClock.BandLed, now);
        var group = Form(leader, RoadGroupKind.WarBand, trip, chosen.Name, side, chosen, now);

        if (BrokeUpShort(leader, group, NoMateJoinedWhy))
        {
            return LookedInVain(leader, now);
        }

        Call(leader, group, TalkCategory.WarBandDepart, new TalkSlots { Place = chosen.Name });
        WorldPlay.Log(
            $"{side} war band: {leader.Name} and {group.Mates.Count} ride out to {chosen.Name}" +
            (enemySpot is { } enemy && enemy == chosen ? " to meet the enemy band" : string.Empty)
        );
        return true;
    }

    /// <summary>
    /// An idle lawful fighter calls the free lawful fighters within call and rides out with them
    /// to clear the reds off a hot spot: the one a red was last reported at, else the one the
    /// rotation names.
    /// </summary>
    private static bool TrySweep(SosariaCharacter leader, DateTime now)
    {
        if (!LawfulLeaderReady(leader, RoadGroupKind.Sweep, _nextSweep, now) ||
            HotSpots.SweepTarget(leader, now) is not { } spot)
        {
            return false;
        }

        InSight(leader);

        if (!FindMates(leader, RoadGroupKind.Sweep, mate => mate.Disposition == DispositionKind.Lawful, now) ||
            SetOut(leader, new FactionPatrolSkill(spot, GuildType.Regular)) is not { } trip)
        {
            return LookedInVain(leader, now);
        }

        _nextSweep = now + PartyRoadRules.Gap(RoadGroupKind.Sweep, Utility.RandomDouble());
        leader.StartClock(RuleClock.BandLed, now);
        var group = Form(leader, RoadGroupKind.Sweep, trip, spot.Name, GuildType.Regular, spot, now);

        if (BrokeUpShort(leader, group, NoMateJoinedWhy))
        {
            return LookedInVain(leader, now);
        }

        Call(leader, group, TalkCategory.SweepDepart, new TalkSlots { Place = spot.Name });
        WorldPlay.Log($"anti-pk sweep: {leader.Name} and {group.Mates.Count} ride out to {spot.Name}");
        return true;
    }

    /// <summary>
    /// A blue murdered by <paramref name="killer"/>: the nearest of its friends in call (its
    /// party or its guild) or of the PK hunters in call leads a posse after the killer, and up
    /// to three more of them ride along. Each drops what it was doing, unless it is in a fight,
    /// a group or a dungeon. The posse rides wherever the killer went, into the Den too
    /// (<see cref="RidesAgainstDen"/>). One posse rides after one killer, and only a few at once.
    /// </summary>
    public static void RaisePosse(SosariaCharacter victim, Mobile killer)
    {
        var now = Core.Now;

        if (victim?.Map == null || victim.Map == Map.Internal || killer is not PlayerMobile { Deleted: false, Alive: true } ||
            killer.Map != victim.Map || !WorldPlay.OpenPvp(victim.Map) || HuntedByPosse(killer) ||
            !PartyRoadRules.MayForm(RoadGroupKind.Posse, Count(RoadGroupKind.Posse), LfgBoard.Population(now)))
        {
            return;
        }

        InSight(victim);
        bool Answers(SosariaCharacter rider) => rider != killer && AnswersPosse(victim, rider, now);

        if (NearestIn(victim, Answers) is not { } leader || !FindMates(leader, RoadGroupKind.Posse, Answers, now))
        {
            _pool.Clear();
            _mates.Clear();
            return;
        }

        var place = GossipLines.PlaceWord(PlaceNames.Of(killer));
        var ground = new FactionSpot(place, killer.Location);

        if (SetOut(leader, new FactionPatrolSkill(ground, GuildType.Regular, killer)) is not { } trip)
        {
            _mates.Clear();
            return;
        }

        var group = Form(leader, RoadGroupKind.Posse, trip, place, GuildType.Regular, ground, now);
        group.Quarry = killer.Serial;
        Call(leader, group, TalkCategory.PosseDepart, new TalkSlots { Foe = killer.Name, Name = victim.Name });
        WorldPlay.Log($"posse: {leader.Name} and {group.Mates.Count} ride after {killer.Name} for {victim.Name}");
    }

    // A friend of the dead, or a PK hunter, free to ride: a fighter out of any fight, group,
    // duel or dungeon. It drops the errand it was on.
    private static bool AnswersPosse(SosariaCharacter victim, SosariaCharacter rider, DateTime now) =>
        rider != victim && rider.Map == victim.Map && FreeToLead(rider, now) &&
        rider.Build?.Role == CharacterRole.Fighter && !DungeonTripSkill.InDungeon(rider) &&
        (rider.IsPkHunter || WorldPlay.AreFriends(victim, rider));

    /// <summary>The one in <see cref="_pool"/> nearest <paramref name="at"/> that passes <paramref name="fits"/>, or null.</summary>
    private static SosariaCharacter NearestIn(Mobile at, Func<SosariaCharacter, bool> fits)
    {
        SosariaCharacter nearest = null;
        var nearestTiles = int.MaxValue;

        for (var i = 0; i < _pool.Count; i++)
        {
            var tiles = NavMetric.Chebyshev(at.Location, _pool[i].Location);

            if (tiles < nearestTiles && fits(_pool[i]))
            {
                nearest = _pool[i];
                nearestTiles = tiles;
            }
        }

        return nearest;
    }

    private static bool HuntedByPosse(Mobile killer)
    {
        foreach (var group in Groups.Values)
        {
            if (group.Kind == RoadGroupKind.Posse && group.Quarry == killer.Serial)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Now and then an idle lawful fighter calls two or three free lawful fighters within call
    /// and rides into Buccaneer's Den with them to fight the reds on their own ground. A sweep
    /// never picks the Den: it keeps to its leash, and every blue town lies 800 tiles and more
    /// from the Den, so no blue came in and the Den saw only reds fighting reds. None of the
    /// band rides on another raid for <see cref="PartyRoadRules.DenRaidRest"/>, and each rider is
    /// strong enough for the Den (<see cref="PartyRoadRules.StrongEnoughForDen"/>). Once the mates are
    /// found, Jev may hold the raid back (<see cref="DenRaidCall"/>); a held raid waits for the
    /// next shard-wide gap.
    /// </summary>
    private static bool TryDenRaid(SosariaCharacter leader, DateTime now)
    {
        if (!LawfulLeaderReady(leader, RoadGroupKind.DenRaid, _nextDenRaid, now) || !DenRaidRested(leader, now) ||
            !PartyRoadRules.StrongEnoughForDen(CharacterPower.For(leader)) || HotSpots.DenSpot(leader.Map) is not { } den)
        {
            return false;
        }

        // One band's go-or-hold is out: its leader stands by for the answer, any other waits its turn.
        if (DenRaidAsk.Waiting(now))
        {
            return _denRaidAsker == leader.Serial;
        }

        InSight(leader);

        if (!FindMates(
                leader,
                RoadGroupKind.DenRaid,
                mate => mate.Disposition == DispositionKind.Lawful && DenRaidRested(mate, now) &&
                        PartyRoadRules.StrongEnoughForDen(CharacterPower.For(mate)),
                now
            ))
        {
            return LookedInVain(leader, now);
        }

        if (DenRaidCall(leader, den.Name, now) is not { } rides)
        {
            return true;
        }

        if (!rides)
        {
            WaitForNextDenRaid(now);
            return false;
        }

        if (SetOut(leader, new FactionPatrolSkill(den, GuildType.Regular)) is not { } trip)
        {
            return LookedInVain(leader, now);
        }

        WaitForNextDenRaid(now);
        leader.StartClock(RuleClock.BandLed, now);
        var group = Form(leader, RoadGroupKind.DenRaid, trip, den.Name, GuildType.Regular, den, now);

        // A mate whose party would not form stays behind; a band too small for the Den stays home.
        if (BrokeUpShort(leader, group, TooFewForTheDenWhy))
        {
            return false;
        }

        group.RodeOut = true;
        leader.StartClock(RuleClock.DenRaid, now);

        for (var i = 0; i < group.Mates.Count; i++)
        {
            (World.FindMobile(group.Mates[i]) as SosariaCharacter)?.StartClock(RuleClock.DenRaid, now);
        }

        Call(leader, group, TalkCategory.SweepDepart, new TalkSlots { Place = den.Name });
        WorldPlay.Log($"anti-pk raid: {leader.Name} and {group.Mates.Count} ride out to {den.Name}");
        return true;
    }

    private const string TooFewForTheDenWhy = "too few mates joined to ride into the Den";

    private const string NoMateJoinedWhy = "too few mates joined the party";

    /// <summary>
    /// True when fewer mates joined the engine party than the group's kind sets out with: the
    /// group breaks up and the leader drops the trip before any call goes out, so nobody is
    /// asked along on a group that is not there.
    /// </summary>
    private static bool BrokeUpShort(SosariaCharacter leader, RoadGroup group, string why)
    {
        if (group.Mates.Count >= PartyRoadRules.MinMatesOf(group.Kind))
        {
            return false;
        }

        Disband(leader.Serial, group, why);
        leader.Routine?.AbortActive();
        return true;
    }

    /// <summary>A raid rode out or was held back: the next one waits out the shard-wide gap.</summary>
    private static void WaitForNextDenRaid(DateTime now) =>
        _nextDenRaid = now + PartyRoadRules.Gap(RoadGroupKind.DenRaid, Utility.RandomDouble());

    private static bool DenRaidRested(SosariaCharacter character, DateTime now) =>
        TimeRules.Rested(character.ClockAt(RuleClock.DenRaid), now, PartyRoadRules.DenRaidRest);

    /// <summary>
    /// Jev's go or hold for the band just found (<see cref="RedMomentJev.RaidGoes"/>): true to
    /// ride, false to hold, null while the question is out. With no provider, a refused call, or
    /// no clear answer in time the band rides, the rule. The answer is logged with its source.
    /// </summary>
    private static bool? DenRaidCall(SosariaCharacter leader, string den, DateTime now)
    {
        if (_denRaidAsker == leader.Serial && DenRaidAsk.TryTake(now, out var verdict))
        {
            var rides = RedMomentJev.RaidGoes(verdict);
            WorldPlay.Log($"anti-pk raid: {leader.Name}'s band {(rides ? "rides for" : "holds off from")} {den} ({verdict.Source})");
            return rides;
        }

        _denRaidAsker = leader.Serial;
        return DenRaidAsk.Ask(now, answer => RedMomentJev.TryAskDenRaid(leader, DenRaidFactsOf(leader), answer)) ? null : true;
    }

    /// <summary>The band a Den raid would take from <see cref="_mates"/>, nearest first, and what it knows of the Den.</summary>
    private static DenRaidFacts DenRaidFactsOf(SosariaCharacter leader)
    {
        var mates = Math.Min(_mates.Count, PartyRoadRules.MaxMates);
        var shortOfSupplies = SupplyCheck.IsLow(leader) ? OneRider : 0;

        for (var i = 0; i < mates; i++)
        {
            if (SupplyCheck.IsLow(_mates[i]))
            {
                shortOfSupplies++;
            }
        }

        return new DenRaidFacts(mates + OneRider, shortOfSupplies, RedGang.RedsInDen(leader.Map), _lastDenRaid);
    }

    /// <summary>
    /// A lawful fighter who may lead an anti-PK group of <paramref name="kind"/> now: free, on a
    /// facet with open PvP, whole, rested from its last band, the kind's shard-wide gap run out
    /// and fewer of the kind out than its cap.
    /// </summary>
    private static bool LawfulLeaderReady(SosariaCharacter leader, RoadGroupKind kind, DateTime nextAt, DateTime now) =>
        leader.Disposition == DispositionKind.Lawful && now >= nextAt && FreeToSetOut(leader, now) &&
        WorldPlay.OpenPvp(leader.Map) && leader.Hits >= leader.HitsMax &&
        TimeRules.Rested(leader.ClockAt(RuleClock.BandLed), now, FactionRules.PatrolRest) &&
        PartyRoadRules.MayForm(kind, Count(kind), LfgBoard.Population(now));

    /// <summary>
    /// A guilded person who just set out on a long walk to another town, or who stands about
    /// and proposes one, calls free guildmates over a town and its roads to come along; they
    /// gather, walk the road together and part at the bank.
    /// </summary>
    private static void TryConvoy(SosariaCharacter leader, DateTime now)
    {
        if (leader.Guild is not Guild guild || now < _nextConvoy || !FreeToLead(leader, now) ||
            !StandingAboutOrTravelling(leader) ||
            !PartyRoadRules.MayForm(RoadGroupKind.Convoy, Count(RoadGroupKind.Convoy), LfgBoard.Population(now)))
        {
            return;
        }

        GuildRoster(guild);

        if (!FindMates(leader, RoadGroupKind.Convoy, mate => mate.Guild == guild, now) ||
            ConvoyGoal(leader, now) is not { } goal ||
            SetOut(leader, new TownTrip(goal)) is not { } trip)
        {
            LookedInVain(leader, now);
            return;
        }

        _nextConvoy = now + PartyRoadRules.Gap(RoadGroupKind.Convoy, Utility.RandomDouble());
        var place = GossipLines.PlaceWord(PlaceNames.Of(goal, leader.Map));
        var group = Form(leader, RoadGroupKind.Convoy, trip, place, GuildType.Regular, null, now);

        if (BrokeUpShort(leader, group, NoMateJoinedWhy))
        {
            LookedInVain(leader, now);
            return;
        }

        Call(leader, group, TalkCategory.ConvoyDepart, new TalkSlots { Place = place, Guild = guild.Abbreviation });
        WorldPlay.Log($"{leader.Name} set out with {group.Mates.Count} [{guild.Abbreviation}] guildmates for {place}");
    }

    /// <summary>
    /// The bank a convoy walks to: the one the leader's own trip already goes to when that is a
    /// real trip away, else, for a leader standing about, a bank it proposes (see
    /// <see cref="PartyRoadRules.ConvoyBank"/>). Null for a leader busy with anything else.
    /// </summary>
    private static Point3D? ConvoyGoal(SosariaCharacter leader, DateTime now)
    {
        if (leader.Routine?.CurrentSkill is TownTrip trip)
        {
            return PartyRoadRules.ConvoyTrip(NavMetric.Chebyshev(leader.Location, trip.Goal)) ? trip.Goal : null;
        }

        return WorldPlay.IsIdle(leader) &&
               PartyRoadRules.ConvoyBank(
                   NavWorld.DestinationsFor(leader.Map.Name)?.All,
                   leader.Location,
                   leader.Memory.Danger.Active(now),
                   Utility.Random(int.MaxValue)
               ) is { } bank
            ? bank.Arrival
            : null;
    }

    /// <summary>
    /// A call that formed no group: this leader calls again after <see cref="PartyRoadRules.LookRetry"/>,
    /// not on its next scan. False, for the caller to return.
    /// </summary>
    private static bool LookedInVain(SosariaCharacter leader, DateTime now)
    {
        NextLook[leader.Serial] = now + PartyRoadRules.LookRetry;
        return false;
    }

    /// <summary>The online members of the guild, in <see cref="_pool"/>.</summary>
    private static void GuildRoster(Guild guild)
    {
        _pool.Clear();
        _pool.AddRange(GuildChat.OnlineCharacters(guild));
    }

    /// <summary>The online members of every guild of the side, in <see cref="_pool"/>.</summary>
    private static void SideRoster(GuildType side)
    {
        _pool.Clear();
        EngineGuilds.AddSideMembers(side, _pool);
    }

    /// <summary>The characters within call of the leader, in <see cref="_pool"/>.</summary>
    private static void InSight(SosariaCharacter leader)
    {
        _pool.Clear();

        foreach (var mobile in leader.Map.GetMobilesInRange(leader.Location, PartyRoadRules.CallRange))
        {
            if (mobile is SosariaCharacter character)
            {
                _pool.Add(character);
            }
        }
    }

    /// <summary>
    /// Free fighters of the leader's side from <see cref="_pool"/> that hear its call, nearest
    /// first, in <see cref="_mates"/>. True when enough are free for a group of <paramref name="kind"/>.
    /// A posse's side says who is free itself (<see cref="AnswersPosse"/>): a friend drops its errand.
    /// </summary>
    private static bool FindMates(SosariaCharacter leader, RoadGroupKind kind, Func<SosariaCharacter, bool> sameSide, DateTime now)
    {
        _mates.Clear();

        for (var i = 0; i < _pool.Count; i++)
        {
            var mate = _pool[i];

            if (mate != leader && mate.Map == leader.Map &&
                PartyRoadRules.HearsCall(kind, NavMetric.Chebyshev(leader.Location, mate.Location)) &&
                sameSide(mate) && (kind == RoadGroupKind.Posse || FreeToSetOut(mate, now)))
            {
                _mates.Add(mate);
            }
        }

        _pool.Clear();
        _mates.Sort((first, second) =>
            NavMetric.Chebyshev(leader.Location, first.Location).CompareTo(NavMetric.Chebyshev(leader.Location, second.Location))
        );
        return _mates.Count >= PartyRoadRules.MinMatesOf(kind);
    }

    /// <summary>The leader drops what it did for the group's trip. Null when the routine did not take it.</summary>
    private static PartyRoadTrip SetOut(SosariaCharacter leader, Skill trip)
    {
        var road = new PartyRoadTrip(trip);
        WorldPlay.StartWork(leader, road);
        return ReferenceEquals(leader.Routine?.CurrentSkill, road) ? road : null;
    }

    /// <summary>
    /// The leader says where it goes, aloud, once the group stands (see <see cref="Form"/>);
    /// when a mate walks over from past speech range, the call went out in guild chat too, and
    /// that mate answers there. A player who answers it, aloud or in guild chat, joins the group
    /// (<see cref="JoinPlayer"/>).
    /// </summary>
    private static void Call(SosariaCharacter leader, RoadGroup group, string category, in TalkSlots slots)
    {
        Talk.Say(leader, category, slots);
        var inChat = false;

        for (var i = 0; i < group.Mates.Count; i++)
        {
            if (World.FindMobile(group.Mates[i]) is SosariaCharacter { Guild: not null } mate &&
                !PartyRoadRules.WithinSpeech(NavMetric.Chebyshev(leader.Location, mate.Location)))
            {
                inChat = true;
                GuildChat.Say(mate, Talk.Line(TalkCategory.GuildOnMyWay));
            }
        }

        if (inChat)
        {
            group.CalledInChat = true;
            GuildChat.Say(leader, Talk.Line(category, slots));
        }
    }

    // Forms the engine party from the mates found, nearest first; each drops what it was doing
    // and walks over to follow. The first one in speech range answers aloud.
    private static RoadGroup Form(
        SosariaCharacter leader,
        RoadGroupKind kind,
        Skill trip,
        string place,
        GuildType side,
        FactionSpot? spot,
        DateTime now
    )
    {
        var group = new RoadGroup(kind, trip, place, side, spot, now);
        var take = PartyRoadRules.MatesToTake(kind, _mates.Count, Utility.Random(PartyRoadRules.PercentScale));
        var answered = false;

        for (var i = 0; i < take; i++)
        {
            var mate = _mates[i];

            if (!GameParty.TryForm(leader, mate))
            {
                continue;
            }

            group.Mates.Add(mate.Serial);
            WorldPlay.StartWork(mate, new FollowSkill(null, SosariaCombat.FollowRangeMin));

            if (!answered && PartyRoadRules.WithinSpeech(NavMetric.Chebyshev(leader.Location, mate.Location)))
            {
                answered = true;
                Talk.Say(mate, TalkCategory.LfgJoinCall, new TalkSlots { Name = leader.Name });
            }
        }

        Groups[leader.Serial] = group;
        return group;
    }

    // Alive, no outlaw, out of any fight, duel, group or flight, and out of the death grace.
    private static bool FreeToLead(SosariaCharacter character, DateTime now) =>
        character.Alive && !character.IsGhost && !character.IsPk &&
        character.Combatant == null && !character.CheckFlee() && Duels.Find(character) == null &&
        !GameParty.InParty(character) && !Groups.ContainsKey(character.Serial) &&
        WorldPlay.OutOfDeathGrace(character.LastDeathAt, now);

    // An armed fighter free to lead, standing about or on its own walk to another town.
    private static bool FreeToSetOut(SosariaCharacter character, DateTime now) =>
        FreeToLead(character, now) && character.Build?.Role == CharacterRole.Fighter && FactionWar.Armed(character) &&
        StandingAboutOrTravelling(character);

    private static bool StandingAboutOrTravelling(SosariaCharacter character) =>
        WorldPlay.IsIdle(character) || character.Routine?.CurrentSkill is TownTrip;

    private static FactionSpot? EnemyBandSpot(GuildType side)
    {
        foreach (var group in Groups.Values)
        {
            if (group.Kind == RoadGroupKind.WarBand && group.Side != side && group.Spot is { } spot)
            {
                return spot;
            }
        }

        return null;
    }

    private static int Count(RoadGroupKind kind)
    {
        var count = 0;

        foreach (var group in Groups.Values)
        {
            if (group.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static string KindWord(RoadGroupKind kind) =>
        kind switch
        {
            RoadGroupKind.Convoy => "convoy",
            RoadGroupKind.WarBand => "war band",
            RoadGroupKind.Sweep => "sweep",
            RoadGroupKind.Posse => "posse",
            _ => "Den raid"
        };
}
