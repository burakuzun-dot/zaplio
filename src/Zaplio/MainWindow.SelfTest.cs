using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>The walk-through behind "--self-test" (see <see cref="SelfTest"/>): real window, real layout, real animations.</summary>
public partial class MainWindow
{
    /// <summary>Lets layout finish and a few animation frames run, so layout loops and animation errors surface.</summary>
    private async Task SettleAsync(int milliseconds = 450)
    {
        await Task.Delay(milliseconds);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    internal async Task RunSelfTestAsync(string demoList)
    {
        static void Check(bool passed, string what) => SelfTest.Check(passed, what);
        try
        {
            for (var wait = 0; wait < 40 && !_isReady; wait++) await Task.Delay(100);
            Check(_isReady, "pencere açıldı");
            await SettleAsync();

            // ── no sources yet
            Check(_state.Sources.Count == 0, "denetim boş bir veri klasöründe başlar");
            Check(EmptyLibrary.IsVisible, "kaynak yokken 'ilk liste' ekranı görünür");

            // ── demo source: a local file, parsed without any network request
            await AddSourceAsync(new Source(Guid.NewGuid(), "Demo", SourceKind.M3uFile, demoList), _lifetime.Token);
            await SettleAsync();
            Check(!EmptyLibrary.IsVisible, "kaynak eklenince 'ilk liste' ekranı kalkar");
            Check(ChannelList.Items.Count is > 0 and <= 12, $"kanallar listelenir ({ChannelList.Items.Count} satır)");
            Check(ChannelList.Items.Count < 12 || !_state.Settings.MergeQualities, "kalite kopyaları tek satırda birleşir");
            Check(GroupList.Items.Count >= 5, $"gruplar listelenir ({GroupList.Items.Count})");
            CheckLiveLayout("canlı TV");

            // ── favourites from a row, then the favourites group
            if (ChannelList.Items.Count > 0 && ChannelList.Items[0] is Channel first)
            {
                OnRowFavoriteToggled(new FrameworkElement { DataContext = first }, true);
                await SettleAsync(900);
                Check(IsFavorite(first), "satırdaki şimşek kanalı favorilere ekler");
                GroupList.SelectedIndex = 1;
                await SettleAsync();
                Check(ChannelList.Items.Count == 1, $"Favoriler grubunda yalnızca favori kanal görünür ({ChannelList.Items.Count})");
                OnRowFavoriteToggled(new FrameworkElement { DataContext = ChannelList.Items[0] }, false);
                await SettleAsync();
                Check(!IsFavorite(first), "şimşek favoriden çıkarır");
                GroupList.SelectedIndex = 0;
                await SettleAsync();

                // ── "channel is sleeping"
                ShowSleeping(first);
                await SettleAsync();
                Check(SleepingView.IsVisible && SleepingView.ActualHeight > 100, "'kanal uykuda' ekranı oynatma alanını doldurur");
                HideSleeping();
            }

            // ── movies and series: tabs, indicator, room for the list, source card under the list
            foreach (var (tab, name) in new[] { (MoviesTab, "Filmler"), (SeriesTab, "Diziler") })
            {
                tab.IsChecked = true;
                await SettleAsync(700);
                Check(VodPane.IsVisible && !ChannelPane.IsVisible, $"{name}: liste paneli görünür");
                CheckVodLayout(name);
                foreach (var list in new[] { VodFavoritesTab, VodWatchlistTab, VodAllTab })
                {
                    list.IsChecked = true;
                    await SettleAsync();
                    CheckVodLayout($"{name} › {AutomationOrContent(list)}");
                }
            }

            LiveTab.IsChecked = true;
            await SettleAsync(700);
            Check(ChannelPane.IsVisible && !VodPane.IsVisible, "Canlı TV'ye dönünce kanal paneli geri gelir");
            CheckLiveLayout("Canlı TV'ye dönüş");

            // ── full-window screens and loading placeholders
            OfflineView.Visibility = Visibility.Visible;
            await SettleAsync();
            Check(OfflineView.IsVisible && OfflineView.ActualHeight > 200, "'bağlantı koptu' ekranı pencereyi kaplar");
            OfflineView.Visibility = Visibility.Collapsed;
            ChannelSkeleton.Visibility = Visibility.Visible;
            await SettleAsync();
            Check(ChannelSkeleton.ActualHeight > 100, "yükleniyor iskeleti çizilir");
            ChannelSkeleton.Visibility = Visibility.Collapsed;

            // ── other windows (shown without blocking, then closed)
            var settings = new SettingsWindow(_state.Settings, _state.Volume, () => _state.Sources.ToArray(), _ => { }) { Owner = this, ShowActivated = false };
            settings.Show();
            foreach (var (nav, page, name) in new (RadioButton, FrameworkElement, string)[]
                     {
                         (settings.SourcesNav, settings.SourcesPage, "Kaynaklar"), (settings.PlaybackNav, settings.PlaybackPage, "Oynatma"),
                         (settings.LookNav, settings.LookPage, "Görünüm"), (settings.GeneralNav, settings.GeneralPage, "Genel"),
                         (settings.AboutNav, settings.AboutPage, "Hakkında")
                     })
            {
                nav.IsChecked = true;
                await SettleAsync(200);
                Check(page.IsVisible && page.ActualHeight > 60, $"Ayarlar › {name} sayfası görünür");
            }
            Check(settings.SourceList.Items.Count == 1, "Ayarlar › Kaynaklar demo kaynağı listeler");
            settings.Close();

            foreach (var window in new Window[] { new ShortcutsWindow { Owner = this, ShowActivated = false }, new LegalNoticeWindow(requireAcceptance: false) { Owner = this, ShowActivated = false } })
            {
                window.Show();
                await SettleAsync(200);
                Check(window.ActualHeight > 100, $"{window.Title} penceresi açılır");
                window.Close();
            }

            await SettleAsync();
            Check(Mouse.Captured is null, "fare hiçbir denetimde takılı kalmadı");
        }
        catch (Exception exception)
        {
            SelfTest.Fail("öz denetim yarıda kesildi", exception);
        }
    }

    private static string AutomationOrContent(RadioButton tab) =>
        tab.Content as string ?? System.Windows.Automation.AutomationProperties.GetName(tab);

    private void CheckLiveLayout(string when)
    {
        SelfTest.Check(ChannelList.ActualHeight > 150, $"{when}: kanal listesine yer kalır ({ChannelList.ActualHeight:0} px)");
        SelfTest.Check(ReferenceEquals(SourceCard.Parent, SourceCardHostLive) && SourceCard.IsVisible, $"{when}: kaynak kartı sol altta");
        SelfTest.Check(SourceCard.ActualHeight is > 30 and < 120, $"{when}: kaynak kartı normal boyda ({SourceCard.ActualHeight:0} px)");
        CheckIndicator(LibraryIndicator, when + ": üst sekme göstergesi");
    }

    private void CheckVodLayout(string when)
    {
        SelfTest.Check(VodListTabs.ActualHeight is > 24 and < 70, $"{when}: sekme satırı normal boyda ({VodListTabs.ActualHeight:0} px)");
        SelfTest.Check(VodList.ActualHeight > 150, $"{when}: listeye yer kalır ({VodList.ActualHeight:0} px)");
        SelfTest.Check(VodCategoryPicker.IsVisible && VodSearchBox.IsVisible && VodSearchBox.ActualWidth > 120,
            $"{when}: kategori ve arama görünür (arama {VodSearchBox.ActualWidth:0} px)");
        SelfTest.Check(ReferenceEquals(SourceCard.Parent, SourceCardHostVod) && SourceCard.IsVisible, $"{when}: kaynak kartı listenin altında");
        CheckIndicator(VodListIndicator, when + ": liste sekme göstergesi");
        CheckIndicator(LibraryIndicator, when + ": üst sekme göstergesi");
    }

    private static void CheckIndicator(FrameworkElement indicator, string what) =>
        SelfTest.Check(!double.IsNaN(indicator.Width) && indicator.Width is > 20 and < 400 && indicator.ActualHeight is > 16 and < 60,
            $"{what} yerleşti ({indicator.Width:0}×{indicator.ActualHeight:0})");
}
