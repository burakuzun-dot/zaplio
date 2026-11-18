using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Zaplio.Views;

/// <summary>
/// "Bu kanal şu an uykuda" ekranı.
/// Akış: görünür olur → geri sayım başlar → süre dolunca ya da "Uyandır"a basılınca
/// <see cref="RetryRequested"/> tetiklenir ve ekran "uyandırılıyor" durumuna geçer.
/// Deneme başarısız olursa <see cref="RetryFailed"/> çağır; geri sayım baştan başlar.
/// Yayın açılırsa ekranı gizle (Visibility = Collapsed).
/// </summary>
public partial class ChannelSleepingView : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ChannelNumberProperty = DependencyProperty.Register(
        nameof(ChannelNumber), typeof(string), typeof(ChannelSleepingView),
        new PropertyMetadata(string.Empty, (d, e) => ((ChannelSleepingView)d).ChannelNumberText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ChannelNameProperty = DependencyProperty.Register(
        nameof(ChannelName), typeof(string), typeof(ChannelSleepingView),
        new PropertyMetadata(string.Empty, (d, e) => ((ChannelSleepingView)d).ChannelNameText.Text = (string)e.NewValue));

    public static readonly DependencyProperty AutoRetrySecondsProperty = DependencyProperty.Register(
        nameof(AutoRetrySeconds), typeof(int), typeof(ChannelSleepingView), new PropertyMetadata(10));

    /// <summary>Yeniden deneme isteği (geri sayım bitti ya da kullanıcı "Uyandır"a bastı).</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Kullanıcı "Sonraki kanal"a bastı.</summary>
    public event EventHandler? NextChannelRequested;

    public string ChannelNumber { get => (string)GetValue(ChannelNumberProperty); set => SetValue(ChannelNumberProperty, value); }
    public string ChannelName { get => (string)GetValue(ChannelNameProperty); set => SetValue(ChannelNameProperty, value); }
    public int AutoRetrySeconds { get => (int)GetValue(AutoRetrySecondsProperty); set => SetValue(AutoRetrySecondsProperty, value); }

    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _connectTimeout = new() { Interval = TimeSpan.FromSeconds(20) };
    private int _remaining;
    private bool _connecting;

    public ChannelSleepingView()
    {
        InitializeComponent();
        _countdown.Tick += (_, _) =>
        {
            if (_connecting || AutoRetrySeconds <= 0) return;
            _remaining--;
            if (_remaining <= 0) StartRetry();
            else UpdateStatus();
        };
        // Uygulama sonucu hiç bildirmezse ekran sonsuza dek "uyandırılıyor"da kalmasın.
        _connectTimeout.Tick += (_, _) => RetryFailed();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) OnShown(); else OnHidden(); };
    }

    /// <summary>Yeniden deneme başarısız olduğunda çağır: geri sayım baştan başlar.</summary>
    public void RetryFailed()
    {
        _connectTimeout.Stop();
        SetConnecting(false);
        _remaining = AutoRetrySeconds;
        UpdateStatus();
    }

    private void OnShown()
    {
        if (SystemParameters.ClientAreaAnimation)
            ((Storyboard)Resources["IdleStoryboard"]).Begin(this, true);
        _remaining = AutoRetrySeconds;
        SetConnecting(false);
        UpdateStatus();
        _countdown.Start();
        WakeButton.Focus();
    }

    private void OnHidden()
    {
        _countdown.Stop();
        _connectTimeout.Stop();
        ((Storyboard)Resources["IdleStoryboard"]).Stop(this);
        ((Storyboard)Resources["SpinnerStoryboard"]).Stop(this);
    }

    private void OnWakeClick(object sender, RoutedEventArgs e) => StartRetry();

    private void OnNextClick(object sender, RoutedEventArgs e) => NextChannelRequested?.Invoke(this, EventArgs.Empty);

    private void StartRetry()
    {
        if (_connecting) return;
        SetConnecting(true);
        _connectTimeout.Start();
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetConnecting(bool connecting)
    {
        _connecting = connecting;
        WakeButton.IsEnabled = !connecting;
        Spinner.Visibility = connecting ? Visibility.Visible : Visibility.Collapsed;
        var spinner = (Storyboard)Resources["SpinnerStoryboard"];
        if (connecting && SystemParameters.ClientAreaAnimation) spinner.Begin(this, true);
        else spinner.Stop(this);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = _connecting
            ? Lang.T("Kanal uyandırılıyor…")
            : AutoRetrySeconds <= 0 ? Lang.T("Hazır olduğunda kanalı uyandırabilirsin")
            : Lang.F($"{_remaining} saniye sonra kendiliğinden tekrar denenecek");

        // Ekran okuyucuya yalnızca durum değişimlerini ve son 3 saniyeyi bildir (her saniye konuşmasın).
        if (_connecting || AutoRetrySeconds <= 0 || _remaining == AutoRetrySeconds || _remaining <= 3)
            UIElementAutomationPeer.FromElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
