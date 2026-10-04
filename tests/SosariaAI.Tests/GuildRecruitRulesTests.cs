using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GuildRecruitRulesTests
{
    [Fact]
    public void IsPlayerGuildTag_PluginTagsAreNotPlayerGuilds()
    {
        var pluginTag = GuildCatalog.All[0].Tag;

        Assert.False(GuildRecruitRules.IsPlayerGuildTag(pluginTag, GuildCatalog.All));
        Assert.False(GuildRecruitRules.IsPlayerGuildTag(pluginTag.ToLowerInvariant(), GuildCatalog.All));
        Assert.True(GuildRecruitRules.IsPlayerGuildTag("ZZQ", GuildCatalog.All));
        Assert.False(GuildRecruitRules.IsPlayerGuildTag(" ", GuildCatalog.All));
    }

    [Fact]
    public void JoinLine_NamesTheTag() =>
        Assert.Equal("zzq for life", GuildRecruitRules.JoinLine("ZZQ"));
}
