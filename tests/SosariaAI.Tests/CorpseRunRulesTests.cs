using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class CorpseRunRulesTests
{
    /// <summary>A tile route's last leg may stop this far short of its goal.</summary>
    private const int LastLegSlackTiles = 1;

    private static readonly TimeSpan Early = TimeSpan.FromSeconds(5);

    /// <summary>A guard line across the straight way, from the top of the map down to this row.</summary>
    private const int GuardLineX = 10;
    private const int GuardLineBottomY = 5;
    private const int RouteEndX = 20;

    /// <summary>A rune lands this close to the Den's bank or it does not serve it.</summary>
    private const int NearTiles = 48;

    private static bool Guarded(int x, int y, int z) => x == GuardLineX && y <= GuardLineBottomY;

    [Fact]
    public void Next_LootsInReach_AndLooksWhereAMissingBodyLay()
    {
        Assert.Equal(CorpseRunStep.Reclaim, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange, 0, Early));
        Assert.Equal(CorpseRunStep.Reclaim, CorpseRunRules.Next(true, true, 0, CorpseRunRules.MaxWalks, CorpseRunRules.GiveUpAfter));
        Assert.Equal(CorpseRunStep.Reclaim, CorpseRunRules.Next(false, false, 0, 0, Early));
    }

    [Fact]
    public void Next_OneTilePastTheReach_StillWalks()
    {
        // The walk that stopped three tiles off used to end the run with the gear left on the body.
        Assert.Equal(CorpseRunStep.CloseWalk, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, 1, Early));
    }

    [Fact]
    public void Next_WalksCloseByTile_FarByTravel()
    {
        Assert.Equal(CorpseRunStep.CloseWalk, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles, 0, Early));
        Assert.Equal(CorpseRunStep.LongWalk, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles + 1, 0, Early));
    }

    [Fact]
    public void Next_GivesUp_OnAnotherFacet_PastTheWalks_OrPastTheTime()
    {
        Assert.Equal(CorpseRunStep.GiveUp, CorpseRunRules.Next(true, false, 0, 0, Early));
        Assert.Equal(CorpseRunStep.GiveUp, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, CorpseRunRules.MaxWalks, Early));
        Assert.Equal(CorpseRunStep.GiveUp, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles, 0, CorpseRunRules.GiveUpAfter));
    }

    [Fact]
    public void WhyGiveUp_NamesTheCause()
    {
        Assert.Equal(CorpseRunRules.OtherFacetReason, CorpseRunRules.WhyGiveUp(sameMap: false, walks: 0));
        Assert.Equal(CorpseRunRules.NoWayReason, CorpseRunRules.WhyGiveUp(sameMap: true, walks: CorpseRunRules.MaxWalks));
        Assert.Equal(CorpseRunRules.TooLongReason, CorpseRunRules.WhyGiveUp(sameMap: true, walks: 1));
    }

    [Fact]
    public void WalkRange_EndsInsideTheLootReach()
    {
        Assert.InRange(CorpseRunRules.WalkRange + LastLegSlackTiles, CorpseRunRules.WalkRange, CorpseRunRules.LootRange);
    }

    [Fact]
    public void BodyLost_OnlyWhenTheBodyIsGoneOrEmpty()
    {
        Assert.True(CorpseRunRules.BodyLost(CorpseReclaimResult.Decayed));
        Assert.True(CorpseRunRules.BodyLost(CorpseReclaimResult.Stripped));
        Assert.False(CorpseRunRules.BodyLost(CorpseReclaimResult.Exists));
        Assert.False(CorpseRunRules.BodyLost(CorpseReclaimResult.Reclaimed));
        Assert.Equal("WHO LOOTED MY CORPSE", Talk.Line(TalkCategory.DeathLooted, 0, default));
    }

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(false, 1, false)]
    [InlineData(true, CorpseRunRules.MaxTownBuys, false)]
    public void BuysAgain_WhileTheLastTripBoughtAndTheCountAllows(bool bought, int buys, bool again) =>
        Assert.Equal(again, CorpseRunRules.BuysAgain(bought, buys));

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void RunsAfterRaise_OnlyForARaiseTheGhostStepDidNotRun(bool standUp, bool ghostRunning, bool body, bool runs) =>
        Assert.Equal(runs, CorpseRunRules.RunsAfterRaise(standUp, ghostRunning, body));
    [Fact]
    public void Next_Red_WalksToAGuardFreeBodyOnItsFacet_CloseOrFar()
    {
        Assert.Equal(CorpseRunStep.CloseWalk, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles, 0, Early, murderer: true));
        Assert.Equal(CorpseRunStep.LongWalk, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles + 1, 0, Early, murderer: true));
        Assert.Equal(CorpseRunStep.Reclaim, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange, 0, Early, murderer: true, bodyUnderGuards: true));
    }

    [Fact]
    public void Next_Red_RecallsHome_NeverAGiveUp()
    {
        Assert.Equal(CorpseRunStep.RecallHome, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles + 1, CorpseRunRules.MaxWalks, Early, murderer: true));
        Assert.Equal(CorpseRunStep.RecallHome, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, 0, Early, murderer: true, bodyUnderGuards: true));
        Assert.Equal(CorpseRunStep.RecallHome, CorpseRunRules.Next(true, false, 0, 0, Early, murderer: true));
        Assert.Equal(CorpseRunStep.RecallHome, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, CorpseRunRules.MaxWalks, Early, murderer: true));
        Assert.Equal(CorpseRunStep.RecallHome, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, 0, CorpseRunRules.GiveUpAfter, murderer: true));
    }

    [Fact]
    public void Next_Blue_UnderGuardsStillWalks()
    {
        Assert.Equal(CorpseRunStep.CloseWalk, CorpseRunRules.Next(true, true, CorpseRunRules.LootRange + 1, 0, Early, bodyUnderGuards: true));
        Assert.Equal(CorpseRunStep.LongWalk, CorpseRunRules.Next(true, true, CorpseRunRules.CloseWalkTiles + 1, 0, Early, bodyUnderGuards: true));
    }

    [Theory]
    [InlineData(true, CorpseRunStep.RecallHome)]
    [InlineData(false, CorpseRunStep.LongWalk)]
    public void AfterNoCloseRoute_RedRecallsHome_BlueTravels(bool murderer, CorpseRunStep next) =>
        Assert.Equal(next, CorpseRunRules.AfterNoCloseRoute(murderer));

    [Fact]
    public void WhyRecallHome_NamesTheCause()
    {
        Assert.Equal(CorpseRunRules.OtherFacetReason, CorpseRunRules.WhyRecallHome(false, true, 0));
        Assert.Equal(CorpseRunRules.UnderGuardsReason, CorpseRunRules.WhyRecallHome(true, true, 0));
        Assert.Equal(CorpseRunRules.NoWayReason, CorpseRunRules.WhyRecallHome(true, false, CorpseRunRules.MaxWalks));
        Assert.Equal(CorpseRunRules.TooLongReason, CorpseRunRules.WhyRecallHome(true, false, 1));
    }

    [Fact]
    public void OffGuards_RefusesAStepOntoGuardedGround()
    {
        var walker = CorpseRunRules.OffGuards(TestWalkers.Flat, Guarded);

        Assert.False(walker.Step(GuardLineX - 1, 0, 0, GuardLineX, 0, out _));
        Assert.True(walker.Step(GuardLineX - 1, GuardLineBottomY + 1, 0, GuardLineX, GuardLineBottomY + 1, out _));
        Assert.True(TestWalkers.Flat.Step(GuardLineX - 1, 0, 0, GuardLineX, 0, out _));
    }

    [Fact]
    public void OffGuards_TheTileRouteGoesRoundTheGuards()
    {
        var path = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(RouteEndX, 0, 0), CorpseRunRules.OffGuards(TestWalkers.Flat, Guarded));

        Assert.NotEmpty(path);
        Assert.DoesNotContain(path, tile => Guarded(tile.X, tile.Y, tile.Z));
    }

    [Fact]
    public void OffGuards_AGuardLineWithNoGap_LeavesNoRoute()
    {
        static bool GuardWall(int x, int y, int z) => x == GuardLineX;

        var from = new Point3D(0, 0, 0);
        var to = new Point3D(RouteEndX, 0, 0);

        Assert.NotEmpty(TileRoute.Find(from, to, TestWalkers.Flat));
        Assert.Empty(TileRoute.Find(from, to, CorpseRunRules.OffGuards(TestWalkers.Flat, GuardWall)));
    }

    [Theory]
    [InlineData(true, NearTiles + 1, true)]
    [InlineData(true, NearTiles, false)]
    [InlineData(false, NearTiles + 1, false)]
    public void RecallsHome_OnlyFromPastALandingWalkToAKnownBank(bool denKnown, int tiles, bool recalls) =>
        Assert.Equal(recalls, CorpseRunRules.RecallsHome(denKnown, tiles, NearTiles));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void ShopsAfterReclaim_ALostBodyOrOneThatGaveNoArms(bool bodyLost, bool armed, bool shops) =>
        Assert.Equal(shops, CorpseRunRules.ShopsAfterReclaim(bodyLost, armed));
}
