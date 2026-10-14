using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>What a character can truthfully tell a player who asks the way.</summary>
public enum WayKind
{
    /// <summary>Too far to tell from here, by foot or by moongate: the asker gets a shrug.</summary>
    Unknown,

    /// <summary>The place is in sight.</summary>
    Here,

    /// <summary>The character stands inside a dungeon, where a compass means nothing.</summary>
    Inside,

    Close,
    ShortWalk,
    Far,

    /// <summary>A far place a public moongate brings much nearer; the direction is to the gate.</summary>
    Moongate,

    /// <summary>As <see cref="Moongate"/>, with the gate in sight.</summary>
    MoongateHere
}

/// <summary>A way answer: its kind, and the compass word it gives ("south-east"), or null when it gives none.</summary>
public readonly record struct WayAnswer(WayKind Kind, string Direction);

/// <summary>
/// Directions the way a 1999 player gave them: a compass word from where the character stands,
/// a rough distance, and "take the moongate" when a far place is much nearer through the gates.
/// Built from the nav data at no model cost, so the answer is true for the character's own map
/// and never a guess. A place past <see cref="KnownWayTiles"/> by any road, such as land a
/// moongate does not reach, gets a shrug: a player who did not know the way said so. Pure.
/// </summary>
public static class DirectionRules
{
    /// <summary>A place this near is on the screen: the client's view range.</summary>
    public const int InSightTiles = 18;

    /// <summary>A place this near is close: a few screens.</summary>
    public const int CloseTiles = 120;

    /// <summary>A place this near is a short walk; farther is far.</summary>
    public const int ShortWalkTiles = 400;

    /// <summary>A far place is "take the moongate" when the gates save at least this many tiles.</summary>
    public const int MoongateSavingTiles = 200;

    /// <summary>Past this many tiles by the shorter road the character does not know the way.</summary>
    public const int KnownWayTiles = 1500;

    public const string DirectionToken = "{dir}";

    /// <summary>The words a player names a public moongate with; the catalog holds no gates, the gate list does.</summary>
    private static readonly string[] MoongateWords = ["moongate", "moongates", "gate", "gates"];

    private static readonly string[] HereLines =
        ["right here", "its right here", "ur standing at it lol", "look around lol", "u found it", "here, right in front of u"];

    private static readonly string[] InsideLines =
        ["not from down here lol", "get out first then ask", "no clue from down here", "cant tell from in here"];

    private static readonly string[] CloseLines =
        ["just {dir} of here", "{dir}, real close", "its close, go {dir}", "{dir} a bit", "few screens {dir}"];

    private static readonly string[] ShortWalkLines =
        ["{dir} of here, short walk", "go {dir}, not far", "{dir}, cant miss it", "{dir}, little walk", "bit of a walk {dir}"];

    private static readonly string[] FarLines =
        ["far {dir}", "way {dir}, long walk", "its a trek, go {dir}", "long way {dir}", "{dir}, far tho"];

    private static readonly string[] MoongateLines =
        [
            "far, take the moongate. its {dir} of here", "use the moongate, {dir} of here", "moongate, its {dir}",
            "too far to walk, moongate is {dir}", "take the gate, {dir} of here"
        ];

    private static readonly string[] MoongateHereLines =
        ["far, take the moongate right here", "moongate, its right here", "use the gate here, way faster", "far, gate is right there"];

    /// <summary>Every line a way answer may say, for the speech checks.</summary>
    public static IEnumerable<string> AllLines()
    {
        foreach (var lines in new[] { HereLines, InsideLines, CloseLines, ShortWalkLines, FarLines, MoongateLines, MoongateHereLines })
        {
            foreach (var line in lines)
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// The answer from <paramref name="from"/> to <paramref name="to"/> on one map, with that
    /// map's public moongates. <paramref name="fromInside"/>: the character stands in a dungeon.
    /// </summary>
    public static WayAnswer Answer(Point3D from, Point3D to, IReadOnlyList<Point3D> moongates, bool fromInside)
    {
        var walk = NavMetric.Chebyshev(from, to);

        if (walk <= InSightTiles)
        {
            return new WayAnswer(WayKind.Here, null);
        }

        if (fromInside)
        {
            return new WayAnswer(WayKind.Inside, null);
        }

        var byGate = NavMetric.ByMoongate(from, to, moongates);

        if (walk > ShortWalkTiles && byGate + MoongateSavingTiles <= walk)
        {
            if (byGate > KnownWayTiles)
            {
                return new WayAnswer(WayKind.Unknown, null);
            }

            var gate = Nearest(from, moongates);

            return NavMetric.Chebyshev(from, gate) <= InSightTiles
                ? new WayAnswer(WayKind.MoongateHere, null)
                : new WayAnswer(WayKind.Moongate, Compass(from, gate));
        }

        if (walk > KnownWayTiles)
        {
            return new WayAnswer(WayKind.Unknown, null);
        }

        var kind = walk <= CloseTiles ? WayKind.Close : walk <= ShortWalkTiles ? WayKind.ShortWalk : WayKind.Far;
        return new WayAnswer(kind, Compass(from, to));
    }

    /// <summary>True when the asked words name a public moongate: "the moongate", "nearest gate".</summary>
    public static bool NamesMoongate(string words)
    {
        foreach (var token in PartyOrderRules.PlaceTokens(words))
        {
            if (Array.IndexOf(MoongateWords, token) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The compass word from one spot to another, by the engine's own eight directions.</summary>
    public static string Compass(Point3D from, Point3D to) =>
        Utility.GetDirection(from, to) switch
        {
            Direction.North => "north",
            Direction.Right => "north-east",
            Direction.East => "east",
            Direction.Down => "south-east",
            Direction.South => "south",
            Direction.Left => "south-west",
            Direction.West => "west",
            _ => "north-west"
        };

    /// <summary>The line said for an answer, picked by <paramref name="seed"/>; null for <see cref="WayKind.Unknown"/>.</summary>
    public static string Line(WayAnswer answer, int seed)
    {
        var lines = answer.Kind switch
        {
            WayKind.Here => HereLines,
            WayKind.Inside => InsideLines,
            WayKind.Close => CloseLines,
            WayKind.ShortWalk => ShortWalkLines,
            WayKind.Far => FarLines,
            WayKind.Moongate => MoongateLines,
            WayKind.MoongateHere => MoongateHereLines,
            _ => null
        };

        if (lines == null)
        {
            return null;
        }

        var line = lines[Math.Abs(seed % lines.Length)];
        return answer.Direction == null ? line : line.Replace(DirectionToken, answer.Direction, StringComparison.Ordinal);
    }

    /// <summary>The point nearest <paramref name="from"/>; the list must hold one.</summary>
    public static Point3D Nearest(Point3D from, IReadOnlyList<Point3D> points)
    {
        var best = points[0];

        for (var i = 1; i < points.Count; i++)
        {
            if (NavMetric.Chebyshev(from, points[i]) < NavMetric.Chebyshev(from, best))
            {
                best = points[i];
            }
        }

        return best;
    }
}
