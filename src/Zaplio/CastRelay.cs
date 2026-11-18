using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Zaplio;

/// <summary>
/// Hands a stream to a TV on the home network. The TV is given an address on this computer with a random secret path,
/// never the provider address (which carries the account name and password); Zaplio fetches from the provider and passes
/// the bytes on. Only the chosen TV may connect, and only one provider connection is open at a time: a new request from
/// the TV (it reconnects when seeking) ends the previous one.
/// </summary>
internal sealed class CastRelay : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private readonly TcpListener _listener;
    private readonly IPAddress _allowed;
    private readonly Uri _upstream;
    private readonly string? _userAgent;
    private readonly string? _referrer;
    private readonly string _mime;
    private readonly bool _live;
    private readonly string _path;
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _active;
    private long? _length;

    /// <param name="listenOn">This computer's address on the TV's network.</param>
    /// <param name="allowed">The TV: requests from any other address are refused.</param>
    public CastRelay(IPAddress listenOn, IPAddress allowed, Uri upstream, string? userAgent, string? referrer, string mime, bool live,
        HttpMessageHandler? handler = null)
    {
        _allowed = allowed;
        _upstream = upstream;
        _userAgent = userAgent;
        _referrer = referrer;
        _mime = mime;
        _live = live;
        var extension = Path.GetExtension(upstream.AbsolutePath);
        _path = $"/{Guid.NewGuid():N}/yayin{(string.IsNullOrEmpty(extension) ? ".ts" : extension.ToLowerInvariant())}";
        _http = handler is null ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan } : new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _listener = new TcpListener(listenOn, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Address = new Uri($"http://{listenOn}:{port}{_path}");
        _ = AcceptLoopAsync();
    }

    /// <summary>What the TV is told to play.</summary>
    public Uri Address { get; }

    /// <summary>Set once the TV has asked for the stream (if it never does, a firewall is usually in the way).</summary>
    public bool TvConnected { get; private set; }

    public long BytesSent => Interlocked.Read(ref _bytesSent);
    private long _bytesSent;

    private async Task AcceptLoopAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false); }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException) { return; }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using var _ = client;
        try
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
            if (!remote.Equals(_allowed))
            {
                await WriteAsync(stream, "403 Forbidden", [], _lifetime.Token).ConfigureAwait(false);
                return;
            }
            var request = await ReadRequestAsync(stream, _lifetime.Token).ConfigureAwait(false);
            if (request is null) return;
            if (request.Value.Path != _path || request.Value.Method is not ("GET" or "HEAD"))
            {
                await WriteAsync(stream, "404 Not Found", [], _lifetime.Token).ConfigureAwait(false);
                return;
            }
            TvConnected = true;
            await RelayAsync(stream, request.Value.Method == "HEAD", request.Value.Range).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The TV closed the connection (it does when seeking or stopping) or the relay is shutting down.
        }
    }

    private async Task RelayAsync(NetworkStream client, bool headOnly, string? range)
    {
        // A live HEAD needs no provider request at all.
        if (headOnly && (_live || _length is not null))
        {
            await WriteAsync(client, "200 OK", Headers(_live ? null : _length, null), _lifetime.Token).ConfigureAwait(false);
            return;
        }

        var mine = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _active;
            _active = mine;
        }
        previous?.Cancel(); // one provider connection at a time

        try
        {
            using var response = await OpenUpstreamAsync(range, mine.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                AppLog.Warn($"Televizyona aktarım: sağlayıcı yanıtı {(int)response.StatusCode}.");
                await WriteAsync(client, "502 Bad Gateway", [], mine.Token).ConfigureAwait(false);
                return;
            }

            var partial = response.StatusCode == HttpStatusCode.PartialContent;
            var length = response.Content.Headers.ContentLength;
            var contentRange = response.Content.Headers.ContentRange;
            if (!_live && !partial && length is not null) _length = length;
            if (!_live && contentRange?.Length is { } total) _length = total;
            await WriteAsync(client, partial ? "206 Partial Content" : "200 OK",
                Headers(_live ? null : length, partial ? contentRange?.ToString() : null), mine.Token).ConfigureAwait(false);
            if (headOnly) return;

            await using var body = await response.Content.ReadAsStreamAsync(mine.Token).ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await body.ReadAsync(buffer, mine.Token).ConfigureAwait(false)) > 0)
            {
                await client.WriteAsync(buffer.AsMemory(0, read), mine.Token).ConfigureAwait(false);
                Interlocked.Add(ref _bytesSent, read);
            }
        }
        catch (HttpRequestException exception)
        {
            AppLog.Warn("Televizyona aktarım: sağlayıcıya bağlanılamadı.", exception);
        }
        finally
        {
            lock (_gate)
            {
                if (_active == mine) _active = null;
            }
            mine.Dispose();
        }
    }

    /// <summary>Waits before each new try when the provider says the account's connections are taken.</summary>
    internal static readonly TimeSpan[] BusyRetryDelays = [TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1600)];

    /// <summary>
    /// Statuses providers give when the account's only connection still counts as open. Playback here stops a moment before
    /// the TV asks, and providers release a connection a second or two late, so the TV's first request often gets one.
    /// </summary>
    internal static bool ConnectionBusy(HttpStatusCode status) => (int)status is 401 or 403 or 456 or 509;

    /// <summary>One provider request; on a "connection taken" answer it tries again twice, a little later (not a burst).</summary>
    private async Task<HttpResponseMessage> OpenUpstreamAsync(string? range, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var upstream = new HttpRequestMessage(HttpMethod.Get, _upstream);
            if (!_live && range is not null) upstream.Headers.TryAddWithoutValidation("Range", range);
            if (!string.IsNullOrEmpty(_userAgent)) upstream.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            if (!string.IsNullOrEmpty(_referrer)) upstream.Headers.TryAddWithoutValidation("Referer", _referrer);
            var response = await _http.SendAsync(upstream, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode || !ConnectionBusy(response.StatusCode) || attempt >= BusyRetryDelays.Length) return response;
            response.Dispose();
            await Task.Delay(BusyRetryDelays[attempt], token).ConfigureAwait(false);
        }
    }

    private List<(string, string)> Headers(long? length, string? contentRange)
    {
        var headers = new List<(string, string)>
        {
            ("Content-Type", _mime),
            ("transferMode.dlna.org", "Streaming"),
            ("contentFeatures.dlna.org", DlnaProtocol.ContentFeatures(_live)),
            ("Accept-Ranges", _live ? "none" : "bytes")
        };
        if (length is not null) headers.Add(("Content-Length", length.Value.ToString(CultureInfo.InvariantCulture)));
        if (contentRange is not null) headers.Add(("Content-Range", contentRange));
        return headers;
    }

    private static async Task WriteAsync(NetworkStream stream, string status, IEnumerable<(string Name, string Value)> headers, CancellationToken token)
    {
        var text = new StringBuilder($"HTTP/1.1 {status}\r\nServer: Zaplio\r\nConnection: close\r\n");
        foreach (var (name, value) in headers) text.Append($"{name}: {value}\r\n");
        text.Append("\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(text.ToString()), token).ConfigureAwait(false);
    }

    internal readonly record struct Request(string Method, string Path, string? Range);

    /// <summary>Reads the request line and headers (nothing else is needed); null when the client sent nothing usable.</summary>
    internal static async Task<Request?> ReadRequestAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[MaxHeaderBytes];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), token).ConfigureAwait(false);
            if (read == 0) return null;
            filled += read;
            var text = Encoding.ASCII.GetString(buffer, 0, filled);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end < 0) continue;
            return ParseRequest(text[..end]);
        }
        return null;
    }

    internal static Request? ParseRequest(string head)
    {
        var lines = head.Split("\r\n");
        var first = lines[0].Split(' ');
        if (first.Length < 2) return null;
        string? range = null;
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Range", StringComparison.OrdinalIgnoreCase)) range = line[(colon + 1)..].Trim();
        }
        return new Request(first[0].ToUpperInvariant(), first[1], range);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        lock (_gate) _active?.Cancel();
        try { _listener.Stop(); }
        catch (SocketException) { }
        _http.Dispose();
    }
}
