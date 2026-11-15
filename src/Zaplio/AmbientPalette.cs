using System.Windows.Media;

namespace Zaplio;

/// <summary>Glow colors taken from the four edges of the picture (alpha says how much of that edge actually has color).</summary>
internal readonly record struct AmbientPalette(Color Left, Color Right, Color Top, Color Bottom)
{
    /// <summary>Strongest glow alpha; a fully colored edge reaches it.</summary>
    internal const byte MaxAlpha = 150;

    private const int DarkLevel = 24;

    internal static AmbientPalette Off => new(Colors.Transparent, Colors.Transparent, Colors.Transparent, Colors.Transparent);

    internal static AmbientPalette FromBgra(ReadOnlySpan<byte> pixels, int width, int height)
    {
        // Letterbox bars are black: only the rows that really carry picture decide the glow.
        var first = -1;
        var last = -1;
        var minimum = Math.Max(1, width / 16);
        for (var y = 0; y < height; y++)
        {
            var lit = 0;
            for (var x = 0; x < width; x++)
                if (Brightest(pixels, (y * width + x) * 4) >= DarkLevel) lit++;
            if (lit < minimum) continue;
            if (first < 0) first = y;
            last = y;
        }
        if (first < 0) return Off;

        var contentHeight = last - first + 1;
        var sideInset = contentHeight / 5;
        var sideStart = first + sideInset;
        var sideEnd = last + 1 - sideInset;
        if (sideEnd <= sideStart) (sideStart, sideEnd) = (first, last + 1);
        var bandWidth = Math.Max(1, width / 3);
        var bandHeight = Math.Max(1, contentHeight / 3);
        var columnInset = width / 5;
        var columnStart = columnInset;
        var columnEnd = width - columnInset;
        if (columnEnd <= columnStart) (columnStart, columnEnd) = (0, width);

        return new AmbientPalette(
            Average(pixels, width, sideStart, sideEnd, 0, bandWidth),
            Average(pixels, width, sideStart, sideEnd, width - bandWidth, width),
            Average(pixels, width, first, first + bandHeight, columnStart, columnEnd),
            Average(pixels, width, last + 1 - bandHeight, last + 1, columnStart, columnEnd));
    }

    /// <summary>Moves the glow part of the way to a new picture so scene cuts fade instead of jumping.</summary>
    internal static AmbientPalette Blend(AmbientPalette from, AmbientPalette to, double amount) =>
        new(Mix(from.Left, to.Left, amount), Mix(from.Right, to.Right, amount),
            Mix(from.Top, to.Top, amount), Mix(from.Bottom, to.Bottom, amount));

    private static Color Mix(Color from, Color to, double amount)
    {
        // An edge that was dark has no color of its own to fade from; an edge that goes dark keeps its color while fading out.
        if (from.A == 0) return Color.FromArgb((byte)Math.Round(to.A * amount), to.R, to.G, to.B);
        if (to.A == 0) return Color.FromArgb((byte)Math.Round(from.A * (1 - amount)), from.R, from.G, from.B);
        return Color.FromArgb(Lerp(from.A, to.A, amount), Lerp(from.R, to.R, amount), Lerp(from.G, to.G, amount), Lerp(from.B, to.B, amount));
    }

    private static byte Lerp(byte from, byte to, double amount) => (byte)Math.Round(from + (to - from) * amount);

    private static int Brightest(ReadOnlySpan<byte> pixels, int offset) => Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));

    private static Color Average(ReadOnlySpan<byte> pixels, int width, int top, int bottom, int left, int right)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = (y * width + x) * 4;
                if (Brightest(pixels, offset) < DarkLevel) continue;
                blue += pixels[offset];
                green += pixels[offset + 1];
                red += pixels[offset + 2];
                count++;
            }
        }

        var total = (bottom - top) * (right - left);
        if (count == 0 || total <= 0) return Colors.Transparent;
        return Vivid(red / (double)count, green / (double)count, blue / (double)count, (byte)(MaxAlpha * count / total));
    }

    /// <summary>Averages come out muddy; push saturation up and keep the glow from being too dark to read as a color.</summary>
    internal static Color Vivid(double red, double green, double blue, byte alpha)
    {
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var value = max / 255;
        var saturation = max <= 0 ? 0 : (max - min) / max;
        var hue = 0.0;
        if (max > min)
        {
            var spread = max - min;
            if (max == red) hue = ((green - blue) / spread + 6) % 6;
            else if (max == green) hue = (blue - red) / spread + 2;
            else hue = (red - green) / spread + 4;
        }

        if (saturation > 0.04) saturation = Math.Min(1, saturation * 1.45 + 0.05);
        value = Math.Clamp(value, 0.5, 1);

        double Channel(double offset)
        {
            var k = (offset + hue) % 6;
            return value * (1 - saturation * Math.Max(0, Math.Min(Math.Min(k, 4 - k), 1)));
        }

        return Color.FromArgb(alpha, (byte)Math.Round(Channel(5) * 255), (byte)Math.Round(Channel(3) * 255), (byte)Math.Round(Channel(1) * 255));
    }
}
