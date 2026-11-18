using System.IO;
using System.IO.Compression;
using System.Windows.Media.Imaging;

namespace Zaplio;

internal static class IntroFrameSequence
{
    internal const int FramesPerSecond = 30;
    internal static string PathOnDisk => Path.Combine(AppContext.BaseDirectory, "Assets", "Intro-transparent.frames.zip");

    internal static BitmapSource[] Load(string path, bool animate)
    {
        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
        if (entries.Length == 0) throw new InvalidDataException(Lang.T("Açılış animasyonunda kare bulunamadı."));
        if (!animate) return [Decode(entries[^1])];
        return entries.Select(Decode).ToArray();
    }

    private static BitmapSource Decode(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var frame = new BitmapImage();
        frame.BeginInit();
        frame.StreamSource = buffer;
        frame.CacheOption = BitmapCacheOption.OnLoad;
        frame.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        frame.EndInit();
        frame.Freeze();
        return frame;
    }
}
