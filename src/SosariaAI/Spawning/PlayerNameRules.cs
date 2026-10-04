using System;
using Server.Misc;

namespace SosariaAI.Spawning;

/// <summary>
/// Composes a player-style name for a person. Styles follow what players typed at the
/// character screen: a plain name, a full name, "Name of Town", an epithet, a handle,
/// a lower-case first name, a rare "xX..Xx" and a knightly "Sir". Every name passes the
/// engine's own player-name check. The first free candidate for an id is its name, so
/// a person keeps its name across reboots and no two live people share one.
/// </summary>
public static class PlayerNameRules
{
    public const int PlainWeight = 32;
    public const int FullNameWeight = 24;
    public const int TownWeight = 8;
    public const int EpithetWeight = 7;
    public const int HandleWeight = 14;
    public const int CasualWeight = 9;
    public const int WrappedWeight = 2;
    public const int TitledWeight = 4;

    public const int LowerCaseHandlePercent = 25;
    public const int RolledAttempts = 64;

    public const string TownJoin = " of ";
    public const string WrapOpen = "xX";
    public const string WrapClose = "Xx";
    public const string KnightTitle = "Sir ";
    public const string DameTitle = "Dame ";
    private const string Space = " ";

    private const int NameSalt = 701;
    private const int AttemptStride = 16;
    private const int StylePart = 0;
    private const int FirstPart = 1;
    private const int SecondPart = 2;
    private const int CasePart = 3;
    private const int SweepSalt = 709;

    private static readonly int[] StyleWeights =
    [
        PlainWeight, FullNameWeight, TownWeight, EpithetWeight, HandleWeight, CasualWeight, WrappedWeight, TitledWeight
    ];

    private enum Style
    {
        Plain,
        FullName,
        Town,
        Epithet,
        Handle,
        Casual,
        Wrapped,
        Titled
    }

    /// <summary>
    /// The person's name: the first rolled candidate nobody wears, then a sweep through
    /// every first name and surname pair, which holds tens of thousands of names.
    /// </summary>
    public static string Pick(string uniqueId, bool female, Func<string, bool> isTaken)
    {
        var taken = isTaken ?? (_ => false);

        for (var attempt = 0; attempt < RolledAttempts; attempt++)
        {
            var candidate = Candidate(uniqueId, female, attempt);

            if (IsValid(candidate) && !taken(candidate))
            {
                return candidate;
            }
        }

        var firsts = female ? PlayerNames.Female : PlayerNames.Male;
        var pairs = firsts.Length * PlayerNames.Surnames.Length;
        var start = PersonDice.Roll(uniqueId, SweepSalt, pairs);

        for (var step = 0; step < pairs; step++)
        {
            var index = (start + step) % pairs;
            var candidate = firsts[index % firsts.Length] + Space + PlayerNames.Surnames[index / firsts.Length];

            if (IsValid(candidate) && !taken(candidate))
            {
                return candidate;
            }
        }

        return firsts[start % firsts.Length];
    }

    /// <summary>One rolled candidate. It may be too long or clash; <see cref="Pick"/> skips those.</summary>
    private static string Candidate(string uniqueId, bool female, int attempt)
    {
        var salt = NameSalt + attempt * AttemptStride;
        var first = PersonDice.Pick(uniqueId, salt + FirstPart, female ? PlayerNames.Female : PlayerNames.Male);

        return (Style)PersonDice.Weighted(uniqueId, salt + StylePart, StyleWeights) switch
        {
            Style.Plain => first,
            Style.FullName => first + Space + PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Surnames),
            Style.Town => first + TownJoin + PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Towns),
            Style.Epithet => first + Space + PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Epithets),
            Style.Handle => Handle(uniqueId, salt),
            Style.Casual => PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Casual),
            Style.Wrapped => WrapOpen + PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Handles).Replace(Space, string.Empty) + WrapClose,
            _ => (female ? DameTitle : KnightTitle) + first
        };
    }

    /// <summary>The engine's character-creation rule: 2 to 16 letters with single spaces, dashes and quotes, no staff or skill titles.</summary>
    public static bool IsValid(string name) =>
        !string.IsNullOrWhiteSpace(name) && NameVerification.ValidatePlayerName(name);

    /// <summary>
    /// The short name people call a person by: the first name of "Robard Fairbairn", "Terrin of
    /// Jhelom" and "Gudrun the Green", the name under "Dame Hedda", the handle inside
    /// "xXStormbringerXx". Null when the whole name is what people say: one word, or a handle
    /// such as "Big Tom", whose first word is no name.
    /// </summary>
    public static string CallingName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var whole = name.Trim();

        if (Array.Exists(PlayerNames.Handles, handle => handle.Equals(whole, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var bare = Unwrapped(Untitled(whole));
        var space = bare.IndexOf(Space, StringComparison.Ordinal);
        var called = space < 0 ? bare : bare[..space];

        return called.Length == 0 || called.Equals(whole, StringComparison.OrdinalIgnoreCase) ? null : called;
    }

    private static string Untitled(string name)
    {
        foreach (var title in (string[])[KnightTitle, DameTitle])
        {
            if (name.Length > title.Length && name.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            {
                return name[title.Length..];
            }
        }

        return name;
    }

    private static string Unwrapped(string name) =>
        name.Length > WrapOpen.Length + WrapClose.Length &&
        name.StartsWith(WrapOpen, StringComparison.Ordinal) && name.EndsWith(WrapClose, StringComparison.Ordinal)
            ? name[WrapOpen.Length..^WrapClose.Length]
            : name;

    private static string Handle(string uniqueId, int salt)
    {
        var handle = PersonDice.Pick(uniqueId, salt + SecondPart, PlayerNames.Handles);
        return PersonDice.Chance(uniqueId, salt + CasePart, LowerCaseHandlePercent)
            ? handle.ToLowerInvariant()
            : handle;
    }
}
