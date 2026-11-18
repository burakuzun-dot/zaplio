using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Zaplio.Tests;

/// <summary>
/// Points Zaplio's data folder (settings, cache, logs) at a throwaway folder before any test runs, so tests never touch
/// the user's real %LocalAppData%\Zaplio. Code under test that logs a warning used to write into the user's real log.
/// </summary>
internal static class TestData
{
    public static string Folder { get; } = Path.Combine(Path.GetTempPath(), "zaplio-tests-" + Environment.ProcessId);

    [ModuleInitializer]
    internal static void Isolate()
    {
        Environment.SetEnvironmentVariable(AppPaths.OverrideVariable, Folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        };
    }
}

public sealed class TestDataTests
{
    [Fact]
    public void Tests_never_use_the_real_data_folder()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zaplio");
        Assert.True(AppPaths.IsOverridden);
        Assert.NotEqual(Path.GetFullPath(real), Path.GetFullPath(AppPaths.Root));
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(AppLog.Folder));
    }
}
