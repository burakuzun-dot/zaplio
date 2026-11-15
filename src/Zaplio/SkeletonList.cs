using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Zaplio;

/// <summary>
/// Placeholder rows with a soft shimmer, shown while a channel or movie list loads, topped by a pulsing bolt.
/// </summary>
internal sealed class SkeletonList : StackPanel
{
    private readonly TranslateTransform _sweep = new(-1, 0);
    private readonly ScaleTransform _boltPulse = new(1, 1);
    private readonly TextBlock _label = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private bool _built;
    private bool _running;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SkeletonList), new PropertyMetadata("Yükleniyor…", (d, e) => ((SkeletonList)d)._label.Text = (string)e.NewValue));

    /// <summary>Rows shaped like posters (tall) instead of channel logos (square).</summary>
    public static readonly DependencyProperty PostersProperty = DependencyProperty.Register(
        nameof(Posters), typeof(bool), typeof(SkeletonList), new PropertyMetadata(false));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool Posters
    {
        get => (bool)GetValue(PostersProperty);
        set => SetValue(PostersProperty, value);
    }

    public SkeletonList()
    {
        IsHitTestVisible = false;
        _label.Text = Text;
        Loaded += (_, _) => Build();
        IsVisibleChanged += (_, _) => Update();
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");

        var bolt = new Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _boltPulse,
            VerticalAlignment = VerticalAlignment.Center
        };
        bolt.SetResourceReference(Path.DataProperty, "ZpBoltIcon");
        bolt.SetResourceReference(Shape.FillProperty, "ZpBoltBrush");
        Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 0, 12), Children = { bolt, _label } });

        var shimmer = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            RelativeTransform = _sweep,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x1A, 0x20, 0x30), 0),
                new GradientStop(Color.FromRgb(0x1A, 0x20, 0x30), 0.35),
                new GradientStop(Color.FromRgb(0x2A, 0x31, 0x48), 0.5),
                new GradientStop(Color.FromRgb(0x1A, 0x20, 0x30), 0.65),
                new GradientStop(Color.FromRgb(0x1A, 0x20, 0x30), 1)
            }
        };
        var tile = Posters ? new Size(38, 54) : new Size(38, 38);
        for (var index = 0; index < 9; index++)
        {
            var row = new Grid { Margin = new Thickness(6, 0, 6, Posters ? 14 : 12), Opacity = 1 - index * 0.085 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tile.Width + 10) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new Rectangle { Width = tile.Width, Height = tile.Height, RadiusX = 8, RadiusY = 8, Fill = shimmer, HorizontalAlignment = HorizontalAlignment.Left });
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lines, 1);
            lines.Children.Add(new Rectangle { Height = 10, RadiusX = 5, RadiusY = 5, Fill = shimmer, Width = 120 + index * 37 % 70 });
            lines.Children.Add(new Rectangle
            {
                Height = 8, RadiusX = 4, RadiusY = 4, Fill = shimmer, Width = 60 + index * 23 % 40,
                Margin = new Thickness(0, 7, 0, 0), HorizontalAlignment = HorizontalAlignment.Left
            });
            ((Rectangle)lines.Children[0]).HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(lines);
            Children.Add(row);
        }
        Update();
    }

    private void Update()
    {
        var run = _built && IsVisible && Motion.Enabled;
        if (run == _running) return;
        _running = run;
        if (!run)
        {
            _sweep.BeginAnimation(TranslateTransform.XProperty, null);
            _boltPulse.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _boltPulse.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            return;
        }
        _sweep.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-1, 1, TimeSpan.FromSeconds(1.5)) { RepeatBehavior = RepeatBehavior.Forever });
        var pulse = new DoubleAnimation(0.8, 1.15, TimeSpan.FromSeconds(0.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _boltPulse.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        _boltPulse.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }
}
