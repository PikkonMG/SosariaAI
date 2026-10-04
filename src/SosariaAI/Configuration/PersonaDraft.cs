using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

/// <summary>
/// A persona the chat model wrote for one copy. The model returns every field but the id
/// and the era; the writer adds those and saves the draft to
/// personas-generated/&lt;characterId&gt;.json. <see cref="ToPersona"/> lays it over the
/// composed persona, which keeps its drives, hours, jobs and temper.
/// </summary>
public sealed class PersonaDraft
{
    /// <summary>
    /// Leads the wants in the background. The background goes into every chat prompt as who
    /// the person is now, and a bare "Wants:" read as today's plan, so the model said it was
    /// off somewhere it never went. The lead holds no word the ambition seed reads.
    /// </summary>
    public const string WantsLead = " Long hopes, for some far day and not today: ";
    public const string WantsSeparator = "; ";
    public const string SentenceEnd = ".";

    [JsonPropertyName("characterId")]
    public string CharacterId { get; set; }

    /// <summary>The era band tag the draft was written for: t2a, ml, modern.</summary>
    [JsonPropertyName("era")]
    public string Era { get; set; }

    [JsonPropertyName("background")]
    public string Background { get; set; }

    [JsonPropertyName("voice")]
    public string Voice { get; set; }

    [JsonPropertyName("likes")]
    public List<string> Likes { get; set; } = [];

    [JsonPropertyName("dislikes")]
    public List<string> Dislikes { get; set; } = [];

    [JsonPropertyName("wants")]
    public List<string> Wants { get; set; } = [];

    [JsonPropertyName("idleLines")]
    public List<string> IdleLines { get; set; } = [];

    [JsonPropertyName("greetingLines")]
    public List<string> GreetingLines { get; set; } = [];

    [JsonPropertyName("returnLines")]
    public List<string> ReturnLines { get; set; } = [];

    [JsonPropertyName("combatLines")]
    public List<string> CombatLines { get; set; } = [];

    [JsonPropertyName("lootLines")]
    public List<string> LootLines { get; set; } = [];

    /// <summary>
    /// The personal persona: the draft's words over the composed persona's numbers. The wants
    /// join the background as hopes for a later day, where the ambition seed reads them.
    /// </summary>
    public Persona ToPersona(Persona composed)
    {
        var source = composed ?? Persona.CreateNeutral();

        return new Persona
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Jobs = source.Jobs,
            Eras = source.Eras,
            Disposition = source.Disposition,
            Drives = source.Drives,
            ActiveStartHour = source.ActiveStartHour,
            ActiveEndHour = source.ActiveEndHour,
            Background = BackgroundWithWants(),
            Voice = Voice,
            Likes = [.. Likes],
            Dislikes = [.. Dislikes],
            IdleLines = [.. IdleLines],
            Greetings = [.. GreetingLines],
            ReturnLines = [.. ReturnLines],
            CombatLines = [.. CombatLines],
            LootLines = [.. LootLines]
        };
    }

    private string BackgroundWithWants() =>
        Wants.Count == 0
            ? Background
            : Background + WantsLead + string.Join(WantsSeparator, Wants) + SentenceEnd;
}
