using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A plan's "hunt-returned" success needs a kill on the trip. The count was read and never set,
/// so that success never passed.
/// </summary>
public class HuntTripKillsTests
{
    private const int TwoKills = 2;
    private const string Prey = "a mongbat";
    private static readonly DateTime TripStart = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    static HuntTripKillsTests() => Timer.Init(0);

    public HuntTripKillsTests() => TestMap.EnsureInternal();

    [Fact]
    public void NoteKill_CountsEachKillOfTheTrip()
    {
        var hunter = new SosariaCharacter((Serial)0x7E41) { Name = "Bran" };

        hunter.NoteKill(Prey);
        hunter.NoteKill(Prey);

        Assert.Equal(TwoKills, hunter.HuntKillsThisTrip);
        Assert.True(StepProof.MeetsSuccess(StepProof.SuccessHuntReturned, AtHomeWith(hunter.HuntKillsThisTrip)));
    }

    [Fact]
    public void BeginHuntTrip_StartsTheCountAgain()
    {
        var hunter = new SosariaCharacter((Serial)0x7E42) { Name = "Grim" };
        hunter.NoteKill(Prey);

        hunter.BeginHuntTrip(TripStart);

        Assert.Equal(0, hunter.HuntKillsThisTrip);
        Assert.Equal(TripStart, hunter.LastHuntAt);
        Assert.False(StepProof.MeetsSuccess(StepProof.SuccessHuntReturned, AtHomeWith(hunter.HuntKillsThisTrip)));
    }

    private static WorldFacts AtHomeWith(int kills) =>
        new(0, 0, 0, 0, false, false, "", "britain", AtHome: true, false, "", true, false, kills, 0, false);
}
