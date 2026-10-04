using System;
using Server;
using SosariaAI.Behaviour;
using Xunit;
using SosariaAI.Memory;

namespace SosariaAI.Tests;

public class ConversationHoldTests
{
    private static readonly DateTime Start = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
    private static readonly Serial Player = (Serial)5u;

    [Fact]
    public void NewHold_IsNotActive() => Assert.False(new ConversationHold().IsActive(Start));

    [Fact]
    public void Hold_StaysActiveInsideTheWindow()
    {
        var hold = new ConversationHold();
        hold.Hold(Player, Start, Window);

        Assert.True(hold.IsActive(Start + TimeSpan.FromSeconds(29)));
        Assert.Equal(Player, hold.Partner);
    }

    [Fact]
    public void Hold_EndsWhenTheWindowPasses()
    {
        var hold = new ConversationHold();
        hold.Hold(Player, Start, Window);

        Assert.False(hold.IsActive(Start + Window));
    }

    [Fact]
    public void Hold_ExtendsWithEachLine()
    {
        var hold = new ConversationHold();
        hold.Hold(Player, Start, Window);
        hold.Hold(Player, Start + TimeSpan.FromSeconds(20), Window);

        Assert.True(hold.IsActive(Start + TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public void Clear_EndsTheHold()
    {
        var hold = new ConversationHold();
        hold.Hold(Player, Start, Window);
        hold.Clear();

        Assert.False(hold.IsActive(Start + TimeSpan.FromSeconds(1)));
        Assert.Equal(Serial.Zero, hold.Partner);
    }

    [Fact]
    public void WaitForReply_IgnoresTheAttentionWindow()
    {
        var hold = new ConversationHold();
        hold.WaitForReply(Player, Start, ConversationHold.ReplyWaitCeiling);

        Assert.True(hold.IsActive(Start + Window));
        Assert.True(hold.IsActive(Start + TimeSpan.FromSeconds(119)));
    }

    [Fact]
    public void WaitForReply_CeilingEndsTheWait()
    {
        var hold = new ConversationHold();
        hold.WaitForReply(Player, Start, ConversationHold.ReplyWaitCeiling);

        Assert.False(hold.IsActive(Start + ConversationHold.ReplyWaitCeiling));
        Assert.True(hold.GiveUpIfDue(Start + ConversationHold.ReplyWaitCeiling));
        Assert.Equal(Serial.Zero, hold.Partner);
    }

    [Fact]
    public void HeardReply_ReturnsToTheNormalWindow()
    {
        var hold = new ConversationHold();
        hold.WaitForReply(Player, Start, ConversationHold.ReplyWaitCeiling);
        hold.HeardReply(Start + TimeSpan.FromSeconds(40), Window);

        Assert.True(hold.IsActive(Start + TimeSpan.FromSeconds(69)));
        Assert.False(hold.IsActive(Start + TimeSpan.FromSeconds(70)));
    }

    [Fact]
    public void GiveUpIfDue_DoesNothingWhileStillWaiting()
    {
        var hold = new ConversationHold();
        hold.WaitForReply(Player, Start, ConversationHold.ReplyWaitCeiling);

        Assert.False(hold.GiveUpIfDue(Start + TimeSpan.FromSeconds(10)));
        Assert.True(hold.IsActive(Start + TimeSpan.FromSeconds(10)));
    }
}
