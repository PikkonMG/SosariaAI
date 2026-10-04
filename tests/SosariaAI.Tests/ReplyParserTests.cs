using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ReplyParserTests
{
    private const int MaxReply = 20;

    [Fact]
    public void ParseDecide_ReadsBareJson_AndIgnoresAMoodKey()
    {
        var (say, choose, act) = ReplyParser.ParseDecide("""{"say":"Bank is west.","mood":"tired"}""", 160);
        Assert.Equal("Bank is west.", say);
        Assert.Equal(string.Empty, choose);
        Assert.Equal(string.Empty, act);
    }

    [Fact]
    public void ParseDecide_StripsMarkdownFence()
    {
        var raw = """
                  ```json
                  {"say":"Aye.","mood":"calm"}
                  ```
                  """;
        var (say, _, _) = ReplyParser.ParseDecide(raw, 160);
        Assert.Equal("Aye.", say);
    }

    [Fact]
    public void ParseDecide_IgnoresLeadingAndTrailingProse()
    {
        var raw = "Sure, here you go: {\"say\":\"This way.\",\"mood\":\"brief\"} hope that helps";
        var (say, _, _) = ReplyParser.ParseDecide(raw, 160);
        Assert.Equal("This way.", say);
    }

    [Fact]
    public void ParseDecide_EmptySay_ReturnsEmptySay()
    {
        var (say, _, _) = ReplyParser.ParseDecide("""{"say":"","mood":"quiet"}""", 160);
        Assert.Equal(string.Empty, say);
    }

    [Fact]
    public void ParseDecide_Garbage_ReturnsEmptySay()
    {
        var (say, choose, act) = ReplyParser.ParseDecide("not json at all", 160);
        Assert.Equal(string.Empty, say);
        Assert.Equal(string.Empty, choose);
        Assert.Equal(string.Empty, act);
    }

    [Fact]
    public void ParseDecide_ReadsOptionalAct()
    {
        var (say, choose, act) = ReplyParser.ParseDecide(
            """{"choose":"town","say":"Later.","mood":"calm","act":"greet"}""",
            160
        );
        Assert.Equal("Later.", say);
        Assert.Equal("town", choose);
        Assert.Equal(ImmediateActs.Greet, act);
    }

    [Fact]
    public void ParseDecide_UnknownAct_IsIgnored()
    {
        var (_, choose, act) = ReplyParser.ParseDecide(
            """{"choose":"graveyard","act":"fly"}""",
            160
        );
        Assert.Equal("graveyard", choose);
        Assert.Equal(string.Empty, act);
        Assert.Null(ImmediateActs.Normalize("fly"));
    }

    [Fact]
    public void ParseDecide_TrimsSayAtWordBoundary()
    {
        var (say, _, _) = ReplyParser.ParseDecide("""{"say":"one two three four five"}""", MaxReply);
        Assert.True(say.Length <= MaxReply);
        Assert.Equal("one two three four", say);
        Assert.DoesNotContain("five", say);
    }
}
