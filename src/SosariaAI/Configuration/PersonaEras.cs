using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;

namespace SosariaAI.Configuration;

/// <summary>
/// Era tags on personas and persona parts. Untagged content fits every era; a tag list
/// limits it to the bands it names (see <see cref="EraBands"/>).
/// </summary>
public static class PersonaEras
{
    /// <summary>Content from Age of Shadows on: champions, bonding, IDOC, Malas, the zoo.</summary>
    public static List<string> AosOnward() => [EraBands.MLTag, EraBands.ModernTag];

    /// <summary>Content from Stygian Abyss on: gargoyles, High Seas, Eodon, Endless Journey.</summary>
    public static List<string> SaOnward() => [EraBands.ModernTag];

    /// <summary>An empty tag list is kept as null, so the JSON file omits it.</summary>
    public static List<string> OrNull(List<string> eras) => eras is { Count: > 0 } ? eras : null;

    public static bool Fits(Persona persona, EraBand band) => persona != null && EraBands.Fits(persona.Eras, band);

    public static bool Fits(PersonaPart part, EraBand band) => part != null && EraBands.Fits(part.Eras, band);

    /// <summary>
    /// Fixtures named in characters.json whose persona is tagged for other eras. A fixture
    /// keeps its authored persona anyway; the operator only gets a warning.
    /// </summary>
    public static List<(string CharacterId, string PersonaId)> MisfitFixtures(
        CharactersConfiguration characters,
        PersonaCatalog personas,
        EraBand band
    )
    {
        var misfits = new List<(string CharacterId, string PersonaId)>();

        if (characters?.Facets == null || personas == null)
        {
            return misfits;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var facet in characters.Facets.Values)
        {
            var roster = facet?.Roster;

            if (roster == null)
            {
                continue;
            }

            for (var i = 0; i < roster.Count; i++)
            {
                var fixture = roster[i];

                if (fixture == null || string.IsNullOrWhiteSpace(fixture.Id) || string.IsNullOrWhiteSpace(fixture.Persona) ||
                    !seen.Add(fixture.Id))
                {
                    continue;
                }

                var persona = personas.Resolve(fixture.Persona);

                if (!Fits(persona, band))
                {
                    misfits.Add((fixture.Id, persona.Id));
                }
            }
        }

        return misfits;
    }
}
