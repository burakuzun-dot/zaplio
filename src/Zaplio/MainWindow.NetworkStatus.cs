using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

public partial class MainWindow
{
    private readonly DispatcherTimer _networkClock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly StreamHealth _streamHealth = new();
    private NetworkInterface[] _interfaces = [];
    private long _interfacesAt;
    private long _netReceived = -1;
    private long _netSent = -1;
    private long _netSampledAt;

    private void MarkSeek()
    {
        _streamHealth.MarkSeek(Environment.TickCount64);
        _smoothClock.Reset();
    }

    private void InitializeNetworkStatus()
    {
        _networkClock.Tick += (_, _) => TickNetworkStatus();
        _networkClock.Start();
        TickNetworkStatus();
    }

    /// <summary>Called when a new stream starts so its counters are not compared with the previous one.</summary>
    private void ResetStreamHealth(bool keepHistory)
    {
        _playStartedAt = 0; // a new stream counts as connecting until VLC reports Playing
        _streamHealth.StartStream(keepHistory);
    }

    /// <summary>VLC reports buffering again after playback started only when the stream ran dry (a visible stall).</summary>
    private void OnPlayerBuffering(MediaPlayer player, int revision, float cache)
    {
        if (player != _player || revision != _playbackRevision || _playStartedAt == 0 || cache >= 100) return;
        _streamHealth.RecordStall(Environment.TickCount64);
    }

    private void TickNetworkStatus()
    {
        if (_closing) return;
        var now = Environment.TickCount64;
        UpdateNetworkSpeed(now);
        UpdateStability(now);
        UpdateLiveStatus();
        _ = UpdateAmbientLightAsync(now);
    }

    private void UpdateNetworkSpeed(long now)
    {
        if (!TryReadInterfaceTotals(now, out var received, out var sent))
        {
            DownloadSpeed.Text = UploadSpeed.Text = "—";
            return;
        }
        var seconds = (now - _netSampledAt) / 1000.0;
        if (_netReceived >= 0 && seconds > 0.2 && received >= _netReceived && sent >= _netSent)
        {
            var download = (received - _netReceived) * 8 / seconds;
            DownloadSpeed.Text = FormatBits(download);
            NoteDownloadSpeed(download);
            UploadSpeed.Text = FormatBits((sent - _netSent) * 8 / seconds);
        }
        _netReceived = received;
        _netSent = sent;
        _netSampledAt = now;
    }

    private bool TryReadInterfaceTotals(long now, out long received, out long sent)
    {
        received = sent = 0;
        try
        {
            if (_interfaces.Length == 0 || now - _interfacesAt > 15_000)
            {
                // Virtual switches (Hyper-V, WSL, VPN taps…) repeat the physical adapter's traffic; count real adapters only.
                _interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                        adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) &&
                        !adapter.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                        !adapter.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                        !adapter.Name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                _interfacesAt = now;
                _netReceived = _netSent = -1; // the adapter set may have changed
            }
            foreach (var adapter in _interfaces)
            {
                var statistics = adapter.GetIPStatistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
            return _interfaces.Length > 0;
        }
        catch (NetworkInformationException)
        {
            _interfaces = [];
            return false;
        }
    }

    private void UpdateStability(long now)
    {
        if (_player is not { } player)
        {
            StabilityPanel.Visibility = Visibility.Collapsed;
            return;
        }
        StabilityPanel.Visibility = Visibility.Visible;

        if (_reconnecting || (_playStartedAt == 0 && _reconnectAttempts > 0))
        {
            SetStability("Error", "Bağlantı kesildi", $"Yayın yeniden bağlanıyor ({_reconnectAttempts}/{MaxReconnects}).");
            return;
        }
        if (_playStartedAt == 0)
        {
            SetStability("Muted", "Bağlanıyor…", "Yayın sunucusuna bağlanılıyor.");
            return;
        }
        if (player.State == VLCState.Paused) return;

        using (var media = player.Media)
        {
            if (media is null) return;
            var statistics = media.Statistics;
            _streamHealth.AddSample(now, (uint)statistics.ReadBytes, statistics.LostPictures,
                statistics.DemuxCorrupted + statistics.DemuxDiscontinuity, _playingChannel?.IsOnDemand == true);
        }

        var report = _streamHealth.Evaluate(now);
        if (report.Level == HealthLevel.Measuring)
        {
            SetStability("Muted", "Ölçülüyor…", "Bağlantı birkaç saniye izlendikten sonra değerlendirilir.");
            return;
        }
        var detail = $"Yayın hızı: {FormatBits(report.BitsPerSecond)}\nSon {report.Samples} sn: {report.StallSeconds} sn takılma, " +
                     $"{report.DamagedSeconds} sn bozuk görüntü" + (report.RecentReconnect ? "\nSon 1 dakikada yeniden bağlanıldı." : "");
        switch (report.Level)
        {
            case HealthLevel.Weak: SetStability("Error", "Zayıf bağlantı", detail); break;
            case HealthLevel.Unsteady: SetStability("Warning", "Dalgalı", detail); break;
            default: SetStability("LiveColor", "Stabil", detail); break;
        }
    }

    private void SetStability(string brush, string label, string detail)
    {
        var color = (Brush)FindResource(brush);
        StabilityLight.Fill = color;
        StabilityText.Text = label;
        StabilityText.Foreground = brush == "Muted" ? color : (Brush)FindResource("Text");
        StabilityPanel.ToolTip = detail;
    }

    private static string FormatBits(double bitsPerSecond) => StreamHealth.FormatBits(bitsPerSecond);
}
