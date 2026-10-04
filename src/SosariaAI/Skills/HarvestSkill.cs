using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Targeting;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;
using SosariaAI.Spawning;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Shared harvest loop: walk to a resource, swing, give up a dead spot, load up to the body's
/// carry limit (<see cref="CarryRules"/>), never past it. Lumberjack, Mine and Fish only differ
/// in the harvest system, the tool, and how a spot is found.
/// </summary>
public abstract class HarvestSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(HarvestSkill));

    private const string NoPatchWhy = "no patch of the site has anything to work";
    private const string NoPatchWalkWhy = "no walk to the work patch";
    private const string EmptyPatchWhy = "nothing to work at the patch";
    private const string PatchWalkFailedWhy = "the walk to the work patch failed";
    private const string LoadedWhy = "carries a full load";
    private const string EmptyWorkWhy = "the patch gave nothing";
    private const string TripTooLongWhy = "the walk to the work patch took too long";
    private const string NoToolWhy = "no tool";
    private const string ToolBrokeWhy = "the tool broke";

    private static readonly HashSet<Point2D> NoExclusions = [];

    private readonly Rectangle2D _routineArea;
    private Rectangle2D _area;
    private readonly double _fillFraction;
    private readonly HashSet<Point2D> _excluded = [];
    private readonly TreeProgress _progress = new();
    private SosariaCharacter _character;
    private Skill _walk;
    private bool _walkingToArea;
    private bool _ownStock;
    private int _patchMoves;
    private HarvestSpot _spot;
    private DateTime _started;
    private DateTime _workStarted;
    private string _siteName;
    private int _countAtStart;
    private int _loadedOnBeast;
    private int _lastCarried;
    private int _swingStones;

    /// <summary>The work at the patch lasts this long, counted from the arrival there.</summary>
    public const int DefaultHarvestMinutes = 8;

    /// <summary>
    /// A whole trip, the walk out and the work, ends by this time. The work clock once ran from
    /// the start of the walk: miners walked a median 276 tiles, came to the patch with a few
    /// minutes left and failed "the patch gave nothing" 215 times, woodcutters 299 times.
    /// </summary>
    public const int MaxTripMinutes = 20;

    public static readonly TimeSpan WorkTime = TimeSpan.FromMinutes(DefaultHarvestMinutes);
    public static readonly TimeSpan TripTime = TimeSpan.FromMinutes(MaxTripMinutes);

    protected HarvestSkill(Rectangle2D area, double fillFraction)
    {
        _routineArea = area;
        _area = area;
        _fillFraction = fillFraction;
    }

    public int ResourceCount { get; private set; }

    protected abstract HarvestSystem System { get; }

    protected abstract HarvestDefinition Definition { get; }

    protected abstract Item FindTool(SosariaCharacter character);

    protected abstract int CountResource(Container pack);

    /// <summary>What the haul is called aloud: "ore", "logs", "fish".</summary>
    protected abstract string HaulNoun { get; }

    /// <summary>
    /// The resource worked from <paramref name="tile"/> when it has stock left in the engine's
    /// harvest bank, with the tile to stand on nearest <paramref name="from"/>, or false.
    /// Tiles in <paramref name="excluded"/> were given up.
    /// </summary>
    protected abstract bool TryResourceAt(
        Map map,
        Point2D tile,
        Point3D from,
        HashSet<Point2D> excluded,
        out HarvestSpot spot
    );

    protected virtual int WalkRange => Definition.MaxRange;

    protected virtual int AreaWidenings => HarvestArea.MaxWidenings;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _walkingToArea = false;
        _spot = default;
        _progress.Reset();
        _started = Core.Now;
        _workStarted = default;
        _siteName = null;
        ResourceCount = CountResource(character.Backpack);
        _countAtStart = ResourceCount;
        _loadedOnBeast = 0;
        _lastCarried = CarryLoad.Carried(character);
        _swingStones = 0;
        _patchMoves = 0;
        _ownStock = GathersOwnStock(character);
        _area = _routineArea;

        if (FindTool(character) == null)
        {
            LogCannotStart(character, "has no tool for");
            return CannotStart(NoToolWhy);
        }

        // A loaded gatherer sells or banks first. It does not carry a full load out to the patch.
        LoadBeastWhenLoaded();

        if (IsLoaded())
        {
            LogCannotStart(character, "carries too much to start");
            return CannotStart(LoadedWhy);
        }

        var why = StartOnPatch();

        if (why != null)
        {
            LogCannotStart(character, "found nothing to work for");
            return CannotStart(why);
        }

        return true;
    }

    /// <summary>True when the harvest yields the character's own trade's stock: a smith digging its own ore.</summary>
    private bool GathersOwnStock(SosariaCharacter character) => CraftMarket.TradeOf(character)?.GatherKind == Name;

    /// <summary>
    /// Starts on the next patch with work: walks to it, or to its first spot when the person
    /// already stands in it. A patch that turns out empty rests and the next one is tried, a
    /// few times at most. Returns why nothing started, or null.
    /// </summary>
    private string StartOnPatch()
    {
        while (true)
        {
            if (!TryPickPatch(out _area))
            {
                return NoPatchWhy;
            }

            _excluded.Clear();

            if (!_area.Contains(_character.Location))
            {
                // A crafter's own patch is already its walk target: the Britain wood would send it
                // to the wood its id picks.
                var approach = WorkSites.Approach(_area, _character.CharacterId, _character.Z);
                _walk = new TravelSkill(approach, CharactersFile.DefaultGoToRange, exactTarget: _ownStock);
                _walkingToArea = true;

                if (_walk.Begin(_character))
                {
                    return null;
                }

                _walk = null;
                _walkingToArea = false;
                return NoPatchWalkWhy;
            }

            if (TryBeginWalkToSpot())
            {
                StartWorkClock();
                return null;
            }

            if (!RestPatchAndMoveOn())
            {
                return EmptyPatchWhy;
            }
        }
    }

    /// <summary>The work clock starts where the work starts: at the patch, not at the door.</summary>
    private void StartWorkClock()
    {
        if (_workStarted == default)
        {
            _workStarted = Core.Now;
        }
    }

    /// <summary>
    /// The trip is over: its work at the patch ran <see cref="WorkTime"/>, or the whole trip,
    /// walk and all, ran <see cref="TripTime"/>. <paramref name="workStarted"/> is default
    /// while the worker has not reached its patch.
    /// </summary>
    public static bool TimeUp(DateTime started, DateTime workStarted, DateTime now) =>
        workStarted != default && now - workStarted >= WorkTime || now - started >= TripTime;

    /// <summary>
    /// The hashed work patch can sit where the graph has no nodes — copies mined the same
    /// dead mountain cell forever — or where the trees or the fish are all taken: seventeen
    /// woodcutters walked into the cut-bare Britain wood and failed there. The site's cells
    /// are tried in the id's order; a resting cell and a cell with no stock left in the
    /// engine's harvest bank are passed over, and a cell the character proved unreachable
    /// only serves when no other has work. A site with no cell to work gives way to the next
    /// site of the harvest in the leash (<see cref="WorkSites.HarvestSites"/>); with none left,
    /// the harvest is not the worker's job until the banks refill (<see cref="HarvestPatches.NoteDry"/>).
    /// A crafter's own harvest works the site its routine names, near its station town.
    /// </summary>
    private bool TryPickPatch(out Rectangle2D patch)
    {
        var map = _character.Map;
        patch = default;

        if (!People.InWorld(_character))
        {
            return false;
        }

        var id = _character.CharacterId;
        var now = Core.Now;
        var goals = _character.Memory.Unreachable.Active(now);
        var sites = WorkSites.HarvestSites(Name, id, _routineArea, _ownStock, _character.HomeSpot, HomeLeash.ConfiguredRadius());

        for (var siteIndex = 0; siteIndex < sites.Count; siteIndex++)
        {
            var site = sites[siteIndex];
            _siteName = site.Name;
            var cells = new List<Rectangle2D>(WorkSites.PatchCount(site.Harvest));

            for (var i = 0; i < WorkSites.PatchCount(site.Harvest); i++)
            {
                cells.Add(WorkSites.Patch(site.Harvest, id, i));
            }

            if (HarvestPatches.Pick(
                    cells,
                    cell => HarvestPatches.IsResting(Name, map.MapID, cell, now),
                    cell => PatchHasWork(map, cell, WorkSites.Approach(cell, id, _character.Z)),
                    cell => !NavSearch.IsNearAny(WorkSites.Approach(cell, id, _character.Z), goals),
                    RestPatch,
                    out patch
                ))
            {
                return true;
            }
        }

        HarvestPatches.NoteDry(_character.Serial.Value, Name, now + Definition.MinRespawn);
        return false;
    }

    /// <summary>True when some tile of the patch holds a resource with stock left and a place to stand.</summary>
    private bool PatchHasWork(Map map, Rectangle2D patch, Point3D from)
    {
        var tiles = HarvestScan.NearFirst(from, patch);

        for (var i = 0; i < tiles.Count; i++)
        {
            if (TryResourceAt(map, tiles[i], from, NoExclusions, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The emptied patch rests for everyone until the engine's bank can refill it (the
    /// definition's shortest respawn), and the job walks on when it has moves left.
    /// </summary>
    private bool RestPatchAndMoveOn()
    {
        RestPatch(_area);
        return HarvestPatches.MayMoveOn(_patchMoves++);
    }

    private void RestPatch(Rectangle2D patch) =>
        HarvestPatches.Rest(Name, _character.Map.MapID, patch, Core.Now + Definition.MinRespawn);

    /// <summary>The line names the site the worker tried last: the patch is unset when none had work.</summary>
    private void LogCannotStart(SosariaCharacter character, string reason)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} {Reason} {Skill} at {Site}", character.Name, reason, Name, _siteName ?? _routineArea.ToString());
        }
    }

    public override SkillStatus Tick()
    {
        if (_character.Deleted || !People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        ResourceCount = CountResource(_character.Backpack);
        NoteSwing();
        LoadBeastWhenLoaded();

        if (TimeUp(_started, _workStarted, Core.Now) || IsLoaded())
        {
            var outcome = Finish(_workStarted == default ? TripTooLongWhy : EmptyWorkWhy);

            if (outcome == SkillStatus.Done)
            {
                Talk.Maybe(
                    _character,
                    TalkCategory.GatherHaul,
                    TalkOdds.GatherHaulPercent,
                    new TalkSlots { Item = HaulNoun, Count = ResourceCount }
                );
            }

            return outcome;
        }

        if (!CanSwing())
        {
            return ContinueAfterObserve(harvesting: true);
        }

        if (_walk != null)
        {
            var walkStatus = _walk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk.Abort();
            _walk = null;

            if (_walkingToArea)
            {
                _walkingToArea = false;

                if (walkStatus != SkillStatus.Done)
                {
                    return Finish(PatchWalkFailedWhy);
                }

                StartWorkClock();
                return TryBeginWalkToSpot() ? SkillStatus.Running : MoveToNextPatch();
            }

            if (walkStatus == SkillStatus.Failed)
            {
                return ExcludeCurrentSpotAndContinue();
            }
        }

        if (!SpotHasResources(_character.Map, _spot))
        {
            return ExcludeCurrentSpotAndContinue();
        }

        var tool = FindTool(_character);

        if (tool == null)
        {
            return Fail(ToolBrokeWhy);
        }

        GearEquip.EquipTool(_character, tool);
        _character.Direction = _character.GetDirectionTo(_spot.Resource);
        System.StartHarvesting(_character, tool, CreateTarget(_character.Map, _spot));

        return ContinueAfterObserve(harvesting: !CanSwing());
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _workStarted = SkillClock.Shift(_workStarted, held);
        _walk?.Resume(held);
    }

    /// <summary>A patch that gave nothing is a failure, so the failure streak counts it.</summary>
    public static SkillStatus Outcome(int countAtStart, int countNow) =>
        countNow > countAtStart ? SkillStatus.Done : SkillStatus.Failed;

    private SkillStatus Finish(string emptyReason) =>
        Outcome(_countAtStart, ResourceCount + _loadedOnBeast) == SkillStatus.Done ? SkillStatus.Done : Fail(emptyReason);

    /// <summary>The tool of type <typeparamref name="T"/> in either hand, else the first in the pack, or null.</summary>
    protected static T FindHeldTool<T>(SosariaCharacter character) where T : Item =>
        character.FindItemOnLayer(Layer.OneHanded) as T ??
        character.FindItemOnLayer(Layer.TwoHanded) as T ??
        character.Backpack?.FindItemByType<T>();

    /// <summary>The amount of <typeparamref name="T"/> piled at the top of <paramref name="pack"/>; 0 without a pack.</summary>
    protected static int CountInPack<T>(Container pack) where T : Item
    {
        if (pack == null)
        {
            return 0;
        }

        var count = 0;

        foreach (var item in pack.Items)
        {
            if (item is T)
            {
                count += item.Amount;
            }
        }

        return count;
    }

    protected bool SpotHasResources(Map map, HarvestSpot spot)
    {
        var bank = Definition.GetBank(map, spot.Resource.X, spot.Resource.Y);
        return bank?.Current >= Definition.ConsumedPerHarvest;
    }

    /// <summary>
    /// A tile beside <paramref name="resource"/> a person fits on, nearest <paramref name="from"/>,
    /// cardinals before diagonals (<see cref="HarvestStand.TryBeside"/>); never the resource tile.
    /// </summary>
    public static bool TryStandBeside(Map map, Point3D resource, Point3D from, out Point3D standAt)
    {
        standAt = default;
        var neighbours = HarvestStand.Neighbours(resource);
        var candidates = new List<Point3D>(HarvestStand.NeighbourCount);

        for (var i = 0; i < neighbours.Count; i++)
        {
            var tile = neighbours[i];
            var z = map.GetAverageZ(tile.X, tile.Y);

            if (!map.CanFit(tile.X, tile.Y, z, PersonBody.Height, false, true))
            {
                continue;
            }

            candidates.Add(new Point3D(tile.X, tile.Y, z));
        }

        return HarvestStand.TryBeside(resource, candidates, from, out standAt);
    }

    private bool TryBeginWalkToSpot()
    {
        if (!TryFindReachableSpot())
        {
            return false;
        }

        _progress.Reset();
        _walk = new GoToSkill(_spot.StandAt, WalkRange);
        return _walk.Begin(_character);
    }

    private bool TryFindReachableSpot()
    {
        // A spot past one walk leg (NavMetric.WithinLegCap) is passed over for the next nearest.
        var areas = HarvestArea.SearchOrder(_area, AreaWidenings);

        for (var i = 0; i < areas.Count; i++)
        {
            while (TryFindSpot(areas[i], out _spot))
            {
                if (HarvestOccupancy.HasOccupant(_character, _spot.Resource))
                {
                    _excluded.Add(new Point2D(_spot.Resource.X, _spot.Resource.Y));
                    continue;
                }

                if (NavMetric.WithinLegCap(_character.Location, _spot.StandAt))
                {
                    return true;
                }

                _excluded.Add(new Point2D(_spot.Resource.X, _spot.Resource.Y));
            }
        }

        return false;
    }

    /// <summary>The nearest tile of <paramref name="area"/> with a resource to work, from where the character stands.</summary>
    private bool TryFindSpot(Rectangle2D area, out HarvestSpot spot)
    {
        spot = default;
        var map = _character.Map;
        var tiles = HarvestScan.NearFirst(_character.Location, area);

        for (var i = 0; i < tiles.Count; i++)
        {
            if (TryResourceAt(map, tiles[i], _character.Location, _excluded, out spot))
            {
                return true;
            }
        }

        return false;
    }

    private SkillStatus ExcludeCurrentSpotAndContinue()
    {
        _excluded.Add(new Point2D(_spot.Resource.X, _spot.Resource.Y));
        return TryBeginWalkToSpot() ? SkillStatus.Running : MoveToNextPatch();
    }

    /// <summary>The patch ran dry: it rests, and the job walks on to the next one with work.</summary>
    private SkillStatus MoveToNextPatch()
    {
        if (!RestPatchAndMoveOn())
        {
            return Finish(EmptyPatchWhy);
        }

        var why = StartOnPatch();
        return why == null ? SkillStatus.Running : Finish(why);
    }

    private SkillStatus ContinueAfterObserve(bool harvesting)
    {
        if (_progress.Observe(ResourceCount, harvesting))
        {
            return ExcludeCurrentSpotAndContinue();
        }

        return SkillStatus.Running;
    }

    /// <summary>The heaviest single gain in carried weight so far: what one more swing may add.</summary>
    private void NoteSwing()
    {
        var carried = CarryLoad.Carried(_character);
        _swingStones = Math.Max(_swingStones, carried - _lastCarried);
        _lastCarried = carried;
    }

    /// <summary>A full load goes onto the pack beast in reach, and the work goes on.</summary>
    private void LoadBeastWhenLoaded()
    {
        if (!IsLoaded() || PackAnimals.Load(_character) is not (var loaded and > 0))
        {
            return;
        }

        _loadedOnBeast += loaded;
        ResourceCount = CountResource(_character.Backpack);
        _lastCarried = CarryLoad.Carried(_character);
    }

    /// <summary>The load is at its fill line of the body's limit, or one more swing would pass the limit.</summary>
    private bool IsLoaded() =>
        CarryRules.StopsWork(CarryLoad.Carried(_character), CarryLoad.Limit(_character), _fillFraction, _swingStones);

    private bool CanSwing() => _character.CanBeginAction(System);

    private static object CreateTarget(Map map, HarvestSpot spot) =>
        spot.IsLand
            ? new LandTarget(spot.Resource, map)
            : new StaticTarget(spot.Resource, spot.TileId);
}
