using System;
using System.Net.Http;

namespace SosariaAI.Deliberation;

/// <summary>
/// The HTTP client a brain provider worker owns. The handler keeps no idle connection: a
/// pooled connection that sat idle for a minute is closed by the handler while its read-ahead
/// still waits, and that close throws inside the handler. The frontier provider, quiet a
/// minute or more between big moments, threw four IOExceptions each time, 458 in one night.
/// A request here is seconds of model time; a fresh connection costs tens of milliseconds.
/// </summary>
public static class ProviderHttp
{
    /// <summary>Idle time a connection may wait for reuse: none, so the handler never cuts a waiting read.</summary>
    public static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.Zero;

    public static HttpClient Create(TimeSpan timeout) =>
        new(new SocketsHttpHandler { PooledConnectionIdleTimeout = PooledConnectionIdleTimeout }) { Timeout = timeout };

    /// <summary>Why a request failed on the wire, for the one line that reports it.</summary>
    public static string Reason(HttpRequestException error) =>
        error.HttpRequestError != HttpRequestError.Unknown
            ? error.HttpRequestError.ToString()
            : (error.InnerException ?? error).Message;
}
