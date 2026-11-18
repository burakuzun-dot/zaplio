using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace Zaplio;

/// <summary>A TV or other DLNA media renderer on the home network that Zaplio can send a stream to.</summary>
/// <param name="AvTransport">Control address of the AVTransport service (play, pause, seek…).</param>
/// <param name="RenderingControl">Control address of the RenderingControl service (volume); may be missing.</param>
internal sealed record DlnaRenderer(string Name, IPAddress Address, Uri AvTransport, Uri? RenderingControl, string Udn)
{
    public override string ToString() => Name;
}

internal enum DlnaState { Unknown, Stopped, Playing, Paused, Transitioning, NoMedia }

/// <summary>Pure DLNA/UPnP text handling: discovery replies, device descriptions, SOAP envelopes and DIDL metadata.</summary>
internal static class DlnaProtocol
{
    public const string AvTransportType = "urn:schemas-upnp-org:service:AVTransport:1";
    public const string RenderingControlType = "urn:schemas-upnp-org:service:RenderingControl:1";
    public const string RendererType = "urn:schemas-upnp-org:device:MediaRenderer:1";
    private static readonly XNamespace Device = "urn:schemas-upnp-org:device-1-0";

    public static string SearchRequest(int waitSeconds = 2) =>
        "M-SEARCH * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "MAN: \"ssdp:discover\"\r\n" +
        $"MX: {waitSeconds}\r\n" +
        $"ST: {RendererType}\r\n\r\n";

    /// <summary>The LOCATION header of an SSDP reply (where the device describes itself); null when there is none.</summary>
    public static Uri? Location(string reply)
    {
        foreach (var line in reply.Split("\r\n"))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(line[(colon + 1)..].Trim(), UriKind.Absolute, out var location) && location.Scheme == "http")
                return location;
        }
        return null;
    }

    /// <summary>Reads a device description; null when the device cannot be told what to play (no AVTransport).</summary>
    public static DlnaRenderer? ParseDescription(string xml, Uri location)
    {
        var root = XDocument.Parse(xml).Root;
        var device = root?.Descendants(Device + "device").FirstOrDefault();
        if (root is null || device is null) return null;
        var baseUrl = Uri.TryCreate(root.Element(Device + "URLBase")?.Value?.Trim(), UriKind.Absolute, out var declared) ? declared : location;

        Uri? Control(string type) => root.Descendants(Device + "service")
            .Where(service => service.Element(Device + "serviceType")?.Value.Trim() == type)
            .Select(service => service.Element(Device + "controlURL")?.Value.Trim())
            .Where(url => !string.IsNullOrEmpty(url))
            .Select(url => new Uri(baseUrl, url))
            .FirstOrDefault();

        if (Control(AvTransportType) is not { } transport) return null;
        var name = device.Element(Device + "friendlyName")?.Value.Trim();
        if (string.IsNullOrEmpty(name)) name = device.Element(Device + "modelName")?.Value.Trim() ?? Lang.T("Televizyon");
        return new DlnaRenderer(name, IPAddress.Parse(location.Host), transport, Control(RenderingControlType),
            device.Element(Device + "UDN")?.Value.Trim() ?? location.ToString());
    }

    public static string Envelope(string serviceType, string action, IEnumerable<(string Name, string Value)> arguments)
    {
        var body = new StringBuilder();
        foreach (var (name, value) in arguments) body.Append($"<{name}>{SecurityElement.Escape(value)}</{name}>");
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
               "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
               $"<s:Body><u:{action} xmlns:u=\"{serviceType}\">{body}</u:{action}></s:Body></s:Envelope>";
    }

    /// <summary>The value of an output argument in a SOAP reply (namespaces ignored).</summary>
    public static string? Output(string reply, string name) =>
        XDocument.Parse(reply).Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    /// <summary>
    /// DIDL-Lite metadata for SetAVTransportURI: many TVs (Samsung among them) refuse a URI without it or show no title.
    /// </summary>
    public static string Metadata(string title, Uri media, string mime, bool live)
    {
        var protocol = ProtocolInfo(mime, live);
        return "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
               "xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\">" +
               $"<item id=\"zaplio\" parentID=\"0\" restricted=\"1\"><dc:title>{SecurityElement.Escape(title)}</dc:title>" +
               "<upnp:class>object.item.videoItem</upnp:class>" +
               $"<res protocolInfo=\"{SecurityElement.Escape(protocol)}\">{SecurityElement.Escape(media.ToString())}</res></item></DIDL-Lite>";
    }

    /// <summary>
    /// The DLNA content features: a movie can be sought by byte range (OP=01), a live stream cannot (OP=00).
    /// FLAGS mark it as streamed, not downloaded first.
    /// </summary>
    public static string ContentFeatures(bool live) =>
        $"DLNA.ORG_OP={(live ? "00" : "01")};DLNA.ORG_CI=0;DLNA.ORG_FLAGS=01700000000000000000000000000000";

    public static string ProtocolInfo(string mime, bool live) => $"http-get:*:{mime}:{ContentFeatures(live)}";

    /// <summary>MIME type the TV is told for a stream, from its address; null when it is not a format TVs play (HLS).</summary>
    public static string? MimeFor(Uri stream)
    {
        var extension = Path.GetExtension(stream.AbsolutePath).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "ts" or "m2ts" or "mts" => "video/mpeg",
            "mp4" or "m4v" => "video/mp4",
            "mkv" => "video/x-mkv",
            "avi" => "video/x-msvideo",
            "webm" => "video/webm",
            "mov" => "video/quicktime",
            "mpg" or "mpeg" => "video/mpeg",
            "flv" => "video/x-flv",
            "m3u8" or "m3u" => null,
            "" => "video/mpeg", // extensionless live addresses are almost always MPEG-TS
            _ => null
        };
    }

    /// <summary>"01:02:03" for Seek; "H+:MM:SS" as UPnP wants it.</summary>
    public static string Clock(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

    /// <summary>Reads "01:02:03" or "1:02:03.000"; null for "NOT_IMPLEMENTED" or nonsense.</summary>
    public static TimeSpan? ParseClock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Trim().Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !double.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
            return null;
        return new TimeSpan(hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
    }

    public static DlnaState State(string? value) => value switch
    {
        "PLAYING" => DlnaState.Playing,
        "PAUSED_PLAYBACK" or "PAUSED_RECORDING" => DlnaState.Paused,
        "STOPPED" => DlnaState.Stopped,
        "TRANSITIONING" => DlnaState.Transitioning,
        "NO_MEDIA_PRESENT" => DlnaState.NoMedia,
        _ => DlnaState.Unknown
    };
}

/// <summary>Finds renderers with an SSDP search and talks to one over SOAP. Only on the local network.</summary>
internal sealed class DlnaClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    /// <summary>Asks the local network for media renderers for <paramref name="wait"/>; each device is listed once.</summary>
    public async Task<IReadOnlyList<DlnaRenderer>> SearchAsync(TimeSpan wait, CancellationToken token)
    {
        var locations = new HashSet<Uri>();
        foreach (var local in LocalAddresses())
        {
            try
            {
                using var udp = new UdpClient(new IPEndPoint(local, 0));
                var request = Encoding.ASCII.GetBytes(DlnaProtocol.SearchRequest());
                var group = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
                await udp.SendAsync(request, group, token).ConfigureAwait(false);
                await Task.Delay(150, token).ConfigureAwait(false);
                await udp.SendAsync(request, group, token).ConfigureAwait(false);
                using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
                window.CancelAfter(wait);
                while (!window.IsCancellationRequested)
                {
                    try
                    {
                        var reply = await udp.ReceiveAsync(window.Token).ConfigureAwait(false);
                        if (DlnaProtocol.Location(Encoding.ASCII.GetString(reply.Buffer)) is { } location) locations.Add(location);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { break; }
                }
            }
            catch (SocketException exception)
            {
                AppLog.Warn($"Ağda televizyon aranamadı ({local})", exception);
            }
        }

        var renderers = new List<DlnaRenderer>();
        foreach (var location in locations)
        {
            try
            {
                var xml = await _http.GetStringAsync(location, token).ConfigureAwait(false);
                if (DlnaProtocol.ParseDescription(xml, location) is { } renderer && renderers.All(known => known.Udn != renderer.Udn))
                    renderers.Add(renderer);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or FormatException)
            {
                AppLog.Warn("Ağdaki bir cihazın tanıtım bilgisi okunamadı.", exception);
            }
        }
        return renderers.OrderBy(renderer => renderer.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>This computer's IPv4 addresses on networks that are up (Wi-Fi, Ethernet), skipping loopback and tunnels.</summary>
    internal static IEnumerable<IPAddress> LocalAddresses() =>
        System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                          nic.NetworkInterfaceType is not (System.Net.NetworkInformation.NetworkInterfaceType.Loopback or
                              System.Net.NetworkInformation.NetworkInterfaceType.Tunnel))
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Distinct();

    /// <summary>The address of this computer that the TV can reach (the one the route to it goes out of).</summary>
    public static IPAddress AddressTowards(IPAddress tv)
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        probe.Connect(tv, 9); // no packet is sent; this only picks the outgoing interface
        return ((IPEndPoint)probe.LocalEndPoint!).Address;
    }

    public Task SetUriAsync(DlnaRenderer tv, Uri media, string title, string mime, bool live, CancellationToken token) =>
        CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "SetAVTransportURI",
            [("InstanceID", "0"), ("CurrentURI", media.ToString()), ("CurrentURIMetaData", DlnaProtocol.Metadata(title, media, mime, live))], token);

    public Task PlayAsync(DlnaRenderer tv, CancellationToken token) =>
        CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "Play", [("InstanceID", "0"), ("Speed", "1")], token);

    public Task PauseAsync(DlnaRenderer tv, CancellationToken token) =>
        CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "Pause", [("InstanceID", "0")], token);

    public Task StopAsync(DlnaRenderer tv, CancellationToken token) =>
        CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "Stop", [("InstanceID", "0")], token);

    public Task SeekAsync(DlnaRenderer tv, TimeSpan to, CancellationToken token) =>
        CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "Seek",
            [("InstanceID", "0"), ("Unit", "REL_TIME"), ("Target", DlnaProtocol.Clock(to))], token);

    public async Task<(TimeSpan? Position, TimeSpan? Duration)> PositionAsync(DlnaRenderer tv, CancellationToken token)
    {
        var reply = await CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "GetPositionInfo", [("InstanceID", "0")], token)
            .ConfigureAwait(false);
        return (DlnaProtocol.ParseClock(DlnaProtocol.Output(reply, "RelTime")), DlnaProtocol.ParseClock(DlnaProtocol.Output(reply, "TrackDuration")));
    }

    public async Task<DlnaState> StateAsync(DlnaRenderer tv, CancellationToken token)
    {
        var reply = await CallAsync(tv.AvTransport, DlnaProtocol.AvTransportType, "GetTransportInfo", [("InstanceID", "0")], token)
            .ConfigureAwait(false);
        return DlnaProtocol.State(DlnaProtocol.Output(reply, "CurrentTransportState"));
    }

    private async Task<string> CallAsync(Uri control, string serviceType, string action, IEnumerable<(string, string)> arguments,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, control)
        {
            Content = new StringContent(DlnaProtocol.Envelope(serviceType, action, arguments), Encoding.UTF8, "text/xml")
        };
        request.Content.Headers.ContentType!.CharSet = "\"utf-8\"";
        request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{serviceType}#{action}\"");
        using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
        var reply = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new SourceException(Lang.F($"Televizyon '{action}' komutunu kabul etmedi ({(int)response.StatusCode}{UpnpError(reply)})."));
        return reply;
    }

    private static string UpnpError(string reply)
    {
        try { return DlnaProtocol.Output(reply, "errorDescription") is { Length: > 0 } text ? $": {text}" : ""; }
        catch (System.Xml.XmlException) { return ""; }
    }

    public void Dispose() => _http.Dispose();
}
