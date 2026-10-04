using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>A spot a person could stand about at, and who is there already.</summary>
/// <param name="Others">People standing at the spot; on a bank plaza, the idlers on the whole plaza.</param>
public readonly record struct LoiterSpotLook(Point3D Spot, int Others, bool OnBankPlaza);

/// <summary>
/// Where a person may stand about. A spot holds a handful of people, and the next person
/// goes on to another: the home corner, then the town's shops, inns and docks. The bank
/// plaza keeps its standing crowd and room for only a couple of idlers beside it; a
/// hundred people once stood in the Britain bank because every stay began where the last
/// errand ended. Pure. No world objects.
/// </summary>
public static class LoiterSpotRules
{
    /// <summary>People within this many tiles of a spot stand at it.</summary>
    public const int SpotRadius = 3;

    /// <summary>People one spot holds before the next person goes elsewhere.</summary>
    public const int SpotCap = 5;

    /// <summary>Idlers the bank plaza holds beside its standing crowd.</summary>
    public const int PlazaIdlerCap = 2;

    public const int NoSpot = -1;

    /// <summary>
    /// A person told to idle where it stands that already loiters keeps that stand. The
    /// lifecycle pulse idles an off-hours fixture every minute; dropping the routine each
    /// time restarted its step, and one hider rejoined the bank crowd sixty times.
    /// </summary>
    public static bool AlreadyLoitering(string currentSkill, bool needsNext) =>
        !needsNext && currentSkill == SkillKinds.Loiter;

    public static bool IsFull(LoiterSpotLook look) =>
        look.Others >= (look.OnBankPlaza ? PlazaIdlerCap : SpotCap);

    /// <summary>
    /// The first spot in the list that is not full, or else the least full one. The list is in
    /// the person's order of preference. <see cref="NoSpot"/> for an empty list.
    /// </summary>
    public static int Pick(IReadOnlyList<LoiterSpotLook> looks)
    {
        var least = NoSpot;

        for (var i = 0; i < (looks?.Count ?? 0); i++)
        {
            if (!IsFull(looks[i]))
            {
                return i;
            }

            if (least == NoSpot || Load(looks[i]) < Load(looks[least]))
            {
                least = i;
            }
        }

        return least;
    }

    // How full a spot is against its own cap, so a plaza and a street compare fairly.
    private static double Load(LoiterSpotLook look) =>
        (double)look.Others / (look.OnBankPlaza ? PlazaIdlerCap : SpotCap);
}
