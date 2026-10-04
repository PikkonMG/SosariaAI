using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Social;
using SosariaAI.Spawning;

namespace SosariaAI.Mobiles;

/// <summary>
/// How the character sounds: its own persona written by the chat model, its typing habits,
/// and a check that it does not repeat an idle line someone nearby just said. Nothing here
/// is saved: the persona file is on disk and the typing profile is rolled from the id.
/// </summary>
public partial class SosariaCharacter
{
    // Mixes the id into the line count so two people on the same count type differently.
    private const int VoiceSeedPrime = 16777619;

    private TypingProfile _typingProfile;
    private int _voiceLines;

    /// <summary>This person's typing habits, rolled from the character id.</summary>
    public TypingProfile TypingProfile
    {
        get
        {
            if (_typingProfile == null && !string.IsNullOrWhiteSpace(CharacterId))
            {
                _typingProfile = TypingProfile.For(CharacterId);
            }

            return _typingProfile ?? TypingProfile.Plain;
        }
    }

    /// <summary>
    /// A copy takes its saved personal persona, or joins the queue to have one written.
    /// Call on bind after the persona is composed and the name, looks and guild are set.
    /// A fixture keeps its authored persona.
    /// </summary>
    public void BindVoice()
    {
        if (!WorkSites.IsCopy(CharacterId))
        {
            return;
        }

        var personal = PersonaWriter.Find(CharacterId, EraBands.Current());

        if (personal != null)
        {
            TakePersonalPersona(personal);
            return;
        }

        PersonaWriter.Want(this);
    }

    /// <summary>The written words over the composed persona's drives, hours and temper.</summary>
    internal void TakePersonalPersona(PersonaDraft draft) => Persona = draft.ToPersona(Persona);

    /// <summary>Who this person is, for the persona writer.</summary>
    internal PersonaFacts VoiceFacts() =>
        new(
            Name,
            Female,
            PersonProfile.Class,
            PersonProfile.Tier,
            PersonProfile.Traits,
            PersonProfile.Wealth,
            HomeTownName(),
            Guild?.Abbreviation,
            EraBands.Current()
        );

    /// <summary>
    /// The line as this person types it, or null when it should not be said. A scripted line
    /// (trade, combat, guard call) is always said and keeps its numbers and capitalised names.
    /// An ambient line that someone within earshot said in the last minutes becomes another
    /// line from the same persona pool, or stays unsaid unless the character is talking to
    /// someone — a <paramref name="directed"/> reply counts as talking to someone. Staff text
    /// is left as it is.
    /// </summary>
    internal string ShapeForVoice(string line, bool scripted, bool directed = false)
    {
        if (string.IsNullOrWhiteSpace(line) || AccessLevel > AccessLevel.Player)
        {
            return line;
        }

        var now = Core.Now;
        var spoken = scripted || Map == null ? line : FreshLine(line, now, directed);

        if (spoken == null)
        {
            return null;
        }

        if (Map != null)
        {
            HeardLines.Shared.Record(Map.MapID, X, Y, spoken, now);
        }

        return TypingStyle.Apply(spoken, TypingProfile, NextVoiceSeed(), guarded: scripted);
    }

    /// <summary>Says the shaped line, or nothing when <see cref="ShapeForVoice"/> held it back.</summary>
    private void DeliverInVoice(string line, bool scripted, bool directed = false)
    {
        var shaped = ShapeForVoice(line, scripted, directed);

        if (!string.IsNullOrEmpty(shaped))
        {
            Deliver(shaped);
        }
    }

    private string FreshLine(string line, DateTime now, bool directed)
    {
        var heard = HeardLines.Shared;

        if (!heard.HeardNear(Map.MapID, X, Y, line, now))
        {
            return line;
        }

        var others = Persona?.AlternativesTo(line, _voiceLines, Routine?.CurrentSkill?.Name);

        for (var i = 0; others != null && i < others.Count; i++)
        {
            if (!heard.HeardNear(Map.MapID, X, Y, others[i], now) &&
                !SpokenRepeat.IsNearRepeat(others[i], Memory.Working.RecentSpeech))
            {
                return others[i];
            }
        }

        return directed || Conversation.IsActive(now) ? line : null;
    }

    private int NextVoiceSeed() =>
        unchecked(WorkSites.StableIndex(CharacterId) * VoiceSeedPrime + _voiceLines++);

    private string HomeTownName()
    {
        if (string.IsNullOrWhiteSpace(HomeFacet) || !Map.TryParse(HomeFacet, null, out var map) || map == null)
        {
            return null;
        }

        for (var region = Region.Find(HomeSpot, map); region != null; region = region.Parent)
        {
            if (!string.IsNullOrWhiteSpace(region.Name))
            {
                return region.Name;
            }
        }

        return null;
    }
}
