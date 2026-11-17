using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public void TheNameDependsOnTheDataFolderAndUserButHidesBoth()
    {
        var name = SingleInstance.NameFor(@"C:\Kullanıcı\Veri\Zaplio", "S-1-5-21-1");

        Assert.Equal(name, SingleInstance.NameFor(@"c:\kullanıcı\veri\zaplio\", "S-1-5-21-1")); // case and trailing slash do not matter
        Assert.NotEqual(name, SingleInstance.NameFor(@"C:\Başka\Zaplio", "S-1-5-21-1"));      // the self test's folder gets its own
        Assert.NotEqual(name, SingleInstance.NameFor(@"C:\Kullanıcı\Veri\Zaplio", "S-1-5-21-2"));
        Assert.StartsWith("Zaplio-", name);
        Assert.DoesNotContain("Veri", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("S-1-5", name);
    }

    [Fact]
    public void MessagesCarryPathsAndDropEmptyOnes()
    {
        var decoded = SingleInstance.Decode(SingleInstance.Encode([@"C:\v\film.mkv", "", "  ", @"D:\dizi"]));
        Assert.Equal([@"C:\v\film.mkv", @"D:\dizi"], decoded);
        Assert.Empty(SingleInstance.Decode(SingleInstance.Encode([])));
    }

    [Fact]
    public async Task ASecondLaunchHandsItsFilesToTheFirst()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zaplio-tek-" + Guid.NewGuid().ToString("N"));
        using var first = new SingleInstance(folder);
        using var second = new SingleInstance(folder);
        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);

        var received = new TaskCompletionSource<IReadOnlyList<string>>();
        using var stop = new CancellationTokenSource();
        var listening = first.ListenAsync(paths => received.TrySetResult(paths), stop.Token);

        Assert.True(second.SendToFirst([@"C:\v\film.mkv"], timeoutMs: 5000));
        var paths = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([@"C:\v\film.mkv"], paths);

        stop.Cancel();
        await listening.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WithNobodyListeningTheSecondLaunchKnowsItFailed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zaplio-tek-" + Guid.NewGuid().ToString("N"));
        using var first = new SingleInstance(folder);
        using var second = new SingleInstance(folder);
        Assert.False(second.SendToFirst([], timeoutMs: 200)); // the first never listened: start normally instead of vanishing
    }
}
