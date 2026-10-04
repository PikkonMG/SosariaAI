using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class StartingMountRulesTests
{
    private const int People = 400;

    private static PersonProfile Profile(SkillTier tier, PersonWealth wealth) =>
        new(PersonClass.Warrior, tier, PersonTrait.None, wealth, ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female: false);

    [Fact]
    public void NovicesAndThePoor_Walk()
    {
        for (var i = 0; i < People; i++)
        {
            var id = $"Felucca:p#{i}";
            Assert.Equal(StartingMount.None, StartingMountRules.Pick(Profile(SkillTier.Apprentice, PersonWealth.Rich), id));
            Assert.Equal(StartingMount.None, StartingMountRules.Pick(Profile(SkillTier.Master, PersonWealth.Poor), id));
        }
    }

    [Fact]
    public void RichVeterans_MostlyRide_AndHorsesLead()
    {
        var mounts = Enumerable.Range(0, People)
            .Select(i => StartingMountRules.Pick(Profile(SkillTier.Master, PersonWealth.Rich), $"Felucca:p#{i}"))
            .ToList();

        Assert.True(mounts.Count(m => m != StartingMount.None) > People / 2);
        Assert.True(mounts.Count(m => m == StartingMount.Horse) > mounts.Count(m => m == StartingMount.Llama));
    }
}
