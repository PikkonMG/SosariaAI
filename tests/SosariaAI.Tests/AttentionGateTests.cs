using System;
using Server;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class AttentionGateTests
{
    private const string Name = "Connor";
    private static readonly Serial Character = (Serial)0x10;
    private static readonly Serial Speaker = (Serial)0x20;
    private static readonly Serial OtherSpeaker = (Serial)0x30;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
    private static readonly DateTime Start = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Hello Connor")]
    [InlineData("connor, what do you do?")]
    [InlineData("what do you do, CONNOR")]
    [InlineData("Connor's axe looks old")]
    public void MentionsName_FindsWholeWordAnywhere(string text)
    {
        Assert.True(AttentionGate.MentionsName(text, Name));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("Connors are everywhere")]
    [InlineData("O'Connor is here")]
    [InlineData("")]
    public void MentionsName_IgnoresOtherWords(string text)
    {
        Assert.False(AttentionGate.MentionsName(text, Name));
    }

    [Theory]
    [InlineData("Connor")]
    [InlineData("connor!")]
    [InlineData("  Connor? ")]
    public void IsOnlyName_TrueForBareName(string text)
    {
        Assert.True(AttentionGate.IsOnlyName(text, Name));
    }

    [Fact]
    public void IsOnlyName_FalseWhenMoreWordsFollow()
    {
        Assert.False(AttentionGate.IsOnlyName("Connor hello", Name));
    }

    [Fact]
    public void ShouldListen_IgnoresSpeechWithoutNameAndWithoutWindow()
    {
        var gate = new AttentionGate();

        Assert.False(gate.ShouldListen(Character, Name, Speaker, "hello", Start, Window));
    }

    [Fact]
    public void ShouldListen_NameOpensWindowForThatSpeakerOnly()
    {
        var gate = new AttentionGate();

        Assert.True(gate.ShouldListen(Character, Name, Speaker, "hello Connor", Start, Window));
        Assert.True(gate.ShouldListen(Character, Name, Speaker, "what do you do?", Start + TimeSpan.FromSeconds(10), Window));
        Assert.False(gate.ShouldListen(Character, Name, OtherSpeaker, "what do you do?", Start + TimeSpan.FromSeconds(10), Window));
    }

    [Fact]
    public void ShouldListen_WindowCloses()
    {
        var gate = new AttentionGate();
        gate.Open(Character, Speaker, Start, Window);

        Assert.True(gate.ShouldListen(Character, Name, Speaker, "still there?", Start + Window - TimeSpan.FromSeconds(1), Window));
        Assert.False(gate.ShouldListen(Character, Name, Speaker, "still there?", Start + Window, Window));
    }

    [Fact]
    public void Open_ExtendsAnExistingWindow()
    {
        var gate = new AttentionGate();
        gate.Open(Character, Speaker, Start, Window);
        gate.Open(Character, Speaker, Start + TimeSpan.FromSeconds(20), Window);

        Assert.True(gate.ShouldListen(Character, Name, Speaker, "one more", Start + TimeSpan.FromSeconds(45), Window));
    }

    [Theory]
    [InlineData("hi Robard", "Robard Fairbairn")]
    [InlineData("robard, got a sec?", "Robard Fairbairn")]
    [InlineData("hey Hedda", "Dame Hedda")]
    [InlineData("Terrin how are u", "Terrin of Jhelom")]
    [InlineData("yo stormbringer", "xXStormbringerXx")]
    [InlineData("hi Robard Fairbairn", "Robard Fairbairn")]
    public void MentionsName_HearsTheNamePeopleCallBy(string text, string name) =>
        Assert.True(AttentionGate.MentionsName(text, name));

    [Theory]
    [InlineData("hi Fairbairn", "Robard Fairbairn")]
    [InlineData("big news everyone", "Big Tom")]
    [InlineData("the green one", "Gudrun the Green")]
    public void MentionsName_IgnoresOtherPartsOfTheName(string text, string name) =>
        Assert.False(AttentionGate.MentionsName(text, name));

    [Fact]
    public void IsOnlyName_TrueForTheCallingName() =>
        Assert.True(AttentionGate.IsOnlyName("Robard?", "Robard Fairbairn"));
}
