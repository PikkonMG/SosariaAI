using System;

namespace SosariaAI.Navigation;

/// <summary>
/// One step from a tile at a height onto a neighbouring tile. True when the walker can
/// make it; <paramref name="toZ"/> is then the height it stands at.
/// </summary>
public delegate bool TileStep(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ);

/// <summary>
/// How a walker meets the tile grid: the floor it stands on near a height, one step to a
/// neighbouring tile, and the tiles an armed trap hurts. The tile router, the building exit
/// and the road builder all walk with one of these. <see cref="Standable.Walker"/> gives the
/// live map's.
/// </summary>
/// <param name="FloorNear">The floor height at a tile near a height, or null when none stands.</param>
/// <param name="Step">One step onto a neighbouring tile, as the engine allows it, traps or not.</param>
/// <param name="Harms">True when an armed trap hurts a person at a tile and height; null for ground with no traps.</param>
public sealed record TileWalker(Func<int, int, int, int?> FloorNear, TileStep Step, Func<int, int, int, bool> Harms = null)
{
    /// <summary>True when an armed trap hurts a person standing at the tile at that height.</summary>
    public bool IsHarmed(int x, int y, int z) => Harms?.Invoke(x, y, z) == true;

    /// <summary>
    /// One step as a player who knows the traps takes it: <see cref="Step"/>, but never from
    /// a safe tile onto one a trap hurts (<see cref="TrapRules.MayEnter"/>).
    /// </summary>
    public bool SafeStep(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
        Step(fromX, fromY, fromZ, toX, toY, out toZ) &&
        TrapRules.MayEnter(IsHarmed(fromX, fromY, fromZ), IsHarmed(toX, toY, toZ));

    /// <summary>The floor near a height where a node may sit: <see cref="FloorNear"/>, but none on a tile a trap hurts.</summary>
    public int? SafeFloorNear(int x, int y, int z) =>
        FloorNear(x, y, z) is { } floor && !IsHarmed(x, y, floor) ? floor : null;
}
