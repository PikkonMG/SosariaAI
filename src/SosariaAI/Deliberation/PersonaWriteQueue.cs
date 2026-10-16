using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

/// <summary>
/// Copies waiting for their persona to be written, oldest first. The writer takes a few a
/// minute, so a full population fills in over hours instead of in one burst. A copy stays
/// marked from the time it is queued, so a rebind while it waits or while its call is out
/// does not ask twice. A failed answer queues the copy again, up to <see cref="MaxTries"/>
/// answers in all; a copy that is gone when its turn comes is unmarked.
/// </summary>
public sealed class PersonaWriteQueue
{
    /// <summary>The most answers one copy gets in one boot; after that it keeps its composed persona.</summary>
    public const int MaxTries = 3;

    private readonly Queue<(string CharacterId, Serial Serial)> _waiting = new();
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);

    public int Waiting => _waiting.Count;

    /// <summary>Queues the copy. False when it is already waiting, out, written, or out of tries this boot.</summary>
    public bool Add(string characterId, Serial serial)
    {
        if (string.IsNullOrWhiteSpace(characterId) || !_known.Add(characterId))
        {
            return false;
        }

        _waiting.Enqueue((characterId, serial));
        return true;
    }

    /// <summary>The next waiting copy. The copy stays marked, so it is not queued again unless <see cref="Release"/> unmarks it.</summary>
    public bool TryTake(out string characterId, out Serial serial)
    {
        if (_waiting.TryDequeue(out var next))
        {
            (characterId, serial) = next;
            return true;
        }

        characterId = null;
        serial = Serial.Zero;
        return false;
    }

    /// <summary>Puts a taken copy back at the end when its call could not go out.</summary>
    public void PutBack(string characterId, Serial serial) => _waiting.Enqueue((characterId, serial));

    /// <summary>
    /// A taken copy's answer failed or was rejected. It goes to the back of the queue until it
    /// has failed <see cref="MaxTries"/> times; then it stays marked until the next boot. True
    /// when it was queued again.
    /// </summary>
    public bool Retry(string characterId, Serial serial)
    {
        var failures = _failures.GetValueOrDefault(characterId) + 1;
        _failures[characterId] = failures;

        if (failures >= MaxTries)
        {
            return false;
        }

        _waiting.Enqueue((characterId, serial));
        return true;
    }

    /// <summary>Unmarks a taken copy that is gone or bound to another character, so it is queued again when it binds again.</summary>
    public void Release(string characterId) => _known.Remove(characterId);
}
