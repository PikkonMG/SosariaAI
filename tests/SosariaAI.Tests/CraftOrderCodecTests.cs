using System;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class CraftOrderCodecTests
{
    private const int Price = 9000;
    private const int Deposit = 4500;
    private const uint Buyer = 0x99;
    private const uint HeldPiece = 0x40000010;
    private static readonly DateTime Placed = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RoundTrip_KeepsEveryField()
    {
        var order = new CraftOrder("o1", Buyer, "Sir Ann|Bold, the", ["PlateChest", "PlateArms"], Price, Deposit, Placed, [HeldPiece]);

        Assert.True(CraftOrderCodec.TryDecode(CraftOrderCodec.Encode(order), out var back));
        Assert.Equal(order.Id, back.Id);
        Assert.Equal(order.BuyerSerial, back.BuyerSerial);
        Assert.Equal("Sir Ann Bold  the", back.BuyerName);
        Assert.Equal(order.ItemTypes, back.ItemTypes);
        Assert.Equal(Price, back.Price);
        Assert.Equal(Deposit, back.Deposit);
        Assert.Equal(Placed, back.PlacedAt);
        Assert.Equal(order.PieceSerials, back.PieceSerials);
    }

    [Theory]
    [InlineData("")]
    [InlineData("o1|x|y")]
    [InlineData("o1|notanumber|Ann|PlateChest|1|1|0|")]
    public void TryDecode_RejectsBrokenLines(string line) => Assert.False(CraftOrderCodec.TryDecode(line, out _));
}
