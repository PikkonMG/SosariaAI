using Server;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestAreaTests
{
    private const int MineHomeX = 1450;
    private const int MineHomeY = 1500;
    private const int MineHomeWidth = 50;
    private const int MineHomeHeight = 50;

    [Fact]
    public void SearchOrder_StartsAtHomeThenWidens()
    {
        var home = new Rectangle2D(1376, 1708, 40, 40);

        var order = HarvestArea.SearchOrder(home, HarvestArea.MaxWidenings);

        Assert.Equal(HarvestArea.MaxWidenings + 1, order.Count);
        Assert.Equal(home, order[0]);

        for (var i = 1; i < order.Count; i++)
        {
            var inner = order[i - 1];
            var outer = order[i];
            Assert.True(outer.X < inner.X && outer.Y < inner.Y);
            Assert.True(outer.X + outer.Width > inner.X + inner.Width);
            Assert.True(outer.Y + outer.Height > inner.Y + inner.Height);
        }
    }

    [Fact]
    public void SearchOrder_HomePatchIsFirst()
    {
        var home = new Rectangle2D(MineHomeX, MineHomeY, MineHomeWidth, MineHomeHeight);

        var order = HarvestArea.SearchOrder(home, HarvestArea.MaxWidenings);

        Assert.Equal(HarvestArea.MaxWidenings + 1, order.Count);
        Assert.Equal(home, order[0]);
        Assert.NotEqual(home, order[1]);
    }

    [Fact]
    public void SearchOrder_MineDoesNotWidenNorthIntoTrollCountry()
    {
        var home = new Rectangle2D(
            CharactersFile.DefaultMineAreaX,
            CharactersFile.DefaultMineAreaY,
            CharactersFile.DefaultMineAreaWidth,
            CharactersFile.DefaultMineAreaHeight
        );

        var order = HarvestArea.SearchOrder(home, HarvestArea.MineWidenings);

        Assert.Single(order);
        Assert.Equal(home, order[0]);
        Assert.Equal(CharactersFile.DefaultMineAreaY, order[0].Y);
    }
}
