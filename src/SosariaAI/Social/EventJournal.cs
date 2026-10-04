using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Memory;
using SosariaAI.Navigation;

namespace SosariaAI.Social;

/// <summary>
/// The shard's recent news, for gossip. With a store, every new event and every telling is
/// saved, and <see cref="Restore"/> brings the saved news back after a restart.
/// </summary>
public sealed class EventJournal
{
    private readonly Queue<ShardEvent> _events = new();
    private readonly MemoryStore _store;

    /// <param name="store">Saves the news; null keeps it in RAM only.</param>
    public EventJournal(MemoryStore store = null) => _store = store;

    public void Record(ShardEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        Keep(evt);
        _store?.SaveNews(evt);
    }

    /// <summary>Takes back the saved news still young enough to tell, oldest first.</summary>
    public void Restore(DateTime now)
    {
        if (_store == null)
        {
            return;
        }

        var saved = _store.TakeSavedNews(now);

        for (var i = 0; i < saved.Count; i++)
        {
            Keep(saved[i]);
        }
    }

    private void Keep(ShardEvent evt)
    {
        _events.Enqueue(evt);

        // Murders and deaths warm the shard's danger map where they happened.
        DangerMap.Shared.Note(evt.Facet, new Point3D(evt.X, evt.Y, evt.Z), DangerMap.HeatOf(evt.Type), evt.At);

        while (_events.Count > GossipRules.JournalCapacity)
        {
            _events.Dequeue();
        }
    }

    public IReadOnlyList<ShardEvent> Snapshot() => [.. _events];

    /// <summary>
    /// The news this teller passes on now, or null. Picking it counts as telling it: the
    /// tell count and time move and the teller is marked, so nobody tells the same story twice
    /// and a story fades after a few tellings. A roll over the fade chance keeps far news to itself.
    /// </summary>
    public ShardEvent PickGossip(string speakerName, Point3D speaker, DateTime now, string facet = null, int fadeRoll = 0)
    {
        var best = FindReport(speakerName, speaker, now, facet: facet, tellableOnly: true);

        if (best == null)
        {
            return null;
        }

        var dist = NavMetric.Chebyshev(speaker, new Point2D(best.X, best.Y));
        var detail = GossipRules.DetailLevel(dist, now - best.At, best.Involves(speakerName));

        if (!GossipRules.Retold(detail, fadeRoll))
        {
            return null;
        }

        best.NoteTold(speakerName, now);
        _store?.SaveNews(best);
        return best;
    }

    /// <summary>
    /// The weightiest news this speaker has heard, or null. <paramref name="accepts"/>, when
    /// given, passes over the events the caller would not act on.
    /// </summary>
    public ShardEvent FindReport(
        string speakerName,
        Point3D speaker,
        DateTime now,
        string requiredType = null,
        string facet = null,
        bool tellableOnly = false,
        Func<ShardEvent, bool> accepts = null
    )
    {
        ShardEvent best = null;
        var bestWeight = 0.0;

        foreach (var evt in _events)
        {
            var age = now - evt.At;

            if (age < TimeSpan.Zero || age > GossipRules.MaxAge)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(requiredType) &&
                !string.Equals(evt.Type, requiredType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(facet) &&
                !string.IsNullOrWhiteSpace(evt.Facet) &&
                !string.Equals(evt.Facet, facet, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tellableOnly &&
                (!GossipRules.MayTell(evt.Type, evt.TellCount, evt.LastToldAt, now) || evt.ToldBy(speakerName) ||
                 GossipRules.KeepsQuiet(evt.Type, evt.IsActor(speakerName))))
            {
                continue;
            }

            if (accepts?.Invoke(evt) == false)
            {
                continue;
            }

            var own = evt.Involves(speakerName);
            var dist = NavMetric.Chebyshev(speaker, new Point2D(evt.X, evt.Y));

            if (GossipRules.TooLocal(dist) || !GossipRules.NewsHasArrived(dist, age, own))
            {
                continue;
            }

            var weight = GossipRules.Weight(evt.Type, evt.TellCount, own);

            if (weight > bestWeight)
            {
                bestWeight = weight;
                best = evt;
            }
        }

        return best;
    }

    /// <summary>Murders by this killer still in living memory: a repeat killer gets named.</summary>
    public int KillsBy(string killerName, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(killerName))
        {
            return 0;
        }

        var kills = 0;

        foreach (var evt in _events)
        {
            var age = now - evt.At;

            if (evt.Type == ShardEventType.Pk && age >= TimeSpan.Zero && age <= GossipRules.MaxAge &&
                string.Equals(evt.Other, killerName, StringComparison.OrdinalIgnoreCase))
            {
                kills++;
            }
        }

        return kills;
    }
}
