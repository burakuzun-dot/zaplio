using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Zaplio;

/// <summary>
/// Attached property that loads channel logos and posters for virtualized list rows:
/// <c>local:LogoLoader.Url="{Binding Logo}"</c>. Images are cached on disk (30 days) and in memory, decoded small,
/// fetched a few at a time, and only for rows that are actually on screen.
/// </summary>
public static class LogoLoader
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MemoryLimit = 600;
    private static readonly TimeSpan DiskMaxAge = TimeSpan.FromDays(30);
    private static readonly HttpClient Http = CreateClient();
    private static readonly SemaphoreSlim Downloads = new(6);
    private static readonly Dictionary<string, ImageSource> Memory = new();
    private static readonly LinkedList<string> MemoryOrder = new();
    private static readonly Dictionary<string, Task<ImageSource?>> Pending = new();
    private static readonly HashSet<string> Failed = new();

    public static bool Enabled { get; set; } = true;

    public static string Folder { get; } = Path.Combine(CatalogCache.Folder, "logos");

    /// <summary>
    /// Width images are stored at: the widest DecodeWidth in use (the preview poster), so nothing on screen loses detail.
    /// Providers' full-size posters (often 100 KB–2 MB) shrink by about 60 %, the folder holds far more of them and
    /// scrolling decodes much less.
    /// </summary>
    internal const int StoredWidth = 360;

    /// <summary>Files smaller than this are left as they are; shrinking them would gain little.</summary>
    internal const int ShrinkAbove = 48 * 1024;

    /// <summary>Upper limit for the logo and poster files on disk; large lists otherwise grow the folder without end.</summary>
    internal const long DiskLimitBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Deletes expired images, then the least recently written ones until the folder fits in <paramref name="maxBytes"/>
    /// (they are simply downloaded again when needed). Returns the bytes freed. Never throws: a file in use is skipped.
    /// </summary>
    internal static long PruneDiskCache(string folder, long maxBytes, TimeSpan maxAge)
    {
        long freed = 0;
        try
        {
            if (!Directory.Exists(folder)) return 0;
            var files = new DirectoryInfo(folder).GetFiles("*.img").OrderBy(file => file.LastWriteTimeUtc).ToList();
            var total = files.Sum(file => file.Length);
            var expiredBefore = DateTime.UtcNow - maxAge;
            foreach (var file in files)
            {
                if (total <= maxBytes && file.LastWriteTimeUtc >= expiredBefore) continue;
                try
                {
                    var length = file.Length;
                    file.Delete();
                    total -= length;
                    freed += length;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        return freed;
    }

    /// <summary>Background clean-up at startup.</summary>
    internal static void PruneDiskCache() => _ = Task.Run(() =>
    {
        var freed = PruneDiskCache(Folder, DiskLimitBytes, DiskMaxAge);
        if (freed > 0) AppLog.Info($"Logo önbelleği küçültüldü: {freed / (1024 * 1024)} MB silindi.");
    });

    public static readonly DependencyProperty UrlProperty = DependencyProperty.RegisterAttached(
        "Url", typeof(string), typeof(LogoLoader), new PropertyMetadata(null, OnUrlChanged));

    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.RegisterAttached(
        "DecodeWidth", typeof(int), typeof(LogoLoader), new PropertyMetadata(80));

    public static string? GetUrl(DependencyObject element) => (string?)element.GetValue(UrlProperty);
    public static void SetUrl(DependencyObject element, string? value) => element.SetValue(UrlProperty, value);
    public static int GetDecodeWidth(DependencyObject element) => (int)element.GetValue(DecodeWidthProperty);
    public static void SetDecodeWidth(DependencyObject element, int value) => element.SetValue(DecodeWidthProperty, value);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = MaxBytes };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Zaplio/1.0");
        return client;
    }

    private static async void OnUrlChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Image image) return;
        image.Source = null; // recycled rows must not show the previous channel's logo
        if (!Enabled || e.NewValue is not string url || !IsWebAddress(url) || Failed.Contains(url)) return;
        if (Memory.TryGetValue(url, out var known))
        {
            Touch(url);
            image.Source = known;
            return;
        }

        if (!Pending.TryGetValue(url, out var task))
        {
            var width = GetDecodeWidth(image);
            Pending[url] = task = Task.Run(() => LoadAsync(url, width));
        }
        var source = await task;
        Pending.Remove(url);
        if (source is null)
        {
            Failed.Add(url);
            return;
        }
        Remember(url, source);
        if (GetUrl(image) != url) return;
        image.Source = source;
        Motion.FadeIn(image); // arrived from the network: ease in instead of popping
    }

    private static bool IsWebAddress(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var address) && address.Scheme is "http" or "https";

    private static async Task<ImageSource?> LoadAsync(string url, int width)
    {
        var file = Path.Combine(Folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32] + ".img");
        try
        {
            byte[]? bytes = null;
            if (File.Exists(file) && DateTime.Now - File.GetLastWriteTime(file) < DiskMaxAge)
            {
                bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
                // Saved by an older version at full size: store it small from now on, keeping its age.
                if (bytes.Length > ShrinkAbove && Shrink(bytes, StoredWidth) is { } smaller && smaller.Length < bytes.Length)
                {
                    var written = File.GetLastWriteTimeUtc(file);
                    bytes = smaller;
                    await WriteAtomicAsync(file, bytes).ConfigureAwait(false);
                    File.SetLastWriteTimeUtc(file, written);
                }
            }

            if (bytes is null)
            {
                await Downloads.WaitAsync().ConfigureAwait(false);
                try { bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false); }
                finally { Downloads.Release(); }
                if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;
                var decoded = Decode(bytes, width);
                if (decoded is null) return null;
                if (bytes.Length > ShrinkAbove && Shrink(bytes, StoredWidth) is { } smaller && smaller.Length < bytes.Length) bytes = smaller;
                await WriteAtomicAsync(file, bytes).ConfigureAwait(false);
                return decoded;
            }
            return Decode(bytes, width);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or
            UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task WriteAtomicAsync(string file, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = $"{file}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            File.Move(temporary, file, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>
    /// The image scaled down to <paramref name="maxWidth"/> pixels wide: PNG when it has transparency (logos), otherwise
    /// JPEG. Null when it cannot be read or is already that narrow; the caller then keeps the original, as it also does
    /// when the result is not smaller.
    /// </summary>
    internal static byte[]? Shrink(byte[] original, int maxWidth)
    {
        try
        {
            var decoder = BitmapDecoder.Create(new MemoryStream(original, writable: false),
                BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            if (frame.PixelWidth <= maxWidth) return null;

            var scale = (double)maxWidth / frame.PixelWidth;
            BitmapSource scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            var transparent = HasAlpha(frame.Format) ||
                frame.Palette?.Colors.Any(color => color.A < 255) == true;
            BitmapEncoder encoder;
            if (transparent)
            {
                encoder = new PngBitmapEncoder();
                if (!HasAlpha(scaled.Format)) scaled = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            }
            else
            {
                encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                if (scaled.Format != PixelFormats.Bgr24 && scaled.Format != PixelFormats.Gray8)
                    scaled = new FormatConvertedBitmap(scaled, PixelFormats.Bgr24, null, 0);
            }
            encoder.Frames.Add(BitmapFrame.Create(scaled));
            using var output = new MemoryStream();
            encoder.Save(output);
            return output.ToArray();
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException or ArgumentException or
            InvalidOperationException or IOException or OverflowException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static bool HasAlpha(PixelFormat format) =>
        format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 || format == PixelFormats.Rgba64 ||
        format == PixelFormats.Prgba64 || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float;

    private static ImageSource? Decode(byte[] bytes, int width)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = width;
            image.StreamSource = new MemoryStream(bytes, writable: false);
            image.EndInit();
            image.Freeze(); // decoded on a worker thread, shown on the UI thread
            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException or ArgumentException or
            InvalidOperationException or IOException)
        {
            return null; // SVG, broken files and HTML error pages are skipped
        }
    }

    private static void Remember(string url, ImageSource source)
    {
        if (Memory.ContainsKey(url)) { Touch(url); return; }
        Memory[url] = source;
        MemoryOrder.AddFirst(url);
        while (MemoryOrder.Count > MemoryLimit)
        {
            Memory.Remove(MemoryOrder.Last!.Value);
            MemoryOrder.RemoveLast();
        }
    }

    private static void Touch(string url)
    {
        MemoryOrder.Remove(url);
        MemoryOrder.AddFirst(url);
    }
}
