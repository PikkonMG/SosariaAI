using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ActionDescriptionsTests
{
    [Fact]
    public void Phrase_UsesPlainWordsNotIds()
    {
        Assert.Equal("cut wood", ActionDescriptions.Phrase(SkillKinds.Lumberjack));
        Assert.Equal("sell at the vendor", ActionDescriptions.Phrase(SkillKinds.VendorSell));
        Assert.Equal("walk to the bank", ActionDescriptions.Phrase(SkillKinds.BankDeposit));
        Assert.Equal("rest at the inn", ActionDescriptions.Phrase(SkillKinds.Tavern));
        Assert.Equal("flee", ActionDescriptions.Phrase(SkillKinds.Flee));
        Assert.Equal("work the forge", ActionDescriptions.Phrase(SkillKinds.Smith));
        Assert.Equal("place a boat", ActionDescriptions.Phrase(SkillKinds.Boat));
        Assert.Equal("steal", ActionDescriptions.Phrase(SkillKinds.Steal));
        Assert.Equal("hide", ActionDescriptions.Phrase(SkillKinds.Hide));
        Assert.Equal("peek into a pack", ActionDescriptions.Phrase(SkillKinds.Snoop));
        Assert.Equal("bandage a wound", ActionDescriptions.Phrase(SkillKinds.Heal));
        Assert.Equal("brew a potion", ActionDescriptions.Phrase(SkillKinds.Alchemy));
        Assert.Equal("play peace", ActionDescriptions.Phrase(SkillKinds.Peace));
        Assert.Equal("track", ActionDescriptions.Phrase(SkillKinds.Track));
        Assert.DoesNotContain(":", ActionDescriptions.Phrase(SkillKinds.GoTo, "work:GoTo:1392,1724,5"));
    }
}
