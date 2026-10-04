using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Spells.Third;
using Server.Targeting;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// One real treasure hunt on an engine treasure map. The hunter buys a shovel (and lockpicks
/// when it opens locks by hand) at a tinker, decodes the map with the engine's Cartography
/// roll, travels to the chest spot the map itself holds (walking, or recalling where the trip
/// pays), and digs with the engine's own dig: the map's dig order, its cursor answered with the
/// ground, the dig timer and its guardians. The combat brain fights the guardians. Then the
/// trap: Telekinesis from outside the blast, the Remove Trap skill, or the blast taken; the
/// lock: the Unlock spell or a lockpick; and the loot lifted into the pack. The dig goes into
/// the shard's journal for gossip.
/// </summary>
public sealed class TreasureHuntSkill : Skill
{
    private enum Phase
    {
        Outfit,
        Decode,
        Travel,
        Approach,
        Dig,
        Guardians,
        Trap,
        Unlock,
        Loot
    }

    /// <summary>The chest spot is reached when the traveller stands this close.</summary>
    public const int TravelArrivalRange = 3;

    public const int LockpickBuyCount = 5;
    public const int MaxDecodeTries = 6;
    public const int MaxDigTries = 3;
    public const int MaxTrapTries = 4;
    public const int MaxUnlockTries = 10;

    /// <summary>A guardian further than this from the chest has wandered off and no longer guards it.</summary>
    public const int GuardianRange = 12;

    /// <summary>The engine's lockpick and container reach.</summary>
    public const int HandReach = 1;

    public const int DecodeRetrySeconds = 3;

    /// <summary>The engine's lockpick timer is three seconds; the check waits a little longer.</summary>
    public const int PickWaitSeconds = 4;

    /// <summary>The Remove Trap skill holds the skill clock for ten seconds.</summary>
    public const int DisarmWaitSeconds = 11;

    public const int GuardianWaitMinutes = 3;
    public const int HealWaitMinutes = 2;

    public static readonly TimeSpan DecodeRetry = TimeSpan.FromSeconds(DecodeRetrySeconds);
    public static readonly TimeSpan PickWait = TimeSpan.FromSeconds(PickWaitSeconds);
    public static readonly TimeSpan DisarmWait = TimeSpan.FromSeconds(DisarmWaitSeconds);
    public static readonly TimeSpan GuardianWait = TimeSpan.FromMinutes(GuardianWaitMinutes);
    public static readonly TimeSpan HealWait = TimeSpan.FromMinutes(HealWaitMinutes);

    private static readonly ILogger logger = SosariaLog.For(typeof(TreasureHuntSkill));

    private readonly TreasureMap _map;
    private SosariaCharacter _character;
    private Skill _walk;
    private Phase _phase;
    private Point3D _chestAt;
    private TreasureMapChest _chest;
    private DateTime _phaseSince;
    private DateTime _nextTry;
    private int _tries;
    private bool _acting;
    private bool _trapMethodsSpent;

    public TreasureHuntSkill(TreasureMap map) => _map = map;

    public override string Name => CartographyRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _chest = null;
        _acting = false;
        _trapMethodsSpent = false;

        if (!CraftStationSkill.MayWork(character) || _map is not { Deleted: false })
        {
            return false;
        }

        _chestAt = TreasureMaps.ChestPoint(_map);

        if (_map.Completed)
        {
            _chest = TreasureMaps.ChestOf(character, _map);
            return _chest != null && (NeedsTools() ? StartOutfit() : StartTravel());
        }

        if (!TreasureMaps.MayHunt(character, _map))
        {
            return false;
        }

        return NeedsTools() ? StartOutfit() : StartDecodeOrTravel();
    }

    public override SkillStatus Tick()
    {
        if (!CraftStationSkill.MayWork(_character) || _map.Deleted)
        {
            return SkillStatus.Failed;
        }

        return _phase switch
        {
            Phase.Outfit => AfterWalk(Outfitted),
            Phase.Decode => TickDecode(),
            Phase.Travel => AfterWalk(StartApproach),
            Phase.Approach => AfterWalk(ArrivedAtChest),
            Phase.Dig => TickDig(),
            Phase.Guardians => TickGuardians(),
            Phase.Trap => TickTrap(),
            Phase.Unlock => TickUnlock(),
            _ => TickLoot()
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held)
    {
        _phaseSince = SkillClock.Shift(_phaseSince, held);
        _walk?.Resume(held);
    }

    private void Enter(Phase phase)
    {
        _phase = phase;
        _phaseSince = Core.Now;
        _nextTry = default;
        _tries = 0;
        _acting = false;
    }

    private SkillStatus AfterWalk(Func<SkillStatus> onArrival)
    {
        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;
        return walk == SkillStatus.Done ? onArrival() : SkillStatus.Failed;
    }

    /// <summary>Walks toward a spot. Null once there; otherwise the walk's own status.</summary>
    private SkillStatus? WalkTo(Point3D goal, int range)
    {
        if (NavMetric.Chebyshev(_character.Location, goal) <= range)
        {
            _walk?.Abort();
            _walk = null;
            return null;
        }

        if (_walk == null)
        {
            _walk = new GoToSkill(goal, range);

            if (!_walk.Begin(_character))
            {
                _walk = null;
                return SkillStatus.Failed;
            }
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;
        return status == SkillStatus.Done ? null : SkillStatus.Failed;
    }

    private ChestOpening Opening() =>
        TreasureHuntRules.OpenBy(_map.Level, _character.Skills.Lockpicking.Value, _character.Skills.Magery.Value);

    private bool NeedsTools() =>
        !_map.Completed && !TreasureMap.HasDiggingTool(_character) ||
        Opening() == ChestOpening.Lockpick && _character.Backpack?.FindItemByType<Lockpick>() == null;

    private bool StartOutfit()
    {
        var shop = ShopFinder.Nearest(_character, ShopFinder.TinkerToken, TinkerRules.BritainTinker);
        Enter(Phase.Outfit);
        _walk = new TravelSkill(shop, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(_character);
    }

    // At the tinker's counter: a shovel for the dig, lockpicks for a hand-opened lock.
    private SkillStatus Outfitted()
    {
        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);

        if (!_map.Completed && !TreasureMap.HasDiggingTool(_character))
        {
            VendorDeal.Buy(_character, vendors, [typeof(Shovel)], 1, PackGold());
        }

        if (Opening() == ChestOpening.Lockpick && _character.Backpack?.FindItemByType<Lockpick>() == null)
        {
            VendorDeal.Buy(_character, vendors, [typeof(Lockpick)], LockpickBuyCount, PackGold());
        }

        if (NeedsTools())
        {
            Log("could not buy digging or lock tools at {Location}", _character.Location);
            return SkillStatus.Failed;
        }

        if (_map.Completed)
        {
            return StartTravel() ? SkillStatus.Running : SkillStatus.Failed;
        }

        return StartDecodeOrTravel() ? SkillStatus.Running : SkillStatus.Failed;
    }

    private int PackGold() => _character.Backpack?.GetAmount(typeof(Gold)) ?? 0;

    private bool StartDecodeOrTravel()
    {
        if (_map.Decoder != null)
        {
            return StartTravel();
        }

        Enter(Phase.Decode);
        return true;
    }

    /// <summary>
    /// The engine's decode roll. TreasureMap.Decode itself ends by drawing the map on the
    /// decoder's client and cannot run for a character without one, so the same roll and the
    /// same outcome are applied here.
    /// </summary>
    private SkillStatus TickDecode()
    {
        if (Core.Now < _nextTry)
        {
            return SkillStatus.Running;
        }

        var min = TreasureHuntRules.DecodeMinSkill(_map.Level);

        if (_character.CheckSkill(SkillName.Cartography, min, min + TreasureHuntRules.DecodeSkillSpan))
        {
            _map.Decoder = _character;

            if (Core.AOS)
            {
                _map.LootType = LootType.Blessed;
            }

            Log("decoded a level {Level} treasure map", _map.Level);

            // "Off to dig" only once the walk to the chest has begun.
            if (!StartTravel())
            {
                return SkillStatus.Failed;
            }

            Talk.Maybe(_character, TalkCategory.TreasureDecoded, TalkOdds.TreasureDecodedPercent);
            return SkillStatus.Running;
        }

        _nextTry = Core.Now + DecodeRetry;
        return ++_tries >= MaxDecodeTries ? SkillStatus.Failed : SkillStatus.Running;
    }

    private bool StartTravel()
    {
        Enter(Phase.Travel);
        _walk = new TravelSkill(_chestAt, TravelArrivalRange);
        return _walk.Begin(_character);
    }

    private SkillStatus StartApproach()
    {
        Enter(Phase.Approach);

        if (TreasureChestWork.Beside(_map.ChestMap, _chestAt, _character.Location) is not { } stand)
        {
            Log("found no footing beside the chest spot {Spot}", _chestAt);
            return SkillStatus.Failed;
        }

        _walk = new GoToSkill(stand, 0);
        return _walk.Begin(_character) ? SkillStatus.Running : SkillStatus.Failed;
    }

    private SkillStatus ArrivedAtChest()
    {
        if (_map.Completed)
        {
            Enter(Phase.Guardians);
            return SkillStatus.Running;
        }

        Enter(Phase.Dig);
        return SkillStatus.Running;
    }

    // The map's dig order, then its cursor answered with the chest spot. The engine's dig
    // timer raises the chest over some seconds; any step or skill use in that time stops it.
    private SkillStatus TickDig()
    {
        if (_acting)
        {
            if (!_character.CanBeginAction<TreasureMap>())
            {
                return SkillStatus.Running;
            }

            _acting = false;

            if (_map.Completed && _map.CompletedBy == _character)
            {
                return ChestIsUp();
            }

            return ++_tries >= MaxDigTries ? SkillStatus.Failed : SkillStatus.Running;
        }

        if (!TreasureMap.HasDiggingTool(_character))
        {
            return SkillStatus.Failed;
        }

        Target.Cancel(_character);
        _character.Direction = _character.GetDirectionTo(_chestAt);
        _map.OnBeginDig(_character);

        if (_character.Target is not { } cursor)
        {
            Log("was refused the dig at {Spot}", _chestAt);
            return SkillStatus.Failed;
        }

        cursor.Invoke(_character, new LandTarget(_chestAt, _map.ChestMap));
        _acting = !_character.CanBeginAction<TreasureMap>();

        return _acting || ++_tries < MaxDigTries ? SkillStatus.Running : SkillStatus.Failed;
    }

    private SkillStatus ChestIsUp()
    {
        _chest = TreasureMaps.ChestOf(_character, _map);

        if (_chest == null)
        {
            return SkillStatus.Failed;
        }

        Talk.Say(_character, TalkCategory.TreasureChestUp);
        _character.NoteMusingEvent($"dug up a level {_map.Level} treasure chest");
        TreasureNews.ChestDug(_character, _chest.Location);
        Log("dug up a level {Level} treasure chest at {Spot}", _map.Level, _chest.Location);
        Enter(Phase.Guardians);
        return SkillStatus.Running;
    }

    // The combat brain answers the guardians; this step only waits for them to fall or leave.
    private SkillStatus TickGuardians()
    {
        if (_chest.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (GuardiansStand() && Core.Now - _phaseSince < GuardianWait)
        {
            return SkillStatus.Running;
        }

        Enter(Phase.Trap);
        return SkillStatus.Running;
    }

    private bool GuardiansStand()
    {
        foreach (var guardian in _chest.Guardians ?? [])
        {
            if (guardian is { Deleted: false, Alive: true } && guardian.Map == _chest.Map &&
                guardian.InRange(_chest.Location, GuardianRange))
            {
                return true;
            }
        }

        return false;
    }

    private SkillStatus TickTrap()
    {
        if (_chest.Deleted)
        {
            return SkillStatus.Failed;
        }

        var plan = TreasureHuntRules.TrapPlan(
            _chest.TrapType != TrapType.None,
            !_trapMethodsSpent && CanTelekinesis(),
            !_trapMethodsSpent && TreasureHuntRules.CanDisarm(
                _character.Skills.Lockpicking.Value,
                _character.Skills.DetectHidden.Value
            ),
            _character.Hits,
            _chest.TrapLevel
        );

        return plan switch
        {
            ChestTrapPlan.None or ChestTrapPlan.Accept => StartUnlock(),
            ChestTrapPlan.Telekinesis => Telekinesis(),
            ChestTrapPlan.Disarm => Disarm(),
            _ => Core.Now - _phaseSince < HealWait ? SkillStatus.Running : Give("was too hurt to spring the trap")
        };
    }

    private bool CanTelekinesis()
    {
        if (_character.Skills.Magery.Value < TreasureHuntRules.TelekinesisMinMagery)
        {
            return false;
        }

        var spell = new TelekinesisSpell(_character);
        return SpellCasting.Knows(_character, spell) && SpellCasting.HasReagents(_character.Backpack, spell.Info);
    }

    // From outside the blast, so the trap goes off on nobody.
    private SkillStatus Telekinesis()
    {
        if (!_acting)
        {
            if (NavMetric.Chebyshev(_character.Location, _chest.Location) <= TreasureHuntRules.TrapBlastRange ||
                !_character.InLOS(_chest))
            {
                if (TreasureChestWork.StandOff(_chest.Map, _chest.Location, _character.Location,
                        TreasureHuntRules.TelekinesisStandOff) is not { } spot)
                {
                    return SpendTrapMethods();
                }

                var walk = WalkTo(spot, 0);

                if (walk != null)
                {
                    return walk == SkillStatus.Failed ? SpendTrapMethods() : SkillStatus.Running;
                }
            }

            _acting = SpellCasting.TryBegin(_character, new TelekinesisSpell(_character));
            return _acting || ++_tries < MaxTrapTries ? SkillStatus.Running : SpendTrapMethods();
        }

        var step = TreasureChestWork.Aim(_character, _chest);

        if (step == ChestCastStep.Casting)
        {
            return SkillStatus.Running;
        }

        _acting = false;
        return ++_tries < MaxTrapTries ? SkillStatus.Running : SpendTrapMethods();
    }

    // The Remove Trap skill at the chest: its cursor answered with the chest.
    private SkillStatus Disarm()
    {
        var walk = WalkTo(_chest.Location, HandReach);

        if (walk != null)
        {
            return walk == SkillStatus.Failed ? SpendTrapMethods() : SkillStatus.Running;
        }

        if (Core.Now < _nextTry)
        {
            return SkillStatus.Running;
        }

        _nextTry = Core.Now + DisarmWait;

        Target.Cancel(_character);

        if (_character.UseSkill(SkillName.RemoveTrap) && _character.Target is { } cursor)
        {
            cursor.Invoke(_character, _chest);
        }

        return ++_tries < MaxTrapTries ? SkillStatus.Running : SpendTrapMethods();
    }

    // Neither spell nor skill took: what is left is taking the blast.
    private SkillStatus SpendTrapMethods()
    {
        _trapMethodsSpent = true;
        _walk?.Abort();
        _walk = null;
        Enter(Phase.Trap);
        return SkillStatus.Running;
    }

    private SkillStatus StartUnlock()
    {
        Enter(Phase.Unlock);
        return SkillStatus.Running;
    }

    private SkillStatus TickUnlock()
    {
        if (_chest.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (!_chest.Locked)
        {
            Enter(Phase.Loot);
            return SkillStatus.Running;
        }

        if (_tries >= MaxUnlockTries)
        {
            return Give("could not open the treasure chest");
        }

        return Opening() switch
        {
            ChestOpening.MagicUnlock => MagicUnlock(),
            ChestOpening.Lockpick => Pick(),
            _ => Give("has no way to open the treasure chest")
        };
    }

    private SkillStatus MagicUnlock()
    {
        if (!_acting)
        {
            _acting = SpellCasting.TryBegin(_character, new UnlockSpell(_character));
            _tries++;
            return SkillStatus.Running;
        }

        if (TreasureChestWork.Aim(_character, _chest) != ChestCastStep.Casting)
        {
            _acting = false;
        }

        return SkillStatus.Running;
    }

    // A lockpick from the pack, its cursor answered with the chest; the engine's pick timer decides.
    private SkillStatus Pick()
    {
        var walk = WalkTo(_chest.Location, HandReach);

        if (walk != null)
        {
            return walk == SkillStatus.Failed ? SkillStatus.Failed : SkillStatus.Running;
        }

        if (Core.Now < _nextTry)
        {
            return SkillStatus.Running;
        }

        if (_character.Backpack?.FindItemByType<Lockpick>() is not { } pick)
        {
            return Give("ran out of lockpicks");
        }

        Target.Cancel(_character);
        pick.OnDoubleClick(_character);
        _character.Target?.Invoke(_character, _chest);
        _nextTry = Core.Now + PickWait;
        _tries++;
        return SkillStatus.Running;
    }

    // Opened the engine's way (a trap still armed goes off now), then emptied into the pack.
    private SkillStatus TickLoot()
    {
        if (_chest.Deleted)
        {
            return SkillStatus.Failed;
        }

        var walk = WalkTo(_chest.Location, HandReach);

        if (walk != null)
        {
            return walk.Value;
        }

        _chest.OnDoubleClick(_character);

        if (_chest.Locked)
        {
            return SkillStatus.Failed;
        }

        var gold = TreasureChestWork.Loot(_character, _chest);
        Log("looted {Gold} gold from a level {Level} treasure chest", gold, _map.Level);
        Talk.Maybe(
            _character,
            TalkCategory.TreasureLoot,
            TalkOdds.TreasureLootPercent,
            gold > 0 ? new TalkSlots { Price = GoldWords.Spoken(gold) } : default
        );
        return SkillStatus.Done;
    }

    private SkillStatus Give(string reason)
    {
        Log(reason + " at {Spot}", _chestAt);
        return SkillStatus.Failed;
    }

    private void Log<T>(string what, T value)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} " + what, _character.Name, value);
        }
    }

    private void Log<T1, T2>(string what, T1 first, T2 second)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} " + what, _character.Name, first, second);
        }
    }
}
