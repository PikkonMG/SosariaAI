using System;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A look round the shops with nothing on the list, as players drifted through the smithy
/// and the mage shop between errands: a shop kind picked at random, a few minutes on its
/// floor, a word now and then. Pure.
/// </summary>
public static class BrowseRules
{
    public const string Kind = SkillKinds.Browse;

    public const int MinMinutes = 2;
    public const int MaxMinutes = 5;

    /// <summary>The browser strolls this close to where it came in.</summary>
    public const int FloorRadius = 3;

    /// <summary>Stay weight of each stand: a look at the wares, then a few steps.</summary>
    public const int StayWeight = 4;

    public const int LineMinSeconds = 20;
    public const int LineMaxSeconds = 50;

    /// <summary>Out of a hundred, how often the browser says something when its turn comes.</summary>
    public const int LinePercent = 50;

    private const int InclusiveSpanPad = 1;

    /// <summary>The shops worth a look, by the role the world generator records.</summary>
    public static readonly string[] ShopRoles =
    [
        "Smith", "Armorer", "Weaponsmith", "Tailor", "Jeweler", "Mage", "Alchemist", "Provisioner", "Bowyer",
        "Tinker", "Carpenter", "Herbalist"
    ];

    /// <summary>The shop kind for one try: the roll picks where to start, each try the next kind.</summary>
    public static string TokenAt(int roll, int attempt) =>
        DestinationCatalog.VendorTokenPrefix + ShopRoles[(Math.Abs(roll % ShopRoles.Length) + attempt) % ShopRoles.Length];

    public static TimeSpan Length(int roll) =>
        TimeSpan.FromMinutes(MinMinutes + Math.Abs(roll % (MaxMinutes - MinMinutes + InclusiveSpanPad)));

    public static TimeSpan LineGap(int roll) =>
        TimeSpan.FromSeconds(LineMinSeconds + Math.Abs(roll % (LineMaxSeconds - LineMinSeconds + InclusiveSpanPad)));
}
