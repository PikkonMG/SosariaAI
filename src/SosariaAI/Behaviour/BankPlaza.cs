using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// The packed street in front of a banker. Britain bank is 1425,1695; the live
/// pile sat at 1415,1700. Guild wars started there because everyone idled there.
/// </summary>
public static class BankPlaza
{
    public const int Range = 16;

    public static bool Contains(Point3D at, Point3D bank) =>
        bank != Point3D.Zero && NavMetric.Chebyshev(at, bank) <= Range;

    public static bool MayStartPersonFight(bool onPlaza) => !onPlaza;

    public static Point3D BankFor(DestinationCatalog catalog, Point3D at)
    {
        var banks = catalog?.NearestFirst("bank", at, 1);
        if (banks is { Count: > 0 } && banks[0].Arrival != Point3D.Zero)
        {
            return banks[0].Arrival;
        }

        return CharactersFile.DefaultBankSpot;
    }
}
