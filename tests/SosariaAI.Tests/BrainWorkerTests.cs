using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Server;
using SosariaAI.Deliberation;
using Xunit;
using static SosariaAI.Tests.LocalHttp;

namespace SosariaAI.Tests;

public class BrainWorkerTests
{
    private const string TestToken = "unit-test-token";
    private const string TestModel = "test-model";
    private const string BankReply =
        """{"choices":[{"message":{"role":"assistant","content":"{\"say\":\"Bank is west.\",\"mood\":\"calm\"}"}}]}""";
    private const string ReasoningOnlyReply =
        """{"choices":[{"message":{"role":"assistant","content":"","reasoning":"The player asks about the bank. I should answer briefly."}}]}""";

    [Fact]
    public async Task Complete_AsksWithThinkingOff_ByDefault()
    {
        var body = await FirstBody(CreateWorker);

        Assert.Contains("\"reasoning_effort\":\"none\"", body);
    }

    [Fact]
    public async Task Complete_SendsTheOperatorsLevel_WhenThinkingIsOn()
    {
        var body = await FirstBody((port, deliver) => CreateWorker(port, deliver, 1, thinking: true, reasoningEffort: "low"));

        Assert.Contains("\"reasoning_effort\":\"low\"", body);
    }

    [Fact]
    public async Task Complete_SendsNoLevel_WhenThinkingIsOnWithoutOne()
    {
        var body = await FirstBody((port, deliver) => CreateWorker(port, deliver, 1, thinking: true));

        Assert.DoesNotContain("reasoning_effort", body);
    }

    [Fact]
    public async Task Complete_DropsThinkingOff_WhenTheEndpointRefusesIt()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var bodies = new List<string>();

        var serve = Task.Run(async () =>
        {
            var refused = await listener.GetContextAsync();
            bodies.Add(await ReadBody(refused));
            refused.Response.StatusCode = 400;
            refused.Response.Close();

            var retried = await listener.GetContextAsync();
            bodies.Add(await ReadBody(retried));
            await WriteJson(retried, BankReply);

            var later = await listener.GetContextAsync();
            bodies.Add(await ReadBody(later));
            await WriteJson(later, BankReply);
        });

        var results = Channel.CreateUnbounded<BrainResult>();

        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result));
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));
        var first = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 2 }));
        var second = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("Bank is west.", first.Say);
        Assert.Equal("Bank is west.", second.Say);
        Assert.Contains("\"reasoning_effort\":\"none\"", bodies[0]);
        Assert.DoesNotContain("reasoning_effort", bodies[1]);
        Assert.DoesNotContain("reasoning_effort", bodies[2]);
    }

    [Fact]
    public async Task Complete_KeepsThinkingOff_WhenThePlainRetryFailsToo()
    {
        const int BadRequest = 400;
        var port = GetFreePort();
        using var listener = StartListener(port);
        var bodies = new List<string>();

        var serve = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var tooLong = await listener.GetContextAsync();
                bodies.Add(await ReadBody(tooLong));
                tooLong.Response.StatusCode = BadRequest;
                tooLong.Response.Close();
            }

            var later = await listener.GetContextAsync();
            bodies.Add(await ReadBody(later));
            await WriteJson(later, BankReply);
        });

        var results = Channel.CreateUnbounded<BrainResult>();

        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result));
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));
        var first = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 2 }));
        await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal($"HTTP {BadRequest}", first.Error);
        Assert.DoesNotContain("reasoning_effort", bodies[1]);
        Assert.Contains("\"reasoning_effort\":\"none\"", bodies[2]);
    }

    [Fact]
    public async Task Complete_KeepsThinkingOff_AfterARateLimit()
    {
        const int TooManyRequests = 429;
        var port = GetFreePort();
        using var listener = StartListener(port);
        var bodies = new List<string>();

        var serve = Task.Run(async () =>
        {
            var limited = await listener.GetContextAsync();
            bodies.Add(await ReadBody(limited));
            limited.Response.StatusCode = TooManyRequests;
            limited.Response.Close();

            var later = await listener.GetContextAsync();
            bodies.Add(await ReadBody(later));
            await WriteJson(later, BankReply);
        });

        var results = Channel.CreateUnbounded<BrainResult>();

        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result));
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));
        var first = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 2 }));
        await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal($"HTTP {TooManyRequests}", first.Error);
        Assert.Contains("\"reasoning_effort\":\"none\"", bodies[1]);
    }

    private static async Task<string> FirstBody(Func<int, Action<BrainResult>, BrainWorker> create)
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string body = null;

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            body = await ReadBody(ctx);
            await WriteJson(ctx, BankReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = create(port, result => delivered.TrySetResult(result));
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        return body;
    }

    [Fact]
    public async Task Complete_SendsHeaderModelAndMessages_ThenDeliversParsedReply()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string recordedAuth = null;
        string recordedPath = null;
        string recordedBody = null;

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            recordedAuth = ctx.Request.Headers["Authorization"];
            recordedPath = ctx.Request.Url?.AbsolutePath;
            recordedBody = await ReadBody(ctx);
            await WriteJson(
                ctx,
                """{"choices":[{"message":{"role":"assistant","content":"{\"say\":\"Bank is west.\",\"mood\":\"calm\"}"}}]}"""
            );
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("/v1/chat/completions", recordedPath);
        Assert.Equal($"Bearer {TestToken}", recordedAuth);
        using var doc = JsonDocument.Parse(recordedBody);
        Assert.Equal(TestModel, doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("system", doc.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("user", doc.RootElement.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Contains("where is the bank", doc.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Null(result.Error);
        Assert.Equal("Bank is west.", result.Say);
        Assert.Equal("frontier", result.ProviderName);
    }

    [Fact]
    public async Task Complete_PersonaWriteGetsARoomyReply_AndHandsBackTheRawJson()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string recordedBody = null;
        var persona = PersonaDraftSamples.ModelReply(PersonaDraftSamples.Valid());
        var reply = JsonSerializer.Serialize(
            new { choices = new[] { new { message = new { role = "assistant", content = persona } } } }
        );

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            recordedBody = await ReadBody(ctx);
            await WriteJson(ctx, reply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest() with { Kind = BrainEventKind.PersonaWrite }));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        using var doc = JsonDocument.Parse(recordedBody);
        Assert.Equal(PersonaPrompt.MaxReplyTokens, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Null(result.Error);
        Assert.Equal(BrainEventKind.PersonaWrite, result.Kind);
        Assert.Equal(persona, result.Raw);
    }

    [Fact]
    public async Task Complete_RetriesOnceOnServerErrorThenFails()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var requests = 0;

        var serve = Task.Run(async () =>
        {
            while (requests < 2)
            {
                var ctx = await listener.GetContextAsync();
                Interlocked.Increment(ref requests);
                _ = await ReadBody(ctx);
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
            }
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await serve.WaitAsync(TimeSpan.FromSeconds(15));
        worker.Dispose();

        Assert.Equal(2, requests);
        Assert.Equal("HTTP 500", result.Error);
        Assert.Equal(string.Empty, result.Say);
        Assert.Equal("frontier", result.ProviderName);
    }

    [Fact]
    public async Task Drain_OverlapsTwoHttpCalls_WhenParallelismIsTwo()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var secondArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serve = Task.Run(async () =>
        {
            var first = await listener.GetContextAsync();
            var second = await listener.GetContextAsync();
            secondArrived.TrySetResult();
            await WriteJson(first, BankReply);
            await WriteJson(second, BankReply);
        });

        var results = Channel.CreateUnbounded<BrainResult>();
        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result), maxConcurrentRequests: 2);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 2 }));

        await secondArrived.Task.WaitAsync(TimeSpan.FromSeconds(8));
        _ = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8));
        _ = await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8));
        await serve.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Dispose();
    }

    [Fact]
    public async Task Drain_ServesPlayerChatBeforeQueuedBotTalk()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var order = new List<long>();

        var serve = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var ctx = await listener.GetContextAsync();
                await WriteJson(ctx, BankReply);
            }
        });

        var results = Channel.CreateUnbounded<BrainResult>();
        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 1, HighPriority = false }));
        Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = 2, HighPriority = true }));
        worker.Start();

        order.Add((await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8))).RequestId);
        order.Add((await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8))).RequestId);
        await serve.WaitAsync(TimeSpan.FromSeconds(8));
        worker.Dispose();

        Assert.Equal([2L, 1L], order);
    }

    [Fact]
    public async Task Drain_LetsANormalItemThroughAfterTheHighStreakLimit()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var count = WorkerQueuePolicy.MaxHighBeforeNormal + 2;
        var order = new List<long>();

        var serve = Task.Run(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                var ctx = await listener.GetContextAsync();
                await WriteJson(ctx, BankReply);
            }
        });

        var results = Channel.CreateUnbounded<BrainResult>();
        using var worker = CreateWorker(port, result => results.Writer.TryWrite(result));

        for (var i = 1; i <= WorkerQueuePolicy.MaxHighBeforeNormal + 1; i++)
        {
            Assert.True(worker.TryEnqueue(SampleRequest() with { RequestId = i, HighPriority = true }));
        }

        Assert.True(
            worker.TryEnqueue(
                SampleRequest() with
                {
                    RequestId = 100,
                    HighPriority = false
                }
            )
        );
        worker.Start();

        for (var i = 0; i < count; i++)
        {
            order.Add((await results.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).RequestId);
        }

        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal(100, order[WorkerQueuePolicy.MaxHighBeforeNormal]);
    }

    private static BrainWorker CreateWorker(int port, TaskCompletionSource<BrainResult> delivered) =>
        CreateWorker(port, result => delivered.TrySetResult(result));

    private static BrainWorker CreateWorker(int port, Action<BrainResult> deliver) => CreateWorker(port, deliver, 1);

    private static BrainWorker CreateWorker(
        int port,
        Action<BrainResult> deliver,
        int maxConcurrentRequests,
        bool thinking = false,
        string reasoningEffort = null
    ) =>
        new(
            $"http://127.0.0.1:{port}/v1",
            TestToken,
            TestModel,
            0.8,
            160,
            TimeSpan.FromSeconds(5),
            deliver,
            maxConcurrentRequests,
            thinking,
            reasoningEffort,
            http: new HttpClient { Timeout = TimeSpan.FromSeconds(5) }
        );

    private static BrainRequest SampleRequest() =>
        new(
            1,
            (Serial)1u,
            (Serial)2u,
            "Connor",
            BrainEventKind.Spoken,
            "You are Connor.",
            "A player named Bob, standing next to you, says: 'where is the bank?'",
            ProviderName: "frontier"
        );
}
