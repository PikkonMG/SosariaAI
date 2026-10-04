using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class TownStayRulesTests
{
    [Fact]
    public void MaySell_Worker_OnlyInTown()
    {
        Assert.False(TownStayRules.MaySell(CharacterRole.Worker, inTownRegion: false));
        Assert.True(TownStayRules.MaySell(CharacterRole.Worker, inTownRegion: true));
    }

    [Fact]
    public void MaySell_Fighter_Anywhere()
    {
        Assert.True(TownStayRules.MaySell(CharacterRole.Fighter, inTownRegion: false));
        Assert.True(TownStayRules.MaySell(CharacterRole.Fighter, inTownRegion: true));
    }
}
