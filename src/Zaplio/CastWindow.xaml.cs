using System.Windows;
using System.Windows.Input;

namespace Zaplio;

/// <summary>Searches the home network for TVs (DLNA renderers) and lets the user pick one.</summary>
public partial class CastWindow : Window
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<DlnaRenderer>>> _search;
    private readonly string? _preferred;
    private CancellationTokenSource? _searching;

    /// <param name="search">Runs one network search.</param>
    /// <param name="preferred">Device id chosen last time; selected again when found.</param>
    internal CastWindow(string title, Func<CancellationToken, Task<IReadOnlyList<DlnaRenderer>>> search, string? preferred)
    {
        InitializeComponent();
        _search = search;
        _preferred = preferred;
        Hint.Text = Lang.F($"“{title}” seçtiğin televizyonda açılır; bu bilgisayarda oynatma durur.");
        Loaded += async (_, _) => await SearchAsync();
        Closed += (_, _) => _searching?.Cancel();
    }

    internal DlnaRenderer? Selected { get; private set; }

    private async Task SearchAsync()
    {
        _searching?.Cancel();
        using var searching = new CancellationTokenSource();
        _searching = searching;
        SearchButton.IsEnabled = false;
        Status.Text = Lang.T("Ağda televizyon aranıyor…");
        Devices.ItemsSource = null;
        try
        {
            var found = await _search(searching.Token);
            if (!IsLoaded) return;
            Devices.ItemsSource = found;
            Devices.SelectedItem = found.FirstOrDefault(device => device.Udn == _preferred) ?? (found.Count == 1 ? found[0] : null);
            Status.Text = found.Count switch
            {
                0 => Lang.T("Televizyon bulunamadı. Televizyonun açık ve bilgisayarla aynı ağda olduğundan emin ol, sonra yeniden ara."),
                1 => Lang.T("Bir televizyon bulundu."),
                _ => Lang.F($"{found.Count} cihaz bulundu; birini seç.")
            };
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_searching == searching) _searching = null;
            if (IsLoaded) SearchButton.IsEnabled = true;
        }
    }

    private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        SendButton.IsEnabled = Devices.SelectedItem is DlnaRenderer;

    private void OnDeviceDoubleClick(object sender, MouseButtonEventArgs e) => OnSend(sender, e);

    private void OnSend(object sender, RoutedEventArgs e)
    {
        if (Devices.SelectedItem is not DlnaRenderer device) return;
        Selected = device;
        DialogResult = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
