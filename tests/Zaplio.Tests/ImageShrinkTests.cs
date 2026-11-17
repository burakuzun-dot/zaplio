using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Zaplio.Tests;

[Collection(WpfCollection.Name)]
public sealed class ImageShrinkTests
{
    /// <summary>A busy picture (so it does not compress to nothing), encoded the way providers send posters.</summary>
    private static byte[] Picture(int width, int height, bool transparent)
    {
        var format = transparent ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var random = new Random(7);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)random.Next(256);
            pixels[i + 1] = (byte)(i / stride % 256);
            pixels[i + 2] = (byte)(i % stride % 256);
            pixels[i + 3] = transparent && i % stride < stride / 2 ? (byte)0 : (byte)255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        BitmapEncoder encoder = transparent ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = 95 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static BitmapFrame Read(byte[] bytes) =>
        BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

    [Fact]
    public void A_large_poster_is_stored_at_the_widest_size_shown_and_much_smaller()
    {
        var original = Picture(1000, 1500, transparent: false);
        var shrunk = LogoLoader.Shrink(original, LogoLoader.StoredWidth);

        Assert.NotNull(shrunk);
        Assert.True(shrunk!.Length < original.Length / 2, $"{original.Length} → {shrunk.Length}");
        var frame = Read(shrunk);
        Assert.Equal(LogoLoader.StoredWidth, frame.PixelWidth);
        Assert.InRange(frame.PixelHeight, 539, 541); // proportions kept
        Assert.IsType<JpegBitmapDecoder>(frame.Decoder);
    }

    [Fact]
    public void A_logo_with_transparency_stays_transparent()
    {
        var shrunk = LogoLoader.Shrink(Picture(800, 400, transparent: true), LogoLoader.StoredWidth);

        Assert.NotNull(shrunk);
        var frame = Read(shrunk!);
        Assert.IsType<PngBitmapDecoder>(frame.Decoder);
        Assert.True(frame.Format == PixelFormats.Bgra32 || frame.Format == PixelFormats.Pbgra32, frame.Format.ToString());
    }

    [Fact]
    public void Narrow_images_and_unreadable_bytes_are_left_alone()
    {
        Assert.Null(LogoLoader.Shrink(Picture(300, 450, transparent: false), LogoLoader.StoredWidth));
        Assert.Null(LogoLoader.Shrink([1, 2, 3, 4, 5], LogoLoader.StoredWidth));
        Assert.Null(LogoLoader.Shrink([], LogoLoader.StoredWidth));
    }

    [Fact]
    public void Every_decode_width_in_use_fits_in_the_stored_width()
    {
        // If a wider DecodeWidth is added somewhere, stored images would look soft there: raise StoredWidth with it.
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Zaplio"));
        var widths = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".xaml") || path.EndsWith(".cs")) && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path),
                @"(?:DecodeWidth=""|SetDecodeWidth\([^,]+,\s*)(\d+)").Select(match => int.Parse(match.Groups[1].Value)))
            .ToList();
        Assert.NotEmpty(widths);
        Assert.All(widths, width => Assert.True(width <= LogoLoader.StoredWidth, $"DecodeWidth {width} > {LogoLoader.StoredWidth}"));
    }
}
