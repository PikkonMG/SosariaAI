using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// The walk back to the body after any raise, shared by the ghost's own way back and by a
/// raise from anyone else (see <see cref="CorpseRunRules"/>). A far body is travelled to; the
/// last stretch is walked tile by tile straight at it, into the guards too, since a person's
/// own body is never a crime. At the body it takes everything back, wears what it wore within
/// its class armor limit and drops the death robe. A body gone, emptied or out of reach sends
/// it to its bank for gold and the shops for its kit. A red goes for a body clear of the
/// guards on its facet, near or far, over guard-free ground; else, and when that walk fails,
/// it recalls home to the Den by its runebook, waiting out a
/// refusal that passes, and re-arms from the spare kit in its bank box (<see cref="SpareKit"/>).
/// With no recall to cast it walks to the Den's bank. A red that could do either, walk close
/// and clear or recall home, lets Jev choose once (<see cref="RedMomentJev"/>).
/// A rider raised with its own mount near gets on first, once per run: the engine sets the
/// mount down at the body and it follows the ghost. The remount waited out the whole run, bank
/// and shops too, with the horse trailing behind, and a recall home left the horse behind.
/// </summary>
public sealed class CorpseRunSkill : Skill
{
    public const string SkillName = "CorpseRun";

    /// <summary>What the log says for a town step that failed without naming why.</summary>
    public const string NoReasonGiven = "no reason given";

    /// <summary>Why a red left a body it could have walked to, for the log.</summary>
    public const string JevChoseRecallReason = "it chose to re-arm first";

    private static readonly ILogger logger = SosariaLog.For(typeof(CorpseRunSkill));

    private SosariaCharacter _character;
    private Skill _walk;
    private Skill _mount;
    private bool _mountLooked;
    private Skill _townStep;
    private bool _shopping;
    private bool _homeward;
    private Skill _recall;
    private Point3D _denBank;
    private DateTime _recallWaitEnds;
    private int _walks;
    private int _townBuys;
    private DateTime _startedAt;
    private readonly JevWait _wayBack = new();
    private bool _wayBackAsked;

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _mount = null;
        _mountLooked = false;
        _townStep = null;
        _shopping = false;
        _homeward = false;
        _recall = null;
        _walks = 0;
        _townBuys = 0;
        _startedAt = Core.Now;
        _wayBack.Close();
        _wayBackAsked = false;
        return character is { Deleted: false, Alive: true };
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true })
        {
            return SkillStatus.Failed;
        }

        if (MountsUpFirst())
        {
            return SkillStatus.Running;
        }

        if (_shopping)
        {
            return TickTownBuy();
        }

        if (_homeward)
        {
            return TickRecallHome();
        }

        if (_walk != null)
        {
            if (_walk.Tick() == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;
        }

        // Jev's walk-or-recall is out: the red stands by until the answer comes or the wait ends.
        if (_wayBack.Waiting(Core.Now))
        {
            return SkillStatus.Running;
        }

        var corpse = _character.OwnCorpse;
        var sameMap = corpse?.Map == _character.Map;
        var distance = corpse == null ? 0 : NavMetric.Chebyshev(_character.Location, corpse.Location);
        var murderer = PkRules.IsRed(_character.Kills);
        var bodyUnderGuards = corpse != null && GuardCall.IsGuardedPlace(corpse.Location, corpse.Map);

        var ruleStep = CorpseRunRules.Next(corpse != null, sameMap, distance, _walks, Core.Now - _startedAt, murderer, bodyUnderGuards);

        if (JudgedStep(ruleStep, murderer, corpse, distance) is not { } step)
        {
            return SkillStatus.Running;
        }

        switch (step)
        {
            case CorpseRunStep.Reclaim:
            {
                return Reclaim(corpse);
            }
            case CorpseRunStep.GiveUp:
            {
                return GiveUp(corpse, sameMap);
            }
            case CorpseRunStep.RecallHome:
            {
                return StartRecallHome(
                    corpse,
                    step == ruleStep ? CorpseRunRules.WhyRecallHome(sameMap, bodyUnderGuards, _walks) : JevChoseRecallReason
                );
            }
            default:
            {
                // A walk that would not start counts as a walk: the next tick tries again, the count ends it.
                return WalkTo(corpse, distance <= CorpseRunRules.CloseWalkTiles, murderer);
            }
        }
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _mount?.Abort();
        _mount = null;
        _recall?.Abort();
        _recall = null;
        _townStep?.Abort();
        _townStep = null;
    }

    public override void Resume(TimeSpan held)
    {
        _startedAt = SkillClock.Shift(_startedAt, held);
        _recallWaitEnds = SkillClock.Shift(_recallWaitEnds, held);
        _walk?.Resume(held);
        _mount?.Resume(held);
        _recall?.Resume(held);
        _townStep?.Resume(held);
    }

    /// <summary>
    /// True while the rider climbs onto its own mount before the run. It looks once, at the
    /// run's first tick (<see cref="MountRules.RemountsFirst"/>); a mount it could not reach is
    /// left to the remount after the run.
    /// </summary>
    private bool MountsUpFirst()
    {
        if (_mount != null)
        {
            if (_mount.Tick() == SkillStatus.Running)
            {
                return true;
            }

            _mount = null;
            return false;
        }

        if (_mountLooked)
        {
            return false;
        }

        _mountLooked = true;

        if (!MountRules.RemountsFirst(_character.Mounted, _character.MayRemount(), _character.Combatant != null, retryDue: true))
        {
            return false;
        }

        var mount = new MountSkill();

        if (!mount.Begin(_character))
        {
            return false;
        }

        _mount = mount;
        return true;
    }

    /// <summary>
    /// A red's close walk to its body, put to Jev once per run when a recall home is open too
    /// (<see cref="RedMomentJev.AsksWayBack"/>): the walk or the recall Jev picks, null while the
    /// question goes out, and the rule's step for no answer, a doubtful one, or a late one. Any
    /// other step is the rule's alone.
    /// </summary>
    private CorpseRunStep? JudgedStep(CorpseRunStep ruleStep, bool murderer, Corpse corpse, int distance)
    {
        var now = Core.Now;

        if (ruleStep == CorpseRunStep.CloseWalk && _wayBack.TryTake(now, out var verdict))
        {
            LogWayBack(verdict);
            return RedMomentJev.WayBackStep(verdict);
        }

        if (!RedMomentJev.AsksWayBack(murderer, ruleStep, _wayBackAsked, RecallHomeOpen))
        {
            return ruleStep;
        }

        _wayBackAsked = true;
        var facts = WayBackFactsOf(corpse, distance);
        return _wayBack.Ask(now, answer => RedMomentJev.TryAskWayBack(_character, facts, answer)) ? null : ruleStep;
    }

    /// <summary>The recall home to the Den's bank could begin now: the bank known and far enough off, a rune and the means.</summary>
    private bool RecallHomeOpen()
    {
        var denBank = RuneKit.DenBankOf(_character)?.Arrival ?? Point3D.Zero;

        return CorpseRunRules.RecallsHome(denBank != Point3D.Zero, NavMetric.Chebyshev(_character.Location, denBank), RuneShelf.NearPlaceTiles) &&
               RecallRules.WhyNoRecall(_character, denBank, RecallRules.NoRoadMinTripTiles) == null;
    }

    /// <summary>What the raised red knows of its body and the ground round it, for Jev.</summary>
    private WayBackFacts WayBackFactsOf(Corpse corpse, int distance)
    {
        var map = corpse.Map;
        var killer = ResurrectOffer.KillerOf(_character);

        return new WayBackFacts(
            SpareKit.Armed(_character),
            distance,
            DungeonGround.RegionNames(map)(corpse.Location) != null,
            killer is { Deleted: false, Alive: true } && killer.Map == map && killer.InRange(corpse.Location, RedMomentJev.WatchTiles),
            RedGang.PeopleNear(_character, corpse.Location, RedMomentJev.WatchTiles, killer),
            DungeonReturn.HostilesNear(map, corpse.Location, RedMomentJev.WatchTiles),
            RedGang.GangMatesNear(_character)
        );
    }

    private void LogWayBack(JevPickVerdict verdict)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} {Source} way back: {Step}",
                _character.Name,
                verdict.Source,
                RedMomentJev.WayBackStep(verdict)
            );
        }
    }

    /// <summary>
    /// Close by, a tile route straight at the body, which no guard rule turns away; failing
    /// that, or far off, a trip by road, gate and rune. A red keeps its tile route off guarded
    /// ground (<see cref="CorpseRunRules.OffGuards"/>) while it stands out of the guards, and
    /// with no such route goes home by recall instead of by road.
    /// </summary>
    private SkillStatus WalkTo(Corpse corpse, bool close, bool murderer)
    {
        _walks++;
        var body = corpse.Location;
        var map = _character.Map;

        if (close)
        {
            var walker = _character.Motor.KeepsOffGuards
                ? CorpseRunRules.OffGuards(Standable.Walker(map), (x, y, z) => GuardCall.IsGuardedPlace(new Point3D(x, y, z), map))
                : Standable.Walker(map);
            var tiles = TileRoute.Find(
                _character.Location,
                body,
                walker,
                (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
                CorpseRunRules.WalkRange
            );

            if (tiles.Count > 0 &&
                StartWalk(GoToSkill.FromPoints(tiles, WalkArrival.TileRouteEndRange(tiles[^1], body, CorpseRunRules.WalkRange))))
            {
                return SkillStatus.Running;
            }
        }

        if (close && CorpseRunRules.AfterNoCloseRoute(murderer) == CorpseRunStep.RecallHome)
        {
            return StartRecallHome(corpse, CorpseRunRules.NoGuardFreeWalkReason);
        }

        StartWalk(new TravelSkill(body, CorpseRunRules.WalkRange));
        return SkillStatus.Running;
    }

    private bool StartWalk(Skill walk)
    {
        if (!walk.Begin(_character))
        {
            return false;
        }

        _walk = walk;
        return true;
    }

    /// <summary>
    /// Takes everything back off the body and dresses again. No body, or an empty one, draws
    /// the shout; that, or a body that gave back no arms, sends it to the bank and the shops
    /// (<see cref="CorpseRunRules.ShopsAfterReclaim"/>).
    /// </summary>
    private SkillStatus Reclaim(Corpse corpse)
    {
        var worn = corpse?.EquipItems is { } equipped ? new List<Item>(equipped) : [];
        var result = _character.TryReclaimCorpse();
        Redress(worn);
        var lost = CorpseRunRules.BodyLost(result);

        if (!CorpseRunRules.ShopsAfterReclaim(lost, SpareKit.Armed(_character)))
        {
            return SkillStatus.Done;
        }

        if (lost)
        {
            Talk.Say(_character, TalkCategory.DeathLooted);
            Scenes.LootedReply(_character);
        }

        return StartTownBuy();
    }

    /// <summary>A body it cannot reach is as good as lost: said once, then the bank and the shops.</summary>
    private SkillStatus GiveUp(Corpse corpse, bool sameMap)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} gives up on its corpse at {Location} ({Why})",
                _character.Name,
                corpse.Location,
                CorpseRunRules.WhyGiveUp(sameMap, _walks)
            );
        }

        Redress([]);
        return StartTownBuy();
    }

    /// <summary>
    /// Wears again what was worn at death, save armor the build does not keep on, then dresses
    /// for its build from the pack (the death robe comes off) and draws its weapon.
    /// </summary>
    private void Redress(List<Item> worn)
    {
        var armorCeiling = GearLadder.KeepCeiling(ClassBuilds.TemplateOf(_character));

        for (var i = 0; i < worn.Count; i++)
        {
            var item = worn[i];

            if (item is { Deleted: false } && item.IsChildOf(_character.Backpack) &&
                item.Layer is not (Layer.Invalid or Layer.Backpack or Layer.Bank or Layer.Mount) &&
                GearEquip.WithinCeiling(item, armorCeiling))
            {
                GearEquip.EquipOrPack(_character, item);
            }
        }

        GearEquip.DressForBuild(_character);
        GearEquip.EquipReadyWeapon(_character);
    }

    /// <summary>
    /// A red leaves its body for the Den: said once in the log, dressed in what it has, then
    /// the recall home (<see cref="TickRecallHome"/>).
    /// </summary>
    private SkillStatus StartRecallHome(Corpse corpse, string why)
    {
        _homeward = true;
        _denBank = RuneKit.DenBankOf(_character)?.Arrival ?? Point3D.Zero;
        _recallWaitEnds = Core.Now + RecallRules.HomeRecallWait;

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} leaves its corpse at {Location} and heads home to the Den ({Why})",
                _character.Name,
                corpse?.Location ?? _character.Location,
                why
            );
        }

        Redress([]);
        return TickRecallHome();
    }

    /// <summary>
    /// The recall home to the Den's bank by the runebook, on any trip length and with no dice
    /// (<see cref="RecallRules.NoRoadMinTripTiles"/>). A refusal that passes, or the criminal
    /// flag, is waited out up to <see cref="RecallRules.HomeRecallWait"/>. Landed, close already,
    /// or with no recall to cast, the bank and the shops follow: a murderer's bank trip walks
    /// to the Den's bank, where the spare kit waits.
    /// </summary>
    private SkillStatus TickRecallHome()
    {
        if (_recall != null)
        {
            var status = _recall.Tick();

            if (status == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (status == SkillStatus.Failed)
            {
                LogWalksHome(_recall.FailReason ?? RecallRules.WordsFailedWhy);
            }

            _recall = null;
            return StartTownBuy();
        }

        if (!CorpseRunRules.RecallsHome(
                _denBank != Point3D.Zero,
                NavMetric.Chebyshev(_character.Location, _denBank),
                RuneShelf.NearPlaceTiles
            ))
        {
            return StartTownBuy();
        }

        var whyNot = RecallRules.WhyNoRecall(_character, _denBank, RecallRules.NoRoadMinTripTiles);

        if (whyNot == null)
        {
            var recall = new RecallSkill(_denBank, RecallRules.NoRoadMinTripTiles);

            if (recall.Begin(_character))
            {
                _recall = recall;
                return SkillStatus.Running;
            }

            whyNot = recall.FailReason ?? RecallRules.WordsFailedWhy;
        }

        if (RecallRules.HomeRecallWaits(whyNot) && Core.Now < _recallWaitEnds)
        {
            return SkillStatus.Running;
        }

        LogWalksHome(whyNot);
        return StartTownBuy();
    }

    /// <summary>A red that could not recall home walks to the Den's bank; the log says why no recall took.</summary>
    private void LogWalksHome(string why)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} walks home to the Den: no recall ({Why})", _character.Name, why);
        }
    }

    /// <summary>To its own bank for gold first, then the shops for the kit.</summary>
    private SkillStatus StartTownBuy()
    {
        _shopping = true;
        return StartTownStep(new BankDepositSkill(Point3D.Zero)) ? SkillStatus.Running : BuyKit();
    }

    private SkillStatus TickTownBuy()
    {
        if (_townStep == null)
        {
            return SkillStatus.Done;
        }

        var status = _townStep.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        var step = _townStep;
        _townStep = null;

        if (status == SkillStatus.Failed)
        {
            LogTownStepFailed(step);
        }

        if (step is UpgradeGearSkill shop && !CorpseRunRules.BuysAgain(shop.Bought, ++_townBuys))
        {
            return SkillStatus.Done;
        }

        return BuyKit();
    }

    /// <summary>One shop trip for the next piece the person lost; none left to buy ends the run.</summary>
    private SkillStatus BuyKit() =>
        StartTownStep(new UpgradeGearSkill()) ? SkillStatus.Running : SkillStatus.Done;

    private bool StartTownStep(Skill step)
    {
        if (!step.Begin(_character))
        {
            LogTownStepFailed(step);
            return false;
        }

        _townStep = step;
        return true;
    }

    /// <summary>
    /// Why a bank or shop step after the death did not happen, in the log: the reds that
    /// gave up a body went home and bought nothing, and no line said why.
    /// </summary>
    private void LogTownStepFailed(Skill step)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} could not {Step} after its death ({Why})",
                _character.Name,
                step.Name,
                step.FailReason ?? NoReasonGiven
            );
        }
    }
}
