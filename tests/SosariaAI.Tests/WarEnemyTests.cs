using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class WarEnemyTests : IDisposable
{
    private const int OpenX = 20;
    private const int OpenY = 20;

    private readonly List<Mobile> _placed = [];

    static WarEnemyTests() => Timer.Init(0);

    public WarEnemyTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void IsEnemy_WarOpponent_IsAnEnemyBothWays()
    {
        var first = new SosariaCharacter((Serial)0x7101) { GuildIndex = 40 };
        var second = new SosariaCharacter((Serial)0x7102) { GuildIndex = 41 };
        OutInTheOpen(first);
        OutInTheOpen(second);

        Assert.False(first.IsEnemy(second));

        SosariaSettings.GuildWars.RecordAggression(40, 41, Core.Now);

        Assert.True(first.IsEnemy(second));
        Assert.True(second.IsEnemy(first));
    }

    [Fact]
    public void IsEnemy_WarOpponent_NotOutsideTheOpenWorld()
    {
        var first = new SosariaCharacter((Serial)0x7107) { GuildIndex = 46 };
        var second = new SosariaCharacter((Serial)0x7108) { GuildIndex = 47 };
        SosariaSettings.GuildWars.RecordAggression(46, 47, Core.Now);

        Assert.False(first.IsEnemy(second));
        Assert.False(second.IsEnemy(first));
    }

    // A test land with no guards, banks, healers, moongates or shrines.
    private void OutInTheOpen(SosariaCharacter character)
    {
        character.DefaultMobileInit();
        TestArms.Arm(character);
        character.MoveToWorld(new Point3D(OpenX, OpenY, 0), TestMap.EnsureLand());
        _placed.Add(character);
    }

    [Fact]
    public void IsEnemy_WarOpponent_NotRightAfterDeath()
    {
        var first = new SosariaCharacter((Serial)0x7105) { GuildIndex = 44 };
        var second = new SosariaCharacter((Serial)0x7106) { GuildIndex = 45 };
        SosariaSettings.GuildWars.RecordAggression(44, 45, DateTime.UtcNow);
        first.MarkDeath(DateTime.UtcNow);

        Assert.False(first.IsEnemy(second));
        Assert.False(second.IsEnemy(first));
    }

    [Fact]
    public void IsEnemy_UnguildedOrAtPeace_IsNotAnEnemy()
    {
        var first = new SosariaCharacter((Serial)0x7103) { GuildIndex = 42 };
        var second = new SosariaCharacter((Serial)0x7104) { GuildIndex = 43 };

        Assert.False(first.IsEnemy(second));
        Assert.False(second.IsEnemy(first));
    }
}
