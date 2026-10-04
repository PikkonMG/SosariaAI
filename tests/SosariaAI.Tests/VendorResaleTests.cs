using System;
using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// What a vendor buys goes on its resale shelf, and the engine clears a piece an hour old only
/// when a client opens the buy list. Simulated sellers open none, and the shard's shelves grew
/// from 2,159 to 13,761 pieces in one night. A sale now clears the shelf by the engine's rule.
/// </summary>
public class VendorResaleTests
{
    private const uint ShelfSerial = 0x5F01;
    private const uint OldPieceSerial = 0x5F02;
    private const uint FreshPieceSerial = 0x5F03;
    private const uint LastHourPieceSerial = 0x5F04;
    private const int FreshMinutes = 5;

    public VendorResaleTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    [Fact]
    public void ClearStaleResale_DropsWhatSatAnHour_AndKeepsTheRest()
    {
        var now = DateTime.UtcNow;
        var shelf = new Backpack((Serial)ShelfSerial) { Movable = true, MaxItems = Container.GlobalMaxItems };
        var old = Piece(OldPieceSerial, now - VendorDeal.ResaleShelfLife - TimeSpan.FromMinutes(FreshMinutes));
        var atTheHour = Piece(LastHourPieceSerial, now - VendorDeal.ResaleShelfLife);
        var fresh = Piece(FreshPieceSerial, now - TimeSpan.FromMinutes(FreshMinutes));
        shelf.AddItem(old);
        shelf.AddItem(atTheHour);
        shelf.AddItem(fresh);

        try
        {
            Assert.Equal(2, VendorDeal.ClearStaleResale(shelf, now));
            Assert.True(old.Deleted);
            Assert.True(atTheHour.Deleted);
            Assert.False(fresh.Deleted);
            Assert.Equal([fresh], shelf.Items);
            Assert.Equal(0, VendorDeal.ClearStaleResale(shelf, now));
        }
        finally
        {
            shelf.Delete();
        }
    }

    [Fact]
    public void ClearStaleResale_NoShelf_DropsNothing()
    {
        Assert.Equal(0, VendorDeal.ClearStaleResale((Mobile)null, DateTime.UtcNow));
    }

    private static SmithHammer Piece(uint serial, DateTime lastMoved) =>
        new((Serial)serial) { Movable = true, Amount = 1, LastMoved = lastMoved };
}
