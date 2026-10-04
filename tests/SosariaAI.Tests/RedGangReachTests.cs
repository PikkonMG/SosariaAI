using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class RedGangReachTests
{
    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, true, false)]
    public void Reaches_ByRoadOrRune_NeverUnderTheGuards(bool guarded, bool walkable, bool recallable, bool reaches) =>
        // A red sent to a Serpent's Hold door refused the walk into the guards at its first step.
        Assert.Equal(reaches, RedGangReach.Reaches(guarded, walkable, recallable));

    [Fact]
    public void AddUnreachableOutings_NoCharacter_AddsNothing()
    {
        var unmet = new System.Collections.Generic.List<string>();

        RedGangReach.AddUnreachableOutings(null, unmet);

        Assert.Empty(unmet);
    }
}
