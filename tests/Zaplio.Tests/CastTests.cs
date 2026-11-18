using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Zaplio.Tests;

public sealed class DlnaProtocolTests
{
    private const string Description = """
        <?xml version="1.0"?>
        <root xmlns="urn:schemas-upnp-org:device-1-0">
          <device>
            <deviceType>urn:schemas-upnp-org:device:MediaRenderer:1</deviceType>
            <friendlyName>Salon Ekranı</friendlyName>
            <modelName>Ekran 1</modelName>
            <UDN>uuid:1234</UDN>
            <serviceList>
              <service><serviceType>urn:schemas-upnp-org:service:RenderingControl:1</serviceType><controlURL>/upnp/control/RenderingControl1</controlURL></service>
              <service><serviceType>urn:schemas-upnp-org:service:ConnectionManager:1</serviceType><controlURL>/upnp/control/ConnectionManager1</controlURL></service>
              <service><serviceType>urn:schemas-upnp-org:service:AVTransport:1</serviceType><controlURL>/upnp/control/AVTransport1</controlURL></service>
            </serviceList>
          </device>
        </root>
        """;

    [Fact]
    public void A_discovery_reply_gives_where_the_device_describes_itself()
    {
        const string reply = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nlocation: http://192.168.1.50:9197/dmr\r\nST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n\r\n";
        Assert.Equal(new Uri("http://192.168.1.50:9197/dmr"), DlnaProtocol.Location(reply));
        Assert.Null(DlnaProtocol.Location("HTTP/1.1 200 OK\r\nST: x\r\n\r\n"));
    }

    [Fact]
    public void A_renderer_description_gives_its_name_and_control_addresses()
    {
        var renderer = DlnaProtocol.ParseDescription(Description, new Uri("http://192.168.1.50:9197/dmr"));

        Assert.NotNull(renderer);
        Assert.Equal("Salon Ekranı", renderer!.Name);
        Assert.Equal(IPAddress.Parse("192.168.1.50"), renderer.Address);
        Assert.Equal(new Uri("http://192.168.1.50:9197/upnp/control/AVTransport1"), renderer.AvTransport);
        Assert.Equal(new Uri("http://192.168.1.50:9197/upnp/control/RenderingControl1"), renderer.RenderingControl);
        Assert.Equal("uuid:1234", renderer.Udn);
    }

    [Fact]
    public void A_device_that_cannot_be_told_what_to_play_is_skipped() =>
        Assert.Null(DlnaProtocol.ParseDescription(Description.Replace("AVTransport", "Something"), new Uri("http://192.168.1.50:9197/dmr")));

    [Fact]
    public void Soap_and_metadata_are_escaped()
    {
        var envelope = DlnaProtocol.Envelope(DlnaProtocol.AvTransportType, "SetAVTransportURI", [("CurrentURI", "http://a/b?x=1&y=2")]);
        Assert.Contains("<u:SetAVTransportURI xmlns:u=\"urn:schemas-upnp-org:service:AVTransport:1\">", envelope);
        Assert.Contains("http://a/b?x=1&amp;y=2", envelope);

        var metadata = DlnaProtocol.Metadata("Film & <Dizi>", new Uri("http://192.168.1.10:5000/abc/yayin.mp4"), "video/mp4", live: false);
        Assert.Contains("<dc:title>Film &amp; &lt;Dizi&gt;</dc:title>", metadata);
        Assert.Contains("http-get:*:video/mp4:DLNA.ORG_OP=01", metadata);
        // The metadata travels inside another XML document, escaped once more.
        Assert.Contains("&lt;DIDL-Lite", DlnaProtocol.Envelope(DlnaProtocol.AvTransportType, "SetAVTransportURI", [("CurrentURIMetaData", metadata)]));
    }

    [Fact]
    public void Live_streams_are_not_offered_for_seeking() =>
        Assert.StartsWith("DLNA.ORG_OP=00", DlnaProtocol.ContentFeatures(live: true));

    [Theory]
    [InlineData("http://s/live/u/p/1.ts", "video/mpeg")]
    [InlineData("http://s/movie/u/p/2.mp4", "video/mp4")]
    [InlineData("http://s/movie/u/p/3.MKV", "video/x-mkv")]
    [InlineData("http://s/live/u/p/1", "video/mpeg")]
    [InlineData("http://s/live/u/p/1.m3u8", null)]
    public void Formats_the_tv_plays(string address, string? mime) => Assert.Equal(mime, DlnaProtocol.MimeFor(new Uri(address)));

    [Fact]
    public void Times_and_states_are_read_and_written_the_upnp_way()
    {
        Assert.Equal("01:02:03", DlnaProtocol.Clock(new TimeSpan(1, 2, 3)));
        Assert.Equal("26:00:00", DlnaProtocol.Clock(TimeSpan.FromHours(26)));
        Assert.Equal(new TimeSpan(0, 1, 2, 3, 500), DlnaProtocol.ParseClock("1:02:03.500"));
        Assert.Null(DlnaProtocol.ParseClock("NOT_IMPLEMENTED"));
        Assert.Equal(DlnaState.Paused, DlnaProtocol.State("PAUSED_PLAYBACK"));
        Assert.Equal(DlnaState.Unknown, DlnaProtocol.State("?"));
        Assert.Equal("12", DlnaProtocol.Output("<s:Envelope xmlns:s=\"x\"><s:Body><u:R xmlns:u=\"y\"><RelTime>12</RelTime></u:R></s:Body></s:Envelope>", "RelTime"));
    }
}

public sealed class CastRelayTests
{
    /// <summary>Stands in for the provider: serves 1000 numbered bytes and honours byte ranges.</summary>
    private sealed class FakeProvider : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        private static readonly byte[] Body = Enumerable.Range(0, 1000).Select(i => (byte)(i % 251)).ToArray();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request);
            if (request.Headers.Range?.Ranges.FirstOrDefault() is { From: { } from } range)
            {
                var to = range.To ?? Body.Length - 1;
                var part = new ByteArrayContent(Body[(int)from..((int)to + 1)]);
                part.Headers.ContentRange = new ContentRangeHeaderValue(from, to, Body.Length);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = part });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Body) });
        }
    }

    private static async Task<(string Head, byte[] Body)> GetAsync(Uri address, string? range = null, string method = "GET")
    {
        using var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port);
        var stream = client.GetStream();
        var request = $"{method} {address.PathAndQuery} HTTP/1.1\r\nHost: {address.Authority}\r\n" + (range is null ? "" : $"Range: {range}\r\n") + "\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var all = new MemoryStream();
        await stream.CopyToAsync(all);
        var bytes = all.ToArray();
        var split = Encoding.ASCII.GetString(bytes).IndexOf("\r\n\r\n", StringComparison.Ordinal);
        return (Encoding.ASCII.GetString(bytes, 0, split), bytes[(split + 4)..]);
    }

    private static CastRelay Relay(FakeProvider provider, bool live, string upstream = "http://saglayici.test/movie/kullanici/sifre/9.mp4") =>
        new(IPAddress.Loopback, IPAddress.Loopback, new Uri(upstream), "Zaplio-Test", null, live ? "video/mpeg" : "video/mp4", live, provider);

    [Fact]
    public void The_tv_never_sees_the_provider_address()
    {
        using var relay = Relay(new FakeProvider(), live: false);
        var address = relay.Address.ToString();
        Assert.DoesNotContain("kullanici", address);
        Assert.DoesNotContain("sifre", address);
        Assert.DoesNotContain("saglayici", address);
        Assert.EndsWith("/yayin.mp4", address);
    }

    [Fact]
    public async Task A_movie_is_passed_on_whole_and_by_range()
    {
        var provider = new FakeProvider();
        using var relay = Relay(provider, live: false);

        var whole = await GetAsync(relay.Address);
        Assert.StartsWith("HTTP/1.1 200 OK", whole.Head);
        Assert.Contains("Content-Length: 1000", whole.Head);
        Assert.Contains("Accept-Ranges: bytes", whole.Head);
        Assert.Contains("contentFeatures.dlna.org: DLNA.ORG_OP=01", whole.Head);
        Assert.Equal(1000, whole.Body.Length);

        var part = await GetAsync(relay.Address, "bytes=100-199");
        Assert.StartsWith("HTTP/1.1 206 Partial Content", part.Head);
        Assert.Contains("Content-Range: bytes 100-199/1000", part.Head);
        Assert.Equal(Enumerable.Range(100, 100).Select(i => (byte)(i % 251)), part.Body);
        Assert.True(relay.TvConnected);
        Assert.Equal("Zaplio-Test", provider.Requests[0].Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task A_live_stream_takes_no_range_and_its_head_costs_no_provider_request()
    {
        var provider = new FakeProvider();
        using var relay = Relay(provider, live: true, upstream: "http://saglayici.test/live/kullanici/sifre/1.ts");

        var head = await GetAsync(relay.Address, method: "HEAD");
        Assert.StartsWith("HTTP/1.1 200 OK", head.Head);
        Assert.Contains("Accept-Ranges: none", head.Head);
        Assert.Empty(provider.Requests);

        var body = await GetAsync(relay.Address, "bytes=500-");
        Assert.Equal(1000, body.Body.Length); // the range is not passed on for live
        Assert.Null(provider.Requests.Single().Headers.Range);
    }

    [Fact]
    public async Task Other_paths_are_refused()
    {
        var provider = new FakeProvider();
        using var relay = Relay(provider, live: false);
        var wrong = new UriBuilder(relay.Address) { Path = "/baska/yayin.mp4" }.Uri;

        var answer = await GetAsync(wrong);
        Assert.StartsWith("HTTP/1.1 404", answer.Head);
        Assert.Empty(provider.Requests);
        Assert.False(relay.TvConnected);
    }

    /// <summary>Says "connection taken" a given number of times, then serves.</summary>
    private sealed class BusyProvider(int refusals) : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Interlocked.Increment(ref Requests) <= refusals
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[500]) });
    }

    [Fact]
    public async Task A_connection_the_provider_still_counts_is_waited_out()
    {
        var provider = new BusyProvider(refusals: 1);
        using var relay = new CastRelay(IPAddress.Loopback, IPAddress.Loopback, new Uri("http://saglayici.test/live/k/s/1.ts"),
            null, null, "video/mpeg", live: true, provider);

        var answer = await GetAsync(relay.Address);
        Assert.StartsWith("HTTP/1.1 200 OK", answer.Head);
        Assert.Equal(500, answer.Body.Length);
        Assert.Equal(2, provider.Requests);
    }

    [Fact]
    public async Task A_provider_that_keeps_refusing_gets_only_two_more_tries()
    {
        var provider = new BusyProvider(refusals: 100);
        using var relay = new CastRelay(IPAddress.Loopback, IPAddress.Loopback, new Uri("http://saglayici.test/live/k/s/1.ts"),
            null, null, "video/mpeg", live: true, provider);

        var answer = await GetAsync(relay.Address);
        Assert.StartsWith("HTTP/1.1 502", answer.Head);
        Assert.Equal(1 + CastRelay.BusyRetryDelays.Length, provider.Requests);
    }

    [Theory]
    [InlineData("GET /a/b.ts HTTP/1.1\r\nHost: x\r\nrange: bytes=0-", "GET", "/a/b.ts", "bytes=0-")]
    [InlineData("head /a HTTP/1.1", "HEAD", "/a", null)]
    public void Requests_are_read(string head, string method, string path, string? range) =>
        Assert.Equal(new CastRelay.Request(method, path, range), CastRelay.ParseRequest(head));
}
