using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class MountRulesTests
{
    [Fact]
    public void Reach_IsOneTile() =>
        Assert.Equal(1, MountRules.ReachTiles);

    [Fact]
    public void RiderMayMount_WhenAlreadyMounted_IsFalse() =>
        Assert.False(MountRules.RiderMayMount(alreadyMounted: true));

    [Fact]
    public void RiderMayMount_WhenOnFoot_IsTrue() =>
        Assert.True(MountRules.RiderMayMount(alreadyMounted: false));

    [Fact]
    public void MountIsReady_RejectsBusyOrIll()
    {
        Assert.False(MountRules.MountIsReady(deleted: true, isDeadPet: false, hasRider: false, poisoned: false));
        Assert.False(MountRules.MountIsReady(deleted: false, isDeadPet: true, hasRider: false, poisoned: false));
        Assert.False(MountRules.MountIsReady(deleted: false, isDeadPet: false, hasRider: true, poisoned: false));
        Assert.False(MountRules.MountIsReady(deleted: false, isDeadPet: false, hasRider: false, poisoned: true));
        Assert.True(MountRules.MountIsReady(deleted: false, isDeadPet: false, hasRider: false, poisoned: false));
    }

    [Fact]
    public void RiderControls_OwnedOrSummoned()
    {
        Assert.True(MountRules.RiderControls(
            controlled: true,
            controlMasterIsRider: true,
            summoned: false,
            summonMasterIsRider: false));
        Assert.True(MountRules.RiderControls(
            controlled: false,
            controlMasterIsRider: false,
            summoned: true,
            summonMasterIsRider: true));
        Assert.False(MountRules.RiderControls(
            controlled: true,
            controlMasterIsRider: false,
            summoned: false,
            summonMasterIsRider: false));
        Assert.False(MountRules.RiderControls(
            controlled: false,
            controlMasterIsRider: false,
            summoned: false,
            summonMasterIsRider: false));
    }

    [Fact]
    public void ChooseIndex_PrefersOwnedNearest()
    {
        (bool Owned, int Chebyshev)[] candidates =
        [
            (Owned: false, Chebyshev: 0),
            (Owned: true, Chebyshev: 1),
            (Owned: true, Chebyshev: 0)
        ];

        Assert.Equal(2, MountRules.ChooseIndex(candidates));
    }

    [Fact]
    public void ChooseIndex_OwnedWithinWalk_IsPicked()
    {
        // A pet set to Follow trails a tile or two behind. Demanding that it stand
        // next to the rider failed every mount in the log.
        (bool Owned, int Chebyshev)[] candidates =
        [
            (Owned: true, Chebyshev: MountRules.ReachTiles + 1)
        ];

        Assert.Equal(0, MountRules.ChooseIndex(candidates));
    }

    [Fact]
    public void ChooseIndex_OwnedBeyondSearch_IsNone()
    {
        (bool Owned, int Chebyshev)[] candidates =
        [
            (Owned: true, Chebyshev: MountRules.SearchTiles + 1)
        ];

        Assert.Equal(-1, MountRules.ChooseIndex(candidates));
    }

    [Fact]
    public void NeedsWalk_OnlyBeyondReach()
    {
        Assert.False(MountRules.NeedsWalk(MountRules.ReachTiles));
        Assert.True(MountRules.NeedsWalk(MountRules.ReachTiles + 1));
    }

    [Fact]
    public void ChooseIndex_WildOnly_IsNone()
    {
        (bool Owned, int Chebyshev)[] candidates =
        [
            (Owned: false, Chebyshev: 0)
        ];

        Assert.Equal(-1, MountRules.ChooseIndex(candidates));
    }

    [Fact]
    public void GenderAllowed_MatchesBaseMountFlags()
    {
        Assert.True(MountRules.GenderAllowed(femaleRider: false, allowMale: true, allowFemale: false));
        Assert.False(MountRules.GenderAllowed(femaleRider: true, allowMale: true, allowFemale: false));
        Assert.True(MountRules.GenderAllowed(femaleRider: true, allowMale: false, allowFemale: true));
    }

    [Fact]
    public void RemountsFirst_OnFootBesideItsOwnHorse() =>
        Assert.True(MountRules.RemountsFirst(mounted: false, ownMountInReach: true, inFight: false, retryDue: true));

    [Fact]
    public void RemountsFirst_NotWhenRidingFightingOrJustTried()
    {
        Assert.False(MountRules.RemountsFirst(mounted: true, ownMountInReach: true, inFight: false, retryDue: true));
        Assert.False(MountRules.RemountsFirst(mounted: false, ownMountInReach: false, inFight: false, retryDue: true));
        Assert.False(MountRules.RemountsFirst(mounted: false, ownMountInReach: true, inFight: true, retryDue: true));
        Assert.False(MountRules.RemountsFirst(mounted: false, ownMountInReach: true, inFight: false, retryDue: false));
    }
}
