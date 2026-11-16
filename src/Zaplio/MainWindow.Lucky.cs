using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Zaplio;

/// <summary>
/// "Şansıma": the bolt button beside the list headings. Cards of the list on screen spin past in a reel, slow down, stop on a
/// random one and it opens (a movie or series shows its preview, a channel starts playing). Click or Esc skips the spin.
/// The reel is a popup so it also shows over a playing video.
/// </summary>
public partial class MainWindow
{
    private sealed record LuckyEntry(string Key, string Title, string? Image, string Glyph, object Item);

    private const double LuckyGap = 14;
    private const int LuckySpinMilliseconds = 2800;

    private readonly Random _luckyRandom = new();
    private readonly List<string> _luckyRecent = [];
    private Popup? _luckyPopup;
    private Action? _luckySkip;

    internal bool IsLuckySpinning => _luckyPopup is not null;
    internal FrameworkElement? LuckyReelView => _luckyPopup?.Child as FrameworkElement;

    private async void OnLucky(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: Path bolt }) Zap(bolt);
        await RunLuckyAsync(Motion.Enabled ? LuckySpinMilliseconds : 0);
    }

    /// <summary>Spins (unless <paramref name="milliseconds"/> is 0), then opens the pick. Returns the picked item.</summary>
    internal async Task<object?> RunLuckyAsync(int milliseconds, bool open = true)
    {
        if (IsLuckySpinning) return null;
        var pool = LuckyPool();
        if (pool.Count == 0)
        {
            SetFooter(_librarySection == LibrarySection.Live ? "Listede şansını deneyecek kanal yok." : "Listede şansını deneyecek içerik yok.");
            return null;
        }

        var winner = LuckyReel.Pick(pool.Select(entry => entry.Key).ToArray(), _luckyRecent, _luckyRandom);
        _luckyRecent.Add(pool[winner].Key);
        while (_luckyRecent.Count > Math.Min(8, pool.Count - 1)) _luckyRecent.RemoveAt(0);

        if (milliseconds > 0 && pool.Count > 1) await SpinAsync(pool, winner, milliseconds);
        if (_closing) return null;
        if (open) await OpenLuckyAsync(pool[winner].Item);
        return pool[winner].Item;
    }

    private List<LuckyEntry> LuckyPool() => _librarySection == LibrarySection.Live
        ? VisibleChannels.Select(channel => new LuckyEntry(channel.Id, channel.Name, channel.Logo,
            channel.Name.Trim() is { Length: > 0 } name ? name[..1].ToUpperInvariant() : "?", channel)).ToList()
        : VodList.Items.OfType<VodRow>().Select(row => new LuckyEntry(row.Item.Playable?.Id ?? $"{_librarySection}:{row.Item.ProviderId}",
            row.Title, row.Item.Poster, row.Tile, row)).ToList();

    private async Task OpenLuckyAsync(object item)
    {
        switch (item)
        {
            case VodRow row:
                VodList.SelectedItem = row; // selecting opens the preview, as a click would
                VodList.ScrollIntoView(row);
                break;
            case Channel channel:
                ChannelList.SelectedItem = channel;
                ChannelList.ScrollIntoView(channel);
                await PlayChannelAsync(channel);
                break;
        }
    }

    // ── the reel ───────────────────────────────────────────────────────────────────────────────────────

    private async Task SpinAsync(IReadOnlyList<LuckyEntry> pool, int winner, int milliseconds)
    {
        if (Content is not FrameworkElement root || root.ActualWidth < 200) return;
        var live = _librarySection == LibrarySection.Live;
        var cardWidth = live ? 150.0 : 156.0;
        var cardHeight = live ? 186.0 : 270.0;
        var viewport = Math.Min(root.ActualWidth - 80, 1120);

        var strip = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = new TranslateTransform() };
        var reel = LuckyReel.Build(pool.Count, winner, _luckyRandom);
        Border? winnerCard = null;
        for (var slot = 0; slot < reel.Length; slot++)
        {
            var card = LuckyCard(pool[reel[slot]], cardWidth, cardHeight, live);
            card.Margin = new Thickness(0, 0, LuckyGap, 0);
            if (slot == LuckyReel.WinnerSlot) winnerCard = card;
            strip.Children.Add(card);
        }

        var canvas = new Canvas { Width = viewport, Height = cardHeight + 28, ClipToBounds = true };
        Canvas.SetTop(strip, 14);
        canvas.Children.Add(strip);
        canvas.OpacityMask = new LinearGradientBrush(new GradientStopCollection
        {
            new(Colors.Transparent, 0), new(Colors.Black, 0.14), new(Colors.Black, 0.86), new(Colors.Transparent, 1)
        }, new Point(0, 0.5), new Point(1, 0.5));

        var accent = (Brush)FindResource("Accent");
        var bolt = (Brush)FindResource("ZpBoltBrush");
        var marker = new Border
        {
            Width = cardWidth + 12, Height = cardHeight + 12, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2),
            BorderBrush = bolt, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false, Effect = new DropShadowEffect { Color = ((SolidColorBrush)bolt).Color, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.75 }
        };
        var logo = new Path
        {
            Data = (Geometry)FindResource("ZpBoltIcon"), Fill = bolt, Stretch = Stretch.Uniform, Width = 34, Height = 34,
            HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(), Effect = new DropShadowEffect { Color = ((SolidColorBrush)bolt).Color, BlurRadius = 18, ShadowDepth = 0 }
        };
        var heading = new TextBlock
        {
            Text = live ? "Şansına hangi kanal çıkacak?" : "Şansına ne çıkacak?", FontSize = 20, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Text"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 16)
        };
        var result = new TextBlock
        {
            FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = viewport, Margin = new Thickness(0, 18, 0, 0), Opacity = 0
        };
        var hint = new TextBlock
        {
            Text = "Atlamak için tıkla ya da Esc'ye bas.", FontSize = 12, Foreground = (Brush)FindResource("Muted"),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0)
        };
        var reelFrame = new Grid { Width = viewport, Height = cardHeight + 28 };
        reelFrame.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(22), Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1), Margin = new Thickness(-14, -10, -14, -10)
        });
        reelFrame.Children.Add(canvas);
        reelFrame.Children.Add(marker);

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(logo);
        stack.Children.Add(heading);
        stack.Children.Add(reelFrame);
        stack.Children.Add(result);
        stack.Children.Add(hint);
        var overlay = new Grid
        {
            Width = root.ActualWidth, Height = root.ActualHeight, Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x07, 0x09, 0x10)),
            Cursor = Cursors.Hand
        };
        overlay.Children.Add(stack);
        AutomationProperties.SetName(overlay, "Şansıma");

        var popup = new Popup
        {
            Child = overlay, AllowsTransparency = true, PlacementTarget = root, Placement = PlacementMode.Relative,
            StaysOpen = true, PopupAnimation = PopupAnimation.None
        };
        _luckyPopup = popup;
        popup.IsOpen = true;
        Motion.FadeIn(overlay, 160);
        overlay.UpdateLayout();

        var shift = (TranslateTransform)strip.RenderTransform;
        var distance = LuckyReel.Offset(LuckyReel.WinnerSlot, cardWidth, LuckyGap, viewport);
        var stopped = new TaskCompletionSource();
        var spin = new DoubleAnimation(0, -distance, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new PowerEase { Power = 4, EasingMode = EasingMode.EaseOut }
        };
        spin.Completed += (_, _) => stopped.TrySetResult();
        var skipped = false;
        _luckySkip = () =>
        {
            skipped = true;
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.X = -distance;
            stopped.TrySetResult();
        };
        overlay.MouseLeftButtonDown += (_, _) => _luckySkip?.Invoke();
        shift.BeginAnimation(TranslateTransform.XProperty, spin);
        logo.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, Pulse());
        logo.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, Pulse());

        try
        {
            await stopped.Task;
            if (_closing) return;

            // the stop: the card pops, its name appears, then the reel fades away
            result.Text = pool[winner].Title;
            result.Opacity = 1;
            hint.Text = live ? "Kanal açılıyor…" : "Açılıyor…";
            if (winnerCard is not null && Motion.Enabled)
            {
                var grow = new DoubleAnimation(1, 1.08, TimeSpan.FromMilliseconds(320))
                    { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } };
                winnerCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                winnerCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                Motion.Reveal(result, 8, 260);
            }
            await Task.Delay(skipped ? 250 : 750);
            if (Motion.Enabled)
            {
                overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180)));
                await Task.Delay(190);
            }
        }
        finally
        {
            popup.IsOpen = false;
            popup.Child = null;
            _luckyPopup = null;
            _luckySkip = null;
        }
    }

    private static DoubleAnimation Pulse() => new(1, 1.18, TimeSpan.FromMilliseconds(260))
    {
        AutoReverse = true, RepeatBehavior = new RepeatBehavior(4), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
    };

    private Border LuckyCard(LuckyEntry entry, double width, double height, bool live)
    {
        var imageHeight = live ? width : height - 42;
        var art = new Grid { Height = imageHeight, Background = (Brush)FindResource("Active") };
        art.Children.Add(new TextBlock
        {
            Text = entry.Glyph, FontFamily = live ? (FontFamily)FindResource("UiFont") : (FontFamily)FindResource("GlyphFont"),
            FontSize = live ? 46 : 34, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Accent"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });
        var image = new Image { Stretch = live ? Stretch.Uniform : Stretch.UniformToFill, Margin = live ? new Thickness(18) : new Thickness(0) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        LogoLoader.SetDecodeWidth(image, 240);
        LogoLoader.SetUrl(image, entry.Image);
        art.Children.Add(image);

        var title = new TextBlock
        {
            Text = entry.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text"),
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.Children.Add(art);
        Grid.SetRow(title, 1);
        layout.Children.Add(title);

        var card = new Border
        {
            Width = width, Height = height, CornerRadius = new CornerRadius(12), Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1), Child = layout,
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform()
        };
        layout.Clip = new RectangleGeometry(new Rect(0, 0, width - 2, height - 2), 11, 11);
        return card;
    }

    /// <summary>The button's bolt gives a quick jolt when pressed.</summary>
    private static void Zap(Path bolt)
    {
        if (!Motion.Enabled) return;
        if (bolt.RenderTransform is not RotateTransform turn)
        {
            bolt.RenderTransformOrigin = new Point(0.5, 0.5);
            bolt.RenderTransform = turn = new RotateTransform();
        }
        turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames = { new LinearDoubleKeyFrame(-18, KeyTime.FromPercent(0.25)), new LinearDoubleKeyFrame(14, KeyTime.FromPercent(0.6)),
                new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)) },
            Duration = TimeSpan.FromMilliseconds(320)
        });
    }
}
