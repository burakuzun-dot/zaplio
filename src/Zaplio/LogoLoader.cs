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
                bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);

            if (bytes is null)
            {
                await Downloads.WaitAsync().ConfigureAwait(false);
                try { bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false); }
                finally { Downloads.Release(); }
                if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;
                var decoded = Decode(bytes, width);
                if (decoded is null) return null;
                Directory.CreateDirectory(Folder);
                var temporary = $"{file}.{Guid.NewGuid():N}.tmp";
                await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
                File.Move(temporary, file, overwrite: true);
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
