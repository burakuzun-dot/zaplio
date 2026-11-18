using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Zaplio;

/// <summary>
/// Grid programme guide: one row per channel, time from left to right. Rows are virtualized and compute their cells only
/// when they come on screen; all rows and the ruler share one horizontal shift, driven by the scroll bar at the bottom.
/// </summary>
public partial class GuideGridWindow : Window
{
    internal sealed class Row(Channel channel, Func<Channel, IReadOnlyList<GuideProgramme>> programmes,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        private IReadOnlyList<GuideGrid.Cell>? _cells;

        public Channel Channel { get; } = channel;
        public string Name => Channel.Name;
        public string? Logo => Channel.Logo;
        public string Initial => Channel.Initial;
        public IReadOnlyList<GuideGrid.Cell> Cells => _cells ??= GuideGrid.Cells(programmes(Channel), from, to, now);
    }

    private readonly TranslateTransform _shift = new();
    private readonly Func<Channel, bool> _hasArchive;
    private readonly Func<Channel, GuideGrid.Cell?, Task> _play;
    private readonly DateTimeOffset _from;
    private readonly DateTimeOffset _now;
    private readonly double _timelineWidth;

    /// <param name="channels">Channels of the current list; those without any programme are left out.</param>
    /// <param name="play">Plays a channel live (cell null) or a past programme from the archive.</param>
    internal GuideGridWindow(string listName, IReadOnlyList<Channel> channels, Func<Channel, IReadOnlyList<GuideProgramme>> programmes,
        Func<Channel, bool> hasArchive, Func<Channel, GuideGrid.Cell?, Task> play)
    {
        InitializeComponent();
        _hasArchive = hasArchive;
        _play = play;
        _now = DateTimeOffset.Now;
        (_from, var to) = GuideGrid.Window(_now);
        _timelineWidth = GuideGrid.Width(_from, to);

        var rows = channels.Where(channel => programmes(channel).Count > 0)
            .Select(channel => new Row(channel, programmes, _from, to, _now)).ToList();
        Rows.ItemsSource = rows;
        Subheading.Text = Lang.F($"{listName} · {rows.Count} kanal");
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NowButton.IsEnabled = rows.Count > 0;

        Ruler.Width = _timelineWidth;
        Ruler.Height = 34;
        foreach (var (left, label) in GuideGrid.HourMarks(_from, to))
        {
            var text = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            Canvas.SetLeft(text, left + 8);
            Canvas.SetTop(text, 9);
            Ruler.Children.Add(text);
            var tick = new Border { Width = 1, Height = 34 };
            tick.SetResourceReference(Border.BackgroundProperty, "Line");
            Canvas.SetLeft(tick, left);
            Ruler.Children.Add(tick);
        }
        Canvas.SetLeft(NowLine, GuideGrid.Offset(_now, _from));
        Loaded += (_, _) => ScrollToNow();
    }

    internal int RowCount => Rows.Items.Count;

    /// <summary>Current horizontal position in pixels from the start of the time window.</summary>
    internal double TimeOffset => -_shift.X;

    /// <summary>Every timeline (ruler, each row, the "now" line) moves with the same transform.</summary>
    private void OnTimelineLoaded(object sender, RoutedEventArgs e) => ((UIElement)sender).RenderTransform = _shift;

    private void OnTimelineSizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollRange();

    private void UpdateScrollRange()
    {
        var viewport = Math.Max(0, TimelineHost.ActualWidth);
        TimeScroll.Maximum = Math.Max(0, _timelineWidth - viewport);
        TimeScroll.ViewportSize = viewport;
        TimeScroll.LargeChange = Math.Max(120, viewport * 0.8);
        TimeScroll.SmallChange = 60 * GuideGrid.PixelsPerMinute / 2;
        _shift.X = -TimeScroll.Value;
    }

    private void OnTimeScroll(object sender, RoutedPropertyChangedEventArgs<double> e) => _shift.X = -e.NewValue;

    /// <summary>Puts the current time about a quarter into the view, so what is on now and what follows are both visible.</summary>
    internal void ScrollToNow()
    {
        UpdateScrollRange();
        TimeScroll.Value = Math.Clamp(GuideGrid.Offset(_now, _from) - TimelineHost.ActualWidth * 0.25, 0, TimeScroll.Maximum);
    }

    internal void ScrollTime(double pixels) => TimeScroll.Value = Math.Clamp(TimeScroll.Value + pixels, 0, TimeScroll.Maximum);

    private void OnNow(object sender, RoutedEventArgs e) => ScrollToNow();

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift) return; // plain wheel scrolls the channels
        ScrollTime(-e.Delta * 2);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Left:
                ScrollTime(-60 * GuideGrid.PixelsPerMinute);
                break;
            case Key.Right:
                ScrollTime(60 * GuideGrid.PixelsPerMinute);
                break;
            case Key.Home:
                ScrollToNow();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private async void OnChannelClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Row row) await ActivateAsync(row, null);
    }

    private async void OnCellClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GuideGrid.Cell cell, Tag: Row row }) await ActivateAsync(row, cell);
    }

    /// <summary>On air (or the channel name): play live. Past: play from the archive when the channel keeps one. Upcoming: just say so.</summary>
    internal async Task ActivateAsync(Row row, GuideGrid.Cell? cell)
    {
        if (cell is null || cell.IsOnAir)
        {
            Status.Text = Lang.F($"{row.Name} açılıyor…");
            await _play(row.Channel, null);
        }
        else if (cell.IsPast)
        {
            if (!_hasArchive(row.Channel))
            {
                Status.Text = Lang.F($"{row.Name} geçmiş yayınları saklamıyor; “{cell.Title}” arşivden oynatılamaz.");
                return;
            }
            Status.Text = Lang.F($"“{cell.Title}” arşivden açılıyor…");
            await _play(row.Channel, cell);
        }
        else Status.Text = Lang.F($"“{cell.Title}” henüz başlamadı · {cell.Time}");
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
