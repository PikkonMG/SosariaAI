using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Logging;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Open group calls and the runs they make. A fighter or mage who sets out from a bank, a
/// moongate or a dungeon door for a dungeon or a hard hunt shouts "lfg despise anyone?", and
/// fighters standing about at those spots call groups of their own, more often the more people
/// are online; fitting people nearby (friends and guildmates first) answer and join a real
/// engine party through the engine's invite and accept. The group musters round the leader
/// with a word in party chat, travels by one gate when any of it can cast one (see
/// <see cref="PartyGate"/>), else by recall when all can, else on foot, and the members
/// follow the run: through the door together, room by room, down the stairs. If the leader dies,
/// the next living member takes the party and leads on. "gg all" and the party breaks up at
/// the end. A player can answer a call with "me" and gets a real invite, a leader may ask an
/// idle player close by along, and a player's own "lfg" gets offers. Free. No model. Calls
/// and groups are in memory only, as the engine's parties end with a restart; a leader's rest
/// between calls is its own <see cref="RuleClock.LfgShout"/> clock, which the save keeps.
/// </summary>
public static class LfgBoard
{
    public const string ReasonNobody = "nobody answered";
    public const string ReasonTripOver = "the run is over";
    public const string ReasonOutside = "the leader came back out";
    public const string ReasonLeftTrip = "the leader left the run";
    public const string ReasonWentHomeHurt = "the leader went home hurt";
    public const string ReasonOverdue = "the run ran too long";
    public const string ReasonLoggedOut = "the leader logged out";
    public const string ReasonAbandoned = "the call was left open";
    public const string ReasonAllFell = "everyone fell";
    public const string ReasonAlone = "everyone else left";

    private const string WildWord = "the wild";

    /// <summary>A talk line is picked this many times at most to find one the moment keeps.</summary>
    private const int KeptLineRolls = 3;

    private static readonly ILogger logger = SosariaLog.For(typeof(LfgBoard));
    private static readonly Dictionary<Serial, LfgGroup> Groups = new();
    private static readonly Dictionary<Serial, Skill> ShoutRolled = new();
    private static readonly Dictionary<Serial, (Serial Player, DateTime At)> Volunteers = new();
    private static readonly Dictionary<Serial, (DateTime At, int Answers)> PlayerCalls = new();
    private static int _population;
    private static DateTime _populationAt;
    private static DateTime _lastPrune;
    private static DateTime _nextSpotCall;

    /// <summary>True while the leader's call gathers people and has not set out.</summary>
    public static bool IsRecruiting(Mobile leader) =>
        leader != null && Groups.TryGetValue(leader.Serial, out var group) && !group.Running;

    /// <summary>One world scan for this character: lead its group, stand in for a fallen leader, answer a call, or shout.</summary>
    public static void Consider(SosariaCharacter character)
    {
        if (character?.Map == null || character.Map == Map.Internal || !character.Alive)
        {
            return;
        }

        var now = Core.Now;
        Prune(now);

        if (Groups.TryGetValue(character.Serial, out var own))
        {
            TickLeader(character, own, now);
            return;
        }

        if (TickMember(character, now) || TryAnswer(character, now))
        {
            return;
        }

        TryShout(character, now);
        TryPlaceCall(character, now);
    }

    /// <summary>
    /// A player's group line near a character. "me" joins an open call nearby, or a road group's
    /// call (<see cref="PartyRoads.JoinPlayer"/>); any other group line is the player's own
    /// call, and fitting characters offer to come. True when the line
    /// was a group matter, so nothing else answers it.
    /// </summary>
    public static bool HearPlayer(SosariaCharacter character, Mobile player, string text)
    {
        if (character == null || player == null || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var now = Core.Now;

        return SpeechIntent.IsJoin(text) ? JoinOpenCall(player) : AnswerPlayerCall(character, player, text, now);
    }

    /// <summary>This character offered to come along with this player and accepts the player's invite.</summary>
    public static bool Volunteered(SosariaCharacter character, Mobile inviter) =>
        character != null && inviter != null &&
        Volunteers.TryGetValue(character.Serial, out var offer) && offer.Player == inviter.Serial &&
        LfgRules.StillVolunteering(offer.At, Core.Now);

    /// <summary>Marks a character as coming to join a player, for the player's invite.</summary>
    public static void Volunteer(SosariaCharacter character, Mobile player) =>
        Volunteers[character.Serial] = (player.Serial, Core.Now);

    /// <summary>
    /// A hunt or dungeon step of this character ended. The group's own run ending ends the
    /// group, unless the run is young and the leader fit to go on: then the leader's next
    /// look takes the run up again, and the restart counts.
    /// </summary>
    public static void TripEnded(SosariaCharacter leader, Skill trip)
    {
        // A leader that died on the run hands it on; the members go on without it.
        if (leader is not { Alive: true } || !Groups.TryGetValue(leader.Serial, out var group) ||
            !group.Running || !ReferenceEquals(group.Trip, trip))
        {
            return;
        }

        var now = Core.Now;

        if (GoesOn(leader, group, now))
        {
            group.Restarts++;
            return;
        }

        Finish(leader, group, now, ReasonTripOver);
    }

    /// <summary>Fighters and mages who could answer a call: ready to fight, healthy, free, and not an outlaw.</summary>
    public static bool Fits(SosariaCharacter character) =>
        character is { Deleted: false, Alive: true } &&
        character.Build?.Role == CharacterRole.Fighter &&
        ReadyToFight(character) &&
        !GameParty.InParty(character) &&
        character.Disposition != DispositionKind.Outlaw &&
        Healthy(character) &&
        LfgRules.FreeToJoin(character.Routine?.CurrentSkill?.Name, OnFreshTrip(character) && AtMeetingSpot(character));

    /// <summary>The place a character's own run would go, as people name it.</summary>
    public static string PlaceOf(SosariaCharacter character) => TargetFor(character)?.Place;

    /// <summary>The run a character would call a group for: its dungeon hall first, else its hunt ground.</summary>
    public static LfgTarget TargetFor(SosariaCharacter character)
    {
        var hunt = character?.HuntHomeNow();

        if (hunt?.Dungeon != null && DungeonTarget(character, hunt.Dungeon.Arrival) is { } dungeon)
        {
            return dungeon;
        }

        return hunt?.Ground == null ? null : GroundTarget(character, hunt.Ground);
    }

    /// <summary>
    /// The run a character calls a group for from a meeting spot: a dungeon fit for a group
    /// of <see cref="LfgRules.ExpectedGroup"/>, where the deep and hard halls a lone fighter
    /// leaves alone (Shame, Destard, the Terathan Keep, Wrong) come in reach; else its own run.
    /// The crew that answers is weighed again at the muster (<see cref="FitToCrew"/>), and a
    /// call nobody answered goes on alone only where the caller fits (<see cref="GoesSolo"/>).
    /// </summary>
    private static LfgTarget GroupTarget(SosariaCharacter character)
    {
        var power = LfgRules.GroupPower(CharacterPower.Healthy(character), LfgRules.ExpectedGroup);
        return GroupDungeon(character, power) ?? TargetFor(character);
    }

    /// <summary>
    /// A dungeon a party can get to: the leader's own pick when the party walks to its door or
    /// the leader gates it there (<see cref="PartyReaches"/>), else a pick made on foot. A
    /// leader picked Deceit by its rune, and the party that could not recall walked for the
    /// island: 42 runs in 90 minutes ended "no route to the goal" at the muster.
    /// </summary>
    private static LfgTarget GroupDungeon(SosariaCharacter leader, int power)
    {
        var seed = Utility.Random(int.MaxValue);
        var prey = leader.CurrentAmbition().Prey;

        if (DungeonGround.PickFor(leader, power, seed, prey) is { } hall &&
            DungeonTarget(leader, hall.Arrival) is { } picked && PartyReaches(leader, picked))
        {
            return picked;
        }

        return DungeonGround.PickFor(leader, power, seed, prey, onFoot: true) is { } walked
            ? DungeonTarget(leader, walked.Arrival)
            : null;
    }

    /// <summary>A party gets to a run's door: a hunt, a door it walks to, or one its leader gates it to.</summary>
    private static bool PartyReaches(SosariaCharacter leader, LfgTarget target) =>
        !target.Dungeon || CanGateToward(leader, target) || DungeonGround.WalksToDoorOf(leader, target.At);

    /// <summary>
    /// A group call made in guild chat: the guildmates who said they are coming travel to the
    /// asker and join when they arrive. False when the asker already has a call open.
    /// </summary>
    public static bool OpenGuildCall(SosariaCharacter asker, LfgTarget target, IReadOnlyList<SosariaCharacter> coming)
    {
        var now = Core.Now;

        if (asker == null || target == null || Groups.ContainsKey(asker.Serial) || coming is not { Count: > 0 })
        {
            return false;
        }

        var group = Open(asker, target, PartyScale.GroupSizeFor(Population(now)), LfgRules.GuildCallWindow, now);

        for (var i = 0; i < coming.Count; i++)
        {
            group.Promised.Add(coming[i].Serial);
        }

        return true;
    }

    /// <summary>A player answered a character's guild call from afar: a real invite goes out at once.</summary>
    public static bool InvitePlayerToCall(SosariaCharacter leader, Mobile player)
    {
        if (leader == null || player == null || !Groups.TryGetValue(leader.Serial, out var group) || group.Running ||
            group.Joined.Contains(player.Serial) || LfgRules.Full(group.Joined.Count, group.Size) ||
            !GameParty.TryForm(leader, player))
        {
            return false;
        }

        group.Joined.Add(player.Serial);
        return true;
    }

    /// <summary>A player said yes to a leader's ask: the real invite goes out and the player counts as joined.</summary>
    public static void PlayerAccepted(SosariaCharacter leader, Mobile player)
    {
        if (leader == null || player == null || !GameParty.TryForm(leader, player))
        {
            return;
        }

        if (Groups.TryGetValue(leader.Serial, out var group) && !group.Running && !group.Joined.Contains(player.Serial))
        {
            group.Joined.Add(player.Serial);
        }
    }

    /// <summary>The group leader stands on a dungeon floor: a new floor is written once for the whole party.</summary>
    public static void NoteFloor(SosariaCharacter leader, DungeonFloor? floor)
    {
        if (floor is not { } now || leader == null || !Groups.TryGetValue(leader.Serial, out var group) || !group.Running)
        {
            return;
        }

        group.Fought = true;

        if (!DungeonGate.IsNewArrival(group.Floor, now))
        {
            return;
        }

        group.Floor = (now.Dungeon, now.Level);
        Log($"{leader.Name}'s party of {group.Joined.Count + 1} entered {now.Dungeon} level {now.Level}");
    }

    /// <summary>True while this character leads a group on its run.</summary>
    public static bool LeadsRun(Mobile leader) =>
        leader != null && Groups.TryGetValue(leader.Serial, out var group) && group.Running;

    /// <summary>
    /// True when every member of the leader's group is a character that could recall toward
    /// <paramref name="goal"/> after the leader. A player, or a member without the rune or the
    /// means, keeps the group on foot. False outside a group. <paramref name="minTripTiles"/>
    /// is the shortest trip the leader recalls for.
    /// </summary>
    public static bool CrewCanRecallTo(SosariaCharacter leader, Point3D goal, int minTripTiles = RecallRules.MinTripTiles)
    {
        if (leader == null || !Groups.TryGetValue(leader.Serial, out var group))
        {
            return false;
        }

        for (var i = 0; i < group.Joined.Count; i++)
        {
            if (World.FindMobile(group.Joined[i]) is not SosariaCharacter { Deleted: false, Alive: true } member ||
                !RecallRules.CanRecallToward(member, goal, minTripTiles))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The living characters of the leader's group standing within <paramref name="tiles"/> of
    /// it on its map, in the order they joined. Players are not in it: nobody walks them.
    /// </summary>
    public static List<SosariaCharacter> CrewNear(SosariaCharacter leader, int tiles)
    {
        var crew = new List<SosariaCharacter>();

        if (leader == null || !Groups.TryGetValue(leader.Serial, out var group))
        {
            return crew;
        }

        for (var i = 0; i < group.Joined.Count; i++)
        {
            if (World.FindMobile(group.Joined[i]) is SosariaCharacter { Deleted: false, Alive: true } member &&
                member.Map == leader.Map && NavMetric.Chebyshev(leader.Location, member.Location) <= tiles)
            {
                crew.Add(member);
            }
        }

        return crew;
    }

    /// <summary>How far the furthest living member of the leader's group stands, on the same map. Zero outside a group.</summary>
    public static int FarthestCrew(SosariaCharacter leader)
    {
        if (leader == null || !Groups.TryGetValue(leader.Serial, out var group))
        {
            return 0;
        }

        var farthest = 0;

        for (var i = 0; i < group.Joined.Count; i++)
        {
            if (World.FindMobile(group.Joined[i]) is { Deleted: false, Alive: true } member && member.Map == leader.Map)
            {
                farthest = Math.Max(farthest, NavMetric.Chebyshev(leader.Location, member.Location));
            }
        }

        return farthest;
    }

    private static void TryShout(SosariaCharacter character, DateTime now)
    {
        // One roll per run: this runs on every world scan.
        var trip = character.Routine is { NeedsNext: false } routine ? routine.CurrentSkill : null;

        if (trip is not (DungeonTripSkill or HuntSkill) ||
            ShoutRolled.TryGetValue(character.Serial, out var rolled) && ReferenceEquals(rolled, trip))
        {
            return;
        }

        ShoutRolled[character.Serial] = trip;
        var target = FreshTripTarget(character, trip);

        // A lone run taken by rune is no run for a party that must walk.
        if (target == null || !PartyReaches(character, target) ||
            !LfgRules.RollsShout(target.Dungeon, Utility.Random(LfgRules.PercentScale)) ||
            !MayLead(character, now))
        {
            return;
        }

        Shout(character, target, now);
    }

    /// <summary>
    /// A fighter of some standing standing about at a bank, a moongate or a dungeon door calls
    /// a group for its own dungeon or hunt when the shard's next spot call is due: the 1999
    /// bank and gates were where groups formed. The more people online, the sooner the next.
    /// </summary>
    private static void TryPlaceCall(SosariaCharacter character, DateTime now)
    {
        if (now < _nextSpotCall || !LfgRules.Interruptible(character.Routine?.CurrentSkill?.Name) ||
            !LfgRules.MayCallFromSpot(character.PersonProfile.Tier) || !MayLead(character, now) ||
            GroupTarget(character) is not { } target)
        {
            return;
        }

        _nextSpotCall = now + LfgRules.SpotCallGap(Population(now), Utility.RandomDouble());
        Shout(character, target, now);
    }

    // Rested, under the cap, and fit to lead: armed, healthy, ungrouped, no outlaw, at a meeting spot.
    private static bool MayLead(SosariaCharacter character, DateTime now) =>
        LfgRules.Rested(character.ClockAt(RuleClock.LfgShout), now) &&
        Groups.Count < PartyScale.HuntPartyCap(Population(now)) &&
        LfgRules.MayShout(
            character.Build?.Role == CharacterRole.Fighter,
            ReadyToFight(character),
            GameParty.InParty(character),
            character.Disposition == DispositionKind.Outlaw,
            Healthy(character),
            AtMeetingSpot(character)
        );

    private static void Shout(SosariaCharacter character, LfgTarget target, DateTime now)
    {
        var size = PartyScale.GroupSizeFor(Population(now));
        Open(character, target, size, LfgRules.RecruitWindow, now);
        Talk.Say(character, TalkCategory.LfgShout, new TalkSlots { Place = target.Place, Count = LfgRules.Needed(size) });
        Log($"{character.Name} shouted lfg for {target.Place} (wants {size})");
    }

    /// <summary>Where a run just begun goes, when it is one worth a group: any dungeon, or a hard hunt.</summary>
    private static LfgTarget FreshTripTarget(SosariaCharacter character, Skill trip) =>
        trip switch
        {
            DungeonTripSkill { ReachedInside: false } dungeon => DungeonTarget(character, dungeon.Hall),
            HuntSkill { IsHunting: false } when character.HuntHomeNow()?.Ground is { } ground &&
                                               LfgRules.StrongHunt(ground.Difficulty.GetValueOrDefault(), CharacterPower.For(character)) =>
                GroundTarget(character, ground),
            _ => null
        };

    /// <summary>True when the character could open a gate toward a dungeon run's door from where it stands.</summary>
    private static bool CanGateToward(SosariaCharacter character, LfgTarget target) =>
        target.Dungeon && GateRules.CanOpenToward(character, target.At, DungeonEntryRules.RecallMinTripTiles);

    private static LfgTarget DungeonTarget(SosariaCharacter character, Point3D hall)
    {
        var word = LfgRules.DungeonWord(DungeonGround.RegionNames(character.Map)(hall));
        return word == null ? null : new LfgTarget(true, hall, word);
    }

    private static LfgTarget GroundTarget(SosariaCharacter character, Destination ground)
    {
        var word = LfgRules.HuntWord(ground.Name, ground.Role, DungeonGround.RegionNames(character.Map)(ground.Arrival));
        return word == null ? null : new LfgTarget(false, ground.Arrival, word);
    }

    private static LfgGroup Open(SosariaCharacter leader, LfgTarget target, int size, TimeSpan window, DateTime now)
    {
        var group = new LfgGroup(leader.Serial, target, leader.Map, leader.Location, now, size, window)
        {
            HasGater = CanGateToward(leader, target)
        };
        leader.StartClock(RuleClock.LfgShout, now);
        Groups[leader.Serial] = group;
        WorldPlay.StartWork(leader, new PartyGatherSkill());
        return group;
    }

    private static bool TryAnswer(SosariaCharacter candidate, DateTime now)
    {
        if (Groups.Count == 0 || !Fits(candidate))
        {
            return false;
        }

        foreach (var group in Groups.Values)
        {
            if (group.Running || group.Map != candidate.Map || group.Answered.Contains(candidate.Serial) ||
                LfgRules.Full(group.Joined.Count, group.Size) ||
                NavMetric.Chebyshev(group.MeetAt, candidate.Location) > LfgRules.AnswerRange ||
                World.FindMobile(group.Leader) is not SosariaCharacter { Deleted: false, Alive: true } leader ||
                PkRules.IsRed(leader.Kills) != PkRules.IsRed(candidate.Kills) ||
                !PartyInviteRules.PowerFits(CharacterPower.For(candidate), CharacterPower.For(leader)))
            {
                continue;
            }

            var gates = CanGateToward(candidate, group.Target);
            var promised = group.Promised.Contains(candidate.Serial);
            var sure = promised || LfgRules.GaterWanted(group.Target.Dungeon, group.HasGater, gates);
            var friend = sure || SpeechResponder.KnowsWell(candidate, leader) ||
                         LfgRules.IsCrewmate(Recall.BondOf(MemoryStore.Shared, candidate, leader));

            if (LfgRules.StrangerMustWait(friend, now - group.ShoutedAt))
            {
                continue;
            }

            group.Answered.Add(candidate.Serial);

            if (!sure && !LfgRules.MayAnswer(friend, Utility.Random(LfgRules.PercentScale)))
            {
                continue;
            }

            if (!GameParty.InviteCharacter(leader, candidate))
            {
                return true;
            }

            // The "me" is said once the candidate is in the party, never for a join that failed.
            if (!promised)
            {
                Talk.Say(candidate, TalkCategory.LfgJoinCall, new TalkSlots { Name = leader.Name });
            }

            group.Joined.Add(candidate.Serial);
            group.HasGater |= gates;

            // The scorer picks again at once, and following the leader wins inside a party.
            candidate.Routine?.AbortActive();
            Log($"{candidate.Name} answered {leader.Name}'s lfg for {group.Place}");
            return true;
        }

        return false;
    }

    private static void TickLeader(SosariaCharacter leader, LfgGroup group, DateTime now)
    {
        DropLeavers(leader, group);

        if (group.Running)
        {
            TickRun(leader, group, now);
            return;
        }

        if (group.Mustering)
        {
            if (LfgRules.Mustered(FarthestCrew(leader), now - group.MusterSince))
            {
                SetOut(leader, group, now);
            }

            return;
        }

        TryAskPlayer(leader, group, now);

        switch (LfgRules.NextStep(group.Joined.Count, group.Size, now - group.ShoutedAt, group.Window))
        {
            case LfgCallStep.Muster:
                FitToCrew(leader, group);
                group.HasGater = CrewCanGate(leader, group);
                group.MusterSince = now;
                GameParty.Chat(leader, MusterLine(group));
                Log($"{leader.Name} musters a party of {group.Joined.Count + 1} for {group.Place}");
                return;
            case LfgCallStep.GiveUp:
                Finish(leader, group, now, ReasonNobody);

                if (!GoesSolo(leader, group.Target))
                {
                    return;
                }

                // "nm going solo": the leader makes the run it called for on its own, and says
                // so only once the run is its job. A leader that kept off the place said it and
                // went nowhere.
                var solo = NewTrip(group.Target);
                ShoutRolled[leader.Serial] = solo;
                WorldPlay.StartWork(leader, solo);

                if (ReferenceEquals(leader.Routine?.CurrentSkill, solo))
                {
                    Talk.Say(leader, TalkCategory.LfgGiveUp);
                }

                return;
        }
    }

    /// <summary>
    /// Someone of the group could open a gate toward its run, as it stands at the muster: the
    /// leader, or a character who joined. Read again for a run the crew was fitted to.
    /// </summary>
    private static bool CrewCanGate(SosariaCharacter leader, LfgGroup group)
    {
        if (CanGateToward(leader, group.Target))
        {
            return true;
        }

        for (var i = 0; i < group.Joined.Count; i++)
        {
            if (World.FindMobile(group.Joined[i]) is SosariaCharacter { Deleted: false, Alive: true } member &&
                CanGateToward(member, group.Target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The muster's word in party chat: one that speaks of a gate only when someone of the group
    /// can open one. "on me, gating to shame soon" went out for groups that then walked.
    /// </summary>
    private static string MusterLine(LfgGroup group) =>
        KeptLine(
            TalkCategory.PartyMuster,
            new TalkSlots { Place = group.Place },
            line => !group.HasGater && PartyGateRules.SpeaksOfGate(line)
        );

    /// <summary>
    /// A line of the category the moment keeps, or null when every roll of
    /// <see cref="KeptLineRolls"/> broke it: the operator's talk files may hold any line, so the
    /// code decides what is said, not the file.
    /// </summary>
    private static string KeptLine(string category, in TalkSlots slots, Func<string, bool> breaks)
    {
        for (var i = 0; i < KeptLineRolls; i++)
        {
            var line = Talk.Line(category, slots);

            if (!breaks(line))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>
    /// "nm going solo" only where the caller fits alone, with its pets: a call picked for a
    /// crew of three is no run for one. Mavis, power 206, called for Destard, nobody came, and
    /// she died to a drake on its door tile 18 seconds after she recalled in alone.
    /// </summary>
    private static bool GoesSolo(SosariaCharacter leader, LfgTarget target)
    {
        var power = DungeonGround.FightingPower(leader);
        var difficulty = DungeonGround.DifficultyAt(leader.Map?.Name, target.At);

        if (LfgRules.RunFits(difficulty, power))
        {
            return true;
        }

        Log($"{leader.Name} keeps off {target.Place} alone{HuntGround.FitNote(power, difficulty, PlaceWord(target))}");
        return false;
    }

    /// <summary>
    /// The crew mustered: when it is too light for the place called, the leader takes it where
    /// it fits, a dungeon hall for the crew's power, else the leader's own run.
    /// </summary>
    private static void FitToCrew(SosariaCharacter leader, LfgGroup group)
    {
        var power = DungeonGround.FightingPower(leader);
        var facet = leader.Map?.Name;

        if (LfgRules.RunFits(DungeonGround.DifficultyAt(facet, group.Target.At), power))
        {
            return;
        }

        var called = group.Target;
        var fitted = (called.Dungeon ? GroupDungeon(leader, power) : null) ?? TargetFor(leader);

        if (fitted == null)
        {
            return;
        }

        group.Target = fitted;
        Log(
            $"{leader.Name}'s party of {group.Joined.Count + 1} is too light for {called.Place} and goes to {fitted.Place}" +
            HuntGround.FitNote(power, DungeonGround.DifficultyAt(facet, fitted.At), PlaceWord(fitted))
        );
    }

    private static string PlaceWord(LfgTarget target) => target.Dungeon ? DungeonCrawlRules.FloorWord : HuntGround.GroundWord;

    private static void SetOut(SosariaCharacter leader, LfgGroup group, DateTime now)
    {
        group.RunningSince = now;
        StartTrip(leader, group, now);
        GameParty.Chat(leader, Talk.Line(TalkCategory.LfgStart));
        Scenes.PartyDepart(leader, group.Place);
        Log($"{leader.Name} formed a party of {group.Joined.Count + 1} for {group.Place}");
    }

    private static void StartTrip(SosariaCharacter leader, LfgGroup group, DateTime now)
    {
        group.Trip = NewTrip(group.Target);
        group.LastOnTrip = now;
        ShoutRolled[leader.Serial] = group.Trip;
        WorldPlay.StartWork(leader, group.Trip);
    }

    private static Skill NewTrip(LfgTarget target) =>
        target.Dungeon ? SkillFactory.DungeonTrip(target.At) : SkillFactory.GroundHunt(target.At);

    /// <summary>
    /// The leader on its run. A leader in a fight is on it; a leader that took up another run
    /// keeps the group on it; one that flees, mends or lies dead is waited for; one whose job
    /// a fight or a reaction cut short is put back on the run at once; one pulled off by any
    /// other pick of the goal loop goes back to the run; one that walks home hurt, or strays
    /// too often, has ended it. Two of four runs in a busy hour ended because each blow of a
    /// running fight cut the leader's job short and used up a restart.
    /// </summary>
    private static void TickRun(SosariaCharacter leader, LfgGroup group, DateTime now)
    {
        if (group.Joined.Count == 0)
        {
            Finish(leader, group, now, ReasonAlone);
            return;
        }

        if (LfgRules.TripCounts(group.RunningSince, now))
        {
            group.Fought = true;
        }

        if (leader.Combatant is { Deleted: false, Alive: true })
        {
            group.LastOnTrip = now;
            return;
        }

        var routine = leader.Routine;
        var current = routine is { NeedsNext: false } ? routine.CurrentSkill : null;

        // Out of the dungeon on the way home, by the door or a recall, the group has done its
        // run, unless it came up early with the leader fit: then the group goes back in.
        if (current is DungeonTripSkill { HeadingHome: true, ReachedInside: true } && !DungeonTripSkill.InDungeon(leader))
        {
            if (!GoesOn(leader, group, now))
            {
                Finish(leader, group, now, ReasonOutside);
                return;
            }

            group.Restarts++;
            StartTrip(leader, group, now);
            return;
        }

        if (ReferenceEquals(current, group.Trip) || LfgRules.SameRun(group.Target.Dungeon, current?.Name))
        {
            group.Trip = current;
            group.LastOnTrip = now;
            return;
        }

        var hurt = leader.HitsMax > 0 && (double)leader.Hits / leader.HitsMax < DungeonCrawlRules.LeaveHitsFraction;

        switch (LfgRules.Detour(current?.Name, hurt))
        {
            case RunDetour.End:
                Finish(leader, group, now, ReasonWentHomeHurt);
                return;
            case RunDetour.Hold:
                if (LfgRules.HoldOver(group.LastOnTrip, now))
                {
                    Finish(leader, group, now, ReasonLeftTrip);
                }

                return;
        }

        var counts = LfgRules.CountsRestart(current?.Name);

        if (!counts || group.Restarts < LfgRules.MaxTripRestarts)
        {
            group.Restarts += counts ? 1 : 0;
            StartTrip(leader, group, now);
            return;
        }

        if (LfgRules.TripOver(group.LastOnTrip, now))
        {
            Finish(leader, group, now, ReasonLeftTrip);
        }
    }

    /// <summary>A run that ended early goes on: young, the crew still there, the leader well, stocked and not overloaded.</summary>
    private static bool GoesOn(SosariaCharacter leader, LfgGroup group, DateTime now) =>
        group.Joined.Count > 0 &&
        LfgRules.RunGoesOn(
            now - group.RunningSince,
            Healthy(leader),
            SupplyCheck.IsLow(leader),
            CarryLoad.IsLoaded(leader, SosariaCombat.HuntPackFillFraction),
            group.Restarts
        );

    /// <summary>
    /// A member of a running group. While the leader lies dead the first living member
    /// takes the party and leads the run on. True while this character is on a group's run.
    /// </summary>
    private static bool TickMember(SosariaCharacter member, DateTime now)
    {
        var group = RunningGroupOf(member);

        if (group == null)
        {
            return false;
        }

        if (World.FindMobile(group.Leader) is not SosariaCharacter { Deleted: false } leader || leader.Alive)
        {
            group.LeaderDownAt = default;
            return true;
        }

        if (group.LeaderDownAt == default)
        {
            group.LeaderDownAt = now;
        }

        if (LfgRules.TakeOverDue(group.LeaderDownAt, now) && Successor(leader) == member)
        {
            TakeOver(group, leader, member, now);
        }

        return true;
    }

    private static LfgGroup RunningGroupOf(SosariaCharacter member)
    {
        foreach (var group in Groups.Values)
        {
            if (group.Running && group.Joined.Contains(member.Serial))
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>The first living character in the fallen leader's party, in the party's order. Null when none, or a player is in it.</summary>
    private static SosariaCharacter Successor(SosariaCharacter leader)
    {
        var party = GameParty.Of(leader);

        if (party == null || GameParty.HasPlayer(party))
        {
            return null;
        }

        var fit = new bool[party.Members.Count];
        var leaderIndex = PartyLead.NoOne;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var mobile = party.Members[i].Mobile;
            fit[i] = mobile is SosariaCharacter { Deleted: false, Alive: true };
            leaderIndex = mobile == leader ? i : leaderIndex;
        }

        var next = PartyLead.Successor(leaderIndex, fit);
        return next == PartyLead.NoOne ? null : party.Members[next].Mobile as SosariaCharacter;
    }

    /// <summary>
    /// The engine gives a party no new leader, so the next member forms the party again under
    /// itself, the fallen leader included so the group can raise it, and leads the run on.
    /// </summary>
    private static void TakeOver(LfgGroup group, SosariaCharacter fallen, SosariaCharacter heir, DateTime now)
    {
        var crew = new List<SosariaCharacter>();
        var party = GameParty.Of(fallen);

        for (var i = 0; i < (party?.Members.Count ?? 0); i++)
        {
            if (party.Members[i].Mobile is SosariaCharacter { Deleted: false } member && member != heir)
            {
                crew.Add(member);
            }
        }

        Groups.Remove(fallen.Serial);
        GameParty.Leave(fallen, null);
        group.Leader = heir.Serial;
        group.LeaderDownAt = default;
        group.Joined.Clear();

        for (var i = 0; i < crew.Count; i++)
        {
            if (GameParty.InviteCharacter(heir, crew[i]))
            {
                group.Joined.Add(crew[i].Serial);
            }
        }

        Groups[heir.Serial] = group;
        group.Restarts = 0;
        StartTrip(heir, group, now);
        GameParty.Chat(heir, Talk.Line(TalkCategory.PartyTakeLead, new TalkSlots { Name = fallen.Name }));
        Log($"{heir.Name} took over {fallen.Name}'s party of {group.Joined.Count + 1} for {group.Place}");
    }

    private static void Finish(SosariaCharacter leader, LfgGroup group, DateTime now, string reason)
    {
        Groups.Remove(group.Leader);
        leader.StartClock(RuleClock.LfgShout, now);
        var party = GameParty.Of(leader);
        AdventureTracker.Shared.PartyFinished(party, now);
        var names = new List<string>();
        var crew = new List<Mobile>();

        for (var i = 0; i < group.Joined.Count; i++)
        {
            if (World.FindMobile(group.Joined[i]) is { Deleted: false } member && party?.Contains(member) == true)
            {
                names.Add(member.Name);
                crew.Add(member);
            }
        }

        var ran = group.Running && group.Fought;

        // "gg, im heading to bank" with no bank trip behind it: the gg claims no trip.
        if (ran && names.Count > 0)
        {
            GameParty.Chat(leader, KeptLine(TalkCategory.LfgGg, default, PromiseLines.ClaimsTrip));
            ShardNews.PartyRun(leader, names, group.Target.At, now);
        }

        for (var i = 0; i < crew.Count; i++)
        {
            if (crew[i] is not SosariaCharacter member)
            {
                continue;
            }

            if (ran && Utility.Random(LfgRules.PercentScale) < LfgRules.GgReplyPercent)
            {
                GameParty.Chat(member, Talk.Line(TalkCategory.LfgGgReply));
            }

            GameParty.Leave(member, null);
        }

        // The engine breaks up a party when its leader leaves, players included: the run is over.
        if (party != null && party.Leader == leader)
        {
            GameParty.Leave(leader, null);
        }

        if (ran && crew.Count > 0)
        {
            Scenes.PartyReturn(leader, crew, group.Place);
        }

        if (group.Running)
        {
            var minutes = (int)(now - group.RunningSince).TotalMinutes;
            Log($"{leader.Name}'s party of {crew.Count + 1} disbanded at {WhereWord(leader)} after {minutes} min: {reason}");
        }
        else
        {
            Log($"{leader.Name}'s lfg for {group.Place} closed: {reason}");
        }
    }

    /// <summary>Asks one idle player of like power standing near an open call to come along. Once per call.</summary>
    private static void TryAskPlayer(SosariaCharacter leader, LfgGroup group, DateTime now)
    {
        if (group.AskedPlayer || leader.InviteAskedAt != default || now < leader.InviteCooldownUntil ||
            LfgRules.Full(group.Joined.Count, group.Size))
        {
            return;
        }

        var mobile = PlayerToAsk(leader, group);

        if (mobile == null)
        {
            return;
        }

        group.AskedPlayer = true;
        var idle = InviteAskRules.PlayerIdle(
            mobile.Combatant != null,
            mobile.Warmode,
            TimeSpan.FromMilliseconds(Core.TickCount - mobile.LastMoveTime)
        );

        if (InviteAskRules.MayAskPlayer(
                idle,
                PartyInviteRules.PowerFits(CharacterPower.For(leader), CharacterPower.For(mobile)),
                InviteAskRules.RemembersDecline(Recall.BondOf(MemoryStore.Shared, leader, mobile)),
                GameParty.PlayerAskRested(mobile, now),
                Utility.Random(LfgRules.PercentScale)
            ))
        {
            GameParty.AskPlayer(leader, mobile, group.Place);
        }
    }

    /// <summary>
    /// The player near the call the leader most wants along (<see cref="LfgRules.InviteRank"/>):
    /// a friend it ran with before a stranger. Null when no player there may be asked.
    /// </summary>
    private static Mobile PlayerToAsk(SosariaCharacter leader, LfgGroup group)
    {
        var players = new List<Mobile>();
        var bonds = new List<Bond>();
        var leaderId = Recall.IdOf(leader);

        foreach (var mobile in leader.Map.GetMobilesInRange(leader.Location, LfgRules.AnswerRange))
        {
            if (!People.IsHuman(mobile) || !GameParty.IsApproachable(leader, mobile) || GameParty.InParty(mobile) ||
                group.Joined.Contains(mobile.Serial))
            {
                continue;
            }

            players.Add(mobile);
            bonds.Add(MemoryStore.Shared.BondOf(leaderId, Recall.IdOf(mobile)));
        }

        return players.Count == 0 ? null : players[LfgRules.FavoredIndex(bonds)];
    }

    /// <summary>A player's "me": an open group call nearby first, else a road group's call (<see cref="PartyRoads.JoinPlayer"/>).</summary>
    private static bool JoinOpenCall(Mobile player) => JoinLfgCall(player) || JoinRoadCall(player);

    private static bool JoinRoadCall(Mobile player)
    {
        if (PartyRoads.JoinPlayer(player, null, out var invited) is not { } leader)
        {
            return false;
        }

        if (invited)
        {
            SpeechResponder.SayTo(leader, player, Talk.Line(TalkCategory.LfgInvite, new TalkSlots { Name = player.Name }));
        }

        return true;
    }

    private static bool JoinLfgCall(Mobile player)
    {
        foreach (var group in Groups.Values)
        {
            if (group.Running || group.Map != player.Map ||
                NavMetric.Chebyshev(group.MeetAt, player.Location) > LfgRules.AnswerRange ||
                World.FindMobile(group.Leader) is not SosariaCharacter { Deleted: false, Alive: true } leader)
            {
                continue;
            }

            if (group.Joined.Contains(player.Serial))
            {
                return true;
            }

            if (LfgRules.Full(group.Joined.Count, group.Size) || !GameParty.TryForm(leader, player))
            {
                continue;
            }

            group.Joined.Add(player.Serial);
            SpeechResponder.SayTo(leader, player, Talk.Line(TalkCategory.LfgInvite, new TalkSlots { Name = player.Name }));
            Log($"{player.Name} answered {leader.Name}'s lfg for {group.Place}");
            return true;
        }

        return false;
    }

    private static bool AnswerPlayerCall(SosariaCharacter character, Mobile player, string text, DateTime now)
    {
        if (GameParty.Of(player)?.Contains(character) == true)
        {
            return true;
        }

        var named = AttentionGate.MentionsName(text, character.Name);
        var seed = Utility.Random(int.MaxValue);

        if (!Fits(character) || Groups.ContainsKey(character.Serial))
        {
            if (named)
            {
                SpeechResponder.SayTo(character, player, Talk.Line(TalkCategory.LfgDecline, seed, default));
            }

            return true;
        }

        var call = PlayerCalls.TryGetValue(player.Serial, out var open) && now - open.At <= LfgRules.PlayerCallWindow
            ? open
            : (At: now, Answers: 0);

        if (!named && (call.Answers >= LfgRules.PlayerCallAnswers ||
                       Utility.Random(LfgRules.PercentScale) >= LfgRules.PlayerCallAnswerPercent))
        {
            PlayerCalls[player.Serial] = call;
            return true;
        }

        PlayerCalls[player.Serial] = (call.At, call.Answers + 1);
        Volunteer(character, player);
        SpeechResponder.SayTo(character, player, Talk.Line(TalkCategory.LfgOfferPlayer, seed, new TalkSlots { Name = player.Name }));
        Log($"{character.Name} offered to group with {player.Name}");
        return true;
    }

    private static void DropLeavers(SosariaCharacter leader, LfgGroup group)
    {
        var party = GameParty.Of(leader);

        // A player's invite may still be pending; anyone neither in the party nor invited is dropped.
        group.Joined.RemoveAll(serial =>
            World.FindMobile(serial) is not { Deleted: false } member ||
            party?.Contains(member) != true && party?.Candidates.Contains(member) != true);
    }

    private static void Prune(DateTime now)
    {
        if (now - _lastPrune < LfgRules.PruneGap)
        {
            return;
        }

        _lastPrune = now;
        List<(LfgGroup Group, string Reason)> stale = null;

        foreach (var group in Groups.Values)
        {
            var reason = StaleReason(group, now);

            if (reason != null)
            {
                (stale ??= []).Add((group, reason));
            }
        }

        for (var i = 0; i < (stale?.Count ?? 0); i++)
        {
            var (group, reason) = stale[i];

            if (World.FindMobile(group.Leader) is SosariaCharacter { Deleted: false } leader)
            {
                Finish(leader, group, now, reason);
            }
            else
            {
                Groups.Remove(group.Leader);
            }
        }
    }

    /// <summary>Why a group can no longer go on, or null while it can.</summary>
    private static string StaleReason(LfgGroup group, DateTime now)
    {
        if (World.FindMobile(group.Leader) is not SosariaCharacter { Deleted: false } leader)
        {
            return ReasonLoggedOut;
        }

        if (leader.Map == null || leader.Map == Map.Internal)
        {
            return ReasonLoggedOut;
        }

        if (LfgRules.TripOverdue(group.RunningSince, now))
        {
            return ReasonOverdue;
        }

        if (LfgRules.CallAbandoned(group.Running, group.ShoutedAt, group.Window, now))
        {
            return ReasonAbandoned;
        }

        // A player in the party keeps it together while the leader waits for a raise.
        if (!group.Running || leader.Alive || GameParty.InPlayerParty(leader))
        {
            return null;
        }

        // A dead leader with nobody left standing to lead on: the run is over.
        group.LeaderDownAt = group.LeaderDownAt == default ? now : group.LeaderDownAt;
        return LfgRules.TakeOverDue(group.LeaderDownAt, now) && Successor(leader) == null ? ReasonAllFell : null;
    }

    /// <summary>People in the world now: characters and connected players, counted once a minute.</summary>
    public static int Population(DateTime now)
    {
        if (_populationAt != default && now - _populationAt < LfgRules.PopulationRefresh)
        {
            return _population;
        }

        var count = 0;

        foreach (var mobile in World.Mobiles.Values)
        {
            if ((mobile is SosariaCharacter { Deleted: false } character && character.Map != null &&
                 character.Map != Map.Internal) ||
                (People.IsHuman(mobile) && mobile.NetState != null))
            {
                count++;
            }
        }

        _population = count;
        _populationAt = now;
        return count;
    }

    /// <summary>
    /// Armed (<see cref="SpareKit.Armed"/>), with a weapon in hand or Magery and a spellbook to
    /// fight with. A weapon or a book alone let a stripped mage answer an orc cave call naked.
    /// </summary>
    private static bool ReadyToFight(SosariaCharacter character) =>
        SpareKit.Armed(character) &&
        (GearScore.HasWeapon(character) ||
         SpellBook.IsCaster(character.Skills.Magery.Value) && Spellbook.FindRegular(character) != null);

    /// <summary>A dungeon run or a hunt that has not reached its fight yet.</summary>
    private static bool OnFreshTrip(SosariaCharacter character) =>
        character.Routine is { NeedsNext: false } routine &&
        routine.CurrentSkill is DungeonTripSkill { ReachedInside: false } or HuntSkill { IsHunting: false };

    private static bool Healthy(SosariaCharacter character) =>
        character.HitsMax <= 0 || (double)character.Hits / character.HitsMax >= PartyInviteRules.MinHitsFraction;

    /// <summary>Where groups formed: in town or at a bank, by a public moongate, or outside a dungeon door.</summary>
    private static bool AtMeetingSpot(SosariaCharacter character) =>
        SosariaCharacter.UnderGuards(character) || Meeting.AtMeetingPoint(character.Location) ||
        BankPlaza.Contains(character.Location, BankPlaza.BankFor(NavWorld.DestinationsFor(character.HomeFacet), character.Location)) ||
        NearMoongate(character) || AtDungeonDoor(character);

    private static bool NearMoongate(SosariaCharacter character)
    {
        if (character.Map is not { } map || map == Map.Internal)
        {
            return false;
        }

        foreach (var _ in map.GetItemsInRange<PublicMoongate>(character.Location, LfgRules.MoongateCallTiles))
        {
            return true;
        }

        return false;
    }

    private static bool AtDungeonDoor(SosariaCharacter character)
    {
        if (DungeonTripSkill.InDungeon(character))
        {
            return false;
        }

        var doors = DungeonGround.DoorsOf(character.Map);

        for (var i = 0; i < doors.Count; i++)
        {
            if (NavMetric.Chebyshev(character.Location, doors[i].Door) <= LfgRules.DoorCallTiles)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Where the leader stands, as the log names it: a dungeon floor, a region, or the wild.</summary>
    private static string WhereWord(SosariaCharacter leader)
    {
        if (leader.Map != null && leader.Map != Map.Internal &&
            DungeonAtlas.For(leader.Map.Name).FloorAt(leader.Location) is { } floor)
        {
            return $"{floor.Dungeon} level {floor.Level}";
        }

        var region = leader.Region?.Name;
        return string.IsNullOrWhiteSpace(region) ? WildWord : region;
    }

    private static void Log(string line)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", line);
        }
    }
}
