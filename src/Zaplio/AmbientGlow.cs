using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Zaplio;

/// <summary>
/// Soft light that spreads inward from the four window edges, tinted by the colors of the picture.
/// Each edge is a gradient that fades out before the middle of the window, so the center stays clean.
/// </summary>
internal sealed class AmbientGlow : Grid
{
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(1100);

    private readonly Edge _left = new(new Point(0, 0.5), new Point(1, 0.5));
    private readonly Edge _right = new(new Point(1, 0.5), new Point(0, 0.5));
    private readonly Edge _top = new(new Point(0.5, 0), new Point(0.5, 1));
    private readonly Edge _bottom = new(new Point(0.5, 1), new Point(0.5, 0));
    private double _intensity = 1;

    public AmbientGlow()
    {
        IsHitTestVisible = false;
        foreach (var edge in new[] { _left, _right, _top, _bottom }) Children.Add(edge.Surface);
    }

    internal void Show(AmbientPalette palette, bool animate)
    {
        _left.Set(palette.Left, animate);
        _right.Set(palette.Right, animate);
        _top.Set(palette.Top, animate);
        _bottom.Set(palette.Bottom, animate);
    }

    internal void Clear()
    {
        Show(AmbientPalette.Off, animate: false);
        SetIntensity(1, animate: false);
    }

    /// <summary>1 while playing, lower while paused so the light settles down with the picture.</summary>
    internal void SetIntensity(double intensity, bool animate)
    {
        if (Math.Abs(intensity - _intensity) < 0.01) return;
        _intensity = intensity;
        BeginAnimation(OpacityProperty, animate
            ? new DoubleAnimation(intensity, Fade) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } }
            : null);
        if (!animate) Opacity = intensity;
    }

    private sealed class Edge
    {
        private readonly GradientStop _strong = new(Colors.Transparent, 0);
        private readonly GradientStop _middle = new(Colors.Transparent, 0.22);
        private readonly GradientStop _gone = new(Colors.Transparent, 0.62);

        public Edge(Point start, Point end)
        {
            Surface = new Border
            {
                IsHitTestVisible = false,
                Background = new LinearGradientBrush(new GradientStopCollection { _strong, _middle, _gone }, start, end)
            };
        }

        public Border Surface { get; }

        public void Set(Color target, bool animate)
        {
            Apply(_strong, target, 1, animate);
            Apply(_middle, target, 0.38, animate);
            Apply(_gone, target, 0, animate);
        }

        private static void Apply(GradientStop stop, Color target, double strength, bool animate)
        {
            var next = Color.FromArgb((byte)Math.Round(target.A * strength), target.R, target.G, target.B);
            var current = stop.Color;
            stop.Color = next;
            if (!animate || current == next)
            {
                stop.BeginAnimation(GradientStop.ColorProperty, null);
                return;
            }
            // Fade from the old tint (an edge that was dark takes on the new tint at once, only its alpha rises).
            var from = current.A == 0 ? Color.FromArgb(0, next.R, next.G, next.B) : current;
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(from, next, Fade)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
        }
    }
}
