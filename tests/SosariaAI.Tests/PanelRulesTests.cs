using System;
using SosariaAI.Admin;
using Xunit;

namespace SosariaAI.Tests;

public class PanelRulesTests
{
    private static readonly DateTime LandedAt = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SpotsFresh_KeepsSpotsWithinTheirLifeOnTheSameGraph()
    {
        Assert.True(PanelRules.SpotsFresh(sameGraph: true, LandedAt, LandedAt));
        Assert.True(PanelRules.SpotsFresh(sameGraph: true, LandedAt, LandedAt + PanelRules.SpotsLife - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SpotsFresh_LandsAgainOnANewGraphOrOnceTheLifeIsOver()
    {
        Assert.False(PanelRules.SpotsFresh(sameGraph: false, LandedAt, LandedAt));
        Assert.False(PanelRules.SpotsFresh(sameGraph: true, LandedAt, LandedAt + PanelRules.SpotsLife));
        Assert.False(PanelRules.SpotsFresh(sameGraph: true, default, LandedAt));
    }
}
