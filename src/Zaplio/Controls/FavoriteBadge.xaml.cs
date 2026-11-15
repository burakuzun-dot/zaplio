using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Zaplio.Controls;

/// <summary>
/// Favori kanalın logosundaki şimşek rozeti. Kanal ekranda açıkken favoriye eklenirse
/// rozet zıplayarak belirir. Liste yüklenirken ya da sanallaştırılmış listede satır
/// yeniden kullanılırken (DataContext değişimi) animasyon oynamaz.
/// </summary>
public partial class FavoriteBadge : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
        nameof(IsFavorite), typeof(bool), typeof(FavoriteBadge),
        new PropertyMetadata(false, (d, e) => ((FavoriteBadge)d).OnIsFavoriteChanged((bool)e.NewValue)));

    public static readonly DependencyProperty CutoutBrushProperty = DependencyProperty.Register(
        nameof(CutoutBrush), typeof(Brush), typeof(FavoriteBadge),
        new PropertyMetadata(CreateDefaultCutout()));

    // Bağımlılık özelliğinin varsayılan fırçası dondurulmuş olmalı, yoksa WPF açılışta hata verir.
    private static Brush CreateDefaultCutout()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x15, 0x18, 0x29));
        brush.Freeze();
        return brush;
    }

    private bool _dataContextChanging;

    public bool IsFavorite
    {
        get => (bool)GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    public Brush CutoutBrush
    {
        get => (Brush)GetValue(CutoutBrushProperty);
        set => SetValue(CutoutBrushProperty, value);
    }

    public FavoriteBadge()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            _dataContextChanging = true;
            Dispatcher.InvokeAsync(() => _dataContextChanging = false, DispatcherPriority.Loaded);
        };
    }

    private void OnIsFavoriteChanged(bool isFavorite)
    {
        Root.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;

        bool animate = isFavorite && IsLoaded && !_dataContextChanging && SystemParameters.ClientAreaAnimation;
        if (animate)
            ((Storyboard)Resources["PopStoryboard"]).Begin(this, true);
    }
}
