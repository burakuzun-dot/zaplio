using System.Windows;
using System.Windows.Media.Imaging;

namespace Zaplio;

public partial class MediaDetailsWindow : Window
{
    internal MediaDetailsWindow(VodItem item, int seasonCount)
    {
        InitializeComponent();
        ActionLabel.Text = item.Playable is not null || seasonCount > 0 ? "Oynat" : "Bölümlere git";
        UpdateDetails(item, seasonCount);
    }

    internal void UpdateDetails(VodItem item, int seasonCount)
    {
        Heading.Text = item.Title;
        seasonCount = Math.Max(seasonCount, item.SeasonCount);
        Meta.Text = string.Join(" · ", new[] { item.Category, item.Detail, seasonCount > 0 ? $"{seasonCount} sezon" : "" }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        Synopsis.Text = string.IsNullOrWhiteSpace(item.Plot) ? "Bu içerik için özet bulunamadı." : item.Plot;
        if (Uri.TryCreate(item.Poster, UriKind.Absolute, out var address) && address.Scheme is "https" or "http" &&
            (Poster.Source as BitmapImage)?.UriSource != address)
        {
            try { Poster.Source = new BitmapImage(address); }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.IO.IOException) { }
        }
    }

    private void OnActivate(object sender, RoutedEventArgs e) => DialogResult = true;
}
