using System;
using Server;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class DangerMapTests
{
    private const string Felucca = "Felucca";
    private const string Trammel = "Trammel";
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Point3D Destard = new(1176, 2637, 0);
    private static readonly Point3D SameSquare = new(1180, 2640, 0);
    private static readonly Point3D Britain = new(1425, 1695, 0);

    [Fact]
    public void Heat_HalvesEveryHalfLife()
    {
        var map = new DangerMap();
        map.Note(Felucca, Destard, DangerMap.MurderHeat, Now);

        Assert.Equal(DangerMap.MurderHeat, map.Heat(Felucca, Destard, Now), 6);
        Assert.Equal(DangerMap.MurderHeat / 2, map.Heat(Felucca, Destard, Now + DangerMap.HalfLife), 6);
        Assert.Equal(DangerMap.MurderHeat / 4, map.Heat(Felucca, Destard, Now + DangerMap.HalfLife * 2), 6);
    }

    [Fact]
    public void Heat_AddsUpInOneSquareOnly()
    {
        var map = new DangerMap();
        map.Note(Felucca, Destard, DangerMap.MurderHeat, Now);
        map.Note(Felucca, SameSquare, DangerMap.DeathHeat, Now);

        Assert.Equal(DangerMap.MurderHeat + DangerMap.DeathHeat, map.Heat(Felucca, Destard, Now), 6);
        Assert.Equal(0, map.Heat(Felucca, Britain, Now));
        Assert.Equal(0, map.Heat(Trammel, Destard, Now));
        Assert.True(map.VisitFactor(Felucca, Destard, Now) < DangerMap.FullVisits);
        Assert.Equal(DangerMap.FullVisits, map.VisitFactor(Felucca, Britain, Now));
    }

    [Fact]
    public void VisitFactor_ColdIsFullAndHotHasAFloor()
    {
        Assert.Equal(DangerMap.FullVisits, DangerMap.VisitFactorFor(0));
        Assert.True(DangerMap.VisitFactorFor(DangerMap.MurderHeat) < DangerMap.FullVisits);
        Assert.Equal(DangerMap.MinVisitFactor, DangerMap.VisitFactorFor(1000));
    }

    [Fact]
    public void HeatOf_OnlyDangerNews()
    {
        Assert.Equal(DangerMap.MurderHeat, DangerMap.HeatOf(ShardEventType.Pk));
        Assert.Equal(DangerMap.DeathHeat, DangerMap.HeatOf(ShardEventType.Death));
        Assert.Equal(0, DangerMap.HeatOf(ShardEventType.Party));
        Assert.Equal(0, DangerMap.HeatOf(ShardEventType.GuildWar));
    }

    [Fact]
    public void Note_IgnoresNoHeatAndNoFacet()
    {
        var map = new DangerMap();
        map.Note(Felucca, Destard, 0, Now);
        map.Note(null, Destard, DangerMap.MurderHeat, Now);

        Assert.Equal(0, map.Heat(Felucca, Destard, Now));
    }

    [Fact]
    public void ScreamLine_ShoutsThePlace()
    {
        Assert.Equal("RED AT DESTARD!!", DangerMap.ScreamLine("Destard"));
        Assert.Equal("RED!!", DangerMap.ScreamLine(null));
    }
}
