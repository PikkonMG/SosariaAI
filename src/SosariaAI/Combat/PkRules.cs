using Server;

namespace SosariaAI.Combat;

/// <summary>
/// Murder counts, and the reds' town. Buccaneer's Den has no guards and the only bank a red
/// may use; a red anywhere else under the guards dies to them.
/// </summary>
public static class PkRules
{
    public const int MurdersToRed = 5;
    public const int BucsDenMinX = 2600;
    public const int BucsDenMaxX = 2800;
    public const int BucsDenMinY = 2060;
    public const int BucsDenMaxY = 2290;

    /// <summary>The Den's square by the docks, where a red's home and first day start.</summary>
    public static readonly Point3D BucsDenHaven = new(2706, 2163, 0);

    public static bool IsRed(int kills) => kills >= MurdersToRed;

    public static bool InBuccaneersDen(int x, int y) =>
        x >= BucsDenMinX && x <= BucsDenMaxX && y >= BucsDenMinY && y <= BucsDenMaxY;

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
