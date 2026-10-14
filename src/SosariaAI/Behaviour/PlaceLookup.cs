using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// The place a player's words name, looked up in one facet's destination catalog: a party
/// leader's "go to the britain bank", or a stranger's "where is trinsic". The words are tried
/// whole, then word by word (<see cref="PartyOrderRules.PlaceTokens"/>), each nearest first. A
/// kind of place said with a named one ("the britain bank", "bank in minoc") is the one of that
/// kind nearest the named place, not the nearest to the speaker: word by word, "britain" alone
/// gave the middle of Britain and "bank" alone the bank next to the asker. Pure.
/// </summary>
public static class PlaceLookup
{
    /// <summary>The destination the words name, or null when the catalog has none of them.</summary>
    public static Destination Resolve(DestinationCatalog catalog, string words, Point3D from)
    {
        if (catalog == null)
        {
            return null;
        }

        Destination named = null;
        string kindToken = null;

        foreach (var token in PartyOrderRules.PlaceTokens(words))
        {
            if (DestinationCatalog.TryParseKind(token, out _, out _))
            {
                kindToken ??= token;
                continue;
            }

            named ??= catalog.Resolve(token, from);
        }

        return kindToken == null ? named : catalog.Resolve(kindToken, named?.Arrival ?? from);
    }
}
