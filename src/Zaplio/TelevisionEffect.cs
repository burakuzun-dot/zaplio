using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Zaplio;

public sealed class TelevisionEffect : FrameworkElement
{
    internal static readonly TimeSpan PowerOffDuration = TimeSpan.FromMilliseconds(340);
    private static readonly TimeSpan StaticDuration = TimeSpan.FromMilliseconds(220);
    private static readonly Lazy<BitmapSource[]> StaticFrames = new(CreateStaticFrames);
    private static readonly Brush Phosphor = CreatePhosphorBrush();

    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(TelevisionEffect), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _powerOff;

    internal static bool IsChannelChange(Channel? previous, Channel next, bool reconnect) =>
        !reconnect && previous is { IsOnDemand: false } && !next.IsOnDemand && previous.Id != next.Id;

    internal void ShowStatic() => Start(powerOff: false, StaticDuration);

    internal void ShowPowerOff() => Start(powerOff: true, PowerOffDuration);

    private void Start(bool powerOff, TimeSpan duration)
    {
        _powerOff = powerOff;
        Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(0, 1, new Duration(duration));
        if (!powerOff)
        {
            animation.Completed += (_, _) =>
            {
                Visibility = Visibility.Collapsed;
                BeginAnimation(ProgressProperty, null);
            };
        }
        BeginAnimation(ProgressProperty, animation);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        var area = new Rect(RenderSize);
        var progress = (double)GetValue(ProgressProperty);
        if (_powerOff)
        {
            drawing.PushOpacity(Math.Min(1, progress * 8));
            drawing.DrawRectangle(Brushes.Black, null, area);
            drawing.Pop();

            // The glow collapses vertically, then contracts into a point and fades.
            var width = area.Width * 0.88 * (1 - Math.Clamp((progress - 0.48) / 0.5, 0, 1));
            var height = Math.Max(2, area.Height * 0.20 * Math.Pow(Math.Max(0, 1 - progress * 2.2), 3));
            drawing.PushOpacity(0.8 * Math.Min(progress * 8, 1 - progress));
            drawing.DrawRoundedRectangle(Phosphor, null,
                new Rect((area.Width - width) / 2, (area.Height - height) / 2, width, height), 2, 2);
            drawing.Pop();
            return;
        }

        var frames = StaticFrames.Value;
        var frame = Math.Min((int)(progress * frames.Length), frames.Length - 1);
        drawing.PushOpacity(Math.Min(1, (1 - progress) * 3) * 0.88);
        drawing.DrawImage(frames[frame], area);
        drawing.Pop();
    }

    private static BitmapSource[] CreateStaticFrames()
    {
        const int width = 128;
        const int height = 72;
        var random = new Random(17);
        var frames = new BitmapSource[4];
        var pixels = new byte[width * height];
        for (var frame = 0; frame < frames.Length; frame++)
        {
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = (byte)(random.Next(18, 96) + (y % 3 == 0 ? 10 : 0));
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
            bitmap.Freeze();
            frames[frame] = bitmap;
        }
        return frames;
    }

    private static Brush CreatePhosphorBrush()
    {
        var edge = Color.FromRgb(133, 113, 198);
        var brush = new LinearGradientBrush(new GradientStopCollection
        {
            new(edge, 0), new(Color.FromRgb(226, 220, 255), 0.5), new(edge, 1)
        }, 0);
        brush.Freeze();
        return brush;
    }
}
