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

        string name = string.IsNullOrWhiteSpace(ChannelName) ? "Bu kanalı" : $"{ChannelName} kanalını";
        string label = IsFavorite ? $"{name} favorilerden çıkar" : $"{name} favorilere ekle";
        AutomationProperties.SetName(Toggle, label);
        Toggle.ToolTip = IsFavorite ? "Favorilerden çıkar" : "Favorilere ekle";
    }
}
