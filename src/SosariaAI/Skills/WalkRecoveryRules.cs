using System;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>What a stuck walk leg does this think.</summary>
public enum WalkRecovery
{
    None,

    /// <summary>Step to one side of the facing. A closed door ahead is opened by the step itself.</summary>
    StepAside,

    /// <summary>Drop the engine path and plan again from where the walker stands.</summary>
    Replan,

    /// <summary>Step aside, then plan again from the new spot.</summary>
    NudgeAndReplan,

    /// <summary>
    /// Leave the engine path and take the walker's own steps to the leg. The engine path
    /// plans round a shut door as round a wall; the character's own step opens it.
    /// </summary>
    StepAround
}

/// <summary>
/// The stuck ladder for one walk leg, the way a player works a jam. Two frozen thinks and it
/// steps aside. A short spell without getting closer and it plans again from where it
/// stands; the spell after that it takes its own steps, through the door the engine path
/// will not open. Later spells step aside and plan again. When nothing helps the approach
/// guard ends the leg, and the trip marks the edge and plans round it. A walker carried well
/// off its leg between two thinks sets out again from where it stands instead, and a road from
/// there past a place it ran from waits until the place goes quiet (<see cref="DangerBarsRoad"/>).
/// </summary>
public static class WalkRecoveryRules
{
    public const int FrozenTicksBeforeStepAside = 2;

    /// <summary>Thinks without a closer distance between replans: about 1.2 s at the active think.</summary>
    public const int StallTicksPerReplan = 5;

    /// <summary>
    /// Fresh plans a trip may make from where it stands after a failed leg or a sealed
    /// start. Past this the trip ends and the planner picks a new goal.
    /// </summary>
    public const int MaxTripReplans = 3;

    /// <summary>
    /// Thinks without a closer distance before the walker takes its own steps: the second
    /// spell. Trinsic and Skara Brae homes behind a shut door stalled the whole leg without it.
    /// </summary>
    public const int StepAroundStallTicks = StallTicksPerReplan * 2;

    /// <summary>
    /// Tiles further from its leg than it stood a think before that make a walker's leg
    /// stale: a fight, a flight, or a pad the plan did not take carried it off. Half a tile
    /// leg: a slow think walks a few tiles round a wall, never this far away from the leg.
    /// </summary>
    public const int CarriedOffLegTiles = TileRoute.WaypointSpacing / 2;

    private const int ReplanKinds = 2;

    public static bool MayReplanTrip(int replansSoFar) => replansSoFar < MaxTripReplans;

    /// <summary>
    /// True when a walker carried since its last think (<see cref="GateHopRules.Carried"/>)
    /// now stands off its leg: on another map, or more than <see cref="CarriedOffLegTiles"/>
    /// further from the leg than it stood before. Walking the old leg from there stalled 264
    /// walks in one evening, most after a fight had pulled the walker away.
    /// </summary>
    public static bool CarriedOffLeg(bool sameMap, int legTilesBefore, int legTilesNow) =>
        !sameMap || legTilesNow - legTilesBefore > CarriedOffLegTiles;

    /// <summary>
    /// True when a trip reads each road it finds for the places its walker ran from before it
    /// walks it: a trip set to shun danger, and any trip a fight or a flight carried off its
    /// road. Such a road recalls over the danger or ends the trip. Terrin of Jhelom was chased
    /// off his road home 66 times in 80 minutes by the reds at the Minoc moongate and set out
    /// past them again every time: he got clear, walked back, and was run off again. A party
    /// carried off its road walks on together: 19 of 23 dungeon runs that ended "the road there
    /// passes a place it ran from" were parties at a moongate that had just won the fight, and
    /// Magincia's one way out is its moongate.
    /// </summary>
    public static bool ReadsRoadForDanger(bool shunsDanger, bool carriedOff, bool inParty) =>
        shunsDanger || carriedOff && !inParty;

    /// <summary>
    /// Minutes a place the walker ran from must lie unseen before the road past it is walked
    /// again; each run and each sighting renews the place (<see cref="DangerSpots.NoteSighting"/>).
    /// Terrin of Jhelom set out past the same reds about every 73 seconds (66 times in 80
    /// minutes): two minutes is longer than his whole round of run, clear and walk back.
    /// </summary>
    public const int DangerQuietMinutes = 2;

    public static readonly TimeSpan DangerQuietAfter = TimeSpan.FromMinutes(DangerQuietMinutes);

    /// <summary>
    /// Quiet spans one wait for danger lasts at most: a threat seen once more in the first span
    /// is given a second. A wait ends sooner the moment its road no longer passes a barring place.
    /// </summary>
    public const int DangerWaitQuietSpans = 2;

    public static readonly TimeSpan MaxDangerWait = TimeSpan.FromMinutes(DangerQuietMinutes * DangerWaitQuietSpans);

    /// <summary>
    /// Waits for danger one trip may make. A road still barred after a wait, or after the next
    /// flight, is waited out once more, for the reds may move on; a road barred a third time is
    /// the loop Terrin of Jhelom walked, and the trip is given up. Two full waits stand a walker
    /// still for eight minutes, under the ten after which the fleet watchdog ends a step that
    /// stands still (<see cref="SosariaAI.Admin.StallRules.StallAfter"/>).
    /// </summary>
    public const int MaxDangerWaits = 2;

    /// <summary>
    /// True while a place the walker ran from still bars its road: the place was seen less than
    /// <see cref="DangerQuietAfter"/> ago, or a threat it must run from stands there now. Only a
    /// place gone quiet is looked at (<paramref name="threatThere"/>), for a fresh one bars the
    /// road whatever stands there. A quiet place with nobody there is walked past, as a player
    /// went on once the reds moved on. Before this every remembered place barred the road for its
    /// full ten minutes: 1,317 of 1,395 walks home that ended "the road there passes a place it
    /// ran from" ended at the first road read after a flight.
    /// </summary>
    public static bool DangerBarsRoad(TimeSpan sinceSeen, Func<bool> threatThere) =>
        sinceSeen < DangerQuietAfter || threatThere();

    /// <summary>
    /// True when a trip whose road still passes danger, with no recall over it, stands where it
    /// is and waits for the place to go quiet before it gives up. A trip set to shun danger is
    /// one its walker may give up, and gives it up at once: the planner picks something else.
    /// </summary>
    public static bool MayWaitOutDanger(bool shunsDanger, int waitsSoFar) =>
        !shunsDanger && waitsSoFar < MaxDangerWaits;

    public static WalkRecovery Next(int frozenTicks, int stallTicks)
    {
        if (stallTicks == StepAroundStallTicks)
        {
            return WalkRecovery.StepAround;
        }

        if (stallTicks > 0 && stallTicks % StallTicksPerReplan == 0)
        {
            return stallTicks / StallTicksPerReplan % ReplanKinds == 1
                ? WalkRecovery.Replan
                : WalkRecovery.NudgeAndReplan;
        }

        return frozenTicks == FrozenTicksBeforeStepAside ? WalkRecovery.StepAside : WalkRecovery.None;
    }
}
