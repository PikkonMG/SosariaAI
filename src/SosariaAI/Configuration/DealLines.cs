using System.Text.RegularExpressions;

namespace SosariaAI.Configuration;

/// <summary>
/// Lines that set a price, settle a sale or ask for its payment. Only a character in a real
/// deal says them (<see cref="Behaviour.TradeSessions"/>): a healer told a player "bandages 5gp
/// each", then "hand over the 50 gold and theyre urs", with no deal behind the words, and the
/// gold the player dropped on it came straight back. Pure.
/// </summary>
public static class DealLines
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex Settles = new(
        string.Join(
            '|',
            @"\bhand (it |them |the gold |the coin |the gp )?over\b",
            @"\b(they're|theyre|they are|its|it's|thats|that's) (urs|yours|ur|yers)\b",
            @"\b(drop|hand|send|give|toss|pass) (me )?(the |ur |your )?(\d+k? ?)?(gold|gp|coin|coins)\b",
            @"\b\d+k? ?(gp|gold|g) (each|ea|apiece|a piece|per)\b",
            @"\b\d+k? ?(gp|gold|g) (gets|buys) (u|you|ya)\b",
            @"(^|[,.!;]\s*)(deal|sold)\b"
        ),
        Options
    );

    /// <summary>The speaker sets a price, settles a sale or asks for the gold: "5gp each", "deal", "hand over the 50 gold".</summary>
    public static bool SettlesSale(string line) => !string.IsNullOrWhiteSpace(line) && Settles.IsMatch(line);
}
