using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace Zaplio;

public partial class GuideSourceWindow : Window
{
    internal GuideSourceWindow(Source source, bool hasAutomaticGuide)
    {
        InitializeComponent();
        SourceName.Text = source.Name;
        SourceHint.Text = hasAutomaticGuide
            ? Lang.T("Boş bırakırsan yayın kaynağının bildirdiği rehber kullanılır.")
            : Lang.T("Bu yayın kaynağı için XMLTV bağlantısı gir veya bir .xml / .xml.gz dosyası seç.");
        AddressInput.Text = source.GuideLocation ?? "";
        AddressInput.TextChanged += (_, _) => AutoButton.IsEnabled = AddressInput.Text.Length > 0;
        AutoButton.IsEnabled = AddressInput.Text.Length > 0;
        Loaded += (_, _) => AddressInput.Focus();
    }

    internal string? GuideLocation { get; private set; }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = Lang.T("XMLTV rehberi seç"),
            Filter = Lang.T("XMLTV rehberleri (*.xml;*.xml.gz;*.gz)|*.xml;*.xml.gz;*.gz|Tüm dosyalar (*.*)|*.*"),
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) == true) AddressInput.Text = picker.FileName;
    }

    private void OnUseAutomatic(object sender, RoutedEventArgs e)
    {
        AddressInput.Clear();
        AddressInput.Focus();
        SourceError.Text = "";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(AddressInput.Text))
            {
                var address = XmlTvLoader.Address(AddressInput.Text);
                GuideLocation = address.IsFile ? address.LocalPath : address.AbsoluteUri;
            }
            DialogResult = true;
        }
        catch (SourceException exception)
        {
            SourceError.Text = exception.Message;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
