using System.Windows.Media;

namespace Zaplio;

internal readonly record struct AmbientPalette(Color Left, Color Right)
{
    internal static AmbientPalette FromBgra(ReadOnlySpan<byte> pixels, int width, int height)
    {
        var band = Math.Max(1, width / 3);
        return new AmbientPalette(AverageEdge(pixels, width, height, 0, band),
            AverageEdge(pixels, width, height, width - band, width));
    }

    private static Color AverageEdge(ReadOnlySpan<byte> pixels, int width, int height, int start, int end)
    {
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;
        var inset = height / 5;
        for (var y = inset; y < height - inset; y++)
        {
            for (var x = start; x < end; x++)
            {
                var offset = (y * width + x) * 4;
                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];
                if (Math.Max(r, Math.Max(g, b)) < 24) continue;
                red += r;
                green += g;
                blue += b;
                count++;
            }
        }

        var total = Math.Max(1, (end - start) * (height - 2 * inset));
        return count == 0 ? Colors.Transparent : Color.FromArgb((byte)(116 * count / total),
            (byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }
}
