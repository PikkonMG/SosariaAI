using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>One agreed duel between two characters.</summary>
public sealed class Duel
{
    public Duel(Serial challenger, Serial partner, Point3D ground, DateTime now)
    {
        Challenger = challenger;
        Partner = partner;
        Ground = ground;
        Stage = DuelStage.Walking;
        StageStarted = now;
    }

    public Serial Challenger { get; }

    public Serial Partner { get; }

    /// <summary>The middle of the duel ground, ten tiles from the banker.</summary>
    public Point3D Ground { get; }

    public DuelStage Stage { get; private set; }

    public DateTime StageStarted { get; private set; }

    /// <summary>Who dropped to the floor first, or zero when the duel was called off.</summary>
    public Serial Loser { get; private set; }

    public bool ChallengerReady { get; set; }

    public bool PartnerReady { get; set; }

    public bool Involves(Mobile mobile) => mobile != null && (mobile.Serial == Challenger || mobile.Serial == Partner);

    public Serial OtherOf(Serial self) => self == Challenger ? Partner : Challenger;

    public void BeginFight(DateTime now)
    {
        Stage = DuelStage.Fighting;
        StageStarted = now;
    }

    public void Finish(Serial loser, DateTime now)
    {
        Stage = DuelStage.Over;
        StageStarted = now;
        Loser = loser;
    }
}

/// <summary>
/// The duels going on now. In memory only: a restart calls every duel off. The rest after a
/// duel is each duelist's own <see cref="RuleClock.DuelFought"/> clock, which the save keeps.
/// </summary>
public static class Duels
{
    /// <summary>The eight ways off a bank, tried in turn from a seed so duels spread round it.</summary>
    private static readonly (int X, int Y)[] Ways =
    [
        (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)
    ];

    private static readonly Dictionary<Serial, Duel> _byDuelist = new();
    private static DateTime _nextStart;

    public static Duel Find(Mobile mobile) =>
        mobile != null && _byDuelist.TryGetValue(mobile.Serial, out var duel) ? duel : null;

    /// <summary>True while these two are trading blows in an agreed duel: the blows are lawful.</summary>
    public static bool AreFighting(Mobile first, Mobile second) =>
        Find(first) is { Stage: DuelStage.Fighting } duel && duel.Involves(second);

    public static bool Rested(SosariaCharacter character, DateTime now) =>
        character != null && DuelRules.Rested(character.ClockAt(RuleClock.DuelFought), now);

    public static Duel Start(Mobile challenger, Mobile partner, Point3D ground, DateTime now)
    {
        ForgetStale(now);
        var duel = new Duel(challenger.Serial, partner.Serial, ground, now);
        _byDuelist[challenger.Serial] = duel;
        _byDuelist[partner.Serial] = duel;
        return duel;
    }

    /// <summary>Ends the fight for both sides at once and starts both duelists' rest.</summary>
    public static void End(Duel duel, Serial loser, DateTime now)
    {
        if (duel == null || duel.Stage == DuelStage.Over)
        {
            return;
        }

        duel.Finish(loser, now);
        StartRest(duel.Challenger, now);
        StartRest(duel.Partner, now);
        StandDown(duel.Challenger, duel.Partner);
        StandDown(duel.Partner, duel.Challenger);

        if (loser == Serial.Zero || World.FindMobile(duel.OtherOf(loser)) is not { } winner ||
            World.FindMobile(loser) is not { } beaten)
        {
            return;
        }

        Scenes.DuelEnd(winner, beaten);
        ShardNews.Duel(winner, beaten, now);
        AdventureTracker.Shared.Duel(winner, beaten, now);
        Befriend(winner, beaten);
        Befriend(beaten, winner);
        WorldPlay.Log($"{winner.Name} beat {beaten.Name} in a duel");
    }

    /// <summary>The duelist is done with its closing words; the duel no longer holds it.</summary>
    public static void Release(Mobile mobile)
    {
        if (Find(mobile) is { } duel && duel.Stage == DuelStage.Over)
        {
            _byDuelist.Remove(mobile.Serial);
        }
    }

    /// <summary>
    /// A fighter idling by a bank challenges another idle fighter there when the shard's next
    /// duel is due. A duel that runs past its clock is ended here, since the routine may not
    /// tick: a walk-out is called off, a fight goes to the one less hurt.
    /// </summary>
    public static void Consider(SosariaCharacter character)
    {
        var now = Core.Now;

        if (Find(character) is { } duel)
        {
            if (DuelRules.TimedOut(duel.Stage, duel.StageStarted, now))
            {
                End(duel, duel.Stage == DuelStage.Fighting ? JudgedLoser(duel) : Serial.Zero, now);
            }

            return;
        }

        if (now < _nextStart || !DuelRules.MayStartAnother(ActiveCount()) || !MayDuel(character, now))
        {
            return;
        }

        var bank = BankPlaza.BankFor(NavWorld.DestinationsFor(character.HomeFacet), character.Location);

        if (!BankPlaza.Contains(character.Location, bank))
        {
            return;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(character.Location, DuelRules.ChallengeRange))
        {
            if (mobile is not SosariaCharacter partner || partner == character || !MayDuel(partner, now) ||
                !DuelRules.MayChallenge(
                    HitsFraction(character),
                    HitsFraction(partner),
                    character.GuildIndex,
                    partner.GuildIndex,
                    GuildCatalog.None,
                    character.PersonProfile.Tier - partner.PersonProfile.Tier,
                    EngineGuilds.Opposed(character, partner)
                ))
            {
                continue;
            }

            var ground = GroundFor(character.Map, bank, (int)character.Serial.Value);

            if (ground == Point3D.Zero)
            {
                return;
            }

            _nextStart = now + TimeSpan.FromSeconds(
                Utility.RandomMinMax((int)DuelRules.AttemptGapMin.TotalSeconds, (int)DuelRules.AttemptGapMax.TotalSeconds)
            );
            var started = Start(character, partner, ground, now);
            WorldPlay.StartWork(character, new DuelSkill(started));
            WorldPlay.StartWork(partner, new DuelSkill(started));
            Scenes.DuelStart(character, partner);
            WorldPlay.Log($"{character.Name} and {partner.Name} walk out to duel at {ground}");
            return;
        }
    }

    // An idle, healthy, rested blue fighter out of any fight, duel or group.
    private static bool MayDuel(SosariaCharacter character, DateTime now) =>
        character.Alive && !character.IsPk && !PkRules.IsRed(character.Kills) && !character.Criminal &&
        character.Build?.Role == CharacterRole.Fighter && character.Combatant == null &&
        WorldPlay.OpenPvp(character.Map) && !GameParty.InParty(character) &&
        Find(character) == null && Rested(character, now) && WorldPlay.IsIdle(character);

    // Duels still walking out or fighting; each has two duelists in the list.
    private static int ActiveCount()
    {
        var duelists = 0;

        foreach (var duel in _byDuelist.Values)
        {
            if (duel.Stage != DuelStage.Over)
            {
                duelists++;
            }
        }

        return duelists / 2;
    }

    // Out of time on the floor: the one less hurt wins on points.
    private static Serial JudgedLoser(Duel duel) =>
        World.FindMobile(duel.Challenger) is not { } challenger || World.FindMobile(duel.Partner) is not { } partner
            ? Serial.Zero
            : DuelRules.ChallengerLosesOnPoints(HitsFraction(challenger), HitsFraction(partner))
                ? duel.Challenger
                : duel.Partner;

    // A good duel is how half the friendships of the era started.
    private static void Befriend(Mobile self, Mobile other)
    {
        if (self is SosariaCharacter character)
        {
            character.Memory.ShiftBond(other, DuelRules.FriendlyBond, DuelRules.FriendlyReason);
        }
    }

    private static double HitsFraction(Mobile mobile) =>
        mobile.HitsMax > 0 ? (double)mobile.Hits / mobile.HitsMax : 0;

    /// <summary>
    /// The duel ground ten tiles off the banker: the first way round the bank where both
    /// duelists' spots have room to stand.
    /// </summary>
    private static Point3D GroundFor(Map map, Point3D bank, int seed)
    {
        for (var turn = 0; turn < Ways.Length; turn++)
        {
            var (x, y) = Ways[Math.Abs(seed + turn) % Ways.Length];
            var groundX = bank.X + x * DuelRules.ClearTiles;
            var groundY = bank.Y + y * DuelRules.ClearTiles;
            var half = DuelRules.SpacingTiles / 2;
            var z = map.GetAverageZ(groundX, groundY);

            if (map.CanSpawnMobile(new Point3D(groundX - half, groundY, z)) &&
                map.CanSpawnMobile(new Point3D(groundX + half, groundY, z)))
            {
                return new Point3D(groundX, groundY, z);
            }
        }

        return Point3D.Zero;
    }

    // A duelist taken off the world or deleted before its closing words never releases itself.
    private static void ForgetStale(DateTime now)
    {
        List<Serial> stale = null;

        foreach (var (serial, duel) in _byDuelist)
        {
            if (duel.Stage == DuelStage.Over && now - duel.StageStarted >= DuelRules.WalkLimit)
            {
                (stale ??= []).Add(serial);
            }
        }

        for (var i = 0; stale != null && i < stale.Count; i++)
        {
            _byDuelist.Remove(stale[i]);
        }
    }

    private static void StartRest(Serial duelist, DateTime now) =>
        (World.FindMobile(duelist) as SosariaCharacter)?.StartClock(RuleClock.DuelFought, now);

    private static void StandDown(Serial duelist, Serial partner)
    {
        if (World.FindMobile(duelist) is SosariaCharacter { Deleted: false } character &&
            (character.Combatant == null || character.Combatant.Serial == partner))
        {
            character.Combatant = World.FindMobile(partner);
            character.StandDown();

            // A spar ends clean: a poison from the fight must not finish what the floor stopped.
            character.CurePoison(character);
        }
    }
}
