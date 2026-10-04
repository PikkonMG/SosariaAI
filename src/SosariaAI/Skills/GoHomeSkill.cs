using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using System;
using System.Collections.Generic;

namespace SosariaAI.Skills;

/// <summary>
/// Walk to the home spot with TravelSkill and gates. Never "the nearest bank": from
/// beyond the leash that is another town's bank, and the character never gets home. A long
/// way home starts with a recall inside <see cref="TravelSkill"/>, which waits for the words
/// to end before it walks. Once a walk home from inside a dungeon, or a place this era's
/// world does not have (<see cref="DungeonGround.PlacesToLeave(Server.Map)"/>), has failed, the way out
/// comes first (<see cref="DungeonEscapeRules"/>): Recall, a pad that leads out, and after
/// five minutes stuck, the dungeon's door.
/// </summary>
public sealed class GoHomeSkill : Skill
{
    private const string NoWayOutWhy = "no way out of the dungeon yet";
    private const string NoWalkFromLandingWhy = "no walk home from the landing";
    private const string NoWalkFromDoorWhy = "no walk home from the dungeon door";

    /// <summary>The walk home and the moongate walk both failed; the end line adds the last walk's reason.</summary>
    public const string EveryWayFailedWhy = "every way home failed";

    /// <summary>A pad fires when the walker stands on it.</summary>
    private const int OnThePad = 0;

    private TravelSkill _walk;
    private RecallSkill _recall;
    private SosariaCharacter _character;
    private bool _usedBypass;
    private bool _triedDirect;
    private bool _triedGate;
    private bool _waiting;
    private bool _leavingDungeon;
    private bool _triedRecall;
    private bool _triedExitPad;
    private string _wayOutWhy;
    private DateTime _waitUntil;

    /// <summary>
    /// Every way home failed, the gate walk too. A character that asked again three
    /// seconds later, for an hour, wrote twenty lines a minute and went nowhere. It
    /// waits here a while, then tries the direct way again.
    /// </summary>
    public static readonly TimeSpan RetryWait = TimeSpan.FromMinutes(3);

    public static bool ShouldWait(int failedHomeWalks) => failedHomeWalks > HomeLeash.FailedWalksBeforeMoongate;

    public override string Name => SkillKinds.GoHome;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _usedBypass = false;
        _triedDirect = false;
        _triedGate = false;
        _waiting = false;
        _leavingDungeon = false;
        _triedRecall = false;
        _triedExitPad = false;
        _recall = null;
        _walk = null;

        var dungeon = DungeonAt(character);
        var inDungeon = dungeon != null;

        // Inside, once a walk home from this dungeon failed, the way out comes first; with
        // none left yet the walk home is tried again. The wait below never runs in a dungeon:
        // it made the dungeon tile the character's home, where it loitered, tried the same
        // dead walk, and waited again.
        if (DungeonEscapeRules.NeedsWayOut(character.DungeonTrouble, dungeon) && BeginWayOut())
        {
            return true;
        }

        if (!inDungeon && ShouldWait(character.FailedHomeWalks))
        {
            // Every way home failed. When the tile itself is sealed the wait only
            // repeats the same no-route, so the character stands back on the road
            // first and the direct walk is tried again.
            if (character.TryMaroonedRescue())
            {
                character.FailedHomeWalks = 0;
            }
            else
            {
                _waiting = true;
                _waitUntil = Core.Now + RetryWait;
                character.Home = character.Location;
                character.RangeHome = CharactersFile.DefaultIdleRadius;
                return true;
            }
        }

        var range = CharactersFile.DefaultGoToRange;
        var home = character.HomeCorner;
        var recentRuns = character.Memory.Danger.RecentRuns(Core.Now);

        if (recentRuns > 0 && home != Point3D.Zero && BypassSpot(character, home) is { } bypass)
        {
            // Round the danger first; a bypass that cannot be walked falls back to the direct way.
            _walk = new TravelSkill(bypass, range);

            if (_walk.Begin(character))
            {
                _usedBypass = true;
                return true;
            }
        }

        if (character.FailedHomeWalks >= HomeLeash.FailedWalksBeforeMoongate &&
                 !AtHomeGate(character))
        {
            _walk = new TravelSkill(HomeGate(character), range);
            _triedGate = true;
        }
        else
        {
            _walk = new TravelSkill(home, range);
            _triedDirect = true;
        }

        return _walk.Begin(character) || CannotStart(_triedGate ? "no walk to the moongate" : "no walk home");
    }

    public override SkillStatus Tick()
    {
        if (_waiting)
        {
            if (Core.Now < _waitUntil)
            {
                _character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
                return SkillStatus.Running;
            }

            _character.FailedHomeWalks = 0;
            return SkillStatus.Done;
        }

        if (_recall != null)
        {
            return TickRecall();
        }

        // The pad carried the character out: home is walked from the far side.
        if (_leavingDungeon && DungeonAt(_character) == null)
        {
            _leavingDungeon = false;
            return StartWalkHome() ? SkillStatus.Running : Fail(NoWalkFromDoorWhy);
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return walk;
        }

        // The walk to the pad ended inside: the next way out, else the walk home.
        if (_leavingDungeon)
        {
            _leavingDungeon = false;
            return BeginWayOut() || StartWalkHome() ? SkillStatus.Running : Fail(_wayOutWhy);
        }

        // The bypass leg ends short of home, reached or not: home is walked from here.
        if (_usedBypass && !_triedDirect && StartWalk(_character.HomeCorner))
        {
            _triedDirect = true;
            return SkillStatus.Running;
        }

        if (walk == SkillStatus.Done)
        {
            _character.DungeonTrouble = default;
            return walk;
        }

        // A road home past the place the walker ran from is no reason to walk to a moongate:
        // the walk found no road round it, could not recall, and waited for the place to go
        // quiet while a threat still held it (WalkRecoveryRules.DangerBarsRoad). The trip ends.
        if (_walk?.FailReason == TravelSkill.DangerousRoadWhy)
        {
            return Fail(TravelSkill.DangerousRoadWhy);
        }

        if (!_triedGate && StartGateWalk())
        {
            _triedGate = true;
            return SkillStatus.Running;
        }

        return Fail(TravelSkill.WithWalkWhy(EveryWayFailedWhy, _walk));
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _recall?.Abort();
        _recall = null;
        _character = null;
    }

    public override void Resume(TimeSpan held)
    {
        _waitUntil = SkillClock.Shift(_waitUntil, held);
        _walk?.Resume(held);
    }

    /// <summary>The dungeon or dropped place the character stands in, as this world names it, or null.</summary>
    private static string DungeonAt(SosariaCharacter character) =>
        DungeonGround.PlacesToLeave(character.Map)(character.Location);

    /// <summary>
    /// Begins the next way out of the dungeon that is left, or false, with the reason in
    /// <see cref="_wayOutWhy"/>, when none is: Recall home, the walk onto a pad that leads
    /// out, the door once stuck.
    /// </summary>
    private bool BeginWayOut()
    {
        _wayOutWhy = NoWayOutWhy;
        var pad = _triedExitPad ? Point3D.Zero : ExitPad();
        var way = DungeonEscapeRules.Next(
            RecallRules.CanRecallToward(_character, _character.HomeSpot),
            _triedRecall,
            pad != Point3D.Zero,
            _triedExitPad,
            DungeonEscapeRules.IsStuck(_character.DungeonTrouble, Core.Now)
        );

        switch (way)
        {
            case DungeonWayOut.Recall:
                _triedRecall = true;
                _recall = new RecallSkill();

                if (_recall.Begin(_character))
                {
                    return true;
                }

                _recall = null;
                return BeginWayOut();
            case DungeonWayOut.ExitPad:
                _triedExitPad = true;
                _walk?.Abort();
                _walk = new TravelSkill(pad, OnThePad, arrivalFloor: true);

                if (_walk.Begin(_character))
                {
                    _leavingDungeon = true;
                    return true;
                }

                return BeginWayOut();
            case DungeonWayOut.Door:
                if (!_character.TryDungeonDoorRescue())
                {
                    return false;
                }

                _wayOutWhy = NoWalkFromDoorWhy;
                return StartWalkHome();
            default:
                return false;
        }
    }

    private SkillStatus TickRecall()
    {
        var cast = _recall.Tick();

        if (cast == SkillStatus.Running)
        {
            return cast;
        }

        _recall = null;

        if (cast == SkillStatus.Done)
        {
            return StartWalkHome() ? SkillStatus.Running : Fail(NoWalkFromLandingWhy);
        }

        return BeginWayOut() || StartWalkHome() ? SkillStatus.Running : Fail(_wayOutWhy);
    }

    /// <summary>
    /// The nearest real teleporter pad in reach that lands outside every dungeon and dropped
    /// place, or zero: a pad inside Blighted Grove lands in the Grove again.
    /// The pads come from the world, not the graph: a graph pad in the Britain Sewer had no
    /// teleporter under it, and a walker stood on it for forty minutes.
    /// </summary>
    private Point3D ExitPad()
    {
        var map = _character.Map;
        var dungeonAt = DungeonGround.PlacesToLeave(map);
        var pads = new List<(Point3D Pad, bool LandsWanted)>();

        foreach (var item in map.GetItemsInRange<Teleporter>(_character.Location, DungeonEscapeRules.ExitPadSearchTiles))
        {
            if (!GateTravel.IsUsablePad(item, map, _character))
            {
                continue;
            }

            pads.Add((item.Location, dungeonAt(item.PointDest) == null));
        }

        var pick = DungeonEntryRules.PickPad(_character.Location, pads, DungeonEscapeRules.ExitPadSearchTiles);
        return pick == DungeonEntryRules.NoPad ? Point3D.Zero : pads[pick].Pad;
    }

    private bool StartWalkHome()
    {
        _triedDirect = true;
        return StartWalk(_character.HomeCorner);
    }

    /// <summary>
    /// The side step round the danger, moved onto the road node nearest it. The raw step
    /// is a point in the wild: west of Britain it fell on a hillside with no standable tile
    /// in eight, and every walker who fled there spent six minutes failing to reach it.
    /// Null when no outdoor node lies close enough; the walk then goes straight home.
    /// </summary>
    private static Point3D? BypassSpot(SosariaCharacter character, Point3D home)
    {
        var bypass = FleeRules.BypassTowardHome(character.Location, home);
        var node = NavWorld.GraphFor(character.HomeFacet)?.FindNearest(bypass);

        return node is { Indoor: false } && HomeLeash.SnapsBypass(bypass, node.Location)
            ? node.Location
            : null;
    }

    /// <summary>
    /// The gate walk is the last way home. On the pad itself it is no way at all: the
    /// walk ends where it starts, reads as done, and the failure count that would
    /// block the character resets to zero.
    /// </summary>
    private bool StartGateWalk() =>
        _character != null &&
        !AtHomeGate(_character) &&
        StartWalk(HomeGate(_character));

    /// <summary>
    /// The nearest public moongate the character may walk onto: a red's never a guarded pad.
    /// From the Britain graveyard a red's last way home was the guarded Britain gate, and the
    /// walk refused itself.
    /// </summary>
    private static Point3D HomeGate(SosariaCharacter character) =>
        WorkSites.NearestGate(character.Location, gate => TravelSkill.MayWalkInto(character, gate));

    private static bool AtHomeGate(SosariaCharacter character) =>
        WorkSites.AtNearestGate(character.Location, gate => TravelSkill.MayWalkInto(character, gate));

    private bool StartWalk(Point3D dest)
    {
        if (_character == null || dest == Point3D.Zero)
        {
            return false;
        }

        _walk?.Abort();
        _walk = new TravelSkill(dest, CharactersFile.DefaultGoToRange);
        return _walk.Begin(_character);
    }
}
