using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Deliberation;

/// <summary>
/// Gives each copy a persona of its own, written once by the chat model. A copy that binds
/// with no file in personas-generated/ joins a queue; once a minute the writer sends at most
/// <see cref="BrainConfiguration.PersonaWritesPerMinute"/> of them, inside the paid caps and
/// the in-flight limit, so a full population fills in over hours. A checked answer is saved
/// and read on every later boot; a failed one leaves the composed persona, and the copy is
/// not asked about again until the next boot. Fixtures keep their authored persona.
/// With <see cref="BrainConfiguration.PersonaWriter"/> off it neither writes nor reads, and
/// every copy keeps the persona composed from the part library. Game thread only.
/// </summary>
public static class PersonaWriter
{
    public static readonly TimeSpan DrainInterval = TimeSpan.FromMinutes(1);

    private static readonly ILogger logger = SosariaLog.For(typeof(PersonaWriter));
    private static readonly PersonaWriteQueue _queue = new();
    private static readonly Dictionary<long, string> _out = new();
    private static Dictionary<string, PersonaDraft> _saved = new(StringComparer.OrdinalIgnoreCase);
    private static string _directory;
    private static int _perMinute;
    private static bool _enabled;
    private static bool _drainSet;

    /// <summary>
    /// Reads the saved personas when the writer is on. Called from <see cref="Brain.Configure"/>,
    /// whether or not the Brain is on.
    /// </summary>
    public static void Configure(string directory, int writesPerMinute, bool enabled)
    {
        _directory = directory;
        _perMinute = Math.Max(0, writesPerMinute);
        _enabled = enabled;
        _saved = enabled
            ? PersonasGeneratedFile.Load(directory)
            : new Dictionary<string, PersonaDraft>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when a copy with no saved persona may join the queue: the writer is on with a rate above 0.</summary>
    internal static bool WritesNew => _enabled && _perMinute > 0 && _directory != null;

    /// <summary>The saved persona for this copy when it passes the checks for <paramref name="band"/>, else null.</summary>
    public static PersonaDraft Find(string characterId, EraBand band)
    {
        if (string.IsNullOrWhiteSpace(characterId) || !_saved.TryGetValue(characterId, out var draft))
        {
            return null;
        }

        var clean = PersonaDraftRules.Vet(draft, band, out var reason);

        if (clean == null && SosariaSettings.LogActivity)
        {
            logger.Information("Saved persona for {Id} is not used: {Reason}", characterId, reason);
        }

        return clean;
    }

    /// <summary>Queues a copy that has no persona file. Nothing happens when writing is off.</summary>
    public static void Want(SosariaCharacter character)
    {
        var id = character?.CharacterId;

        if (!WritesNew || !Brain.IsEnabled || !WorkSites.IsCopy(id) ||
            PersonasGeneratedFile.Exists(_directory, id) || !_queue.Add(id, character.Serial))
        {
            return;
        }

        ScheduleDrain();
    }

    /// <summary>The model's answer, from <see cref="Brain"/>. False when the result is not a persona write.</summary>
    internal static bool Receive(BrainResult result)
    {
        if (result == null || !_out.Remove(result.RequestId, out var id))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            Note("Persona for {Id} was not written: {Detail}", id, result.Error);
            return true;
        }

        var band = EraBands.Current();
        var clean = PersonaDraftRules.Vet(PersonaDraftRules.Parse(result.Raw), band, out var reason);

        if (clean == null)
        {
            Note("Persona for {Id} was rejected: {Detail}", id, reason);
            return true;
        }

        clean.CharacterId = id;
        clean.Era = EraBands.Tag(band);

        if (!PersonasGeneratedFile.SaveIfMissing(_directory, clean))
        {
            Note("Persona for {Id} was not saved: {Detail}", id, "a file is already there");
            return true;
        }

        _saved[id] = clean;
        Note("Persona for {Id} was written: {Detail}", id, clean.Background);

        if (World.FindMobile(result.CharacterSerial) is SosariaCharacter { Deleted: false } character &&
            string.Equals(character.CharacterId, id, StringComparison.OrdinalIgnoreCase))
        {
            character.TakePersonalPersona(clean);
        }

        return true;
    }

    private static void ScheduleDrain()
    {
        if (_drainSet)
        {
            return;
        }

        _drainSet = true;
        Timer.StartTimer(DrainInterval, Drain);
    }

    private static void Drain()
    {
        _drainSet = false;

        // No chat provider this boot: the queue waits, and no timer runs for it.
        if (!Brain.HasChatWriter)
        {
            return;
        }

        var now = Core.Now;
        var sent = 0;

        while (sent < _perMinute && Brain.PersonaWriteReady(now) && _queue.TryTake(out var id, out var serial))
        {
            if (World.FindMobile(serial) is not SosariaCharacter { Deleted: false } character ||
                !string.Equals(character.CharacterId, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var facts = character.VoiceFacts();
            var requestId = Brain.RequestPersonaWrite(
                character,
                PersonaPrompt.SystemMessage(facts.Band),
                PersonaPrompt.UserMessage(facts)
            );

            if (requestId <= 0)
            {
                _queue.PutBack(id, serial);
                break;
            }

            _out[requestId] = id;
            sent++;
        }

        if (_queue.Waiting > 0)
        {
            ScheduleDrain();
        }
    }

    private static void Note(string template, string id, string detail)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(template, id, detail);
        }
    }
}
