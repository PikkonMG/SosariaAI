using Server;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SpawnSpreadTests
{
    [Fact]
    public void Offset_FixtureId_KeepsSpawn()
    {
        var spawn = new Point3D(1425, 1695, 0);

        Assert.Equal(spawn, SpawnSpread.Offset(spawn, "kerr"));
        Assert.Equal(spawn, SpawnSpread.Offset(spawn, null));
    }

    [Fact]
    public void Offset_DuplicateId_LeavesTheTile()
    {
        var spawn = new Point3D(1425, 1695, 0);
        var copy = SpawnSpread.Offset(spawn, "kerr#1");

        Assert.NotEqual(spawn, copy);
        Assert.InRange(copy.X, spawn.X - SpawnSpread.DefaultRadius, spawn.X + SpawnSpread.DefaultRadius);
        Assert.InRange(copy.Y, spawn.Y - SpawnSpread.DefaultRadius, spawn.Y + SpawnSpread.DefaultRadius);
        Assert.True(SpawnSpread.DefaultRadius >= 32);
    }

    [Fact]
    public void RadiusFor_SmallCrowd_KeepsDefault()
    {
        Assert.Equal(SpawnSpread.DefaultRadius, SpawnSpread.RadiusFor(1));
        Assert.Equal(SpawnSpread.DefaultRadius, SpawnSpread.RadiusFor(0));
    }

    [Fact]
    public void RadiusFor_BigCrowd_GrowsAndCaps()
    {
        var grown = SpawnSpread.RadiusFor(100);

        Assert.True(grown > SpawnSpread.DefaultRadius, $"crowd of 100 should widen past {SpawnSpread.DefaultRadius}, got {grown}");
        Assert.Equal(SpawnSpread.MaxRadius, SpawnSpread.RadiusFor(100_000));
    }

    [Fact]
    public void Offset_BigCrowd_SpreadsWider()
    {
        var spawn = new Point3D(1425, 1695, 0);
        SpawnSpread.PlannedCount = 2500;

        try
        {
            var radius = SpawnSpread.RadiusFor(SpawnSpread.CrowdPerSite);
            var copy = SpawnSpread.Offset(spawn, "kerr#1");

            Assert.True(radius > SpawnSpread.DefaultRadius);
            Assert.InRange(copy.X, spawn.X - radius, spawn.X + radius);
            Assert.InRange(copy.Y, spawn.Y - radius, spawn.Y + radius);
        }
        finally
        {
            SpawnSpread.PlannedCount = 0;
        }
    }
}
