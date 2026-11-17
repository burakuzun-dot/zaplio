namespace Zaplio;

/// <summary>The subscription line under the active source in Settings › Kaynaklar.</summary>
/// <param name="BrushKey">Theme brush for the text: "Muted", "Warning" or "Error".</param>
/// <param name="CanCheck">Whether "Aboneliği kontrol et" asks the provider (Xtream sources only).</param>
internal sealed record AccountLine(string Text, string BrushKey, string? Details, bool CanCheck);

/// <summary>
/// What Settings › Kaynaklar needs from the main window to choose the active source, refresh its lists and show its
/// subscription. Everything applies at once, like adding or removing a source; Kaydet is not needed.
/// </summary>
internal sealed class SourceSettingsHost
{
    public required Func<Guid?> ActiveSourceId { get; init; }
    public required Func<Guid?, Task> Activate { get; init; }
    public required Func<bool> CanRefresh { get; init; }
    public required Func<Task> Refresh { get; init; }
    public required Func<AccountLine?> Account { get; init; }
    public required Func<Task> CheckAccount { get; init; }

    /// <summary>Everything Zaplio keeps, for a backup (null hides the backup buttons).</summary>
    public Func<LocalState>? CurrentState { get; init; }

    /// <summary>Puts a restored backup in place of everything and restarts Zaplio.</summary>
    public Action<LocalState>? Restore { get; init; }

    /// <summary>Opens the kids profile setup over the given window (null hides the row).</summary>
    public Func<System.Windows.Window, Task>? EditChildProfile { get; init; }

    /// <summary>One line on the kids profile for Settings › Genel.</summary>
    public Func<string>? ChildStatus { get; init; }

    /// <summary>Raised by the main window when the active source, its lists or its subscription changed.</summary>
    public event Action? Changed;

    internal void NotifyChanged() => Changed?.Invoke();
}
