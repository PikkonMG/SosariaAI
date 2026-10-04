using System;

namespace SosariaAI.Navigation;

/// <summary>What one of the eight tiles round a walker offers a step off a trap.</summary>
/// <param name="CanStep">The walker can step there.</param>
/// <param name="Harmed">An armed trap hurts a person standing there.</param>
/// <param name="BesideTrap">A trap lies on that tile or on one beside it.</param>
public readonly record struct TrapStepOption(bool CanStep, bool Harmed, bool BesideTrap);

/// <summary>
/// How a player who knows a dungeon treats its floor traps: never steps from a safe tile
/// onto one a trap hurts, walks a long way round before crossing one, and steps off when it
/// stands on one or idles beside one. The reaches follow ModernUO's own traps
/// (Items/Traps): a spike, saw, gas, fire column or giant spike trap hurts only the tile it
/// lies on; a flame spurt burns the tiles round it; a stone face breathes on the tiles
/// beside its wall; a mushroom bursts on anyone two tiles out. Pure.
/// </summary>
public static class TrapRules
{
    /// <summary>A trap that hurts only a person on its own tile.</summary>
    public const int OnTileReach = 0;

    /// <summary>A flame spurt burns a person who steps next to it.</summary>
    public const int FlameSpurtReach = 1;

    /// <summary>A stone face breathes fire on the tiles beside it.</summary>
    public const int StoneFaceReach = 1;

    /// <summary>A mushroom bursts when a person comes within two tiles.</summary>
    public const int MushroomReach = 2;

    /// <summary>A trap that never hurts, such as the stone face that only breathes for show.</summary>
    public const int Harmless = -1;

    /// <summary>The furthest any trap hurts from its own tile.</summary>
    public const int MaxReach = MushroomReach;

    /// <summary>A trap reaches a person standing up to this high above it, as the engine's trap range check.</summary>
    public const int ReachAbove = 8;

    /// <summary>A trap reaches a person standing less than this far below it, as the engine's trap range check.</summary>
    public const int ReachBelow = 16;

    /// <summary>
    /// A tile route walks up to this many extra tiles round a tile a trap hurts before it
    /// crosses one. A corridor trapped wall to wall is still crossed: there is no other way.
    /// </summary>
    public const int DetourTiles = 60;

    /// <summary>No step off: every way is shut or hurts.</summary>
    public const int NoStep = -1;

    private const int DirectionCount = 8;

    /// <summary>True when a trap at <paramref name="trapZ"/> reaches a person standing at <paramref name="z"/>.</summary>
    public static bool ReachesHeight(int trapZ, int z) => trapZ + ReachAbove >= z && z + ReachBelow > trapZ;

    /// <summary>
    /// True when a walker may step onto a tile: never from a safe tile onto one a trap hurts.
    /// A walker already caught on a hurt tile may step anywhere, so it can always get off.
    /// </summary>
    public static bool MayEnter(bool fromHarmed, bool toHarmed) => !toHarmed || fromHarmed;

    /// <summary>
    /// True when a standing walker steps off: it stands on a tile a trap hurts, or idles
    /// beside a trap outside a fight. A walker on its way is left to its walk, which already
    /// goes round the traps or crosses where nothing else leads on; a fighter holds its
    /// ground beside a trap and never on one.
    /// </summary>
    public static bool ShouldStepOff(bool walking, bool fighting, bool onHarmed, bool besideTrap) =>
        !walking && (onHarmed || besideTrap && !fighting);

    /// <summary>
    /// The direction to step off: the first, counting round from <paramref name="start"/>, to
    /// a tile neither hurt nor beside a trap, else the first to a tile that is not hurt.
    /// <see cref="NoStep"/> when neither exists.
    /// </summary>
    /// <param name="options">One option per direction, in the engine's direction order.</param>
    /// <param name="start">The direction tried first, such as the walker's facing.</param>
    public static int StepOff(ReadOnlySpan<TrapStepOption> options, int start)
    {
        var fallback = NoStep;

        for (var turn = 0; turn < options.Length; turn++)
        {
            var direction = ((start + turn) % DirectionCount + DirectionCount) % DirectionCount;

            if (direction >= options.Length)
            {
                continue;
            }

            var option = options[direction];

            if (!option.CanStep || option.Harmed)
            {
                continue;
            }

            if (!option.BesideTrap)
            {
                return direction;
            }

            if (fallback == NoStep)
            {
                fallback = direction;
            }
        }

        return fallback;
    }
}
