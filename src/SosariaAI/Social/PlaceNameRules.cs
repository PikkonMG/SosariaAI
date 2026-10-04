using System;

namespace SosariaAI.Social;

/// <summary>
/// The name people give a spot when they tell of it: its region ("Despise", "Britain",
/// "Buccaneer's Den"), else a landmark close by ("Covetous", "Britain Cemetery"), else the
/// nearest town, else the wild. Nobody died "at felucca" or "at AirElemental 1743-589": a facet
/// is not a place and a spawner's catalog name is a handle. Pure.
/// </summary>
public static class PlaceNameRules
{
    /// <summary>What a spot far from every town and landmark is called.</summary>
    public const string Wild = "the wild";

    /// <summary>A landmark this close names the spot.</summary>
    public const int LandmarkRange = 40;

    /// <summary>A town this close names the spot out on its roads.</summary>
    public const int TownRange = 150;

    /// <summary>A public moongate this close names the spot after the gate.</summary>
    public const int GateRange = 8;

    /// <summary>A name shorter than this ("In", "Out", "Pit") is a handle, not a name.</summary>
    public const int MinMarkerLength = 4;

    /// <summary>Marks that only authoring handles carry: coordinates, tags and notes.</summary>
    private static readonly char[] HandleMarks = ['[', ']', '(', ')', ':', '/', '#', '_', '-'];

    private const char WordSeparator = ' ';

    /// <summary>A last word shorter than this is a lettered part of a place ("Area C").</summary>
    private const int MinLastWordLength = 2;

    /// <summary>
    /// A name a person would say aloud: a real name, capitalised like one, with no numbers,
    /// tags, notes or lettered parts ("Area C").
    /// </summary>
    public static bool IsSayableMarker(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        if (trimmed.Length < MinMarkerLength || trimmed.IndexOfAny(HandleMarks) >= 0 || !char.IsUpper(trimmed[0]))
        {
            return false;
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (char.IsDigit(trimmed[i]))
            {
                return false;
            }
        }

        var lastWord = trimmed.LastIndexOf(WordSeparator);
        return lastWord < 0 || trimmed.Length - lastWord - 1 >= MinLastWordLength;
    }

    /// <summary>
    /// A public moongate goes by the town it serves: "the Moonglow gate". Null when the town has
    /// no name, so the spot is named some other way.
    /// </summary>
    public static string GateName(string town) => string.IsNullOrWhiteSpace(town) ? null : $"the {town.Trim()} gate";

    /// <summary>A region with a name of its own. The facet's own name is not a place.</summary>
    public static bool IsNamedRegion(string regionName, string facetName) =>
        !string.IsNullOrWhiteSpace(regionName) &&
        !string.Equals(regionName.Trim(), facetName?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The spoken name of a spot: its region, else a sayable landmark in range, else a town in
    /// range, else <see cref="Wild"/>.
    /// </summary>
    public static string Spoken(
        string regionName,
        string facetName,
        string landmark,
        int landmarkDistance,
        string town,
        int townDistance
    )
    {
        if (IsNamedRegion(regionName, facetName))
        {
            return regionName.Trim();
        }

        if (IsSayableMarker(landmark) && landmarkDistance <= LandmarkRange)
        {
            return landmark.Trim();
        }

        if (IsNamedRegion(town, facetName) && townDistance <= TownRange)
        {
            return town.Trim();
        }

        return Wild;
    }
}
