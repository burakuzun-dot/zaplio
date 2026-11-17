using Xunit;

namespace Zaplio.Tests;

public sealed class ChildProfileTests
{
    private static readonly Guid Source = Guid.NewGuid();

    private static Channel Live(string group) =>
        new("k1", Source, "Kanal A", group, new Uri("http://ornek.test/1.ts"));

    [Fact]
    public void NothingIsAllowedUntilAParentAllowsIt()
    {
        var profile = new ChildProfile();
        Assert.False(profile.AllowsChannel(Live("Çizgi film")));
        profile.SetAllowed(Source, LibrarySection.Live, ["Çizgi film"]);
        Assert.True(profile.AllowsChannel(Live("Çizgi film")));
        Assert.False(profile.AllowsChannel(Live("Haber")));
    }

    [Fact]
    public void AChannelInSeveralCategoriesNeedsOnlyOneAllowed()
    {
        var profile = new ChildProfile();
        profile.SetAllowed(Source, LibrarySection.Live, ["Belgesel"]);
        Assert.True(profile.AllowsChannel(Live("Haber;Belgesel")));
    }

    [Fact]
    public void SectionsAndSourcesAreSeparate()
    {
        var profile = new ChildProfile();
        profile.SetAllowed(Source, LibrarySection.Movies, ["Aile"]);
        Assert.True(profile.Allows(Source, LibrarySection.Movies, "Aile"));
        Assert.False(profile.Allows(Source, LibrarySection.Series, "Aile"));
        Assert.False(profile.Allows(Guid.NewGuid(), LibrarySection.Movies, "Aile"));

        profile.SetAllowed(Source, LibrarySection.Series, ["Animasyon"]);
        profile.SetAllowed(Source, LibrarySection.Movies, []);
        Assert.False(profile.Allows(Source, LibrarySection.Movies, "Aile"));
        Assert.Equal(["Animasyon"], profile.AllowedIn(Source, LibrarySection.Series));
    }

    [Fact]
    public void UncategorizedChannelsAreAllowedSeparately()
    {
        var profile = new ChildProfile();
        Assert.False(profile.AllowsChannel(Live("")));
        profile.SetAllowed(Source, LibrarySection.Live, [""]);
        Assert.True(profile.AllowsChannel(Live("")));
    }

    [Fact]
    public void MoviesAreJudgedByTheirCategoryAndEpisodesNotByTheirSeasonLabel()
    {
        var profile = new ChildProfile();
        profile.SetAllowed(Source, LibrarySection.Movies, ["Aile"]);
        var movie = new Channel("m1", Source, "Film 1", "Aile", new Uri("http://ornek.test/m.mp4"), IsOnDemand: true);
        var episode = movie with { Group = "Aile", SeriesId = 7, EpisodeId = 9 };
        Assert.True(profile.AllowsChannel(movie));
        Assert.False(profile.AllowsChannel(episode));
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("12345678", true)]
    [InlineData("123", false)]
    [InlineData("123456789", false)]
    [InlineData("12a4", false)]
    [InlineData("", false)]
    public void PinIsFourToEightDigits(string pin, bool valid) => Assert.Equal(valid, ChildProfile.IsValidPin(pin));

    [Fact]
    public void ThePinIsCheckedButNotStored()
    {
        var profile = new ChildProfile();
        Assert.False(profile.CheckPin("1234"));
        profile.SetPin("2468");
        Assert.True(profile.CheckPin("2468"));
        Assert.False(profile.CheckPin("2469"));
        Assert.DoesNotContain("2468", profile.PinHash);
        Assert.Throws<ArgumentException>(() => profile.SetPin("12"));
    }

    [Fact]
    public void TheProfileSurvivesSavingAndABackup()
    {
        var state = new LocalState();
        state.Child.SetPin("1357");
        state.Child.Active = true;
        state.Child.SetAllowed(Source, LibrarySection.Live, ["Çizgi film"]);

        var restored = BackupFile.Open(BackupFile.Create(state, "uzun-bir-parola"), "uzun-bir-parola");
        Assert.True(restored.Child.Active);
        Assert.True(restored.Child.CheckPin("1357"));
        Assert.True(restored.Child.Allows(Source, LibrarySection.Live, "Çizgi film"));
    }

    [Fact]
    public void WrongGuessesMakeTheNextTryWait()
    {
        var gate = new PinGate();
        var now = DateTimeOffset.UtcNow;
        gate.Failed(now);
        gate.Failed(now);
        Assert.Equal(TimeSpan.Zero, gate.Wait(now));
        gate.Failed(now);
        Assert.Equal(TimeSpan.FromSeconds(30), gate.Wait(now));
        gate.Failed(now.AddSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(60), gate.Wait(now.AddSeconds(30)));
        gate.Succeeded();
        Assert.Equal(TimeSpan.Zero, gate.Wait(now));
    }
}
