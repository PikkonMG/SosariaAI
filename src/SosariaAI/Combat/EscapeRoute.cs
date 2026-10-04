using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

/// <summary>
/// Picks where a runner heads, from places it can really reach: the end of a straight walk
/// each of the eight ways (tested step by step, so a coast or a wall ends it), the spots of
/// the way it came in, and the nearby road nodes. <see cref="EscapeRules.PickGoal"/> weighs
/// them. A living red out of the guards runs only by ground the guards do not cover
/// (<see cref="EscapeRules.KeepsOffGuards"/>), and a guarded safety does not pull it; one under
/// the guards is pulled to the nearest open ground (<see cref="WayOutOfGuards"/>). World
/// thread only: it reads the map.
/// </summary>
public static class EscapeRoute
{
    /// <summary>Road nodes looked at around the runner.</summary>
    public const int NavSample = 8;

    /// <summary>A road node or trail spot farther than this many legs is not one leg of a run.</summary>
    public const int MaxLegsAway = 2;

    /// <summary>A road node this far above or below is on another floor.</summary>
    public const int MaxFloorGap = 16;

    /// <summary>
    /// The goal of the next leg away from <paramref name="threat"/>, or null when no place
    /// gains ground. <paramref name="safety"/> (a bank or a healer) pulls the choice its way.
    /// Goals near one in <paramref name="stalled"/> are skipped.
    /// </summary>
    public static Point3D? PickGoal(
        SosariaCharacter character,
        Point3D threat,
        int legTiles,
        Point3D? safety,
        IReadOnlyList<Point3D> stalled
    )
    {
        var map = character?.Map;

        if (map == null || map == Map.Internal)
        {
            return null;
        }

        var step = character.Motor.EscapeStep;
        var keepOffGuards = character.Motor.KeepsOffGuards;
        bool Barred(Point3D spot) => keepOffGuards && GuardCall.IsGuardedPlace(spot, map);
        var from = character.Location;
        var options = new List<EscapeOption>();

        if (safety is { } haven && Barred(haven))
        {
            safety = null;
        }

        safety = WayOutOfGuards(character) ?? safety;

        for (var d = 0; d < EscapeRules.DirectionCount; d++)
        {
            var probe = EscapeRules.Probe(step, from.X, from.Y, from.Z, d, legTiles);

            if (probe.Reach >= EscapeRules.MinLegTiles)
            {
                AddUnlessStalled(options, new EscapeOption(probe.At, probe.Exits, Known: false), stalled);
            }
        }

        var maxLeg = legTiles * MaxLegsAway;
        var trail = character.Motor.Trail.Spots;

        for (var i = 0; i < trail.Count; i++)
        {
            AddKnown(options, step, from, trail[i], maxLeg, stalled, Barred);
        }

        var nodes = NavWorld.GraphFor(map.Name)?.FindNearest(from, NavSample) ?? [];

        for (var i = 0; i < nodes.Count; i++)
        {
            if (!nodes[i].Indoor)
            {
                AddKnown(options, step, from, nodes[i].Location, maxLeg, stalled, Barred);
            }
        }

        var best = EscapeRules.PickGoal(options, from, threat.X, threat.Y, safety);
        return best == EscapeRules.NoChoice ? null : options[best].At;
    }

    /// <summary>
    /// The open ground a living red or criminal under the guards makes for
    /// (<see cref="EscapeRules.WayOutOfGuards"/>), or null for anyone the guards leave be, or
    /// when no straight walk reaches open ground near. It is such a runner's safety: whatever
    /// else would pull its run, the town's edge pulls first.
    /// </summary>
    public static Point3D? WayOutOfGuards(SosariaCharacter character)
    {
        var map = character?.Map;

        if (map == null || map == Map.Internal || character.IsGhost ||
            !ResurrectAid.WantedUnderGuards(character.Criminal, character.Murderer, SosariaCharacter.UnderGuards(character)))
        {
            return null;
        }

        return EscapeRules.WayOutOfGuards(
            character.Motor.GroundStep,
            (x, y, z) => GuardCall.IsGuardedPlace(new Point3D(x, y, z), map),
            character.Location,
            EscapeRules.GuardExitProbeTiles
        );
    }

    private static void AddKnown(
        List<EscapeOption> options,
        TileStep step,
        Point3D from,
        Point3D spot,
        int maxLeg,
        IReadOnlyList<Point3D> stalled,
        Func<Point3D, bool> barred
    )
    {
        var leg = NavMetric.Chebyshev(from, spot);

        if (leg < EscapeRules.MinLegTiles || leg > maxLeg || Math.Abs(spot.Z - from.Z) > MaxFloorGap || barred(spot))
        {
            return;
        }

        AddUnlessStalled(options, new EscapeOption(spot, EscapeRules.Exits(step, spot.X, spot.Y, spot.Z), Known: true), stalled);
    }

    private static void AddUnlessStalled(List<EscapeOption> options, EscapeOption option, IReadOnlyList<Point3D> stalled)
    {
        if (!EscapeRules.Stalled(option.At, stalled))
        {
            options.Add(option);
        }
    }
}
