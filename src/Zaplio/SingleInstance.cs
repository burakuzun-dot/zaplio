using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Zaplio;

/// <summary>
/// One Zaplio per data folder: two windows writing the same settings file could undo each other's changes. A second launch
/// (for example "Open with" in Windows while Zaplio is open) hands its files to the running one over a named pipe that only the
/// same Windows user can open, brings that window forward and exits. The name comes from the data folder, so the self test
/// (which uses its own folder) never meets the user's Zaplio.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const int MaxMessage = 64 * 1024;
    private readonly Mutex _mutex;
    private readonly string _pipe;

    internal bool IsFirst { get; }

    internal SingleInstance(string dataFolder)
    {
        var name = NameFor(dataFolder, WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName);
        _pipe = name;
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out var createdNew);
        IsFirst = createdNew;
    }

    /// <summary>A stable name for one data folder of one Windows user (no path or user name in it).</summary>
    internal static string NameFor(string dataFolder, string user)
    {
        var key = Path.GetFullPath(dataFolder).TrimEnd('\\', '/').ToUpperInvariant() + "|" + user;
        return "Zaplio-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..20].ToLowerInvariant();
    }

    /// <summary>Second launch: lets the running Zaplio come to the front and gives it the files. False if it could not be reached.</summary>
    internal bool SendToFirst(IReadOnlyList<string> paths, int timeoutMs = 3000)
    {
        AllowSetForegroundWindow(-1); // ASFW_ANY: the window that receives the files may come forward
        try
        {
            using var client = new NamedPipeClientStream(".", _pipe, PipeDirection.Out);
            client.Connect(timeoutMs);
            var message = Encode(paths);
            client.Write(BitConverter.GetBytes(message.Length));
            client.Write(message);
            client.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            AppLog.Warn("Açık Zaplio'ya ulaşılamadı.", exception);
            return false;
        }
    }

    /// <summary>First launch: waits for later launches and passes their files (possibly none) to <paramref name="received"/>.</summary>
    internal async Task ListenAsync(Action<IReadOnlyList<string>> received, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = CreateServer();
                await server.WaitForConnectionAsync(token);
                var lengthBytes = new byte[4];
                await server.ReadExactlyAsync(lengthBytes, token);
                var length = BitConverter.ToInt32(lengthBytes);
                if (length is < 0 or > MaxMessage) continue;
                var message = new byte[length];
                await server.ReadExactlyAsync(message, token);
                received(Decode(message));
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) when (exception is IOException or JsonException or EndOfStreamException)
            {
                AppLog.Warn("İkinci açılıştan gelen ileti okunamadı.", exception);
            }
        }
    }

    /// <summary>A pipe that only the current Windows user can open.</summary>
    private NamedPipeServerStream CreateServer()
    {
        var security = new PipeSecurity();
        if (WindowsIdentity.GetCurrent().User is { } user)
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(_pipe, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
            MaxMessage, MaxMessage, security);
    }

    internal static byte[] Encode(IReadOnlyList<string> paths) => JsonSerializer.SerializeToUtf8Bytes(paths);

    internal static IReadOnlyList<string> Decode(byte[] message) =>
        JsonSerializer.Deserialize<string[]>(message)?.Where(path => !string.IsNullOrWhiteSpace(path)).Take(500).ToArray() ?? [];

    public void Dispose()
    {
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { } // released on another thread already
        }
        _mutex.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
