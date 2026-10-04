using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class HomeLeashTests
{
    [Fact]
    public void BeyondLeash_UsesNamedRadius()
    {
        var home = CharactersFile.DefaultBankSpot;
        Assert.False(HomeLeash.BeyondLeash(home, home, CareerSettings.DefaultLeashRadius));
        Assert.True(HomeLeash.BeyondLeash(new Point3D(2728, 893, 0), home, CareerSettings.DefaultLeashRadius));
        Assert.Equal(0, HomeLeash.DistanceFromHome(home, home));
    }

    [Fact]
    public void SnapsBypass_OnlyToANearRoadNode()
    {
        // The side step west of Britain fell on a hillside with no standable tile in eight.
        var bypass = new Point3D(1119, 1861, 0);

        Assert.True(HomeLeash.SnapsBypass(bypass, new Point3D(1119 + HomeLeash.BypassSnapTiles, 1861, 0)));
        Assert.False(HomeLeash.SnapsBypass(bypass, new Point3D(1119 + HomeLeash.BypassSnapTiles + 1, 1861, 0)));
    }

    [Fact]
    public void StandCenter_FarFromHome_IsWhereThePersonStands()
    {
        var moonglow = new Point3D(4471, 1156, 0);
        var trinsic = new Point3D(1823, 2821, 0);
        var nearHome = new Point3D(4480, 1160, 0);

        Assert.Equal(trinsic, HomeLeash.StandCenter(trinsic, moonglow, CareerSettings.DefaultLeashRadius));
        Assert.Equal(moonglow, HomeLeash.StandCenter(nearHome, moonglow, CareerSettings.DefaultLeashRadius));
    }

    [Fact]
    public void CannotFindHomeLine_NamesTheSpot()
    {
        var line = HomeLeash.CannotFindHomeLine("Connor", new Point3D(2728, 893, 0));
        Assert.Contains("Connor", line);
        Assert.Contains("(2728,893)", line);
    }

    [Fact]
    public void IdleCenter_FarAuthoredCentre_BecomesTheHomeSpot()
    {
        var britain = new Point3D(1445, 1697, 0);
        var moonglow = new Point3D(4471, 1156, 0);

        Assert.Equal(moonglow, HomeLeash.IdleCenter(britain, moonglow, 400, isCopy: false));
        Assert.Equal(britain, HomeLeash.IdleCenter(britain, new Point3D(1425, 1695, 0), 400, isCopy: false));
        Assert.Equal(moonglow, HomeLeash.IdleCenter(Point3D.Zero, moonglow, 400, isCopy: false));
        Assert.True(HomeLeash.MayIdleAt(new Point3D(4480, 1160, 0), moonglow, 400));
        Assert.False(HomeLeash.MayIdleAt(britain, moonglow, 400));
    }

    [Fact]
    public void IdleCenter_CopyIdlesAtItsOwnCorner()
    {
        // Every template's authored centre stands in front of the Britain bank. A copy
        // keeping it put the whole town's idlers in the bank.
        var authoredAtTheBank = new Point3D(1430, 1697, 0);
        var ownCorner = new Point3D(1449, 1723, 6);

        Assert.Equal(ownCorner, HomeLeash.IdleCenter(authoredAtTheBank, ownCorner, 400, isCopy: true));
        Assert.Equal(authoredAtTheBank, HomeLeash.IdleCenter(authoredAtTheBank, ownCorner, 400, isCopy: false));
        Assert.Equal(authoredAtTheBank, HomeLeash.IdleCenter(authoredAtTheBank, Point3D.Zero, 400, isCopy: true));
    }

    [Fact]
    public void SameSpotStalls_CountsOnlyStallsNearTheLastOne()
    {
        var spot = new Point3D(1425, 1695, 0);
        var near = new Point3D(spot.X + HomeLeash.MaroonedStallRadius, spot.Y, 0);
        var far = new Point3D(spot.X + HomeLeash.MaroonedStallRadius + 1, spot.Y, 0);

        Assert.Equal(2, HomeLeash.SameSpotStalls(1, near, spot));
        Assert.Equal(1, HomeLeash.SameSpotStalls(2, far, spot));
    }

    [Fact]
    public void MayCheckMarooned_WantsEnoughStallsAndACooledCheck()
    {
        var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        var enough = HomeLeash.StallsBeforeMaroonedCheck;

        Assert.True(HomeLeash.MayCheckMarooned(enough, now, now));
        Assert.False(HomeLeash.MayCheckMarooned(enough - 1, now, now));
        Assert.False(HomeLeash.MayCheckMarooned(enough, now, now + HomeLeash.MaroonedCheckCooldown));
    }
}
