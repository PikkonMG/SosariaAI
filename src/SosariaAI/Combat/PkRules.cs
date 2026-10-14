using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Combat;

/// <summary>
/// The reds' own town on one facet: the ground where they bank, shop and rest and where no
/// red starts a fight, and the spot a red's home and first day start from.
/// </summary>
public sealed record RedHaven(string Facet, IReadOnlyList<Rectangle2D> Areas, Point3D Home)
{
    /// <summary>True when the tile lies in the haven's ground.</summary>
    public bool Contains(int x, int y)
    {
        for (var i = 0; i < (Areas?.Count ?? 0); i++)
        {
            if (Areas[i].Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Murder counts, and the reds' town. Buccaneer's Den has no guards and the only bank a red
/// may use; a red anywhere else under the guards dies to them. On a map the stock tables do not
/// fit, the reds' town is the haven the boot picks (<see cref="Spawning.WorldAnchors.HavenFor"/>);
/// every Den test reads <see cref="Haven"/>. The shard has one haven: on the first map in
/// characters.json that carries reds.
/// </summary>
public static class PkRules
{
    public const int MurdersToRed = 5;
    public const int BucsDenMinX = 2600;
    public const int BucsDenMaxX = 2800;
    public const int BucsDenMinY = 2060;
    public const int BucsDenMaxY = 2290;

    /// <summary>Buccaneer's Den on Felucca: the island town, and its square by the docks.</summary>
    public static readonly RedHaven BucsDen = new(
        FacetNames.Felucca,
        [new Rectangle2D(BucsDenMinX, BucsDenMinY, BucsDenMaxX - BucsDenMinX + 1, BucsDenMaxY - BucsDenMinY + 1)],
        new Point3D(2706, 2163, 0)
    );

    /// <summary>The reds' haven this boot: Buccaneer's Den until the nav load picks another.</summary>
    public static RedHaven Haven { get; private set; } = BucsDen;

    /// <summary>The haven's square, where a red's home and first day start.</summary>
    public static Point3D BucsDenHaven => Haven.Home;

    /// <summary>Sets the reds' haven for this boot; null keeps Buccaneer's Den.</summary>
    public static void UseHaven(RedHaven haven) => Haven = haven ?? BucsDen;

    public static bool IsRed(int kills) => kills >= MurdersToRed;

    /// <summary>True when the tile lies in the reds' haven (<see cref="Haven"/>).</summary>
    public static bool InBuccaneersDen(int x, int y) => Haven.Contains(x, y);

    public static bool MayAttack(
        bool attackerIsPk,
        bool attackerUnderGuards,
        bool victimUnderGuards,
        bool victimIsRed,
        bool inBucsDen
    )
    {
        if (!attackerIsPk)
        {
            return false;
        }

        if (attackerUnderGuards || victimUnderGuards)
        {
            return false;
        }

        if (victimIsRed)
        {
            return false;
        }

        if (inBucsDen)
        {
            return false;
        }

        return true;
    }

    public static bool MayBankAt(bool isRed, int x, int y) =>
        !isRed || InBuccaneersDen(x, y);

    /// <summary>A red keeps out of guarded places: towns and the public moongates.</summary>
    public static bool MayVisit(bool isRed, bool guarded) => !isRed || !guarded;
}
