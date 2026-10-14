using System;

namespace SosariaAI.Combat;

/// <summary>
/// Rats, birds, and sheep are not a reason to flee or to skip a hunt. A type is matched on
/// its last word (GiantRat, TropicalBird), so a Ratman or a Pirate is not taken for a rat. A
/// cat is matched on the whole name: a HellCat and a PredatorHellCat end on the word too, and
/// breathe fire.
/// </summary>
public static class HarmlessCreatures
{
    private static readonly string[] Tokens =
    [
        "rat",
        "bird",
        "chicken",
        "sheep",
        "hind",
        "rabbit",
        "dog",
        "goat",
        "cow",
        "horse",
        "llama",
        "pig"
    ];

    /// <summary>Harmless only under this exact type name: the house cat.</summary>
    private static readonly string[] WholeNames = ["cat"];

    public static bool IsHarmless(string typeName, int score)
    {
        if (score > 0 && score < ThreatRating.MinThreatToFlee && !LooksNamed(typeName))
        {
            return true;
        }

        return LooksNamed(typeName);
    }

    public static bool LooksNamed(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        for (var i = 0; i < WholeNames.Length; i++)
        {
            if (typeName.Equals(WholeNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        for (var i = 0; i < Tokens.Length; i++)
        {
            if (typeName.EndsWith(Tokens[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
