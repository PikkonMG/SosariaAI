using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PresenceFocusTests
{
    [Fact]
    public void PlayerNearby_Null_IsFalse()
    {
        Assert.False(PresenceFocus.PlayerNearby(null));
        Assert.False(PresenceFocus.ClientWatching(null));
    }

    [Fact]
    public void MustThinkFast_WhenWatchedCombatGhostOrHunt()
    {
        Assert.True(PresenceFocus.MustThinkFast(playerNearby: true, inCombat: false, isGhost: false, hunting: false));
        Assert.True(PresenceFocus.MustThinkFast(playerNearby: false, inCombat: true, isGhost: false, hunting: false));
        Assert.True(PresenceFocus.MustThinkFast(playerNearby: false, inCombat: false, isGhost: true, hunting: false));
        Assert.True(PresenceFocus.MustThinkFast(playerNearby: false, inCombat: false, isGhost: false, hunting: true));
        Assert.False(PresenceFocus.MustThinkFast(playerNearby: false, inCombat: false, isGhost: false, hunting: false));
    }

    [Fact]
    public void FarThink_IsSlowerThanNear()
    {
        Assert.True(PresenceFocus.FarThinkSeconds > PresenceFocus.NearThinkSeconds);
        Assert.True(PresenceFocus.FarDangerMs > ScanPace.DangerMs);
    }
}
