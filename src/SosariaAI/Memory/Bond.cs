using System;

namespace SosariaAI.Memory;

/// <summary>
/// How one person feels about another, one way: "Halvard about Tamsin" is a different bond
/// from "Tamsin about Halvard". One row of the <c>bonds</c> table.
/// </summary>
public sealed record Bond(
    string OwnerId,
    string OtherId,
    int Score,
    DateTime FirstMetAt,
    string FirstMetPlace,
    DateTime LastSeenAt,
    int SharedCount,
    string LastReason
);
