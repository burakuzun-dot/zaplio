using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Zaplio;

/// <summary>
/// The kids profile: while it is on, only the categories a parent allowed are listed (live, movies and series alike; a new
/// provider category stays hidden until it is allowed), and Settings, sources and leaving the profile ask for the PIN.
/// </summary>
internal sealed class ChildProfile
{
    public const int PinMinimum = 4;
    public const int PinMaximum = 8;
    /// <summary>Label shown for channels the provider put in no category.</summary>
    public const string Uncategorized = "Kategorisiz";
    private const int Iterations = 100_000;

    public bool Active { get; set; }
    public string? PinHash { get; set; }
    public string? PinSalt { get; set; }
    /// <summary>Allowed categories, keyed "{sourceId:N}|{l|m|s}|{category}" (see <see cref="Key"/>).</summary>
    public HashSet<string> Allowed { get; set; } = [];

    [JsonIgnore]
    public bool HasPin => !string.IsNullOrEmpty(PinHash) && !string.IsNullOrEmpty(PinSalt);

    public static string Key(Guid source, LibrarySection section, string category) =>
        $"{source:N}|{Letter(section)}|{category.Trim()}";

    private static char Letter(LibrarySection section) => section switch
    {
        LibrarySection.Movies => 'm',
        LibrarySection.Series => 's',
        _ => 'l'
    };

    /// <summary>Whether something filed under any of <paramref name="categories"/> may be shown (none means "Kategorisiz").</summary>
    public bool Allows(Guid source, LibrarySection section, IReadOnlyCollection<string> categories) =>
        categories.Count == 0
            ? Allowed.Contains(Key(source, section, ""))
            : categories.Any(category => Allowed.Contains(Key(source, section, category)));

    public bool Allows(Guid source, LibrarySection section, string category) => Allowed.Contains(Key(source, section, category));

    /// <summary>A live channel (any of its categories) or a movie (its category). Episodes are judged by their series.</summary>
    public bool AllowsChannel(Channel channel) => channel.IsOnDemand
        ? channel.SeriesId == 0 && Allows(channel.SourceId, LibrarySection.Movies, channel.Group)
        : Allows(channel.SourceId, LibrarySection.Live, channel.Categories);

    /// <summary>Allowed categories of one section of one source.</summary>
    public IEnumerable<string> AllowedIn(Guid source, LibrarySection section)
    {
        var prefix = Key(source, section, "");
        return Allowed.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..]);
    }

    /// <summary>Replaces what is allowed in one section of one source; other sections and sources keep theirs.</summary>
    public void SetAllowed(Guid source, LibrarySection section, IEnumerable<string> categories)
    {
        var prefix = Key(source, section, "");
        Allowed.RemoveWhere(key => key.StartsWith(prefix, StringComparison.Ordinal));
        foreach (var category in categories) Allowed.Add(Key(source, section, category));
    }

    public static bool IsValidPin(string pin) =>
        pin.Length is >= PinMinimum and <= PinMaximum && pin.All(char.IsAsciiDigit);

    public void SetPin(string pin)
    {
        if (!IsValidPin(pin)) throw new ArgumentException(Lang.T("PIN 4–8 rakam olmalı."), nameof(pin));
        var salt = RandomNumberGenerator.GetBytes(16);
        PinSalt = Convert.ToBase64String(salt);
        PinHash = Convert.ToBase64String(Hash(pin, salt));
    }

    public bool CheckPin(string pin)
    {
        if (!HasPin) return false;
        try
        {
            var expected = Convert.FromBase64String(PinHash!);
            return CryptographicOperations.FixedTimeEquals(expected, Hash(pin, Convert.FromBase64String(PinSalt!)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Hash(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, 32);
}

/// <summary>Slows down PIN guessing: after three wrong tries each further try waits, longer every time (up to ten minutes).</summary>
internal sealed class PinGate
{
    private int _failures;
    private DateTimeOffset _openAt = DateTimeOffset.MinValue;

    /// <summary>How long until the next try is accepted; zero when it is accepted now.</summary>
    public TimeSpan Wait(DateTimeOffset now) => _openAt > now ? _openAt - now : TimeSpan.Zero;

    public void Failed(DateTimeOffset now)
    {
        _failures++;
        if (_failures < 3) return;
        var seconds = Math.Min(600, 30 * Math.Pow(2, _failures - 3));
        _openAt = now.AddSeconds(seconds);
    }

    public void Succeeded()
    {
        _failures = 0;
        _openAt = DateTimeOffset.MinValue;
    }
}
