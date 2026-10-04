using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class TileGridTests
{
    [Fact]
    public void Neighbours_AreTheEightTilesAround()
    {
        var neighbours = TileGrid.Neighbours;

        Assert.Equal(8, neighbours.Length);

        foreach (var (x, y) in neighbours)
        {
            Assert.True(x is >= -1 and <= 1 && y is >= -1 and <= 1 && (x, y) != (0, 0));
        }
    }
}
