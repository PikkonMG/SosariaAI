using System;

namespace SosariaAI.Combat;

/// <summary>
/// Rats, birds, and sheep are not a reason to flee or to skip a hunt. A type is matched on
/// its last word (GiantRat, TropicalBird), so a Ratman or a Pirate is not taken for a rat.
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
        "cat",
        "dog",
        "goat",
        "cow",
        "horse",
        "llama",
        "pig"
    ];

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
