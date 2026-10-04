using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ChatHelpRulesTests
{
    [Theory]
    [InlineData(BrainEventKind.DungeonEnded)]
    [InlineData(BrainEventKind.Attacked)]
    [InlineData(BrainEventKind.PlayerNoticed)]
    public void ChatMayHelp_ADecisionEvent(BrainEventKind kind) =>
        Assert.True(Brain.ChatMayHelp(BrainProviders.DecisionKind, kind, JevAsk.Plain));

    [Fact]
    public void ChatMayHelp_NeverAPlan() =>
        Assert.False(Brain.ChatMayHelp(BrainProviders.DecisionKind, BrainEventKind.Plan, JevAsk.Plain));

    [Fact]
    public void ChatMayHelp_NeverAChatCall_SoTheChatRouteCannotAskItself() =>
        Assert.False(Brain.ChatMayHelp(BrainProviders.ChatKind, BrainEventKind.Spoken, JevAsk.Plain));

    [Theory]
    [InlineData(JevAsk.Gate)]
    [InlineData(JevAsk.Intent)]
    public void ChatMayHelp_NeverATypedAsk(JevAsk ask) =>
        Assert.False(Brain.ChatMayHelp(BrainProviders.DecisionKind, BrainEventKind.Spoken, ask));

    [Theory]
    [InlineData(400, true)]
    [InlineData(422, true)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    [InlineData(429, false)]
    public void MayBeFieldRefusal_OnlyABadRequest(int code, bool refusal) =>
        Assert.Equal(refusal, BrainWorker.MayBeFieldRefusal(code));
}
