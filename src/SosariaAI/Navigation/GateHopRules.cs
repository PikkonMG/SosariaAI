using System;
using Server;
using SosariaAI.Common;

namespace SosariaAI.Navigation;

/// <summary>
/// Public moons are not a ride. Hop once, step off the pad, and rest before the next hop.
/// Standing on a moon and taking it every tick is the spam. A teleporter or spell gate
/// moves a person by the engine's own rules; these say when that has happened.
/// </summary>
public static class GateHopRules
{
    public const int AlreadyThereTiles = 8;
    public const int StepOffTiles = 2;
    public const int RestSeconds = 45;

    /// <summary>
    /// A think walks a person a tile or two. A jump further may be a gate carrying it, or a
    /// slow think that walked several tiles; a landing check tells which.
    /// </summary>
    public const int WalkStepTiles = 2;

    /// <summary>How far round a person a spell gate is looked for: a leader's gate a few steps off.</summary>
    public const int SpellGateSearchTiles = 8;

    /// <summary>A spell gate follows a leader when it lands this close to where the leader now is.</summary>
    public const int SpellGateLeaderSlackTiles = 24;

    /// <summary>True when the person stands where a pad was planned to land it.</summary>
    public static bool Landed(Point3D at, Point3D plannedLanding) =>
        plannedLanding != Point3D.Zero &&
        NavMetric.Chebyshev(at, plannedLanding) <= GatePad.LandingSlackTiles &&
        NavMetric.SameFloor(at, plannedLanding);

    /// <summary>
    /// True when a person waiting to take the gate at <paramref name="pad"/> no longer stands
    /// where it can step on: a fight or a flight carried it off while it waited.
    /// </summary>
    public static bool LeftThePad(Point3D at, Point3D pad) => NavMetric.Chebyshev(at, pad) > GatePad.ReachTiles;

    /// <summary>True when the person moved further than a step since <paramref name="from"/>.</summary>
    public static bool Carried(Point3D from, Point3D now, bool sameMap) =>
        !sameMap || NavMetric.Chebyshev(from, now) > WalkStepTiles;

    public static bool GateLandsNear(Point3D gateTarget, Point3D toward) =>
        NavMetric.Chebyshev(gateTarget, toward) <= SpellGateLeaderSlackTiles;

    public static bool ShouldHop(Point3D from, Point3D dest, bool sameMap) =>
        !sameMap || NavMetric.Chebyshev(from, dest) > AlreadyThereTiles;

    public static bool MayHop(DateTime lastHop, DateTime now) =>
        TimeRules.Rested(lastHop, now, TimeSpan.FromSeconds(RestSeconds));

    public static Point3D StepOff(Point3D pad, Point3D dest)
    {
        var dx = dest.X == pad.X ? 1 : Math.Sign(dest.X - pad.X);
        var dy = dest.Y == pad.Y ? 0 : Math.Sign(dest.Y - pad.Y);

        if (dx == 0 && dy == 0)
        {
            dx = 1;
        }

        return new Point3D(pad.X + dx * StepOffTiles, pad.Y + dy * StepOffTiles, pad.Z);
    }
}
