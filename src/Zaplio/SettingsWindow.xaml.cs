using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Zaplio;

internal enum SettingsPage { Sources, Playback, Look, General, About }

public partial class SettingsWindow : Window
{
    private sealed record StartupChoice(LibrarySection Section, string Label);
    private sealed record QualityChoice(QualityMode Mode, string Label);
    private sealed record SourceRow(Guid Id, string Name, string TypeLabel, string Icon, bool IsActive);
    private sealed record ThemeChoice(AppTheme Mode, string Label);
    private sealed record AccentSwatch(string Key, string Name, System.Windows.Media.Brush Swatch, bool IsSelected);
    private string _accent = "purple";
    /// <summary>Set once the look was tried out here; only then does closing without Kaydet put the saved look back.</summary>
    private bool _lookPreviewed;

    private readonly PlaybackSettings _original;
    private readonly Func<IReadOnlyList<Source>> _sources;
    private readonly Action<Window> _manageSources;
    private readonly SourceSettingsHost? _host;
    private bool _syncingSources;

    /// <param name="sources">Current sources; read again after the sources window closes.</param>
    /// <param name="manageSources">Opens the sources window over this one (changes apply at once, not on Kaydet).</param>
    /// <param name="host">The main window's side of the active source, refresh and subscription; without it the list is read-only.</param>
    internal SettingsWindow(PlaybackSettings settings, int volume, Func<IReadOnlyList<Source>> sources, Action<Window> manageSources,
        SettingsPage page = SettingsPage.Sources, SourceSettingsHost? host = null)
    {
        InitializeComponent();
        _original = settings; // keeps values the window does not show (e.g. the accepted legal notice)
        _sources = sources;
        _manageSources = manageSources;
        _host = host;
        if (host is not null)
        {
            host.Changed += UpdateSources;
            Closed += (_, _) => host.Changed -= UpdateSources;
        }

        StartupChoice[] choices = [new(LibrarySection.Live, "Canlı TV"), new(LibrarySection.Movies, "Filmler"), new(LibrarySection.Series, "Diziler")];
        StartupPicker.ItemsSource = choices;
        StartupPicker.SelectedItem = choices.FirstOrDefault(choice => choice.Section == settings.StartupSection) ?? choices[0];
        RememberSource.IsChecked = settings.RememberSource;
        AutoPlayChannel.IsChecked = settings.AutoPlayChannel;
        ResumeOnDemand.IsChecked = settings.ResumeOnDemand;
        AutoReconnect.IsChecked = settings.AutoReconnect;
        RecordReady.IsChecked = settings.RecordReady;
        ThemeChoice[] themes = [new(AppTheme.Dark, "Koyu"), new(AppTheme.Light, "Açık"), new(AppTheme.System, "Windows ile aynı")];
        ThemePicker.ItemsSource = themes;
        ThemePicker.SelectedItem = themes.FirstOrDefault(choice => choice.Mode == settings.Theme) ?? themes[0];
        _accent = ThemePalette.Accent(settings.Accent).Key;
        BackupRow.Visibility = host?.CurrentState is null || host.Restore is null ? Visibility.Collapsed : Visibility.Visible;
        ChildRow.Visibility = host?.EditChildProfile is null ? Visibility.Collapsed : Visibility.Visible;
        ShowChildStatus();
        ShowAccents();
        Closed += (_, _) =>
        {
            // Kaydet keeps what the window shows; Vazgeç (or closing) puts the saved look back.
            if (DialogResult != true && _lookPreviewed) ThemeManager.Apply(Application.Current.Resources, _original.Theme, _original.Accent);
        };
        CheckForUpdates.IsChecked = settings.CheckForUpdates;
        ShowLogos.IsChecked = settings.ShowLogos;
        ShowIntro.IsChecked = settings.ShowIntro;
        AmbientLight.IsChecked = settings.AmbientLight;
        MergeQualities.IsChecked = settings.MergeQualities;
        QualityChoice[] qualities =
            [new(QualityMode.Auto, "Otomatik"), new(QualityMode.Highest, "En yüksek"), new(QualityMode.Saver, "Veri tasarrufu")];
        QualityPicker.ItemsSource = qualities;
        QualityPicker.SelectedItem = qualities.FirstOrDefault(choice => choice.Mode == settings.QualityMode) ?? qualities[0];
        SoundSlider.Value = Math.Clamp(volume, 0, 100);
        VersionText.Text = $"Zaplio {UpdateChecker.CurrentVersion}";
        UpdateSources();

        (page switch
        {
            SettingsPage.Playback => PlaybackNav,
            SettingsPage.Look => LookNav,
            SettingsPage.General => GeneralNav,
            SettingsPage.About => AboutNav,
            _ => SourcesNav
        }).IsChecked = true;
    }

    internal PlaybackSettings Settings => _original with
    {
        StartupSection = ((StartupChoice)StartupPicker.SelectedItem).Section,
        RememberSource = RememberSource.IsChecked == true,
        AutoPlayChannel = AutoPlayChannel.IsChecked == true,
        ResumeOnDemand = ResumeOnDemand.IsChecked == true,
        AutoReconnect = AutoReconnect.IsChecked == true,
        RecordReady = RecordReady.IsChecked == true,
        Theme = ((ThemeChoice)ThemePicker.SelectedItem).Mode,
        Accent = _accent,
        CheckForUpdates = CheckForUpdates.IsChecked == true,
        ShowLogos = ShowLogos.IsChecked == true,
        ShowIntro = ShowIntro.IsChecked == true,
        AmbientLight = AmbientLight.IsChecked == true,
        MergeQualities = MergeQualities.IsChecked == true,
        QualityMode = ((QualityChoice)QualityPicker.SelectedItem).Mode
    };

    internal int Volume => (int)SoundSlider.Value;

    private void OnPageChanged(object sender, RoutedEventArgs e)
    {
        if (SourcesPage is null) return; // InitializeComponent is still running
        SourcesPage.Visibility = sender == SourcesNav ? Visibility.Visible : Visibility.Collapsed;
        PlaybackPage.Visibility = sender == PlaybackNav ? Visibility.Visible : Visibility.Collapsed;
        LookPage.Visibility = sender == LookNav ? Visibility.Visible : Visibility.Collapsed;
        GeneralPage.Visibility = sender == GeneralNav ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = sender == AboutNav ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.ScrollToTop();
    }

    private LocalState? _pendingRestore;

    private void ShowChildStatus()
    {
        ChildStatusText.Text = _host?.ChildStatus?.Invoke() ?? "";
        ChildButton.IsEnabled = _host?.ActiveSourceId() is not null;
        ChildButton.ToolTip = ChildButton.IsEnabled ? null : "Önce bir kaynak seç.";
    }

    private async void OnChildProfile(object sender, RoutedEventArgs e)
    {
        if (_host?.EditChildProfile is not { } edit) return;
        ChildButton.IsEnabled = false;
        try { await edit(this); }
        finally { ShowChildStatus(); }
    }

    private async void OnBackup(object sender, RoutedEventArgs e)
    {
        if (_host?.CurrentState is not { } current) return;
        var prompt = new PasswordPromptWindow("Yedek parolası",
            "Bu parolayı unutma: yedek onsuz açılamaz ve parola hiçbir yerde saklanmaz.", "Yedek oluştur", confirm: true,
            minimum: BackupFile.MinimumPassword) { Owner = this };
        if (prompt.ShowDialog() != true) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Yedeği kaydet", Filter = $"Zaplio yedeği|*{BackupFile.Extension}",
            FileName = $"Zaplio-yedek-{DateTime.Now:yyyyMMdd}{BackupFile.Extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != true) return;
        ShowBackupStatus("Yedek hazırlanıyor…", error: false);
        var password = prompt.Value;
        var state = current();
        try
        {
            var bytes = await Task.Run(() => BackupFile.Create(state, password));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            ShowBackupStatus($"Yedek kaydedildi: {Path.GetFileName(dialog.FileName)} ({BackupFile.Summary(state)}).", error: false);
            AppLog.Info("Yedek oluşturuldu.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BackupException)
        {
            ShowBackupStatus(exception is BackupException ? exception.Message : "Yedek kaydedilemedi.", error: true);
        }
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Yedeği seç", Filter = $"Zaplio yedeği|*{BackupFile.Extension}" };
        if (dialog.ShowDialog(this) != true) return;
        var prompt = new PasswordPromptWindow("Yedeği aç", $"{Path.GetFileName(dialog.FileName)} dosyasının parolası.", "Aç", confirm: false)
            { Owner = this };
        if (prompt.ShowDialog() != true) return;
        var password = prompt.Value;
        ShowBackupStatus("Yedek açılıyor…", error: false);
        try
        {
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            _pendingRestore = await Task.Run(() => BackupFile.Open(bytes, password));
            BackupStatus.Visibility = Visibility.Collapsed;
            RestoreSummary.Text = $"Yedekte: {BackupFile.Summary(_pendingRestore)}.";
            RestoreConfirm.Visibility = Visibility.Visible;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BackupException)
        {
            _pendingRestore = null;
            RestoreConfirm.Visibility = Visibility.Collapsed;
            ShowBackupStatus(exception is BackupException ? exception.Message : "Yedek dosyası okunamadı.", error: true);
        }
    }

    private void OnRestoreConfirmed(object sender, RoutedEventArgs e)
    {
        if (_pendingRestore is not { } restored || _host?.Restore is not { } restore) return;
        AppLog.Info("Yedek geri yükleniyor.");
        restore(restored);
    }

    private void ShowBackupStatus(string text, bool error)
    {
        BackupStatus.Text = text;
        BackupStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, error ? "Error" : "Muted");
        BackupStatus.Visibility = Visibility.Visible;
    }

    private void ShowAccents() => AccentPicker.ItemsSource = ThemePalette.Accents
        .Select(accent => new AccentSwatch(accent.Key, accent.Name, Freeze(new System.Windows.Media.SolidColorBrush(accent.Strong)), accent.Key == _accent))
        .ToArray();

    private static System.Windows.Media.Brush Freeze(System.Windows.Media.SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void OnAccentPicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AccentSwatch swatch || swatch.Key == _accent) return;
        _accent = swatch.Key;
        OnLookPreview(sender, e);
    }

    /// <summary>The theme and accent show at once, in this window and behind it; Vazgeç takes them back.</summary>
    private void OnLookPreview(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || ThemePicker?.SelectedItem is not ThemeChoice theme) return; // filling the picker on open is not a choice
        _lookPreviewed = true;
        ThemeManager.Apply(Application.Current.Resources, theme.Mode, _accent);
    }

    private void UpdateSources()
    {
        var sources = _sources();
        var active = _host?.ActiveSourceId();
        var rows = sources.Select(source => new SourceRow(source.Id, source.Name, source.TypeLabel, source.Icon, source.Id == active)).ToArray();
        _syncingSources = true;
        SourceList.ItemsSource = rows;
        SourceList.SelectedItem = rows.FirstOrDefault(row => row.IsActive);
        SourceList.IsHitTestVisible = _host is not null;
        _syncingSources = false;
        NoSources.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ManageSourcesText.Text = sources.Count == 0 ? "Kaynak ekle" : "Kaynakları yönet";
        ManageSourcesIcon.Text = sources.Count == 0 ? IconGlyphs.Add : IconGlyphs.Plug;

        RefreshListsButton.Visibility = RefreshHint.Visibility = _host is null || sources.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshListsButton.IsEnabled = _host?.CanRefresh() == true;

        var account = _host?.Account();
        AccountPanel.Visibility = account is null ? Visibility.Collapsed : Visibility.Visible;
        AccountText.Text = account?.Text;
        AccountText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, account?.BrushKey ?? "Muted");
        AccountPanel.ToolTip = account?.Details;
        CheckAccountButton.IsEnabled = account?.CanCheck == true;
    }

    private async void OnSourceSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_syncingSources || _host is null || SourceList.SelectedItem is not SourceRow row || row.Id == _host.ActiveSourceId()) return;
        await _host.Activate(row.Id);
        UpdateSources();
    }

    private async void OnRefreshLists(object sender, RoutedEventArgs e)
    {
        if (_host is null || !_host.CanRefresh()) return;
        var refresh = _host.Refresh();
        UpdateSources(); // the button turns off while the lists load
        await refresh;
        if (IsLoaded) UpdateSources();
    }

    private async void OnCheckAccount(object sender, RoutedEventArgs e)
    {
        if (_host is null) return;
        await _host.CheckAccount();
        if (IsLoaded) UpdateSources();
    }

    private void OnManageSources(object sender, RoutedEventArgs e)
    {
        _manageSources(this);
        UpdateSources();
    }

    private void OnClearCache(object sender, RoutedEventArgs e)
    {
        var removed = CatalogCache.Clear();
        MaintenanceStatus.Text = removed ? "Önbellek temizlendi. Listeler bir sonraki yüklemede sunucudan alınır."
            : "Önbellek tamamen temizlenemedi; bazı dosyalar kullanımda olabilir.";
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLog.Folder);
            Process.Start(new ProcessStartInfo(AppLog.Folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            MaintenanceStatus.Text = $"Klasör açılamadı: {AppLog.Folder}";
        }
    }

    private void OnLegalNotice(object sender, RoutedEventArgs e) => new LegalNoticeWindow(requireAcceptance: false) { Owner = this }.ShowDialog();

    private void OnShortcuts(object sender, RoutedEventArgs e) => new ShortcutsWindow { Owner = this }.ShowDialog();

    private void OnProjectPage(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateChecker.ProjectPage) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            MaintenanceStatus.Text = $"Tarayıcı açılamadı: {UpdateChecker.ProjectPage}";
        }
    }

    private void OnLicenses(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Licenses", "ThirdPartyNotices.txt")) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            MaintenanceStatus.Text = "Lisans belgesi açılamadı. Uygulama klasöründeki Licenses bölümüne bak.";
        }
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
