using SosariaAI.Memory;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>A line about an adventure two people really shared, and the adventure it tells.</summary>
public readonly record struct Recollection(string Line, long AdventureId);

/// <summary>
/// Natural greetings used when a persona line is missing or too short to sound like speech,
/// and the lines old friends trade about an adventure they shared. The tone follows the bond;
/// the lines live in the talk library.
/// </summary>
public static class GreetingLines
{
    public const int MinNaturalLength = 12;

    /// <summary>How a greeting names someone whose name is unknown.</summary>
    private const string SomeoneName = "u";

    public static bool IsTooShort(string line) =>
        string.IsNullOrWhiteSpace(line) || line.Trim().Length < MinNaturalLength;

    public static string Pick(int score, string name, string personaLine, int roll)
    {
        if (!IsTooShort(personaLine))
        {
            return personaLine;
        }

        var category = BondRules.IsCold(score) ? TalkCategory.GreetCold
            : BondRules.IsWarm(score) ? TalkCategory.GreetWarm
            : TalkCategory.GreetPlain;
        var who = string.IsNullOrWhiteSpace(name) ? SomeoneName : name;
        return Talk.Line(category, roll < 0 ? 0 : roll, new TalkSlots { Name = who });
    }

    /// <summary>
    /// A line of <paramref name="category"/> about the adventure the teller and the listener
    /// shared (<see cref="Recall.Tellable"/>), filled with the listener, the place, and the deed.
    /// Null when they share none, when the teller's bond to the listener is cold, or when no line fits.
    /// </summary>
    public static Recollection? Recollect(
        MemoryStore store,
        string category,
        string tellerId,
        string listenerId,
        string listenerName,
        int roll
    )
    {
        if (store == null || BondRules.IsCold(store.BondOf(tellerId, listenerId)?.Score ?? BondRules.NeutralScore))
        {
            return null;
        }

        var shared = Recall.Tellable(store, tellerId, listenerId);

        if (shared == null)
        {
            return null;
        }

        var line = Talk.Line(category, roll, SlotsOf(shared, listenerName));
        return string.IsNullOrEmpty(line) ? null : new Recollection(line, shared.Id);
    }

    /// <summary>The talk slots a shared adventure fills: the friend, the place said aloud, and the deed.</summary>
    public static TalkSlots SlotsOf(Adventure shared, string friendName) =>
        new()
        {
            Friend = string.IsNullOrWhiteSpace(friendName) ? null : friendName,
            Place = TalkWords.RememberedPlace(shared.Place),
            Deed = TalkWords.Deed(shared.Kind)
        };
}
