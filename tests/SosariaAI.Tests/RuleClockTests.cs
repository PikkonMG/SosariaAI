using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class RuleClockTests
{
    private const uint Aldo = 0x7D0101;
    private const uint Brisa = 0x7D0102;
    private const uint Cato = 0x7D0103;
    private const uint Dagny = 0x7D0104;
    private const uint FirstPet = 0x7D0201;
    private const uint SecondPet = 0x7D0202;

    private static readonly DateTime Start = new(2026, 9, 28, 3, 12, 40, DateTimeKind.Utc);

    public RuleClockTests() => TestMap.EnsureInternal();

    private static SosariaCharacter Person(uint serial) => new((Serial)serial);

    [Fact]
    public void StartClock_WritesTheSavedFieldAsAnAbsoluteTime()
    {
        var aldo = Person(Aldo);

        Assert.Equal(default(DateTime), aldo.ClockAt(RuleClock.DenRaid));

        aldo.StartClock(RuleClock.DenRaid, Start);

        Assert.Equal(Start, aldo.ClockAt(RuleClock.DenRaid));
        Assert.Equal(Start, aldo.RuleClocks[RuleClock.DenRaid]);

        aldo.StopClock(RuleClock.DenRaid);

        Assert.Empty(aldo.RuleClocks);
    }

    [Fact]
    public void SavedClocks_KeepTheRestOfTheLoadedPersonToTheSameMoment()
    {
        // A copy of the saved field is what a load after a restart holds: the Den raid rest
        // ends three hours after the ride, not three hours after the boot.
        var brisa = Person(Brisa);
        var loaded = Person(Brisa);
        brisa.StartClock(RuleClock.DenRaid, Start);

        foreach (var (clock, at) in brisa.RuleClocks)
        {
            loaded.RuleClocks[clock] = at;
        }

        var restEnd = Start + PartyRoadRules.DenRaidRest;

        Assert.False(TimeRules.Rested(loaded.ClockAt(RuleClock.DenRaid), restEnd - TimeSpan.FromSeconds(1), PartyRoadRules.DenRaidRest));
        Assert.True(TimeRules.Rested(loaded.ClockAt(RuleClock.DenRaid), restEnd, PartyRoadRules.DenRaidRest));
    }

    [Fact]
    public void StopClocks_StopsOnlyThePrefixedClocksNotKept()
    {
        var cato = Person(Cato);
        var kept = RuleClock.LostPet((Serial)FirstPet);
        var found = RuleClock.LostPet((Serial)SecondPet);
        cato.StartClock(kept, Start);
        cato.StartClock(found, Start);
        cato.StartClock(RuleClock.DuelFought, Start);

        cato.StopClocks(RuleClock.LostPetPrefix, [kept]);

        Assert.Equal(Start, cato.ClockAt(kept));
        Assert.Equal(default(DateTime), cato.ClockAt(found));
        Assert.Equal(Start, cato.ClockAt(RuleClock.DuelFought));

        cato.StopClocks(RuleClock.LostPetPrefix, null);

        Assert.Equal(default(DateTime), cato.ClockAt(kept));
        Assert.Equal(Start, cato.ClockAt(RuleClock.DuelFought));
    }

    [Fact]
    public void LostPet_EachPetHasItsOwnClock()
    {
        var first = RuleClock.LostPet((Serial)FirstPet);

        Assert.StartsWith(RuleClock.LostPetPrefix, first);
        Assert.NotEqual(first, RuleClock.LostPet((Serial)SecondPet));
    }

    [Fact]
    public void Duels_RestAfterADuelIsTheDuelistsClock()
    {
        var dagny = Person(Dagny);

        Assert.True(Duels.Rested(dagny, Start));

        dagny.StartClock(RuleClock.DuelFought, Start);

        Assert.False(Duels.Rested(dagny, Start + DuelRules.Rest - TimeSpan.FromSeconds(1)));
        Assert.True(Duels.Rested(dagny, Start + DuelRules.Rest));
        Assert.False(Duels.Rested(null, Start));
    }
}
