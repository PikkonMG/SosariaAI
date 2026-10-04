using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestScanTests
{
    [Fact]
    public void NearFirst_StartsAtTheWorker_AndStaysInTheArea()
    {
        var area = new Rectangle2D(1000, 1000, 80, 80);
        var tiles = HarvestScan.NearFirst(new Point3D(1040, 1040, 0), area);

        Assert.True(tiles.Count > 0);
        Assert.True(tiles.Count <= HarvestScan.MaxTiles);
        Assert.Equal(new Point2D(1040, 1040), tiles[0]);
        Assert.All(tiles, tile => Assert.True(area.Contains(tile)));
    }

    [Fact]
    public void NearFirst_DoesNotScanTheWholeMountain()
    {
        var area = new Rectangle2D(2000, 1000, 180, 180);
        var tiles = HarvestScan.NearFirst(new Point3D(2090, 1090, 0), area);

        Assert.Equal(HarvestScan.MaxTiles, tiles.Count);
        Assert.True(tiles.Count < area.Width * area.Height);
    }
}
