using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>The bank-floor shout lines <see cref="ChatLines.Shout"/> says.</summary>
public class ChatLinesTests
{
    private const string Goods = "leather";
    private const string Price = "150";
    private const int Rolls = 32;

    [Fact]
    public void WtsLine_SaysTheGoodsAndThePrice()
    {
        var line = Talk.Line(TalkCategory.Wts, 0, new TalkSlots { Item = Goods, Price = Price });
        Assert.Equal("WTS leather 150", line);
    }

    [Fact]
    public void WtsLines_AllNameGoodsAndPrice()
    {
        for (var roll = 0; roll < Rolls; roll++)
        {
            var line = Talk.Line(TalkCategory.Wts, roll, new TalkSlots { Item = Goods, Price = Price });
            Assert.Contains(Goods, line);
            Assert.Contains(Price, line);
        }
    }

    [Fact]
    public void WtbLine_NeedsNoPrice()
    {
        var line = Talk.Line(TalkCategory.Wtb, 0, new TalkSlots { Item = Goods });
        Assert.Equal("WTB leather", line);
    }

    [Fact]
    public void ShoutWithoutGoods_SaysNothing()
    {
        Assert.Null(Talk.Line(TalkCategory.Wts, 0, new TalkSlots { Price = Price }));
        Assert.Null(Talk.Line(TalkCategory.Wtb, 0, default));
    }

    [Fact]
    public void GreetReply_NamesTheGreeter()
    {
        Assert.Equal("hey Sela", Talk.Line(TalkCategory.GreetReply, 0, new TalkSlots { Name = "Sela" }));
    }

    [Fact]
    public void EmoteBody_WrappedLine_GivesTheGesture()
    {
        Assert.Equal("stretches", Talk.EmoteBody("*stretches*"));
        Assert.Equal("waves at you", Talk.EmoteBody("*waves at you*"));
        Assert.Null(Talk.EmoteBody("hello there"));
        Assert.Null(Talk.EmoteBody("* not closed"));
        Assert.Null(Talk.EmoteBody("**"));
        Assert.Null(Talk.EmoteBody(null));
    }

    [Fact]
    public void EmoteLines_AllWrappedInStars()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (topic.Name is not TalkCategory.Emotes)
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                Assert.NotNull(Talk.EmoteBody(line));
            }
        }
    }

    [Fact]
    public void PlanFailed_NoDoing_StillSaysSomething()
    {
        // Kinds without a spoken gerund (bank runs, conflict) take a slot-free complaint.
        Assert.False(string.IsNullOrWhiteSpace(Talk.Line(TalkCategory.PlanFailed, 0, default)));
        Assert.False(string.IsNullOrWhiteSpace(Talk.Line(TalkCategory.PlanFailed, 7, default)));
    }

    [Fact]
    public void SmallTalkPools_Read1999Chat_LowercaseAndShort()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (topic.Name is not (TalkCategory.SmallTalk or TalkCategory.SmallTalkReply))
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                var spoken = line.Replace("{name}", string.Empty, StringComparison.Ordinal);
                Assert.Equal(spoken.ToLowerInvariant(), spoken);
                Assert.True(line.Length <= 60, line);
            }
        }
    }
}
