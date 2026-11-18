using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zaplio;

/// <summary>A backup could not be opened; the message is shown to the user as it is.</summary>
internal sealed class BackupException(string message) : Exception(message);

/// <summary>
/// "Yedekle / taşı": everything Zaplio keeps (sources with their passwords, favourites, lists, positions, history, settings)
/// in one file protected by a password the user chooses, to move to another computer or keep safe.
/// The password is stretched with PBKDF2-SHA256 (600 000 rounds) and the content sealed with AES-256-GCM, so a wrong password
/// or a single changed byte is refused. The password itself is never stored.
/// </summary>
internal static class BackupFile
{
    internal const string Extension = ".zaplio";
    internal const int MinimumPassword = 8;
    private const int Iterations = 600_000;
    private static readonly byte[] Magic = "ZAPLIO-YEDEK-1\n"u8.ToArray();

    private sealed record Content(int Format, DateTimeOffset CreatedAt, string AppVersion, LocalState State);

    internal static byte[] Create(LocalState state, string password)
    {
        if (password.Length < MinimumPassword) throw new BackupException(Lang.F($"Parola en az {MinimumPassword} karakter olmalı."));
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Content(1, DateTimeOffset.UtcNow, UpdateChecker.CurrentVersion.ToString(), state));
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Key(password, salt);
        var sealedBytes = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, tag.Length)) aes.Encrypt(nonce, plain, sealedBytes, tag, Magic);
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plain);
        return [.. Magic, .. salt, .. nonce, .. tag, .. sealedBytes];
    }

    internal static LocalState Open(byte[] file, string password)
    {
        var header = Magic.Length + 16 + 12 + 16;
        if (file.Length < header || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new BackupException(Lang.T("Bu dosya bir Zaplio yedeği değil."));
        var salt = file.AsSpan(Magic.Length, 16);
        var nonce = file.AsSpan(Magic.Length + 16, 12);
        var tag = file.AsSpan(Magic.Length + 28, 16);
        var sealedBytes = file.AsSpan(header);
        var plain = new byte[sealedBytes.Length];
        var key = Key(password, salt.ToArray());
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, sealedBytes, tag, plain, Magic);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new BackupException(Lang.T("Parola yanlış ya da dosya bozulmuş."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        try
        {
            var content = JsonSerializer.Deserialize<Content>(plain) ?? throw new BackupException(Lang.T("Yedeğin içeriği okunamadı."));
            if (content.Format != 1) throw new BackupException(Lang.T("Bu yedek Zaplio'nun daha yeni bir sürümüyle alınmış; önce Zaplio'yu güncelle."));
            return content.State;
        }
        catch (JsonException)
        {
            throw new BackupException(Lang.T("Yedeğin içeriği okunamadı."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>What a backup holds, in one line ("2 kaynak · 14 kanal favorisi · …").</summary>
    internal static string Summary(LocalState state)
    {
        var parts = new List<string> { Lang.F($"{state.Sources.Count} kaynak") };
        if (state.Favorites.Count > 0) parts.Add(Lang.F($"{state.Favorites.Count} kanal favorisi"));
        if (state.VodFavorites.Count > 0) parts.Add(Lang.F($"{state.VodFavorites.Count} film/dizi favorisi"));
        if (state.VodWatchlist.Count > 0) parts.Add($"{state.VodWatchlist.Count} izleme listesi");
        if (state.WatchPositions.Count > 0) parts.Add(Lang.F($"{state.WatchPositions.Count} kaldığın yer"));
        if (state.WatchHistory.Count > 0) parts.Add(Lang.F($"{state.WatchHistory.Count} izleme geçmişi"));
        if (state.Child.HasPin) parts.Add(state.Child.Active ? Lang.T("çocuk profili (açık)") : Lang.T("çocuk profili"));
        return string.Join(" · ", parts);
    }

    private static byte[] Key(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
}
