using Server;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonReturnRulesTests
{
    private static readonly Point3D LogoutTile = new(5395, 126, 0);
    private static readonly Point3D NearRoom = new(5405, 130, 0);
    private static readonly Point3D FarRoom = new(5440, 150, 0);

    [Fact]
    public void Pick_AQuietLogoutTileWins()
    {
        ReturnSpot[] spots = [new(LogoutTile, true, 0), new(NearRoom, true, 0)];

        Assert.Equal(0, DungeonReturnRules.Pick(spots));
    }

    [Fact]
    public void Pick_AMonsterCrowdSendsItToTheNearestQuietRoom()
    {
        ReturnSpot[] spots = [new(LogoutTile, true, 4), new(NearRoom, true, 1), new(FarRoom, true, 0)];

        Assert.Equal(2, DungeonReturnRules.Pick(spots));
    }

    [Fact]
    public void Pick_NoQuietSpot_TakesTheFewestMonsters()
    {
        ReturnSpot[] spots = [new(LogoutTile, true, 4), new(NearRoom, true, 1), new(FarRoom, true, 3)];

        Assert.Equal(1, DungeonReturnRules.Pick(spots));
    }

    [Fact]
    public void Pick_NothingStandable_IsNoSpot()
    {
        ReturnSpot[] spots = [new(LogoutTile, false, 0)];

        Assert.Equal(DungeonReturnRules.NoSpot, DungeonReturnRules.Pick(spots));
        Assert.Equal(DungeonReturnRules.NoSpot, DungeonReturnRules.Pick(null));
    }

    [Fact]
    public void LoggedBackInLine_IsEasyToCount() =>
        Assert.Equal("Iolo logged back in inside Shame level 2", DungeonReturnRules.LoggedBackInLine("Iolo", "Shame", 2));
}
