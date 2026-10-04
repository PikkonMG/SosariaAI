using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SosariaAI.Deliberation;
using Xunit;
using static SosariaAI.Tests.LocalHttp;

namespace SosariaAI.Tests;

/// <summary>
/// A provider's pooled connection that idled a minute was closed by the handler under its
/// waiting read-ahead, and each close threw inside the handler: 458 IOExceptions in one night.
/// The provider client keeps no connection idle, so no close ever cuts a waiting read.
/// </summary>
public class ProviderHttpTests
{
    private const int TimeoutSeconds = 10;
    private const string EmptyJson = "{}";

    [Fact]
    public async Task Create_OpensAFreshConnectionForEachRequest()
    {
        var port = GetFreePort();
        using var listener = StartListener(port);
        using var http = ProviderHttp.Create(TimeSpan.FromSeconds(TimeoutSeconds));
        var url = $"http://127.0.0.1:{port}/";

        var first = await ClientPortOf(listener, http, url);
        var second = await ClientPortOf(listener, http, url);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Reason_NamesTheWireError()
    {
        Assert.Equal(
            nameof(HttpRequestError.ConnectionError),
            ProviderHttp.Reason(new HttpRequestException(HttpRequestError.ConnectionError, "refused"))
        );
    }

    [Fact]
    public void Reason_FallsBackToTheInnerMessage_WhenTheErrorIsUnknown()
    {
        const string reset = "Connection reset by peer";

        Assert.Equal(reset, ProviderHttp.Reason(new HttpRequestException("send failed", new IOException(reset))));
        Assert.Equal(reset, ProviderHttp.Reason(new HttpRequestException(reset)));
    }

    private static async Task<int> ClientPortOf(HttpListener listener, HttpClient http, string url)
    {
        var send = http.GetStringAsync(url);
        var ctx = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
        var port = ctx.Request.RemoteEndPoint.Port;
        await WriteJson(ctx, EmptyJson);
        await send.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
        return port;
    }
}
