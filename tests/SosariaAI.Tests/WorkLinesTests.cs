using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class WorkLinesTests
{
    [Theory]
    [InlineData("lumberjacking is slow going today", SkillKinds.Lumberjack)]
    [InlineData("mining is slow today", SkillKinds.Mine)]
    [InlineData("this anvil been here forever", SkillKinds.Smith)]
    [InlineData("gotta smelt this before dark", SkillKinds.Mine)]
    [InlineData("quiet out here today, good for sewing", SkillKinds.Tailor)]
    [InlineData("brb need to bait the hooks", SkillKinds.Fish)]
    public void FitsWork_TrueAtTheJobTheLineNames(string line, string doing)
    {
        Assert.True(WorkLines.FitsWork(line, doing));
    }

    [Theory]
    [InlineData("lumberjacking is slow going today", SkillKinds.BankShop)]
    [InlineData("lumberjacking is slow going today", null)]
    [InlineData("mining is slow today", SkillKinds.Lumberjack)]
    [InlineData("this anvil been here forever", SkillKinds.Fish)]
    [InlineData("quiet out here today, good for sewing", SkillKinds.Conflict)]
    public void FitsWork_FalseAwayFromThatJob(string line, string doing)
    {
        Assert.False(WorkLines.FitsWork(line, doing));
    }

    [Theory]
    [InlineData("price of ore is up again i swear")]
    [InlineData("gm carpenter someday heh")]
    [InlineData("someone sell me iron ore")]
    [InlineData("quiet day")]
    [InlineData("")]
    [InlineData(null)]
    public void FitsWork_TrueForMarketTalkHopesAndPlainChatAnywhere(string line)
    {
        Assert.True(WorkLines.FitsWork(line, SkillKinds.BankShop));
        Assert.True(WorkLines.FitsWork(line, null));
    }
}
