using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GuildChatTests
{
    private const byte GuildType = 0x0D;
    private const byte EncodedFlag = 0xC0;
    private const byte RegularType = 0x00;
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void ReadGuildLine_PlainGuildLine_ReturnsTheText()
    {
        var packet = Packet(GuildType, BigUnicode("  what is everyone doing  "));
        var reader = new SpanReader(packet);

        Assert.Equal("what is everyone doing", GuildChatHook.ReadGuildLine(ref reader));
    }

    [Fact]
    public void ReadGuildLine_EncodedGuildLine_SkipsTheKeywords()
    {
        // One keyword: count 1 in the top twelve bits, then a single byte for the first id.
        var keywords = new byte[] { 0x00, 0x10, 0x2A };
        var packet = Packet((byte)(GuildType | EncodedFlag), [.. keywords, .. Utf8("lfg despise")]);
        var reader = new SpanReader(packet);

        Assert.Equal("lfg despise", GuildChatHook.ReadGuildLine(ref reader));
    }

    [Fact]
    public void ReadGuildLine_OrdinarySpeech_IsNotAGuildLine()
    {
        var packet = Packet(RegularType, BigUnicode("hail"));
        var reader = new SpanReader(packet);

        Assert.Null(GuildChatHook.ReadGuildLine(ref reader));
    }

    [Fact]
    public void ReadGuildLine_CutShortPacket_IsNull()
    {
        var reader = new SpanReader(new byte[] { GuildType, 0x00 });

        Assert.Null(GuildChatHook.ReadGuildLine(ref reader));
    }

    [Fact]
    public void NextChatter_FallsInsideTheGap()
    {
        Assert.Equal(Now + GuildChatRules.ChatterGapMin, GuildChatRules.NextChatter(Now, 0));
        Assert.Equal(Now + GuildChatRules.ChatterGapMax, GuildChatRules.NextChatter(Now, 100));
        Assert.True(GuildChatRules.ChatterDue(default, Now));
        Assert.False(GuildChatRules.ChatterDue(Now + TimeSpan.FromSeconds(1), Now));
    }

    [Fact]
    public void DelayFor_StaggersTheAnswers()
    {
        Assert.Equal(GuildChatRules.AnswerDelay, GuildChatRules.DelayFor(0));
        Assert.Equal(GuildChatRules.AnswerDelay + GuildChatRules.AnswerStep * 2, GuildChatRules.DelayFor(2));
    }

    [Fact]
    public void CallStillOpen_OnlyInsideTheWindow()
    {
        Assert.False(GuildChatRules.CallStillOpen(default, Now));
        Assert.True(GuildChatRules.CallStillOpen(Now, Now + GuildChatRules.OpenCallWindow));
        Assert.False(GuildChatRules.CallStillOpen(Now, Now + GuildChatRules.OpenCallWindow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Lines_FillNameAndPlace()
    {
        Assert.Equal("wb Mira", Talk.Line(TalkCategory.GuildWelcome, 0, new TalkSlots { Name = "Mira" }));
        Assert.Equal(
            "anyone wanna hunt despise?",
            Talk.Line(TalkCategory.GuildAskGroup, 0, new TalkSlots { Place = "despise" })
        );
    }

    private static byte[] Packet(byte type, byte[] body)
    {
        var bytes = new List<byte> { type, 0x00, 0x34, 0x00, 0x03 };
        bytes.AddRange(Encoding.ASCII.GetBytes("ENU\0"));
        bytes.AddRange(body);
        return [.. bytes];
    }

    private static byte[] BigUnicode(string text) => [.. Encoding.BigEndianUnicode.GetBytes(text), 0x00, 0x00];

    private static byte[] Utf8(string text) => [.. Encoding.UTF8.GetBytes(text), 0x00];
}
