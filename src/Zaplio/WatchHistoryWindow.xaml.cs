using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Zaplio;

public partial class WatchHistoryWindow : Window
{
    private sealed record HistoryRow(Channel Channel, string Caption, bool CanResume)
    {
        public string Title => Channel.Name;
        public override string ToString() => Title;
    }

    private readonly IReadOnlyList<HistoryRow> _rows;
    internal Channel? SelectedChannel { get; private set; }

    internal WatchHistoryWindow(string sourceName, IEnumerable<WatchEntry> entries, IReadOnlyDictionary<string, long> positions,
        bool resumeEnabled)
    {
        InitializeComponent();
        SourceLabel.Text = sourceName;
        _rows = entries.Select(entry =>
        {
            var position = resumeEnabled ? positions.GetValueOrDefault(entry.Channel.Id) : 0;
            var date = entry.WatchedAt.ToLocalTime().ToString("dd MMM yyyy · HH:mm", CultureInfo.GetCultureInfo("tr-TR"));
            var progress = position > 0 ? $" · {TimeSpan.FromMilliseconds(position):h\\:mm\\:ss} konumunda" : "";
            return new HistoryRow(entry.Channel, $"{date}{progress}", entry.Channel.IsOnDemand && position > 0);
        }).ToArray();
        HistoryTab.IsChecked = true;
    }

    private void OnFilter(object sender, RoutedEventArgs e)
    {
        var resume = ResumeTab.IsChecked == true;
        var rows = resume ? _rows.Where(row => row.CanResume).ToArray() : _rows;
        Entries.ItemsSource = rows;
        Entries.SelectedIndex = rows.Count > 0 ? 0 : -1;
        PlayHistoryButton.IsEnabled = rows.Count > 0;
        EmptyMessage.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyMessage.Text = resume ? "Devam edilecek film veya bölüm yok." : "Bu kaynakta izleme geçmişi henüz boş.";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayHistoryButton.IsEnabled = Entries.SelectedItem is HistoryRow;

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        if (Entries.SelectedItem is not HistoryRow row) return;
        SelectedChannel = row.Channel;
        DialogResult = true;
    }
}
