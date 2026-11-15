using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Zaplio.Views;

/// <summary>
/// "İnternet bağlantın koptu" ekranı.
/// - Kullanıcı "Tekrar bağlan"a basınca <see cref="RetryRequested"/> tetiklenir. Uygulama bağlantıyı gerçekten
///   dener (ör. listeyi/yayını yeniden açar); başarısızsa <see cref="RetryFailed"/>, başarılıysa ekranı gizler.
/// - Windows ağın geri geldiğini bildirince <see cref="ConnectionRestored"/> tetiklenir.
///   Not: "ağ var" her zaman "internet var" demek değildir; asıl kontrolü uygulama yapmalı.
/// </summary>
public partial class OfflineView : System.Windows.Controls.UserControl
{
    /// <summary>Kullanıcı "Tekrar bağlan"a bastı.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Windows ağ bağlantısının geri geldiğini bildirdi (UI iş parçacığında tetiklenir).</summary>
    public event EventHandler? ConnectionRestored;

    private readonly DispatcherTimer _connectTimeout = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _connecting;

    public OfflineView()
    {
        InitializeComponent();
        _connectTimeout.Tick += (_, _) => RetryFailed();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) OnShown(); else OnHidden(); };
        Unloaded += (_, _) => NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    /// <summary>Bağlantı denemesi başarısız olduğunda çağır.</summary>
    public void RetryFailed()
    {
        _connectTimeout.Stop();
        SetConnecting(false);
        SetStatus("Hâlâ bağlantı yok. Arka planda denemeye devam ediyoruz.");
    }

    private void OnShown()
    {
        if (SystemParameters.ClientAreaAnimation)
            ((Storyboard)Resources["IdleStoryboard"]).Begin(this, true);
        SetConnecting(false);
        SetStatus("Arka planda bağlantıyı aramaya devam ediyoruz.");
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        RetryButton.Focus();
    }

    private void OnHidden()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _connectTimeout.Stop();
        ((Storyboard)Resources["IdleStoryboard"]).Stop(this);
        ((Storyboard)Resources["SpinnerStoryboard"]).Stop(this);
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable) return;
        // Bu olay arka plan iş parçacığından gelir; arayüze dönmeden önce UI iş parçacığına geç.
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible) return;
            SetConnecting(true);
            _connectTimeout.Start();
            ConnectionRestored?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (_connecting) return;
        SetConnecting(true);

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            // Hiç ağ yoksa denemeye gerek yok; kısa bir "aranıyor" anından sonra sonucu göster.
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            t.Tick += (_, _) => { t.Stop(); RetryFailed(); };
            t.Start();
            return;
        }

        _connectTimeout.Start();
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetConnecting(bool connecting)
    {
        _connecting = connecting;
        RetryButton.IsEnabled = !connecting;
        RetryText.Text = connecting ? "Bağlanıyor…" : "Tekrar bağlan";
        RetryIcon.Visibility = connecting ? Visibility.Collapsed : Visibility.Visible;
        Spinner.Visibility = connecting ? Visibility.Visible : Visibility.Collapsed;

        var spinner = (Storyboard)Resources["SpinnerStoryboard"];
        if (connecting && SystemParameters.ClientAreaAnimation) spinner.Begin(this, true);
        else spinner.Stop(this);

        if (connecting) SetStatus("Bağlantı aranıyor…");
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        UIElementAutomationPeer.FromElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
