using System.Windows;
using System.Windows.Input;

namespace Zaplio;

public partial class TextPromptWindow : Window
{
    internal TextPromptWindow(string heading, string hint, string value)
    {
        InitializeComponent();
        Title = $"{heading} · Zaplio";
        Heading.Text = heading;
        Hint.Text = hint;
        Input.Text = value;
        Input.MaxLength = 120;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    internal string Value => Input.Text;

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
