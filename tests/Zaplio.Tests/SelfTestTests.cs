using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class SelfTestTests : IDisposable
{
    private const string LocalAppData = @"C:\Users\x\AppData\Local";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "zaplio-test-selftest-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Data_folder_is_local_app_data_unless_redirected()
    {
        Assert.Equal(Path.Combine(LocalAppData, "Zaplio"), AppPaths.Resolve(null, LocalAppData));
        Assert.Equal(Path.Combine(LocalAppData, "Zaplio"), AppPaths.Resolve("  ", LocalAppData));
        Assert.Equal(Path.GetFullPath(_folder), AppPaths.Resolve(" " + _folder + " ", LocalAppData));
    }

    [Fact]
    public void Self_test_refuses_to_run_on_the_real_data_folder()
    {
        // As a normal start of the app would be: no ZAPLIO_DATA_DIR. (The test process itself has one, see TestData.)
        Assert.False(SelfTest.TryStart(["--self-test", Path.Combine(_folder, "rapor.txt")], dataFolderOverridden: false, out var refusal));
        Assert.Contains(AppPaths.OverrideVariable, refusal);
        Assert.False(SelfTest.Active);

        Assert.False(SelfTest.TryStart(["--self-test"], out var missing));
        Assert.NotNull(missing);
    }

    [Fact]
    public void A_normal_start_is_not_a_self_test()
    {
        Assert.True(SelfTest.TryStart([], out var refusal));
        Assert.Null(refusal);
        Assert.False(SelfTest.Active);
    }

    [Fact]
    public void Demo_list_is_a_local_file_with_made_up_channels_on_example_com()
    {
        var path = SelfTest.WriteDemoList(_folder);
        var lines = File.ReadAllLines(path);

        Assert.Equal("#EXTM3U", lines[0]);
        Assert.Equal(12, lines.Count(line => line.StartsWith("#EXTINF", StringComparison.Ordinal)));
        Assert.All(lines.Where(line => line.StartsWith("http", StringComparison.Ordinal)),
            line => Assert.StartsWith("http://example.com/", line, StringComparison.Ordinal));
    }
}
