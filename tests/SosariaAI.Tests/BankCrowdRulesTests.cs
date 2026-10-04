using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class BankCrowdRulesTests
{
    private const double ClassicStealthRequirement = 80;
    private const double NoSkill = 0;
    private const double GoodHiding = 50;
    private const double MasterHiding = 90;

    private static BankCrowdCandidate Plain(
        bool lawful = true,
        bool social = false,
        bool loner = false,
        bool greedy = false,
        bool resist = false,
        double hiding = NoSkill,
        bool goods = false,
        bool poor = false,
        bool green = false
    ) =>
        new(lawful, social, loner, greedy, false, resist, hiding, ClassicStealthRequirement, goods, poor, green);

    private const int Target = BankCrowdRules.MinCrowd + 2;
    private const int Visible = BankCrowdRules.VisibleBeforeHidden;

    private static List<BankCrowdRole> Regulars(int count) => [.. Enumerable.Repeat(BankCrowdRole.Regular, count)];

    [Fact]
    public void Choose_OutlawNeverJoins() =>
        Assert.Null(BankCrowdRules.Choose([], Plain(lawful: false, social: true), Target));

    [Fact]
    public void Choose_FullCrowdTakesNoOne()
    {
        var full = new List<BankCrowdRole>();

        for (var i = 0; i < Target; i++)
        {
            full.Add(BankCrowdRole.Afk);
        }

        Assert.Null(BankCrowdRules.Choose(full, Plain(social: true), Target));
    }

    [Fact]
    public void Choose_SkilledPeopleTrain()
    {
        var visible = Regulars(Visible);

        Assert.Equal(BankCrowdRole.StealthTrainer, BankCrowdRules.Choose(visible, Plain(hiding: MasterHiding), Target));
        Assert.Equal(BankCrowdRole.ResistTrainer, BankCrowdRules.Choose([], Plain(resist: true), Target));
        Assert.Equal(BankCrowdRole.Hider, BankCrowdRules.Choose(visible, Plain(hiding: GoodHiding), Target));
    }

    [Fact]
    public void Choose_HidersWaitForAVisibleCrowdAndStayOne()
    {
        // Sixty-two of 168 joins were hiders: a bank of people nobody can see.
        Assert.Equal(BankCrowdRole.Regular, BankCrowdRules.Choose([], Plain(hiding: MasterHiding), Target));

        var withHider = Regulars(Visible);
        withHider.Add(BankCrowdRole.Hider);

        Assert.NotEqual(BankCrowdRole.StealthTrainer, BankCrowdRules.Choose(withHider, Plain(hiding: MasterHiding), Target));
        Assert.False(BankCrowdRules.IsOpen(withHider, BankCrowdRole.Hider, BankCrowdRules.MaxCrowd));
    }

    [Fact]
    public void Choose_SocialHiderTalksTradeFirst() =>
        Assert.Equal(BankCrowdRole.Regular, BankCrowdRules.Choose(Regulars(1), Plain(social: true, hiding: MasterHiding), Target));

    [Fact]
    public void Choose_TemperPicksTheRole()
    {
        Assert.Equal(BankCrowdRole.Hawker, BankCrowdRules.Choose([], Plain(greedy: true, goods: true), Target));
        Assert.Equal(BankCrowdRole.Regular, BankCrowdRules.Choose([], Plain(social: true), Target));
        Assert.Equal(BankCrowdRole.Afk, BankCrowdRules.Choose([], Plain(loner: true), Target));
    }

    [Fact]
    public void Choose_StreetLife_ThePoorBegAndTheGreenAsk()
    {
        // Every 1999 bank had its beggar and its lost newbie, one of each.
        Assert.Equal(BankCrowdRole.Beggar, BankCrowdRules.Choose([], Plain(poor: true, social: true), Target));
        Assert.Equal(BankCrowdRole.Newbie, BankCrowdRules.Choose([], Plain(green: true, social: true), Target));
        Assert.Equal(
            BankCrowdRole.Regular,
            BankCrowdRules.Choose([BankCrowdRole.Beggar], Plain(poor: true, social: true), Target)
        );
        Assert.False(BankCrowdRules.Fits(BankCrowdRole.Beggar, Plain()));
        Assert.False(BankCrowdRules.Fits(BankCrowdRole.Newbie, Plain()));
    }

    [Fact]
    public void StopsTrailing_WhenThePlayerLeavesTheFloorOrTheTimeRunsOut()
    {
        var near = BankCrowdRules.StreetTrailTiles;
        var shortWhile = TimeSpan.FromSeconds(1);

        Assert.False(BankCrowdRules.StopsTrailing(0, near, shortWhile));
        Assert.True(BankCrowdRules.StopsTrailing(BankCrowdRules.LeaveRange, near, shortWhile));
        Assert.True(BankCrowdRules.StopsTrailing(0, BankCrowdRules.StreetNoticeTiles * 2, shortWhile));
        Assert.True(BankCrowdRules.StopsTrailing(0, near, BankCrowdRules.StreetTrail));
    }

    [Fact]
    public void HasRoom_ASeatPromisedAtTheChoiceIsNotFreeForTheNext()
    {
        // Ashen chose the crowd with one seat left, davey chose it a second later, davey began
        // first and took the seat, and Ashen failed "no place in the bank crowd fits".
        Assert.True(BankCrowdRules.HasRoom(seated: Target - 1, promised: 0, Target));
        Assert.False(BankCrowdRules.HasRoom(seated: Target - 1, promised: 1, Target));
        Assert.False(BankCrowdRules.HasRoom(seated: Target, promised: 0, Target));
        Assert.True(BankCrowdRules.HasRoom(seated: 0, promised: Target - 1, Target));
    }

    [Fact]
    public void Promise_HoldsFromTheChoiceToTheClaim()
    {
        var chosen = new DateTime(2026, 9, 27, 14, 1, 37, DateTimeKind.Utc);
        var until = chosen + BankCrowdRules.PromiseHold;

        Assert.True(BankCrowdRules.PromiseHolds(until, chosen));
        Assert.True(BankCrowdRules.PromiseHolds(until, chosen + TimeSpan.FromSeconds(BankCrowdRules.PromiseSeconds - 1)));
        Assert.False(BankCrowdRules.PromiseHolds(until, until));
    }

    [Fact]
    public void Choose_HawkerNeedsRealGoods()
    {
        var hawking = BankCrowdRules.Choose(Regulars(2), Plain(greedy: true), Target);

        Assert.NotEqual(BankCrowdRole.Hawker, hawking);
        Assert.False(BankCrowdRules.Fits(BankCrowdRole.Hawker, Plain(goods: false)));
    }

    [Fact]
    public void Choose_TakenRoleGivesWayToAnOpenOne()
    {
        var seated = Regulars(BankCrowdRules.Cap(BankCrowdRole.Regular, Target));

        Assert.Equal(BankCrowdRole.Afk, BankCrowdRules.Choose(seated, Plain(social: true, loner: true), Target));
        Assert.Equal(BankCrowdRole.Hawker, BankCrowdRules.Choose(seated, Plain(social: true, goods: true), Target));
    }

    [Theory]
    [InlineData(BankCrowdRules.MinCrowd)]
    [InlineData(BankCrowdRules.MinCrowd + 2)]
    [InlineData(BankCrowdRules.MaxCrowd)]
    public void Choose_CrowdFillsWithVariety(int target)
    {
        var seated = new List<BankCrowdRole>();

        while (BankCrowdRules.Choose(seated, Plain(social: true, goods: true, resist: true, hiding: MasterHiding), target) is { } role)
        {
            seated.Add(role);
        }

        Assert.Equal(target, seated.Count);
        Assert.True(BankCrowdRules.HiddenCount(seated) <= BankCrowdRules.HiddenCap);

        foreach (var role in Enum.GetValues<BankCrowdRole>())
        {
            Assert.True(BankCrowdRules.Count(seated, role) <= BankCrowdRules.Cap(role, target));
        }
    }

    [Fact]
    public void TargetFor_BusyTownsHoldMoreThanSmallOnes()
    {
        Assert.Equal(BankCrowdRules.MinCrowd, BankCrowdRules.TargetFor(0));
        Assert.Equal(BankCrowdRules.MinCrowd, BankCrowdRules.TargetFor(-1));
        Assert.Equal(BankCrowdRules.MaxCrowd, BankCrowdRules.TargetFor(int.MaxValue / 2));
        Assert.InRange(BankCrowdRules.TargetFor(BankCrowdRules.ResidentsPerMember * 5), 5, BankCrowdRules.MaxCrowd);
        Assert.True(BankCrowdRules.TargetFor(200) > BankCrowdRules.TargetFor(50));
        Assert.True(BankCrowdRules.MaxCrowd <= BankCrowdRules.SeatCount);
    }

    [Fact]
    public void Cap_BusyCrowdHoldsMoreRegularsHawkersAndStatues()
    {
        Assert.True(BankCrowdRules.Cap(BankCrowdRole.Regular, BankCrowdRules.MaxCrowd) >
                    BankCrowdRules.Cap(BankCrowdRole.Regular, BankCrowdRules.MinCrowd));
        Assert.Equal(BankCrowdRules.BusyMaxPerRole, BankCrowdRules.Cap(BankCrowdRole.Hawker, BankCrowdRules.BusyCrowd));
        Assert.Equal(BankCrowdRules.MaxPerRole, BankCrowdRules.Cap(BankCrowdRole.Afk, BankCrowdRules.MinCrowd));
        Assert.Equal(BankCrowdRules.MaxPerRole, BankCrowdRules.Cap(BankCrowdRole.Hider, BankCrowdRules.MaxCrowd));
    }

    [Fact]
    public void Canonical_TwoMarkersInOneHallAreOneBank()
    {
        var named = Bank("Magincia Bank", "Bank", 3730, 2161);
        var generated = Bank("Banker 3734-2149", BankCrowdRules.GeneratedBankerRole, 3734, 2149);
        var farTown = Bank("Bank of Minoc", "Bank of Minoc", 2503, 552);
        List<Destination> banks = [generated, named, farTown];

        Assert.Same(named, BankCrowdRules.Canonical(banks, generated));
        Assert.Same(named, BankCrowdRules.Canonical(banks, named));
        Assert.Same(farTown, BankCrowdRules.Canonical(banks, farTown));
        Assert.Null(BankCrowdRules.Canonical(banks, null));
    }

    private static Destination Bank(string name, string role, int x, int y) =>
        new() { Name = name, Kind = "Bank", Role = role, X = x, Y = y };

    [Fact]
    public void HoldLength_IsALongVisibleStretch()
    {
        for (var roll = 0; roll < BankCrowdRules.MaxHoldMinutes * 2; roll++)
        {
            var minutes = BankCrowdRules.HoldLength(roll, 1).TotalMinutes;
            Assert.InRange(minutes, BankCrowdRules.MinHoldMinutes, BankCrowdRules.MaxHoldMinutes);
        }

        Assert.True(BankCrowdRules.HoldLength(0, BankCrowdRules.MaxHoldScale) > BankCrowdRules.HoldLength(0, 1));
        Assert.Equal(BankCrowdRules.HoldLength(0, BankCrowdRules.MaxHoldScale), BankCrowdRules.HoldLength(0, double.MaxValue));
    }

    [Fact]
    public void SpeechGaps_KeepTheRateSane()
    {
        for (var roll = 0; roll < BankCrowdRules.RegularChatMaxSeconds * 2; roll++)
        {
            Assert.InRange(BankCrowdRules.ChatGap(roll).TotalSeconds, BankCrowdRules.RegularChatMinSeconds, BankCrowdRules.RegularChatMaxSeconds);
            Assert.InRange(BankCrowdRules.ShoutGap(roll).TotalSeconds, BankCrowdRules.HawkerShoutMinSeconds, BankCrowdRules.HawkerShoutMaxSeconds);
        }
    }

    [Fact]
    public void FreeSeat_TakesTheLowestOpenSeat()
    {
        Assert.Equal(0, BankCrowdRules.FreeSeat([]));
        Assert.Equal(1, BankCrowdRules.FreeSeat([0, 2]));

        var all = new List<int>();

        for (var seat = 0; seat < BankCrowdRules.SeatCount; seat++)
        {
            all.Add(seat);
        }

        Assert.Equal(BankCrowdRules.NoSeat, BankCrowdRules.FreeSeat(all));
    }

    [Fact]
    public void SeatSpot_SpreadsTheCrowdAroundTheBank()
    {
        var bank = new Point3D(1425, 1695, 0);
        var spots = new HashSet<Point3D>();

        for (var seat = 0; seat < BankCrowdRules.SeatCount; seat++)
        {
            var spot = BankCrowdRules.SeatSpot(bank, seat);
            Assert.Equal(BankCrowdRules.SeatRadius, NavMetric.Chebyshev(bank, spot));
            Assert.True(BankCrowdRules.AtBank(spot, bank));
            spots.Add(spot);
        }

        Assert.Equal(BankCrowdRules.SeatCount, spots.Count);
    }

    [Fact]
    public void HasLeft_WalkGraceThenStayAtTheBank()
    {
        var shortWhile = TimeSpan.FromMinutes(1);

        Assert.False(BankCrowdRules.HasLeft(arrived: false, atBank: false, shortWhile));
        Assert.True(BankCrowdRules.HasLeft(arrived: false, atBank: false, BankCrowdRules.WalkGrace + shortWhile));
        Assert.True(BankCrowdRules.HasLeft(arrived: true, atBank: false, shortWhile));
        Assert.False(BankCrowdRules.HasLeft(arrived: true, atBank: true, shortWhile));
        Assert.True(BankCrowdRules.HasLeft(arrived: true, atBank: true, BankCrowdRules.LongestHold + shortWhile));
    }

    [Fact]
    public void Chance_FollowsThePercent()
    {
        Assert.True(BankCrowdRules.Chance(0, BankCrowdRules.AfkLinePercent));
        Assert.False(BankCrowdRules.Chance(BankCrowdRules.AfkLinePercent, BankCrowdRules.AfkLinePercent));
    }
}
