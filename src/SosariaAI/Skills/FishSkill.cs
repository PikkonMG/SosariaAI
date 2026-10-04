using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// Fishing through the engine's harvest. A shore line now and then brings up a corked bottle
/// or a sodden treasure map (<see cref="FishCatchRules"/>); the fisherman opens a bottle at
/// once and holds up what he has no use for, a map or the SOS note, while he keeps fishing, so
/// a person or a treasure hunter nearby can buy it.
/// </summary>
public sealed class FishSkill : HarvestSkill
{
    private const string FishNoun = "fish";

    public const int ShoreWalkRange = 1;

    private const int NoCount = -1;

    private static readonly sbyte[] NeighborX = [-1, 0, 1, -1, 1, -1, 0, 1];
    private static readonly sbyte[] NeighborY = [-1, -1, -1, 0, 0, 1, 1, 1];

    private SosariaCharacter _fisher;
    private int _lastCount = NoCount;
    private Item _held;
    private int _asking;
    private DateTime _nextShout;

    public FishSkill(Rectangle2D area, double fillFraction) : base(area, fillFraction)
    {
    }

    public override string Name => SkillKinds.Fish;

    protected override string HaulNoun => FishNoun;

    protected override HarvestSystem System => Fishing.System;

    protected override HarvestDefinition Definition => Fishing.System.Definitions[0];

    protected override int WalkRange => ShoreWalkRange;

    protected override Item FindTool(SosariaCharacter character) => FindHeldTool<FishingPole>(character);

    public override bool Begin(SosariaCharacter character)
    {
        _fisher = character;
        _lastCount = NoCount;
        _held = null;
        return base.Begin(character);
    }

    public override SkillStatus Tick()
    {
        var status = base.Tick();

        if (_fisher is { Deleted: false } && People.InWorld(_fisher))
        {
            NoteCatch();
            HoldUpFinds(status == SkillStatus.Running);
        }

        return status;
    }

    public override void Abort()
    {
        HoldUpFinds(fishing: false);
        base.Abort();
    }

    // A fish more than last look is a catch; a shore catch can bring up something odd.
    private void NoteCatch()
    {
        var count = ResourceCount;
        var caught = _lastCount != NoCount && count > _lastCount;
        _lastCount = count;

        if (!caught)
        {
            return;
        }

        var find = FishCatchRules.Roll(
            _fisher.Skills.Fishing.Value,
            SpecialFishingNet.FullValidation(_fisher.Map, _fisher.X, _fisher.Y),
            Utility.Random(FishCatchRules.PerMille)
        );

        if (find == ShoreFind.None)
        {
            return;
        }

        var item = Fishing.System.Construct(find == ShoreFind.Bottle ? typeof(MessageInABottle) : typeof(TreasureMap), _fisher);

        if (item == null)
        {
            return;
        }

        _fisher.AddToBackpack(item);
        Talk.Say(_fisher, find == ShoreFind.Bottle ? TalkCategory.SosBottleCatch : TalkCategory.SosMapCatch);
        TreasureNews.SeaFind(_fisher);

        // The engine's own open: the bottle becomes the SOS note in the pack.
        if (item is MessageInABottle bottle)
        {
            bottle.OnDoubleClick(_fisher);
        }
    }

    // While fishing, the find is held up with a shout now and then; when fishing ends it comes down.
    private void HoldUpFinds(bool fishing)
    {
        if (!fishing)
        {
            TreasureMarket.Withdraw(_fisher, _held);
            _held = null;
            return;
        }

        var now = Core.Now;

        if (_held is not { Deleted: false } || !_held.IsChildOf(_fisher.Backpack))
        {
            TreasureMarket.Withdraw(_fisher, _held);
            _held = null;

            if (BankCrowd.TryGetHawkerOffer(_fisher, out _) || TreasureMaps.FirstForSale(_fisher) is not { } goods)
            {
                return;
            }

            _held = goods;
            _asking = TreasureMarket.Offer(_fisher, goods, Utility.Random(int.MaxValue)).Asking;
            _nextShout = now;
        }

        if (now < _nextShout)
        {
            return;
        }

        var roll = Utility.Random(int.MaxValue);
        _nextShout = now + BankCrowdRules.ShoutGap(roll);
        Talk.Say(_fisher, TalkCategory.SosHawk, TreasureLines.Sale(_held, _asking));
    }

    protected override int CountResource(Container pack) => CountInPack<Fish>(pack);

    /// <summary>The fisher stands on <paramref name="tile"/> and casts at the water beside it.</summary>
    protected override bool TryResourceAt(
        Map map,
        Point2D tile,
        Point3D from,
        HashSet<Point2D> excluded,
        out HarvestSpot spot
    )
    {
        spot = default;

        return map != null && map != Map.Internal &&
               map.CanSpawnMobile(tile.X, tile.Y, map.GetAverageZ(tile.X, tile.Y)) &&
               TryFindAdjacentWater(map, Definition, tile.X, tile.Y, excluded, out spot);
    }

    private bool TryFindAdjacentWater(
        Map map,
        HarvestDefinition definition,
        int x,
        int y,
        HashSet<Point2D> excluded,
        out HarvestSpot spot
    )
    {
        spot = default;

        for (var i = 0; i < NeighborX.Length; i++)
        {
            var wx = x + NeighborX[i];
            var wy = y + NeighborY[i];
            var key = new Point2D(wx, wy);

            if (excluded.Contains(key))
            {
                continue;
            }

            if (TryWaterSpot(map, definition, x, y, wx, wy, out spot))
            {
                return SpotHasResources(map, spot);
            }
        }

        return false;
    }

    private static bool TryWaterSpot(
        Map map,
        HarvestDefinition definition,
        int standX,
        int standY,
        int waterX,
        int waterY,
        out HarvestSpot spot
    )
    {
        var land = map.Tiles.GetLandTile(waterX, waterY);
        var landId = land.ID & TileData.MaxLandValue;

        if (definition.Validate(landId, isLand: true))
        {
            var water = new Point3D(waterX, waterY, land.Z);
            var stand = new Point3D(standX, standY, map.GetAverageZ(standX, standY));
            spot = new HarvestSpot(stand, water, landId, isLand: true);
            return true;
        }

        foreach (var tile in map.Tiles.GetStaticTiles(waterX, waterY))
        {
            if (!definition.Validate(tile.ID, isLand: false))
            {
                continue;
            }

            var water = new Point3D(waterX, waterY, tile.Z);
            var stand = new Point3D(standX, standY, map.GetAverageZ(standX, standY));
            spot = new HarvestSpot(stand, water, tile.ID, isLand: false);
            return true;
        }

        spot = default;
        return false;
    }
}
