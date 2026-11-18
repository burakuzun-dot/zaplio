using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Zaplio;

public partial class SourcesWindow : Window
{
    private readonly ObservableCollection<Source> _sources;
    private readonly Func<Source, CancellationToken, Task> _add;
    private readonly Func<Source, Task> _remove;
    private CancellationTokenSource? _pending;
    private bool _closed;
    private Source? _editing;

    internal SourcesWindow(IEnumerable<Source> sources, Func<Source, CancellationToken, Task> add, Func<Source, Task> remove, bool startWithXtream)
    {
        _sources = new(sources);
        _add = add;
        _remove = remove;
        InitializeComponent();
        SavedSources.ItemsSource = _sources;
        if (startWithXtream) XtreamType.IsChecked = true;
        UpdateSections();
        Loaded += (_, _) =>
        {
            if (XtreamType.IsChecked == true) ServerInput.Focus();
            else UrlInput.Focus();
        };
    }

    private void UpdateSections()
    {
        ExistingSection.Visibility = _sources.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        FileFields.Visibility = FileType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UrlFields.Visibility = UrlType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        XtreamFields.Visibility = XtreamType.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTypeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ShowMessage("");
        UpdateSections();
        if (UrlType.IsChecked == true) UrlInput.Focus();
        if (XtreamType.IsChecked == true) ServerInput.Focus();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = Lang.T("M3U listesi seç"),
            Filter = Lang.T("M3U listeleri (*.m3u;*.m3u8)|*.m3u;*.m3u8|Tüm dosyalar (*.*)|*.*"),
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) == true) FileInput.Text = picker.FileName;
    }

    private Source GetSource()
    {
        var label = NameInput.Text.Trim();
        if (FileType.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(FileInput.Text)) throw new SourceException(Lang.T("Bir M3U dosyası seç."));
            return new Source(Guid.NewGuid(), label.Length > 0 ? label : Path.GetFileNameWithoutExtension(FileInput.Text),
                SourceKind.M3uFile, FileInput.Text);
        }

        var isXtream = XtreamType.IsChecked == true;
        var address = CatalogLoader.HttpAddress(isXtream ? ServerInput.Text : UrlInput.Text);
        if (label.Length == 0) label = address.Host;
        if (!isXtream) return new Source(Guid.NewGuid(), label, SourceKind.M3uUrl, address.AbsoluteUri);

        var username = UserInput.Text.Trim();
        var password = PasswordInput.Password;
        if (username.Length == 0 || password.Length == 0) throw new SourceException(Lang.T("Xtream kullanıcı adını ve şifreni gir."));
        return new Source(Guid.NewGuid(), label, SourceKind.Xtream, address.AbsoluteUri, username, password);
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        using var pending = new CancellationTokenSource();
        try
        {
            var source = GetSource();
            if (_editing is { } original) source = source with { Id = original.Id, GuideLocation = original.GuideLocation };
            _pending = pending;
            SetBusy(true);
            ShowMessage(Lang.T("Kanallar yükleniyor…"));
            await _add(source, pending.Token);
            if (!_closed) Close();
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!_closed) ShowMessage(CatalogLoader.UserMessage(exception), isError: true);
        }
        finally
        {
            _pending = null;
            if (!_closed) SetBusy(false);
        }
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (SavedSources.SelectedItem is not Source source) return;
        SetBusy(true);
        try
        {
            await _remove(source);
            if (_closed) return;
            _sources.Remove(source);
            UpdateSections();
            ShowMessage(Lang.F($"{source.Name} kaldırıldı."));
        }
        catch (Exception exception)
        {
            if (!_closed) ShowMessage(CatalogLoader.UserMessage(exception), isError: true);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Fields.IsEnabled = SavedSources.IsEnabled = AddButton.IsEnabled = !busy;
        TypeSelector.IsEnabled = !busy && _editing is null;
        EditButton.IsEnabled = RemoveButton.IsEnabled = !busy && _editing is null && SavedSources.SelectedItem is Source;
    }

    private void ShowMessage(string text, bool isError = false)
    {
        Message.Text = text;
        Message.SetResourceReference(TextBlock.ForegroundProperty, isError ? "Error" : "Muted");
    }

    private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        EditButton.IsEnabled = RemoveButton.IsEnabled = _editing is null && SavedSources.SelectedItem is Source;

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (SavedSources.SelectedItem is not Source source) return;
        _editing = source;
        NameInput.Text = source.Name;
        switch (source.Kind)
        {
            case SourceKind.Xtream:
                XtreamType.IsChecked = true;
                ServerInput.Text = source.Location;
                UserInput.Text = source.Username;
                PasswordInput.Password = source.Password;
                break;
            case SourceKind.M3uFile:
                FileType.IsChecked = true;
                FileInput.Text = source.Location;
                break;
            default:
                UrlType.IsChecked = true;
                UrlInput.Text = source.Location;
                break;
        }
        UpdateSections();
        SetBusy(false);
        var label = new TextBlock { Text = Lang.T("Değişiklikleri kaydet") };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Ink");
        AddButton.Content = label;
        ShowMessage(Lang.F($"{source.Name} düzenleniyor."));
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _pending?.Cancel();
        base.OnClosed(e);
    }
}
