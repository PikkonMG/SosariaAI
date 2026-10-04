using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Deliberation;

/// <summary>One next-job question out to Jev: the options it saw, so the answer maps back to actions.</summary>
public sealed record JevJobAsk(long RequestId, DateTime AskedAt, IReadOnlyList<JobOption> Options, string ProviderName = null);

/// <summary>
/// The next-job questions out to Jev, one per character, when each character last asked, and
/// the big moment each is in. Game loop only. A character with an ask out waits; an ask older
/// than the answer wait is dropped, and its late answer finds nothing to apply.
/// </summary>
public sealed class JevJobDesk
{
    private readonly Dictionary<Serial, string> _moments = new();
    private readonly Dictionary<Serial, JevJobAsk> _open = new();
    private readonly HashSet<Serial> _expired = new();
    private readonly CharacterCooldown _asked = new();

    public void Open(Serial serial, JevJobAsk ask)
    {
        _open[serial] = ask;
        _expired.Remove(serial);
        _asked.Mark(serial, ask.AskedAt);
    }

    public bool CoolingDown(Serial serial, DateTime now, TimeSpan cooldown) => _asked.IsCooling(serial, now, cooldown);

    /// <summary>True while an ask is out and still inside the wait; an overdue ask is dropped and noted.</summary>
    public bool Waiting(Serial serial, DateTime now, TimeSpan wait)
    {
        if (!_open.TryGetValue(serial, out var ask))
        {
            return false;
        }

        if (now - ask.AskedAt < wait)
        {
            return true;
        }

        _open.Remove(serial);
        _expired.Add(serial);
        return false;
    }

    /// <summary>True once after an ask ran out of time, so the scorer's pick says why.</summary>
    public bool TakeExpired(Serial serial) => _expired.Remove(serial);

    /// <summary>The open ask this answer belongs to; a stale or unknown answer takes nothing.</summary>
    public bool TryTake(Serial serial, long requestId, out JevJobAsk ask)
    {
        if (_open.TryGetValue(serial, out ask) && ask.RequestId == requestId)
        {
            _open.Remove(serial);
            return true;
        }

        ask = null;
        return false;
    }

    /// <summary>
    /// True once when a character comes into a big moment: back from death stays one moment
    /// until the death is no longer recent, a player near stays one until the player leaves. A
    /// pick with no moment ends the last one.
    /// </summary>
    public bool NewMoment(Serial serial, string moment)
    {
        if (moment == null)
        {
            _moments.Remove(serial);
            return false;
        }

        if (_moments.TryGetValue(serial, out var last) && last == moment)
        {
            return false;
        }

        _moments[serial] = moment;
        return true;
    }

    /// <summary>Something else took over the character; its open ask no longer applies.</summary>
    public void Forget(Serial serial)
    {
        _open.Remove(serial);
        _expired.Remove(serial);
    }
}
