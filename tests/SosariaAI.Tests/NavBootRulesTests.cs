using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavBootRulesTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    public void Rebuilds_OnlyWhenAskedAndTheWorldIsSetUp(bool forced, bool rebuildOnBoot, bool worldWaits, bool rebuilds) =>
        Assert.Equal(rebuilds, NavBootRules.Rebuilds(forced, rebuildOnBoot, worldWaits));

    [Fact]
    public void Lines_SayTheBuildWaitsForTheSetup()
    {
        Assert.Contains("First Time Setup", NavBootRules.WaitsForSetupLine);
        Assert.Contains("First Time Setup", NavBootRules.RebuildWaitsLine);
        Assert.Contains("checking the saved nav graphs", NavBootRules.SetupCheckLine);
    }
}
