using System;

namespace SosariaAI.Memory;

/// <summary>
/// One queued change for <c>memory.db</c>. Each write carries a frozen copy of its row, so the
/// writer thread never reads live game state. Every write is safe to run again after a failed
/// batch rolls back.
/// </summary>
internal abstract record MemoryWrite;

/// <summary>Upserts one <c>people</c> row.</summary>
internal sealed record PersonWrite(PersonRef Person, DateTime LastSeenAt) : MemoryWrite;

/// <summary>Upserts one <c>bonds</c> row.</summary>
internal sealed record BondWrite(Bond Bond) : MemoryWrite;

/// <summary>Upserts one <c>adventures</c> row and replaces its <c>adventure_members</c> rows.</summary>
internal sealed record AdventureWrite(Adventure Adventure) : MemoryWrite;

/// <summary>Adds one telling to an adventure.</summary>
internal sealed record TellWrite(long AdventureId) : MemoryWrite;

/// <summary>Adds one <c>places_seen</c> row unless it is there.</summary>
internal sealed record PlaceWrite(string OwnerId, string Place, DateTime FirstAt) : MemoryWrite;

/// <summary>Upserts one <c>shard_news</c> row.</summary>
internal sealed record NewsWrite(NewsRow Row) : MemoryWrite;

/// <summary>Deletes every faded bond and adventure, and news too old to tell.</summary>
internal sealed record FadeWrite(DateTime Now) : MemoryWrite;

/// <summary>A frozen copy of a shard event, its tellings and its tellers.</summary>
internal sealed record NewsRow(
    string Id,
    DateTime At,
    string Type,
    string Actor,
    string Other,
    string Place,
    string Facet,
    int X,
    int Y,
    int Z,
    int TellCount,
    DateTime LastToldAt,
    string[] Tellers
);
