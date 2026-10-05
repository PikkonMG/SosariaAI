using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Combat;

/// <summary>
/// The fighting mind of a character, in place of the engine AI a PlayerMobile lacks. It
/// reads the room on the danger scan pace, keeps its own nerve, judges the fight by the
/// trade of blows, fights by its gear (melee, bow or spells) in a stance (its own rules, or
/// Jev's in a fight that matters), steps clear of blows before a cast or a shot onto open
/// ground, looks after its wounds, sings a bard's songs, draws one foe off a group, retreats
/// by walkable ground away from the pack when the fight turns, hides once out of sight, turns
/// on a lone chaser when the pack strings out or on a person once healed or outrun, and ends
/// the fight when the foe is gone.
/// </summary>
public static partial class CombatBrain
{
    /// <summary>A foe this far past perception range, or out of sight, is lost.</summary>
    public const int FoeLostSlackTiles = 4;

    /// <summary>A lost or unreachable foe ends the fight after this long.</summary>
    public const int FoeLostMs = 5000;

    public const int MeleeRange = 1;
    public const int ArcherKeepMin = 4;
    public const int ArcherKeepMax = 8;
    public const int MageKeepMax = 8;
    public const int MinFleeSeconds = 10;
    public const int MaxFleeSeconds = 30;

    /// <summary>A line a think repeats about the same foe is logged again only after this quiet.</summary>
    public static readonly TimeSpan RepeatLineQuiet = TimeSpan.FromSeconds(30);

    private const string BlowsBarelyHurt = "the blows barely hurt";
    private const string Cornered = "cornered";
    private const string PullLine = "pull";
    private const string ShakeLine = "shake";
    private const string StepClearLine = "step clear";
    private const string DrawLine = "draw";
    private const string HuntedLine = "hunted";
    private const string HealedUp = "healed up";
    private const string CannotOutrun = "cannot outrun";

    private static readonly ILogger logger = SosariaLog.For(typeof(CombatBrain));

    /// <summary>
    /// Lines a think can repeat while one thing goes on, once per event: one fighter logged
    /// "picks off a dread spider" every two seconds, 859 times, and runners stopped at bay
    /// every second of a fight.
    /// </summary>
    private static readonly LogGate<(Serial Self, string Line, Serial Foe)> RepeatLines = new(RepeatLineQuiet);

    /// <summary>The foe's side read by <see cref="WouldRunFrom"/>, reused call to call.</summary>
    private static readonly List<HostileStats> FoeSide = [];

    /// <summary>One think while the body is in Combat or Flee.</summary>
    public static void Think(SosariaCharacter character)
    {
        if (character?.Deleted != false || !character.Alive)
        {
            return;
        }

        var memory = MemoryOf(character);
        EndMeditation(character);
        ResolvePendingCast(character, memory);

        if (character.Motor.Action == CharacterAction.Flee)
        {
            ThinkFlee(character, memory);
        }
        else
        {
            ThinkFight(character, memory);
        }
    }

    private static void ThinkFight(SosariaCharacter character, Memory memory)
    {
        var scanned = ScanIfDue(character, memory);
        var foe = character.Combatant;
        var presence = FoePresence(character, memory, foe);

        if (presence == Presence.Gone)
        {
            if (foe is { } fallen && (fallen.Deleted || !fallen.Alive))
            {
                Speak(character, TalkCategory.CombatVictory, TalkOdds.VictoryPercent, fallen);
            }

            var lost = foe;
            foe = NearestAttacker(character, memory);

            if (foe == null)
            {
                GiveUpIfLost(character, memory, lost);
                EndFight(character, memory);
                return;
            }

            memory.FoeLostSince = 0;
            memory.Unreachable = false;
            presence = Presence.Here;
        }

        if (scanned)
        {
            foe = ReconsiderTarget(character, memory, foe);

            if (TryRetreat(character, memory, foe, ordered: false) || TrySing(character, memory, foe))
            {
                return;
            }

            ReadKit(character, memory);
            GearEquip.EquipReadyWeapon(character);
        }

        var caster = IsSpellFighter(character);
        var style = StyleOf(character, caster);

        if (style == CombatStyle.Melee)
        {
            foe = TurnOnCaster(character, memory, foe);
        }

        HoldFight(character, foe);
        var distance = NavMetric.Chebyshev(character.Location, foe.Location);
        ReadThreats(character, memory, foe);
        var safeCircle = SafeCircleNow(character, memory);
        var stance = StanceFor(character, memory, foe, style, caster, distance);
        var careWantsRoom = Care(character, memory, inFight: true, safeCircle, spellsAllowed: stance != CombatStance.Flee);

        // A lost foe is not chased. The fight ends when the grace runs out.
        if (presence == Presence.Lost)
        {
            return;
        }

        if (stance == CombatStance.Flee)
        {
            if (!TryRetreat(character, memory, foe, ordered: true))
            {
                OpenGap(character, memory, foe, FootworkRules.DoubleStep);
            }

            return;
        }

        switch (style)
        {
            case CombatStyle.Mage:
            {
                FightAsMage(character, memory, foe, distance, stance, safeCircle, careWantsRoom);
                break;
            }
            case CombatStyle.Archer:
            {
                if (CastsBetweenBlows(caster, memory))
                {
                    TryCastAttack(character, memory, foe, distance, TankCastGapMs, safeCircle);
                }

                FightAsArcher(character, memory, foe, distance, stance);
                break;
            }
            default:
            {
                if (CastsBetweenBlows(caster, memory))
                {
                    TryCastAttack(character, memory, foe, distance, TankCastGapMs, safeCircle);
                }

                // Jev's "dip away, heal up, come back": a step out of reach while the bandage
                // or the potion works, and back in when the stance says press again.
                if (stance == CombatStance.Heal && JevHolds(memory, CombatStance.Heal) && memory.Threats.Adjacent > 0)
                {
                    OpenGap(character, memory, foe, FootworkRules.SingleStep);
                    break;
                }

                if (Draws(character, memory, foe))
                {
                    break;
                }

                Approach(character, memory, foe, Math.Max(MeleeRange, character.Weapon?.MaxRange ?? MeleeRange));
                break;
            }
        }
    }

    /// <summary>
    /// A fighter that also casts puts spells between its blows until the blows keep breaking
    /// them; then it swings only, until the interrupt window clears.
    /// </summary>
    private static bool CastsBetweenBlows(bool caster, Memory memory) =>
        caster && !StanceRules.KeepsBreaking(memory.Interrupts.Count(Core.TickCount));

    private enum Presence
    {
        Here,
        Lost,
        Gone
    }

    /// <summary>
    /// Here, lost for a while (out of sight, far off, or no way to reach it), or gone (dead,
    /// deleted, another facet, or lost past the grace).
    /// </summary>
    private static Presence FoePresence(SosariaCharacter character, Memory memory, Mobile foe)
    {
        if (foe == null || foe.Deleted || !foe.Alive || foe.Map != character.Map)
        {
            return Presence.Gone;
        }

        var inSight = character.CanSee(foe) &&
                      NavMetric.Chebyshev(character.Location, foe.Location) <=
                      character.RangePerception + FoeLostSlackTiles;
        var now = Core.TickCount;

        // One more try at a foe that had no way to it; the lost clock keeps running meanwhile.
        if (inSight && memory.Unreachable &&
            FootworkRules.RetryChase(foe.Location != memory.UnreachableFoeAt, now - memory.UnreachableSince))
        {
            memory.Unreachable = false;
            return Presence.Here;
        }

        if (inSight && !memory.Unreachable)
        {
            memory.FoeLostSince = 0;
            return Presence.Here;
        }

        if (memory.FoeLostSince == 0)
        {
            memory.FoeLostSince = now;
        }

        return now - memory.FoeLostSince < FoeLostMs ? Presence.Lost : Presence.Gone;
    }

    /// <summary>
    /// A fight that ends on a foe lost past the grace leaves that foe be for the stand-down
    /// grace (<see cref="FootworkRules.GivesUpChase"/>): calls, watches and scans pass it over
    /// (<see cref="SosariaCharacter.StoodDownFrom"/>) as they do a fight stood down from.
    /// </summary>
    private static void GiveUpIfLost(SosariaCharacter character, Memory memory, Mobile lost)
    {
        if (lost == null ||
            !FootworkRules.GivesUpChase(!lost.Deleted && lost.Alive, lost.Map == character.Map, memory.FoeLostSince != 0))
        {
            return;
        }

        character.LastStandDownFoe = lost.Serial;
        character.LastStandDownAt = Core.Now;
    }

    private static void HoldFight(SosariaCharacter character, Mobile foe)
    {
        if (character.Combatant != foe)
        {
            character.Combatant = foe;
        }

        if (character.FocusMob != foe)
        {
            character.FocusMob = foe;
        }

        if (!character.Warmode)
        {
            character.Warmode = true;
        }
    }

    /// <summary>
    /// A runner turns and fights <paramref name="foe"/>: the foe is held first, so the flee's end
    /// finds a live foe and keeps the fighting stance, then the body is in Combat.
    /// </summary>
    private static void TurnToFight(SosariaCharacter character, Mobile foe)
    {
        HoldFight(character, foe);
        character.StopFlee();
        character.Motor.Action = CharacterAction.Combat;
    }

    private static CombatStyle StyleOf(SosariaCharacter character, bool caster)
    {
        if (character.Weapon is BaseRanged)
        {
            return CombatStyle.Archer;
        }

        return caster && HeldWeapon(character) == null ? CombatStyle.Mage : CombatStyle.Melee;
    }

    /// <summary>
    /// Walks into weapon reach. Swings are the engine's once Combatant is set and in range. A
    /// red stops at the guard line (<see cref="CharacterMotor.MoveTo"/>): a foe under the guards
    /// is out of its reach, and the fight ends as for any foe it cannot reach.
    /// </summary>
    private static void Approach(SosariaCharacter character, Memory memory, Mobile foe, int range)
    {
        var moving = character.Motor.MoveTo(foe, range);
        NoteReach(memory, foe, blocked: !moving && CanWalk(character));
    }

    /// <summary>Marks a chase that found no way, with where the foe stood, so it is tried again when that changes.</summary>
    private static void NoteReach(Memory memory, Mobile foe, bool blocked)
    {
        if (blocked && !memory.Unreachable)
        {
            memory.UnreachableSince = Core.TickCount;
            memory.UnreachableFoeAt = foe.Location;
        }

        memory.Unreachable = blocked;
    }

    /// <summary>
    /// A bow shoots only after the archer stood still (a full second before AOS), so an archer
    /// holds a band of distance. A foe inside the band is stepped away from when a shot would
    /// not get off before its blow, and shot where the archer stands once the steps cannot
    /// buy the room.
    /// </summary>
    private static void FightAsArcher(SosariaCharacter character, Memory memory, Mobile foe, int distance, CombatStance stance)
    {
        if (character.Spell != null)
        {
            return;
        }

        if (!character.InLOS(foe))
        {
            EndKite(memory);
            Approach(character, memory, foe, MeleeRange);
            return;
        }

        var keepMax = Math.Min(character.Weapon.MaxRange, ArcherKeepMax);

        if (distance > keepMax)
        {
            EndKite(memory);
            Approach(character, memory, foe, keepMax);
            return;
        }

        var now = Core.TickCount;
        var needMs = FootworkRules.ShotNeedMs(
            FootworkRules.BowStillMs(Core.AOS, Core.SE),
            now - character.LastMoveTime,
            character.NextCombatTime - now
        );
        var holds = CastTiming.Holds(needMs, memory.Threats.SoonestBlowMs);
        ExpireBreakAway(memory);
        var footwork = distance >= ArcherKeepMin
            ? Footwork.Act
            : FootworkRules.Next(holds, KitingMs(memory), memory.Pinned, stance);

        NoteReach(memory, foe, blocked: false);

        switch (footwork)
        {
            case Footwork.OpenGap:
            {
                OpenGap(character, memory, foe, FootworkRules.RetreatSteps(memory.Threats.NearestDistance));
                return;
            }
            case Footwork.Commit:
            {
                if (!memory.Committed && LogsOnce(character, ShakeLine, foe))
                {
                    logger.Information(
                        "{Name} cannot shake {Foe} ({Why}) and shoots where it stands",
                        character.Name,
                        foe.Name,
                        CommitReason(memory, stance)
                    );
                }

                memory.Committed = true;
                break;
            }
            default:
            {
                if (memory.KitingSince != 0 && !memory.Committed && LogsOnce(character, StepClearLine, foe))
                {
                    logger.Information("{Name} stepped clear of {Foe} to shoot", character.Name, foe.Name);
                }

                EndKite(memory);
                break;
            }
        }

        character.Motor.Stop();
    }

    private static bool CanWalk(SosariaCharacter character) =>
        !character.Paralyzed && !character.Frozen && character.Spell?.IsCasting != true;

    /// <summary>
    /// Leaves on the numbers, on a foe far past its dare, or on the hit line, unless the trade
    /// of blows says the fight is being won; or at once when Jev <paramref name="ordered"/> the
    /// flee. A person is left at the same line as a monster. Never where the guards shelter it
    /// (a red under the guards may run out), never from a room too weak to be a threat
    /// (FleeRules.MayFlee), and never once a hunted runner stands fast in its corner
    /// (<see cref="RetreatRules.StandsFast"/>), not even on Jev's word.
    /// </summary>
    private static bool TryRetreat(SosariaCharacter character, Memory memory, Mobile foe, bool ordered)
    {
        var picture = memory.Picture;

        if (memory.StandsFast || !FleeRules.MayFlee(GuardsShelter(character), picture.Threat))
        {
            return false;
        }

        var nerve = NerveOf(character, memory);
        var dare = NerveRules.DarePower(CharacterPower.For(character), nerve, AlliesOn(memory, foe));
        var multiple = ThreatMultiple();
        var hits = Vitals.HitsFraction(character);
        var outnumbered = memory.PickingOff
            ? RetreatRules.PackCaughtUp(picture.CloseAttackers, picture.CloseThreat, dare, multiple)
            : RetreatRules.IsOutnumbered(picture.Attackers, picture.Threat, dare, multiple);
        var overwhelmed = DangerRules.Overwhelms(SoloThreatOf(memory, foe), dare, Party.AlliesPower(character), multiple);
        var line = RetreatRules.Line(BaseLineOf(character), nerve, picture.Attackers);
        var gambling = RetreatRules.IsGambling(nerve, picture.Attackers, hits, Vitals.HitsFraction(foe));

        if (!ordered && (Core.TickCount - memory.AtBayUntil < 0 ||
                         !RetreatRules.ShouldRetreat(hits, line, outnumbered || overwhelmed, gambling, memory.Outlook)))
        {
            return false;
        }

        character.Memory.Danger.NoteSighting(new Point3D(picture.CenterX, picture.CenterY, character.Z), Core.Now);
        BreakOff(character, memory, foe, ordered, FleeSpan());

        if (!Changed(memory, Decision.Retreat))
        {
            return true;
        }

        Speak(character, TalkCategory.CombatFlee, TalkOdds.FleePercent, foe);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} retreats from {Foe} at {Hits}/{HitsMax} hits with {Attackers} on them ({Why}, trade {Outlook})",
                character.Name,
                foe.Name,
                character.Hits,
                character.HitsMax,
                picture.Attackers,
                ordered ? $"{memory.StanceSource} said flee"
                : outnumbered ? "outnumbered"
                : overwhelmed ? "overwhelmed"
                : "below the line",
                memory.Outlook
            );
        }

        return true;
    }

    /// <summary>
    /// Starts a run from <paramref name="from"/>, or from the room when null. A second run from
    /// the same thing inside a minute is a hunted run: it goes farther before it is clear and
    /// keeps off the ground longer. A person run from is left alone for its own grace, beside
    /// the guard-line one (see <see cref="SosariaCharacter.LeavesBe"/>): rallies, the outlaw
    /// watch and the hunt do not hand the fight back. Under the guards the watch still shouts for them.
    /// </summary>
    public static void RunFrom(SosariaCharacter character, Mobile from) => RunFrom(character, MemoryOf(character), from, FleeSpan());

    /// <summary>
    /// A run decided outside the fight's own read: a red leaving a stronger side or a mob by
    /// its outlaw rules. Like a flee Jev ordered, blows that barely hurt do not turn it round;
    /// only a corner does. It keeps the source, the hunted count and the grace for the person
    /// run from. A red run that kept none of them got clear at its first scan, and stood at bay
    /// on the foe it had just left: it ran, turned and ran from the same people every few seconds.
    /// </summary>
    public static void RunDecided(SosariaCharacter character, Mobile from, TimeSpan duration) =>
        BreakOff(character, MemoryOf(character), from, ordered: true, duration);

    private static TimeSpan FleeSpan() => TimeSpan.FromSeconds(Utility.RandomMinMax(MinFleeSeconds, MaxFleeSeconds));

    /// <summary>True when the guards keep this character safe where it stands (<see cref="FleeRules.GuardsShelter"/>).</summary>
    private static bool GuardsShelter(SosariaCharacter character) =>
        FleeRules.GuardsShelter(SosariaCharacter.UnderGuards(character), character.Criminal, character.Murderer);

    /// <summary>Drops the fight and its pull or pick-off, and runs from <paramref name="from"/>.</summary>
    private static void BreakOff(SosariaCharacter character, Memory memory, Mobile from, bool ordered, TimeSpan duration)
    {
        memory.PickingOff = false;
        memory.EscapeGoal = null;
        memory.FleeOrdered = ordered;
        memory.PullFrom = null;
        character.Combatant = null;
        character.FocusMob = null;
        character.Warmode = false;
        RunFrom(character, memory, from, duration);
    }

    private static void RunFrom(SosariaCharacter character, Memory memory, Mobile from, TimeSpan duration)
    {
        var now = Core.TickCount;
        memory.Hunted = RetreatRules.IsHunted(from != null && from == memory.LastFleeFrom, memory.LastFleeAt, now);
        memory.RunStartHits = Vitals.HitsFraction(character);
        memory.FleeFrom = from;
        memory.LastFleeFrom = from;
        memory.LastFleeAt = now;

        if (People.IsLivingPlayer(from))
        {
            character.LastRanFrom = from.Serial;
            character.LastRanFromAt = Core.Now;
        }

        character.BeginFlee(duration);

        if (memory.Hunted && LogsOnce(character, HuntedLine, from))
        {
            logger.Information("{Name} is hunted by {Foe} and runs farther", character.Name, from.Name);
        }
    }

    /// <summary>True when <paramref name="attacker"/> is far past what this character dares (<see cref="DangerRules.Overwhelms"/>).</summary>
    public static bool Overwhelms(SosariaCharacter character, Mobile attacker)
    {
        var memory = MemoryOf(character);
        var dare = NerveRules.DarePower(CharacterPower.For(character), NerveOf(character, memory), AlliesOn(memory, attacker));
        return DangerRules.Overwhelms(SoloThreatOf(memory, attacker), dare, Party.AlliesPower(character), ThreatMultiple());
    }

    /// <summary>
    /// True when a fight on <paramref name="foe"/> is one this character would run from at once
    /// (<see cref="RetreatRules.WouldLeaveAtOnce"/>): the nerve, the dare and the lines of
    /// <see cref="TryRetreat"/>, put before the first blow. The two sides at the spot are
    /// weighed: the foe's side is the foe and the people near it that <paramref name="onFoeSide"/>
    /// takes in, and everyone already on this character wherever it stands, as the retreat
    /// counts them (<see cref="TryRetreat"/>); this character's side is the people near already
    /// fighting the foe, and the fighters of its own group or side near
    /// (<see cref="WorldPlay.Allied"/>) free to join (<see cref="FactionWar.FreeToJoin"/>), as
    /// many as a draw brings in (<see cref="FactionRules.MatesBesideDrawer"/>). Counting the enemy
    /// crowd and not the friends beside it refused every even skirmish: a war band met a band and
    /// nobody drew. Leaving out the pack still chasing it, a red that got clear drew on the next
    /// blue and retreated "outnumbered" the same second, 45 to 60 times a red in two hours.
    /// </summary>
    public static bool WouldRunFrom(SosariaCharacter character, Mobile foe, Func<Mobile, bool> onFoeSide)
    {
        var map = foe?.Map;

        if (map == null || map == Map.Internal || map != character.Map)
        {
            return false;
        }

        FoeSide.Clear();
        FoeSide.Add(HostileRead.Of(foe));
        var alliesOnFoe = 0;
        var freeAllies = 0;

        foreach (var mobile in map.GetMobilesInRange(foe.Location, FactionRules.DraftRange))
        {
            if (mobile == character || mobile == foe || !People.IsLivingPlayer(mobile) || !character.CanSee(mobile))
            {
                continue;
            }

            if (onFoeSide(mobile))
            {
                FoeSide.Add(HostileRead.Of(mobile));
            }
            else if (mobile.Combatant == foe)
            {
                alliesOnFoe++;
            }
            else if (freeAllies < FactionRules.MatesBesideDrawer && mobile is SosariaCharacter mate &&
                     character.InRange(mate.Location, FactionRules.DraftRange) && WorldPlay.Allied(character, mate) &&
                     FactionWar.FreeToJoin(mate, foe))
            {
                freeAllies++;
            }
        }

        foreach (var mobile in map.GetMobilesInRange(character.Location, Math.Max(character.RangePerception, AssistRules.HelpRange)))
        {
            if (mobile != foe && mobile.Alive && mobile.Combatant == character && character.CanSee(mobile) &&
                !(onFoeSide(mobile) && People.IsLivingPlayer(mobile) &&
                  NavMetric.Chebyshev(foe.Location, mobile.Location) <= FactionRules.DraftRange))
            {
                FoeSide.Add(HostileRead.Of(mobile));
            }
        }

        var memory = MemoryOf(character);
        var nerve = NerveOf(character, memory);
        var dare = NerveRules.DarePower(CharacterPower.For(character), nerve, alliesOnFoe + freeAllies);
        var multiple = ThreatMultiple();
        var sideThreat = ThreatRating.Score(FoeSide);

        return FleeRules.MayFlee(GuardsShelter(character), sideThreat) &&
               RetreatRules.WouldLeaveAtOnce(
                   Vitals.HitsFraction(character),
                   RetreatRules.StartLine(BaseLineOf(character), nerve, FoeSide.Count),
                   RetreatRules.IsOutnumbered(FoeSide.Count, sideThreat, dare, multiple),
                   DangerRules.Overwhelms(SoloScore(memory, FoeSide[0]), dare, Party.AlliesPower(character), multiple)
               );
    }

    private static void ThinkFlee(SosariaCharacter character, Memory memory)
    {
        if (!character.CheckFlee())
        {
            if (!RetreatRules.RunsOn(
                    RetreatRules.IsClear(memory.Picture.NearestDistance, SourceDistance(character, memory), memory.Hunted),
                    ChaserOnRunner(character, memory) != null,
                    Core.TickCount - memory.LastFleeAt))
            {
                LeaveGround(character, memory);
                EndFight(character, memory);
                return;
            }

            character.BeginFlee(FleeSpan());
        }

        var scanned = ScanIfDue(character, memory);
        var picture = memory.Picture;

        if (RetreatRules.IsClear(picture.NearestDistance, SourceDistance(character, memory), memory.Hunted))
        {
            if (Changed(memory, Decision.Clear))
            {
                Speak(character, TalkCategory.CombatClear, TalkOdds.ClearPercent, null);

                if (SosariaSettings.LogActivity)
                {
                    logger.Information("{Name} got clear at {Location}", character.Name, character.Location);
                }
            }

            LeaveGround(character, memory);
            EndFight(character, memory);
            return;
        }

        // Casting stops the feet, so a runner drinks and bandages but does not cast; a red's
        // recall home once it broke contact is the one cast a run makes. A hidden runner does
        // neither: a bandage or a potion shows it again.
        if (!character.Hidden)
        {
            Care(character, memory, inFight: true, CastTiming.NoCircle, spellsAllowed: false);
        }

        if (TryRecallOut(character, memory, scanned) || TryFaceChaser(character, memory, picture, scanned) ||
            HidesFromChasers(character, memory, scanned) || TryTurnOnChaser(character, memory, picture))
        {
            return;
        }

        if (WorthStanding(character, memory, picture) && FoeAtBay(character, memory) is { } barelyHurting)
        {
            StandAtBay(character, memory, barelyHurting, BlowsBarelyHurt);
            return;
        }

        // Rooted, not stuck: held or still saying words, the runner waits to be free.
        if (!CanWalk(character))
        {
            return;
        }

        if (RunFromPack(character, memory, picture))
        {
            memory.CorneredTicks = 0;
        }
        else if (++memory.CorneredTicks >= RetreatRules.EscapeStallTicks && NearestRated(memory) is var index and >= 0)
        {
            StandAtBay(character, memory, memory.Foes[index].Mobile, Cornered);
        }
    }

    /// <summary>
    /// A red on the run recalls home to the Den once it broke contact (<see cref="RecallOutRules"/>):
    /// the one cast a runner makes, since the words stop its feet. Asked on the scan pace; a
    /// refusal that passes, the heat of battle above all, is asked again later in the run, and
    /// one that stays ends the tries for the fight. True while the words are being said, so the
    /// runner stands for them.
    /// </summary>
    private static bool TryRecallOut(SosariaCharacter character, Memory memory, bool scanned)
    {
        if (!PkRules.IsRed(character.Kills))
        {
            return false;
        }

        if (TravelSpells.IsCasting(character))
        {
            return true;
        }

        if (!scanned || memory.RecallOutOver)
        {
            return false;
        }

        ReadThreats(character, memory, memory.FleeFrom);

        if (!RecallOutRules.ContactBroken(memory.Threats.SoonestBlowMs, SlowedByProtection(character)))
        {
            return false;
        }

        var home = character.HomeSpot;
        var whyNot = RecallRules.WhyNoRecall(character, home, RecallOutRules.MinTripTiles);
        memory.RecallOutOver = !RecallOutRules.TriesAgain(whyNot);

        if (whyNot == TravelSpells.HeatWhy)
        {
            return WaitOutHeat(character);
        }

        if (whyNot != null || !RecallRules.TryRecallToward(character, home, RecallOutRules.MinTripTiles))
        {
            return false;
        }

        character.Motor.Stop();

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} broke contact and recalls home out of the fight from {Location}", character.Name, character.Location);
        }

        return true;
    }

    /// <summary>
    /// A runner in the heat of battle (<see cref="TravelHeat"/>) keeps running until it cools,
    /// and one that can hide hides and stands still for it, as players did before the words
    /// home. True while it stands hidden.
    /// </summary>
    private static bool WaitOutHeat(SosariaCharacter character)
    {
        character.KeepFleeing(TravelHeat.CoolsIn(character));
        return HideOut(character);
    }

    /// <summary>
    /// A runner out of every chaser's sight hides if it can, as a player ducked round a corner
    /// so the chaser thought it gone. The chaser loses a foe it cannot see and gives it up
    /// (<see cref="GiveUpIfLost"/>). True while the runner stands hidden.
    /// </summary>
    private static bool HidesFromChasers(SosariaCharacter character, Memory memory, bool scanned) =>
        (character.Hidden ||
         scanned && RecallOutRules.HidesOut(character.Skills.Hiding.Value) && !SeenByChasers(character, memory)) &&
        HideOut(character);

    /// <summary>True while a foe on this runner has it in its line of sight: hiding fails then (Hiding.OnUse).</summary>
    private static bool SeenByChasers(SosariaCharacter character, Memory memory)
    {
        foreach (var sighting in memory.Foes)
        {
            if (sighting.AttacksSelf && sighting.Mobile.InLOS(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hides where the runner stands when its Hiding is up to it (<see cref="RecallOutRules.HidesOut"/>),
    /// and holds still while hidden: a step shows it again. True while it stands hidden.
    /// </summary>
    private static bool HideOut(SosariaCharacter character)
    {
        if (!character.Hidden && RecallOutRules.HidesOut(character.Skills.Hiding.Value))
        {
            Server.Skills.UseSkill(character, SkillName.Hiding);

            if (character.Hidden && SosariaSettings.LogActivity)
            {
                logger.Information("{Name} hides from the chase at {Location}", character.Name, character.Location);
            }
        }

        if (!character.Hidden)
        {
            return false;
        }

        character.Motor.Stop();
        return true;
    }

    /// <summary>How far off the thing the run started from stands; <see cref="RoomSurvey.NoDistance"/> when there is none or it is gone.</summary>
    private static int SourceDistance(SosariaCharacter character, Memory memory) =>
        memory.FleeFrom is { Deleted: false, Alive: true } source && source.Map == character.Map
            ? NavMetric.Chebyshev(character.Location, source.Location)
            : RoomSurvey.NoDistance;

    /// <summary>The thing the run started from while it is still on the runner; null when it is not.</summary>
    private static Mobile ChaserOnRunner(SosariaCharacter character, Memory memory) =>
        memory.FleeFrom is { Deleted: false, Alive: true } chaser && chaser.Combatant == character ? chaser : null;

    /// <summary>
    /// A fighter a person keeps chasing turns on that person, as a player did: once it healed
    /// up on the way to a fight it would hold (<see cref="RetreatRules.ComesBackHealed"/>), or
    /// once the chase went on so long it cannot outrun the person (<see cref="RetreatRules.CannotOutrun"/>).
    /// A worker, a runner without its arms and a hidden runner keep to the run. Without it a run
    /// the person stayed on went on for <see cref="RetreatRules.MaxChasedRunMs"/>, at full hits too.
    /// </summary>
    private static bool TryFaceChaser(SosariaCharacter character, Memory memory, RoomPicture picture, bool scanned)
    {
        if (!scanned || character.Hidden || !IsFighter(character) || !SpareKit.Armed(character) ||
            ChaserOnRunner(character, memory) is not { } chaser || !People.IsLivingPlayer(chaser))
        {
            return false;
        }

        var why = RetreatRules.ComesBackHealed(memory.RunStartHits, Vitals.HitsFraction(character)) &&
                  HoldsGround(character, memory, picture)
            ? HealedUp
            : RetreatRules.CannotOutrun(Core.TickCount - memory.LastFleeAt)
                ? CannotOutrun
                : null;

        if (why == null)
        {
            return false;
        }

        StandAtBay(character, memory, chaser, why);
        return true;
    }

    /// <summary>
    /// The ground a run left is declined for fresh fights a while (<see cref="RetreatRules.Avoids"/>):
    /// where the pack stood, else where the thing it ran from stands, else here. Without it a
    /// runner turned round on the first monster that followed and was outnumbered again.
    /// </summary>
    private static void LeaveGround(SosariaCharacter character, Memory memory)
    {
        var picture = memory.Picture;
        memory.AvoidAt = picture.Any
            ? new Point3D(picture.CenterX, picture.CenterY, character.Z)
            : memory.FleeFrom is { Deleted: false } source && source.Map == character.Map
                ? source.Location
                : character.Location;
        memory.AvoidUntil = Core.TickCount + RetreatRules.AvoidForMs(memory.Hunted);
    }

    /// <summary>
    /// A melee pull walks in for its first blow (<see cref="FightPullRules.WaitsForFirstBlow"/>),
    /// then backs off the middle of the group while the foe it pulled follows, for a few
    /// seconds at most, and turns to fight once only the target is near or the group is well
    /// behind (<see cref="FightPullRules.KeepsDrawing"/>). True while it still backs off.
    /// </summary>
    private static bool Draws(SosariaCharacter character, Memory memory, Mobile foe)
    {
        if (memory.PullFrom is not { } group)
        {
            return false;
        }

        var follows = foe.Combatant == character;

        if (FightPullRules.WaitsForFirstBlow(memory.PullSince != 0, follows))
        {
            return false;
        }

        var now = Core.TickCount;

        if (memory.PullSince == 0)
        {
            memory.PullSince = now;
        }

        if (FightPullRules.KeepsDrawing(follows, now - memory.PullSince, memory.Picture.Count, NavMetric.Chebyshev(character.Location, group)))
        {
            StepClear(character, group);
            return true;
        }

        memory.PullFrom = null;

        if (LogsOnce(character, DrawLine, foe))
        {
            logger.Information("{Name} drew {Foe} off the group and fights it at {Location}", character.Name, foe.Name, character.Location);
        }

        return false;
    }

    /// <summary>
    /// A fighter running from foes that barely scratch it, with the hits to start a fight and a
    /// pack it would answer if hit, is running from a fight it would win: it stops. A worker
    /// keeps running, and so does a runner Jev sent off or a red that decided to leave a
    /// stronger side (<see cref="RunDecided"/>). The foes' size on paper does not send
    /// it off; the damage it takes does.
    /// </summary>
    private static bool WorthStanding(SosariaCharacter character, Memory memory, RoomPicture picture)
    {
        if (memory.FleeOrdered || picture.Attackers == 0 || !IsFighter(character) ||
            !FightTrendRules.LightDamage(memory.Trend, character.HitsMax))
        {
            return false;
        }

        return HoldsGround(character, memory, picture);
    }

    /// <summary>True where the retreat test would not send this runner off again (<see cref="RetreatRules.HoldsGround"/>).</summary>
    private static bool HoldsGround(SosariaCharacter character, Memory memory, RoomPicture picture)
    {
        var nerve = NerveOf(character, memory);

        return RetreatRules.HoldsGround(
            Vitals.HitsFraction(character),
            BaseLineOf(character),
            nerve,
            picture.Attackers,
            NerveRules.DarePower(CharacterPower.For(character), nerve, AlliesOn(memory, memory.FleeFrom)),
            picture.Threat,
            HuntSkill.HasHealing(character),
            Party.AlliesPower(character),
            ThreatMultiple()
        );
    }

    /// <summary>A character with a fighting build; a worker only runs.</summary>
    private static bool IsFighter(SosariaCharacter character) =>
        (character.Build?.Role ?? CharacterRole.Worker) != CharacterRole.Worker;

    /// <summary>
    /// The foe a runner the blows barely hurt turns on: the one it ran from while that one is
    /// still on it and in reach, else the nearest that is. Turning on the nearest alone put
    /// one or two blows on each mob of a pack in turn.
    /// </summary>
    private static Mobile FoeAtBay(SosariaCharacter character, Memory memory) =>
        memory.FleeFrom is { Deleted: false, Alive: true } left &&
        left.Map == character.Map &&
        left.Combatant == character &&
        character.InRange(left, RoomSurvey.MeleeStragglerReach)
            ? left
            : NearestAttacker(character, memory);

    /// <summary>
    /// Stops running and fights <paramref name="foe"/> where it stands: a runner with nowhere
    /// left to go (water, walls, a dead end, or a person it cannot outrun) rather than pushing
    /// on, one the blows barely hurt, or one that healed up. It does not try to run again for
    /// <see cref="RetreatRules.AtBayMs"/>, long enough for the trade of blows to show how the
    /// fight really goes, and a flee Jev ordered is dropped, or it would send the runner off at
    /// once; a hunted runner with nowhere left to go does not run again this fight
    /// (<see cref="RetreatRules.StandsFast"/>).
    /// </summary>
    private static void StandAtBay(SosariaCharacter character, Memory memory, Mobile foe, string why)
    {
        memory.CorneredTicks = 0;
        memory.AtBayUntil = Core.TickCount + RetreatRules.AtBayMs;
        memory.StandsFast |= RetreatRules.StandsFast(memory.Hunted, why is Cornered or CannotOutrun);
        memory.EscapeGoal = null;

        if (memory.JevStance == CombatStance.Flee)
        {
            memory.JevStance = null;
            memory.StanceUntil = 0;
        }

        TurnToFight(character, foe);

        if (LogsOnce(character, why, foe))
        {
            logger.Information(
                "{Name} stops running at {Location} ({Why}) and fights {Foe}",
                character.Name,
                character.Location,
                why,
                foe.Name
            );
        }
    }

    /// <summary>
    /// The pack strung out and one chaser runs alone in front: turn and fight it, if it is a
    /// foe this character would fight alone and it is fit to start. Limited per fight.
    /// </summary>
    private static bool TryTurnOnChaser(SosariaCharacter character, Memory memory, RoomPicture picture)
    {
        var lead = NearestRated(memory);

        if (lead < 0 || memory.Turns >= RetreatRules.MaxTurns)
        {
            return false;
        }

        var chaser = memory.Foes[lead];
        var ranged = character.Weapon is BaseRanged;
        var keepMax = ranged ? Math.Min(character.Weapon.MaxRange, ArcherKeepMax) : ArcherKeepMax;
        var straggler = RoomSurvey.IsStraggler(
            picture.NearestDistance,
            picture.SecondDistance,
            ranged,
            ArcherKeepMin,
            keepMax
        );
        var nerve = NerveOf(character, memory);
        var hits = Vitals.HitsFraction(character);
        var startLine = RetreatRules.StartLine(BaseLineOf(character), nerve, RetreatRules.SingleAttacker);
        var soloDare = NerveRules.DarePower(CharacterPower.For(character), nerve, alliesOnFoe: 0);
        var fits = ThreatRating.ShouldEngage(
            soloDare,
            chaser.SoloThreat,
            hits,
            HuntSkill.HasHealing(character),
            alliesPower: 0,
            ThreatMultiple(),
            startLine,
            alreadyAttacked: false
        );

        if (!RetreatRules.ShouldTurnOnChaser(memory.Turns, chaser.IsPerson, hits >= startLine, straggler, fits))
        {
            return false;
        }

        memory.Turns++;
        memory.PickingOff = true;
        memory.EscapeGoal = null;
        TurnToFight(character, chaser.Mobile);

        if (!Changed(memory, Decision.Turn))
        {
            return true;
        }

        Speak(character, TalkCategory.CombatTurn, TalkOdds.TurnPercent, chaser.Mobile);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} turns on {Foe} running {Distance} tiles ahead of the pack",
                character.Name,
                chaser.Mobile.Name,
                chaser.Distance
            );
        }

        return true;
    }

    /// <summary>
    /// Runs one leg away from the middle of the pack, not from the one foe in view, to a place
    /// it can reach (<see cref="EscapeRoute"/>): along a coast or a wall, back the way it came,
    /// or to a road node, never into the water or a corner. Arriving still in trouble chains to
    /// the next leg; a leg that points back toward the pack is replaced; one that stalls is
    /// struck off and another is picked. With no place to go it steps clear by the ground.
    /// False when it could not move at all this think.
    /// </summary>
    private static bool RunFromPack(SosariaCharacter character, Memory memory, RoomPicture picture)
    {
        var here = character.Location;
        var pack = new Point3D(picture.CenterX, picture.CenterY, character.Z);

        if (memory.EscapeGoal != null &&
            (NavMetric.Chebyshev(here, memory.EscapeAt) <= EscapeRules.SameGoalTiles ||
             !RoomSurvey.StillAway(memory.EscapeAt, here, picture.CenterX, picture.CenterY)))
        {
            memory.EscapeGoal = null;
        }

        if (memory.EscapeGoal == null)
        {
            // Reading the ground costs a few hundred step checks; with nowhere found it waits a moment.
            if (Core.TickCount - memory.NextEscapePickAt < 0)
            {
                return StepClear(character, pack);
            }

            if (EscapeRoute.PickGoal(character, pack, EscapeRules.LegTiles, SafetyFrom(character, memory), memory.StalledEscapes) is not { } goal)
            {
                memory.NextEscapePickAt = Core.TickCount + RetreatRules.EscapeRepickMs;
                return StepClear(character, pack);
            }

            memory.EscapeAt = goal;
            memory.EscapeGoal = goal;
            memory.EscapeStalls = 0;
        }

        if (character.Motor.MoveToPoint(memory.EscapeGoal))
        {
            memory.EscapeStalls = 0;
            return true;
        }

        if (NavMetric.Chebyshev(character.Location, memory.EscapeAt) <= EscapeRules.SameGoalTiles)
        {
            return true;
        }

        if (++memory.EscapeStalls < RetreatRules.EscapeStallTicks)
        {
            return true;
        }

        memory.StalledEscapes.Add(memory.EscapeAt);
        memory.EscapeGoal = null;
        return StepClear(character, pack);
    }

    /// <summary>
    /// A run from a person leans toward a safe place (<see cref="FleeSkill.SafePlace"/>): a
    /// house door or the guards of a bank, where a red breaks off. Run straight away from it,
    /// a miner led its red round the wild for ten minutes. A run from monsters runs away only.
    /// </summary>
    private static Point3D? SafetyFrom(SosariaCharacter character, Memory memory) =>
        People.IsLivingPlayer(memory.FleeFrom) ? FleeSkill.SafePlace(character) : null;

    /// <summary>One step back from the pack by the ground; true while the body could not step yet or did.</summary>
    private static bool StepClear(SosariaCharacter character, Point3D pack) =>
        !character.Motor.CanMoveNow || character.Motor.StepAwayFrom(pack);

    /// <summary>
    /// Ends the fight: no combatant, no war mode, no focus, back to wandering. Leaving the
    /// Combat or Flee action runs the driver's action hook, which restores town stance when
    /// the character is not hunting. A flee that timed out already left the action, with
    /// the combatant still set, so the stance is restored here in that one case.
    /// </summary>
    private static void EndFight(SosariaCharacter character, Memory memory)
    {
        memory.ForgetFight();
        character.Combatant = null;
        character.Warmode = false;
        character.FocusMob = null;
        character.Motor.Stop();

        switch (character.Motor.Action)
        {
            case CharacterAction.Flee:
            {
                character.StopFlee();
                break;
            }
            case CharacterAction.Combat:
            {
                character.Motor.Action = CharacterAction.Wander;
                break;
            }
            default:
            {
                if (!RoutineDriver.IsHunting(character))
                {
                    character.RestoreTownStance();
                }

                break;
            }
        }
    }

    /// <summary>A line at a decision point, now and then; the talk rest keeps a fight from chattering.</summary>
    private static void Speak(SosariaCharacter character, string category, int percent, Mobile foe) =>
        Talk.Maybe(character, category, percent, new TalkSlots { Foe = TalkWords.Foe(foe) });

    /// <summary>True when activity logging is on and <paramref name="line"/> about <paramref name="foe"/> starts a new event.</summary>
    private static bool LogsOnce(SosariaCharacter character, string line, Mobile foe) =>
        SosariaSettings.LogActivity && RepeatLines.Opens((character.Serial, line, foe.Serial), Core.Now);

    /// <summary>
    /// True while the character stands at bay (see <see cref="StandAtBay"/>): a red's own run
    /// waits it out, or the two turn it round every second of the fight.
    /// </summary>
    public static bool HoldsAtBay(SosariaCharacter character) =>
        character != null && Memories.TryGetValue(character, out var memory) && AtBay(memory);

    /// <summary>Inside the bay window, or standing fast for the rest of the fight.</summary>
    private static bool AtBay(Memory memory) => memory.StandsFast || Core.TickCount - memory.AtBayUntil < 0;

    private static bool Changed(Memory memory, Decision decision)
    {
        if (memory.Decision == decision)
        {
            return false;
        }

        memory.Decision = decision;
        return true;
    }

    /// <summary>True while a stance Jev set stands and it is this one.</summary>
    private static bool JevHolds(Memory memory, CombatStance stance) =>
        memory.JevStance == stance && Core.TickCount - memory.StanceUntil < 0;

    private static double BaseLineOf(SosariaCharacter character) =>
        RetreatRules.BaseLine(character.Build?.Role ?? CharacterRole.Worker, character.Build?.Veteran == true);

    private static double ThreatMultiple() =>
        SosariaSettings.Characters?.Career?.ThreatMultiple ?? ThreatRating.DefaultThreatMultiple;
}
