using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

public sealed class Persona
{
    public const string NeutralId = "neutral";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("background")]
    public string Background { get; set; }

    [JsonPropertyName("voice")]
    public string Voice { get; set; }

    [JsonPropertyName("likes")]
    public List<string> Likes { get; set; } = [];

    [JsonPropertyName("dislikes")]
    public List<string> Dislikes { get; set; } = [];

    [JsonPropertyName("idleLines")]
    public List<string> IdleLines { get; set; } = [];

    [JsonPropertyName("greetings")]
    public List<string> Greetings { get; set; } = [];

    [JsonPropertyName("returnLines")]
    public List<string> ReturnLines { get; set; } = [];

    [JsonPropertyName("combatLines")]
    public List<string> CombatLines { get; set; } = [];

    [JsonPropertyName("lootLines")]
    public List<string> LootLines { get; set; } = [];

    [JsonPropertyName("drives")]
    public Dictionary<string, double> Drives { get; set; }

    [JsonPropertyName("disposition")]
    public string Disposition { get; set; }

    /// <summary>Jobs this voice fits: worker, fighter, thief, tamer. Empty fits none but its own template.</summary>
    [JsonPropertyName("jobs")]
    public List<string> Jobs { get; set; } = [];

    /// <summary>Era bands this voice fits: t2a, ml, modern. Empty fits every era.</summary>
    [JsonPropertyName("eras")]
    public List<string> Eras
    {
        get;
        set => field = PersonaEras.OrNull(value);
    }

    public PersonaDrives ResolvedDrives() => PersonaDrives.From(Drives);

    [JsonPropertyName("activeStartHour")]
    public int? ActiveStartHour { get; set; }

    [JsonPropertyName("activeEndHour")]
    public int? ActiveEndHour { get; set; }

    public static Persona CreateNeutral() =>
        new()
        {
            Id = NeutralId,
            DisplayName = null,
            Background = "A labourer in Sosaria. Keeps to the work at hand.",
            Voice =
                "Short sentences. Plain speech. Never uses modern slang. Never mentions being an AI, a bot, a program, or a game.",
            Likes = ["honest work", "a quiet drink"],
            Dislikes = ["trouble", "idle boasts"],
            IdleLines =
            [
                "Anyone seen a good deal on ingots today?",
                "Hard work, this.",
                "Another day of it."
            ],
            Greetings =
            [
                "Morning, {name}.",
                "Well met, {name}."
            ],
            ReturnLines =
            [
                "That was a bad day.",
                "Back to work."
            ]
        };

    public const string NamePlaceholder = "{name}";

    /// <summary>
    /// An idle line that fits what the person does now (<paramref name="doingKind"/>, a
    /// <see cref="SkillKinds"/> name or null): a line about chopping waits for the chopping
    /// (<see cref="WorkLines"/>).
    /// </summary>
    public string PickIdleLine(string doingKind) => Pick(IdleLines, line => IsSayable(line) && WorkLines.FitsWork(line, doingKind));

    public string PickReturnLine() => Pick(ReturnLines, IsSayable);

    public string PickCombatLine() => Pick(CombatLines, IsSayable);

    public string PickLootLine() => Pick(LootLines, IsSayable);

    public const string UnnamedGreetee = "friend";

    public string PickGreeting(string otherName)
    {
        var template = Pick(Greetings, IsSayable);
        return string.IsNullOrEmpty(template) ? null : FillName(template, otherName);
    }

    /// <summary>
    /// The other lines of the pool <paramref name="line"/> came from, starting at a turn set by
    /// <paramref name="offset"/>: idle, return, combat, loot, or a greeting with the same name
    /// filled in. Empty when the line is not one of this persona's written lines. A character
    /// that just heard its line said nearby says one of these instead, so a promise line is
    /// left out here as in every pick, and an idle line that does not fit
    /// <paramref name="doingKind"/> as in <see cref="PickIdleLine"/>.
    /// </summary>
    public List<string> AlternativesTo(string line, int offset, string doingKind)
    {
        var found = new List<string>();

        if (string.IsNullOrWhiteSpace(line))
        {
            return found;
        }

        List<string>[] pools = [IdleLines, ReturnLines, CombatLines, LootLines];

        for (var i = 0; i < pools.Length; i++)
        {
            if (pools[i] != null && pools[i].Contains(line))
            {
                Predicate<string> sayable = ReferenceEquals(pools[i], IdleLines)
                    ? raw => IsSayable(raw) && WorkLines.FitsWork(raw, doingKind)
                    : IsSayable;
                AddTurned(found, pools[i], line, offset, name: null, sayable);
                return found;
            }
        }

        if (TryGreetingName(line, out var greeted))
        {
            AddTurned(found, Greetings, line, offset, greeted, IsSayable);
        }

        return found;
    }

    private static string FillName(string template, string otherName) =>
        template.Replace(
            NamePlaceholder,
            string.IsNullOrEmpty(otherName) ? UnnamedGreetee : otherName,
            StringComparison.OrdinalIgnoreCase
        );

    // The greeting template the line was filled from, and the name in it.
    private bool TryGreetingName(string line, out string name)
    {
        name = null;

        if (Greetings == null)
        {
            return false;
        }

        for (var i = 0; i < Greetings.Count; i++)
        {
            var template = Greetings[i];

            if (string.IsNullOrEmpty(template))
            {
                continue;
            }

            var at = template.IndexOf(NamePlaceholder, StringComparison.OrdinalIgnoreCase);

            if (at < 0)
            {
                if (string.Equals(template, line, StringComparison.OrdinalIgnoreCase))
                {
                    name = UnnamedGreetee;
                    return true;
                }

                continue;
            }

            var before = template[..at];
            var after = template[(at + NamePlaceholder.Length)..];

            if (line.Length > before.Length + after.Length &&
                line.StartsWith(before, StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith(after, StringComparison.OrdinalIgnoreCase))
            {
                name = line[before.Length..^after.Length];
                return true;
            }
        }

        return false;
    }

    private static void AddTurned(
        List<string> found,
        List<string> pool,
        string line,
        int offset,
        string name,
        Predicate<string> sayable)
    {
        var count = pool.Count;
        var start = count == 0 ? 0 : (offset % count + count) % count;

        for (var n = 0; n < count; n++)
        {
            var raw = pool[(start + n) % count];

            if (string.IsNullOrWhiteSpace(raw) || !sayable(raw))
            {
                continue;
            }

            var filled = name == null ? raw : FillName(raw, name);

            if (!string.Equals(filled, line, StringComparison.OrdinalIgnoreCase) && !found.Contains(filled))
            {
                found.Add(filled);
            }
        }
    }

    /// <summary>
    /// A line that promises nothing: no invite and no trip. A written line is said at random
    /// to whoever stands near, and nothing behind it ever goes along or leaves, so a promise
    /// line is never said. This holds for every preset, generated and hand-edited file.
    /// </summary>
    private static bool IsSayable(string line) => !PromiseLines.IsPromise(line);

    private static string Pick(List<string> lines, Predicate<string> fits)
    {
        var sayable = lines?.FindAll(fits);

        if (sayable == null || sayable.Count == 0)
        {
            return null;
        }

        return sayable[Utility.Random(sayable.Count)];
    }
}
