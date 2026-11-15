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

    private readonly PlaybackSettings _original;
    private readonly Func<IReadOnlyList<Source>> _sources;
    private readonly Action<Window> _manageSources;

    /// <param name="sources">Current sources; read again after the sources window closes.</param>
    /// <param name="manageSources">Opens the sources window over this one (changes apply at once, not on Kaydet).</param>
    internal SettingsWindow(PlaybackSettings settings, int volume, Func<IReadOnlyList<Source>> sources, Action<Window> manageSources,
        SettingsPage page = SettingsPage.Sources)
    {
        InitializeComponent();
        _original = settings; // keeps values the window does not show (e.g. the accepted legal notice)
        _sources = sources;
        _manageSources = manageSources;

        StartupChoice[] choices = [new(LibrarySection.Live, "Canlı TV"), new(LibrarySection.Movies, "Filmler"), new(LibrarySection.Series, "Diziler")];
        StartupPicker.ItemsSource = choices;
        StartupPicker.SelectedItem = choices.FirstOrDefault(choice => choice.Section == settings.StartupSection) ?? choices[0];
        RememberSource.IsChecked = settings.RememberSource;
        AutoPlayChannel.IsChecked = settings.AutoPlayChannel;
        ResumeOnDemand.IsChecked = settings.ResumeOnDemand;
        AutoReconnect.IsChecked = settings.AutoReconnect;
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

    private void UpdateSources()
    {
        var sources = _sources();
        SourceList.ItemsSource = sources;
        NoSources.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EditSourcesButton.Visibility = sources.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
