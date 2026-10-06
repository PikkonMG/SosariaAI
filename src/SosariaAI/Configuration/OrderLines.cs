using System.Text.RegularExpressions;

namespace SosariaAI.Configuration;

/// <summary>
/// Lines about work made to order, and about repairs. A crafter may offer orders only while it
/// can take one (<see cref="Skills.StationStall"/>, <see cref="Behaviour.OrderDesk"/>), and a
/// shopper may ask for one only when it really orders. No code repairs anything, so a repair line
/// is never said. A smith said "gm smith here, taking orders" with no order desk behind it. Pure.
/// </summary>
public static class OrderLines
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex Offers = new(
        string.Join(
            '|',
            @"\b(taking|takin) orders\b",
            @"\bi take orders\b",
            @"\bmade to order\b",
            @"\bcustom (orders?|work|pieces?)\b",
            @"\b(i can|ill|i'll|i will) make (you|u|ya) (one|some|a|an)\b",
            @"\bcommissions?\b"
        ),
        Options
    );

    private static readonly Regex Asks = new(
        string.Join(
            '|',
            @"\b(you|u|ya) take orders\b",
            @"\b(can|could|would|will) (you|u|ya) (make|craft|forge|sew|build|fletch) (me|us)\b",
            @"\bmake me (a|an|one|some)\b",
            @"\b(craft|forge|sew|fletch) me\b"
        ),
        Options
    );

    // An offer to repair, not a need: "i need to repair" is a person short of a smith.
    private static readonly Regex Repairs = new(
        string.Join(
            '|',
            @"\b(anyone|any1|who) needs? (a |any )?repairs?\b",
            @"\brepairs? (while (you|u) wait|here|cheap|for sale|done)\b",
            @"\b(taking|doing) repairs\b",
            @"\b(i|ill|i'll|i can|can) (repair|fix) (your|ur|yer|you|u)\b"
        ),
        Options
    );

    /// <summary>The speaker offers work to order: "taking orders", "made to order", "i can make you one".</summary>
    public static bool OffersWork(string line) => !string.IsNullOrWhiteSpace(line) && Offers.IsMatch(line);

    /// <summary>The speaker asks for work: "u take orders?", "can you make me a katana".</summary>
    public static bool AsksForWork(string line) => !string.IsNullOrWhiteSpace(line) && Asks.IsMatch(line);

    /// <summary>The speaker offers repairs, which no code does.</summary>
    public static bool ClaimsRepairs(string line) => !string.IsNullOrWhiteSpace(line) && Repairs.IsMatch(line);

    public static bool IsOrderTalk(string line) => OffersWork(line) || AsksForWork(line);
}
