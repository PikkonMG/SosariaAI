using System;
using System.Collections.Generic;

namespace SosariaAI.Economy;

/// <summary>
/// One order a crafter took: who asked, the item types to make (one, or the six pieces of a
/// suit), the agreed price, the deposit already paid, when it was placed, and the serials of
/// the finished pieces held for the buyer. Ready when every type has its piece. Pure.
/// </summary>
public sealed record CraftOrder(
    string Id,
    uint BuyerSerial,
    string BuyerName,
    IReadOnlyList<string> ItemTypes,
    int Price,
    int Deposit,
    DateTime PlacedAt,
    IReadOnlyList<uint> PieceSerials
)
{
    public bool Ready => PieceSerials.Count >= ItemTypes.Count;

    /// <summary>The gold still owed at pickup.</summary>
    public int Rest => Math.Max(0, Price - Deposit);

    /// <summary>The type of the next piece to make, or null when the order is ready.</summary>
    public string NextType => Ready ? null : ItemTypes[PieceSerials.Count];

    /// <summary>The same order with one more finished piece held for the buyer.</summary>
    public CraftOrder WithPiece(uint serial) => this with { PieceSerials = [.. PieceSerials, serial] };
}
