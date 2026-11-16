using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class LocalMediaTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "zaplio-local-" + Guid.NewGuid().ToString("N"));

    public LocalMediaTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Theory]
    [InlineData("film.mkv", true)]
    [InlineData("FILM.MP4", true)]
    [InlineData("klip.webm", true)]
    [InlineData("liste.m3u", false)]
    [InlineData("not.txt", false)]
    public void RecognisesVideoFiles(string name, bool video) => Assert.Equal(video, LocalMedia.IsVideo(name));

    [Fact]
    public void SortsVideosAndSubtitlesAndExpandsFolders()
    {
        Touch("Bölüm 10.mkv");
        Touch("Bölüm 2.mkv");
        Touch("Bölüm 1.mkv");
        Touch("notlar.txt");
        var subtitle = Touch("Bölüm 1.srt");

        var (videos, subtitles) = LocalMedia.Sort([_folder, subtitle, Path.Combine(_folder, "Bölüm 2.mkv")]);

        Assert.Equal(["Bölüm 1.mkv", "Bölüm 2.mkv", "Bölüm 10.mkv"], videos.Select(Path.GetFileName));
        Assert.Equal([subtitle], subtitles);
    }

    [Fact]
    public void AnEntryHasAStableIdAndAReadableTitle()
    {
        var path = Touch("Belgesel.Adi.2021.1080p.mkv");

        var first = LocalMedia.ToChannel(path);
        var again = LocalMedia.ToChannel(path.ToUpperInvariant());

        Assert.Equal(first.Id, again.Id);
        Assert.StartsWith("file:", first.Id);
        Assert.DoesNotContain(_folder, first.Id, StringComparison.OrdinalIgnoreCase); // the id does not carry the path
        Assert.Equal("Belgesel Adi 2021 1080p", first.Name);
        Assert.True(first.IsOnDemand);
        Assert.True(LocalMedia.IsLocal(first));
        Assert.Equal(Guid.Empty, first.SourceId);
    }

    [Fact]
    public void NamesWithSpacesKeepTheirDots()
    {
        Assert.Equal("Yaz tatili 2. gün", LocalMedia.Title(@"C:\v\Yaz tatili 2. gün.mp4"));
    }

    [Fact]
    public void AMissingFolderIsIgnored()
    {
        var (videos, subtitles) = LocalMedia.Sort([Path.Combine(_folder, "yok")]);
        Assert.Empty(videos);
        Assert.Empty(subtitles);
    }
}
