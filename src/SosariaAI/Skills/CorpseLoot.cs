using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A hunter's walk to the monsters it killed and the clearing of their corpses. The lift is
/// the engine's own: a corpse the hunter has no right to loot is left, so a hunter never
/// turns gray for someone else's kill. World thread only.
/// </summary>
public sealed class CorpseLoot
{
    private static readonly ILogger logger = SosariaLog.For(typeof(CorpseLoot));

    /// <summary>The word a loot line uses for a body with no known owner.</summary>
    private const string OwnerlessFoeWord = "corpse";

    private readonly Queue<(Mobile Foe, Point3D At)> _pending = new();
    private readonly HashSet<Serial> _handled = new();
    private Corpse _corpse;
    private string _foeName;
    private string _foeWord;
    private DateTime _walkStarted;
    private DateTime _nextGroundScan;

    /// <summary>Gold taken off corpses since the last <see cref="Clear"/>.</summary>
    public int Gold { get; private set; }

    /// <summary>Items other than gold taken since the last <see cref="Clear"/>.</summary>
    public int Items { get; private set; }

    public void Clear()
    {
        _pending.Clear();
        _handled.Clear();
        _corpse = null;
        _foeName = null;
        _foeWord = null;
        _walkStarted = default;
        _nextGroundScan = default;
        Gold = 0;
        Items = 0;
    }

    /// <summary>A foe fell where it last stood; its corpse is worth a look.</summary>
    public void Note(Mobile foe, Point3D at)
    {
        if (foe != null)
        {
            _pending.Enqueue((foe, at));
        }
    }

    /// <summary>True while the hunter walks to a corpse or clears one.</summary>
    public bool Tick(SosariaCharacter hunter)
    {
        if (!People.InWorld(hunter))
        {
            return false;
        }

        if (_corpse == null && !NextCorpse(hunter))
        {
            return false;
        }

        if (_corpse.Deleted || LootRules.TooLong(Core.Now, _walkStarted))
        {
            _handled.Add(_corpse.Serial);
            _corpse = null;
            return false;
        }

        if (!hunter.InRange(_corpse.GetWorldLocation(), LootRules.CorpseReachTiles))
        {
            if (!hunter.Motor.MoveToPoint(_corpse))
            {
                _handled.Add(_corpse.Serial);
                _corpse = null;
                return false;
            }

            return true;
        }

        hunter.Motor.Stop();
        Clean(hunter, _corpse);
        _handled.Add(_corpse.Serial);
        _corpse = null;
        return true;
    }

    private bool NextCorpse(SosariaCharacter hunter)
    {
        while (_pending.Count > 0)
        {
            var (foe, at) = _pending.Dequeue();

            if (!LootRules.WorthTheWalk(NavMetric.Chebyshev(hunter.Location, at)))
            {
                continue;
            }

            var corpse = FindCorpse(hunter.Map, foe, at);

            if (corpse == null || corpse.IsCriminalAction(hunter))
            {
                continue;
            }

            _corpse = corpse;
            _foeName = foe.Name;
            _foeWord = TalkWords.Foe(foe);
            _walkStarted = Core.Now;
            return true;
        }

        return GroundCorpse(hunter);
    }

    /// <summary>
    /// Ground sweep: a nearby corpse that still holds something worth taking, once every few
    /// seconds while the hunter is out of a fight. A corpse already tried is never retried.
    /// </summary>
    private bool GroundCorpse(SosariaCharacter hunter)
    {
        var now = Core.Now;

        if (hunter.Combatant != null || now < _nextGroundScan)
        {
            return false;
        }

        _nextGroundScan = now + LootRules.GroundScanPause;

        Corpse best = null;
        var bestDistance = int.MaxValue;

        foreach (var item in hunter.Map.GetItemsInRange(hunter.Location, LootRules.GroundCorpseTiles))
        {
            if (item is not Corpse { Deleted: false } corpse || _handled.Contains(corpse.Serial) ||
                corpse.IsCriminalAction(hunter) || !WorthLooting(hunter, corpse))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(hunter.Location, corpse.Location);

            if (distance < bestDistance)
            {
                best = corpse;
                bestDistance = distance;
            }
        }

        if (best == null)
        {
            return false;
        }

        _corpse = best;
        _foeName = best.Owner?.Name;
        _foeWord = best.Owner != null ? TalkWords.Foe(best.Owner) : OwnerlessFoeWord;
        _walkStarted = Core.Now;
        return true;
    }

    /// <summary>
    /// A corpse counts when it holds at least one liftable item the hunter would keep, and the
    /// corpse belongs to a monster rather than a player.
    /// </summary>
    private static bool WorthLooting(SosariaCharacter hunter, Corpse corpse)
    {
        if (corpse.Owner == hunter || corpse.Owner is PlayerMobile)
        {
            return false;
        }

        foreach (var item in corpse.Items)
        {
            if (Keeps(hunter, item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for a corpse item the hunter would take: liftable, not blessed, of a kind it wants,
    /// and light enough for its pack.
    /// </summary>
    private static bool Keeps(SosariaCharacter hunter, Item item) =>
        item is { Deleted: false, Movable: true } &&
        item.LootType is not (LootType.Blessed or LootType.Newbied) &&
        LootRules.Wants(StealRules.KindOf(item), IsSupply(item)) &&
        LootRules.HasRoom(hunter.TotalWeight, hunter.MaxWeight, item.PileWeight + item.TotalWeight);

    private static Corpse FindCorpse(Map map, Mobile foe, Point3D at)
    {
        foreach (var item in map.GetItemsInRange(at, LootRules.CorpseSearchTiles))
        {
            if (item is Corpse { Deleted: false } corpse && corpse.Owner == foe)
            {
                return corpse;
            }
        }

        return null;
    }

    private void Clean(SosariaCharacter hunter, Corpse corpse)
    {
        var gold = 0;
        var items = 0;
        var taken = new List<Item>(corpse.Items);

        for (var i = 0; i < taken.Count; i++)
        {
            var item = taken[i];

            if (!Keeps(hunter, item))
            {
                continue;
            }

            var amount = item.Amount;
            var isGold = item is Gold;

            if (!TryLift(hunter, item))
            {
                continue;
            }

            if (isGold)
            {
                gold += amount;
            }
            else
            {
                items++;
            }
        }

        Gold += gold;
        Items += items;

        if (gold <= 0 && items <= 0)
        {
            return;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} looted {Gold} gold from {Foe} with {Items} other items",
                hunter.Name,
                gold,
                _foeName,
                items
            );
        }

        if (gold >= SosariaCombat.LootGoldSayThreshold)
        {
            Talk.Maybe(
                hunter,
                TalkCategory.LootHaul,
                TalkOdds.LootHaulPercent,
                new TalkSlots { Price = GoldWords.Spoken(gold), Foe = _foeWord }
            );
        }
    }

    /// <summary>
    /// Lifts <paramref name="item"/> off the corpse that holds it the engine's way: the corpse
    /// checks the lift (a body the taker has no right to loot refuses it), the item goes into
    /// the taker's pack, and the corpse notes the lift. True when the item moved.
    /// </summary>
    public static bool TryLift(Mobile taker, Item item)
    {
        if (taker == null || item?.Parent is not Corpse corpse)
        {
            return false;
        }

        var reject = LRReason.Inspecific;

        if (!corpse.CheckLift(taker, item, ref reject))
        {
            return false;
        }

        taker.AddToBackpack(item);
        corpse.OnItemLifted(taker, item);
        return true;
    }

    /// <summary>A fighting supply a looter takes off a body: bandages, arrows and bolts.</summary>
    public static bool IsSupply(Item item) => item is Bandage or Arrow or Bolt;
}
