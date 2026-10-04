using System;
using SosariaAI.Navigation;

namespace SosariaAI.Tests;

/// <summary>
/// A test world described as the floor heights on each tile. It stands in for the
/// engine's movement check, which needs real map data: a step lands on the floor nearest
/// the walker's height, at most <see cref="Climb"/> above it and any height below, as the
/// engine lets a walker step off a ledge. A diagonal step needs a floor in reach on both
/// side tiles too.
/// </summary>
internal static class ColumnWorld
{
    internal const int StairRise = 5;
    internal const int Climb = StairRise;

    internal static readonly int[] GroundOnly = [0];
    internal static readonly int[] Solid = [];

    internal static TileWalker Walker(Func<int, int, int[]> floorsAt)
    {
        int? Nearest(int x, int y, int z, int lowest, int highest)
        {
            if (x < 0 || y < 0)
            {
                return null;
            }

            int? best = null;

            foreach (var floor in floorsAt(x, y))
            {
                if (floor >= lowest && floor <= highest &&
                    (best is not { } chosen || Math.Abs(floor - z) < Math.Abs(chosen - z)))
                {
                    best = floor;
                }
            }

            return best;
        }

        bool Reaches(int x, int y, int z, out int landed)
        {
            var floor = Nearest(x, y, z, int.MinValue, z + Climb);
            landed = floor ?? z;
            return floor.HasValue;
        }

        bool Step(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
            Reaches(toX, toY, fromZ, out toZ) &&
            (fromX == toX || fromY == toY || Reaches(toX, fromY, fromZ, out _) && Reaches(fromX, toY, fromZ, out _));

        int? FloorNear(int x, int y, int z) => Nearest(x, y, z, z - Standable.DropBelow, z + Standable.ClimbAbove);

        return new TileWalker(FloorNear, Step);
    }
}
