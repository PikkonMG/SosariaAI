using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using Server.Logging;
using Server.Regions;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// Small Order against Chaos scuffles on town streets (<see cref="TownScuffleRules"/>). Most are
/// called: once a minute, when the shard is due, an idle blue of one side standing in a due town
/// calls two or three of each side from within the call range to a street spot of that town
/// away from the bank (<see cref="TownScuffleCall"/>, <see cref="ScuffleCallSkill"/>), and the
/// fight starts with those who came. The rest open when an Order and a Chaos member meet under a
/// town's guards (<see cref="WorldPlay.ConsiderAlignmentFight"/>). No model is asked to start
/// one. The fighters are chosen at the start and nobody else is drafted; the rest of the street
/// watches and calls out. The same Order and Chaos pair rests from each other a while, so the
/// pairs change.
/// The blows are lawful (Order and Chaos are enemies to the engine), so nobody goes gray and the
/// guards stay out, and <see cref="WorldPlay.ConsiderGuardLine"/> lets this one fight go on. A
/// fighter who turns gray or red, runs, leaves the town, or comes near a bank, a healer, a shrine
/// or a moongate is out, and nobody chases it. The town gaps, the calls and the pair rests are in
/// memory only: after a restart each town waits up to the least gap before its first scuffle.
/// Each fighter's rest is its own <see cref="RuleClock.TownScuffle"/> clock, which the save
/// keeps. World thread only.
/// </summary>
public static class TownScuffles
{
    private static readonly ILogger logger = SosariaLog.For(typeof(TownScuffles));
    private static readonly List<TownScuffle> Active = [];
    private static readonly Dictionary<Serial, TownScuffle> ByFighter = new();
    private static readonly Dictionary<ScuffleTown, DateTime> TownDue = new();
    private static readonly List<TownScuffleCall> Calls = [];
    private static readonly Dictionary<Serial, TownScuffleCall> ByCalled = new();
    private static readonly Dictionary<(Serial, Serial), DateTime> PairScuffled = new();
    private static readonly List<SosariaCharacter> OrderPool = [];
    private static readonly List<SosariaCharacter> ChaosPool = [];
    private static DateTime _shardDue;
    private static int _minutes;
    private static int _called;
    private static int _lapsed;
    private static int _begun;
    private static int _fighters;
    private static int _orderWon;
    private static int _chaosWon;
    private static int _draws;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    private static TownScuffleSettings Settings => SosariaSettings.Characters?.TownScuffles ?? TownScuffleSettings.Defaults;

    /// <summary>The scuffle this person fights in, or null.</summary>
    public static TownScuffle Find(Mobile mobile) =>
        mobile != null && ByFighter.TryGetValue(mobile.Serial, out var scuffle) ? scuffle : null;

    /// <summary>True while this person is called to a town scuffle and has not been let go.</summary>
    public static bool OnCall(Mobile mobile) => mobile != null && ByCalled.ContainsKey(mobile.Serial);

    /// <summary>Takes this person off its call: its walk to the street spot failed.</summary>
    public static void LeaveCall(Mobile mobile)
    {
        if (mobile != null && ByCalled.Remove(mobile.Serial, out var call))
        {
            call.Drop(mobile.Serial);
        }
    }

    /// <summary>True while these two fight each other in one town scuffle: the guard line lets the fight go on.</summary>
    public static bool AreFighting(Mobile first, Mobile second) =>
        second != null && Find(first) is { } scuffle && scuffle.Opposes(first.Serial, second.Serial);

    /// <summary>
    /// An Order and a Chaos member met under the guards: when the town and the shard are due, a
    /// scuffle opens with the two and up to <see cref="TownScuffleSettings.MaxSide"/> of each
    /// side near them. True when it opened; the caller then says no town words.
    /// </summary>
    public static bool TryOpen(SosariaCharacter opener, Mobile other)
    {
        var settings = Settings;
        var now = Core.Now;

        if (!TownScuffleRules.ShardOpen(settings.Enabled, Active.Count + Calls.Count, settings.MaxActive, _shardDue, now) ||
            other is not SosariaCharacter foe || !EngineGuilds.Opposed(opener, foe) || OnCall(opener) || OnCall(foe) ||
            TownOf(opener) is not { } town || TownOf(foe) != town ||
            !TownScuffleRules.TownOpen(TownBusy(town), DueOf(town, settings, now), now) ||
            !MayStand(opener, town, settings, now) || !MayStand(foe, town, settings, now) || !PairFree(opener, foe, settings, now))
        {
            return false;
        }

        var (orderLead, chaosLead) = EngineGuilds.AlignmentOf(opener) == GuildType.Order ? (opener, foe) : (foe, opener);
        var (orderSize, chaosSize) = TownScuffleRules.SideSizes(
            Utility.Random(int.MaxValue),
            Utility.Random(int.MaxValue),
            TownScuffleRules.MinSide,
            settings.MaxSide,
            settings.MaxFighters
        );
        var order = Side(orderLead, chaosLead, orderSize, town, settings, now);
        var chaos = Side(chaosLead, orderLead, chaosSize, town, settings, now);
        var (orderCount, chaosCount) = TownScuffleRules.Balance(order.Count, chaos.Count);
        order.RemoveRange(orderCount, order.Count - orderCount);
        chaos.RemoveRange(chaosCount, chaos.Count - chaosCount);
        Begin(town, order, chaos, settings, now);
        return true;
    }

    /// <summary>
    /// A fighter's look at its scuffle on its world scan: who is out, who fights whom, and whether
    /// it is over; or at its call: who came, and whether the scuffle starts.
    /// </summary>
    public static void Consider(SosariaCharacter character)
    {
        if (Find(character) is { } scuffle)
        {
            Review(scuffle, Core.Now);
        }
        else if (character != null && ByCalled.TryGetValue(character.Serial, out var call))
        {
            Gather(call, Core.Now);
        }
    }

    /// <summary>
    /// Looks for a call: an idle, fit blue of either side, taken in turn from a random start,
    /// who stands in a due town with a street spot, and at least one fit blue of the other side
    /// within the call range. Each side is called two or three strong, nearest the spot first,
    /// and nobody who scuffled one of the other side lately.
    /// </summary>
    private static void TryCall(TownScuffleSettings settings, DateTime now)
    {
        if (!TownScuffleRules.ShardOpen(settings.Enabled, Active.Count + Calls.Count, settings.MaxActive, _shardDue, now))
        {
            return;
        }

        Pool(GuildType.Order, OrderPool, settings, now);
        Pool(GuildType.Chaos, ChaosPool, settings, now);
        var total = OrderPool.Count + ChaosPool.Count;

        if (OrderPool.Count > 0 && ChaosPool.Count > 0)
        {
            var start = Utility.Random(total);

            for (var i = 0; i < total; i++)
            {
                var index = (start + i) % total;
                var lead = index < OrderPool.Count ? OrderPool[index] : ChaosPool[index - OrderPool.Count];

                if (TryCallFrom(lead, settings, now))
                {
                    break;
                }
            }
        }

        OrderPool.Clear();
        ChaosPool.Clear();
    }

    private static bool TryCallFrom(SosariaCharacter lead, TownScuffleSettings settings, DateTime now)
    {
        if (TownOf(lead) is not { } town || !TownScuffleRules.TownOpen(TownBusy(town), DueOf(town, settings, now), now) ||
            StreetSpot(lead, town, settings) is not { } spot)
        {
            return false;
        }

        var leadIsOrder = EngineGuilds.AlignmentOf(lead) == GuildType.Order;
        var (orderWanted, chaosWanted) = TownScuffleRules.SideSizes(
            Utility.Random(int.MaxValue),
            Utility.Random(int.MaxValue),
            TownScuffleRules.CalledSide,
            settings.MaxSide,
            settings.MaxFighters
        );
        var own = new List<SosariaCharacter> { lead };
        var foes = new List<SosariaCharacter>();
        Choose(foes, leadIsOrder ? ChaosPool : OrderPool, own, spot, lead.Map, leadIsOrder ? chaosWanted : orderWanted, settings, now);

        if (foes.Count == 0)
        {
            return false;
        }

        Choose(own, leadIsOrder ? OrderPool : ChaosPool, foes, spot, lead.Map, leadIsOrder ? orderWanted : chaosWanted, settings, now);
        var (order, chaos) = leadIsOrder ? (own, foes) : (foes, own);
        Call(town, spot, order, chaos, settings, now);
        return true;
    }

    // The fit blues of a side who may answer a call: idle, on Felucca's rules, and on no call yet.
    private static void Pool(GuildType side, List<SosariaCharacter> into, TownScuffleSettings settings, DateTime now)
    {
        into.Clear();
        EngineGuilds.AddSideMembers(side, into);
        into.RemoveAll(character => !MayAnswer(character, settings, now));
    }

    private static bool MayAnswer(SosariaCharacter character, TownScuffleSettings settings, DateTime now) =>
        WorldPlay.OpenPvp(character.Map) && WorldPlay.IsIdle(character) && !OnCall(character) && Fit(character, settings, now);

    // Adds to the side the nearest of the pool to the spot, inside the call range, until it has
    // its size; nobody who feuds with, fought or scuffled one of the other side lately.
    private static void Choose(
        List<SosariaCharacter> side,
        List<SosariaCharacter> pool,
        List<SosariaCharacter> against,
        Point3D spot,
        Map map,
        int size,
        TownScuffleSettings settings,
        DateTime now
    )
    {
        var nearest = new List<SosariaCharacter>(pool);
        nearest.Sort((first, second) => NavMetric.Chebyshev(spot, first.Location).CompareTo(NavMetric.Chebyshev(spot, second.Location)));

        for (var i = 0; i < nearest.Count && side.Count < size; i++)
        {
            var candidate = nearest[i];

            if (candidate.Map == map && NavMetric.Chebyshev(spot, candidate.Location) <= settings.CallTiles &&
                !side.Contains(candidate) && FreeOfAll(candidate, against, settings, now))
            {
                side.Add(candidate);
            }
        }
    }

    private static bool FreeOfAll(SosariaCharacter candidate, List<SosariaCharacter> against, TownScuffleSettings settings, DateTime now)
    {
        for (var i = 0; i < against.Count; i++)
        {
            if (FactionWar.Feuding(candidate, against[i]) || !WorldPlay.PairReady(candidate.Serial, against[i].Serial, now) ||
                !PairRested(candidate, against[i], settings, now))
            {
                return false;
            }
        }

        return true;
    }

    // The call goes out on both sides' guild chat; each fighter drops what it did and walks to the spot.
    private static void Call(ScuffleTown town, Point3D spot, List<SosariaCharacter> order, List<SosariaCharacter> chaos, TownScuffleSettings settings, DateTime now)
    {
        var call = new TownScuffleCall(
            town,
            spot,
            [.. SerialsOf(order)],
            [.. SerialsOf(chaos)],
            now + TimeSpan.FromMinutes(Math.Max(0, settings.GatherMinutes))
        );
        Calls.Add(call);
        _shardDue = now + TimeSpan.FromMinutes(Math.Max(0, settings.ShardGapMinutes));
        _called++;
        WorldPlay.Log(TownScuffleRules.CallLine(town.Name, NamesOf(order), NamesOf(chaos), spot.X, spot.Y));
        GuildChat.Say(order[0], Talk.Line(TalkCategory.ScuffleCall, new TalkSlots { Place = town.Name }));
        GuildChat.Say(chaos[0], Talk.Line(TalkCategory.ScuffleCall, new TalkSlots { Place = town.Name }));
        SendOut(order, call, spot);
        SendOut(chaos, call, spot);
    }

    private static void SendOut(List<SosariaCharacter> side, TownScuffleCall call, Point3D spot)
    {
        for (var i = 0; i < side.Count; i++)
        {
            ByCalled[side[i].Serial] = call;
            WorldPlay.StartWork(side[i], new ScuffleCallSkill(spot));
        }
    }

    /// <summary>
    /// A call's look at its fighters: one no longer fit, or no longer on its way, is dropped;
    /// the scuffle starts with those at the spot once all came or the gather time is up, and the
    /// call lapses when a side is gone. A lapsed call lets the town call again a little later.
    /// </summary>
    private static void Gather(TownScuffleCall call, DateTime now)
    {
        var settings = Settings;
        DropUnfit(call, call.Order, settings, now);
        DropUnfit(call, call.Chaos, settings, now);
        var order = CameTo(call, call.Order, settings, now);
        var chaos = CameTo(call, call.Chaos, settings, now);

        switch (TownScuffleRules.CallState(call.Order.Count, call.Chaos.Count, order.Count, chaos.Count, call.TimeUp(now)))
        {
            case ScuffleCallState.Starts:
            {
                EndCall(call);
                var (orderCount, chaosCount) = TownScuffleRules.Balance(order.Count, chaos.Count);
                order.RemoveRange(orderCount, order.Count - orderCount);
                chaos.RemoveRange(chaosCount, chaos.Count - chaosCount);
                Begin(call.Town, order, chaos, settings, now);
                break;
            }
            case ScuffleCallState.Lapsed:
            {
                EndCall(call);
                TownDue[call.Town] = now + TownScuffleRules.CallRetry;
                _lapsed++;
                WorldPlay.Log(TownScuffleRules.LapseLine(call.Town.Name, order.Count, chaos.Count));
                break;
            }
        }
    }

    // A called fighter gone, unfit, or turned to other work leaves the call.
    private static void DropUnfit(TownScuffleCall call, IReadOnlyList<Serial> side, TownScuffleSettings settings, DateTime now)
    {
        for (var i = side.Count - 1; i >= 0; i--)
        {
            var serial = side[i];

            if (World.FindMobile(serial) is not SosariaCharacter fighter || !Fit(fighter, settings, now) ||
                fighter.Routine?.CurrentSkill is not ScuffleCallSkill)
            {
                ByCalled.Remove(serial);
                call.Drop(serial);
            }
        }
    }

    // The called fighters of a side who stand at the spot, on the scuffle's ground.
    private static List<SosariaCharacter> CameTo(TownScuffleCall call, IReadOnlyList<Serial> side, TownScuffleSettings settings, DateTime now)
    {
        var came = new List<SosariaCharacter>();

        for (var i = 0; i < side.Count; i++)
        {
            if (World.FindMobile(side[i]) is SosariaCharacter fighter &&
                NavMetric.Chebyshev(fighter.Location, call.Spot) <= TownScuffleRules.MateRange &&
                MayStand(fighter, call.Town, settings, now))
            {
                came.Add(fighter);
            }
        }

        return came;
    }

    // Lets every fighter still called go; each one's walk ends on its next step.
    private static void EndCall(TownScuffleCall call)
    {
        Calls.Remove(call);
        Release(call.Order);
        Release(call.Chaos);
    }

    private static void Release(IReadOnlyList<Serial> side)
    {
        for (var i = 0; i < side.Count; i++)
        {
            ByCalled.Remove(side[i]);
        }
    }

    /// <summary>
    /// The street spot of a call: the nearest outdoor road node to the caller that lies under its
    /// town's guards and clear of every place of peace, or null when none near does.
    /// </summary>
    private static Point3D? StreetSpot(SosariaCharacter lead, ScuffleTown town, TownScuffleSettings settings)
    {
        var nodes = NavWorld.GraphFor(lead.Map.Name)?.FindNearest(lead.Location, TownScuffleRules.SpotNodeScan);

        for (var i = 0; i < (nodes?.Count ?? 0); i++)
        {
            if (!nodes[i].Indoor && OnGround(nodes[i].Location, lead.Map, town, settings))
            {
                return nodes[i].Location;
            }
        }

        return null;
    }

    private static void Begin(ScuffleTown town, List<SosariaCharacter> order, List<SosariaCharacter> chaos, TownScuffleSettings settings, DateTime now)
    {
        var scuffle = new TownScuffle(town, SerialsOf(order), SerialsOf(chaos), now, TownScuffleRules.TimeLimit(settings.TimeLimitSeconds));
        Active.Add(scuffle);
        NotePairs(order, chaos, now);
        TownDue[town] = now + TownScuffleRules.TownGap(Utility.Random(int.MaxValue), settings.TownGapMinMinutes, settings.TownGapMaxMinutes);
        _shardDue = now + TimeSpan.FromMinutes(Math.Max(0, settings.ShardGapMinutes));

        Engage(order, chaos, scuffle, now);
        Engage(chaos, order, scuffle, now);
        Talk.Say(order[0], WorldPlay.AlignmentFightCategory(GuildType.Order), new TalkSlots { Foe = chaos[0].Name });
        Talk.Say(chaos[0], WorldPlay.AlignmentFightCategory(GuildType.Chaos), new TalkSlots { Foe = order[0].Name });
        CallOut(order[0], (Utility.RandomBool() ? order[0] : chaos[0]).Name, scuffle);

        _begun++;
        _fighters += scuffle.Fighters;
        WorldPlay.Log(TownScuffleRules.StartLine(town.Name, NamesOf(order), NamesOf(chaos), order[0].X, order[0].Y));
    }

    // Each fighter opens on a foe of the other side in turn and starts its own rest.
    private static void Engage(List<SosariaCharacter> side, List<SosariaCharacter> foes, TownScuffle scuffle, DateTime now)
    {
        for (var i = 0; i < side.Count; i++)
        {
            var fighter = side[i];
            var foe = foes[TownScuffleRules.FoeIndex(i, foes.Count)];
            ByFighter[fighter.Serial] = scuffle;
            fighter.StartClock(RuleClock.TownScuffle, now);
            WorldPlay.NoteFight(fighter, foe);
            fighter.JoinAgainst(foe);
        }
    }

    // A few people on the street call out; none of them joins.
    private static void CallOut(SosariaCharacter near, string fighter, TownScuffle scuffle)
    {
        var callers = 0;

        foreach (var mobile in near.Map.GetMobilesInRange(near.Location, TownScuffleRules.WatchRange))
        {
            if (callers >= TownScuffleRules.MaxWatchers)
            {
                break;
            }

            if (mobile is SosariaCharacter { Alive: true, IsGhost: false, Combatant: null } watcher && !scuffle.Contains(watcher.Serial) &&
                Talk.Maybe(watcher, TalkCategory.ScuffleWatch, TownScuffleRules.WatchPercent, new TalkSlots { Name = fighter }))
            {
                callers++;
            }
        }
    }

    /// <summary>
    /// Marks out every fighter who can no longer stand in it, points each one left without a foe
    /// at the nearest foe still in, and ends the scuffle once a side is gone or the time is up.
    /// </summary>
    private static void Review(TownScuffle scuffle, DateTime now)
    {
        var settings = Settings;

        MarkFallen(scuffle, scuffle.Order, settings);
        MarkFallen(scuffle, scuffle.Chaos, settings);
        Retarget(scuffle, scuffle.Order);
        Retarget(scuffle, scuffle.Chaos);

        if (TownScuffleRules.Result(
                scuffle.Standing(scuffle.Order),
                scuffle.Standing(scuffle.Chaos),
                scuffle.TimeUp(now),
                SideHits(scuffle, scuffle.Order),
                SideHits(scuffle, scuffle.Chaos)
            ) is { } result)
        {
            End(scuffle, result, now);
        }
    }

    private static void MarkFallen(TownScuffle scuffle, IReadOnlyList<Serial> side, TownScuffleSettings settings)
    {
        for (var i = 0; i < side.Count; i++)
        {
            var serial = side[i];
            var fighter = World.FindMobile(serial) as SosariaCharacter;

            if (!scuffle.IsOut(serial) && !Stands(fighter, scuffle, settings))
            {
                LeaveScuffle(scuffle, serial, fighter);
            }
        }
    }

    // Still in: alive, blue, on the scuffle's ground, not running, and fighting nobody outside it.
    private static bool Stands(SosariaCharacter fighter, TownScuffle scuffle, TownScuffleSettings settings) =>
        fighter is { Deleted: false, Alive: true, IsGhost: false } && fighter.Map == scuffle.Town.Map &&
        TownScuffleRules.IsBlue(fighter.IsPk, PkRules.IsRed(fighter.Kills), fighter.Criminal) &&
        !Running(fighter) && OnGround(fighter, scuffle.Town, settings) &&
        (fighter.Combatant == null || scuffle.Contains(fighter.Combatant.Serial));

    // Out for good. One who ran gives way in words; one still aimed at a scuffle foe drops it.
    private static void LeaveScuffle(TownScuffle scuffle, Serial serial, SosariaCharacter fighter)
    {
        scuffle.MarkOut(serial);

        if (fighter is not { Deleted: false })
        {
            return;
        }

        if (fighter.Alive && Running(fighter))
        {
            Talk.Say(fighter, TalkCategory.ScuffleYield);
        }

        if (fighter.Combatant is { } foe && scuffle.Contains(foe.Serial))
        {
            fighter.StandDown();
        }
    }

    // A fighter whose foe is out takes the nearest foe still in. One refused by its own grace is out.
    private static void Retarget(TownScuffle scuffle, IReadOnlyList<Serial> side)
    {
        for (var i = 0; i < side.Count; i++)
        {
            var serial = side[i];

            if (scuffle.IsOut(serial) || World.FindMobile(serial) is not SosariaCharacter fighter ||
                fighter.Combatant is { } current && !scuffle.IsOut(current.Serial) ||
                NearestStanding(scuffle, scuffle.FoesOf(serial), fighter) is not { } foe)
            {
                continue;
            }

            if (fighter.Combatant != null)
            {
                fighter.StandDown();
            }

            fighter.JoinAgainst(foe);

            if (fighter.Combatant != foe)
            {
                LeaveScuffle(scuffle, serial, fighter);
            }
        }
    }

    private static SosariaCharacter NearestStanding(TownScuffle scuffle, IReadOnlyList<Serial> side, Mobile from)
    {
        SosariaCharacter best = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < side.Count; i++)
        {
            if (scuffle.IsOut(side[i]) || World.FindMobile(side[i]) is not SosariaCharacter foe)
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(from.Location, foe.Location);

            if (distance < bestDistance)
            {
                best = foe;
                bestDistance = distance;
            }
        }

        return best;
    }

    // The mean share of hits the side's fighters still in hold.
    private static double SideHits(TownScuffle scuffle, IReadOnlyList<Serial> side)
    {
        var total = 0.0;
        var count = 0;

        for (var i = 0; i < side.Count; i++)
        {
            if (!scuffle.IsOut(side[i]) && World.FindMobile(side[i]) is { } fighter)
            {
                total += HitsFraction(fighter);
                count++;
            }
        }

        return count == 0 ? 0 : total / count;
    }

    /// <summary>
    /// Ends the scuffle: a losing fighter still standing yields in words, and everyone still
    /// aimed at a scuffle foe drops it, so nobody chases anybody. The town's gap already runs.
    /// </summary>
    private static void End(TownScuffle scuffle, ScuffleResult result, DateTime now)
    {
        Active.Remove(scuffle);
        Settle(scuffle, scuffle.Order, result == ScuffleResult.ChaosWon);
        Settle(scuffle, scuffle.Chaos, result == ScuffleResult.OrderWon);

        switch (result)
        {
            case ScuffleResult.OrderWon:
                _orderWon++;
                break;
            case ScuffleResult.ChaosWon:
                _chaosWon++;
                break;
            default:
                _draws++;
                break;
        }

        WorldPlay.Log(TownScuffleRules.EndLine(scuffle.Town.Name, result, now - scuffle.Started, scuffle.Fighters));
    }

    private static void Settle(TownScuffle scuffle, IReadOnlyList<Serial> side, bool lost)
    {
        for (var i = 0; i < side.Count; i++)
        {
            ByFighter.Remove(side[i]);

            if (World.FindMobile(side[i]) is not SosariaCharacter { Deleted: false } fighter)
            {
                continue;
            }

            if (lost && !scuffle.IsOut(side[i]) && fighter.Alive)
            {
                Talk.Say(fighter, TalkCategory.ScuffleYield);
            }

            if (fighter.Combatant is { } foe && scuffle.Contains(foe.Serial))
            {
                fighter.StandDown();
            }
        }
    }

    // A fighter fit to be chosen, standing on the town's ground.
    private static bool MayStand(SosariaCharacter character, ScuffleTown town, TownScuffleSettings settings, DateTime now) =>
        character.Map == town.Map && Fit(character, settings, now) && OnGround(character, town, settings);

    // A blue fighter by trade, armed, in no fight, run, duel, party or other scuffle, with no pet
    // to pile in, out of the death grace, healthy and rested, wherever it stands.
    private static bool Fit(SosariaCharacter character, TownScuffleSettings settings, DateTime now) =>
        character.Alive && !character.IsGhost &&
        TownScuffleRules.IsBlue(character.IsPk, PkRules.IsRed(character.Kills), character.Criminal) &&
        character.Build?.IsFighter == true && FactionWar.Armed(character) && character.Combatant == null &&
        !character.CheckFlee() && Duels.Find(character) == null && Find(character) == null && !GameParty.InParty(character) &&
        Party.FindByMember(character.CharacterId) == null && character.Followers == 0 &&
        WorldPlay.OutOfDeathGrace(character.LastDeathAt, now) &&
        TownScuffleRules.Ready(
            HitsFraction(character),
            character.ClockAt(RuleClock.TownScuffle),
            now,
            TimeSpan.FromMinutes(Math.Max(0, settings.FighterRestMinutes))
        );

    // Two who may face each other: no feud, no grace from a stand-down or a run, rested as a
    // pair from fights and from scuffles, in sight.
    private static bool PairFree(SosariaCharacter self, SosariaCharacter foe, TownScuffleSettings settings, DateTime now) =>
        !FactionWar.Feuding(self, foe) && !WorldPlay.LeavesBeSide(self, foe) && !WorldPlay.LeavesBeSide(foe, self) &&
        WorldPlay.PairReady(self.Serial, foe.Serial, now) && PairRested(self, foe, settings, now) && self.CanSee(foe);

    // The two have not scuffled each other inside the pair rest.
    private static bool PairRested(Mobile first, Mobile second, TownScuffleSettings settings, DateTime now) =>
        TimeRules.Rested(
            PairScuffled.GetValueOrDefault(WorldPlay.PairKey(first.Serial, second.Serial)),
            now,
            TimeSpan.FromMinutes(Math.Max(0, settings.PairRestMinutes))
        );

    // Every Order and Chaos pair of a scuffle starts its rest; the rests run out are swept first.
    private static void NotePairs(List<SosariaCharacter> order, List<SosariaCharacter> chaos, DateTime now)
    {
        if (PairScuffled.Count >= FactionWar.SweepAt)
        {
            FactionWar.Sweep(PairScuffled, now, TimeSpan.FromMinutes(Math.Max(0, Settings.PairRestMinutes)));
        }

        for (var i = 0; i < order.Count; i++)
        {
            for (var j = 0; j < chaos.Count; j++)
            {
                PairScuffled[WorldPlay.PairKey(order[i].Serial, chaos[j].Serial)] = now;
            }
        }
    }

    // The lead and the fit faction-mates near it, nearest first, up to the side's size.
    private static List<SosariaCharacter> Side(
        SosariaCharacter lead,
        SosariaCharacter foeLead,
        int size,
        ScuffleTown town,
        TownScuffleSettings settings,
        DateTime now
    )
    {
        var side = new List<SosariaCharacter> { lead };

        if (size <= side.Count)
        {
            return side;
        }

        var alignment = EngineGuilds.AlignmentOf(lead);
        var mates = new List<SosariaCharacter>();

        foreach (var mobile in lead.Map.GetMobilesInRange(lead.Location, TownScuffleRules.MateRange))
        {
            if (mobile is SosariaCharacter mate && mate != lead && EngineGuilds.AlignmentOf(mate) == alignment &&
                !OnCall(mate) && mate.CanSee(lead) && MayStand(mate, town, settings, now) && PairFree(mate, foeLead, settings, now))
            {
                mates.Add(mate);
            }
        }

        mates.Sort((first, second) =>
            NavMetric.Chebyshev(lead.Location, first.Location).CompareTo(NavMetric.Chebyshev(lead.Location, second.Location))
        );

        for (var i = 0; i < mates.Count && side.Count < size; i++)
        {
            side.Add(mates[i]);
        }

        return side;
    }

    // Under this town's guards and clear of its banks, healers, shrines and moongates.
    private static bool OnGround(Mobile mobile, ScuffleTown town, TownScuffleSettings settings) =>
        OnGround(mobile.Location, mobile.Map, town, settings);

    private static bool OnGround(Point3D at, Map map, ScuffleTown town, TownScuffleSettings settings) =>
        TownScuffleRules.OnGround(
            GuardCall.IsGuardedPlace(at, map),
            TownAt(at, map) == town,
            FactionWar.NearPeacePlace(at, map, settings.BankClearTiles, settings.PeaceClearTiles)
        );

    /// <summary>The guarded town this person stands in, or null outside the guards.</summary>
    private static ScuffleTown? TownOf(Mobile mobile) => mobile == null ? null : TownAt(mobile.Location, mobile.Map);

    private static ScuffleTown? TownAt(Point3D at, Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        var region = Region.Find(at, map)?.GetRegion<GuardedRegion>();
        return region == null || region.IsDisabled() || string.IsNullOrWhiteSpace(region.Name) ? null : new ScuffleTown(map, region.Name);
    }

    // A scuffle runs in the town, or a call gathers there.
    private static bool TownBusy(ScuffleTown town)
    {
        for (var i = 0; i < Active.Count; i++)
        {
            if (Active[i].Town == town)
            {
                return true;
            }
        }

        for (var i = 0; i < Calls.Count; i++)
        {
            if (Calls[i].Town == town)
            {
                return true;
            }
        }

        return false;
    }

    // The town's next scuffle time; a town seen for the first time waits up to the least gap.
    private static DateTime DueOf(ScuffleTown town, TownScuffleSettings settings, DateTime now)
    {
        if (!TownDue.TryGetValue(town, out var due))
        {
            due = now + TownScuffleRules.FirstGap(Utility.Random(int.MaxValue), settings.TownGapMinMinutes);
            TownDue[town] = due;
        }

        return due;
    }

    private static bool Running(SosariaCharacter fighter) =>
        fighter.CheckFlee() || fighter.Motor.Action == CharacterAction.Flee;

    private static double HitsFraction(Mobile mobile) =>
        mobile.HitsMax > 0 ? (double)mobile.Hits / mobile.HitsMax : 0;

    private static Serial[] SerialsOf(List<SosariaCharacter> side) => side.ConvertAll(fighter => fighter.Serial).ToArray();

    private static List<string> NamesOf(List<SosariaCharacter> side) => side.ConvertAll(fighter => fighter.Name);

    // A scuffle or a call whose fighters no longer think (off the world, gone) still ends; a new call is
    // looked for; the count goes out every ten minutes.
    private static void OnMinute()
    {
        var now = Core.Now;
        var settings = Settings;

        for (var i = Active.Count - 1; i >= 0; i--)
        {
            Review(Active[i], now);
        }

        for (var i = Calls.Count - 1; i >= 0; i--)
        {
            Gather(Calls[i], now);
        }

        TryCall(settings, now);

        if (!CensusText.Due(++_minutes))
        {
            return;
        }

        if (SosariaSettings.LogActivity &&
            TownScuffleRules.SummaryLine(
                _called,
                _lapsed,
                _begun,
                _fighters,
                _orderWon,
                _chaosWon,
                _draws,
                CensusText.CensusMinutes
            ) is { } line)
        {
            logger.Information("{Line}", line);
        }

        _called = 0;
        _lapsed = 0;
        _begun = 0;
        _fighters = 0;
        _orderWon = 0;
        _chaosWon = 0;
        _draws = 0;
    }
}
