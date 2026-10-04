using Server;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class RoomSurveyTests
{
    private const int TrollHits = 110;
    private const int TrollStrength = 190;
    private const int TrollAverageDamage = 11;
    private const int OneTrollThreat = 130;
    private const int TwoTrollThreat = 240;
    private const int SelfX = 100;
    private const int SelfY = 100;
    private const int NearX = 101;
    private const int FarX = 109;
    private const int NearDistance = 1;
    private const int FarDistance = 9;
    private const int ExpectedCenterX = 105;
    private const int OneFoe = 1;
    private const int TwoFoes = 2;
    private const int NoneCount = 0;
    private const int MeleeNearest = 3;
    private const int MeleeTooFar = 6;
    private const int SecondBehind = 8;
    private const int SecondTooClose = 5;
    private const int KeepMin = 2;
    private const int KeepMax = 8;
    private const int RangedTooClose = 1;
    private const int RangedFarNearest = 10;
    private const int RangedBeyondBand = 11;
    private const int PackEastX = 110;
    private const int LegTiles = 10;

    private static readonly HostileStats Troll = new(TrollHits, TrollStrength, TrollAverageDamage);

    [Fact]
    public void Finish_EmptyRoom_CentresOnOriginWithNoThreat()
    {
        var picture = new RoomTally().Finish(SelfX, SelfY);

        Assert.False(picture.Any);
        Assert.Equal(NoneCount, picture.Threat);
        Assert.Equal(SelfX, picture.CenterX);
        Assert.Equal(SelfY, picture.CenterY);
        Assert.Equal(RoomSurvey.NoDistance, picture.NearestDistance);
    }

    [Fact]
    public void Finish_TwoTrolls_SumsThreatCentreAndDistances()
    {
        var tally = new RoomTally();
        tally.Add(NearX, SelfY, NearDistance, onSelf: true, Troll);
        tally.Add(FarX, SelfY, FarDistance, onSelf: false, Troll);

        var picture = tally.Finish(SelfX, SelfY);

        Assert.Equal(TwoFoes, picture.Count);
        Assert.Equal(TwoTrollThreat, picture.Threat);
        Assert.Equal(OneFoe, picture.Attackers);
        Assert.Equal(OneFoe, picture.CloseAttackers);
        Assert.Equal(OneTrollThreat, picture.CloseThreat);
        Assert.Equal(ExpectedCenterX, picture.CenterX);
        Assert.Equal(NearDistance, picture.NearestDistance);
        Assert.Equal(FarDistance, picture.SecondDistance);
    }

    [Fact]
    public void Reset_ClearsEarlierScan()
    {
        var tally = new RoomTally();
        tally.Add(NearX, SelfY, NearDistance, onSelf: true, Troll);
        tally.Reset();

        Assert.False(tally.Finish(SelfX, SelfY).Any);
    }

    [Fact]
    public void Add_FarAttacker_IsNotClose()
    {
        var tally = new RoomTally();
        tally.Add(FarX, SelfY, FarDistance, onSelf: true, Troll);

        var picture = tally.Finish(SelfX, SelfY);

        Assert.Equal(OneFoe, picture.Attackers);
        Assert.Equal(NoneCount, picture.CloseAttackers);
    }

    [Fact]
    public void IsStraggler_MeleeLeadWellAhead_IsTrue() =>
        Assert.True(RoomSurvey.IsStraggler(MeleeNearest, SecondBehind, ranged: false, KeepMin, KeepMax));

    [Fact]
    public void IsStraggler_PackStillTogether_IsFalse() =>
        Assert.False(RoomSurvey.IsStraggler(MeleeNearest, SecondTooClose, ranged: false, KeepMin, KeepMax));

    [Fact]
    public void IsStraggler_MeleeLeadOutOfReach_IsFalse() =>
        Assert.False(RoomSurvey.IsStraggler(MeleeTooFar, RoomSurvey.NoDistance, ranged: false, KeepMin, KeepMax));

    [Fact]
    public void IsStraggler_RangedBand_NeedsDistance()
    {
        Assert.False(RoomSurvey.IsStraggler(RangedTooClose, RoomSurvey.NoDistance, ranged: true, KeepMin, KeepMax));
        Assert.True(RoomSurvey.IsStraggler(RangedFarNearest, RoomSurvey.NoDistance, ranged: true, KeepMin, KeepMax));
        Assert.False(RoomSurvey.IsStraggler(RangedBeyondBand, RoomSurvey.NoDistance, ranged: true, KeepMin, KeepMax));
    }

    [Fact]
    public void IsStraggler_NobodyInSight_IsFalse() =>
        Assert.False(RoomSurvey.IsStraggler(RoomSurvey.NoDistance, RoomSurvey.NoDistance, ranged: false, KeepMin, KeepMax));

    [Fact]
    public void StillAway_GoalBehindPack_IsFalse()
    {
        var from = new Point3D(SelfX, SelfY, 0);
        var west = new Point3D(SelfX - LegTiles, SelfY, 0);
        var east = new Point3D(PackEastX + LegTiles, SelfY, 0);

        Assert.True(RoomSurvey.StillAway(west, from, PackEastX, SelfY));
        Assert.False(RoomSurvey.StillAway(east, from, PackEastX, SelfY));
    }
}
