using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class RedRosterRulesTests
{
    [Theory]
    [InlineData(PersonJobs.Fighter, false, true, SkillTier.Journeyman, true)]
    [InlineData(PersonJobs.Fighter, true, false, SkillTier.Grandmaster, true)]
    [InlineData(PersonJobs.Fighter, false, false, SkillTier.Grandmaster, false)]
    [InlineData(PersonJobs.Fighter, false, true, SkillTier.Apprentice, false)]
    [InlineData(PersonJobs.Worker, false, true, SkillTier.Grandmaster, false)]
    [InlineData(PersonJobs.Thief, false, true, SkillTier.Grandmaster, false)]
    [InlineData(PersonJobs.Tamer, true, false, SkillTier.Grandmaster, false)]
    public void MayRideRed_OnlyAnEstablishedFighterWhoCasts(string job, bool caster, bool travelMagic, SkillTier tier, bool may) =>
        Assert.Equal(may, RedRosterRules.MayRideRed(job, caster, travelMagic, tier));

    [Fact]
    public void RideTogether_LonersPairOff_AnOddLastJoinsThePairBeforeIt()
    {
        // Two gangs of three, then five loners from the gang rules.
        int[] gangs = [0, 0, 0, 1, 1, 1, 2, 3, 4, 5, 6];

        var together = RedRosterRules.RideTogether(gangs);

        Assert.Equal([0, 0, 0, 1, 1, 1, 2, 2, 3, 3, 3], together);
        Assert.Equal((4, 0), RedRosterRules.Count(together));
    }

    [Fact]
    public void RideTogether_ASingleLonerJoinsTheLastGang()
    {
        int[] gangs = [0, 0, 1, 1, 1, 2];

        Assert.Equal([0, 0, 1, 1, 1, 1], RedRosterRules.RideTogether(gangs));
    }

    [Fact]
    public void RideTogether_AllLonersPairOff()
    {
        int[] gangs = [0, 1, 2, 3];

        Assert.Equal([0, 0, 1, 1], RedRosterRules.RideTogether(gangs));
    }

    [Fact]
    public void RideTogether_ALoneRedOfAOneRedFacetRidesAlone()
    {
        Assert.Equal([0], RedRosterRules.RideTogether([0]));
        Assert.Empty(RedRosterRules.RideTogether(null));
    }

    [Fact]
    public void RideTogether_TheGangRulesNeverLeaveARedAlone()
    {
        const int Reds = 150;
        const int GangPercent = 60;
        var gangs = Enumerable.Range(0, Reds).Select(slot => PkGangRules.GangOf(slot, Reds, GangPercent)).ToArray();

        Assert.True(RedRosterRules.Count(gangs).Alone > 0);
        Assert.Equal(0, RedRosterRules.Count(RedRosterRules.RideTogether(gangs)).Alone);
    }
}
