using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A red gang in the world: its pooled power, the pack of reds near and the blues around it,
/// the call that brings mates (and, as often as not, any other red near) onto one victim, the
/// ride out of the Den and back to it as one, the walk out of a guarded place, and the loot
/// run over a body the gang made.
/// </summary>
public static class RedGang
{
    /// <summary>A red loots a body it made within this many tiles.</summary>
    public const int LootRange = 8;

    /// <summary>Items a red takes from one body besides its gold and supplies (<see cref="RedGangRules.StripsAsSupply"/>): the best few, not the whole pack.</summary>
    public const int LootPicks = 3;

    /// <summary>The looted-body list drops decayed corpses once it holds this many.</summary>
    public const int LootedPruneAt = 256;

    /// <summary>A red's pack and crowd are counted at most this often; the combat scan asks per foe.</summary>
    public static readonly TimeSpan SurroundingsLife = TimeSpan.FromSeconds(1);

    /// <summary>A red alone is a pack of one.</summary>
    public const int LonePack = 1;

    private static readonly HashSet<Serial> _looted = [];
    private static readonly Dictionary<Serial, (Serial Corpse, DateTime Since)> _lootWalks = new();
    private static readonly HashSet<SosariaCharacter> _reds = [];
    private static readonly List<SosariaCharacter> _mates = [];

    /// <summary>The reds a call reaches, apart from <see cref="_mates"/>: the run test on each fills that one.</summary>
    private static readonly List<SosariaCharacter> _called = [];

    /// <summary>A mate on another map counts as nowhere near the muster or the camp.</summary>
    private const int OffMap = -1;

    /// <summary>The gang mates a muster or a camp waits for, refilled on each count.</summary>
    private static readonly List<(string Id, int Distance, bool Alive, bool IsLeader)> _rollCall = [];

    /// <summary>The way each gang rode out of the Den for a spot, and when it chose it.</summary>
    private static readonly Dictionary<(int Crew, string Spot), (GangRideWay Way, DateTime At)> _rideWays = new();

    private static readonly Dictionary<Serial, (DateTime At, int Pack, int Crowd)> _surroundings = new();

    /// <summary>This red and every gang mate close enough to join the fight.</summary>
    public static int Power(SosariaCharacter red)
    {
        var power = CharacterPower.For(red);

        foreach (var mate in MatesNear(red))
        {
            power += CharacterPower.For(mate);
        }

        return power;
    }

    /// <summary>The red's pack and the blue crowd round it, counted once a second and reused inside it.</summary>
    public static (int Pack, int Crowd) Surroundings(SosariaCharacter red)
    {
        if (red.Map == null || red.Map == Map.Internal)
        {
            return (LonePack, 0);
        }

        var now = Core.Now;

        if (_surroundings.TryGetValue(red.Serial, out var seen) && now - seen.At < SurroundingsLife)
        {
            return (seen.Pack, seen.Crowd);
        }

        var counted = (Pack: PackSize(red), Crowd: BlueCrowd(red));
        _surroundings[red.Serial] = (now, counted.Pack, counted.Crowd);
        return counted;
    }

    /// <summary>Living reds near this one, itself counted: reds hunt in packs, gang or no gang.</summary>
    public static int PackSize(SosariaCharacter red)
    {
        var pack = LonePack;

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, OutlawRules.PackRange))
        {
            if (mobile != red && IsLivingRed(mobile))
            {
                pack++;
            }
        }

        return pack;
    }

    /// <summary>Living blue people near this red: the crowd a red backs off from. A red player is not in it.</summary>
    public static int BlueCrowd(SosariaCharacter red) => PeopleNear(red, red.Location, OutlawRules.CrowdRange, null);

    /// <summary>Living blue people within <paramref name="range"/> of a spot, the red and the one excepted not counted.</summary>
    public static int PeopleNear(SosariaCharacter red, Point3D at, int range, Mobile except)
    {
        var people = 0;

        foreach (var mobile in red.Map.GetMobilesInRange(at, range))
        {
            if (mobile != red && mobile != except && People.IsLivingPlayer(mobile) &&
                red.CanSee(mobile) && !IsLivingRed(mobile))
            {
                people++;
            }
        }

        return people;
    }

    /// <summary>
    /// Living blue people near this red who fight a red: the mob a red in a fight runs from.
    /// The crowd passing by is not one: a red at a busy moongate ran from its victim within
    /// seconds, while the victim itself ran.
    /// </summary>
    public static int FightersAgainstReds(SosariaCharacter red)
    {
        if (red.Map == null || red.Map == Map.Internal)
        {
            return 0;
        }

        var fighters = 0;

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, OutlawRules.CrowdRange))
        {
            if (mobile != red && People.IsLivingPlayer(mobile) && red.CanSee(mobile) && !IsLivingRed(mobile) &&
                IsLivingRed(mobile.Combatant))
            {
                fighters++;
            }
        }

        return fighters;
    }

    /// <summary>A person and the living people fighting beside it: its party.</summary>
    public static int SidePower(Mobile person) =>
        CharacterPower.For(person) + (person is SosariaCharacter character ? Party.AlliesPower(character) : 0);

    /// <summary>
    /// Gang mates in range turn on the victim, on the draw and while the fight lasts; at the draw
    /// any other free red near piles in half the time: reds have no loyalty, only appetite. A
    /// mate that would run from the fight at once, or ran from the victim's side lately, is not
    /// called in (see <see cref="RedGangRules.JoinsCall"/>).
    /// </summary>
    public static void Call(SosariaCharacter red, Mobile victim, bool strays)
    {
        if (red.Map == null || red.Map == Map.Internal)
        {
            return;
        }

        _called.Clear();

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, PkGangRules.CallRange))
        {
            if (mobile is SosariaCharacter { IsPk: true, Alive: true, Deleted: false } other && other != red &&
                !People.IsLivingPlayer(other.Combatant) &&
                (SameGang(red, other) || strays && Utility.Random(OutlawRules.PercentScale) < OutlawRules.StrayJoinPercent))
            {
                _called.Add(other);
            }
        }

        // The run test counts each mate's own pack, so it runs after the scan, on its own list.
        for (var i = 0; i < _called.Count; i++)
        {
            var mate = _called[i];

            if (RedGangRules.JoinsCall(WorldPlay.LeavesBeSide(mate, victim), WorldPlay.WouldRunFrom(mate, victim, IsBlue)))
            {
                mate.JoinAgainst(victim);
            }
        }
    }

    /// <summary>
    /// A red walking out to its gang's hot spot brings its idle gang mates near along, and the
    /// mates hanging about the Den between runs: the gang marches together and camps the spot
    /// as one. While the red musters in the Den, the call reaches the mates anywhere in the Den
    /// (see <see cref="RedGangRules.HearsRideCall"/>).
    /// </summary>
    public static void RallyToAmbush(SosariaCharacter red)
    {
        if (red.Routine?.CurrentSkill is not ConflictSkill run || red.OutlawGang == PkGangRules.NoGang ||
            red.Map == null || red.Map == Map.Internal)
        {
            return;
        }

        _mates.Clear();
        var redInDen = PkRules.InBuccaneersDen(red.X, red.Y);

        foreach (var mate in _reds)
        {
            if (mate is { IsPk: true, Alive: true, Deleted: false } && mate != red && mate.Map == red.Map &&
                SameGang(red, mate) && mate.Combatant == null &&
                RedGangRules.HearsRideCall(
                    NavMetric.Chebyshev(red.Location, mate.Location),
                    run.Mustering,
                    redInDen && PkRules.InBuccaneersDen(mate.X, mate.Y)
                ) &&
                RedGangRules.JoinsRide(
                    mate.Routine?.CurrentSkill is ConflictSkill,
                    WorldPlay.IsIdle(mate),
                    GoalPlanRules.IsHangingOut(mate.CurrentPlan)
                ) &&
                RedGangRules.MayRide(
                    mate.RestsSkill(SkillKinds.Conflict),
                    RedGangReach.CampInReach(mate),
                    ConflictSkill.WhyNotSetOut(mate) == HuntEndReason.None
                ))
            {
                _mates.Add(mate);
            }
        }

        for (var i = 0; i < _mates.Count; i++)
        {
            WorldPlay.StartWork(_mates[i], new ConflictSkill());
            WorldPlay.Log($"{_mates[i].Name} rides out with {red.Name}'s gang");
        }
    }

    /// <summary>
    /// A patrolling red that lies in wait at its gang's camp brings its patrolling gang mates
    /// near along: the gang waits as one.
    /// </summary>
    public static void CallToAmbush(SosariaCharacter red)
    {
        if (red.OutlawGang == PkGangRules.NoGang || red.Map == null || red.Map == Map.Internal)
        {
            return;
        }

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, OutlawRules.PackRange))
        {
            if (mobile is SosariaCharacter { IsPk: true, Alive: true, Deleted: false } mate && mate != red &&
                SameGang(red, mate) && mate.Routine?.CurrentSkill is ConflictSkill conflict)
            {
                conflict.JoinAmbush();
            }
        }
    }

    /// <summary>
    /// A red whose run at a hot spot is over brings its gang mates on the same spot home with
    /// it, those still on their way to it too: the gang rides back to the Den as one.
    /// </summary>
    public static void CallHome(SosariaCharacter red, string spot)
    {
        if (red.OutlawGang == PkGangRules.NoGang)
        {
            return;
        }

        foreach (var mate in _reds)
        {
            if (mate != red && SameGang(red, mate) && mate.Routine?.CurrentSkill is ConflictSkill conflict)
            {
                conflict.CallHome(spot);
            }
        }
    }

    /// <summary>
    /// Where this red's gang mates on a run to <paramref name="spot"/> in <paramref name="phase"/>
    /// stand against <paramref name="at"/>: waiting while one alive is farther than
    /// <paramref name="meetTiles"/>, formed once all stand there, timed out after
    /// <paramref name="limit"/> (see <see cref="PartyWaitRules.WaitForMembers"/>). The muster in
    /// the Den waits for the mates still mustering; the camp waits for the mates still riding.
    /// </summary>
    public static PartyWaitResult WaitForRunMates(
        SosariaCharacter red,
        string spot,
        GangRunPhase phase,
        Point3D at,
        int meetTiles,
        DateTime since,
        TimeSpan limit,
        DateTime now
    )
    {
        _rollCall.Clear();

        foreach (var mate in _reds)
        {
            if (mate != red && !mate.Deleted && SameGang(red, mate) &&
                mate.Routine?.CurrentSkill is ConflictSkill run && run.IsOn(spot, phase))
            {
                var distance = mate.Map == red.Map ? NavMetric.Chebyshev(at, mate.Location) : OffMap;
                _rollCall.Add((mate.Serial.ToString(), distance, mate.Alive, false));
            }
        }

        return PartyWaitRules.WaitForMembers(now, since, _rollCall, meetTiles, limit);
    }

    /// <summary>
    /// The way this red's gang rides out of the Den for <paramref name="spot"/>: the one the gang
    /// chose already while it holds (<see cref="RedGangRules.WayHolds"/>), else chosen now over
    /// the red and the mates mustered with it at <paramref name="musterPoint"/>, by rune when all
    /// of them can recall by <paramref name="camp"/> (<see cref="RedGangRules.WayOut"/>).
    /// </summary>
    public static GangRideWay RideWay(SosariaCharacter red, string spot, Point3D camp, Point3D musterPoint)
    {
        var key = (HotSpots.CrewKey(red), spot);
        var now = Core.Now;

        if (RodeOut(red, spot, now))
        {
            return _rideWays[key].Way;
        }

        var riders = 1;
        var canRecall = RedGangReach.Recalls(red, camp) ? 1 : 0;

        foreach (var mate in _reds)
        {
            if (mate != red && mate is { Deleted: false, Alive: true } && mate.Map == red.Map && SameGang(red, mate) &&
                mate.Routine?.CurrentSkill is ConflictSkill run && run.IsOn(spot, GangRunPhase.Muster) &&
                NavMetric.Chebyshev(musterPoint, mate.Location) <= RedGangRules.MusterTiles)
            {
                riders++;
                canRecall += RedGangReach.Recalls(mate, camp) ? 1 : 0;
            }
        }

        var way = RedGangRules.WayOut(riders, canRecall);
        _rideWays[key] = (way, now);
        WorldPlay.Log($"{red.Name}'s gang of {riders} rides out of the Den for {spot} {RedGangRules.WayWords(way)}");
        return way;
    }

    /// <summary>True when this red's gang chose its way out for <paramref name="spot"/> lately: it rode out, and a late mate follows now.</summary>
    public static bool RodeOut(SosariaCharacter red, string spot, DateTime now) =>
        _rideWays.TryGetValue((HotSpots.CrewKey(red), spot), out var chosen) && RedGangRules.WayHolds(chosen.At, now);

    /// <summary>The nearest living red in pack range, gang or no gang: the pack a red keeps close to. Null when it is alone.</summary>
    public static Mobile NearestPackmate(SosariaCharacter red)
    {
        if (red.Map == null || red.Map == Map.Internal)
        {
            return null;
        }

        Mobile nearest = null;
        var nearestDistance = int.MaxValue;

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, OutlawRules.PackRange))
        {
            var distance = NavMetric.Chebyshev(red.Location, mobile.Location);

            if (mobile != red && IsLivingRed(mobile) && distance < nearestDistance)
            {
                nearest = mobile;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>A person of the victim's side in a red's fight: anyone not red.</summary>
    private static bool IsBlue(Mobile mobile) => !PkRules.IsRed(mobile.Kills);

    /// <summary>Living gang mates within call of this red (<see cref="PkGangRules.CallRange"/>), itself not counted.</summary>
    public static int GangMatesNear(SosariaCharacter red) => MatesNear(red).Count;

    /// <summary>Living gang mates of this red on its map standing in Buccaneer's Den now, itself not counted.</summary>
    public static int MatesInDen(SosariaCharacter red)
    {
        var mates = 0;

        foreach (var mate in _reds)
        {
            if (mate != red && SameGang(red, mate) && IsOut(mate) && mate.Map == red.Map && PkRules.InBuccaneersDen(mate.X, mate.Y))
            {
                mates++;
            }
        }

        return mates;
    }

    /// <summary>Living gang reds on <paramref name="map"/> standing in Buccaneer's Den now: what a blue band hears of the Den.</summary>
    public static int RedsInDen(Map map)
    {
        var reds = 0;

        foreach (var red in _reds)
        {
            if (IsOut(red) && red.Map == map && PkRules.InBuccaneersDen(red.X, red.Y))
            {
                reds++;
            }
        }

        return reds;
    }

    /// <summary>Keeps a bound person on the roll of reds while the plan makes it red, and off it otherwise.</summary>
    public static void Enlist(SosariaCharacter character)
    {
        if (character.IsPk && character.OutlawGang != PkGangRules.NoGang)
        {
            _reds.Add(character);
        }
        else
        {
            _reds.Remove(character);
        }
    }

    /// <summary>
    /// A red whose gang mates are all logged out or dead rides with the nearest red whose
    /// gang is out: a red alone on the roads starts nothing. True when it changed gangs.
    /// </summary>
    public static bool JoinNearestGang(SosariaCharacter red)
    {
        _reds.RemoveWhere(static member => member.Deleted);

        if (!_reds.Contains(red) || HasMateOut(red))
        {
            return false;
        }

        SosariaCharacter nearest = null;
        var nearestDistance = int.MaxValue;

        foreach (var other in _reds)
        {
            if (other == red || other.Map != red.Map || other.OutlawGang == red.OutlawGang || !IsOut(other))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(red.Location, other.Location);

            if (distance < nearestDistance)
            {
                nearest = other;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        red.OutlawGang = nearest.OutlawGang;
        WorldPlay.Log($"{red.Name} has no gang mate out and rides with {nearest.Name}'s gang");
        return true;
    }

    private static bool HasMateOut(SosariaCharacter red)
    {
        foreach (var other in _reds)
        {
            if (other != red && other.OutlawGang == red.OutlawGang && IsOut(other))
            {
                return true;
            }
        }

        return false;
    }

    // Logged in and alive: a gang mate that can ride today.
    private static bool IsOut(SosariaCharacter red) =>
        red is { Deleted: false, Alive: true, IsPk: true } && red.Map != null && red.Map != Map.Internal;

    /// <summary>
    /// A red under the guards gets out at once (<see cref="LeaveGuardsSkill"/>): a recall home
    /// when it can cast one now, else a run to the nearest open ground, and only then the walk
    /// home. True while it is under the guards, so no other red rule runs.
    /// </summary>
    public static bool LeaveGuards(SosariaCharacter red)
    {
        if (PkRules.MayVisit(PkRules.IsRed(red.Kills), SosariaCharacter.UnderGuards(red)))
        {
            return false;
        }

        LeaveGuardsSkill.StartFor(red);
        return true;
    }

    /// <summary>
    /// A red walks to a fresh body its gang made out of the guards' reach, takes the best few
    /// things, keeps the gear its spare kit lacks (<see cref="SpareKit.KeepFromBody"/>), and
    /// strips the gold and supplies (<see cref="StripSupplies"/>). A body it has not reached in <see cref="RedGangRules.LootWalkLimit"/> is
    /// left for good. True while it is busy with the body.
    /// </summary>
    public static bool LootFreshKill(SosariaCharacter red)
    {
        var corpse = FreshKill(red);

        if (corpse == null)
        {
            _lootWalks.Remove(red.Serial);
            return false;
        }

        if (!red.InRange(corpse.GetWorldLocation(), StealRules.ReachTiles))
        {
            return WalkToBody(red, corpse);
        }

        _lootWalks.Remove(red.Serial);

        for (var pick = 0; pick < LootPicks; pick++)
        {
            var item = StealRules.PickLoot(corpse, int.MaxValue);

            if (item == null || !CorpseLoot.TryLift(red, item))
            {
                break;
            }
        }

        SpareKit.KeepFromBody(red, corpse);
        StripSupplies(red, corpse);
        Remember(corpse.Serial);
        WorldPlay.Log($"{red.Name} looted the body of {corpse.Owner?.Name}");
        return true;
    }

    // Every piece of gold and supply on the body the engine lets the red lift.
    private static void StripSupplies(SosariaCharacter red, Corpse corpse)
    {
        var supplies = new List<Item>();

        foreach (var item in corpse.Items)
        {
            if (item is { Deleted: false, Movable: true } && item.LootType is not (LootType.Blessed or LootType.Newbied) &&
                RedGangRules.StripsAsSupply(StealRules.KindOf(item), CorpseLoot.IsSupply(item)))
            {
                supplies.Add(item);
            }
        }

        foreach (var item in supplies)
        {
            CorpseLoot.TryLift(red, item);
        }
    }

    // A body out of reach is walked to until the walk runs too long; then it is left.
    private static bool WalkToBody(SosariaCharacter red, Corpse corpse)
    {
        var now = Core.Now;

        if (!_lootWalks.TryGetValue(red.Serial, out var walk) || walk.Corpse != corpse.Serial)
        {
            walk = (corpse.Serial, now);
            _lootWalks[red.Serial] = walk;
        }

        if (RedGangRules.GivesUpLootWalk(walk.Since, now))
        {
            _lootWalks.Remove(red.Serial);
            Remember(corpse.Serial);
            red.Motor.ClearMoveIntent();
            return false;
        }

        red.Motor.MoveToPoint(corpse);
        return true;
    }

    private static void Remember(Serial corpse)
    {
        if (_looted.Count >= LootedPruneAt)
        {
            _looted.RemoveWhere(serial => World.FindItem(serial) is not { Deleted: false });
        }

        _looted.Add(corpse);
    }

    private static Corpse FreshKill(SosariaCharacter red)
    {
        if (red.Map == null || red.Map == Map.Internal)
        {
            return null;
        }

        foreach (var item in red.Map.GetItemsInRange(red.Location, LootRange))
        {
            if (item is Corpse { Deleted: false } corpse && corpse.Owner is PlayerMobile &&
                KilledByGang(red, corpse.Killer) &&
                RedGangRules.GoesForBody(
                    GuardCall.IsGuardedPlace(corpse.GetWorldLocation(), red.Map),
                    _looted.Contains(corpse.Serial)
                ))
            {
                return corpse;
            }
        }

        return null;
    }

    private static bool KilledByGang(SosariaCharacter red, Mobile killer) =>
        killer == red || killer is SosariaCharacter { IsPk: true } mate && SameGang(red, mate);

    private static bool SameGang(SosariaCharacter red, SosariaCharacter other) =>
        PkGangRules.SameGang(red.OutlawGang, other.OutlawGang);

    private static bool IsLivingRed(Mobile mobile) =>
        mobile is PlayerMobile { Alive: true, Deleted: false } person && PkRules.IsRed(person.Kills);

    /// <summary>
    /// The gang mates near this red. The list is shared and refilled on every call: read it
    /// before the next call, on the world thread.
    /// </summary>
    private static List<SosariaCharacter> MatesNear(SosariaCharacter red)
    {
        _mates.Clear();

        if (red.OutlawGang == PkGangRules.NoGang || red.Map == null || red.Map == Map.Internal)
        {
            return _mates;
        }

        foreach (var mobile in red.Map.GetMobilesInRange(red.Location, PkGangRules.CallRange))
        {
            if (mobile is SosariaCharacter { IsPk: true, Alive: true, Deleted: false } mate && mate != red &&
                SameGang(red, mate))
            {
                _mates.Add(mate);
            }
        }

        return _mates;
    }
}
