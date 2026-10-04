using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// The world scan's reactions (<see cref="Tick"/>), in order: a player's party invite and
/// leave, party heals, the guard-line stand-down, guild life and guild war, Order against
/// Chaos with its town scuffles, war bands, sweeps and Den raids (<see cref="PartyRoads"/>), a red's
/// rules and the lawful answer to an outlaw, the retreat into a house base, red screams, the
/// gray watch, duels, a thief's crowd work, raising a ghost, pet care, ambient talk, and the
/// everyday scenes. Free. No model.
/// </summary>
public static class WorldPlay
{
    private static readonly ILogger logger = SosariaLog.For(typeof(WorldPlay));

    /// <summary>
    /// An outlaw whose attack dropped without a kill leaves that person be for a while. One
    /// red re-opened on the same character every few seconds for thirty-six minutes and never hit.
    /// </summary>
    public static readonly TimeSpan RetargetWait = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<Serial, (Serial Target, DateTime At)> _lastOutlawTarget = new();
    private static readonly Dictionary<Serial, (Serial Threat, DateTime At)> _lastRun = new();

    public static void Tick(SosariaCharacter character)
    {
        if (character == null || character.Deleted || character.IsGhost)
        {
            return;
        }

        GameParty.ConsiderPlayerInvite(character);
        GameParty.NoticePlayerLeave(character);
        TryHealParty(character);
        ConsiderGuardLine(character);
        FactionWar.ConsiderDisengage(character);
        TownScuffles.Consider(character);
        ConsiderGuildLife(character);
        ConsiderGuildWar(character);
        ConsiderAlignmentFight(character);
        PartyRoads.Consider(character);
        ConsiderOutlaw(character);
        ConsiderLawful(character);
        HouseBases.ConsiderRetreat(character);
        RedAlarm.Consider(character);
        GrayWatch.Consider(character);
        Duels.Consider(character);
        ThiefWork.Consider(character);
        ResurrectOffer.Consider(character);
        PetKeeper.Consider(character);
        AmbientTalk.Consider(character);
        Scenes.ConsiderIdle(character);
    }

    /// <summary>
    /// Skills a reaction may cut short: standing about, not an errand. A thief's lift or a
    /// duel starts only from one of these, so a bank trip or a hunt is never broken off. The
    /// bank crowd is people standing about: the era's duels and pickpockets started there.
    /// </summary>
    private static readonly HashSet<string> IdleSkillKinds = new(StringComparer.Ordinal)
    {
        SkillKinds.IdleWander, SkillKinds.Loiter, SkillKinds.Rest, SkillKinds.Tavern, SkillKinds.Visit, SkillKinds.BankCrowd
    };

    /// <summary>
    /// Felucca rules: a person may harm another person. On a facet with Trammel's rules the
    /// engine refuses a duel blow, an Order-against-Chaos blow and a lift from a person.
    /// </summary>
    public static bool OpenPvp(Map map) =>
        map != null && map != Map.Internal && (map.Rules & MapRules.HarmfulRestrictions) == 0;

    public static bool IsIdle(SosariaCharacter character) =>
        character.Routine is not { NeedsNext: false } routine || IdleSkillKinds.Contains(routine.CurrentSkill?.Name ?? string.Empty);

    /// <summary>A reaction takes over the routine with one skill; the scorer picks again after it.</summary>
    public static void StartWork(SosariaCharacter character, Skills.Skill skill)
    {
        if (character.Routine == null)
        {
            character.AttachRoutine(new Routine([skill]));
        }
        else
        {
            character.Routine.Replace([skill]);
        }
    }

    /// <summary>
    /// A person fight that reaches the guard line ends there. War rallies and
    /// outlaw hunts refuse to start under the guards, but a chase crosses the
    /// line — and a spilled-in fight is how a dozen people died at the bank.
    /// A guild war also ends at a bank, a healer, a moongate or a shrine: players took that
    /// fight to the roads. An agreed duel, Order against Chaos (lawful blows the guards stay
    /// out of, fought at the banks as on the 1999 shards) and a town scuffle
    /// (<see cref="TownScuffles"/>, which keeps its own ground) go on. Only fights between characters stand down: a real player's
    /// swing is the engine's problem. Both sides stood down at the line are left alone for the
    /// grace (<see cref="SosariaCharacter.StoppedAtLineLately"/>), and the stand-down is said once
    /// in it. A red under the guards is the exception: it breaks off
    /// any fight and runs for open ground (<see cref="RedBreaksOffUnderGuards"/>).
    /// </summary>
    public static void ConsiderGuardLine(SosariaCharacter character)
    {
        if (RedBreaksOffUnderGuards(character))
        {
            return;
        }

        if (character.Combatant is not SosariaCharacter combatant ||
            combatant.Deleted || !combatant.Alive || combatant.IsGhost ||
            Duels.AreFighting(character, combatant) || EngineGuilds.Opposed(character, combatant) ||
            TownScuffles.AreFighting(character, combatant))
        {
            return;
        }

        var feud = FactionWar.AreFoes(character, combatant);

        if (!GuardLineRules.ShouldStandDown(
                feud ? FactionWar.InSafePlace(character) : SosariaCharacter.UnderGuards(character),
                feud ? FactionWar.InSafePlace(combatant) : SosariaCharacter.UnderGuards(combatant)
            ))
        {
            return;
        }

        var now = Core.Now;

        if (GuardLineRules.SpeaksAtLine(character.LastLineStopAt, now))
        {
            Talk.Say(character, TalkCategory.GuardStandDown, new TalkSlots { Name = combatant.Name });
        }

        character.StandDown();
        character.LastLineStopAt = now;
        combatant.LastLineStopAt = now;

        // The fight ends for both sides at once. Leaving the foe's aim armed
        // lets its AI re-acquire this character on the next think, and the
        // pair trades stand-down lines every think forever.
        if (combatant.Combatant == character)
        {
            combatant.StandDown();
        }

        Log($"{character.Name} broke off at the guard line");
    }

    /// <summary>
    /// A living red under the guards drops the fight, whoever the foe is, and runs as a decided
    /// run (<see cref="RunFrom"/>): the blows on the way out do not turn it round, and the run
    /// leans for the nearest open ground (<see cref="EscapeRoute.WayOutOfGuards"/>). A fight with
    /// a real player held the red in town, and the walk out never started. True when it broke off.
    /// </summary>
    private static bool RedBreaksOffUnderGuards(SosariaCharacter character)
    {
        var foe = character.Combatant;

        if (!GuardLineRules.RedBreaksOff(
                PkRules.IsRed(character.Kills),
                SosariaCharacter.UnderGuards(character),
                foe is { Deleted: false, Alive: true } && foe.Map == character.Map,
                character.Motor.Action == CharacterAction.Flee
            ))
        {
            return false;
        }

        Log($"{character.Name} is red under the guards and breaks off from {foe.Name}");
        RunFrom(character, foe, RedRunReason.Guards);
        return true;
    }

    /// <summary>
    /// A member of Order who turns red leaves Order at once for its roll without Order (see
    /// <see cref="GuildCatalog.TakesMember"/>): the operator never wants to see a red Order player.
    /// A player's recruit stays where the player put it. True when it left.
    /// </summary>
    private static bool LeavesOrder(SosariaCharacter character)
    {
        var murderer = PkRules.IsRed(character.Kills);

        if (GuildCatalog.TakesMember(GuildCatalog.AlignmentOf(character.GuildIndex), murderer) ||
            GuildRecruits.Recruited(character))
        {
            return false;
        }

        var order = GuildCatalog.All[character.GuildIndex].Name;
        EngineGuilds.Resettle(character, murderer);
        Log($"{character.Name} is red and leaves {order}");
        return true;
    }

    public static void ConsiderGuildLife(SosariaCharacter character)
    {
        if (GuildRecruits.InPlayerGuild(character))
        {
            return;
        }

        if (LeavesOrder(character))
        {
            return;
        }

        if (character.GuildIndex != GuildCatalog.None || character.Map == null || character.Map == Map.Internal)
        {
            return;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, MeetingRules.GreetingRange))
        {
            if (mobile is not SosariaCharacter other || other == character || !GuildCatalog.TakesFriends(other.GuildIndex))
            {
                continue;
            }

            if (!BondRules.IsWarm(Recall.ScoreOf(MemoryStore.Shared, character, other)))
            {
                continue;
            }

            character.GuildIndex = other.GuildIndex;
            EngineGuilds.Join(character);
            Talk.Say(character, TalkCategory.GuildJoin, new TalkSlots { Guild = GuildCatalog.All[other.GuildIndex].Name, Name = other.Name });
            Log($"{character.Name} joined {GuildCatalog.All[other.GuildIndex].Name}");
            return;
        }
    }

    /// <summary>
    /// A rally is a shout and a charge, not a chant. Two enemies who could not reach each
    /// other shouted "To arms!" at each other every second for four minutes.
    /// </summary>
    public static readonly TimeSpan RallyCooldown = TimeSpan.FromMinutes(2);
    private static readonly Dictionary<Serial, DateTime> LastRally = new();

    /// <summary>This person's own rally has cooled down.</summary>
    public static bool RallyReady(Serial self, DateTime now) =>
        TimeRules.Rested(LastRally.GetValueOrDefault(self), now, RallyCooldown);

    /// <summary>
    /// Two enemies who fought leave each other be for a while. The same pairs met forty times
    /// in two hours outside the same town gate. Kept for half an hour, Order and Chaos who had
    /// fought once stood side by side at the Yew moongate: 17 of 27 meetings of one run held off.
    /// </summary>
    public static readonly TimeSpan PairRest = TimeSpan.FromMinutes(10);
    private static readonly Dictionary<(Serial, Serial), DateTime> LastPairFight = new();

    public static (Serial, Serial) PairKey(Serial first, Serial second) =>
        first.Value <= second.Value ? (first, second) : (second, first);

    /// <summary>These two have not fought inside the pair rest.</summary>
    public static bool PairReady(Serial first, Serial second, DateTime now) =>
        TimeRules.Rested(LastPairFight.GetValueOrDefault(PairKey(first, second)), now, PairRest);

    /// <summary>Marks a fight: this person's rally and the pair's rest both start now.</summary>
    public static void NoteFight(SosariaCharacter character, Mobile foe)
    {
        var now = Core.Now;
        LastRally[character.Serial] = now;
        LastPairFight[PairKey(character.Serial, foe.Serial)] = now;
    }

    /// <summary>
    /// A kill ends the matter for a while. The graveyard healer raised a fallen enemy at
    /// once, and he was cut down again, two hundred times in ten minutes.
    /// </summary>
    public static readonly TimeSpan DeathGrace = TimeSpan.FromMinutes(10);

    public static bool OutOfDeathGrace(DateTime lastDeath, DateTime now) => TimeRules.Rested(lastDeath, now, DeathGrace);

    /// <summary>
    /// True while either side died inside <see cref="DeathGrace"/>: nobody freshly dead is drawn
    /// on, and nobody freshly dead draws. The red pack's own pick skipped it, and blues raised at
    /// the Trinsic healer were cut down again by the same reds 169 times in one hour.
    /// </summary>
    public static bool InDeathGrace(SosariaCharacter self, Mobile other, DateTime now) =>
        !OutOfDeathGrace(self.LastDeathAt, now) ||
        other is SosariaCharacter fallen && !OutOfDeathGrace(fallen.LastDeathAt, now);

    /// <summary>
    /// Guild enemies fight out in the open, as Order and Chaos do: never under the guards or at
    /// a bank, a healer, a moongate or a shrine, at most four of a guild in one skirmish, and not
    /// straight back at the one who killed them. A runner starts nothing, and nobody draws into a
    /// fight it would run from at once or on the pack it ran from lately (see
    /// <see cref="FactionWar.MayEngage"/>). A draw brings in the free fighters of both guilds
    /// near, up to four a side (<see cref="FactionWar.DraftGuild"/>), so the war is fought in
    /// groups, and the odds the drawer weighed are the ones it meets.
    /// </summary>
    public static void ConsiderGuildWar(SosariaCharacter character)
    {
        if (character.GuildIndex < 0 || character.Map == null || character.Map == Map.Internal ||
            character.Combatant != null || character.CheckFlee() || !RallyReady(character.Serial, Core.Now) ||
            !OutOfDeathGrace(character.LastDeathAt, Core.Now))
        {
            return;
        }

        var ownGuild = FactionWar.GuildSide(character.Guild);
        bool? selfSafe = null;

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, PartyInviteRules.InviteRange))
        {
            if (mobile is not SosariaCharacter other || other == character || other.IsGhost ||
                SosariaSettings.GuildWars?.AtWar(character.GuildIndex, other.GuildIndex, Core.Now) != true ||
                Party.SharesParty(character.CharacterId, other.CharacterId) ||
                GameParty.Of(character) is { } engineParty && engineParty == GameParty.Of(other))
            {
                continue;
            }

            selfSafe ??= FactionWar.InSafePlace(character);

            if (selfSafe == true)
            {
                return;
            }

            if (!FactionWar.MayEngage(character, other, ownGuild))
            {
                continue;
            }

            NoteFight(character, other);
            Talk.Say(character, TalkCategory.GuildWarRally, new TalkSlots { Guild = GuildCatalog.All[other.GuildIndex].Tag, Foe = other.Name });
            character.JoinAgainst(other);
            Log($"{character.Name} attacks guild enemy {other.Name}");
            FactionWar.DraftGuild(character, other);
            return;
        }
    }

    /// <summary>
    /// Order and Chaos fight on sight anywhere, as the operator wants: in the wild, in town and
    /// at the bank, where the guards stay out of the lawful blows (a shard-wide gap once let one
    /// chance skirmish open in four to twelve minutes, and the sides met in the wild with no
    /// fight). A meeting under a town's guards may open a small town scuffle first
    /// (<see cref="TownScuffles.TryOpen"/>). A meeting that may not come to blows gets words
    /// instead (<see cref="FactionWar.TryTaunt"/>). A draw brings in the faction-mates of both
    /// sides, up to four a side. The rally rest, the death grace, the revenge rest and the pair
    /// rest hold, and a runner starts nothing.
    /// </summary>
    public static void ConsiderAlignmentFight(SosariaCharacter character)
    {
        var side = EngineGuilds.AlignmentOf(character);

        if (side == GuildType.Regular || !OpenPvp(character.Map) || character.Combatant != null || character.CheckFlee() ||
            !RallyReady(character.Serial, Core.Now) || !OutOfDeathGrace(character.LastDeathAt, Core.Now))
        {
            return;
        }

        var ownSide = FactionWar.AlignmentSide(side);

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, FactionRules.SightRange))
        {
            if (mobile is not PlayerMobile { Alive: true, Hidden: false } other || other == character ||
                !EngineGuilds.Opposed(character, other) || !character.CanSee(other))
            {
                continue;
            }

            if (TownScuffles.TryOpen(character, other))
            {
                return;
            }

            if (!FactionWar.MayEngage(character, other, ownSide))
            {
                if (FactionWar.TryTaunt(character, other, side))
                {
                    return;
                }

                continue;
            }

            NoteFight(character, other);
            Talk.Say(character, AlignmentFightCategory(side), new TalkSlots { Foe = other.Name });
            character.JoinAgainst(other);
            Log($"{character.Name} ({side}) draws on {other.Name}");
            FactionWar.Draft(character, other);
            return;
        }
    }

    public static string AlignmentFightCategory(GuildType side) =>
        side == GuildType.Chaos ? TalkCategory.ChaosBattle : TalkCategory.OrderBattle;

    public static void TryHealParty(SosariaCharacter healer)
    {
        if (healer?.Build?.CanHeal != true || healer.Map == null || healer.Map == Map.Internal)
        {
            return;
        }

        var party = GameParty.Of(healer);

        if (party == null)
        {
            return;
        }

        Mobile lowest = null;
        var lowestHits = Vitals.FullHits;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var member = party.Members[i].Mobile;

            if (member == null || member.Deleted || !member.Alive || member == healer)
            {
                continue;
            }

            // A scratch is left to heal on its own; a bandage reaches one tile before AOS.
            if (member.HitsMax <= 0 || !Skills.HealRules.NeedsHeal(member.Hits, member.HitsMax, member.Poisoned) ||
                member.Map != healer.Map || !Skills.HealRules.InReach(healer.Location, member.Location))
            {
                continue;
            }

            var fraction = Vitals.HitsFraction(member);

            if (fraction < lowestHits)
            {
                lowestHits = fraction;
                lowest = member;
            }
        }

        if (lowest != null)
        {
            GameParty.TryHeal(healer, lowest);
        }
    }

    /// <summary>
    /// A red's rules, the 1999 way: run from a stronger side, a mob or a bad wound; bring the gang
    /// (and any other red near, half the time) in on a victim; leave any guarded place; loot the
    /// bodies it made; and pick the next mark only with a pack at its back (a lone red on the
    /// roads prowls, a dungeon red holds its hall alone) and no mob of blues about: near, hurt,
    /// isolated and weaker. The gang's power counts, so a pair takes on what one would not. A mark
    /// it would run from at once is passed over, and so is one <see cref="PassesOverMark"/> names. Leaving
    /// the guards comes before every other rule, a fight with a real player included: the fight
    /// held the red in town, and the walk out never started.
    /// </summary>
    public static void ConsiderOutlaw(SosariaCharacter character)
    {
        if (!OutlawRulesHold(character) || RedGang.LeaveGuards(character))
        {
            return;
        }

        var gangPower = RedGang.Power(character);
        var combatant = character.Combatant;
        // A live monster that is not a human or a character does not hold this pick.
        var fightingHuman = combatant != null &&
            OutlawRules.IsLiveHumanCombatant(
                combatant.Deleted,
                combatant.Alive,
                People.IsHuman(combatant),
                combatant is SosariaCharacter
            );

        var (packSize, blueCrowd) = RedGang.Surroundings(character);

        // A red standing at bay (cornered, or the blows barely hurt) fights on until the bay runs out.
        // In a fight the mob is the blues fighting the reds, not the crowd passing by.
        if (fightingHuman && !CombatBrain.HoldsAtBay(character) &&
            RedRunWhy(character, combatant, gangPower, packSize, RedGang.FightersAgainstReds(character)) is var why &&
            why != RedRunReason.None)
        {
            RunFrom(character, combatant, why);
            return;
        }

        if (fightingHuman)
        {
            RedGang.Call(character, combatant, strays: false);
            return;
        }

        if (RedGang.LootFreshKill(character) || !BankPlaza.MayStartPersonFight(OnBankPlaza(character)))
        {
            return;
        }

        RedGang.RallyToAmbush(character);
        var inDungeon = character.Region?.IsPartOf<DungeonRegion>() == true;

        // A red on the run starts nothing: it picked a new victim the second after it ran.
        if (character.CheckFlee())
        {
            return;
        }

        // Alone on the roads, or before a mob, a red prowls and starts nothing.
        if (packSize < OutlawRules.MinPack && !inDungeon || OutlawRules.IsMob(blueCrowd, packSize))
        {
            return;
        }

        Mobile chosen = null;
        var chosenScore = double.MinValue;
        var courage = DispositionRules.CourageOf(character.Persona?.ResolvedDrives());
        var ownParty = GameParty.Of(character);

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, RedGangRules.VictimScanTiles))
        {
            if (!GameParty.IsApproachable(character, mobile) || ownParty?.Contains(mobile) == true ||
                !BankPlaza.MayStartPersonFight(OnBankPlaza(mobile, character.HomeFacet)))
            {
                continue;
            }

            if (!People.IsHuman(mobile) && mobile is not SosariaCharacter { IsGhost: false, IsPk: false })
            {
                continue;
            }

            if (PassesOverMark(character, mobile))
            {
                continue;
            }

            if (_lastOutlawTarget.TryGetValue(character.Serial, out var lastTarget) &&
                !SameTargetDue(lastTarget.Target, lastTarget.At, mobile.Serial, Core.Now, RetargetWait))
            {
                continue;
            }

            if (!OutlawRules.MayAttack(
                    character.Disposition,
                    felucca: true,
                    guards: SosariaCharacter.UnderGuards(character) || SosariaCharacter.UnderGuards(mobile),
                    packSize,
                    blueCrowd,
                    inDungeon,
                    selfPower: gangPower,
                    // The victim's side, as the run test reads it: a red that drew on a tamer
                    // or a party member alone ran from "a stronger side" the second after.
                    targetPower: RedGang.SidePower(mobile),
                    courage: courage
                ))
            {
                continue;
            }

            if (!PkRules.MayAttack(
                    character.IsPk,
                    SosariaCharacter.UnderGuards(character),
                    SosariaCharacter.UnderGuards(mobile),
                    PkRules.IsRed(mobile.Kills),
                    PkRules.InBuccaneersDen(character.X, character.Y) ||
                    PkRules.InBuccaneersDen(mobile.X, mobile.Y)
                ))
            {
                continue;
            }

            if (GuardCall.AtGuardLine(mobile.Location, mobile.Map) ||
                RedRuns(character, mobile, gangPower, packSize, blueCrowd) ||
                CombatBrain.WouldRunFrom(character, mobile, IsBluePerson))
            {
                continue;
            }

            var score = OutlawRules.VictimScore(
                NavMetric.Chebyshev(character.Location, mobile.Location),
                Vitals.HitsFraction(mobile),
                RedGang.PeopleNear(character, mobile.Location, OutlawRules.AloneRange, mobile),
                mobile is SosariaCharacter victim ? character.PersonProfile.Tier - victim.PersonProfile.Tier : 0
            );

            if (score > chosenScore)
            {
                chosenScore = score;
                chosen = mobile;
            }
        }

        if (chosen == null)
        {
            return;
        }

        _lastOutlawTarget[character.Serial] = (chosen.Serial, Core.Now);
        Talk.Say(character, TalkCategory.PkAttack, new TalkSlots { Target = chosen.Name });
        character.Combatant = chosen;
        character.Warmode = true;
        character.Motor.Action = CharacterAction.Combat;
        RedGang.Call(character, chosen, strays: true);
        Log($"{character.Name} attacks {chosen.Name} with a pack of {packSize}");
    }

    /// <summary>A red's own rules hold: a red of Felucca, out in the world.</summary>
    private static bool OutlawRulesHold(SosariaCharacter character) =>
        character.IsPk && character.HomeFacet == FacetNames.Felucca &&
        character.Map != null && character.Map != Map.Internal;

    /// <summary>A red's run test (<see cref="OutlawRules.RunReason"/>) on a fight with <paramref name="foe"/> and its side.</summary>
    private static bool RedRuns(SosariaCharacter red, Mobile foe, int gangPower, int packSize, int blueCrowd) =>
        RedRunWhy(red, foe, gangPower, packSize, blueCrowd) != RedRunReason.None;

    private static RedRunReason RedRunWhy(SosariaCharacter red, Mobile foe, int gangPower, int packSize, int blueCrowd) =>
        OutlawRules.RunReason(gangPower, RedGang.SidePower(foe), Vitals.HitsFraction(red), blueCrowd, packSize, Vitals.HitsFraction(foe));

    /// <summary>
    /// The run tests put before the first blow: a red's own where its rules hold, and the combat
    /// brain's for everyone (<see cref="CombatBrain.WouldRunFrom"/>). A fight either would leave
    /// at once is not started: 63 guild war draws in half an hour ended "too many" the same second.
    /// </summary>
    public static bool WouldRunFrom(SosariaCharacter character, Mobile foe, Func<Mobile, bool> onFoeSide)
    {
        if (OutlawRulesHold(character))
        {
            var (packSize, blueCrowd) = RedGang.Surroundings(character);

            if (RedRuns(character, foe, RedGang.Power(character), packSize, blueCrowd))
            {
                return true;
            }
        }

        return CombatBrain.WouldRunFrom(character, foe, onFoeSide);
    }

    /// <summary>
    /// A mark a red passes over for now: one of the side it ran from or stood down from lately,
    /// one inside the death grace (<see cref="InDeathGrace"/>), or one that just got away under
    /// the guards (<see cref="FactionWar.StoppedAtLineLately"/>). Bastian Garrow at the Skara
    /// Brae moongate drew on Olaf Mossbank four times in ten minutes, and Olaf ran in each time.
    /// </summary>
    public static bool PassesOverMark(SosariaCharacter red, Mobile mark) =>
        LeavesBeSide(red, mark) || InDeathGrace(red, mark, Core.Now) || FactionWar.StoppedAtLineLately(mark);

    /// <summary>
    /// A foe this character leaves be for now: the one it stood down from at the guard line, and
    /// the pack it ran from lately (<see cref="RanFromSideLately"/>).
    /// </summary>
    public static bool LeavesBeSide(SosariaCharacter character, Mobile foe) =>
        character.StoodDownFrom(foe) || RanFromSideLately(character, foe);

    /// <summary>
    /// Inside the run grace, the person this character ran from and the pack it stood with. The
    /// grace held one name only: Laurel Mason ran from one red and drew on the next of the same
    /// pack, 334 times in twenty minutes.
    /// </summary>
    public static bool RanFromSideLately(SosariaCharacter character, Mobile foe) =>
        character.RanFromLately(foe) || RanFromGroup(character, World.FindMobile(character.LastRanFrom), foe);

    /// <summary>
    /// True when <paramref name="foe"/> is of the pack of <paramref name="ranFrom"/>, run from
    /// inside the grace: its party anywhere, and its guild or its fellow reds only within a
    /// skirmish's reach of it (<see cref="FactionRules.DraftRange"/>). A grace over the whole
    /// guild left an Order or Chaos guild of dozens alone for two minutes after one run from one
    /// member, and every red on the facet after one run from one red.
    /// </summary>
    public static bool RanFromGroup(SosariaCharacter character, Mobile ranFrom, Mobile foe) =>
        character.RanFromLately(ranFrom) && foe != null &&
        (SameParty(ranFrom, foe) || SameGroup(ranFrom, foe) && WithinSkirmish(ranFrom, foe));

    /// <summary>One group: one guild, one party, or two reds, who hunt as a pack.</summary>
    public static bool SameGroup(Mobile first, Mobile second) =>
        first != null && second != null &&
        (first.Guild != null && first.Guild == second.Guild ||
         SameParty(first, second) ||
         PkRules.IsRed(first.Kills) && PkRules.IsRed(second.Kills));

    /// <summary>
    /// Friends in a fight: one group (<see cref="SameGroup"/>), or one side of Order and Chaos.
    /// The run test counts them beside a character (<see cref="CombatBrain.WouldRunFrom"/>).
    /// </summary>
    public static bool Allied(Mobile first, Mobile second) =>
        SameGroup(first, second) ||
        EngineGuilds.AlignmentOf(first) is var side && side != GuildType.Regular && side == EngineGuilds.AlignmentOf(second);

    /// <summary>One party, the engine's or the plugin's.</summary>
    private static bool SameParty(Mobile first, Mobile second) =>
        GameParty.Of(first) is { } party && party == GameParty.Of(second) ||
        first is SosariaCharacter one && second is SosariaCharacter other && Party.SharesParty(one.CharacterId, other.CharacterId);

    /// <summary>Two people close enough to stand in one skirmish.</summary>
    private static bool WithinSkirmish(Mobile first, Mobile second) =>
        first.Map == second.Map && NavMetric.Chebyshev(first.Location, second.Location) <= FactionRules.DraftRange;

    /// <summary>A blue person: the side a red's mark stands on.</summary>
    private static bool IsBluePerson(Mobile mobile) => !PkRules.IsRed(mobile.Kills);

    /// <summary>
    /// One run from one threat is one decision, and a decided one (<see cref="CombatBrain.RunDecided"/>):
    /// blows that barely hurt do not turn the red round, the run is clear only with the threat
    /// well off, and the side run from is left be after. One red said "later mike" and ran 367
    /// times in an hour. The line and the log come once an episode; inside it the red keeps
    /// running without a word.
    /// </summary>
    private static void RunFrom(SosariaCharacter character, Mobile threat, RedRunReason why)
    {
        var now = Core.Now;
        _lastRun.TryGetValue(character.Serial, out var last);

        if (FleeRules.NewEpisode(last.Threat.Value, last.At, threat.Serial.Value, now))
        {
            _lastRun[character.Serial] = (threat.Serial, now);
            Talk.Say(character, TalkCategory.PkRan, new TalkSlots { Name = threat.Name });
            Log($"{character.Name} ran from {threat.Name} ({OutlawRules.RunWords(why)})");
        }

        CombatBrain.RunDecided(
            character,
            threat,
            TimeSpan.FromSeconds(Utility.Random(OutlawRules.RunMinSeconds, OutlawRules.RunSpanSeconds))
        );
    }

    /// <summary>
    /// Blues against reds. A lawful fighter warns the victim. Under the guards the answer is
    /// a shout for them. Out of their reach, adventurers and dungeon crews draw on a red, and
    /// on a gray hurting a person, when their side is strong enough or a friend is the one hurt:
    /// never while running, into a fight they would run from at once, on the side they ran
    /// from lately, or inside the death grace (<see cref="InDeathGrace"/>). Laurel Mason ran from a red pack and drew on it again every second. In
    /// Buccaneer's Den a blue draws only on a Den raid or for a friend
    /// (<see cref="SosariaCharacter.IsOutlawTarget"/>, <see cref="MayStartFightInDen"/>).
    /// </summary>
    public static void ConsiderLawful(SosariaCharacter character)
    {
        if (character.IsPk || character.Map == null || character.Map == Map.Internal)
        {
            return;
        }

        var hunter = LawfulRules.IsHunter(
            character.Build?.Role == CharacterRole.Fighter,
            character.Disposition,
            GameParty.InParty(character),
            RoutineDriver.IsHunting(character)
        );

        if (!hunter && character.Disposition != DispositionKind.Lawful)
        {
            return;
        }

        const int range = RedGangRules.OutlawScanTiles;
        var outlaw = NearestOutlaw(character, range, skipRanFrom: false);

        // A red run from, or one of its pack, followed into town still gets the guards called on
        // it; out of their reach it is left be, and the next outlaw in sight is the one answered.
        if (outlaw != null && RanFromSideLately(character, outlaw) &&
            !SosariaCharacter.UnderGuards(character) && !SosariaCharacter.UnderGuards(outlaw))
        {
            outlaw = NearestOutlaw(character, range, skipRanFrom: true);
        }

        if (outlaw == null)
        {
            return;
        }

        if (SosariaCharacter.UnderGuards(character) || SosariaCharacter.UnderGuards(outlaw))
        {
            GuardCall.TryCall(character, outlaw);
            return;
        }

        Warn(character, outlaw, range);

        if (!hunter || character.Combatant is { Deleted: false, Alive: true } || character.CheckFlee() ||
            !character.IsOutlawTarget(outlaw) || InDeathGrace(character, outlaw, Core.Now) ||
            GuardCall.AtGuardLine(outlaw.Location, outlaw.Map))
        {
            return;
        }

        var sidePower = CharacterPower.For(character) + Party.AlliesPower(character);

        if (!LawfulRules.ShouldHunt(false, sidePower, CharacterPower.For(outlaw), OutlawOnFriend(character, outlaw)) ||
            WouldRunFrom(character, outlaw, IsOutlaw))
        {
            return;
        }

        GameParty.Chat(character, LawfulRules.HelpLine());
        character.JoinAgainst(outlaw);
        Log($"{character.Name} attacks outlaw {outlaw.Name}");
    }

    /// <summary>
    /// The nearest outlaw person in sight, past the one this character fights already and the
    /// one it stood down from at the guard line; past the side it ran from too when asked.
    /// </summary>
    private static Mobile NearestOutlaw(SosariaCharacter character, int range, bool skipRanFrom)
    {
        Mobile outlaw = null;
        var outlawDistance = int.MaxValue;

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, range))
        {
            // Only people are outlaws here. A monster flagged AlwaysMurderer is prey, not a criminal.
            if (mobile is not PlayerMobile { Alive: true, Hidden: false } person || person == character ||
                person == character.Combatant || character.StoodDownFrom(person) ||
                skipRanFrom && RanFromSideLately(character, person) ||
                !IsOutlaw(person) ||
                !character.CanSee(person))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(character.Location, person.Location);

            if (distance < outlawDistance)
            {
                outlawDistance = distance;
                outlaw = person;
            }
        }

        return outlaw;
    }

    /// <summary>A red, or a gray hurting a person.</summary>
    public static bool IsOutlaw(Mobile person) =>
        LawfulRules.IsOutlaw(PkRules.IsRed(person.Kills), person.Criminal, People.IsLivingPlayer(person.Combatant));

    /// <summary>
    /// A fight this character would start on <paramref name="foe"/>, held to the Den rule
    /// (<see cref="LawfulRules.MayDrawInDen"/>): a blue in Buccaneer's Den, or drawing on a
    /// person there, starts one only on a Den raid, a posse or a PK hunter's run, or on an
    /// outlaw hurting its friend
    /// (<see cref="OutlawOnFriend"/>). A red there starts no fight on another red
    /// (<see cref="LawfulRules.RedMayDrawInDen"/>), and a monster is prey anywhere. Every fight a
    /// person picks on its own asks it: the lawful draw and the combat brain's foes
    /// (<see cref="SosariaCharacter.IsOutlawTarget"/>), a guild war foe (and so the help a guild
    /// mate calls, <see cref="Assist"/>), the gray watch, Order against Chaos, the draft,
    /// and a follower taking its leader's foe.
    /// </summary>
    public static bool MayStartFightInDen(SosariaCharacter self, Mobile foe)
    {
        if (foe is not PlayerMobile)
        {
            return true;
        }

        var inDen = PkRules.InBuccaneersDen(self.X, self.Y) || PkRules.InBuccaneersDen(foe.X, foe.Y);

        return PkRules.IsRed(self.Kills)
            ? LawfulRules.RedMayDrawInDen(inDen, PkRules.IsRed(foe.Kills))
            : LawfulRules.MayDrawInDen(inDen, PartyRoads.RidesAgainstDen(self), OutlawOnFriend(self, foe));
    }

    /// <summary>
    /// <paramref name="foe"/> is an outlaw (a red, or a gray hurting a person) whose fight is on
    /// this character or its friend (<see cref="AreFriends"/>).
    /// </summary>
    public static bool OutlawOnFriend(SosariaCharacter self, Mobile foe) =>
        IsOutlaw(foe) && foe.Combatant is { } hurt && AreFriends(self, hurt);

    /// <summary><paramref name="other"/> is this character, its party, its planned party, or its guild.</summary>
    public static bool AreFriends(SosariaCharacter self, Mobile other) =>
        other == self || GameParty.Of(self)?.Contains(other) == true ||
        Party.SharesParty(self.CharacterId, (other as SosariaCharacter)?.CharacterId) ||
        self.Guild != null && other.Guild == self.Guild;

    // A lawful watcher warns someone the outlaw may kill, through the shared warning gate
    // (see WarningGate): the same warning once in a while, and one line per speaker at a time.
    private static void Warn(SosariaCharacter character, Mobile outlaw, int range)
    {
        var now = Core.Now;

        if (character.Disposition != DispositionKind.Lawful || !WarningGate.Shared.SpeakerReady(character.Serial, now))
        {
            return;
        }

        var victim = outlaw.Combatant is { Deleted: false, Alive: true } mark && IsWarnVictim(mark) && People.Perceives(character, mark)
            ? mark
            : null;

        if (victim == null)
        {
            foreach (var other in character.Map.GetMobilesInRange(character.Location, range))
            {
                if (other != character && other != outlaw && !other.Deleted && other.Alive &&
                    IsWarnVictim(other) && character.CanSee(other))
                {
                    victim = other;
                    break;
                }
            }
        }

        var victimName = victim?.Name ?? "friend";

        // A sighting is told once: the first watcher to pass the gates warns, the rest of
        // the crowd does not chant the same line at the same body. The told-key is noted
        // only when a line goes out, so watchers on speaker-rest do not spend the one tell.
        var map = character.Map?.MapID ?? 0;

        if (victim != character &&
            LawfulRules.ShouldWarn(character.Disposition, true, !string.IsNullOrWhiteSpace(victimName)) &&
            !WarningGate.Shared.ToldLately(outlaw.Serial, map, character.X, character.Y, now) &&
            WarningGate.Shared.TryWarn(character.Serial, victim?.Serial ?? Serial.Zero, outlaw.Serial, now))
        {
            WarningGate.Shared.NoteTold(outlaw.Serial, map, character.X, character.Y, now);
            Talk.Say(character, TalkCategory.RedWarn, new TalkSlots { Name = victimName }, varyIfHeard: true);
            Log($"{character.Name} warned {victimName} about {outlaw.Name}");
        }
    }

    /// <summary>
    /// A warning is for a person the outlaw may kill. A red fighting a water elemental put
    /// "a water elemental, red on you." in the mouths of everyone nearby.
    /// </summary>
    public static bool IsWarnVictim(Mobile mobile) =>
        People.IsHuman(mobile) || mobile is SosariaCharacter { IsGhost: false, IsPk: false };

    /// <summary>True for someone new, or for the same one once the wait has passed.</summary>
    public static bool SameTargetDue(Serial last, DateTime lastAt, Serial next, DateTime now, TimeSpan wait) =>
        last != next || now - lastAt >= wait;

    private static bool OnBankPlaza(SosariaCharacter character) =>
        character != null && OnBankPlaza(character, character.HomeFacet);

    private static bool OnBankPlaza(Mobile mobile, string facet) =>
        mobile != null &&
        BankPlaza.Contains(
            mobile.Location,
            BankPlaza.BankFor(NavWorld.DestinationsFor(facet), mobile.Location)
        );

    internal static void Log(string line)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", line);
        }
    }
}
