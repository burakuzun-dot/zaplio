using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class LogoCacheTests : IDisposable
{
    // A throwaway folder: the tests never touch the real cache in %LocalAppData%.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "zaplio-test-logos-" + Guid.NewGuid().ToString("N"));

    public LogoCacheTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string File(string name, int bytes, TimeSpan age)
    {
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        System.IO.File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    [Fact]
    public void Removes_the_oldest_files_until_the_folder_fits()
    {
        var oldest = File("a.img", 400, TimeSpan.FromDays(3));
        var middle = File("b.img", 400, TimeSpan.FromDays(2));
        var newest = File("c.img", 400, TimeSpan.FromDays(1));

        var freed = LogoLoader.PruneDiskCache(_folder, 900, TimeSpan.FromDays(30));

        Assert.Equal(400, freed);
        Assert.False(System.IO.File.Exists(oldest));
        Assert.True(System.IO.File.Exists(middle));
        Assert.True(System.IO.File.Exists(newest));
    }

    [Fact]
    public void Removes_expired_files_even_when_there_is_room()
    {
        var expired = File("old.img", 10, TimeSpan.FromDays(40));
        var fresh = File("new.img", 10, TimeSpan.FromDays(1));

        LogoLoader.PruneDiskCache(_folder, 1_000_000, TimeSpan.FromDays(30));

        Assert.False(System.IO.File.Exists(expired));
        Assert.True(System.IO.File.Exists(fresh));
    }

    [Fact]
    public void Leaves_other_files_and_a_missing_folder_alone()
    {
        var other = File("notes.txt", 5_000, TimeSpan.FromDays(90));

        Assert.Equal(0, LogoLoader.PruneDiskCache(_folder, 1, TimeSpan.FromDays(30)));
        Assert.True(System.IO.File.Exists(other));
        Assert.Equal(0, LogoLoader.PruneDiskCache(Path.Combine(_folder, "yok"), 1, TimeSpan.FromDays(30)));
    }
}
