using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

/// <summary>What a straight walk one way from a tile finds: where it ends, how far it got, and how many ways lead on from there.</summary>
public readonly record struct EscapeProbe(int X, int Y, int Z, int Reach, int Exits)
{
    public Point3D At => new(X, Y, Z);
}

/// <summary>One place a runner could head for: a tile it can reach, how open it is there, and whether it is known ground.</summary>
public readonly record struct EscapeOption(Point3D At, int Exits, bool Known);

/// <summary>
/// Where to run, judged on the ground itself. A player backing off a monster looks where it
/// is going: it turns along a coast or a wall instead of pushing into it, leaves by open
/// ground that keeps room to keep moving, and never backs into a corner or a dead end. The
/// walk is tested step by step with the engine's own step check (a <see cref="TileStep"/>),
/// so a way that is blocked is never chosen. Pure over the step delegate.
/// </summary>
public static class EscapeRules
{
    public const int DirectionCount = 8;

    /// <summary>A kite step looks this many tiles ahead in its direction.</summary>
    public const int KiteLookahead = 4;

    /// <summary>One leg of a retreat. Short enough for one path search to reach.</summary>
    public const int LegTiles = 10;

    /// <summary>How far a runner under the guards looks each way for open ground (<see cref="WayOutOfGuards"/>).</summary>
    public const int GuardExitProbeTiles = 12;

    /// <summary>A way shorter than this is no way out.</summary>
    public const int MinLegTiles = 3;

    /// <summary>A goal must lie this much farther from the pack than the runner stands now.</summary>
    public const int MinGainTiles = 3;

    /// <summary>The way in plus at most one more: a corridor end, a corner, or a stall.</summary>
    public const int DeadEndExits = 2;

    public const int AwayWeight = 3;
    public const int LegWeight = 1;
    public const int ReachWeight = 1;
    public const int ExitWeight = 2;
    public const int DeadEndPenalty = 12;
    public const int KnownBonus = 4;
    public const int SafetyBonus = 6;

    /// <summary>A goal this near one that stalled is the same goal.</summary>
    public const int SameGoalTiles = 1;

    public const int NoChoice = -1;

    /// <summary>
    /// True when a runner keeps off guarded ground: a living red out of the guards. The engine
    /// sends a guard the moment a red steps in near a townsperson, so a red that ran from a
    /// fight into Yew or Magincia died to the guards seconds later. A red
    /// already under the guards runs by any ground, since every way out starts there.
    /// </summary>
    public static bool KeepsOffGuards(bool murderer, bool ghost, bool underGuards) =>
        murderer && !ghost && !underGuards;

    // Direction order of the engine: North, Right (north-east), East, Down (south-east),
    // South, Left (south-west), West, Up (north-west).
    private static readonly int[] StepX = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] StepY = [-1, -1, 0, 1, 1, 1, 0, -1];

    /// <summary>Walks straight one way until the next step is blocked or <paramref name="maxSteps"/> are made.</summary>
    public static EscapeProbe Probe(TileStep step, int x, int y, int z, int direction, int maxSteps)
    {
        var reach = 0;

        while (reach < maxSteps && step(x, y, z, x + StepX[direction], y + StepY[direction], out var nextZ))
        {
            x += StepX[direction];
            y += StepY[direction];
            z = nextZ;
            reach++;
        }

        return new EscapeProbe(x, y, z, reach, Exits(step, x, y, z));
    }

    /// <summary>How many of the eight neighbours a walker on this tile can step onto.</summary>
    public static int Exits(TileStep step, int x, int y, int z)
    {
        var exits = 0;

        for (var d = 0; d < DirectionCount; d++)
        {
            if (step(x, y, z, x + StepX[d], y + StepY[d], out _))
            {
                exits++;
            }
        }

        return exits;
    }

    /// <summary>
    /// The nearest open ground a runner under the guards reaches by a straight walk: each of the
    /// eight ways is walked step by step for at most <paramref name="maxSteps"/>, and the first
    /// tile <paramref name="guarded"/> leaves open ends that way. The way with the fewest steps
    /// wins. Null when no way reaches open ground. A red caught in town runs for the town's
    /// edge before anything else; a walk home planned from where it stood went through the town.
    /// </summary>
    public static Point3D? WayOutOfGuards(TileStep step, Func<int, int, int, bool> guarded, Point3D from, int maxSteps)
    {
        Point3D? best = null;
        var bestSteps = maxSteps + 1;

        for (var d = 0; d < DirectionCount; d++)
        {
            var x = from.X;
            var y = from.Y;
            var z = from.Z;
            var steps = 0;

            while (++steps < bestSteps && step(x, y, z, x + StepX[d], y + StepY[d], out var nextZ))
            {
                x += StepX[d];
                y += StepY[d];
                z = nextZ;

                if (!guarded(x, y, z))
                {
                    best = new Point3D(x, y, z);
                    bestSteps = steps;
                    break;
                }
            }
        }

        return best;
    }

    /// <summary>A straight walk that hit something and has at most the way back and one turn left.</summary>
    public static bool IsDeadEnd(int reach, int maxSteps, int exits) => reach < maxSteps && exits <= DeadEndExits;

    public static int StepScore(int gainTiles, int reach, int maxSteps, int exits) =>
        gainTiles * AwayWeight + reach * ReachWeight + exits * ExitWeight -
        (IsDeadEnd(reach, maxSteps, exits) ? DeadEndPenalty : 0);

    /// <summary>
    /// The direction of the next step back from a threat, or <see cref="NoChoice"/> when
    /// pinned. A step never brings the runner nearer the threat; sideways along a wall or a
    /// coast counts. Among the steps left, the one whose short straight walk ends farthest
    /// away, on open ground, wins.
    /// </summary>
    public static int ChooseStep(TileStep step, Point3D from, int threatX, int threatY, int lookahead)
    {
        var threat = new Point3D(threatX, threatY, from.Z);
        var here = NavMetric.Chebyshev(from, threat);
        var best = NoChoice;
        var bestScore = int.MinValue;

        for (var d = 0; d < DirectionCount; d++)
        {
            var x = from.X + StepX[d];
            var y = from.Y + StepY[d];

            if (NavMetric.Chebyshev(new Point3D(x, y, from.Z), threat) < here || !step(from.X, from.Y, from.Z, x, y, out var z))
            {
                continue;
            }

            var probe = Probe(step, x, y, z, d, lookahead - 1);
            var gain = NavMetric.Chebyshev(probe.At, threat) - here;
            var score = StepScore(gain, probe.Reach + 1, lookahead, probe.Exits);

            if (score > bestScore)
            {
                best = d;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// How good a goal is: farther from the pack than here by at least <see cref="MinGainTiles"/>,
    /// then open ground, known ground (walked in by, or on the road net), and the way to safety.
    /// A goal that gains too little is no option.
    /// </summary>
    public static int GoalScore(int gainTiles, int legTiles, int exits, bool known, bool towardSafety)
    {
        if (gainTiles < MinGainTiles)
        {
            return int.MinValue;
        }

        return gainTiles * AwayWeight - legTiles * LegWeight + exits * ExitWeight +
               (known ? KnownBonus : 0) + (towardSafety ? SafetyBonus : 0) -
               (exits <= DeadEndExits ? DeadEndPenalty : 0);
    }

    /// <summary>The best goal among the options, or <see cref="NoChoice"/>.</summary>
    public static int PickGoal(IReadOnlyList<EscapeOption> options, Point3D from, int threatX, int threatY, Point3D? safety)
    {
        var threat = new Point3D(threatX, threatY, from.Z);
        var here = NavMetric.Chebyshev(from, threat);
        var best = NoChoice;
        var bestScore = int.MinValue;

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var towardSafety = safety is { } safe && NavMetric.Chebyshev(option.At, safe) < NavMetric.Chebyshev(from, safe);
            var score = GoalScore(
                NavMetric.Chebyshev(option.At, threat) - here,
                NavMetric.Chebyshev(from, option.At),
                option.Exits,
                option.Known,
                towardSafety
            );

            if (score > bestScore)
            {
                best = i;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>True when the point lies at, or next to, a goal that stalled before.</summary>
    public static bool Stalled(Point3D point, IReadOnlyList<Point3D> stalled)
    {
        for (var i = 0; i < stalled.Count; i++)
        {
            if (NavMetric.Chebyshev(point, stalled[i]) <= SameGoalTiles)
            {
                return true;
            }
        }

        return false;
    }
}
