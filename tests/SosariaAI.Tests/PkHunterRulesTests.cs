using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class PkHunterRulesTests
{
    private const int PeopleToRoll = 2000;
    private const int SeedsToRoll = 2000;
    private const int PercentScale = 100;
    private const double ShareSlack = 0.05;
    private const string IdStem = "hal#";

    [Fact]
    public void IsHunter_OnlyAStrongBlueFighterWhoseDiceSaySo()
    {
        Assert.True(PkHunterRules.IsHunter(rollsHunter: true, fighter: true, red: false, PkHunterRules.MinTier));
        Assert.True(PkHunterRules.IsHunter(rollsHunter: true, fighter: true, red: false, SkillTier.Grandmaster));
        Assert.False(PkHunterRules.IsHunter(rollsHunter: false, fighter: true, red: false, SkillTier.Grandmaster));
        Assert.False(PkHunterRules.IsHunter(rollsHunter: true, fighter: false, red: false, SkillTier.Grandmaster));
        Assert.False(PkHunterRules.IsHunter(rollsHunter: true, fighter: true, red: true, SkillTier.Grandmaster));
        Assert.False(PkHunterRules.IsHunter(rollsHunter: true, fighter: true, red: false, PkHunterRules.MinTier - 1));
    }

    [Fact]
    public void RollsHunter_IsTheSameEveryBoot_AndAboutTheHunterShare()
    {
        var hunters = 0;

        for (var i = 0; i < PeopleToRoll; i++)
        {
            var id = IdStem + i;
            Assert.Equal(PkHunterRules.RollsHunter(id), PkHunterRules.RollsHunter(id));
            hunters += PkHunterRules.RollsHunter(id) ? 1 : 0;
        }

        Assert.InRange(
            (double)hunters / PeopleToRoll,
            (double)PkHunterRules.HunterPercent / PercentScale - ShareSlack,
            (double)PkHunterRules.HunterPercent / PercentScale + ShareSlack
        );
    }

    [Fact]
    public void HuntsThisPhase_MostPhasesButNotAll()
    {
        var hunts = 0;

        for (var seed = 0; seed < SeedsToRoll; seed++)
        {
            hunts += PkHunterRules.HuntsThisPhase(seed) ? 1 : 0;
        }

        Assert.InRange((double)hunts / SeedsToRoll, PkHunterRules.HuntShare - ShareSlack, PkHunterRules.HuntShare + ShareSlack);
    }

    [Fact]
    public void RidesToDen_NowAndThen_AndNeverPastTheCap()
    {
        var rides = 0;

        for (var seed = 0; seed < SeedsToRoll; seed++)
        {
            rides += PkHunterRules.RidesToDen(seed, huntersInDen: 0) ? 1 : 0;
            Assert.False(PkHunterRules.RidesToDen(seed, PkHunterRules.MaxInDen));
        }

        Assert.InRange((double)rides / SeedsToRoll, PkHunterRules.DenShare - ShareSlack, PkHunterRules.DenShare + ShareSlack);
    }
}
