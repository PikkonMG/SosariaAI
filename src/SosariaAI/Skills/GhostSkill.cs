using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Deliberation;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// The dead character's whole way back. It wails over its body a moment (a gray one until its
/// flag lapses). A red ghost out of the guards' reach then calls a gang mate or a red near it
/// over (<see cref="ResurrectOffer.CallRedHelper"/>); any ghost asks a living person in sight who
/// can raise it, climbs out of a dungeon, or walks to the ankh or healer with the shortest trip
/// it can get to (<see cref="GhostReach"/>) and is raised there. A red ghost under the guards
/// is raised by nobody and at no ankh there: its bars keep it off the towns but for the way out
/// of the one it stands in, and it walks out to an ankh off the guarded ground. A red ghost with
/// two or three ways back (a red to call, the ankh, a busy gang mate to wait for) lets Jev choose
/// once (<see cref="RedMomentJev"/>).
/// With no way found it mills about and looks again, less often after each miss, and with no
/// way and no headway for <see cref="GhostRules.NoWayFallbackAfter"/> it stands up on its own.
/// Alive again, it runs the shared corpse run (<see cref="CorpseRunSkill"/>): back to the body,
/// its things and its armour back on, or the bank and the shops when the body is lost. A person
/// raised while no ghost step ran starts at that corpse run (<see cref="AfterRaise"/>).
/// </summary>
public sealed class GhostSkill : Skill
{
    public const string SkillName = "Ghost";

    private const string AnkhReason = "ankh";
    private const string HealerReason = "healer";

    /// <summary>The raise tile of each shrine by its catalog spot, found once: ankhs do not move.</summary>
    private static readonly Dictionary<(Map Map, Point3D Shrine), Point3D?> AnkhSpots = new();

    private readonly Dictionary<Serial, DateTime> _refusedPeople = new();
    private readonly Dictionary<Point3D, DateTime> _refusedSites = new();

    private readonly bool _raisedElsewhere;

    private SosariaCharacter _character;
    private GhostPhase _phase;
    private TravelSkill _walk;
    private CorpseRunSkill _corpseRun;
    private HomeRange _home;
    private DateTime _hauntUntil;
    private DateTime _seekRetryAt;
    private int _seekMisses;
    private Point3D _waitAnchor;
    private DateTime _nextHelperScanAt;
    private Mobile _helper;
    private int _asks;
    private DateTime _firstAskAt;
    private DateTime _lastAskAt;
    private Point3D _site;
    private bool _raisedAtSite;
    private Point3D _lastSpot;
    private DateTime _lastProgressAt;
    private readonly JevWait _wayAsk = new();
    private bool _wayAsked;
    private GhostWayFacts _wayFacts;
    private ResSource? _wayAnkh;
    private DateTime _mateWaitUntil;

    public GhostSkill()
    {
    }

    private GhostSkill(bool raisedElsewhere) => _raisedElsewhere = raisedElsewhere;

    public override string Name => SkillName;

    /// <summary>The way back for a person already raised by someone else: it starts at the corpse run.</summary>
    public static GhostSkill AfterRaise() => new(raisedElsewhere: true);

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _corpseRun = null;
        _seekRetryAt = default;
        _seekMisses = 0;
        _waitAnchor = Point3D.Zero;
        _nextHelperScanAt = default;
        _helper = null;
        _site = Point3D.Zero;
        _raisedAtSite = false;
        _lastSpot = character.Location;
        _lastProgressAt = Core.Now;
        _refusedPeople.Clear();
        _refusedSites.Clear();
        _wayAsk.Close();
        _wayAsked = false;
        _wayFacts = default;
        _wayAnkh = null;
        _mateWaitUntil = default;
        _phase = GhostPhase.Haunt;
        _hauntUntil = Core.Now + GhostRules.HauntFor(character.Serial.Value);
        _home = HomeRange.Capture(character);

        if (character is not { Deleted: false })
        {
            return false;
        }

        // Alive already: only a step made for that raise runs, and its first tick starts the corpse run.
        if (!character.IsGhost)
        {
            return _raisedElsewhere;
        }

        character.Home = character.CorpseLocation == Point3D.Zero ? character.Location : character.CorpseLocation;
        character.RangeHome = GhostRules.HauntDrift;
        Manifest();
        // The killer fills {foe}: "that troll came outta nowhere" says what really happened.
        Talk.Say(character, TalkCategory.GhostHaunt, new TalkSlots { Foe = character.LastKiller?.Name });
        Scenes.DeathJoke(character);
        return true;
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (!_character.IsGhost && !IsRaisedPhase(_phase))
        {
            return AdvanceRaised();
        }

        if (_character.IsGhost)
        {
            var coming = ResurrectOffer.HelperFor(_character);
            NoteProgress(coming != null);

            if (GhostRules.ShouldFallback(Core.Now, _character.GhostSince, _lastProgressAt))
            {
                return FallBack(GhostRules.StuckReason);
            }

            Manifest();

            if (coming != null)
            {
                return HoldFor(coming);
            }
        }

        return _phase switch
        {
            GhostPhase.Haunt => TickHaunt(),
            GhostPhase.SeekAid => TickSeekAid(),
            GhostPhase.AskHelper => TickAskHelper(),
            GhostPhase.ExitDungeon => TickGhostWalk(raiseOnArrival: false),
            GhostPhase.WalkToShrine => TickGhostWalk(raiseOnArrival: true),
            GhostPhase.CorpseRun => TickCorpseRun(),
            _ => SkillStatus.Failed
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _corpseRun?.Abort();
        _corpseRun = null;
        _home.Restore(_character);
    }

    public override void Resume(TimeSpan held)
    {
        _lastProgressAt = SkillClock.Shift(_lastProgressAt, held);
        _hauntUntil = SkillClock.Shift(_hauntUntil, held);
        _seekRetryAt = SkillClock.Shift(_seekRetryAt, held);
        _firstAskAt = SkillClock.Shift(_firstAskAt, held);
        _lastAskAt = SkillClock.Shift(_lastAskAt, held);
        _mateWaitUntil = SkillClock.Shift(_mateWaitUntil, held);
        _walk?.Resume(held);
        _corpseRun?.Resume(held);
    }

    /// <summary>The phase after the raise runs to the body; the raise itself happens only once.</summary>
    public static bool IsRaisedPhase(GhostPhase phase) => phase is GhostPhase.CorpseRun;

    public static bool MaySeekAgain(DateTime now, DateTime retryAt) =>
        retryAt == default || now >= retryAt;

    /// <summary>
    /// A step taken, or a helper on the way, is headway on the way back. Milling about while it
    /// waits for its next look is not: the stuck ghost still stands up in the end.
    /// </summary>
    private void NoteProgress(bool helperComing)
    {
        if (_character.Location == _lastSpot && !helperComing)
        {
            return;
        }

        _lastSpot = _character.Location;

        if (!helperComing && WaitingToSeek)
        {
            return;
        }

        _lastProgressAt = Core.Now;
        _seekMisses = 0;
    }

    private bool WaitingToSeek => _phase == GhostPhase.SeekAid && !MaySeekAgain(Core.Now, _seekRetryAt);

    /// <summary>A ghost in war mode shows itself to the living, the way a player asks for a res.</summary>
    private void Manifest()
    {
        if (!_character.Warmode)
        {
            _character.Warmode = true;
        }
    }

    private SkillStatus TickHaunt()
    {
        if (GhostRules.KeepsHaunting(Core.Now, _hauntUntil, _character.Criminal, _character.Murderer, _character.GhostSince))
        {
            _character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
            return SkillStatus.Running;
        }

        _home.Restore(_character);
        _phase = GhostPhase.SeekAid;
        return TickSeekAid();
    }

    private SkillStatus TickSeekAid()
    {
        if (TryRaiseAtSite())
        {
            return AdvanceRaised();
        }

        // Between looks, and while Jev weighs its way back, the ghost mills about where it stands.
        if (!MaySeekAgain(Core.Now, _seekRetryAt) || _wayAsk.Waiting(Core.Now))
        {
            DriftWhileWaiting();
            return SkillStatus.Running;
        }

        // Over the shard's planning budget, the look waits for a later think.
        if (!PlanBudget.TryEnter())
        {
            return SkillStatus.Running;
        }

        EndWait();

        if (JudgedWayBack() is { } judged)
        {
            return judged;
        }

        // A red ghost calls a red over before it walks anywhere: reds help reds up.
        if (CallRedHelper() is { } coming)
        {
            return HoldFor(coming);
        }

        var helper = FindHelper();
        var exit = DungeonExit();

        // A door the ghost could not walk to is no way out; it looks for another way instead.
        if (IsRefusedSite(exit))
        {
            exit = Point3D.Zero;
        }

        _phase = GhostRules.NextRoute(helper != null, exit != Point3D.Zero);

        if (_phase == GhostPhase.AskHelper)
        {
            AskNew(helper);
            return SkillStatus.Running;
        }

        if (_phase == GhostPhase.ExitDungeon)
        {
            return WalkToSite(exit, GhostRules.HealerRange);
        }

        var started = PlanBudget.Start();
        var source = PickSource(out _);
        PlanBudget.Charge(started);

        return source is { } site ? WalkToSite(site.Location, site.Range) : NoWayYet();
    }

    /// <summary>
    /// A red ghost's way back as Jev judges it (<see cref="RedMomentJev"/>): once per death, above
    /// ground, with no other helper in sight, and only with two or more of its ways open: a free
    /// red to call, the ankh with the shortest trip, a busy gang mate near to wait for. Jev's way
    /// is taken while it still holds; a missing, doubtful or late answer takes the rule order.
    /// Null leaves this look to the rule order below it.
    /// </summary>
    private SkillStatus? JudgedWayBack()
    {
        var now = Core.Now;

        if (_wayAsk.TryTake(now, out var verdict))
        {
            var way = RedMomentJev.GhostWay(verdict, _wayFacts);
            WorldPlay.Log($"{_character.Name} {verdict.Source} way back as a ghost: {way}");
            return FollowWay(way, now);
        }

        if (now < _mateWaitUntil)
        {
            return WaitForMate(now);
        }

        if (!RedMomentJev.MayAskGhostWay(_character.Murderer, ResurrectOffer.WantedUnderGuards(_character), _wayAsked) ||
            DungeonExit() != Point3D.Zero || FindHelper() != null)
        {
            return null;
        }

        _wayAsked = true;
        var facts = WayFactsOf(out _wayAnkh);
        _wayFacts = facts;
        return _wayAsk.Ask(now, answer => RedMomentJev.TryAskGhostWay(_character, facts, answer)) ? SkillStatus.Running : null;
    }

    /// <summary>Sets out on the way chosen; null when it no longer holds, and the rule order runs.</summary>
    private SkillStatus? FollowWay(RedGhostWay way, DateTime now)
    {
        switch (way)
        {
            case RedGhostWay.CallHelper:
            {
                return CallRedHelper() is { } coming ? HoldFor(coming) : null;
            }
            case RedGhostWay.WalkToAnkh:
            {
                if (_wayAnkh is not { } site || IsRefusedSite(site.Location))
                {
                    return null;
                }

                _phase = GhostRules.NextRoute(helperInSight: false, underground: false);
                return WalkToSite(site.Location, site.Range);
            }
            default:
            {
                _mateWaitUntil = now + RedMomentJev.MateWait;
                return WaitForMate(now);
            }
        }
    }

    /// <summary>By its body for a busy gang mate: it calls again every helper scan until the wait runs out.</summary>
    private SkillStatus WaitForMate(DateTime now)
    {
        if (CallRedHelper() is { } coming)
        {
            return HoldFor(coming);
        }

        _seekRetryAt = now + GhostRules.HelperScanGap;
        return SkillStatus.Running;
    }

    /// <summary>
    /// The ways back a red ghost has now, and what it knows of each, for Jev: the red it would
    /// call, the ankh <see cref="PickSource"/> takes (its trip, whether the straight way cuts a
    /// guarded town, blue fighters there), and the gang mates near but busy.
    /// </summary>
    private GhostWayFacts WayFactsOf(out ResSource? ankh)
    {
        var map = _character.Map;
        var helper = ResurrectOffer.FindRedHelper(_character, out var helperIsMate);
        var started = PlanBudget.Start();
        ankh = PickSource(out var tripTiles);
        PlanBudget.Charge(started);
        var busyMates = helper != null && helperIsMate ? 0 : RedGang.GangMatesNear(_character);

        return new GhostWayFacts(
            helper != null,
            helperIsMate,
            helper == null ? 0 : NavMetric.Chebyshev(_character.Location, helper.Location),
            ankh != null,
            tripTiles,
            ankh is { } site &&
            GuardedLegs.Crosses(_character.Location, site.Location, (x, y, z) => GuardCall.IsGuardedPlace(new Point3D(x, y, z), map)),
            ankh is { } at ? RedGang.PeopleNear(_character, at.Location, RedMomentJev.WatchTiles, null) : 0,
            busyMates > 0,
            busyMates,
            ResurrectOffer.KillerNear(_character)
        );
    }

    /// <summary>Sets out for a dungeon door, an ankh or a healer; one the walk cannot start for is skipped a while.</summary>
    private SkillStatus WalkToSite(Point3D site, int range)
    {
        _site = site;
        return StartWalk(site, range) ? SkillStatus.Running : RefuseSite();
    }

    /// <summary>
    /// This look found no helper in sight, no door out and no ankh or healer the ghost can get
    /// to. It looks again later; with no headway for <see cref="GhostRules.NoWayFallbackAfter"/>
    /// it stands up on its own instead of waiting out the half hour.
    /// </summary>
    private SkillStatus NoWayYet() =>
        GhostRules.ShouldFallbackNoWay(Core.Now, _lastProgressAt) ? FallBack(GhostRules.NoWayReason) : SeekAgainLater();

    /// <summary>The last resort: the ghost stands up and goes home, and the log says why.</summary>
    private SkillStatus FallBack(string why)
    {
        _phase = GhostPhase.Fallback;
        _home.Restore(_character);
        _character.FallbackFromGhost(why);
        return SkillStatus.Failed;
    }

    private SkillStatus TickAskHelper()
    {
        var helper = _helper;

        if (!StillUseful(helper))
        {
            return GiveUpOn(helper);
        }

        if (helper is BaseHealer)
        {
            if (TryRaiseAtSite())
            {
                return AdvanceRaised();
            }

            return _character.Motor.MoveTo(helper, GhostRules.HealerRange) ? SkillStatus.Running : GiveUpOn(helper);
        }

        if (!_character.InRange(helper, GhostRules.AskRange))
        {
            return _character.Motor.MoveTo(helper, GhostRules.AskRange) ? SkillStatus.Running : GiveUpOn(helper);
        }

        _character.Motor.ClearMoveIntent();
        _character.Direction = _character.GetDirectionTo(helper);

        if (GhostRules.AskExpired(Core.Now, _firstAskAt))
        {
            return GiveUpOn(helper);
        }

        if (GhostRules.AskIsDue(Core.Now, _lastAskAt, _asks))
        {
            Plead(helper);
        }

        return SkillStatus.Running;
    }

    /// <summary>Someone is on the way to raise this ghost: stand still, show itself and ask.</summary>
    private SkillStatus HoldFor(SosariaCharacter coming)
    {
        _character.Motor.ClearMoveIntent();

        if (_helper != coming)
        {
            AskNew(coming);
        }

        if (GhostRules.AskIsDue(Core.Now, _lastAskAt, _asks))
        {
            SayPlea();
        }

        return SkillStatus.Running;
    }

    private void AskNew(Mobile helper)
    {
        _helper = helper;
        _asks = 0;
        _firstAskAt = default;
        _lastAskAt = default;
    }

    /// <summary>Asks a living character outright; a human hears the same plea and answers it with its own hands.</summary>
    private void Plead(Mobile helper)
    {
        SayPlea();

        if (helper is SosariaCharacter living)
        {
            ResurrectOffer.TryStartAid(living, _character, asked: true);
        }
    }

    private void SayPlea()
    {
        _asks++;
        _lastAskAt = Core.Now;

        if (_firstAskAt == default)
        {
            _firstAskAt = Core.Now;
        }

        Talk.Say(_character, TalkCategory.GhostPlea);
    }

    private SkillStatus GiveUpOn(Mobile helper)
    {
        if (helper != null)
        {
            _refusedPeople[helper.Serial] = Core.Now + GhostRules.HelperRefuseFor;
        }

        _helper = null;
        _phase = GhostPhase.SeekAid;
        return SkillStatus.Running;
    }

    private bool StillUseful(Mobile helper) =>
        helper is { Deleted: false, Alive: true } &&
        helper.Map == _character.Map &&
        _character.InRange(helper, GhostRules.AidSearchRange) &&
        (helper is BaseHealer || ResurrectOffer.MethodFor(helper) != AidMethod.None);

    /// <summary>
    /// The way out of a dungeon or the walk to an ankh or a healer. A red that answers a red
    /// ghost's call on the way, or a helper who comes into sight, is waited for or asked instead.
    /// </summary>
    private SkillStatus TickGhostWalk(bool raiseOnArrival)
    {
        if (TryRaiseAtSite())
        {
            return AdvanceRaised();
        }

        if (Core.Now >= _nextHelperScanAt)
        {
            _nextHelperScanAt = Core.Now + GhostRules.HelperScanGap;

            // The next tick holds for the red that set out (ResurrectOffer.HelperFor).
            if (CallRedHelper() != null)
            {
                return StopWalkFor(GhostPhase.SeekAid);
            }

            if (FindHelper() is { } helper)
            {
                AskNew(helper);
                return StopWalkFor(GhostPhase.AskHelper);
            }
        }

        if (_walk == null)
        {
            return SeekAgainLater();
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (!raiseOnArrival && status == SkillStatus.Done)
        {
            _phase = GhostPhase.SeekAid;
            return SkillStatus.Running;
        }

        if (raiseOnArrival && TryRaiseAtSite())
        {
            return AdvanceRaised();
        }

        return RefuseSite();
    }

    /// <summary>Drops the walk under way and turns to <paramref name="phase"/>.</summary>
    private SkillStatus StopWalkFor(GhostPhase phase)
    {
        _walk?.Abort();
        _walk = null;
        _phase = phase;
        return SkillStatus.Running;
    }

    /// <summary>
    /// A door, ankh or healer the ghost could not walk to, or that did not raise it, is skipped
    /// for a while, so the next look picks another way instead of the same one again.
    /// </summary>
    private SkillStatus RefuseSite()
    {
        if (_site != Point3D.Zero)
        {
            _refusedSites[_site] = Core.Now + GhostRules.SiteRefuseFor;
        }

        return SeekAgainLater();
    }

    // A walk that could not start or that failed part way leaves the ghost where it is.
    // Look for a way back again after a wait, longer after each miss in a row.
    private SkillStatus SeekAgainLater()
    {
        _walk = null;
        _phase = GhostPhase.SeekAid;
        _seekRetryAt = Core.Now + GhostRules.SeekRetryDelay(++_seekMisses);
        return SkillStatus.Running;
    }

    /// <summary>A ghost with no way back yet mills about where it stands, not frozen on one tile.</summary>
    private void DriftWhileWaiting()
    {
        if (_waitAnchor == Point3D.Zero)
        {
            _waitAnchor = _character.Location;
            _character.Home = _waitAnchor;
            _character.RangeHome = GhostRules.HauntDrift;
        }

        _character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
    }

    private void EndWait()
    {
        if (_waitAnchor == Point3D.Zero)
        {
            return;
        }

        _waitAnchor = Point3D.Zero;
        _home.Restore(_character);
    }

    private SkillStatus AdvanceRaised()
    {
        _walk?.Abort();
        _walk = null;
        _home.Restore(_character);
        ThankTheHelper();
        _phase = GhostRules.AfterRaised();
        _corpseRun = new CorpseRunSkill();
        return _corpseRun.Begin(_character) ? SkillStatus.Running : SkillStatus.Done;
    }

    private void ThankTheHelper()
    {
        if (_raisedAtSite)
        {
            return;
        }

        foreach (var mobile in _character.GetMobilesInRange(GhostRules.ThanksRange))
        {
            if (mobile != _character && People.IsLivingPlayer(mobile) && _character.CanSee(mobile))
            {
                Talk.Say(_character, TalkCategory.ResThanks);
                return;
            }
        }
    }

    /// <summary>The shared corpse run ends the way back, however it ends.</summary>
    private SkillStatus TickCorpseRun() =>
        _corpseRun?.Tick() == SkillStatus.Running ? SkillStatus.Running : SkillStatus.Done;

    private bool StartWalk(Point3D target, int range)
    {
        _walk = new TravelSkill(target, range);

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    /// <summary>The nearest living person or healer in sight who could raise this ghost and would.</summary>
    private Mobile FindHelper()
    {
        var map = _character.Map;

        if (map == null || map == Map.Internal || !GhostRules.MayRaiseInPlace(ResurrectOffer.KillerNear(_character)) ||
            ResurrectOffer.WantedUnderGuards(_character))
        {
            return null;
        }

        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var mobile in map.GetMobilesInRange(_character.Location, GhostRules.AidSearchRange))
        {
            if (mobile == _character || ResurrectOffer.TookPartInKill(mobile, _character) ||
                mobile is not { Deleted: false, Alive: true } || IsRefused(mobile) || !_character.CanSee(mobile) ||
                !_character.InLOS(mobile) || !WouldAsk(mobile))
            {
                continue;
            }

            var distance = (int)_character.GetDistanceToSqrt(mobile);

            if (distance < bestDistance)
            {
                best = mobile;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// A town healer turns away murderers and criminals; an evil healer raises anyone. A
    /// character is asked when it would say yes; a human when it carries the means and is
    /// not a red the ghost should fear.
    /// </summary>
    private bool WouldAsk(Mobile mobile) =>
        mobile switch
        {
            PricedHealer => false,
            EvilHealer or EvilWanderingHealer => true,
            BaseHealer => !_character.Murderer && !_character.Criminal,
            SosariaCharacter living => ResurrectOffer.MethodFor(living) != AidMethod.None &&
                                       ResurrectOffer.WillAid(living, _character, asked: true),
            _ => People.IsHuman(mobile) && ResurrectOffer.MethodFor(mobile) != AidMethod.None &&
                 (!mobile.Murderer || _character.Murderer)
        };

    /// <summary>
    /// A red ghost out of the guards' reach calls a gang mate, else a red in sight, over to raise
    /// it (<see cref="ResurrectOffer.CallRedHelper"/>): the red that set out, or null. Under the
    /// guards it calls nobody: it walks out first.
    /// </summary>
    private SosariaCharacter CallRedHelper() => ResurrectOffer.WantedUnderGuards(_character) ? null : ResurrectOffer.CallRedHelper(_character);

    private bool IsRefused(Mobile mobile) =>
        _refusedPeople.TryGetValue(mobile.Serial, out var until) && GhostRules.StillRefused(Core.Now, until);

    private bool IsRefusedSite(Point3D site) =>
        _refusedSites.TryGetValue(site, out var until) && GhostRules.StillRefused(Core.Now, until);

    /// <summary>A healer beside the ghost who accepts it, or an ankh within reach, raises it here.</summary>
    private bool TryRaiseAtSite()
    {
        var map = _character.Map;

        if (!_character.IsGhost || map == null || map == Map.Internal || ResurrectOffer.WantedUnderGuards(_character) ||
            !map.CanFit(_character.Location, PersonBody.Height, false, false))
        {
            return false;
        }

        foreach (var mobile in map.GetMobilesInRange(_character.Location, GhostRules.HealerRange))
        {
            if (mobile is not BaseHealer healer || healer is PricedHealer || IsRefused(healer) || !healer.InLOS(_character))
            {
                continue;
            }

            if (healer.CheckResurrect(_character))
            {
                return RaiseHere(HealerReason);
            }

            _refusedPeople[healer.Serial] = Core.Now + GhostRules.HelperRefuseFor;
        }

        foreach (var item in map.GetItemsInRange(_character.Location, GhostRules.AnkhRange))
        {
            if (item is AnkhWest or AnkhNorth)
            {
                return RaiseHere(AnkhReason);
            }
        }

        return false;
    }

    private bool RaiseHere(string reason)
    {
        _raisedAtSite = true;
        _character.RestoreLife(reason);
        return !_character.IsGhost;
    }

    /// <summary>The door out of the dungeon or dropped place the ghost is in, or zero when above ground.</summary>
    private Point3D DungeonExit() => DungeonGround.DoorOutOf(_character.Map, _character.Location);

    /// <summary>
    /// The tile beside the ankh of the shrine at <paramref name="shrine"/> where a ghost can
    /// stand and stay, or null. World thread: it reads the map's items and tiles.
    /// </summary>
    private static Point3D? AnkhSpot(Map map, Point3D shrine)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        if (AnkhSpots.TryGetValue((map, shrine), out var known))
        {
            return known;
        }

        Point3D? spot = null;

        foreach (var item in map.GetItemsInRange(shrine, GhostRules.AnkhSearchTiles))
        {
            if (item is AnkhWest or AnkhNorth &&
                GhostSeek.RaiseSpot(item.Location, GhostRules.AnkhRange, (x, y) => StandAt(map, x, y, item.Z)) is { } found)
            {
                spot = found;
                break;
            }
        }

        AnkhSpots[(map, shrine)] = spot;
        return spot;
    }

    /// <summary>Where a person stands on a tile near the ankh's height, or null when it does not fit or a pad lies there.</summary>
    private static Point3D? StandAt(Map map, int x, int y, int nearZ)
    {
        ReadOnlySpan<int> heights = [nearZ, map.GetAverageZ(x, y)];

        foreach (var z in heights)
        {
            var at = new Point3D(x, y, z);

            if (map.CanFit(at, PersonBody.Height, false, false) && !GatePad.IsUnder(map, at, NavGateKind.Teleporter))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>
    /// The ankh or healer with the shortest trip from here (<see cref="GhostSeek.ShortestTrip"/>)
    /// that takes this ghost, that it has not given up on, and that it can get to
    /// (<see cref="GhostReach"/>), or null, and the tiles of that trip. A red ghost takes no ankh
    /// under the guards: it would stand up in town.
    /// </summary>
    private ResSource? PickSource(out int tripTiles)
    {
        tripTiles = 0;
        var catalog = NavWorld.DestinationsFor(_character.HomeFacet);

        if (catalog == null || !People.InWorld(_character))
        {
            return null;
        }

        var healerWelcome = !_character.Murderer && !_character.Criminal;
        var sources = new List<ResSource>();

        for (var i = 0; i < catalog.All.Count; i++)
        {
            var dest = catalog.All[i];
            var healer = dest.ParsedKind == DestinationKind.Healer;

            if (!(healer && healerWelcome || dest.ParsedKind == DestinationKind.Shrine))
            {
                continue;
            }

            // A shrine is walked to its raise tile; one with no tile to stand on raises nobody.
            var source = healer
                ? new ResSource(GhostSeek.KindHealer, dest.Arrival, GhostRules.HealerRange)
                : AnkhSpot(_character.Map, dest.Arrival) is { } spot
                    ? new ResSource(GhostSeek.KindShrine, spot, GhostRules.ShrineStandTiles)
                    : (ResSource?)null;

            if (source is { } site && !IsRefusedSite(site.Location) &&
                !(_character.Murderer && GuardCall.IsGuardedPlace(site.Location, _character.Map)))
            {
                sources.Add(site);
            }
        }

        if (sources.Count == 0)
        {
            return null;
        }

        var reach = GhostReach.For(_character);
        var trips = new Dictionary<Point3D, double>();
        var best = GhostSeek.ShortestTrip(
            _character.Location,
            sources,
            site =>
            {
                var trip = reach.TripTiles(site);

                if (trip is { } tiles)
                {
                    trips[site.Location] = tiles;
                }

                return trip;
            }
        );

        tripTiles = best is { } picked ? (int)trips[picked.Location] : 0;
        return best;
    }
}
