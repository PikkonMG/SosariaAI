using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HouseSkillTests
{
    [Fact]
    public void SkillKind_IsHouse()
    {
        Assert.Equal("House", SkillKinds.House);
        Assert.Equal(HouseRules.ArchitectPrice, CareerSettings.DefaultHouseGold);
        Assert.True(HouseRules.PlaceArrivalTiles > 0);
    }

    [Fact]
    public void Done_NeedsSerialAndLiveHouse()
    {
        Assert.False(HouseRules.Stands(0, atFeet: true, serialLive: true));
        Assert.False(HouseRules.Stands(1, atFeet: false, serialLive: false));
        Assert.True(HouseRules.Stands(1, atFeet: true, serialLive: false));
        Assert.True(HouseRules.Stands(1, atFeet: false, serialLive: true));
    }
}
