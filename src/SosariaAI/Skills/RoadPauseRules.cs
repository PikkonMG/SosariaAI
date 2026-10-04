using System;
using SosariaAI.Behaviour;

namespace SosariaAI.Skills;

/// <summary>
/// A person strolling a long road stops now and then: it looks round, turns to someone
/// passing, then walks on, a few seconds about once a minute;
/// a road of walkers who never stopped read as a belt of pieces sliding past. A runner on an
/// errand, a fighter and a ghost never stop, and neither does a walk that is nearly there.
/// Pure. No world objects.
/// </summary>
public static class RoadPauseRules
{
    /// <summary>A walk with less road than this left keeps going: the last street is not a road.</summary>
    public const int MinRoadLeftTiles = 60;

    public const int MinWalkSeconds = 40;
    public const int MaxWalkSeconds = 120;
    public const int MinPauseSeconds = 2;
    public const int MaxPauseSeconds = 5;

    public static bool MayPause(bool running, bool fighting, bool ghost, int roadLeftTiles) =>
        !running && !fighting && !ghost && roadLeftTiles >= MinRoadLeftTiles;

    /// <summary>How long the walker keeps walking before its next stop.</summary>
    public static TimeSpan WalkBetween(int roll) => ArrivalRules.Seconds(MinWalkSeconds, MaxWalkSeconds, roll);

    /// <summary>How long one stop lasts.</summary>
    public static TimeSpan PauseLength(int roll) => ArrivalRules.Seconds(MinPauseSeconds, MaxPauseSeconds, roll);
}
