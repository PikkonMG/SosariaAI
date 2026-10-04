using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PartyRoadRulesTests
{
    private const int QuietShard = 100;
    private const int BusyShard = 1600;
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Fact]
    public void Cap_GrowsWithThePeopleOnline()
    {
        Assert.Equal(PartyRoadRules.MinConvoys, PartyRoadRules.Cap(RoadGroupKind.Convoy, QuietShard));
        Assert.Equal(BusyShard / PartyRoadRules.PopulationPerConvoy, PartyRoadRules.Cap(RoadGroupKind.Convoy, BusyShard));
        Assert.Equal(PartyRoadRules.MinWarBands, PartyRoadRules.Cap(RoadGroupKind.WarBand, QuietShard));
        Assert.Equal(BusyShard / PartyRoadRules.PopulationPerWarBand, PartyRoadRules.Cap(RoadGroupKind.WarBand, BusyShard));
        Assert.Equal(PartyRoadRules.MinSweeps, PartyRoadRules.Cap(RoadGroupKind.Sweep, QuietShard));
        Assert.Equal(BusyShard / PartyRoadRules.PopulationPerSweep, PartyRoadRules.Cap(RoadGroupKind.Sweep, BusyShard));
        Assert.True(PartyRoadRules.MayForm(RoadGroupKind.WarBand, 0, QuietShard));
        Assert.False(PartyRoadRules.MayForm(RoadGroupKind.WarBand, PartyRoadRules.MinWarBands, QuietShard));
    }

    [Fact]
    public void Posse_RidesAloneOrWithEveryFriendUpToThree_AndOnlyAFewAtOnce()
    {
        const int ManyFriends = 10;
        const int TwoFriends = 2;

        Assert.Equal(PartyRoadRules.MaxPosses, PartyRoadRules.Cap(RoadGroupKind.Posse, QuietShard));
        Assert.Equal(PartyRoadRules.MaxPosses, PartyRoadRules.Cap(RoadGroupKind.Posse, BusyShard));
        Assert.Equal(PartyRoadRules.PosseMinMates, PartyRoadRules.MinMatesOf(RoadGroupKind.Posse));

        for (var roll = 0; roll < PartyRoadRules.PercentScale; roll++)
        {
            Assert.Equal(PartyRoadRules.MaxMates, PartyRoadRules.MatesToTake(RoadGroupKind.Posse, ManyFriends, roll));
            Assert.Equal(TwoFriends, PartyRoadRules.MatesToTake(RoadGroupKind.Posse, TwoFriends, roll));
        }
    }

    [Fact]
    public void Posse_GivesUpTheChaseAfterItsLife()
    {
        Assert.False(PartyRoadRules.Expired(RoadGroupKind.Posse, Now, Now + PartyRoadRules.PosseLife - TimeSpan.FromSeconds(1)));
        Assert.True(PartyRoadRules.Expired(RoadGroupKind.Posse, Now, Now + PartyRoadRules.PosseLife));
    }

    [Fact]
    public void StrongEnoughForDen_AboutARedsOwnPower()
    {
        Assert.True(PartyRoadRules.StrongEnoughForDen(PartyRoadRules.DenRaidMinPower));
        Assert.False(PartyRoadRules.StrongEnoughForDen(PartyRoadRules.DenRaidMinPower - 1));
    }

    [Fact]
    public void LookDue_WaitsOutTheRetryAfterACallInVain()
    {
        var retryAt = Now + PartyRoadRules.LookRetry;

        Assert.True(PartyRoadRules.LookDue(default, Now));
        Assert.False(PartyRoadRules.LookDue(retryAt, Now));
        Assert.True(PartyRoadRules.LookDue(retryAt, retryAt));
        Assert.True(PartyRoadRules.LookRetry > TimeSpan.Zero);
    }

    [Fact]
    public void LeaderGone_OffTheWorldOrDeleted()
    {
        Assert.False(PartyRoadRules.LeaderGone(leaderFound: true, onInternal: false));
        Assert.True(PartyRoadRules.LeaderGone(leaderFound: true, onInternal: true));
        Assert.True(PartyRoadRules.LeaderGone(leaderFound: false, onInternal: false));
    }

    [Fact]
    public void ConvoyTrip_NotTwoStreetsNorTheWholeLand()
    {
        Assert.False(PartyRoadRules.ConvoyTrip(PartyRoadRules.ConvoyMinTrip - 1));
        Assert.True(PartyRoadRules.ConvoyTrip(PartyRoadRules.ConvoyMinTrip));
        Assert.True(PartyRoadRules.ConvoyTrip(PartyRoadRules.ConvoyMaxTrip));
        Assert.False(PartyRoadRules.ConvoyTrip(PartyRoadRules.ConvoyMaxTrip + 1));
    }

    [Fact]
    public void MatesToTake_OneToThree_NeverMoreThanThereAre()
    {
        for (var roll = -5; roll < 20; roll++)
        {
            Assert.InRange(PartyRoadRules.MatesToTake(RoadGroupKind.Convoy, 10, roll), PartyRoadRules.MinMates, PartyRoadRules.MaxMates);
        }

        Assert.Equal(1, PartyRoadRules.MatesToTake(RoadGroupKind.Sweep, 1, 2));
        Assert.Equal(0, PartyRoadRules.MatesToTake(RoadGroupKind.WarBand, 0, 2));
        Assert.InRange(
            PartyRoadRules.MatesToTake(RoadGroupKind.Convoy, 10, int.MinValue),
            PartyRoadRules.MinMates,
            PartyRoadRules.MaxMates
        );
    }

    [Fact]
    public void DenRaid_ABandOfThreeOrFour_OneAtATime()
    {
        Assert.Equal(PartyRoadRules.DenRaidMinMates, PartyRoadRules.MinMatesOf(RoadGroupKind.DenRaid));
        Assert.Equal(PartyRoadRules.MinMates, PartyRoadRules.MinMatesOf(RoadGroupKind.Sweep));

        for (var roll = -5; roll < 20; roll++)
        {
            Assert.InRange(PartyRoadRules.MatesToTake(RoadGroupKind.DenRaid, 10, roll), PartyRoadRules.DenRaidMinMates, PartyRoadRules.MaxMates);
        }

        Assert.Equal(PartyRoadRules.DenRaidMinMates, PartyRoadRules.MatesToTake(RoadGroupKind.DenRaid, PartyRoadRules.DenRaidMinMates, 7));
        Assert.Equal(PartyRoadRules.MaxDenRaids, PartyRoadRules.Cap(RoadGroupKind.DenRaid, QuietShard));
        Assert.Equal(PartyRoadRules.MaxDenRaids, PartyRoadRules.Cap(RoadGroupKind.DenRaid, BusyShard));
        Assert.True(PartyRoadRules.MayForm(RoadGroupKind.DenRaid, 0, BusyShard));
        Assert.False(PartyRoadRules.MayForm(RoadGroupKind.DenRaid, PartyRoadRules.MaxDenRaids, BusyShard));
    }

    [Fact]
    public void DenRaid_AboutTwoAnHour_HalfAnHourOut_ThreeHoursRest()
    {
        Assert.Equal(PartyRoadRules.DenRaidGapMin, PartyRoadRules.Gap(RoadGroupKind.DenRaid, 0));
        Assert.Equal(PartyRoadRules.DenRaidGapMax, PartyRoadRules.Gap(RoadGroupKind.DenRaid, 1));
        Assert.True(PartyRoadRules.DenRaidGapMin > PartyRoadRules.SweepGapMax);
        Assert.False(PartyRoadRules.Expired(RoadGroupKind.DenRaid, Now, Now + PartyRoadRules.DenRaidLife - TimeSpan.FromSeconds(1)));
        Assert.True(PartyRoadRules.Expired(RoadGroupKind.DenRaid, Now, Now + PartyRoadRules.DenRaidLife));
        Assert.False(TimeRules.Rested(Now, Now + PartyRoadRules.DenRaidRest - TimeSpan.FromMinutes(1), PartyRoadRules.DenRaidRest));
        Assert.True(TimeRules.Rested(Now, Now + PartyRoadRules.DenRaidRest, PartyRoadRules.DenRaidRest));
        Assert.True(TimeRules.Rested(default, Now, PartyRoadRules.DenRaidRest));
    }

    [Fact]
    public void Intercepts_OnlyWhenAnEnemyBandIsOut()
    {
        Assert.False(PartyRoadRules.Intercepts(false, 0));
        Assert.True(PartyRoadRules.Intercepts(true, 0));
        Assert.False(PartyRoadRules.Intercepts(true, PartyRoadRules.InterceptPercent));
    }

    [Fact]
    public void Expired_ByKind()
    {
        Assert.False(PartyRoadRules.Expired(RoadGroupKind.Convoy, Now, Now + PartyRoadRules.ConvoyLife - TimeSpan.FromSeconds(1)));
        Assert.True(PartyRoadRules.Expired(RoadGroupKind.Convoy, Now, Now + PartyRoadRules.ConvoyLife));
        Assert.True(PartyRoadRules.Expired(RoadGroupKind.WarBand, Now, Now + PartyRoadRules.WarBandLife));
        Assert.False(PartyRoadRules.Expired(RoadGroupKind.Sweep, Now, Now + PartyRoadRules.SweepLife - TimeSpan.FromSeconds(1)));
        Assert.True(PartyRoadRules.Expired(RoadGroupKind.Sweep, Now, Now + PartyRoadRules.SweepLife));
    }

    [Fact]
    public void WarBandCall_ReachesTheSideOverTheLeash()
    {
        Assert.True(PartyRoadRules.WarBandCallRange > PartyRoadRules.CallRange);
        Assert.True(PartyRoadRules.HearsCall(RoadGroupKind.WarBand, PartyRoadRules.CallRange + 1));
        Assert.True(PartyRoadRules.HearsCall(RoadGroupKind.WarBand, PartyRoadRules.WarBandCallRange));
        Assert.False(PartyRoadRules.HearsCall(RoadGroupKind.WarBand, PartyRoadRules.WarBandCallRange + 1));
        Assert.False(PartyRoadRules.HearsCall(RoadGroupKind.WarBand, -1));
    }

    [Fact]
    public void Call_CarriesPastSpeechToATownAndItsRoads()
    {
        Assert.True(PartyRoadRules.CallRange > PartyRoadRules.RecruitRange);
        Assert.True(PartyRoadRules.HearsCall(RoadGroupKind.Convoy, 0));
        Assert.True(PartyRoadRules.HearsCall(RoadGroupKind.Convoy, PartyRoadRules.CallRange));
        Assert.False(PartyRoadRules.HearsCall(RoadGroupKind.Convoy, PartyRoadRules.CallRange + 1));
        Assert.False(PartyRoadRules.HearsCall(RoadGroupKind.Sweep, PartyRoadRules.CallRange + 1));
        Assert.False(PartyRoadRules.HearsCall(RoadGroupKind.Convoy, -1));
        Assert.True(PartyRoadRules.WithinSpeech(PartyRoadRules.RecruitRange));
        Assert.False(PartyRoadRules.WithinSpeech(PartyRoadRules.RecruitRange + 1));
        Assert.True(PartyRoadRules.MusterLimit > TimeSpan.Zero);
    }

    [Fact]
    public void ConvoyBank_OnlyABankARealTripAway()
    {
        var from = new Point3D(1000, 1000, 0);
        var near = Bank("near", from.X + PartyRoadRules.ConvoyMinTrip - 1);
        var far = Bank("far", from.X + PartyRoadRules.ConvoyMaxTrip + 1);
        var right = Bank("right", from.X + PartyRoadRules.ConvoyMinTrip);
        var inn = new Destination { Name = "inn", Kind = "Tavern", X = right.X, Y = from.Y };
        Destination[] places = [near, far, inn, right, null];

        for (var roll = -3; roll < 10; roll++)
        {
            Assert.Same(right, PartyRoadRules.ConvoyBank(places, from, [], roll));
        }

        Assert.Null(PartyRoadRules.ConvoyBank([near, far, inn], from, [], 0));
        Assert.Null(PartyRoadRules.ConvoyBank(null, from, [], 0));
    }

    [Fact]
    public void ConvoyBank_KeepsAwayFromWhereTheLeaderRanFrom()
    {
        var from = new Point3D(1000, 1000, 0);
        var right = Bank("right", from.X + PartyRoadRules.ConvoyMinTrip);

        Assert.Null(PartyRoadRules.ConvoyBank([right], from, [right.Arrival], 0));
    }

    [Fact]
    public void ConvoyBank_TheRollPicksAmongTheFittingBanks()
    {
        var from = new Point3D(1000, 1000, 0);
        var first = Bank("first", from.X + PartyRoadRules.ConvoyMinTrip);
        var second = Bank("second", from.X + PartyRoadRules.ConvoyMaxTrip);
        Destination[] places = [first, second];

        Assert.Same(first, PartyRoadRules.ConvoyBank(places, from, [], 0));
        Assert.Same(second, PartyRoadRules.ConvoyBank(places, from, [], 1));
    }

    [Fact]
    public void PartyRoadTrip_CarriesTheTripsName_SoTheJobPlanMovesOn() =>
        Assert.Equal(SkillKinds.Travel, new PartyRoadTrip(new TownTrip(new Point3D(1000, 1000, 0))).Name);

    [Fact]
    public void Gap_SpansItsKindsWindow()
    {
        Assert.Equal(PartyRoadRules.ConvoyGapMin, PartyRoadRules.Gap(RoadGroupKind.Convoy, 0));
        Assert.Equal(PartyRoadRules.ConvoyGapMax, PartyRoadRules.Gap(RoadGroupKind.Convoy, 1));
        Assert.Equal(PartyRoadRules.WarBandGapMax, PartyRoadRules.Gap(RoadGroupKind.WarBand, 2));
        Assert.Equal(PartyRoadRules.SweepGapMin, PartyRoadRules.Gap(RoadGroupKind.Sweep, 0));
        Assert.Equal(PartyRoadRules.SweepGapMax, PartyRoadRules.Gap(RoadGroupKind.Sweep, 1));
    }

    [Fact]
    public void RaidOutcome_AnyRiderDeadLostFighters()
    {
        Assert.Equal(DenRaidOutcome.CameHomeWhole, PartyRoadRules.RaidOutcome(0));
        Assert.Equal(DenRaidOutcome.LostFighters, PartyRoadRules.RaidOutcome(1));
    }

    [Fact]
    public void PlayerJoinOpen_WhileTheMatesStillWalkOver()
    {
        Assert.True(PartyRoadRules.PlayerJoinOpen(Now, Now));
        Assert.True(PartyRoadRules.PlayerJoinOpen(Now, Now + PartyRoadRules.PlayerJoinWindow));
        Assert.False(PartyRoadRules.PlayerJoinOpen(Now, Now + PartyRoadRules.PlayerJoinWindow + TimeSpan.FromSeconds(1)));
        Assert.False(PartyRoadRules.PlayerJoinOpen(Now, Now - TimeSpan.FromSeconds(1)));
        Assert.True(PartyRoadRules.PlayerJoinWindow <= PartyRoadRules.MusterLimit);
    }

    [Theory]
    [InlineData(RoadGroupKind.Convoy, true, false, false, true)]
    [InlineData(RoadGroupKind.Convoy, false, true, true, false)]
    [InlineData(RoadGroupKind.WarBand, false, true, false, true)]
    [InlineData(RoadGroupKind.WarBand, true, false, true, false)]
    [InlineData(RoadGroupKind.Sweep, false, false, true, true)]
    [InlineData(RoadGroupKind.Sweep, true, true, false, false)]
    [InlineData(RoadGroupKind.DenRaid, false, false, true, true)]
    [InlineData(RoadGroupKind.DenRaid, true, true, false, false)]
    [InlineData(RoadGroupKind.Posse, false, false, true, true)]
    [InlineData(RoadGroupKind.Posse, true, true, false, false)]
    public void PlayerFitsSide_AGuildmateForAConvoy_TheSameShieldForAWarBand_ABlueForTheRest(
        RoadGroupKind kind,
        bool guildmate,
        bool sameFactionSide,
        bool lawful,
        bool fits
    ) =>
        Assert.Equal(fits, PartyRoadRules.PlayerFitsSide(kind, guildmate, sameFactionSide, lawful));

    private static Destination Bank(string name, int x) =>
        new() { Name = name, Kind = TownTripRules.BankKind, X = x, Y = 1000 };
}
