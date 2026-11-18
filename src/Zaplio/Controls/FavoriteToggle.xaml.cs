using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Animation;

namespace Zaplio.Controls;

/// <summary>
/// Kanal satırındaki favori şimşek düğmesi.
/// Animasyon yalnızca kullanıcı tıklayıp favoriye eklediğinde oynar;
/// liste yüklenirken gelen favoriler sessizce dolu görünür.
/// </summary>
public partial class FavoriteToggle : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
        nameof(IsFavorite), typeof(bool), typeof(FavoriteToggle),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FavoriteToggle)d).UpdateState()));

    /// <summary>False for movies and series: the screen reader label then names the title without the word "kanal".</summary>
    public static readonly DependencyProperty IsChannelProperty = DependencyProperty.Register(
        nameof(IsChannel), typeof(bool), typeof(FavoriteToggle),
        new PropertyMetadata(true, (d, _) => ((FavoriteToggle)d).UpdateState()));

    public bool IsChannel
    {
        get => (bool)GetValue(IsChannelProperty);
        set => SetValue(IsChannelProperty, value);
    }

    public static readonly DependencyProperty ChannelNameProperty = DependencyProperty.Register(
        nameof(ChannelName), typeof(string), typeof(FavoriteToggle),
        new PropertyMetadata(string.Empty, (d, _) => ((FavoriteToggle)d).UpdateState()));

    /// <summary>Kanal favorilere eklendiğinde (true) ya da çıkarıldığında (false), kullanıcı tıklamasıyla tetiklenir.
    /// Bildirim (toast) göstermek için kullanabilirsin.</summary>
    public event EventHandler<bool>? FavoriteToggled;

    public bool IsFavorite
    {
        get => (bool)GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    public string ChannelName
    {
        get => (string)GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }

    public FavoriteToggle()
    {
        InitializeComponent();
        Toggle.Click += OnClick;
        UpdateState();
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        bool added = Toggle.IsChecked == true;
        if (added && SystemParameters.ClientAreaAnimation)
            ((Storyboard)Resources["AddedStoryboard"]).Begin(this, true);
        FavoriteToggled?.Invoke(this, added);
    }

    private void UpdateState()
    {
        if (OutlineBolt is null) return; // InitializeComponent öncesi
        OutlineBolt.Visibility = IsFavorite ? Visibility.Collapsed : Visibility.Visible;
        FilledBolt.Visibility = IsFavorite ? Visibility.Visible : Visibility.Collapsed;

        string label;
        if (IsChannel)
        {
            string name = string.IsNullOrWhiteSpace(ChannelName) ? Lang.T("Bu kanalı") : Lang.F($"{ChannelName} kanalını");
            label = IsFavorite ? Lang.F($"{name} favorilerden çıkar") : $"{name} favorilere ekle";
        }
        else
        {
            string name = string.IsNullOrWhiteSpace(ChannelName) ? "" : $"{ChannelName} · ";
            label = IsFavorite ? Lang.F($"{name}Favorilerden çıkar") : $"{name}Favorilere ekle";
        }
        AutomationProperties.SetName(Toggle, label);
        Toggle.ToolTip = IsFavorite ? Lang.T("Favorilerden çıkar") : Lang.T("Favorilere ekle");
    }
}
