using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Server;
using SosariaAI.Deliberation;
using Xunit;
using static SosariaAI.Tests.LocalHttp;

namespace SosariaAI.Tests;

public class JevWorkerTests
{
    private const string TestToken = "unit-test-token";
    private const string TestModel = "jev-latest";
    private const string WorkReply =
        """{"model":"jev-1.13.0","answers":{"next":{"type":"choice","choice":"work","probabilities":{"work":0.8,"loiter":0.2},"confidence":0.6},"act":{"type":"choice","choice":"greet","probabilities":{"none":0.2,"greet":0.8},"confidence":0.7}},"usage":{"input_tokens":300,"output_tokens":20}}""";
    private const string ThinReply =
        """{"model":"jev-1.13.0","answers":{"next":{"type":"choice","choice":"work","probabilities":{"work":0.5,"loiter":0.5},"confidence":0.2}},"usage":{"input_tokens":300,"output_tokens":20}}""";
    // Laya's own confidence is 1 minus the normalised entropy; Von's is the gap between the top
    // two probabilities. Jev's measure from the probabilities is 0.8 for both replies.
    private const string LayaReply =
        """{"model":"laya-rl-agent","answers":{"next":{"type":"choice","choice":"work","probabilities":{"work":0.9,"loiter":0.05,"rest":0.05},"confidence":0.2,"answer_confidence":0.9}},"usage":{"input_tokens":200,"output_tokens":0}}""";
    private const string VonReply =
        """{"model":"von-1.2.0","answers":{"next":{"type":"choice","choice":"work","probabilities":{"work":0.85,"loiter":0.15,"rest":0.0},"confidence":0.4}},"usage":{"input_tokens":0,"output_tokens":0}}""";
    private const string EmptyReply = """{"model":"jev-1.13.0","answers":{},"usage":{"input_tokens":10,"output_tokens":0}}""";
    private const string IntentReply =
        """{"model":"jev-1.13.0","answers":{"intent":{"type":"choice","choice":"Party","probabilities":{"Party":0.9,"Other":0.1},"confidence":0.8}},"usage":{"input_tokens":120,"output_tokens":8}}""";
    private const string GateReply =
        """{"model":"jev-1.13.0","answers":{"reply":{"type":"noul","noul":0.83}},"usage":{"input_tokens":180,"output_tokens":10}}""";

    [Fact]
    public async Task Complete_PostsSystemOneShape_ThenDeliversChoice()
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
            await WriteJson(ctx, WorkReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("/v1/systemone", recordedPath);
        Assert.Equal($"Bearer {TestToken}", recordedAuth);
        using var doc = JsonDocument.Parse(recordedBody);
        Assert.Equal(TestModel, doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("Britain", doc.RootElement.GetProperty("state").GetProperty("situation").GetProperty("place").GetString());
        var next = doc.RootElement.GetProperty("questions").GetProperty("next");
        Assert.Equal("choice", next.GetProperty("type").GetString());
        Assert.Equal("cut wood", next.GetProperty("criteria").GetProperty("work").GetString());
        Assert.Null(result.Error);
        Assert.Equal("work", result.Choose);
        Assert.Equal("greet", result.Act);
        Assert.Equal(300, result.InputTokens);
    }

    [Fact]
    public async Task Complete_GateRequest_ReturnsTheNoul()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string recordedBody = null;

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            recordedBody = await ReadBody(ctx);
            await WriteJson(ctx, GateReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleGateRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        using var doc = JsonDocument.Parse(recordedBody);
        var reply = doc.RootElement.GetProperty("questions").GetProperty("reply");
        Assert.Equal("noul", reply.GetProperty("type").GetString());
        Assert.Null(result.Error);
        Assert.Equal(JevAsk.Gate, result.Ask);
        Assert.Equal(0.83, result.Gate);
        Assert.Equal(180, result.InputTokens);
    }

    [Fact]
    public async Task Complete_GateRequest_Fails_WhenTheReplyHasNoNoul()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, EmptyReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleGateRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("no gate answer in reply", result.Error);
        Assert.Equal(JevAsk.Gate, result.Ask);
    }

    [Fact]
    public async Task Complete_IntentRequest_ReturnsTheIntent()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string recordedBody = null;

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            recordedBody = await ReadBody(ctx);
            await WriteJson(ctx, IntentReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleIntentRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        using var doc = JsonDocument.Parse(recordedBody);
        Assert.Equal("choice", doc.RootElement.GetProperty("questions").GetProperty("intent").GetProperty("type").GetString());
        Assert.Null(result.Error);
        Assert.Equal(JevAsk.Intent, result.Ask);
        Assert.Equal("Party", result.Choose);
        Assert.Equal(120, result.InputTokens);
    }

    [Fact]
    public async Task Complete_IntentRequest_Fails_WhenTheReplyHasNoIntent()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, EmptyReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleIntentRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("no intent in reply", result.Error);
        Assert.Equal(JevAsk.Intent, result.Ask);
    }

    [Fact]
    public async Task Complete_AnswersRequest_HandsBackEveryAnswer()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, IntentReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleIntentRequest() with { Ask = JevAsk.Answers }));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Null(result.Error);
        Assert.Equal(JevAsk.Answers, result.Ask);
        Assert.Equal("Party", result.Answers["intent"].Choice);
    }

    [Fact]
    public async Task Complete_AnswersRequest_WithNoAnswers_IsNotAFailure()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, EmptyReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleIntentRequest() with { Ask = JevAsk.Answers }));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Null(result.Error);
        Assert.Empty(result.Answers);
    }

    [Fact]
    public async Task Complete_SendsNoKeyHeader_WhenNoKeyIsSet()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        string recordedAuth = "sentinel";

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            recordedAuth = ctx.Request.Headers["Authorization"];
            await WriteJson(ctx, WorkReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = new JevWorker(
            $"http://127.0.0.1:{port}",
            null,
            TestModel,
            TimeSpan.FromSeconds(5),
            result => delivered.TrySetResult(result),
            http: new HttpClient { Timeout = TimeSpan.FromSeconds(5) }
        );
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Null(recordedAuth);
    }

    [Fact]
    public async Task Complete_DropsChoice_WhenConfidenceIsBelowTheFloor()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, ThinReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered, minConfidence: 0.5);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Null(result.Error);
        Assert.Null(result.Choose);
    }

    [Theory]
    [InlineData(LayaReply)]
    [InlineData(VonReply)]
    public async Task Complete_JudgesTheFloorOnJevConfidence_ForEveryProvider(string reply)
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, reply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered, minConfidence: 0.5);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Null(result.Error);
        Assert.Equal("work", result.Choose);
    }

    [Fact]
    public async Task Complete_FailsCleanly_WhenTheReplyHasNoChoice()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            _ = await ReadBody(ctx);
            await WriteJson(ctx, EmptyReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await serve.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Dispose();

        Assert.Equal("no choice in reply", result.Error);
        Assert.Null(result.Choose);
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
        Assert.Null(result.Choose);
    }

    [Fact]
    public async Task Complete_DoesNotRetryOnClientError()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var requests = 0;

        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            Interlocked.Increment(ref requests);
            _ = await ReadBody(ctx);
            ctx.Response.StatusCode = 401;
            ctx.Response.Close();
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var worker = CreateWorker(port, delivered);
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await serve.WaitAsync(TimeSpan.FromSeconds(15));
        worker.Dispose();

        Assert.Equal(1, requests);
        Assert.Equal("HTTP 401", result.Error);
    }

    [Fact]
    public async Task Complete_WaitsOutARateLimitThenMetersTheUsage()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var requests = 0;

        var serve = Task.Run(async () =>
        {
            var throttled = await listener.GetContextAsync();
            Interlocked.Increment(ref requests);
            _ = await ReadBody(throttled);
            throttled.Response.StatusCode = JevWorker.TooManyRequests;
            throttled.Response.Close();

            var answered = await listener.GetContextAsync();
            Interlocked.Increment(ref requests);
            _ = await ReadBody(answered);
            await WriteJson(answered, WorkReply);
        });

        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var usage = new JevUsage(1_000_000);

        using var worker = new JevWorker(
            $"http://127.0.0.1:{port}",
            TestToken,
            TestModel,
            TimeSpan.FromSeconds(5),
            result => delivered.TrySetResult(result),
            http: new HttpClient { Timeout = TimeSpan.FromSeconds(5) },
            limiter: new JevRateLimiter(),
            usage: usage
        );
        worker.Start();
        Assert.True(worker.TryEnqueue(SampleRequest()));

        var result = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await serve.WaitAsync(TimeSpan.FromSeconds(15));
        worker.Dispose();

        var hour = usage.Current(DateTime.UtcNow);

        Assert.Null(result.Error);
        Assert.Equal("work", result.Choose);
        Assert.Equal(2, requests);
        Assert.Equal(2, hour.Requests);
        Assert.Equal(300, hour.InputTokens);
        Assert.Equal(2, hour.KindRequests[(int)JevKind.Decision]);
        Assert.Equal(300, hour.KindTokens[(int)JevKind.Decision]);
    }

    [Fact]
    public async Task Complete_FailsWhenOverloadedTwice()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        var requests = 0;

        var serve = Task.Run(async () =>
        {
            while (requests < JevWorker.MaxAttempts)
            {
                var ctx = await listener.GetContextAsync();
                Interlocked.Increment(ref requests);
                _ = await ReadBody(ctx);
                ctx.Response.StatusCode = JevWorker.Overloaded;
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

        Assert.Equal(JevWorker.MaxAttempts, requests);
        Assert.Equal("HTTP 529", result.Error);
    }

    [Fact]
    public void TryEnqueue_DropsARequestWithoutAJevPayload()
    {
        var delivered = new TaskCompletionSource<BrainResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = CreateWorker(GetFreePort(), delivered);

        Assert.False(worker.TryEnqueue(SampleRequest() with { Jev = null }));
    }

    private static JevWorker CreateWorker(
        int port,
        TaskCompletionSource<BrainResult> delivered,
        double minConfidence = 0
    ) =>
        new(
            $"http://127.0.0.1:{port}",
            TestToken,
            TestModel,
            TimeSpan.FromSeconds(5),
            result => delivered.TrySetResult(result),
            minConfidence: minConfidence,
            http: new HttpClient { Timeout = TimeSpan.FromSeconds(5) }
        );

    private static BrainRequest SampleRequest() =>
        new(
            1,
            (Serial)1u,
            (Serial)2u,
            "Connor",
            BrainEventKind.Decide,
            null,
            null,
            Jev: new JevDecision(
                new Dictionary<string, object>
                {
                    ["who_you_are"] = "Connor.",
                    ["situation"] = new Dictionary<string, object> { ["place"] = "Britain" }
                },
                new Dictionary<string, JevQuestion>
                {
                    [JevPrompt.NextQuestion] = new(
                        "choice",
                        "pick",
                        new Dictionary<string, string> { ["work"] = "cut wood", ["loiter"] = "rest" }
                    )
                }
            )
        );

    private static BrainRequest SampleIntentRequest() =>
        SampleRequest() with
        {
            Kind = BrainEventKind.Spoken,
            Ask = JevAsk.Intent,
            Jev = JevPrompt.BuildIntent(
                SosariaAI.Configuration.Persona.CreateNeutral(),
                new BrainEvent(
                    BrainEventKind.Spoken,
                    (Serial)0x55,
                    "Connor",
                    "Hope",
                    (Serial)0x56,
                    true,
                    "anyone up for orcs",
                    "idle",
                    "Britain",
                    DateTime.UtcNow
                )
            )
        };

    private static BrainRequest SampleGateRequest() =>
        SampleRequest() with
        {
            Kind = BrainEventKind.Spoken,
            Ask = JevAsk.Gate,
            Jev = new JevDecision(
                new Dictionary<string, object> { ["who_you_are"] = "Connor." },
                new Dictionary<string, JevQuestion>
                {
                    [JevPrompt.ReplyQuestion] = new("noul", "would they answer?", null)
                }
            )
        };
}
