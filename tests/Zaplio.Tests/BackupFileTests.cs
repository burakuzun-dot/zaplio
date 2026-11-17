using System.Text;
using Xunit;

namespace Zaplio.Tests;

public sealed class BackupFileTests
{
    private static LocalState Sample()
    {
        var state = new LocalState();
        state.Sources.Add(new Source(Guid.NewGuid(), "Ev listesi", SourceKind.Xtream, "http://ornek.test:8080", "kullanici", "gizli-sifre"));
        state.Favorites.Add("kanal-1");
        state.VodWatchlist.Add("film-1");
        state.WatchPositions["film-1"] = 42_000;
        return state;
    }

    [Fact]
    public void ABackupComesBackWithEverythingInIt()
    {
        var file = BackupFile.Create(Sample(), "uzun-bir-parola");
        var restored = BackupFile.Open(file, "uzun-bir-parola");

        Assert.Equal("Ev listesi", restored.Sources[0].Name);
        Assert.Equal("gizli-sifre", restored.Sources[0].Password);
        Assert.Contains("kanal-1", restored.Favorites);
        Assert.Equal(42_000, restored.WatchPositions["film-1"]);
    }

    [Fact]
    public void NothingReadableIsInTheFile()
    {
        var file = BackupFile.Create(Sample(), "uzun-bir-parola");
        var text = Encoding.UTF8.GetString(file);
        Assert.DoesNotContain("gizli-sifre", text);
        Assert.DoesNotContain("kullanici", text);
        Assert.DoesNotContain("Ev listesi", text);
    }

    [Fact]
    public void AWrongPasswordIsRefused()
    {
        var file = BackupFile.Create(Sample(), "uzun-bir-parola");
        var error = Assert.Throws<BackupException>(() => BackupFile.Open(file, "baska-bir-parola"));
        Assert.Contains("Parola yanlış", error.Message);
    }

    [Fact]
    public void ASingleChangedByteIsRefused()
    {
        var file = BackupFile.Create(Sample(), "uzun-bir-parola");
        file[^5] ^= 0x01;
        Assert.Throws<BackupException>(() => BackupFile.Open(file, "uzun-bir-parola"));
    }

    [Fact]
    public void AnotherFileIsNotTakenForABackup()
    {
        var error = Assert.Throws<BackupException>(() => BackupFile.Open(Encoding.UTF8.GetBytes("#EXTM3U\n"), "uzun-bir-parola"));
        Assert.Contains("Zaplio yedeği değil", error.Message);
    }

    [Fact]
    public void AShortPasswordIsNotAccepted() =>
        Assert.Throws<BackupException>(() => BackupFile.Create(Sample(), "kisa"));

    [Fact]
    public void TheSameStateNeverGivesTheSameFile() =>
        Assert.NotEqual(BackupFile.Create(Sample(), "uzun-bir-parola"), BackupFile.Create(Sample(), "uzun-bir-parola"));

    [Fact]
    public void TheSummaryCountsWhatMatters() =>
        Assert.Equal("1 kaynak · 1 kanal favorisi · 1 izleme listesi · 1 kaldığın yer", BackupFile.Summary(Sample()));
}
