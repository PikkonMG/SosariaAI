using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

public sealed class CharacterCooldown
{
    private readonly Dictionary<Serial, DateTime> _lastCall = new();

    public bool IsCooling(Serial serial, DateTime now, TimeSpan cooldown)
    {
        if (cooldown <= TimeSpan.Zero)
        {
            return false;
        }

        return _lastCall.TryGetValue(serial, out var last) && now - last < cooldown;
    }

    public void Mark(Serial serial, DateTime now) => _lastCall[serial] = now;
}
