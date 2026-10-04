using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestPatchesTests
{
    private const int FeluccaId = 0;
    private const int TrammelId = 1;
    private const int RestMinutes = 20;

    private static readonly DateTime Now = new(2026, 9, 27, 13, 56, 1, DateTimeKind.Utc);
    private static readonly TimeSpan Rest = TimeSpan.FromMinutes(RestMinutes);

    private static readonly Rectangle2D First = new(1376, 1708, WorkSites.PatchSize, WorkSites.PatchSize);
    private static readonly Rectangle2D Second = new(1400, 1708, 16, WorkSites.PatchSize);
    private static readonly Rectangle2D Third = new(1376, 1732, WorkSites.PatchSize, 16);

    [Fact]
    public void Rest_AnEmptiedPatchRestsUntilTheBankCanRefill()
    {
        var patch = new Rectangle2D(100, 100, WorkSites.PatchSize, WorkSites.PatchSize);

        HarvestPatches.Rest(SkillKinds.Lumberjack, FeluccaId, patch, Now + Rest);

        Assert.True(HarvestPatches.IsResting(SkillKinds.Lumberjack, FeluccaId, patch, Now));
        Assert.True(HarvestPatches.IsResting(SkillKinds.Lumberjack, FeluccaId, patch, Now + Rest - TimeSpan.FromSeconds(1)));
        Assert.False(HarvestPatches.IsResting(SkillKinds.Lumberjack, FeluccaId, patch, Now + Rest));
    }

    [Fact]
    public void Rest_IsPerHarvestAndPerFacet()
    {
        // A wood cut bare can still hold fish in its river, and the other facet's wood grows on.
        var patch = new Rectangle2D(200, 200, WorkSites.PatchSize, WorkSites.PatchSize);

        HarvestPatches.Rest(SkillKinds.Lumberjack, FeluccaId, patch, Now + Rest);

        Assert.False(HarvestPatches.IsResting(SkillKinds.Fish, FeluccaId, patch, Now));
        Assert.False(HarvestPatches.IsResting(SkillKinds.Lumberjack, TrammelId, patch, Now));
    }

    [Fact]
    public void Pick_SkipsRestingAndEmptyPatches_AndRestsTheEmptyOnes()
    {
        var cells = new[] { First, Second, Third };
        var emptied = new List<Rectangle2D>();

        Assert.True(HarvestPatches.Pick(
            cells,
            resting: cell => cell == First,
            hasWork: cell => cell == Third,
            reachable: _ => true,
            noWork: emptied.Add,
            out var patch
        ));

        Assert.Equal(Third, patch);
        Assert.Equal(new[] { Second }, emptied);
    }

    [Fact]
    public void Pick_PrefersAReachablePatch_ElseTriesTheFirstWithWork()
    {
        var cells = new[] { First, Second, Third };

        Assert.True(HarvestPatches.Pick(cells, _ => false, _ => true, cell => cell == Third, _ => { }, out var reachable));
        Assert.Equal(Third, reachable);

        Assert.True(HarvestPatches.Pick(cells, _ => false, cell => cell != First, _ => false, _ => { }, out var fallback));
        Assert.Equal(Second, fallback);
    }

    [Fact]
    public void Pick_NoPatchWithWork_PicksNone()
    {
        // Seventeen woodcutters walked into a cut-bare Britain wood and failed "nothing to
        // work at the patch" one after another.
        Assert.False(HarvestPatches.Pick([First, Second], _ => false, _ => false, _ => true, _ => { }, out _));
    }

    [Fact]
    public void MayMoveOn_AJobWalksOnToAFewPatchesThenStops()
    {
        Assert.True(HarvestPatches.MayMoveOn(0));
        Assert.True(HarvestPatches.MayMoveOn(HarvestPatches.MaxPatchMoves - 1));
        Assert.False(HarvestPatches.MayMoveOn(HarvestPatches.MaxPatchMoves));
    }

    [Fact]
    public void Dry_AWorkerWithEverySiteBareLeavesTheHarvestUntilTheBanksRefill()
    {
        const uint Estrid = 0x7B0001;

        HarvestPatches.NoteDry(Estrid, SkillKinds.Lumberjack, Now + Rest);

        Assert.True(HarvestPatches.IsDry(Estrid, SkillKinds.Lumberjack, Now));
        Assert.False(HarvestPatches.IsDry(Estrid, SkillKinds.Mine, Now));
        Assert.False(HarvestPatches.IsDry(Estrid, SkillKinds.Lumberjack, Now + Rest));
    }
}
