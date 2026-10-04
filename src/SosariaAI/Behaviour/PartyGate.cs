using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// The group's one gate (see <see cref="PartyGateRules"/>): at the muster any member who can
/// cast Gate Travel toward the dungeon door opens a real gate, the leader walks in first, the
/// rest after it, and the caster holds the gate until everyone it waits for is through, then
/// steps in last. The leader's run and each member's follow ask <see cref="Turn"/> every
/// think; the caster's think reads its own cast. In memory only.
/// </summary>
public static class PartyGate
{
    private const string LeaderStepEvent = "led the party through the gate";
    private const string MemberStepEvent = "stepped through the party gate";
    private const string CasterStepEvent = "stepped through last and let the gate close";
    private const string WhyFizzled = "the gate fizzled";
    private const string WhyClosed = "the gate closed with nobody through";

    private static readonly ILogger logger = SosariaLog.For(typeof(PartyGate));
    private static readonly Dictionary<Serial, Crossing> Crossings = new();

    /// <summary>
    /// Opens the group's gate toward <paramref name="toward"/> when the leader or a member
    /// standing near can cast it. True when the words of power began; the leader then asks
    /// <see cref="Turn"/> until it answers <see cref="GateTurn.None"/>.
    /// </summary>
    public static bool TryOpen(SosariaCharacter leader, Point3D toward, int minTripTiles, string place)
    {
        if (leader == null || !LfgBoard.LeadsRun(leader))
        {
            return false;
        }

        // A crossing left over from a run the leader broke off is closed before a new one.
        if (Crossings.TryGetValue(leader.Serial, out var old))
        {
            Finish(old, Core.Now);
        }

        var people = new List<SosariaCharacter> { leader };
        people.AddRange(LfgBoard.CrewNear(leader, PartyGateRules.CrewTiles));

        if (people.Count < 2)
        {
            return false;
        }

        var canGate = new bool[people.Count];

        for (var i = 0; i < people.Count; i++)
        {
            canGate[i] = GateRules.CanOpenToward(people[i], toward, minTripTiles);
        }

        var pick = PartyGateRules.PickGater(canGate, 0);

        if (pick == PartyGateRules.NoOne || !GateRules.TryOpenToward(people[pick], toward, minTripTiles))
        {
            return false;
        }

        var crossing = new Crossing(leader, people[pick], toward, minTripTiles, place, Core.Now);

        for (var i = 0; i < people.Count; i++)
        {
            if (i != pick)
            {
                crossing.Waiting.Add(people[i].Serial);
            }
        }

        Crossings[leader.Serial] = crossing;
        GameParty.Chat(crossing.Caster, Talk.Line(TalkCategory.PartyGate, new TalkSlots { Place = place }));
        return true;
    }

    /// <summary>True while a group gate waits on this person as leader, caster or member.</summary>
    public static bool Involves(SosariaCharacter who) => Find(who) != null;

    /// <summary>
    /// One think of this person at the group's gate: the caster reads its cast, casts again
    /// after a fizzle, holds, and steps in last; the leader steps in first; a member steps in
    /// after the leader. Anyone through waits on the far side until the crossing is over.
    /// </summary>
    public static GateTurn Turn(SosariaCharacter who)
    {
        var crossing = Find(who);

        if (crossing == null)
        {
            return GateTurn.None;
        }

        var now = Core.Now;

        if (PartyGateRules.Expired(crossing.Gate != null, now - crossing.Since))
        {
            Finish(crossing, now);
            return GateTurn.None;
        }

        if (who == crossing.Caster)
        {
            return CasterTurn(crossing, now);
        }

        if (crossing.Through.Contains(who.Serial) || crossing.Gate == null ||
            !PartyGateRules.MayStep(who == crossing.Leader, crossing.LeaderThrough))
        {
            who.Motor.ClearMoveIntent();
            return GateTurn.Hold;
        }

        var step = GateTravel.StepIntoSpellGate(
            who,
            crossing.Gate,
            who == crossing.Leader ? LeaderStepEvent : MemberStepEvent
        );

        switch (step)
        {
            case GateStep.Through:
                crossing.Through.Add(who.Serial);
                crossing.LeaderThrough |= who == crossing.Leader;
                return GateTurn.Through;
            case GateStep.Waiting:
                return GateTurn.Hold;
            default:
                // The gate would not take this one: it walks, and the caster stops waiting for it.
                crossing.Waiting.Remove(who.Serial);
                crossing.LeaderThrough |= who == crossing.Leader;
                return GateTurn.None;
        }
    }

    private static GateTurn CasterTurn(Crossing crossing, DateTime now)
    {
        var caster = crossing.Caster;

        if (crossing.Gate == null)
        {
            return ReadCast(crossing, now);
        }

        if (crossing.Gate.Deleted)
        {
            Finish(crossing, now);
            return GateTurn.None;
        }

        if (!PartyGateRules.CasterGoes(crossing.Waiting.Count, crossing.Through.Count, now - crossing.OpenedAt))
        {
            caster.Motor.ClearMoveIntent();
            return GateTurn.Hold;
        }

        switch (GateTravel.StepIntoSpellGate(caster, crossing.Gate, CasterStepEvent))
        {
            case GateStep.Waiting:
                return GateTurn.Hold;
            case GateStep.Through:
                crossing.Through.Add(caster.Serial);
                Finish(crossing, now);
                return GateTurn.Through;
            default:
                Finish(crossing, now);
                return GateTurn.None;
        }
    }

    /// <summary>
    /// The caster's words: an open gate starts the crossing; a fizzle is cast again once the
    /// hand is free, or ends it. A fizzle leaves the caster a moment before the next spell;
    /// the cast begun at once was refused, and every group gate that fizzled once fell through.
    /// </summary>
    private static GateTurn ReadCast(Crossing crossing, DateTime now)
    {
        var caster = crossing.Caster;

        switch (TravelSpells.TakeOutcome(caster))
        {
            case TravelCastOutcome.Casting:
                caster.Motor.ClearMoveIntent();
                return GateTurn.Hold;
            case TravelCastOutcome.Succeeded when TravelSpells.GateBeside(caster) is { } gate:
                crossing.Gate = gate;
                crossing.OpenedAt = now;
                crossing.Since = now;
                crossing.Recasting = false;
                return GateTurn.Hold;
            case TravelCastOutcome.None when !crossing.Recasting:
                return GateTurn.Hold;
            case TravelCastOutcome.Succeeded or TravelCastOutcome.Fizzled:
                crossing.Casts++;
                crossing.Recasting = true;
                break;
        }

        if (!PartyGateRules.CastAgain(crossing.Casts))
        {
            crossing.Why = WhyFizzled;
            Finish(crossing, now);
            return GateTurn.None;
        }

        if (GateRules.TryOpenToward(caster, crossing.Toward, crossing.MinTripTiles))
        {
            crossing.Since = now;
            crossing.Recasting = false;
            return GateTurn.Hold;
        }

        if (PartyGateRules.WaitsToRecast(Core.TickCount, caster.NextSpellTime))
        {
            caster.Motor.ClearMoveIntent();
            return GateTurn.Hold;
        }

        crossing.Why = WhyFizzled;
        Finish(crossing, now);
        return GateTurn.None;
    }

    private static Crossing Find(SosariaCharacter who)
    {
        if (who == null || Crossings.Count == 0)
        {
            return null;
        }

        foreach (var crossing in Crossings.Values)
        {
            if (crossing.Leader == who || crossing.Caster == who || crossing.Waiting.Contains(who.Serial))
            {
                return crossing;
            }
        }

        return null;
    }

    /// <summary>Ends a crossing and writes how it went.</summary>
    private static void Finish(Crossing crossing, DateTime now)
    {
        Crossings.Remove(crossing.Leader.Serial);

        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        var line = crossing.Through.Count > 0
            ? PartyGateRules.GatedLine(crossing.Leader.Name, crossing.Through.Count, crossing.Place)
            : PartyGateRules.FellThroughLine(crossing.Leader.Name, crossing.Place, crossing.Why ?? WhyClosed);
        logger.Information("{Line} after {Seconds} s", line, (int)(now - crossing.StartedAt).TotalSeconds);
    }

    private sealed class Crossing
    {
        public Crossing(SosariaCharacter leader, SosariaCharacter caster, Point3D toward, int minTripTiles, string place, DateTime now)
        {
            Leader = leader;
            Caster = caster;
            Toward = toward;
            MinTripTiles = minTripTiles;
            Place = place;
            StartedAt = now;
            Since = now;
        }

        public SosariaCharacter Leader { get; }

        public SosariaCharacter Caster { get; }

        public Point3D Toward { get; }

        public int MinTripTiles { get; }

        public string Place { get; }

        public DateTime StartedAt { get; }

        /// <summary>The last cast begun, or the gate's opening: the clock the limits run on.</summary>
        public DateTime Since { get; set; }

        public DateTime OpenedAt { get; set; }

        public Moongate Gate { get; set; }

        public int Casts { get; set; }

        /// <summary>The last cast fizzled and the next is still to begin.</summary>
        public bool Recasting { get; set; }

        public bool LeaderThrough { get; set; }

        public string Why { get; set; }

        /// <summary>Everyone but the caster the gate waits for, leader first.</summary>
        public HashSet<Serial> Waiting { get; } = [];

        public HashSet<Serial> Through { get; } = [];
    }
}
