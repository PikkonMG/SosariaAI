using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class TownTripRulesTests
{
    private const int Reach = 400;
    private const int SeedSweep = 50;
    private static readonly Point3D Britain = new(1425, 1695, 0);

    private static readonly Destination BritainBank = Bank("Britain Bank", 1430, 1690);
    private static readonly Destination MinocBank = Bank("Minoc Bank", 2500, 560);
    private static readonly Destination TrinsicBank = Bank("Trinsic Bank", 1900, 2800);
    private static readonly Destination MinocSmith = new()
    {
        Name = "Minoc Smith", Kind = "Vendor", X = 2520, Y = 560, Z = 0
    };

    [Fact]
    public void Pick_FromHome_OnlyABankInAnotherTown()
    {
        var places = new List<Destination> { BritainBank, MinocBank, MinocSmith, TrinsicBank };

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var pick = TownTripRules.Pick(places, Britain, Reach, null, null, seed, awayFromHome: false);

            Assert.True(pick == null || pick == MinocBank || pick == TrinsicBank, pick?.Name);
        }
    }

    [Fact]
    public void Pick_HoldsWhereverThePersonStands()
    {
        // A pick measured from where the person stood changed once it got there, and it
        // set off again for yet another town.
        var places = new List<Destination> { BritainBank, MinocBank, TrinsicBank };
        var trips = 0;

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var fromHome = TownTripRules.Pick(places, Britain, Reach, null, null, seed, awayFromHome: false);

            if (fromHome == null)
            {
                continue;
            }

            trips++;
            Assert.Same(fromHome, TownTripRules.Pick(places, fromHome.Arrival, Reach, null, null, seed, awayFromHome: true));
        }

        Assert.True(trips > 0);
    }

    [Fact]
    public void Pick_AvoidsPlacesThePersonRanFrom()
    {
        var places = new List<Destination> { MinocBank, TrinsicBank };
        var danger = new List<Point3D> { MinocBank.Arrival };

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            Assert.Same(TrinsicBank, TownTripRules.Pick(places, Britain, Reach, danger, null, seed, awayFromHome: false));
        }
    }

    [Fact]
    public void Pick_PassesOverABankTheRouterJustFoundNoWayTo()
    {
        // Serpent's Hold has no moongate: a walker from Skara Brae failed that trip and
        // spent the rest of its travel job standing about at home.
        var places = new List<Destination> { MinocBank, TrinsicBank };
        var unreachable = new List<Point3D> { TrinsicBank.Arrival };

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            Assert.Same(MinocBank, TownTripRules.Pick(places, Britain, Reach, null, unreachable, seed, awayFromHome: false));
        }
    }

    [Fact]
    public void Pick_NoOtherTown_IsNull()
    {
        Assert.Null(TownTripRules.Pick([BritainBank], Britain, Reach, null, null, seed: 1, awayFromHome: false));
        Assert.Null(TownTripRules.Pick(null, Britain, Reach, null, null, seed: 1, awayFromHome: false));
    }

    [Fact]
    public void Candidate_IdNamesTheBankAndTheStepWalksThere()
    {
        var trip = TownTripRules.Candidate(MinocBank);

        Assert.Equal(SkillKinds.Travel, trip.SkillKind);
        Assert.Equal(MinocBank.Arrival, trip.Step.Target);
        Assert.Equal(MinocBank.Name, TownTripRules.BankNameOf(trip.Id.Value));
        Assert.Null(TownTripRules.BankNameOf("work:Mine:0"));
    }

    private static Destination Bank(string name, int x, int y) =>
        new() { Name = name, Kind = TownTripRules.BankKind, X = x, Y = y, Z = 0 };
}
