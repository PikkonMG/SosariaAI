using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ImmediateActsTests
{
    private const int MaxReply = 160;

    [Fact]
    public void AllowList_HoldsOnlyTheActsApplyActCarriesOut()
    {
        Assert.Equal(new[] { ImmediateActs.Greet, ImmediateActs.GoHunt, ImmediateActs.GoTown }, ImmediateActs.AllowList);
        Assert.Equal(ImmediateActs.GoTown, ImmediateActs.Normalize(" GO_TOWN "));
        Assert.Equal(ImmediateActs.GoHunt, ImmediateActs.Normalize("go_hunt"));
    }

    [Theory]
    [InlineData("follow")]
    [InlineData("rest")]
    public void ParseDecide_AnOldUnbackedActReadsAsNoAct(string oldAct)
    {
        // Answers written against the old shape still parse: the act is dropped, the rest stays.
        var (say, choose, act) = ReplyParser.ParseDecide(
            $$"""{"choose":"graveyard","say":"off to the graveyard","mood":"grim","act":"{{oldAct}}"}""",
            MaxReply
        );

        Assert.Null(ImmediateActs.Normalize(oldAct));
        Assert.Equal(string.Empty, act);
        Assert.Equal("graveyard", choose);
        Assert.Equal("off to the graveyard", say);
    }
}
