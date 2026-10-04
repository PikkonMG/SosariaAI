using System;
using System.Threading.Tasks;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class ChatCompletionsTests
{
    private static readonly Uri Endpoint = new("http://127.0.0.1:11434/v1/chat/completions");
    private static readonly Uri OpenRouter = new("https://openrouter.ai/api/v1/chat/completions");

    [Fact]
    public async Task CreateRequest_SendsReasoningEffortWhenSet()
    {
        using var message = ChatCompletions.CreateRequest(Endpoint, "k", "m", [], 0.5, 64, "none");
        var body = await message.Content!.ReadAsStringAsync();

        Assert.Contains("\"reasoning_effort\":\"none\"", body);
    }

    [Fact]
    public void MaxTokensForReply_GivesDecideRepliesMoreRoom()
    {
        var chat = ChatCompletions.MaxTokensForReply(160, decideShape: false, planShape: false);
        var decide = ChatCompletions.MaxTokensForReply(160, decideShape: true, planShape: false);

        Assert.Equal(40 + ChatCompletions.JsonOverheadTokens + ChatCompletions.EscapeHeadroomTokens, chat);
        Assert.Equal(40 + ChatCompletions.DecideOverheadTokens + ChatCompletions.EscapeHeadroomTokens, decide);
        var plan = ChatCompletions.MaxTokensForReply(160, decideShape: false, planShape: true);
        Assert.True(plan > decide);
        Assert.Equal(
            ChatCompletions.PlanMinContentTokens + ChatCompletions.PlanOverheadTokens + ChatCompletions.EscapeHeadroomTokens,
            plan
        );
        Assert.True(plan >= 500);
    }

    [Fact]
    public async Task CreateRequest_OpenRouterGetsItsReasoningObject()
    {
        using var message = ChatCompletions.CreateRequest(OpenRouter, "k", "m", [], 0.5, 64, "none");
        var body = await message.Content!.ReadAsStringAsync();

        Assert.Contains("\"reasoning\":{\"effort\":\"none\"}", body);
        Assert.DoesNotContain("reasoning_effort", body);
    }

    [Fact]
    public async Task CreateRequest_OmitsReasoningEffortWhenNull()
    {
        using var message = ChatCompletions.CreateRequest(Endpoint, "k", "m", [], 0.5, 64, null);
        var body = await message.Content!.ReadAsStringAsync();

        Assert.DoesNotContain("reasoning_effort", body);
    }
}
