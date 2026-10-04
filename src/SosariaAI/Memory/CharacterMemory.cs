using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Memory;

/// <summary>
/// Everything one character remembers. <see cref="Working"/> holds what a session forgets:
/// recent thoughts, recent speech, skill streaks, and the open conversation. Bonds and places
/// seen live in <see cref="MemoryStore.Shared"/>, keyed by the character's
/// <see cref="PersonRef"/>; this facade writes them. Adventures are made by
/// <see cref="AdventureTracker"/>. Main game thread only.
/// </summary>
public sealed class CharacterMemory
{
    private readonly SosariaCharacter _owner;

    public CharacterMemory(SosariaCharacter owner) => _owner = owner;

    /// <summary>Transient working state: recent thoughts, speech heard, skill streaks, the open conversation.</summary>
    public WorkingMemory Working { get; } = new();

    /// <summary>Places this character recently ran from. Route planning steers round them.</summary>
    public DangerSpots Danger { get; } = new();

    /// <summary>Places the router proved this character cannot reach. Trip scoring skips them.</summary>
    public UnreachableSpots Unreachable { get; } = new();

    /// <summary>Moves this character's bond to the person behind the other mobile. A creature has no bond.</summary>
    public void ShiftBond(Mobile other, int delta, string reason) => ShiftBond(PersonRef.Of(other), delta, reason);

    /// <summary>Moves this character's bond to a person, who may have left the world since.</summary>
    public void ShiftBond(PersonRef? other, int delta, string reason)
    {
        if (PersonRef.Of(_owner) is { } self && other is { } person)
        {
            MemoryStore.Shared.ShiftBond(self, person, delta, reason, Core.Now);
        }
    }

    /// <summary>This character met the person behind the other mobile here: the first meeting is kept once.</summary>
    public void Met(Mobile other)
    {
        if (PersonRef.Of(_owner) is { } self && PersonRef.Of(other) is { } person)
        {
            MemoryStore.Shared.NoteMet(self, person, PlaceNames.Of(_owner), Core.Now);
        }
    }

    /// <summary>True when the place is new to this person; the visit is then kept.</summary>
    public bool SawPlace(string place) =>
        PersonRef.Of(_owner) is { } self && MemoryStore.Shared.SawPlace(self.Id, place, Core.Now);

    /// <summary>Every place this person saw.</summary>
    public IReadOnlyList<string> PlacesSeen() =>
        PersonRef.Of(_owner) is { } self ? MemoryStore.Shared.PlacesSeenBy(self.Id) : [];
}
