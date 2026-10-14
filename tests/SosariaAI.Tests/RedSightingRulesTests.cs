using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class RedSightingRulesTests
{
    private const int SeedsToRoll = 4000;
    private const double ShareSlack = 0.03;

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void HunterRides_OnWordOfARedOrOnAStandingPatrol(bool word, bool standing, bool rides) =>
        Assert.Equal(rides, RedSightingRules.HunterRides(word, standing));

    [Fact]
    public void DrawsHunter_APairAtMost()
    {
        Assert.True(RedSightingRules.DrawsHunter(0));
        Assert.True(RedSightingRules.DrawsHunter(RedSightingRules.SightingRiders - 1));
        Assert.False(RedSightingRules.DrawsHunter(RedSightingRules.SightingRiders));
    }

    [Fact]
    public void SightingRiders_FewerThanAMurderDraws() =>
        Assert.True(RedSightingRules.SightingRiders < ConflictRules.MaxAnswerers);

    [Fact]
    public void PatrolsWithoutWord_OnlyAFewPhases()
    {
        var patrols = 0;

        for (var seed = 0; seed < SeedsToRoll; seed++)
        {
            patrols += RedSightingRules.PatrolsWithoutWord(seed) ? 1 : 0;
        }

        Assert.InRange(
            (double)patrols / SeedsToRoll,
            RedSightingRules.StandingPatrolShare - ShareSlack,
            RedSightingRules.StandingPatrolShare + ShareSlack
        );
    }

    [Fact]
    public void PatrolsWithoutWord_TheSameSeed_TheSamePhase() =>
        Assert.Equal(RedSightingRules.PatrolsWithoutWord(17), RedSightingRules.PatrolsWithoutWord(17));
}
