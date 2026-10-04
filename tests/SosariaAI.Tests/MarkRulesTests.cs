using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class MarkRulesTests
{
    [Fact]
    public void MinMagery_SixthCircle()
    {
        Assert.True(RecallRules.MinMagery < MarkRules.MinMagery);
        Assert.True(MarkRules.ScrollMinMagery < MarkRules.MinMagery);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void WantsRuneHere_ABlankRuneAndNoRuneForThisPlaceYet(bool hasBlank, bool markedNear, bool wants) =>
        Assert.Equal(wants, MarkRules.WantsRuneHere(hasBlank, markedNear));

    [Theory]
    [InlineData(RecallRules.MinTripTiles, true)]
    [InlineData(RecallRules.MinTripTiles - 1, false)]
    public void TripWorthARune_OnlyAPlaceFarEnoughToRecallTo(int tripTiles, bool worth) =>
        Assert.Equal(worth, MarkRules.TripWorthARune(tripTiles));

    [Fact]
    public void TryMarkHere_NoCharacter_CastsNothing() =>
        Assert.False(MarkRules.TryMarkHere(null));
}
