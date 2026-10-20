using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using EngineParty = Server.Engines.PartySystem.Party;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// One order layer for real parties. The leader, a bot or a person, gives orders that the bot
/// members act on; any person in the party may call for help, for backup, for a regroup on
/// itself, or ask where everyone is. A person's line is read by word list, in party chat or
/// out loud; a bot leader gives the same orders from code at set moments and says the matching
/// line in party chat, so the words always match the action. A member that sets off to a
/// caller says so. A bot in a hard fight calls for backup from code the same way. Bot chat is
/// never read as an order, so bots cannot order each other by accident. With
/// <see cref="BrainConfiguration.PartyOrdersDirect"/> an order replaces the member's job at
/// once; with <see cref="BrainConfiguration.PartyOrdersPlanner"/> it raises a plan trigger and
/// the goal loop picks the order skill at the top score.
/// </summary>
public static class PartyOrders
{
    private static readonly ILogger logger = SosariaLog.For(typeof(PartyOrders));

    private const string AttackTag = "attack";
    private const string BackTag = "back";
    private const string HealTag = "heal";
    private const string HoldTag = "hold";
    private const string RegroupTag = "regroup";
    private const string GoToTag = "goto";
    private const string StuckTag = "stuck";
    private const string OnMyWayTag = "omw";

    private static readonly Dictionary<Serial, PartyOrder> Orders = new();

    /// <summary>The one member each bot leader waits for, by the leader.</summary>
    private static readonly Dictionary<Serial, HelpCall> Waits = new();

    /// <summary>When a leader last gave up waiting for a member, so it does not wait for the same one again at once.</summary>
    private static readonly Dictionary<(Serial Leader, Serial Caller), DateTime> GaveUp = new();

    /// <summary>Where a far follower last stood and since when, for the stuck call.</summary>
    private static readonly Dictionary<Serial, (Point3D At, DateTime Since)> Still = new();

    /// <summary>Leaders seen in a fight on the last scan: the fight's end is a moment to regroup.</summary>
    private static readonly HashSet<Serial> Fighting = [];

    /// <summary>The run each leader last sent its group to, so a run is announced once.</summary>
    private static readonly Dictionary<Serial, Skill> LastTrip = new();

    private static readonly LogGate<(Serial Leader, string Tag, Serial About)> LeaderGate = new(PartyOrderRules.LeaderQuiet);

    /// <summary>One backup call per member in <see cref="PartyOrderRules.BackupQuiet"/>: a long fight is one call, not a scream a scan.</summary>
    private static readonly LogGate<Serial> BackupGate = new(PartyOrderRules.BackupQuiet);

    private sealed record HelpCall(PartyHelpKind Kind, Serial Caller, DateTime Since);

    public static bool ViaPlanner => Brain.PartyOrdersViaPlanner;

    public static PartyOrder Current(Mobile member) =>
        member != null && Orders.TryGetValue(member.Serial, out var order) ? order : null;

    /// <summary>The living foe the leader's attack order sent this member onto, or null: the party's called target.</summary>
    public static Mobile CalledFoe(Mobile member) =>
        Current(member) is { Kind: PartyOrderKind.Attack, Target: var target } &&
        World.FindMobile(target) is { Deleted: false, Alive: true } foe
            ? foe
            : null;

    /// <summary>True while a bot leader stands for a member: its trip does not step on.</summary>
    public static bool LeaderWaits(Mobile leader) => leader != null && Waits.ContainsKey(leader.Serial);

    /// <summary>
    /// A person's line reached a member, in party chat or out loud. True when the line was an
    /// order or a help call the member took, so nothing else answers it.
    /// </summary>
    public static bool Hear(SosariaCharacter listener, Mobile speaker, string text)
    {
        if (listener == null || speaker == null || speaker == listener || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var party = GameParty.Of(speaker);

        if (party == null || !party.Contains(listener))
        {
            return false;
        }

        var human = People.IsHuman(speaker);
        var help = PartyOrderRules.ReadHelp(text);

        if (help != PartyHelpKind.None)
        {
            return PartyOrderRules.MayCallHelp(human) && TakeHelp(listener, speaker, party, help, text);
        }

        var kind = PartyOrderRules.ReadOrder(text, out var remainder);

        if (kind == PartyOrderKind.None || !PartyOrderRules.MayOrder(kind, party.Leader == speaker, human))
        {
            return false;
        }

        Take(listener, speaker, kind, remainder);
        return true;
    }

    /// <summary>
    /// One world scan: a standing order runs out or settles, a bot leader looks over its group,
    /// and a far follower that stands still calls itself stuck.
    /// </summary>
    public static void Consider(SosariaCharacter character)
    {
        if (!People.InWorld(character) || character.IsGhost)
        {
            return;
        }

        var now = Core.Now;
        var party = GameParty.Of(character);

        if (party == null || party.Count < 2)
        {
            Forget(character);
            return;
        }

        TickOrder(character, party, now);
        CallBackup(character, party, now);

        if (party.Leader == character)
        {
            TickLeader(character, party, now);
        }
        else
        {
            TickStuck(character, now);
        }
    }

    /// <summary>
    /// A bot in a hard fight, hurt below half or with two foes on it, calls the group for backup
    /// as a person would, once per <see cref="PartyOrderRules.BackupQuiet"/>: the line goes out
    /// and the free members set off, the same as on a person's call.
    /// </summary>
    private static void CallBackup(SosariaCharacter caller, EngineParty party, DateTime now)
    {
        var foe = caller.Combatant;

        if (foe is not { Deleted: false, Alive: true } || party.Contains(foe) ||
            !PartyOrderRules.CallsBackup(inFight: true, Vitals.HitsFraction(caller), Attackers(caller)) ||
            !BackupGate.Opens(caller.Serial, now))
        {
            return;
        }

        Chat(caller, TalkCategory.OrderNeedBackup, new TalkSlots { Foe = FoeWord(foe) });
        Log(() => $"{caller.Name} calls for backup against {foe.Name}");

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is SosariaCharacter mate && mate != caller)
            {
                AnswerBackup(mate, caller, party, now);
            }
        }
    }

    /// <summary>The living foes on this character now, in sight: the engine's aggressor list, the expired ones left out.</summary>
    private static int Attackers(SosariaCharacter character)
    {
        var count = 0;

        for (var i = 0; i < character.Aggressors.Count; i++)
        {
            var info = character.Aggressors[i];

            if (!info.Expired && info.Attacker is { Deleted: false, Alive: true } attacker && attacker.Map == character.Map &&
                character.InRange(attacker, character.RangePerception))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// One member answers a backup call: it takes the caller's foe when it may, else it walks
    /// to the caller. A bot leader keeps its own run: it takes the foe in sight, or walks back
    /// to the caller as it does for a stuck member, and its followers come with it. The member
    /// that sets off says so in party chat.
    /// </summary>
    private static void AnswerBackup(SosariaCharacter member, Mobile caller, EngineParty party, DateTime now)
    {
        if (member.Map != caller.Map || !member.Alive || !PartyOrderRules.TakesBackup(Current(member)?.Kind ?? PartyOrderKind.None, member.Motor.Action == CharacterAction.Flee))
        {
            return;
        }

        var foe = caller.Combatant is { Deleted: false, Alive: true } fought && !party.Contains(fought) && FollowSkill.TakesLeadersFoe(member, fought)
            ? fought
            : null;
        bool setOff;

        if (party.Leader == member)
        {
            if (foe != null && member.InRange(foe, member.RangePerception))
            {
                setOff = member.Combatant != foe;
                member.Combatant = foe;
            }
            else
            {
                setOff = OpenWait(member, PartyHelpKind.Backup, caller, now);
            }
        }
        else
        {
            setOff = Give(
                member,
                foe != null
                    ? new PartyOrder(PartyOrderKind.Attack, caller.Serial, now, Target: foe.Serial)
                    : new PartyOrder(PartyOrderKind.Regroup, caller.Serial, now)
            );
        }

        if (setOff)
        {
            Say(member, OnMyWayTag, caller.Serial, TalkCategory.OrderOnMyWay, default, now);
        }
    }

    /// <summary>Gives an order. True when it is new: the same order again changes nothing.</summary>
    public static bool Give(SosariaCharacter member, PartyOrder order, bool ack = false)
    {
        if (member == null || order == null || Current(member) is { } standing && standing.SameAs(order.Kind, order.Target, order.Place))
        {
            return false;
        }

        Orders[member.Serial] = order;
        Log(() => $"{GiverName(order)} ordered {member.Name}: {Describe(order)}");

        if (ack && Utility.Random(TalkOdds.PercentScale) < PartyOrderRules.AckPercent)
        {
            Chat(member, TalkCategory.OrderAck);
        }

        Start(member);
        return true;
    }

    /// <summary>An order ran its course: the member follows the leader again.</summary>
    public static void Settle(SosariaCharacter member)
    {
        if (Current(member) is not { } order || order.Kind == PartyOrderKind.Follow)
        {
            return;
        }

        Orders[member.Serial] = new PartyOrder(PartyOrderRules.AfterDone(order.Kind), order.From, Core.Now);
        Log(() => $"{member.Name} settles to follow after {order.Kind}");
    }

    /// <summary>A walk to a place reached it: the member holds there until the leader arrives.</summary>
    public static void Arrived(SosariaCharacter member)
    {
        if (Current(member) is not { Kind: PartyOrderKind.GoTo } order)
        {
            return;
        }

        Orders[member.Serial] = new PartyOrder(PartyOrderKind.Hold, order.From, Core.Now, Place: order.Place, PlaceName: order.PlaceName);
        Log(() => $"{member.Name} reached {order.PlaceName} and holds for the leader");
    }

    /// <summary>A walk to a place found no way: the member says so and follows instead.</summary>
    public static void CannotReach(SosariaCharacter member)
    {
        if (Current(member) is { Kind: PartyOrderKind.GoTo } order)
        {
            Chat(member, TalkCategory.OrderCantReach, new TalkSlots { Place = order.PlaceName });
        }

        Settle(member);
    }

    public static void Drop(Mobile member)
    {
        if (member != null && Orders.Remove(member.Serial))
        {
            Log(() => $"{member.Name} has no party order now");
        }
    }

    private static void Forget(SosariaCharacter character)
    {
        Drop(character);
        Waits.Remove(character.Serial);
        Still.Remove(character.Serial);
        Fighting.Remove(character.Serial);
        LastTrip.Remove(character.Serial);

        // A give-up names the character as the leader or as the member waited for.
        foreach (var (key, _) in GaveUp)
        {
            if (key.Leader == character.Serial || key.Caller == character.Serial)
            {
                GaveUp.Remove(key);
            }
        }
    }

    /// <summary>
    /// True once the leader may wait for this member again. A give-up whose rest passed is
    /// dropped here, so a person who left the party does not stay in the table for good.
    /// </summary>
    private static bool WaitRested(Serial leader, Serial member, DateTime now)
    {
        var key = (leader, member);

        if (!GaveUp.TryGetValue(key, out var gaveUpAt))
        {
            return true;
        }

        if (!PartyOrderRules.WaitRested(gaveUpAt, now))
        {
            return false;
        }

        GaveUp.Remove(key);
        return true;
    }

    private static void Take(SosariaCharacter member, Mobile leader, PartyOrderKind kind, string remainder)
    {
        var now = Core.Now;

        switch (kind)
        {
            case PartyOrderKind.GoTo:
            {
                if (PlaceLookup.Resolve(NavWorld.DestinationsFor(member.HomeFacet ?? member.Map?.Name), remainder, member.Location) is not { } place)
                {
                    Chat(member, TalkCategory.OrderUnknownPlace);
                    return;
                }

                Give(member, new PartyOrder(kind, leader.Serial, now, Place: place.Arrival, PlaceName: place.Name), ack: true);
                return;
            }
            case PartyOrderKind.Attack:
            {
                if (ResolveFoe(member, leader, remainder) is not { } foe)
                {
                    Chat(member, TalkCategory.OrderNoTarget);
                    return;
                }

                Give(member, new PartyOrder(kind, leader.Serial, now, Target: foe.Serial), ack: true);
                return;
            }
            default:
            {
                Give(member, new PartyOrder(kind, leader.Serial, now), ack: true);
                return;
            }
        }
    }

    private static bool TakeHelp(SosariaCharacter listener, Mobile caller, EngineParty party, PartyHelpKind kind, string text)
    {
        var now = Core.Now;

        switch (kind)
        {
            case PartyHelpKind.HealMe:
            {
                if (HealerOnTheWay(party, caller))
                {
                    return true;
                }

                var healer = BestHealer(party, caller);

                if (healer == null)
                {
                    if (Nearest(party, caller) == listener)
                    {
                        Chat(listener, TalkCategory.OrderNoHeal);
                    }

                    return true;
                }

                if (healer == listener && StartHeal(listener, caller))
                {
                    Say(listener, OnMyWayTag, caller.Serial, TalkCategory.OrderOnMyWay, default, now);
                }

                return true;
            }
            case PartyHelpKind.Backup:
            {
                AnswerBackup(listener, caller, party, now);
                return true;
            }
            case PartyHelpKind.Where:
            {
                AnswerWhere(listener, caller, party, text);
                return true;
            }
        }

        // Only a bot leader can stand for a member; a member hearing the call has nothing to do.
        if (party.Leader != listener || !OpenWait(listener, kind, caller, now))
        {
            return true;
        }

        var category = kind == PartyHelpKind.Stuck ? TalkCategory.OrderComingBack : TalkCategory.OrderHold;
        Say(listener, HoldTag, caller.Serial, category, new TalkSlots { Name = caller.Name }, now);
        return true;
    }

    /// <summary>
    /// "Where are you" is answered in party chat, which carries any distance: the spot as
    /// people name it, and the way there from the asker. A line that names one member gets
    /// that member's answer alone.
    /// </summary>
    private static void AnswerWhere(SosariaCharacter listener, Mobile caller, EngineParty party, string text)
    {
        if (!PartyOrderRules.AnswersWhere(AttentionGate.MentionsName(text, listener.Name), NamesAnother(party, listener, text)))
        {
            return;
        }

        var sameMap = listener.Map == caller.Map;
        var bearing = PartyOrderRules.Bearing(
            sameMap,
            sameMap ? (int)listener.GetDistanceToSqrt(caller) : int.MaxValue,
            DirectionRules.Compass(caller.Location, listener.Location)
        );

        Chat(listener, TalkCategory.OrderWhereAmI, new TalkSlots { Place = GossipLines.PlaceWord(PlaceNames.Of(listener)), Bearing = bearing });
    }

    /// <summary>True when the line names a party member other than this one.</summary>
    private static bool NamesAnother(EngineParty party, SosariaCharacter listener, string text)
    {
        for (var i = 0; i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;

            if (member != null && member != listener && AttentionGate.MentionsName(text, member.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static void Start(SosariaCharacter member)
    {
        if (!People.InWorld(member) || member.IsGhost)
        {
            return;
        }

        if (ViaPlanner)
        {
            Brain.RequestPlan(member, PlanTrigger.PartyOrder);
            member.ScoreAndCommit();
            return;
        }

        // A member already on the order skill reads the new order on its next tick.
        if (member.Routine is { NeedsNext: false, CurrentSkill: PartyOrderSkill })
        {
            return;
        }

        WorldPlay.StartWork(member, new PartyOrderSkill());
    }

    private static void TickOrder(SosariaCharacter member, EngineParty party, DateTime now)
    {
        if (Current(member) is not { } order)
        {
            return;
        }

        var giver = World.FindMobile(order.From);
        var giverStands = giver is { Deleted: false, Alive: true } && party.Contains(giver);

        if (!giverStands || member.Motor.Action == CharacterAction.Flee || PartyOrderRules.Expired(order.Kind, order.At, now))
        {
            Settle(member);
        }
    }

    private static void TickLeader(SosariaCharacter leader, EngineParty party, DateTime now)
    {
        var members = Members(party, leader);
        TickWait(leader, party, members, now);
        NoticeFight(leader, party, members, now);
        NoticeHurt(leader, party, members, now);
        NoticeLag(leader, members, now);
        NoticeTrip(leader, members, now);
    }

    private static void TickWait(SosariaCharacter leader, EngineParty party, List<Mobile> members, DateTime now)
    {
        if (!Waits.TryGetValue(leader.Serial, out var call))
        {
            return;
        }

        var caller = World.FindMobile(call.Caller);
        var present = caller is { Deleted: false, Alive: true } && party.Contains(caller);
        var sameMap = present && caller.Map == leader.Map;
        var distance = sameMap ? (int)leader.GetDistanceToSqrt(caller) : int.MaxValue;

        if (PartyOrderRules.WaitEnds(present, sameMap, distance, call.Since, now))
        {
            Waits.Remove(leader.Serial);
            ReleaseHolds(leader, members);

            if (PartyOrderRules.HelpWaitOver(call.Since, now))
            {
                GaveUp[(leader.Serial, call.Caller)] = now;
            }

            Log(() => $"{leader.Name} stops waiting for {caller?.Name ?? call.Caller.ToString()}");
            return;
        }

        if (call.Kind is PartyHelpKind.Stuck or PartyHelpKind.Backup && sameMap)
        {
            leader.Motor.MoveTo(caller, PartyOrderRules.WalkBackRange);
        }
    }

    private static bool OpenWait(SosariaCharacter leader, PartyHelpKind kind, Mobile caller, DateTime now)
    {
        if (Waits.ContainsKey(leader.Serial))
        {
            return false;
        }

        Waits[leader.Serial] = new HelpCall(kind, caller.Serial, now);
        leader.Motor.Stop();
        Log(() => $"{leader.Name} waits for {caller.Name} ({kind})");
        return true;
    }

    /// <summary>The members held back for a wait follow again once it ends; a hold at a place stands.</summary>
    private static void ReleaseHolds(SosariaCharacter leader, List<Mobile> members)
    {
        foreach (var member in members)
        {
            if (member is SosariaCharacter bot && Current(bot) is { Kind: PartyOrderKind.Hold, Place: var place } held &&
                held.From == leader.Serial && place == Point3D.Zero)
            {
                Give(bot, new PartyOrder(PartyOrderKind.Follow, leader.Serial, Core.Now));
            }
        }
    }

    /// <summary>
    /// The party's fight, the tank's foe before the leader's (<see cref="CombatBrain.TankFoe"/>):
    /// the members are sent onto the foe the strongest melee fighter pulled, so the party does
    /// not split onto many monsters. Its end, with a member far off, is a moment to regroup.
    /// </summary>
    private static void NoticeFight(SosariaCharacter leader, EngineParty party, List<Mobile> members, DateTime now)
    {
        var foe = CombatBrain.TankFoe(leader) ?? leader.Combatant;

        if (foe is { Deleted: false, Alive: true })
        {
            Fighting.Add(leader.Serial);

            if (party.Contains(foe))
            {
                return;
            }

            var given = false;

            foreach (var member in members)
            {
                if (member is SosariaCharacter bot && Current(bot)?.Kind != PartyOrderKind.Back && FollowSkill.TakesLeadersFoe(bot, foe))
                {
                    given |= Give(bot, new PartyOrder(PartyOrderKind.Attack, leader.Serial, now, Target: foe.Serial));
                }
            }

            if (given)
            {
                Say(leader, AttackTag, foe.Serial, TalkCategory.OrderAttack, new TalkSlots { Foe = FoeWord(foe) }, now);
            }

            return;
        }

        if (!Fighting.Remove(leader.Serial) || !AnyFar(leader, members))
        {
            return;
        }

        var regrouped = false;

        foreach (var member in members)
        {
            if (member is SosariaCharacter bot)
            {
                regrouped |= Give(bot, new PartyOrder(PartyOrderKind.Regroup, leader.Serial, now));
            }
        }

        if (regrouped)
        {
            Say(leader, RegroupTag, Serial.Zero, TalkCategory.OrderRegroup, default, now);
        }
    }

    private static void NoticeHurt(SosariaCharacter leader, EngineParty party, List<Mobile> members, DateTime now)
    {
        foreach (var member in members)
        {
            if (member is not { Deleted: false, Alive: true } || member.Map != leader.Map)
            {
                continue;
            }

            if (member is SosariaCharacter bot &&
                PartyOrderRules.NeedsBack(bot.Combatant is { Deleted: false, Alive: true }, Vitals.HitsFraction(bot)) &&
                Give(bot, new PartyOrder(PartyOrderKind.Back, leader.Serial, now)))
            {
                Say(leader, BackTag, bot.Serial, TalkCategory.OrderBack, new TalkSlots { Name = bot.Name }, now);
            }

            // A hurt member next to a healer is bandaged where it stands (WorldPlay); a far one is
            // walked to, by one healer at a time: two were sent after the same member in one scan.
            if (!HealRules.NeedsHeal(member.Hits, member.HitsMax, member.Poisoned) || HealerOnTheWay(party, member) ||
                BestHealer(party, member) is not { } healer || HealRules.InReach(healer.Location, member.Location))
            {
                continue;
            }

            if (StartHeal(healer, member))
            {
                Say(leader, HealTag, member.Serial, TalkCategory.OrderHeal, new TalkSlots { Name = member.Name }, now);
            }
        }
    }

    /// <summary>
    /// A leader on its way stands for a member that fell behind. One that is not walking
    /// anywhere, at a muster or a bank, has nothing to stop: the group board gathers it.
    /// </summary>
    private static void NoticeLag(SosariaCharacter leader, List<Mobile> members, DateTime now)
    {
        if (leader.Combatant != null || Waits.ContainsKey(leader.Serial) || !OnTrip(leader))
        {
            return;
        }

        Mobile behind = null;
        var farthest = 0;

        foreach (var member in members)
        {
            if (!member.Alive || member is SosariaCharacter bot && Current(bot) is { } order && !PartyOrderRules.CountsForLag(order.Kind) ||
                !WaitRested(leader.Serial, member.Serial, now))
            {
                continue;
            }

            var sameMap = member.Map == leader.Map;
            var distance = sameMap ? (int)leader.GetDistanceToSqrt(member) : 0;

            if (PartyOrderRules.LagsBehind(sameMap, distance) && distance > farthest)
            {
                behind = member;
                farthest = distance;
            }
        }

        if (behind == null || !OpenWait(leader, PartyHelpKind.WaitForMe, behind, now))
        {
            return;
        }

        // The rest hold; the one behind follows under an order, so the stuck check covers it.
        foreach (var member in members)
        {
            if (member is SosariaCharacter bot)
            {
                Give(bot, new PartyOrder(member == behind ? PartyOrderKind.Follow : PartyOrderKind.Hold, leader.Serial, now));
            }
        }

        Say(leader, HoldTag, behind.Serial, TalkCategory.OrderHold, new TalkSlots { Name = behind.Name }, now);
    }

    private static void NoticeTrip(SosariaCharacter leader, List<Mobile> members, DateTime now)
    {
        var trip = leader.Routine is { NeedsNext: false } routine ? routine.CurrentSkill : null;

        if (trip is not IHuntingSkill || LfgBoard.RunTargetOf(leader) is not { } target)
        {
            LastTrip.Remove(leader.Serial);
            return;
        }

        if (LastTrip.TryGetValue(leader.Serial, out var last) && ReferenceEquals(last, trip))
        {
            return;
        }

        LastTrip[leader.Serial] = trip;
        var given = false;

        foreach (var member in members)
        {
            if (member is SosariaCharacter bot)
            {
                given |= Give(bot, new PartyOrder(PartyOrderKind.GoTo, leader.Serial, now, Place: target.At, PlaceName: target.Place));
            }
        }

        if (given)
        {
            Say(leader, GoToTag, Serial.Zero, TalkCategory.OrderGoTo, new TalkSlots { Place = target.Place }, now);
        }
    }

    private static void TickStuck(SosariaCharacter member, DateTime now)
    {
        if (Current(member) is not { Kind: PartyOrderKind.Follow or PartyOrderKind.Regroup or PartyOrderKind.Back } order ||
            World.FindMobile(order.From) is not SosariaCharacter leader || leader.Map != member.Map || member.Combatant != null ||
            !PartyOrderRules.LagsBehind(sameMap: true, (int)member.GetDistanceToSqrt(leader)))
        {
            Still.Remove(member.Serial);
            return;
        }

        if (!Still.TryGetValue(member.Serial, out var still) || still.At != member.Location)
        {
            Still[member.Serial] = (member.Location, now);
            return;
        }

        if (!PartyOrderRules.IsStuck(still.Since, now) || !OpenWait(leader, PartyHelpKind.Stuck, member, now))
        {
            return;
        }

        Still.Remove(member.Serial);

        if (LeaderGate.Opens((member.Serial, StuckTag, leader.Serial), now))
        {
            Chat(member, TalkCategory.OrderStuck);
        }
    }

    private static bool OnTrip(SosariaCharacter leader) =>
        leader.Routine is { NeedsNext: false, CurrentSkill: IHuntingSkill or TravelSkill };

    private static bool AnyFar(SosariaCharacter leader, List<Mobile> members)
    {
        foreach (var member in members)
        {
            if (member.Alive && PartyOrderRules.LagsBehind(member.Map == leader.Map, (int)leader.GetDistanceToSqrt(member)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<Mobile> Members(EngineParty party, Mobile leader)
    {
        var members = new List<Mobile>(party.Members.Count);

        for (var i = 0; i < party.Members.Count; i++)
        {
            var mobile = party.Members[i].Mobile;

            if (mobile != null && mobile != leader && !mobile.Deleted)
            {
                members.Add(mobile);
            }
        }

        return members;
    }

    /// <summary>
    /// The nearest free bot member with bandages or Magery, or null when none can heal. Never the
    /// leader: its run is the group's, and a leader that left it for an errand would count a restart.
    /// </summary>
    private static SosariaCharacter BestHealer(EngineParty party, Mobile patient)
    {
        SosariaCharacter best = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is not SosariaCharacter bot || bot == patient || bot == party.Leader || bot.Deleted || !bot.Alive ||
                bot.Map != patient.Map || bot.Routine is { NeedsNext: false, CurrentSkill: HealMateSkill } ||
                !PartyOrderRules.FreeToHeal(HuntSkill.HasHealing(bot), bot.Combatant != null, bot.MustRunFromSight()))
            {
                continue;
            }

            var distance = (int)bot.GetDistanceToSqrt(patient);

            if (distance < bestDistance)
            {
                best = bot;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>True while a bot member already walks to heal this patient.</summary>
    private static bool HealerOnTheWay(EngineParty party, Mobile patient)
    {
        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is SosariaCharacter { Routine: { NeedsNext: false, CurrentSkill: HealMateSkill errand } } &&
                errand.Patient == patient.Serial)
            {
                return true;
            }
        }

        return false;
    }

    private static SosariaCharacter Nearest(EngineParty party, Mobile to)
    {
        SosariaCharacter best = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < party.Members.Count; i++)
        {
            if (party.Members[i].Mobile is not SosariaCharacter bot || bot == to || bot.Deleted || bot.Map != to.Map)
            {
                continue;
            }

            var distance = (int)bot.GetDistanceToSqrt(to);

            if (distance < bestDistance)
            {
                best = bot;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static bool StartHeal(SosariaCharacter healer, Mobile patient)
    {
        WorldPlay.StartWork(healer, new HealMateSkill(patient.Serial));
        Log(() => $"{healer.Name} goes to heal {patient.Name}");
        return true;
    }

    /// <summary>The leader's own foe first; else the named creature in sight; else the nearest one.</summary>
    private static Mobile ResolveFoe(SosariaCharacter member, Mobile leader, string named)
    {
        if (leader.Combatant is { Deleted: false, Alive: true } foe && FollowSkill.TakesLeadersFoe(member, foe))
        {
            return foe;
        }

        if (member.Map == null)
        {
            return null;
        }

        var wanted = PartyOrderRules.PlaceToken(named);
        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var mobile in member.Map.GetMobilesInRange(member.Location, member.RangePerception))
        {
            if (mobile is not BaseCreature { Controlled: false, Deleted: false, Alive: true } || !People.Perceives(member, mobile) ||
                !FollowSkill.TakesLeadersFoe(member, mobile) ||
                wanted != null && (mobile.Name == null || !mobile.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var distance = (int)member.GetDistanceToSqrt(mobile);

            if (distance < bestDistance)
            {
                best = mobile;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>A line about one matter, once per <see cref="PartyOrderRules.LeaderQuiet"/> from this speaker.</summary>
    private static void Say(SosariaCharacter speaker, string tag, Serial about, string category, in TalkSlots slots, DateTime now)
    {
        if (LeaderGate.Opens((speaker.Serial, tag, about), now))
        {
            Chat(speaker, category, slots);
        }
    }

    private static void Chat(SosariaCharacter speaker, string category, in TalkSlots slots = default)
    {
        var line = Talk.Line(category, slots);

        if (!string.IsNullOrEmpty(line))
        {
            GameParty.Chat(speaker, line);
        }
    }

    /// <summary>
    /// A creature as people say it in an order, with its article: "the troll"; a person, a
    /// player or a character, by name. A character reloaded from a save may carry no Player
    /// flag, so the type decides: "hit the Kelda" named a thief.
    /// </summary>
    private static string FoeWord(Mobile foe) =>
        foe is PlayerMobile ? foe.Name : TalkWords.Foe(foe) is { } creature ? "the " + creature : null;

    private static string GiverName(PartyOrder order) => World.FindMobile(order.From)?.Name ?? order.From.ToString();

    private static string Describe(PartyOrder order) =>
        order.Kind switch
        {
            PartyOrderKind.GoTo => $"go to {order.PlaceName}",
            PartyOrderKind.Attack => $"attack {World.FindMobile(order.Target)?.Name ?? order.Target.ToString()}",
            _ => order.Kind.ToString().ToLowerInvariant()
        };

    private static void Log(Func<string> line)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", line());
        }
    }
}
