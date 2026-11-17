using System.Windows;
using Xunit;

namespace Zaplio.Tests;

[Collection(WpfCollection.Name)]
public sealed class SettingsWindowTests
{
    [Fact]
    public void Settings_window_loads_shows_sources_switches_pages_and_keeps_hidden_values()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // The window uses the application theme; load it the way App.xaml does.
                var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Zaplio;component/Theme.xaml") });
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Zaplio;component/Themes/StateScreens.xaml") });

                Source[] sources = [new(Guid.NewGuid(), "Liste 1", SourceKind.M3uUrl, "http://example.com/a.m3u"),
                    new(Guid.NewGuid(), "Hesap", SourceKind.Xtream, "http://example.com", "u", "p")];
                var opened = 0;
                var settings = new PlaybackSettings { LegalNoticeAccepted = true, AmbientLight = true };
                var window = new SettingsWindow(settings, 40, () => sources, _ => opened++);
                try
                {
                    Assert.Equal(Visibility.Visible, window.SourcesPage.Visibility);
                    Assert.Equal(2, window.SourceList.Items.Count);
                    Assert.Equal(Visibility.Collapsed, window.NoSources.Visibility);
                    Assert.Equal("Kaynakları yönet", window.ManageSourcesText.Text);

                    window.AboutNav.IsChecked = true;
                    Assert.Equal(Visibility.Visible, window.AboutPage.Visibility);
                    Assert.Equal(Visibility.Collapsed, window.SourcesPage.Visibility);

                    window.AmbientLight.IsChecked = false;
                    Assert.False(window.Settings.AmbientLight);
                    Assert.True(window.Settings.LegalNoticeAccepted); // values the window does not show survive
                    Assert.Equal(40, window.Volume);
                }
                finally
                {
                    window.Close();
                }

                var empty = new SettingsWindow(new PlaybackSettings(), 75, () => [], _ => { }, SettingsPage.Look);
                try
                {
                    Assert.Equal(Visibility.Visible, empty.LookPage.Visibility);
                    Assert.Equal(Visibility.Visible, empty.NoSources.Visibility);
                    Assert.Equal("Kaynak ekle", empty.ManageSourcesText.Text);
                }
                finally
                {
                    empty.Close();
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
