using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Zaplio;

public partial class SettingsWindow : Window
{
    private sealed record StartupChoice(LibrarySection Section, string Label);
    private sealed record QualityChoice(QualityMode Mode, string Label);

    private readonly PlaybackSettings _original;

    internal SettingsWindow(PlaybackSettings settings, int volume)
    {
        InitializeComponent();
        _original = settings; // keeps values the window does not show (e.g. the accepted legal notice)
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
        MergeQualities.IsChecked = settings.MergeQualities;
        QualityChoice[] qualities =
            [new(QualityMode.Auto, "Otomatik (bağlantıya göre)"), new(QualityMode.Highest, "En yüksek"), new(QualityMode.Saver, "Veri tasarrufu")];
        QualityPicker.ItemsSource = qualities;
        QualityPicker.SelectedItem = qualities.FirstOrDefault(choice => choice.Mode == settings.QualityMode) ?? qualities[0];
        SoundSlider.Value = Math.Clamp(volume, 0, 100);
        VersionText.Text = $"Zaplio {UpdateChecker.CurrentVersion}";
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
        MergeQualities = MergeQualities.IsChecked == true,
        QualityMode = ((QualityChoice)QualityPicker.SelectedItem).Mode
    };

    internal int Volume => (int)SoundSlider.Value;

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

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
