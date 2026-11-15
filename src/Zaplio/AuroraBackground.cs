using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Zaplio;

/// <summary>
/// Calm living background for the empty player: three soft brand-colored lights drift slowly and a few sparks float up.
/// Animations run only while the background is on screen and Windows animation effects are on.
/// </summary>
internal sealed class AuroraBackground : Grid
{
    private readonly List<(IAnimatable Target, DependencyProperty Property, AnimationTimeline Animation)> _motions = [];
    private readonly Canvas _sparks = new() { IsHitTestVisible = false };
    private readonly List<(Ellipse Spark, double X, double Y)> _sparkPlaces = [];
    private bool _running;

    public AuroraBackground()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;

        AddLight(Color.FromRgb(0x7B, 0x61, 0xFF), 0x5C, 560, HorizontalAlignment.Right, VerticalAlignment.Center, -90, 40, 23);
        AddLight(Color.FromRgb(0x3D, 0x6B, 0xFF), 0x3A, 480, HorizontalAlignment.Left, VerticalAlignment.Bottom, 70, -50, 29);
        AddLight(Color.FromRgb(0xFF, 0xC8, 0x3D), 0x1E, 360, HorizontalAlignment.Center, VerticalAlignment.Top, 60, 70, 19);

        Children.Add(_sparks);
        var random = new Random(7); // same pattern every time: calm, not busy
        for (var index = 0; index < 11; index++)
        {
            var size = 2 + random.NextDouble() * 2.5;
            var warm = index % 3 == 0;
            var spark = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(warm ? Color.FromRgb(0xFF, 0xC8, 0x3D) : Color.FromRgb(0xC1, 0xB4, 0xFF)),
                Opacity = 0,
                RenderTransform = new TranslateTransform()
            };
            _sparks.Children.Add(spark);
            _sparkPlaces.Add((spark, 0.08 + random.NextDouble() * 0.84, 0.25 + random.NextDouble() * 0.7));

            var duration = TimeSpan.FromSeconds(7 + random.NextDouble() * 5);
            var begin = TimeSpan.FromSeconds(random.NextDouble() * 8);
            _motions.Add((spark.RenderTransform, TranslateTransform.YProperty,
                new DoubleAnimation(0, -46 - random.NextDouble() * 30, duration) { BeginTime = begin, RepeatBehavior = RepeatBehavior.Forever }));
            _motions.Add((spark, OpacityProperty, new DoubleAnimationUsingKeyFrames
            {
                BeginTime = begin,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever,
                KeyFrames =
                {
                    new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)),
                    new LinearDoubleKeyFrame(0.75, KeyTime.FromPercent(0.3)),
                    new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0.7)),
                    new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1))
                }
            }));
        }

        SizeChanged += (_, _) => PlaceSparks();
        IsVisibleChanged += (_, _) => Update();
        Loaded += (_, _) => Update();
        Unloaded += (_, _) => Stop();
    }

    private void AddLight(Color color, byte alpha, double size, HorizontalAlignment horizontal, VerticalAlignment vertical,
        double driftX, double driftY, double seconds)
    {
        var brush = new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
        var shift = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = new Thickness(-size / 3),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { scale, shift } }
        });

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var length = TimeSpan.FromSeconds(seconds);
        _motions.Add((shift, TranslateTransform.XProperty,
            new DoubleAnimation(0, driftX, length) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease }));
        _motions.Add((shift, TranslateTransform.YProperty,
            new DoubleAnimation(0, driftY, TimeSpan.FromSeconds(seconds * 0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease }));
        var breathe = new DoubleAnimation(0.92, 1.12, TimeSpan.FromSeconds(seconds * 0.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
        _motions.Add((scale, ScaleTransform.ScaleXProperty, breathe));
        _motions.Add((scale, ScaleTransform.ScaleYProperty, breathe));
    }

    private void PlaceSparks()
    {
        foreach (var (spark, x, y) in _sparkPlaces)
        {
            Canvas.SetLeft(spark, x * ActualWidth);
            Canvas.SetTop(spark, y * ActualHeight);
        }
    }

    private void Update()
    {
        if (IsVisible && Motion.Enabled) Start();
        else Stop();
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        PlaceSparks();
        foreach (var (target, property, animation) in _motions) target.BeginAnimation(property, animation);
    }

    private void Stop()
    {
        if (!_running) return;
        _running = false;
        foreach (var (target, property, _) in _motions) target.BeginAnimation(property, null);
    }
}
