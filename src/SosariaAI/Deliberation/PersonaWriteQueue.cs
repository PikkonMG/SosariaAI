using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

/// <summary>
/// Copies waiting for their persona to be written, oldest first, each once. The writer
/// takes a few a minute, so a full population fills in over hours instead of in one burst.
/// A copy stays marked from the time it is queued until its one answer is in, so a rebind
/// while it waits or while its call is out does not ask twice.
/// </summary>
public sealed class PersonaWriteQueue
{
    private readonly Queue<(string CharacterId, Serial Serial)> _waiting = new();
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);

    public int Waiting => _waiting.Count;

    /// <summary>Queues the copy. False when it is already waiting, out, or done this boot.</summary>
    public bool Add(string characterId, Serial serial)
    {
        if (string.IsNullOrWhiteSpace(characterId) || !_known.Add(characterId))
        {
            return false;
        }

        _waiting.Enqueue((characterId, serial));
        return true;
    }

    /// <summary>The next waiting copy. The copy stays marked, so it is not queued again this boot.</summary>
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
}
