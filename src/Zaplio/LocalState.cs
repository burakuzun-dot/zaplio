using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Zaplio;

internal sealed class LocalState
{
    public List<Source> Sources { get; init; } = [];
    public HashSet<string> Favorites { get; init; } = [];
    public Dictionary<string, long> WatchPositions { get; init; } = [];
    public List<WatchEntry> WatchHistory { get; init; } = [];
    public HashSet<string> CompletedItems { get; init; } = [];
    /// <summary>Hidden channel groups, keyed as "{sourceId:N}:{group name}".</summary>
    public HashSet<string> HiddenGroups { get; init; } = [];
    /// <summary>User-chosen channel names keyed by channel id.</summary>
    public Dictionary<string, string> ChannelNames { get; init; } = [];
    /// <summary>Manual order of favourite channel ids; favourites missing here keep their list order.</summary>
    public List<string> FavoriteOrder { get; init; } = [];
    /// <summary>Quality variant the user picked for a channel, keyed as "{sourceId:N}:{variant key}" → channel id.</summary>
    public Dictionary<string, string> PreferredVariants { get; init; } = [];
    /// <summary>Fastest download seen (bits/s); lets automatic quality start from a sensible limit.</summary>
    public double PeakDownloadBps { get; set; }
    public Guid? LastSourceId { get; set; }
    public PlaybackSettings Settings { get; set; } = new();
    public int Volume { get; set; } = 75;
}

internal sealed record WatchEntry(Channel Channel, DateTimeOffset WatchedAt);

internal sealed record PlaybackSettings
{
    public LibrarySection StartupSection { get; init; } = LibrarySection.Live;
    public bool RememberSource { get; init; } = true;
    public bool AutoPlayChannel { get; init; } = true;
    public bool ResumeOnDemand { get; init; } = true;
    public bool AutoReconnect { get; init; } = true;
    public bool CheckForUpdates { get; init; } = true;
    public bool ShowLogos { get; init; } = true;
    public bool ShowIntro { get; init; } = true;
    public bool AmbientLight { get; init; } = true;
    public bool LegalNoticeAccepted { get; init; }
    public bool MergeQualities { get; init; } = true;
    public QualityMode QualityMode { get; init; } = QualityMode.Auto;
}

internal sealed class LocalStateStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zaplio", "settings.dat");

    public LocalState Load()
    {
        if (!File.Exists(_path)) return new LocalState();
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<LocalState>(bytes) ?? throw new JsonException();
    }

    public void Save(LocalState state)
    {
        var encrypted = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(state), null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
