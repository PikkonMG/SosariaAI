using System;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TamePracticeTests
{
    private static readonly DateTime Start = new(2026, 9, 27, 20, 47, 12, DateTimeKind.Utc);

    public TamePracticeTests() => TestMap.EnsureInternal();

    [Fact]
    public void Close_RestsTheTamersPractice_ThenOpensItAgain()
    {
        // Katla tamed and let go snow leopards trip after trip all night.
        var katla = new SosariaCharacter((Serial)0x7C0001);
        var hedda = new SosariaCharacter((Serial)0x7C0002);

        Assert.True(TamePractice.IsOpen(katla, Start));

        TamePractice.Close(katla, Start);

        Assert.False(TamePractice.IsOpen(katla, Start));
        Assert.False(TamePractice.IsOpen(katla, Start + TameRules.PracticeRest - TimeSpan.FromSeconds(1)));
        Assert.True(TamePractice.IsOpen(katla, Start + TameRules.PracticeRest));
        Assert.True(TamePractice.IsOpen(hedda, Start));
    }

    [Fact]
    public void Close_TheRestIsTheTamersSavedClock()
    {
        var katla = new SosariaCharacter((Serial)0x7C0003);

        TamePractice.Close(katla, Start);

        Assert.Equal(Start, katla.RuleClocks[RuleClock.TamingSession]);
    }

    [Fact]
    public void IsOpen_AnyoneButAPersonHasNoSessions()
    {
        var player = new PlayerMobile((Serial)0x7C0004);

        Assert.True(TamePractice.IsOpen(player, Start));
    }
}
