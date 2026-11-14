using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class AccountInfoTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static AccountInfo Parse(string json) => AccountInfo.Parse(JsonDocument.Parse(json).RootElement, Now);

    [Fact]
    public void Reads_expiry_and_connections_from_strings_or_numbers()
    {
        var expires = Now.AddDays(28).AddHours(3).ToUnixTimeSeconds();
        var account = Parse($$"""{"exp_date":"{{expires}}","max_connections":"2","active_cons":1,"is_trial":"0","status":"Active"}""");

        Assert.Equal(28, account.DaysLeft(Now));
        Assert.Equal("28 gün kaldı · 1/2 bağlantı", account.CardLine(Now));
        Assert.False(account.ExpiresSoon(Now));
        Assert.Null(account.Warning("ste", Now));
    }

    [Fact]
    public void Unlimited_accounts_have_no_expiry()
    {
        var account = Parse("""{"exp_date":null,"max_connections":"1","active_cons":"0"}""");
        Assert.Null(account.DaysLeft(Now));
        Assert.Equal("Süresiz · 0/1 bağlantı", account.CardLine(Now));
    }

    [Fact]
    public void Warns_in_the_last_week_and_after_expiry()
    {
        var soon = Parse($$"""{"exp_date":"{{Now.AddDays(3).AddHours(1).ToUnixTimeSeconds()}}"}""");
        Assert.True(soon.ExpiresSoon(Now));
        Assert.Equal("ste aboneliğinin bitmesine 3 gün kaldı.", soon.Warning("ste", Now));

        var today = Parse($$"""{"exp_date":"{{Now.AddHours(5).ToUnixTimeSeconds()}}"}""");
        Assert.Equal("Bugün bitiyor", today.CardLine(Now));

        var expired = Parse($$"""{"exp_date":"{{Now.AddDays(-1).ToUnixTimeSeconds()}}"}""");
        Assert.True(expired.IsExpired(Now));
        Assert.Equal("Abonelik bitti", expired.CardLine(Now));
        Assert.Equal("ste aboneliğinin süresi doldu.", expired.Warning("ste", Now));
    }
}
