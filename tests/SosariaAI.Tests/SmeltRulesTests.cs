using Server;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class SmeltRulesTests
{
    private const int MediumOre = 10;
    private const int SmallOdd = 5;

    [Fact]
    public void IngotsFrom_SmallLargeAndMedium()
    {
        Assert.Equal(MediumOre / SmeltRules.SmallOrePerIngot, SmeltRules.IngotsFrom(SmeltRules.SmallOreItemId, MediumOre));
        Assert.Equal(MediumOre * SmeltRules.LargeIngotsPerOre, SmeltRules.IngotsFrom(SmeltRules.LargeOreItemId, MediumOre));
        Assert.Equal(MediumOre, SmeltRules.IngotsFrom(0x19B8, MediumOre));
        Assert.Equal(0, SmeltRules.IngotsFrom(SmeltRules.SmallOreItemId, 1));
    }

    [Fact]
    public void ConsumeAmount_SmallOreDropsTheOddPiece()
    {
        Assert.Equal(SmallOdd - 1, SmeltRules.ConsumeAmount(SmeltRules.SmallOreItemId, SmallOdd));
        Assert.Equal(MediumOre, SmeltRules.ConsumeAmount(SmeltRules.LargeOreItemId, MediumOre));
    }

    [Fact]
    public void KnowsHow_IronIsAlwaysKnown()
    {
        Assert.True(SmeltRules.KnowsHow(0, SmeltRules.IronDifficulty));
        Assert.False(SmeltRules.KnowsHow(40, 65));
        Assert.True(SmeltRules.KnowsHow(65, 65));
    }

    [Fact]
    public void AfterFailure_HalvesAPile()
    {
        Assert.Equal(MediumOre / 2, SmeltRules.AfterFailure(MediumOre));
        Assert.Equal(1, SmeltRules.AfterFailure(1));
    }

    [Fact]
    public void FindNearestForge_NullMap_IsNone()
    {
        Assert.Equal(Point3D.Zero, SmeltRules.FindNearestForge(null, Point3D.Zero));
        Assert.Empty(SmeltRules.ForgesNear(null, Point3D.Zero, SmeltRules.ForgeSearchRange));
        Assert.Empty(StaticForges.Near(null, Point3D.Zero, SmeltRules.ForgeSearchRange));
    }

    [Fact]
    public void CellSpan_CoversTheReachAndStaysOnTheMap()
    {
        const int MapTiles = 7168;
        const int Reach = 16;
        var lastCell = (MapTiles - 1) / StaticForges.CellTiles;

        Assert.Equal((0, 0), StaticForges.CellSpan(Reach, Reach, MapTiles));
        Assert.Equal((0, 1), StaticForges.CellSpan(StaticForges.CellTiles - 1, Reach, MapTiles));
        Assert.Equal((0, 0), StaticForges.CellSpan(0, Reach, MapTiles));
        Assert.Equal((lastCell, lastCell), StaticForges.CellSpan(MapTiles - 1, Reach, MapTiles));
    }

    [Fact]
    public void UnreadCellsPerCall_ReadsTheSmeltReachWholeInOneCall()
    {
        // A smelt check beside a forge must never see half the map art round it.
        const int MapTiles = 7168;

        for (var at = 0; at < StaticForges.CellTiles * 2; at++)
        {
            var (first, last) = StaticForges.CellSpan(at, SmeltRules.ForgeSearchRange, MapTiles);
            var cellsPerSide = last - first + 1;

            Assert.True(cellsPerSide * cellsPerSide <= StaticForges.UnreadCellsPerCall);
        }
    }

    [Fact]
    public void CellDistance_CountsWholeCellsFromTheCellUnderfoot()
    {
        var from = new Point3D(StaticForges.CellTiles + 1, StaticForges.CellTiles + 1, 0);

        Assert.Equal(0, StaticForges.CellDistance(from, 1, 1));
        Assert.Equal(1, StaticForges.CellDistance(from, 0, 2));
        Assert.Equal(2, StaticForges.CellDistance(from, 3, 1));
    }

    [Fact]
    public void IsForgeId_MasksStaticTileFlags()
    {
        Assert.True(SmeltRules.IsForgeId(SmeltRules.ClassicForgeItemId));
        Assert.True(SmeltRules.IsForgeId(SmeltRules.ClassicForgeItemId | 0x4000));
        Assert.False(SmeltRules.IsForgeId(1449));
    }

    [Fact]
    public void IsForgeId_MatchesModernUOBlacksmithy()
    {
        Assert.True(SmeltRules.IsForgeId(SmeltRules.ClassicForgeItemId));
        Assert.True(SmeltRules.IsForgeId(SmeltRules.FireForgeIdMin));
        Assert.True(SmeltRules.IsForgeId(SmeltRules.FireForgeIdMax));
        Assert.True(SmeltRules.IsForgeId(SmeltRules.ElvenForgeItemId));
        Assert.False(SmeltRules.IsForgeId(1));
    }

    [Fact]
    public void ForgeSpots_NearestFirst_OnePerSmithy()
    {
        // Britain from the mine: the smithy forge, the west forge, and a smithy with a
        // forge and two bellows side by side.
        var mine = new Point3D(1446, 1508, 40);
        var smithy = new Point3D(1424, 1558, 30);
        var west = new Point3D(1361, 1574, 30);
        var bellows = new Point3D(1353, 1779, 15);
        var forge = new Point3D(1354, 1779, 15);
        var bellows2 = new Point3D(1355, 1779, 15);

        var spots = SmeltRules.ForgeSpots(mine, [bellows, west, forge, smithy, bellows2], limit: 3);

        Assert.Equal([smithy, west, bellows], spots);
        Assert.Equal([smithy], SmeltRules.ForgeSpots(mine, [west, smithy], limit: 1));
        Assert.Empty(SmeltRules.ForgeSpots(mine, null, limit: 3));
    }
}
