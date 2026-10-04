using System;
using Server;
using SosariaAI.Economy;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class BankDepositRulesTests
{
    [Fact]
    public void IsWorthATrip_NeedsAJob()
    {
        Assert.False(BankDepositRules.IsWorthATrip(bankableItems: 0, suppliesInBox: 0, purseNeedsBanker: false, endsPartyTrip: false, criminal: false));
        Assert.True(BankDepositRules.IsWorthATrip(bankableItems: 1, suppliesInBox: 0, purseNeedsBanker: false, endsPartyTrip: false, criminal: false));
        Assert.True(BankDepositRules.IsWorthATrip(bankableItems: 0, suppliesInBox: 1, purseNeedsBanker: false, endsPartyTrip: false, criminal: false));
        Assert.True(BankDepositRules.IsWorthATrip(bankableItems: 0, suppliesInBox: 0, purseNeedsBanker: true, endsPartyTrip: false, criminal: false));
        Assert.True(BankDepositRules.IsWorthATrip(bankableItems: 0, suppliesInBox: 0, purseNeedsBanker: false, endsPartyTrip: true, criminal: false));
    }

    [Fact]
    public void IsWorthATrip_NotForACriminal()
    {
        // The banker will not open a criminal's box: the trip would only end at the counter.
        Assert.False(BankDepositRules.IsWorthATrip(bankableItems: 5, suppliesInBox: 1, purseNeedsBanker: true, endsPartyTrip: false, criminal: true));
    }

    [Fact]
    public void StaffedBank_ThePlannedBankWhenABankerWorksThere()
    {
        var britain = WorkSites.BritainTown;
        var minoc = WorkSites.MinocTown;

        Assert.Equal(britain, BankDepositRules.StaffedBank(britain, [minoc], _ => true));
    }

    [Fact]
    public void StaffedBank_CoveHasNoBanker_SoTheTripWalksToTheNearestStaffedBank()
    {
        // Cove miners walked to the provisioner's door and failed "no banker works at this
        // bank" seventeen times in fifteen minutes.
        var cove = WorkSites.CoveTown;
        var minoc = WorkSites.MinocTown;
        var vesper = WorkSites.VesperTown;
        var staffed = new[] { vesper };

        Assert.Equal(vesper, BankDepositRules.StaffedBank(cove, [minoc, vesper], spot => Array.IndexOf(staffed, spot) >= 0));
    }

    [Fact]
    public void StaffedBank_NoBankerAnywhere_IsNoBank()
    {
        Assert.Equal(Point3D.Zero, BankDepositRules.StaffedBank(WorkSites.CoveTown, [WorkSites.MinocTown], _ => false));
        Assert.Equal(Point3D.Zero, BankDepositRules.StaffedBank(WorkSites.CoveTown, [], _ => false));
    }
}
