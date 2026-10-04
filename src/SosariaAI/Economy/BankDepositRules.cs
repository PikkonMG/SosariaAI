using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Economy;

public static class BankDepositRules
{
    /// <summary>
    /// A bank trip needs a job: something to bank, supplies to take from the box, a purse to
    /// settle at the banker, or a party trip to end. Without one the character walked to the
    /// bank and "deposited 0 items" again and again. A criminal has none: the banker will not
    /// open its box.
    /// </summary>
    public static bool IsWorthATrip(int bankableItems, int suppliesInBox, bool purseNeedsBanker, bool endsPartyTrip, bool criminal) =>
        !criminal && (bankableItems > 0 || suppliesInBox > 0 || purseNeedsBanker || endsPartyTrip);

    /// <summary>
    /// The bank a trip walks to: <paramref name="planned"/> when a banker works there, else the
    /// first of <paramref name="nearestFirst"/> that has one, else <see cref="Point3D.Zero"/>.
    /// Cove has no bank: its miners walked to the provisioner's door and failed "no banker
    /// works at this bank" seventeen times in fifteen minutes.
    /// </summary>
    public static Point3D StaffedBank(Point3D planned, IReadOnlyList<Point3D> nearestFirst, Func<Point3D, bool> staffed)
    {
        if (staffed(planned))
        {
            return planned;
        }

        for (var i = 0; i < nearestFirst.Count; i++)
        {
            if (staffed(nearestFirst[i]))
            {
                return nearestFirst[i];
            }
        }

        return Point3D.Zero;
    }
}
