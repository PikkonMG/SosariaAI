using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Admin;
using SosariaAI.Navigation;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class StatusPanelTests
{
    private const string Felucca = "Felucca";
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Point3D Britain = new(1434, 1699, 0);
    private static readonly Point3D Vesper = new(2899, 676, 0);

    private static FleetReport Report(IReadOnlyList<StuckLine> stuck = null, IReadOnlyList<ShardEvent> murders = null)
    {
        var counts = FleetCounts.Of(
            [
                new CharacterSample(Felucca, true, false, false, false, false, CharacterSample.NoParty, "GoTo", "bank", "Britain, Felucca", 2, 1)
            ]
        );

        return new FleetReport(
            Now,
            counts,
            stuck ?? [],
            0,
            new JournalTally.Result(1, murders?.Count ?? 0, murders ?? []),
            BrainEnabled: true,
            RoadMoves: 3,
            Rescores: 4
        );
    }

    [Fact]
    public void Render_IsAPlainPageWithNoScriptsOrOutsideFiles()
    {
        var html = StatusPage.Render(Report());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains($"content=\"{StatusPage.RefreshSeconds}\"", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_ShowsCountsJobsAndWatchdog()
    {
        var html = StatusPage.Render(Report([new StuckLine("Erol", "GoTo", "Britain, Felucca", 12, 1)]));

        Assert.Contains("<td>Live</td><td>1</td>", html);
        Assert.DoesNotContain("Parked", html, StringComparison.Ordinal);
        Assert.Contains("<td>Reds</td><td>1</td>", html);
        Assert.Contains("<td>bank</td><td>1</td>", html);
        Assert.Contains("<td>Moved back to a road</td><td>3</td>", html);
        Assert.Contains("<td>Erol</td>", html);
        Assert.Contains("<td>Chat calls</td><td>2</td>", html);
    }

    [Fact]
    public void Render_EncodesNames()
    {
        var html = StatusPage.Render(Report([new StuckLine("<b>Vex</b>", "GoTo", "Cove & Docks", 12, 1)]));

        Assert.Contains("&lt;b&gt;Vex&lt;/b&gt;", html);
        Assert.Contains("Cove &amp; Docks", html);
        Assert.DoesNotContain("<b>Vex", html);
    }

    [Fact]
    public void MurderLine_NamesKillerVictimAndPlace()
    {
        var evt = new ShardEvent
        {
            Type = ShardEventType.Pk, At = Now, Actor = "Erol", Other = "Vex", Place = "Britain, Felucca"
        };

        Assert.Equal("12:00 Vex murdered Erol at Britain, Felucca", StatusPage.MurderLine(evt));
        Assert.Contains(StatusPage.UnknownKiller, StatusPage.MurderLine(new ShardEvent { At = Now, Actor = "Erol" }));
    }

    [Fact]
    public void SpotButton_RoundTrips()
    {
        var id = PanelRules.SpotButton(PanelRules.DungeonSection, 5);

        Assert.True(PanelRules.TryReadSpot(id, out var section, out var index));
        Assert.Equal(PanelRules.DungeonSection, section);
        Assert.Equal(5, index);
    }

    [Theory]
    [InlineData(PanelRules.ButtonRefresh)]
    [InlineData(PanelRules.ButtonWriteStatus)]
    public void TryReadSpot_ActionButtonsAreNotSpots(int buttonId) =>
        Assert.False(PanelRules.TryReadSpot(buttonId, out _, out _));

    [Fact]
    public void TryReadSpot_IndexPastTheLimitIsRefused() =>
        Assert.False(
            PanelRules.TryReadSpot(PanelRules.SpotButton(PanelRules.TownSection, PanelRules.MaxSpotsPerSection), out _, out _)
        );

    [Fact]
    public void Spots_OnePerNameSortedAndCapped()
    {
        var spots = PanelRules.Spots(
            [
                new TravelSpot("Vesper", Vesper),
                new TravelSpot("Britain", Britain),
                new TravelSpot("britain", Vesper),
                new TravelSpot("Nowhere", Point3D.Zero),
                new TravelSpot(" ", Britain)
            ],
            PanelRules.MaxSpotsPerSection
        );

        Assert.Equal(2, spots.Count);
        Assert.Equal("Britain", spots[0].Label);
        Assert.Equal(Britain, spots[0].At);
        Assert.Equal("Vesper", spots[1].Label);
        Assert.Single(PanelRules.Spots([new TravelSpot("A", Britain), new TravelSpot("B", Vesper)], 1));
    }

    private static readonly Point3D Door = new(1296, 1082, 0);
    private const int SewerFloor = 24;
    private const int SewerGround = 20;

    private static bool NoStepOff(int x, int y, int z) => false;

    private static int Ring(Point3D from, Point3D to) => NavMetric.Chebyshev(from, to);

    [Fact]
    public void Landing_OpenGround_StaysOnTheSpot()
    {
        var walker = ColumnWorld.Walker(static (_, _) => ColumnWorld.GroundOnly);

        Assert.Equal(Door, PanelRules.Landing(Door, Door.Z, walker, NoStepOff));
    }

    [Fact]
    public void Landing_OnAGatePad_MovesToTheNearestTileBeside()
    {
        var walker = ColumnWorld.Walker(static (_, _) => ColumnWorld.GroundOnly);

        var landed = PanelRules.Landing(Door, Door.Z, walker, (x, y, _) => x == Door.X && y == Door.Y);

        Assert.NotNull(landed);
        Assert.Equal(1, Ring(Door, landed.Value));
        Assert.Equal(Door.Z, landed.Value.Z);
    }

    [Fact]
    public void Landing_MarkerHeightWithNoFloor_UsesTheGroundHeight()
    {
        // The Britain sewer entrance reads 0; its floor stands at 24 over land at 20.
        var walker = ColumnWorld.Walker(static (_, _) => [SewerFloor]);

        Assert.Equal(new Point3D(Door.X, Door.Y, SewerFloor), PanelRules.Landing(Door, SewerGround, walker, NoStepOff));
    }

    [Fact]
    public void Landing_TileRingedByPads_IsSkippedForOneAPersonCanWalkFrom()
    {
        // The spot and the ring round it stand, but every step off the spot lands on a
        // pad or a ladder, so the nearest good tile is two out.
        var walker = ColumnWorld.Walker(static (_, _) => ColumnWorld.GroundOnly);
        bool Pads(int x, int y, int z) => Ring(Door, new Point3D(x, y, z)) == 1;

        var landed = PanelRules.Landing(Door, Door.Z, walker, Pads);

        Assert.NotNull(landed);
        Assert.Equal(2, Ring(Door, landed.Value));
    }

    [Fact]
    public void Landing_PillarWithNoStepDown_IsSkipped()
    {
        // A roof or a pillar top: it stands, but no step leads off it.
        var walker = ColumnWorld.Walker((x, y) => Ring(Door, new Point3D(x, y, 0)) == 1 ? ColumnWorld.Solid : ColumnWorld.GroundOnly);

        var landed = PanelRules.Landing(Door, Door.Z, walker, NoStepOff);

        Assert.NotNull(landed);
        Assert.Equal(2, Ring(Door, landed.Value));
    }

    [Fact]
    public void Landing_NothingStandsInReach_IsNull()
    {
        var walker = ColumnWorld.Walker(static (_, _) => ColumnWorld.Solid);

        Assert.Null(PanelRules.Landing(Door, Door.Z, walker, NoStepOff));
        Assert.Null(PanelRules.Landing(Door, Door.Z, null, NoStepOff));
    }
}
