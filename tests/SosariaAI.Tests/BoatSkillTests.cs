using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BoatSkillTests
{
    [Fact]
    public void DockAndPrice_AreCorpusBoat()
    {
        Assert.Equal("Boat", SkillKinds.Boat);
        Assert.Equal(2000, AmbitionRules.DefaultBoatGold);
        Assert.Equal(1495, CharactersFile.BritainDock.X);
        Assert.Equal(1768, CharactersFile.BritainDock.Y);
        Assert.Equal(BoatRules.ShoreZ, CharactersFile.BritainDock.Z);
    }

    [Fact]
    public void WalkStaysOnLand_PlaceUsesTheBay()
    {
        var dock = CharactersFile.BritainDock;
        var bay = BoatRules.BritainOpenWater;
        var dx = Math.Abs(bay.X - dock.X);
        var dy = Math.Abs(bay.Y - dock.Y);

        Assert.Equal(BoatRules.ShoreZ, dock.Z);
        Assert.Equal(BoatRules.DeepWaterZ, bay.Z);
        Assert.NotEqual(dock, bay);
        Assert.True(Math.Max(dx, dy) > BoatRules.SearchRadius);
    }
}
