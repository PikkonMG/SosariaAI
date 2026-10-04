using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RuneShelfTests
{
    private static readonly Point3D BritainBank = new(1434, 1699, 0);

    [Fact]
    public void IndexNearest_PicksTheRuneThatLandsClosestOnThisMap()
    {
        RuneMark[] marks =
        [
            new(new Point3D(1450, 1700, 0), SameMap: true),
            new(new Point3D(1436, 1699, 0), SameMap: false),
            new(new Point3D(1440, 1702, 0), SameMap: true)
        ];

        Assert.Equal(2, RuneShelf.IndexNearest(BritainBank, marks, RuneShelf.NearPlaceTiles));
    }

    [Fact]
    public void IndexNearest_SkipsAMarkTheTravelerMayNotLandOn()
    {
        // A red's old town rune lands under the guards: the farther camp rune serves it.
        RuneMark[] marks =
        [
            new(new Point3D(1436, 1699, 0), SameMap: true, Barred: true),
            new(new Point3D(1450, 1700, 0), SameMap: true)
        ];

        Assert.Equal(1, RuneShelf.IndexNearest(BritainBank, marks, RuneShelf.NearPlaceTiles));
        Assert.Equal(-1, RuneShelf.IndexNearest(BritainBank, marks[..1], RuneShelf.NearPlaceTiles));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Barred_ARedLandsNowhereUnderTheGuards(bool red, bool landsGuarded, bool barred) =>
        Assert.Equal(barred, RuneShelf.Barred(red, landsGuarded));

    [Fact]
    public void BarredFor_NoTraveler_BarsNothing() =>
        Assert.False(RuneShelf.BarredFor(null, BritainBank, null));

    [Fact]
    public void IndexNearest_NoRuneNearThePlace_IsMinusOne()
    {
        RuneMark[] marks = [new(new Point3D(2500, 500, 0), SameMap: true)];

        Assert.Equal(-1, RuneShelf.IndexNearest(BritainBank, marks, RuneShelf.NearPlaceTiles));
        Assert.Equal(-1, RuneShelf.IndexNearest(BritainBank, null, RuneShelf.NearPlaceTiles));
    }

    [Fact]
    public void LandingFor_AGoalInADungeon_LandsAtItsDoor()
    {
        var hall = new Point3D(5243, 1004, 0);
        var door = new Point3D(1176, 2635, 0);

        Assert.Equal(door, RuneShelf.LandingFor(hall, "Destard", door));
    }

    [Fact]
    public void LandingFor_OpenGroundOrAnUnknownDoor_LandsAtTheGoal()
    {
        var hall = new Point3D(5243, 1004, 0);

        Assert.Equal(BritainBank, RuneShelf.LandingFor(BritainBank, null, new Point3D(1176, 2635, 0)));
        Assert.Equal(hall, RuneShelf.LandingFor(hall, "Destard", Point3D.Zero));
    }

    [Fact]
    public void Worldside_NoCharacter_FindsNoRune()
    {
        Assert.Null(RuneShelf.Blank(null));
        Assert.Null(RuneShelf.MarkedNear(null, BritainBank, null));
        Assert.Null(RuneShelf.Book(null));
        Assert.Empty(RuneShelf.LooseMarked(null));
        Assert.False(RuneShelf.CarriesMarked(null));
        Assert.False(RuneShelf.Shelve(null, null));
    }

    [Fact]
    public void TravelMark_NothingToTravelBy_IsNoMark()
    {
        Assert.Null(TravelMark.Of((Server.Items.RecallRune)null));
        Assert.Null(TravelMark.Of(null, null));
    }
}
