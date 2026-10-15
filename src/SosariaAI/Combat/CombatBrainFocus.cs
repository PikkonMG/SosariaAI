using System;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Combat;

/// <summary>Target choice and the room survey. One paced map scan feeds both.</summary>
public static partial class CombatBrain
{
    private const string TankWaitLine = "tank wait";
    private const string TankJoinLine = "tank join";

    /// <summary>
    /// Scans perception range for the best foe the fight mode and IsEnemy allow, reads the
    /// room around it, and sets FocusMob when this character dares the fight or can pull one
    /// foe out of it. In a party the tank's foe comes first, and a member opens no fresh fight
    /// while a ready tank is near (<see cref="PartyTacticsRules"/>); a melee fighter healed
    /// since its run goes back for the lone monster it ran from (<see cref="MeleeTacticsRules.ComesBackFor"/>).
    /// Between scans the last answer stands.
    /// </summary>
    public static bool AcquireFocus(SosariaCharacter character)
    {
        if (character?.Deleted != false || !character.Alive || !People.InWorld(character))
        {
            return false;
        }

        if (character.FightMode == FightMode.None)
        {
            character.FocusMob = null;
            return false;
        }

        var memory = MemoryOf(character);

        if (!ScanIfDue(character, memory))
        {
            return character.FocusMob is { Deleted: false, Alive: true } focus &&
                   focus.Map == character.Map &&
                   character.CanSee(focus);
        }

        var tankIndex = TankTargetIndex(character, memory, ownFoeOnSelf: false, out var tank);

        if (tankIndex >= 0)
        {
            character.FocusMob = memory.Foes[tankIndex].Mobile;
            NoteJoinTank(character, tank, character.FocusMob);
            return true;
        }

        var comeBack = ComeBackIndex(character, memory);
        var best = comeBack >= 0 ? comeBack : PickVisible(character, memory, byIsolation: false);

        if (best < 0 || WaitsForTank(character, memory, tank, best) ||
            DecideEngage(character, memory, best, out var pick) == EngageChoice.Decline)
        {
            character.FocusMob = null;
            return false;
        }

        character.FocusMob = memory.Foes[pick].Mobile;

        if (comeBack == pick)
        {
            NoteComeBack(character, character.FocusMob);
        }

        return true;
    }

    /// <summary>
    /// The party's tank: the strongest melee fighter of <paramref name="member"/>'s party near
    /// it, the member itself included (<see cref="PartyTacticsRules.CanTank"/>, <see cref="PartyTacticsRules.StrongerTank"/>).
    /// Null outside a party or with no one fit to tank.
    /// </summary>
    public static Mobile PartyTank(Mobile member)
    {
        var party = member == null ? null : GameParty.Of(member);

        if (party == null || party.Count < PartyTacticsRules.MinPartySize)
        {
            return null;
        }

        Mobile best = null;
        var bestPower = 0;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var mate = party.Members[i].Mobile;

            if (mate == null || mate.Deleted ||
                !PartyTacticsRules.CanTank(
                    mate.Alive,
                    mate.Map == member.Map,
                    NavMetric.Chebyshev(member.Location, mate.Location),
                    StyleOfFoe(mate) == CombatStyle.Melee,
                    mate is not SosariaCharacter bot || IsFighter(bot),
                    mate is SosariaCharacter { Motor.Action: CharacterAction.Flee }))
            {
                continue;
            }

            var power = CharacterPower.Healthy(mate);

            if (best == null || PartyTacticsRules.StrongerTank(power, mate.Serial.Value, bestPower, best.Serial.Value))
            {
                best = mate;
                bestPower = power;
            }
        }

        return best;
    }

    /// <summary>The live foe the party's tank fights, the party's focus; null when the tank fights nothing or there is none.</summary>
    public static Mobile TankFoe(Mobile member) => FoeOfTank(member, PartyTank(member));

    /// <summary>The live foe <paramref name="tank"/> fights outside <paramref name="member"/>'s party, or null.</summary>
    private static Mobile FoeOfTank(Mobile member, Mobile tank) =>
        tank?.Combatant is { Deleted: false, Alive: true } foe && GameParty.Of(member)?.Contains(foe) != true ? foe : null;

    /// <summary>
    /// The index in the last scan of the foe this member's tank fights, when the member takes it
    /// up (<see cref="PartyTacticsRules.TakesTankTarget"/>); else -1. <paramref name="tank"/> is
    /// the party's tank, or null.
    /// </summary>
    private static int TankTargetIndex(SosariaCharacter character, Memory memory, bool ownFoeOnSelf, out Mobile tank)
    {
        tank = PartyTank(character);

        if (tank == null || tank == character || FoeOfTank(character, tank) is not { } target)
        {
            return -1;
        }

        var index = IndexOf(memory, target);
        var inSight = index >= 0 && memory.Foes[index].Acquirable && !memory.Foes[index].OutOfSight && character.InLOS(target);

        return PartyTacticsRules.TakesTankTarget(inSight, ownFoeOnSelf, memory.Picture.CloseAttackers) ? index : -1;
    }

    /// <summary>
    /// True when this member leaves the foe at <paramref name="best"/> for its tank to pull
    /// (<see cref="PartyTacticsRules.WaitsForTank"/>): a bot tank is ready while it hunts, a
    /// person while in war mode.
    /// </summary>
    private static bool WaitsForTank(SosariaCharacter character, Memory memory, Mobile tank, int best)
    {
        if (tank == null || tank == character)
        {
            return false;
        }

        var ready = tank is SosariaCharacter bot ? RoutineDriver.IsHunting(bot) : tank.Warmode;
        var foe = memory.Foes[best].Mobile;

        if (!PartyTacticsRules.WaitsForTank(ready, memory.Foes[best].Tier))
        {
            return false;
        }

        if (LogsOnce(character, TankWaitLine, foe))
        {
            logger.Information("{Name} leaves {Foe} for its tank {Tank} to pull", character.Name, foe.Name, tank.Name);
        }

        return true;
    }

    /// <summary>Logs a member taking up its tank's foe, once.</summary>
    private static void NoteJoinTank(SosariaCharacter character, Mobile tank, Mobile foe)
    {
        if (LogsOnce(character, TankJoinLine, foe))
        {
            logger.Information("{Name} joins its tank {Tank} on {Foe}", character.Name, tank.Name, foe.Name);
        }
    }

    /// <summary>True when <paramref name="mobile"/> stands in the group the last engage call left alone (<see cref="FocusRules.InDeclinedGroup"/>).</summary>
    public static bool Declined(SosariaCharacter character, Mobile mobile) =>
        character != null && mobile != null &&
        Memories.TryGetValue(character, out var memory) &&
        FocusRules.InDeclinedGroup(memory.DeclinedAt, Core.TickCount, NavMetric.Chebyshev(mobile.Location, memory.DeclinedNear));

    /// <summary>
    /// True while <paramref name="at"/> lies on the ground the last run left (<see cref="RetreatRules.Avoids"/>).
    /// A hunt keeps off it as the engage call does, or it walks back into the pack it ran from.
    /// </summary>
    public static bool AvoidsGround(SosariaCharacter character, Point3D at) =>
        character != null &&
        Memories.TryGetValue(character, out var memory) &&
        RetreatRules.Avoids(memory.AvoidUntil, Core.TickCount, NavMetric.Chebyshev(at, memory.AvoidAt), alreadyInFight: false);

    private static bool ScanIfDue(SosariaCharacter character, Memory memory)
    {
        if (!ScanPace.Due(Core.TickCount, ref memory.NextScanAt, ScanPace.DangerMs, character.Serial.Value))
        {
            return false;
        }

        Scan(character, memory);
        return true;
    }

    /// <summary>
    /// Reads everything in reach: the character's own sight, and out to the assist range the
    /// foes already on it or on a friend. A runner reads out to the distance its run is clear
    /// at, so the pack it leaves is seen leaving. The nearest <see cref="MaxSightings"/> are kept.
    /// </summary>
    private static void Scan(SosariaCharacter character, Memory memory)
    {
        memory.Foes.Clear();
        memory.AllyTargets.Clear();
        memory.Room.Reset();
        var map = character.Map;
        var fleeing = character.Motor.Action == CharacterAction.Flee;
        var sight = fleeing ? RetreatRules.ClearAt(memory.Hunted) : character.RangePerception;
        var radius = fleeing ? sight : Math.Max(sight, AssistRules.HelpRange);

        if (map != null && map != Map.Internal)
        {
            foreach (var mobile in map.GetMobilesInRange(character.Location, radius))
            {
                if (mobile == character || mobile.Deleted || !mobile.Alive)
                {
                    continue;
                }

                if (!IsFoe(character, mobile, out var attacksSelf))
                {
                    NoteAlly(character, memory, mobile);
                    continue;
                }

                var distance = NavMetric.Chebyshev(character.Location, mobile.Location);
                var attacksFriend = AttacksFriend(character, mobile);

                if (FocusRules.Registers(distance, sight, attacksSelf, attacksFriend))
                {
                    Keep(character, memory, mobile, distance, attacksSelf, attacksFriend);
                }
            }
        }

        TallyRoom(memory);
        CountPackmates(memory);
        memory.Picture = memory.Room.Finish(character.X, character.Y);
        TallyTrade(character, memory);
    }

    /// <summary>A full list gives up its farthest foe to a nearer one.</summary>
    private static void Keep(SosariaCharacter character, Memory memory, Mobile mobile, int distance, bool attacksSelf, bool attacksFriend)
    {
        var foes = memory.Foes;

        if (foes.Count < MaxSightings)
        {
            foes.Add(Sight(character, memory, mobile, distance, attacksSelf, attacksFriend));
            return;
        }

        var farthest = 0;

        for (var i = 1; i < foes.Count; i++)
        {
            if (foes[i].Distance > foes[farthest].Distance)
            {
                farthest = i;
            }
        }

        if (distance < foes[farthest].Distance)
        {
            foes[farthest] = Sight(character, memory, mobile, distance, attacksSelf, attacksFriend);
        }
    }

    private static void TallyRoom(Memory memory)
    {
        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];

            if (!sighting.Harmless)
            {
                memory.Room.Add(sighting.Mobile.X, sighting.Mobile.Y, sighting.Distance, sighting.AttacksSelf, sighting.Stats);
            }
        }
    }

    /// <summary>
    /// The mobile is fighting a person on this character's side, its team or its party, and a
    /// blow on it is no crime for this character: an Order foe of a Chaos friend is innocent to an
    /// unguilded one, and the guards come for that blow.
    /// </summary>
    private static bool AttacksFriend(SosariaCharacter character, Mobile mobile) =>
        mobile.Combatant is { Deleted: false, Alive: true } victim &&
        victim != character &&
        People.IsLivingPlayer(victim) &&
        (SameSide(character, victim) || GameParty.Of(character) is { } party && party == GameParty.Of(victim)) &&
        !character.IsHarmfulCriminal(mobile);

    /// <summary>
    /// A foe this character leaves be unless it comes at it: the person it broke off from inside
    /// the stand-down grace, the side it ran from lately, and a creature a bard's song calmed.
    /// </summary>
    private static bool LeftAlone(SosariaCharacter character, Mobile mobile) =>
        mobile is BaseCreature { BardPacified: true } || WorldPlay.LeavesBeSide(character, mobile);

    /// <summary>What <paramref name="foe"/> rates alone: from the last scan, or read now when the scan missed it.</summary>
    private static int SoloThreatOf(Memory memory, Mobile foe)
    {
        var index = IndexOf(memory, foe);

        if (index >= 0)
        {
            return memory.Foes[index].SoloThreat;
        }

        return SoloScore(memory, HostileRead.Of(foe));
    }

    /// <summary>What one foe read as <paramref name="stats"/> rates alone.</summary>
    private static int SoloScore(Memory memory, HostileStats stats)
    {
        memory.One[0] = stats;
        return ThreatRating.Score(memory.One);
    }

    /// <summary>
    /// Books the trade of blows since the last scan: what the target and every foe on this
    /// character lost (a foe that died since counts all it had), and this character's hits.
    /// The trend and the outlook it gives are read from the ledger.
    /// </summary>
    private static void TallyTrade(SosariaCharacter character, Memory memory)
    {
        var seen = memory.SeenHits;
        var left = 0;
        seen.Clear();

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];
            var mobile = sighting.Mobile;

            if (sighting.Harmless || !sighting.AttacksSelf && mobile != character.Combatant)
            {
                continue;
            }

            if (memory.FoeHits.TryGetValue(mobile, out var before))
            {
                memory.Ledger.Dealt(before - mobile.Hits);
            }

            seen[mobile] = mobile.Hits;
            left += mobile.Hits;
        }

        foreach (var (mobile, before) in memory.FoeHits)
        {
            if (!seen.ContainsKey(mobile) && (mobile.Deleted || !mobile.Alive))
            {
                memory.Ledger.Dealt(before);
            }
        }

        memory.SeenHits = memory.FoeHits;
        memory.FoeHits = seen;

        var now = Core.TickCount;
        memory.Ledger.Note(now, character.Hits);
        memory.Trend = memory.Ledger.Read(now, left);
        memory.Outlook = FightTrendRules.Outlook(memory.Trend, character.Hits);
    }

    private static bool IsFoe(SosariaCharacter character, Mobile mobile, out bool attacksSelf)
    {
        attacksSelf = mobile.Combatant == character;

        if (mobile.Blessed || mobile.AccessLevel > AccessLevel.Player || mobile.Hidden ||
            mobile.IsDeadBondedPet || SameSide(character, mobile) || !character.CanSee(mobile))
        {
            return false;
        }

        return attacksSelf || character.IsEnemy(mobile);
    }

    /// <summary>
    /// Characters on one team do not fight each other unless their guilds are at war.
    /// A creature on the character's team is on its side too.
    /// </summary>
    private static bool SameSide(SosariaCharacter character, Mobile mobile) =>
        mobile switch
        {
            SosariaCharacter other => other.Team == character.Team &&
                                      SosariaSettings.GuildWars?.AtWar(character.GuildIndex, other.GuildIndex, Core.Now) != true,
            BaseCreature creature => creature.Team == character.Team,
            _ => false
        };

    // A person already fighting something, in sight, is a friend in that fight.
    private static void NoteAlly(SosariaCharacter character, Memory memory, Mobile mobile)
    {
        if (People.IsLivingPlayer(mobile) && People.Perceives(character, mobile) &&
            mobile.Combatant is { Deleted: false } fighting &&
            memory.AllyTargets.Count < MaxSightings)
        {
            memory.AllyTargets.Add(fighting);
        }
    }

    private static Sighting Sight(
        SosariaCharacter character,
        Memory memory,
        Mobile mobile,
        int distance,
        bool attacksSelf,
        bool attacksFriend
    )
    {
        var stats = HostileRead.Of(mobile);
        var solo = SoloScore(memory, stats);
        var harmless = HarmlessCreatures.IsHarmless(mobile.GetType().Name, solo);
        var isPerson = People.IsLivingPlayer(mobile);
        var hostile = attacksSelf || attacksFriend || character.Combatant == mobile || IsAggressor(character, mobile);
        var karma = (mobile as BaseCreature)?.GetMaster()?.Karma ?? mobile.Karma;
        var fresh = !RetreatRules.Avoids(
                        memory.AvoidUntil,
                        Core.TickCount,
                        NavMetric.Chebyshev(mobile.Location, memory.AvoidAt),
                        attacksSelf || attacksFriend
                    ) &&
                    (attacksSelf || !LeftAlone(character, mobile));

        return new Sighting
        {
            Mobile = mobile,
            Distance = distance,
            Tier = FocusRules.Tier(
                isPerson,
                PkRules.IsRed(mobile.Kills),
                mobile.Criminal,
                People.IsLivingPlayer(mobile.Combatant),
                attacksSelf,
                attacksFriend
            ),
            Acquirable = fresh && FocusRules.PassesFightMode(character.FightMode, hostile, karma),
            AttacksSelf = attacksSelf,
            IsPerson = isPerson,
            Harmless = harmless,
            Stats = stats,
            SoloThreat = solo,
            HitsFraction = Vitals.HitsFraction(mobile)
        };
    }

    /// <summary>True when either side hit the other: <paramref name="other"/> is in the aggression lists of <paramref name="self"/>.</summary>
    public static bool IsAggressor(Mobile self, Mobile other)
    {
        foreach (var info in self.Aggressors)
        {
            if (info.Attacker == other)
            {
                return true;
            }
        }

        foreach (var info in self.Aggressed)
        {
            if (info.Defender == other)
            {
                return true;
            }
        }

        return false;
    }

    private static void CountPackmates(Memory memory)
    {
        var foes = memory.Foes;

        for (var i = 0; i < foes.Count; i++)
        {
            var sighting = foes[i];
            var packmates = 0;

            for (var j = 0; j < foes.Count; j++)
            {
                if (i != j && !foes[j].Harmless &&
                    NavMetric.Chebyshev(sighting.Mobile.Location, foes[j].Mobile.Location) <= FightPullRules.IsolateRange)
                {
                    packmates++;
                }
            }

            sighting.Packmates = packmates;
            foes[i] = sighting;
        }
    }

    /// <summary>
    /// The best acquirable foe in line of sight: by rank, or by isolation for a pull.
    /// Line of sight is checked only on the leader, and a blocked leader is skipped.
    /// </summary>
    private static int PickVisible(SosariaCharacter character, Memory memory, bool byIsolation)
    {
        var foes = memory.Foes;

        for (var attempt = 0; attempt < foes.Count; attempt++)
        {
            var best = -1;
            var bestScore = double.MinValue;

            for (var i = 0; i < foes.Count; i++)
            {
                var sighting = foes[i];

                if (!sighting.Acquirable || sighting.OutOfSight)
                {
                    continue;
                }

                var score = byIsolation
                    ? FocusRules.IsolationScore(sighting.Packmates, sighting.Distance)
                    : FocusRules.Score(sighting.Tier, sighting.Distance, sighting.HitsFraction, sighting.Packmates);

                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (best < 0)
            {
                return -1;
            }

            if (character.InLOS(foes[best].Mobile))
            {
                return best;
            }

            var blocked = foes[best];
            blocked.OutOfSight = true;
            foes[best] = blocked;
        }

        return -1;
    }

    /// <summary>
    /// Reads the room around the chosen foe and asks nerve whether to take it on, pull the
    /// most isolated foe out of it, or leave it alone. A bow or a spell leaves a pull alone when
    /// that foe has a packmate near (<see cref="FightPullRules.ShootsIntoPack"/>). <paramref name="pick"/>
    /// is the foe to fight when the answer is not Decline.
    /// </summary>
    private static EngageChoice DecideEngage(SosariaCharacter character, Memory memory, int best, out int pick)
    {
        var foes = memory.Foes;
        var focus = foes[best];
        var extras = 0;
        memory.FoeRoom.Reset();

        for (var i = 0; i < foes.Count; i++)
        {
            var sighting = foes[i];

            if (sighting.Harmless ||
                NavMetric.Chebyshev(sighting.Mobile.Location, focus.Mobile.Location) > FightPullRules.IsolateRange)
            {
                continue;
            }

            memory.FoeRoom.Add(sighting.Mobile.X, sighting.Mobile.Y, sighting.Distance, sighting.AttacksSelf, sighting.Stats);

            if (i != best)
            {
                extras++;
            }
        }

        var room = memory.FoeRoom.Finish(focus.Mobile.X, focus.Mobile.Y);
        var pull = FightPullRules.NeedsIsolate(extras) ? PickVisible(character, memory, byIsolation: true) : best;
        pull = pull < 0 ? best : pull;
        var nerve = NerveOf(character, memory);
        var power = CharacterPower.For(character);
        var alliesOnFoe = AlliesOn(character, memory, focus.Mobile);
        var facts = new EngageFacts(
            power,
            nerve,
            alliesOnFoe,
            Party.AlliesPower(character),
            Vitals.HitsFraction(character),
            HuntSkill.HasHealing(character),
            ThreatMultiple(),
            RetreatRules.StartLine(BaseLineOf(character), nerve, Math.Max(RetreatRules.SingleAttacker, room.Count)),
            room.Threat,
            foes[pull].SoloThreat,
            extras,
            memory.Picture.Attackers > 0 || character.Combatant is { Deleted: false, Alive: true }
        );
        var melee = StyleOf(character, IsSpellFighter(character)) == CombatStyle.Melee;
        var choice = NerveRules.Decide(facts);

        if (choice == EngageChoice.PullStraggler && FightPullRules.ShootsIntoPack(melee, foes[pull].Packmates))
        {
            choice = EngageChoice.Decline;
        }

        memory.DeclinedAt = choice == EngageChoice.Decline ? Core.TickCount : 0;
        memory.DeclinedNear = focus.Mobile.Location;

        switch (choice)
        {
            case EngageChoice.PullStraggler:
            {
                pick = pull;

                // Melee must close to swing, so it draws the foe off the group first; bow and
                // spell already fight from range, at a foe that stands apart.
                memory.PullFrom = melee ? new Point3D(room.CenterX, room.CenterY, character.Z) : null;
                memory.PullSince = 0;

                if (!Changed(memory, Decision.Pull))
                {
                    break;
                }

                Speak(character, TalkCategory.CombatPull, TalkOdds.PullPercent, foes[pull].Mobile);

                if (LogsOnce(character, PullLine, foes[pull].Mobile))
                {
                    logger.Information(
                        "{Name} picks off {Foe} from a group of {Count} at {Location}",
                        character.Name,
                        foes[pull].Mobile.Name,
                        room.Count,
                        character.Location
                    );
                }

                break;
            }
            case EngageChoice.Decline:
            {
                pick = best;
                memory.PullFrom = null;

                if (Changed(memory, Decision.Decline) && SosariaSettings.LogActivity)
                {
                    logger.Information(
                        "{Name} sizes up {Count} around {Foe} and holds back (threat {Threat} vs dare {Dare})",
                        character.Name,
                        room.Count,
                        focus.Mobile.Name,
                        room.Threat,
                        NerveRules.DarePower(power, nerve, alliesOnFoe)
                    );
                }

                break;
            }
            default:
            {
                pick = best;
                memory.PullFrom = null;

                if (Changed(memory, Decision.Engage))
                {
                    Speak(
                        character,
                        alliesOnFoe > 0 ? TalkCategory.CombatAssist : TalkCategory.CombatEngage,
                        alliesOnFoe > 0 ? TalkOdds.AssistPercent : TalkOdds.EngagePercent,
                        focus.Mobile
                    );
                }

                break;
            }
        }

        return choice;
    }

    /// <summary>People in sight already fighting <paramref name="foe"/>.</summary>
    /// <summary>
    /// The friends this character counts beside it against <paramref name="foe"/>: the people
    /// already on it, and its own pets fit to fight (<see cref="TamerTacticsRules.PetReady"/>),
    /// which it sends in. Counting only the people on the foe, a master tamer with a fresh drake
    /// saw a dare of 232 against a dragon, held back and ran, and the dragon killed it.
    /// </summary>
    private static int AlliesOn(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var allies = 0;

        for (var i = 0; i < memory.AllyTargets.Count; i++)
        {
            if (memory.AllyTargets[i] == foe)
            {
                allies++;
            }
        }

        var pets = PetKeeper.PetsOut(character);

        for (var i = 0; i < pets.Count; i++)
        {
            if (TamerTacticsRules.PetReady(pets[i].Hits, pets[i].HitsMax))
            {
                allies++;
            }
        }

        return allies;
    }

    private static int IndexOf(Memory memory, Mobile mobile)
    {
        for (var i = 0; i < memory.Foes.Count; i++)
        {
            if (memory.Foes[i].Mobile == mobile)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The nearest hostile that rates as a threat, or -1.</summary>
    private static int NearestRated(Memory memory)
    {
        var nearest = -1;

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];

            if (!sighting.Harmless && sighting.Mobile.Alive &&
                (nearest < 0 || sighting.Distance < memory.Foes[nearest].Distance))
            {
                nearest = i;
            }
        }

        return nearest;
    }

    /// <summary>The nearest hostile still hitting this character, for when the current foe falls.</summary>
    private static Mobile NearestAttacker(SosariaCharacter character, Memory memory)
    {
        Mobile nearest = null;
        var best = int.MaxValue;

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];
            var mobile = sighting.Mobile;

            if (mobile is { Deleted: false, Alive: true } && mobile.Map == character.Map &&
                mobile.Combatant == character && sighting.Distance < best)
            {
                nearest = mobile;
                best = sighting.Distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Mid-fight: a party member takes up its tank's foe while its own is not on it
    /// (<see cref="PartyTacticsRules.TakesTankTarget"/>). Else a red or a gray attacking people
    /// takes the fight from a monster, one hitting this character from one hitting a friend,
    /// that from the rest, and an attacker much nearer than a foe that is not hitting back takes
    /// it too. A nearly beaten foe keeps the fight (<see cref="FocusRules.ShouldSwitch"/>), and so
    /// does the foe a melee fighter steps out of reach of to heal: it goes back in on that one.
    /// Rate-limited.
    /// </summary>
    private static Mobile ReconsiderTarget(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var now = Core.TickCount;

        if (now - memory.NextSwitchAt < 0 || HoldsTargetToHeal(character, memory, foe))
        {
            return foe;
        }

        var tankIndex = TankTargetIndex(character, memory, ownFoeOnSelf: foe.Combatant == character, out var tank);

        if (tankIndex >= 0 && memory.Foes[tankIndex].Mobile != foe)
        {
            var tankFoe = SwitchTo(character, memory, memory.Foes[tankIndex].Mobile, now);
            NoteJoinTank(character, tank, tankFoe);
            return tankFoe;
        }

        var current = IndexOf(memory, foe);
        var currentTier = current >= 0 ? memory.Foes[current].Tier : FocusRules.PlainTier;
        var currentDistance = current >= 0
            ? memory.Foes[current].Distance
            : NavMetric.Chebyshev(character.Location, foe.Location);
        var currentAttacks = foe.Combatant == character;
        var currentHits = Vitals.HitsFraction(foe);
        var best = -1;

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            var sighting = memory.Foes[i];

            if (i == current || !sighting.Acquirable ||
                !FocusRules.ShouldSwitch(
                    currentTier,
                    currentDistance,
                    currentAttacks,
                    currentHits,
                    sighting.Tier,
                    sighting.Distance,
                    sighting.AttacksSelf))
            {
                continue;
            }

            if (best < 0 || sighting.Distance < memory.Foes[best].Distance)
            {
                best = i;
            }
        }

        if (best < 0 || !character.InLOS(memory.Foes[best].Mobile))
        {
            return foe;
        }

        var next = SwitchTo(character, memory, memory.Foes[best].Mobile, now);

        if (Changed(memory, Decision.Switch) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} turns from {Old} to {New}", character.Name, foe.Name, next.Name);
        }

        return next;
    }

    /// <summary>Makes <paramref name="next"/> the foe, found and in reach until shown otherwise, and starts the switch cooldown.</summary>
    private static Mobile SwitchTo(SosariaCharacter character, Memory memory, Mobile next, long now)
    {
        memory.NextSwitchAt = now + FocusRules.SwitchCooldownMs;
        memory.FoeLostSince = 0;
        memory.Unreachable = false;
        character.Combatant = next;
        character.FocusMob = next;
        return next;
    }

    private static double NerveOf(SosariaCharacter character, Memory memory)
    {
        if (memory.Nerve > 0)
        {
            return memory.Nerve;
        }

        var drives = character.Persona?.ResolvedDrives();
        memory.Nerve = NerveRules.Of(
            DispositionRules.CourageOf(drives),
            drives?.Caution ?? PersonaDrives.NeutralValue,
            character.Build?.Role ?? CharacterRole.Worker,
            character.Build?.Veteran == true,
            CharacterPower.For(character),
            character.Serial.Value
        );
        return memory.Nerve;
    }
}
