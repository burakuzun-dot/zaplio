using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class UpdateInstallerTests
{
    // Shaped like GitHub's answer for the 0.11.0 release.
    private const string Release = """
        {
          "tag_name": "v0.11.0", "draft": false, "prerelease": false,
          "assets": [
            { "name": "Zaplio-0.11.0-setup.exe", "size": 81631649,
              "digest": "sha256:5d0d313f38edb85785e2269ec1231b14217afc9542ecf64d4ace58320c46784d",
              "browser_download_url": "https://github.com/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe" },
            { "name": "Zaplio-0.11.0-win-x64.zip", "size": 127149011,
              "browser_download_url": "https://github.com/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-win-x64.zip" }
          ]
        }
        """;

    private static UpdatePackage? Find(string json, string version = "0.11.0")
    {
        using var document = JsonDocument.Parse(json);
        return UpdateInstaller.FindInstaller(document.RootElement, Version.Parse(version));
    }

    [Fact]
    public void TheInstallerOfTheReleaseIsFoundWithItsSizeAndHash()
    {
        var package = Find(Release);

        Assert.NotNull(package);
        Assert.Equal("https://github.com/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe", package.Url.AbsoluteUri);
        Assert.Equal(81631649, package.Size);
        Assert.Equal("5d0d313f38edb85785e2269ec1231b14217afc9542ecf64d4ace58320c46784d", package.Sha256);
    }

    [Theory]
    [InlineData("https://evil.example/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe")]
    [InlineData("http://github.com/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe")]
    [InlineData("https://github.com/someone/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe")]
    public void AnInstallerFromAnywhereElseIsRefused(string address)
    {
        var json = Release.Replace("https://github.com/burakuzun-dot/zaplio/releases/download/v0.11.0/Zaplio-0.11.0-setup.exe", address);
        Assert.Null(Find(json));
    }

    [Fact]
    public void WithoutAnInstallerForThatVersionThereIsNothingToInstall()
    {
        Assert.Null(Find(Release, "0.12.0"));
        Assert.Null(Find("""{ "tag_name": "v0.11.0" }"""));
    }

    [Fact]
    public void OnlyAnInstalledCopyUpdatesItself()
    {
        var folder = Directory.CreateTempSubdirectory("zaplio-kurulu-").FullName;
        try
        {
            Assert.False(UpdateInstaller.IsInstalledCopy(folder)); // the ZIP version or a development build
            File.WriteAllBytes(Path.Combine(folder, "unins000.exe"), []);
            Assert.True(UpdateInstaller.IsInstalledCopy(folder));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private sealed class Serve(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }

    private static UpdatePackage PackageFor(byte[] body, string? sha) =>
        new(new Uri("https://github.com/burakuzun-dot/zaplio/releases/download/v9.0.0/Zaplio-9.0.0-setup.exe"), body.Length, sha);

    [Fact]
    public async Task AMatchingDownloadIsKept()
    {
        var body = RandomNumberGenerator.GetBytes(300_000);
        var folder = Directory.CreateTempSubdirectory("zaplio-indir-").FullName;
        try
        {
            var reported = new List<double>();
            var path = await UpdateInstaller.DownloadAsync(new HttpClient(new Serve(body)),
                PackageFor(body, Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()), folder, new SyncProgress(reported.Add), default);

            Assert.Equal(body, await File.ReadAllBytesAsync(path));
            Assert.Equal(1.0, reported[^1]);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task ADownloadWithTheWrongHashIsDeletedAndNeverRun()
    {
        var body = RandomNumberGenerator.GetBytes(50_000);
        var folder = Directory.CreateTempSubdirectory("zaplio-indir-").FullName;
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                UpdateInstaller.DownloadAsync(new HttpClient(new Serve(body)), PackageFor(body, new string('0', 64)), folder, null, default));
            Assert.Empty(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void ASizeThatDoesNotMatchIsRefused()
    {
        var package = PackageFor(new byte[10], null);
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Verify(9, "", package));
        UpdateInstaller.Verify(10, "whatever", package); // no hash listed: the size must still match
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
