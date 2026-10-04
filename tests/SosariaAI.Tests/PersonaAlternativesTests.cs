using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaAlternativesTests
{
    private static Persona Sample() =>
        new()
        {
            IdleLines = ["anyone need iron", "slow day", "pick broke again"],
            Greetings = ["hail {name}", "hey {name}", "well met"],
            LootLines = ["nice drop"]
        };

    [Fact]
    public void AlternativesTo_OffersTheRestOfTheSamePool()
    {
        var others = Sample().AlternativesTo("slow day", 0, null);

        Assert.Equal(["anyone need iron", "pick broke again"], others);
    }

    [Fact]
    public void AlternativesTo_TurnsWithTheOffset()
    {
        Assert.Equal("pick broke again", Sample().AlternativesTo("anyone need iron", 2, null)[0]);
    }

    [Fact]
    public void AlternativesTo_FillsTheSameNameIntoOtherGreetings()
    {
        var others = Sample().AlternativesTo("hail Bob", 0, null);

        Assert.Equal(["hey Bob", "well met"], others);
        Assert.Equal(["hail friend", "hey friend"], Sample().AlternativesTo("well met", 0, null));
    }

    [Fact]
    public void AlternativesTo_EmptyForALineThePersonaDidNotWrite()
    {
        Assert.Empty(Sample().AlternativesTo("the moongate is down", 0, null));
        Assert.Empty(Sample().AlternativesTo("nice drop", 0, null));
        Assert.Empty(Sample().AlternativesTo(null, 0, null));
    }

    [Fact]
    public void PickGreeting_StillFillsTheName()
    {
        var persona = new Persona { Greetings = ["hail {name}"] };

        Assert.Equal("hail Bob", persona.PickGreeting("Bob"));
        Assert.Equal("hail " + Persona.UnnamedGreetee, persona.PickGreeting(null));
    }

    private static Persona WithInvites() =>
        new()
        {
            IdleLines = ["anyone want to hunt ogres", "slow day", "pick broke again"],
            Greetings = ["{name}, you coming or what", "hey {name}"],
            ReturnLines = ["back, anyone coming?"],
            CombatLines = ["Detect hidden sweep, walk with me.", "orc on me"],
            LootLines = ["nice drop, come hunt with me"]
        };

    [Fact]
    public void Picks_NeverSayAnInvite()
    {
        var persona = WithInvites();

        for (var i = 0; i < 50; i++)
        {
            Assert.NotEqual("anyone want to hunt ogres", persona.PickIdleLine(null));
            Assert.Equal("hey Bob", persona.PickGreeting("Bob"));
            Assert.Equal("orc on me", persona.PickCombatLine());
        }

        Assert.Null(persona.PickReturnLine());
        Assert.Null(persona.PickLootLine());
    }

    [Fact]
    public void AlternativesTo_LeavesOutInvites()
    {
        Assert.Equal(["pick broke again"], WithInvites().AlternativesTo("slow day", 0, null));
        Assert.Empty(WithInvites().AlternativesTo("hey Bob", 0, null));
    }

    [Fact]
    public void PickIdleLine_SaysAJobLineOnlyAtThatJob()
    {
        // Ansgar stood at the Britain bank buying logs and said "lumberjacking is slow going today".
        var persona = new Persona { IdleLines = ["lumberjacking is slow going today", "quiet day"] };

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal("quiet day", persona.PickIdleLine(SkillKinds.BankShop));
            Assert.Equal("quiet day", persona.PickIdleLine(null));
        }

        var atWork = new Persona { IdleLines = ["lumberjacking is slow going today"] };
        Assert.Equal("lumberjacking is slow going today", atWork.PickIdleLine(SkillKinds.Lumberjack));
    }

    [Fact]
    public void AlternativesTo_LeavesOutIdleLinesForAnotherJob()
    {
        var persona = new Persona { IdleLines = ["slow day", "chopping all day heh", "nice weather"] };

        Assert.Equal(["nice weather"], persona.AlternativesTo("slow day", 0, SkillKinds.Mine));
        Assert.Equal(["chopping all day heh", "nice weather"], persona.AlternativesTo("slow day", 0, SkillKinds.Lumberjack));
    }
}
