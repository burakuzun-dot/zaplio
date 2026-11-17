using System.Windows;
using System.Windows.Input;

namespace Zaplio;

/// <summary>Asks for a password without showing it; when <c>confirm</c> is set it must be typed twice and be long enough.</summary>
public partial class PasswordPromptWindow : Window
{
    private readonly bool _confirm;
    private readonly int _minimum;

    internal PasswordPromptWindow(string heading, string hint, string action, bool confirm, int minimum = 0)
    {
        InitializeComponent();
        Title = $"{heading} · Zaplio";
        Heading.Text = heading;
        Hint.Text = hint;
        OkText.Text = action;
        _confirm = confirm;
        _minimum = minimum;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => First.Focus();
    }

    /// <summary>The password typed; read once after OK, then the boxes are cleared.</summary>
    internal string Value { get; private set; } = "";

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        var problem = _confirm && First.Password.Length > 0 && First.Password.Length < _minimum ? $"En az {_minimum} karakter."
            : _confirm && Second.Password.Length > 0 && Second.Password != First.Password ? "İki parola aynı değil."
            : null;
        Problem.Text = problem;
        Problem.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        OkButton.IsEnabled = problem is null && First.Password.Length >= Math.Max(1, _minimum) && (!_confirm || Second.Password == First.Password);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!OkButton.IsEnabled) return;
        Value = First.Password;
        First.Clear();
        Second.Clear();
        DialogResult = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
