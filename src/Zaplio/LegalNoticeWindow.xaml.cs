using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zaplio;

public partial class LegalNoticeWindow : Window
{
    /// <param name="requireAcceptance">True before the first source is added; false when only reading the notice.</param>
    internal LegalNoticeWindow(bool requireAcceptance)
    {
        InitializeComponent();
        Heading.Text = LegalNotice.Title;
        foreach (var paragraph in LegalNotice.Paragraphs)
        {
            Body.Children.Add(new TextBlock
            {
                Text = paragraph,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 11)
            });
        }
        More.Text = LegalNotice.MoreInfo;
        AcceptText.Text = requireAcceptance ? Lang.T("Anladım, devam et") : Lang.T("Kapat");
        CancelButton.Visibility = requireAcceptance ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
