using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Zaplio.Views;

/// <summary>
/// Henüz liste yokken gösterilen karşılama ekranı. "Liste ekle" düğmesi
/// <see cref="AddPlaylistCommand"/>'ı çalıştırır ve <see cref="AddPlaylistRequested"/> olayını tetikler;
/// hangisi senin yapına uyuyorsa onu kullan.
/// </summary>
public partial class EmptyLibraryView : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty AddPlaylistCommandProperty = DependencyProperty.Register(
        nameof(AddPlaylistCommand), typeof(ICommand), typeof(EmptyLibraryView), new PropertyMetadata(null));

    public event EventHandler? AddPlaylistRequested;

    public ICommand? AddPlaylistCommand
    {
        get => (ICommand?)GetValue(AddPlaylistCommandProperty);
        set => SetValue(AddPlaylistCommandProperty, value);
    }

    public EmptyLibraryView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            var idle = (Storyboard)Resources["IdleStoryboard"];
            if ((bool)e.NewValue && SystemParameters.ClientAreaAnimation) idle.Begin(this, true);
            else idle.Stop(this);
        };
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => AddPlaylistRequested?.Invoke(this, EventArgs.Empty);
}
