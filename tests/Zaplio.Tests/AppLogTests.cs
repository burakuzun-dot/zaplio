using Xunit;

namespace Zaplio.Tests;

public sealed class AppLogTests
{
    [Theory]
    [InlineData("http://h:8080/live/user1/secret/123.ts", "http://***/live/***/***/123.ts")]
    [InlineData("http://h/movie/user1/secret/9.mkv", "http://***/movie/***/***/9.mkv")]
    [InlineData("http://h/timeshift/user1/secret/65/2026-11-13:18-00/1.ts", "http://***/timeshift/***/***/65/2026-11-13:18-00/1.ts")]
    [InlineData("player_api.php?username=abc&password=xyz&action=get_vod", "player_api.php?username=***&password=***&action=get_vod")]
    [InlineData("http://user:pw@host/list.m3u", "http://***@***/list.m3u")]
    [InlineData("Hata (https://ornek.test:8443/get.php?username=a&password=b)", "Hata (https://***/get.php?username=***&password=***)")]
    [InlineData("rtsp://10.0.0.5:554/kanal", "rtsp://***/kanal")]
    [InlineData("http://127.0.0.1:53111/abc.ts", "http://127.0.0.1:53111/abc.ts")]
    [InlineData("http://localhost/x", "http://localhost/x")]
    public void Masks_account_details(string input, string expected) => Assert.Equal(expected, AppLog.Redact(input));

    [Fact]
    public void Leaves_ordinary_text_alone() => Assert.Equal("Yayın başladı: Kanal A · 415 ms", AppLog.Redact("Yayın başladı: Kanal A · 415 ms"));
}
