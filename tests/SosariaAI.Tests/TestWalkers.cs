using System;
using SosariaAI.Navigation;

namespace SosariaAI.Tests;

/// <summary>Walkers over made-up ground for graph generation tests.</summary>
internal static class TestWalkers
{
    /// <summary>Open flat ground at height zero everywhere.</summary>
    public static TileWalker Flat { get; } = Ground(static (_, _, _) => true, null);

    /// <summary>Every tile stands at the height asked, and a step keeps the height it leaves.</summary>
    public static TileWalker AnyFloor { get; } = From(static (_, _, z) => z);

    /// <summary>
    /// A walker whose floor at a tile near a height is <paramref name="floorNear"/>. A step
    /// lands on the floor near the height it leaves, and fails where there is none.
    /// </summary>
    public static TileWalker From(Func<int, int, int, int?> floorNear) =>
        new(
            floorNear,
            (int _, int _, int fromZ, int toX, int toY, out int toZ) =>
            {
                var floor = floorNear(toX, toY, fromZ);
                toZ = floor ?? fromZ;
                return floor != null;
            }
        );

    /// <summary>
    /// Most a test step climbs: the engine's own step height. A step down may drop any
    /// height, as the engine lets a walker step off a ledge.
    /// </summary>
    public const int StepClimb = 2;

    /// <summary>
    /// A walker that knows only the ground: every step lands on the ground height, so it
    /// never finds an upper floor. A step climbs at most <see cref="StepClimb"/> and drops
    /// any height. A diagonal step needs both side tiles as well, as it does for a player.
    /// </summary>
    /// <param name="canStand">True when a walker can stand at a tile near a height.</param>
    /// <param name="groundZ">The ground height at a tile, or null for flat ground at zero.</param>
    public static TileWalker Ground(Func<int, int, int, bool> canStand, Func<int, int, int> groundZ)
    {
        ArgumentNullException.ThrowIfNull(canStand);
        var height = groundZ ?? (static (_, _) => 0);

        int? FloorNear(int x, int y, int z)
        {
            var ground = height(x, y);
            return canStand(x, y, ground) ? ground : null;
        }

        bool Lands(int x, int y, int fromZ, out int z)
        {
            z = height(x, y);
            return x >= 0 && y >= 0 && canStand(x, y, z) && z - fromZ <= StepClimb;
        }

        bool Step(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
            Lands(toX, toY, fromZ, out toZ) &&
            (fromX == toX || fromY == toY || Lands(toX, fromY, fromZ, out _) && Lands(fromX, toY, fromZ, out _));

        return new TileWalker(FloorNear, Step);
    }
}
