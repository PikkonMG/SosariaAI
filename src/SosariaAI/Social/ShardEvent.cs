using System;
using System.Collections.Generic;

namespace SosariaAI.Social;

public sealed class ShardEvent
{
    private readonly HashSet<string> _tellers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Separates the names in <see cref="Other"/> when an event has several (a party run).</summary>
    public const string NameSeparator = ", ";

    /// <summary>The name told for a doer nobody saw.</summary>
    public const string SomeoneName = "someone";

    /// <summary>A stable id, so the saved row of this event is updated in place.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public DateTime At { get; init; }

    public string Type { get; init; }

    public string Actor { get; init; }

    public string Other { get; init; }

    public string Place { get; init; }

    public string Facet { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Z { get; init; }

    public int TellCount { get; set; }

    public DateTime LastToldAt { get; set; }

    /// <summary>
    /// The blues who set out to answer this murder (see <c>ConflictRules.BlueAnswers</c>). Not
    /// saved: after a restart nobody is on the way.
    /// </summary>
    public int Answerers { get; set; }

    /// <summary>Everyone who told this story. Set when a saved event loads.</summary>
    public IReadOnlyCollection<string> Tellers
    {
        get => _tellers;
        init => _tellers.UnionWith(value);
    }

    /// <summary>This person already told the story: nobody tells the same one twice.</summary>
    public bool ToldBy(string teller) => !string.IsNullOrWhiteSpace(teller) && _tellers.Contains(teller);

    /// <summary>One more telling: the count, the time, and the teller who will not tell it again.</summary>
    public void NoteTold(string teller, DateTime now)
    {
        TellCount++;
        LastToldAt = now;

        if (!string.IsNullOrWhiteSpace(teller))
        {
            _tellers.Add(teller);
        }
    }

    public bool IsActor(string name) =>
        !string.IsNullOrWhiteSpace(name) && string.Equals(Actor, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>The named person took part: as the actor, or as one of the others.</summary>
    public bool Involves(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (IsActor(name))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(Other))
        {
            return false;
        }

        foreach (var part in Other.Split(NameSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
