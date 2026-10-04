using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>Where a spell aimed at a chest stands.</summary>
public enum ChestCastStep
{
    /// <summary>The words of power are still being spoken.</summary>
    Casting,

    /// <summary>The cursor came up and was answered with the chest.</summary>
    Aimed,

    /// <summary>No spell and no cursor: it fizzled, was disturbed or never started.</summary>
    Lost
}

/// <summary>
/// The hands-on parts of a dug treasure chest, done the engine's way: a spell's cursor
/// answered with the chest, a spot out of the trap's blast, and the loot lifted out through
/// the chest's own lift hook. World thread only.
/// </summary>
public static class TreasureChestWork
{
    private static readonly int[] StandOffX = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] StandOffY = [-1, -1, 0, 1, 1, 1, 0, -1];

    /// <summary>Answers the spell's cursor with the chest once the words are done.</summary>
    public static ChestCastStep Aim(Mobile caster, Item chest) =>
        SpellCasting.TryAim(caster, chest) ? ChestCastStep.Aimed
        : caster.Spell != null ? ChestCastStep.Casting
        : ChestCastStep.Lost;

    /// <summary>
    /// A standable tile <paramref name="distance"/> tiles out from the chest in one of the
    /// eight directions, with a clear line to it, nearest the character first. Null when none fits.
    /// </summary>
    public static Point3D? StandOff(Map map, Point3D chest, Point3D from, int distance)
    {
        Point3D? best = null;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < StandOffX.Length; i++)
        {
            var x = chest.X + StandOffX[i] * distance;
            var y = chest.Y + StandOffY[i] * distance;
            var z = map.GetAverageZ(x, y);
            var spot = new Point3D(x, y, z);

            if (!map.CanFit(x, y, z, PersonBody.Height, false, true) || !map.LineOfSight(spot, chest))
            {
                continue;
            }

            var away = NavMetric.Chebyshev(from, spot);

            if (away < bestDistance)
            {
                bestDistance = away;
                best = spot;
            }
        }

        return best;
    }

    /// <summary>A standable tile right beside the chest, nearest the character, never on it.</summary>
    public static Point3D? Beside(Map map, Point3D chest, Point3D from) =>
        HarvestSkill.TryStandBeside(map, chest, from, out var at) ? at : null;

    /// <summary>
    /// Lifts what the character can carry out of the chest into its pack, gold first. Each
    /// piece goes through the chest's lift hook, so the engine's chance of a fresh guardian
    /// holds. Returns the gold taken.
    /// </summary>
    public static int Loot(SosariaCharacter looter, TreasureMapChest chest)
    {
        var pack = looter.Backpack;

        if (pack == null)
        {
            return 0;
        }

        var pieces = new List<Item>(chest.Items);
        pieces.Sort((a, b) => (b is Gold).CompareTo(a is Gold));
        var gold = 0;

        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];

            if (piece is not { Deleted: false, Movable: true } || looter.TotalWeight + piece.TotalWeight > looter.MaxWeight)
            {
                continue;
            }

            chest.OnItemLifted(looter, piece);
            pack.DropItem(piece);

            if (piece is Gold coins)
            {
                gold += coins.Amount;
            }
        }

        return gold;
    }
}
