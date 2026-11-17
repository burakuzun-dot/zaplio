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
            SelfTest.Snapshot(this, "ilk liste");
            Check(_mediaSession?.IsActive != true, "oynatma yokken Windows medya kartı kapalıdır");
            Check(HandleMediaKey(Key.MediaPlayPause), "medya kartı kapalıyken pencere medya tuşunu işler");

            // ── demo source: a local file, parsed without any network request
            await AddSourceAsync(new Source(Guid.NewGuid(), "Demo", SourceKind.M3uFile, demoList), _lifetime.Token);
            await SettleAsync();
            Check(!EmptyLibrary.IsVisible, "kaynak eklenince 'ilk liste' ekranı kalkar");
            Check(ChannelList.Items.Count is > 0 and <= 12, $"kanallar listelenir ({ChannelList.Items.Count} satır)");
            Check(ChannelList.Items.Count < 12 || !_state.Settings.MergeQualities, "kalite kopyaları tek satırda birleşir");
            Check(GroupList.Items.Count >= 5, $"gruplar listelenir ({GroupList.Items.Count})");
            CheckLiveLayout("canlı TV");
            SelfTest.Snapshot(this, "canlı tv");

            // ── favourites from a row, then the favourites group
            if (ChannelList.Items.Count > 0 && ChannelList.Items[0] is Channel first)
            {
                ChannelList.SelectedItem = first;
                Check(PlaceholderTitle.Text == "Kanal seçildi" && PlayButton.IsEnabled, "kanal seçilince oynatma adımı görünür");
                await SettleAsync();
                SelfTest.Snapshot(this, "kanal seçili");
                OnGuideGrid(GuideGridButton, new RoutedEventArgs());
                await SettleAsync();
                Check(GuidePane.IsVisible && GuideConnect.IsVisible, "yayın rehberi yokken XMLTV kurulum düğmesi gösterilir");
                SelfTest.Snapshot(this, "rehber paneli");
                SetGuideOpen(false);

                OnRowFavoriteToggled(new FrameworkElement { DataContext = first }, true);
                await SettleAsync(900);
                Check(IsFavorite(first), "satırdaki şimşek kanalı favorilere ekler");
                GroupList.SelectedIndex = 1;
                await SettleAsync();
                SelfTest.Snapshot(this, "favoriler");
                Check(ChannelList.Items.Count == 1, $"Favoriler grubunda yalnızca favori kanal görünür ({ChannelList.Items.Count})");
                OnRowFavoriteToggled(new FrameworkElement { DataContext = ChannelList.Items[0] }, false);
                await SettleAsync();
                Check(!IsFavorite(first), "şimşek favoriden çıkarır");
                GroupList.SelectedIndex = 0;
                await SettleAsync();

                // ── "Şansıma" for channels: only the pick is checked, nothing is played (no network in the self test)
                var luckyChannel = await RunLuckyAsync(600, open: false);
                Check(luckyChannel is Channel picked && VisibleChannels.Contains(picked), "kanallarda 'Şansıma' listedeki bir kanalı seçer");

                // ── "channel is sleeping"
                ShowSleeping(first);
                await SettleAsync();
                Check(SleepingView.IsVisible && SleepingView.ActualHeight > 100, "'kanal uykuda' ekranı oynatma alanını doldurur");
                SelfTest.Snapshot(this, "kanal uykuda");
                HideSleeping();
            }

            // ── movies and series: tabs, indicator, room for the list, source card under the list
            foreach (var (tab, name) in new[] { (MoviesTab, "Filmler"), (SeriesTab, "Diziler") })
            {
                tab.IsChecked = true;
                await SettleAsync(700);
                Check(VodPane.IsVisible && !ChannelPane.IsVisible, $"{name}: liste paneli görünür");
                CheckVodLayout(name);
                if (tab == MoviesTab) await CheckPosterGridAsync();
                foreach (var list in new[] { VodFavoritesTab, VodWatchlistTab, VodAllTab })
                {
                    list.IsChecked = true;
                    await SettleAsync();
                    CheckVodLayout($"{name} › {AutomationOrContent(list)}");
                    SelfTest.Snapshot(this, $"{name} {AutomationOrContent(list)}");
                }
            }

            await CheckVodSectionSwitchAsync();
            LiveTab.IsChecked = true;
            await SettleAsync(700);
            Check(ChannelPane.IsVisible && !VodPane.IsVisible, "Canlı TV'ye dönünce kanal paneli geri gelir");
            SelfTest.Snapshot(this, "canlı tv dönüş");
            CheckLiveLayout("Canlı TV'ye dönüş");

            // ── "Sinema modu": everything around the video fades, a part lights up under the mouse, the lights return after playback
            SetCinema(true);
            await SettleAsync(1300);
            Check(IsCinemaMode && TitleBar.Opacity < 0.1 && ChannelPane.Opacity < 0.1 && NavigationPane.Opacity < 0.1 && CinemaShade.Opacity > 0.9 &&
                PlayerControls.Opacity < 0.1 && Footer.Opacity < 0.1 && VideoFrame.IsVisible && VideoFrame.Opacity >= 1,
                $"sinema modunda çevre kararır, yalnızca video alanı kalır (başlık {TitleBar.Opacity:0.00}, kumandalar {PlayerControls.Opacity:0.00}, alt bilgi {Footer.Opacity:0.00})");
            SelfTest.Snapshot(this, "sinema modu");
            PlayerControls.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            await SettleAsync(400);
            Check(PlayerControls.Opacity > 0.8, $"karartılmış kumandalar fareyle aydınlanır ({PlayerControls.Opacity:0.00})");
            EndCinemaAfterPlayback();
            await SettleAsync(2500);
            Check(!IsCinemaMode && TitleBar.Opacity >= 0.999 && ChannelPane.Opacity >= 0.999 && PlayerControls.Opacity >= 0.999 && CinemaShade.Opacity < 0.01,
                "oynatma bitince ışıklar yavaşça geri açılır");

            // ── the space bar and the timeline (no playback here: only what decides who gets the key, and the timeline's geometry)
            SearchBox.Focus();
            var space = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this)!, 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            Check(!HandleSpaceKey(space) && LastSpaceAction == SpaceAction.PassThrough, "arama kutusuna yazarken Boşluk yazıya gider");
            Keyboard.ClearFocus();
            Check(!VodSeek.Focusable && !VolumeSlider.Focusable, "zaman çubuğu ve ses kaydırıcısı tıklanınca tuşları (Boşluk, oklar) üzerine almaz");
            Check(Math.Abs(SeekTimeAt(SeekXFor(42_000, 500, 100_000), 500, 100_000) - 42_000) < 1 && SeekTimeAt(0, 500, 100_000) == 0 &&
                SeekTimeAt(500, 500, 100_000) == 100_000 && VodSeek.Height >= 28,
                $"zaman çubuğunda fare altındaki zaman başparmağın konumuyla örtüşür, tıklama alanı geniş (yükseklik {VodSeek.Height:0} px)");

            // ── one press drags: the press must reach our handlers even though WPF's slider handles it first
            // The timeline is shown and enabled as during playback, so the slider's own click handling is really in the way.
            var timelineShown = VodTimeline.Visibility;
            VodTimeline.Visibility = Visibility.Visible;
            VodSeek.Maximum = 100_000;
            VodSeek.IsEnabled = true;
            await SettleAsync(150);
            var presses = SeekPresses;
            VodSeek.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = PreviewMouseLeftButtonDownEvent });
            Check(SeekPresses == presses + 1, "zaman çubuğuna basış sürükleme koduna ulaşır (tek basışta sürükleme)");
            VodSeek.IsEnabled = false;
            VodTimeline.Visibility = timelineShown;
            if (VolumeSlider.IsVisible)
            {
                var volume = VolumeSlider.Value;
                VolumeSlider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = PreviewMouseLeftButtonDownEvent });
                var held = VolumeSlider.IsMouseCaptured;
                VolumeSlider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = PreviewMouseLeftButtonUpEvent });
                Check(held && !VolumeSlider.IsMouseCaptured, "ses kaydırıcısı tek basışta fareyi tutar, bırakınca bırakır");
                VolumeSlider.Value = volume;
            }

            // ── recording and speed controls: nothing plays in the self test, so only their idle state is checked
            Check(!RecordButton.IsEnabled && !IsRecording && !RecordBadge.IsVisible, "oynatma yokken kayıt düğmesi kapalı, kayıt rozeti gizli");
            Check(RecordingPlan.Folder.EndsWith("Zaplio", StringComparison.Ordinal), "kayıtlar Videolar\\Zaplio klasörüne gider");
            await CheckLocalClipAsync();

            // ── themes: the light palette and another accent reach the open window at once, then the default comes back
            var resources = Application.Current.Resources;
            ThemeManager.Apply(resources, AppTheme.Light, "teal");
            await SettleAsync(300);
            var footer = (Footer.Background as System.Windows.Media.SolidColorBrush)?.Color;
            Check(ThemeManager.IsLight && footer is { R: > 200, G: > 200, B: > 200 },
                $"açık tema açık duran pencereye yeniden başlatmadan geçer (alt bilgi {footer}, aynı nesne " +
                $"{ReferenceEquals(Footer.Background, resources["Panel"])}, donmuş {(resources["Panel"] as System.Windows.Freezable)?.IsFrozen}, " +
                $"alt bilgi donmuş {Footer.Background?.IsFrozen}, kaynağın rengi {(resources["Panel"] as System.Windows.Media.SolidColorBrush)?.Color})");
            Check((resources["Accent"] as System.Windows.Media.SolidColorBrush)?.Color == ThemePalette.Accent("teal").Deep,
                "vurgu rengi değişir");
            var onVideo = (PlaceholderTitle.Foreground as System.Windows.Media.SolidColorBrush)?.Color;
            Check(onVideo is { R: > 200, G: > 200, B: > 200 }, $"açık temada video alanındaki yazı açık renk kalır ({onVideo})");
            SelfTest.Snapshot(this, "açık tema turkuaz");
            MoviesTab.IsChecked = true;
            await SettleAsync(500);
            SelfTest.Snapshot(this, "açık tema filmler");
            var lightSettings = new SettingsWindow(_state.Settings, _state.Volume, () => _state.Sources.ToArray(), _ => { }, SettingsPage.Look, SourceSettings)
                { Owner = this, ShowActivated = false };
            lightSettings.Show();
            await SettleAsync(300);
            SelfTest.Snapshot(lightSettings, "açık tema ayarlar");
            lightSettings.Close();
            LiveTab.IsChecked = true;
            await SettleAsync(400);
            ThemeManager.Apply(resources, AppTheme.Dark, "orange");
            await SettleAsync(300);
            SelfTest.Snapshot(this, "koyu tema turuncu");
            ThemeManager.Apply(resources, AppTheme.Dark, "purple");
            await SettleAsync(200);
            Check(!ThemeManager.IsLight && (resources["Panel"] as System.Windows.Media.SolidColorBrush)?.Color ==
                System.Windows.Media.Color.FromRgb(0x12, 0x16, 0x21), "varsayılan koyu mor tema geri gelir");

            // ── kids profile (state only in memory; nothing is saved)
            if (_activeSourceId is Guid kidsSource && _catalog.GetValueOrDefault(kidsSource) is { } kidsCatalog)
            {
                var everything = ActiveChannels.Count;
                var groups = kidsCatalog.Channels.SelectMany(channel => channel.Categories).Distinct(CategoryComparer).ToList();
                _state.Child.SetPin("2468");
                _state.Child.SetAllowed(kidsSource, LibrarySection.Live, groups.Take(1));
                _state.Child.Active = true;
                ApplyChildProfile();
                await SettleAsync(300);
                Check(ChildBadge.IsVisible, "çocuk profili açıkken üst çubukta işareti görünür");
                Check(ActiveChannels.Count > 0 && ActiveChannels.All(_state.Child.AllowsChannel) &&
                      (groups.Count < 2 || ActiveChannels.Count < everything),
                    $"çocuk profilinde yalnızca izin verilen kategori listelenir ({ActiveChannels.Count}/{everything})");
                SelfTest.Snapshot(this, "çocuk profili");
                var kids = new ChildProfileWindow("Demo", _state.Child, kidsSource,
                    new Dictionary<LibrarySection, IReadOnlyList<string>?> { [LibrarySection.Live] = groups, [LibrarySection.Movies] = null })
                    { Owner = this, ShowActivated = false };
                kids.Show();
                await SettleAsync(300);
                Check(kids.Categories.Items.Count == groups.Count && kids.StopButton.IsVisible, "çocuk profili penceresi kategorileri listeler");
                SelfTest.Snapshot(kids, "çocuk profili ayarları");
                kids.SectionPicker.SelectedIndex = 1;
                await SettleAsync(200);
                Check(kids.EmptyText.IsVisible, "yüklenmemiş bölüm için açıklama görünür");
                kids.Close();
                _state.Child = new ChildProfile();
                ApplyChildProfile();
                await SettleAsync(300);
                Check(!ChildBadge.IsVisible && ActiveChannels.Count == everything, "çocuk profili kapanınca tüm liste geri gelir");
            }

            // ── full-window screens and loading placeholders
            OfflineView.Visibility = Visibility.Visible;
            await SettleAsync();
            Check(OfflineView.IsVisible && OfflineView.ActualHeight > 200, "'bağlantı koptu' ekranı pencereyi kaplar");
            SelfTest.Snapshot(this, "bağlantı koptu");
            OfflineView.Visibility = Visibility.Collapsed;
            ChannelSkeleton.Visibility = Visibility.Visible;
            await SettleAsync();
            Check(ChannelSkeleton.ActualHeight > 100, "yükleniyor iskeleti çizilir");
            SelfTest.Snapshot(this, "iskelet");
            ChannelSkeleton.Visibility = Visibility.Collapsed;

            // ── other windows (shown without blocking, then closed)
            var settings = new SettingsWindow(_state.Settings, _state.Volume, () => _state.Sources.ToArray(), _ => { }, host: SourceSettings)
                { Owner = this, ShowActivated = false };
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
                SelfTest.Snapshot(settings, $"ayarlar {name}");
            }
            Check(settings.SourceList.Items.Count == 1, "Ayarlar › Kaynaklar demo kaynağı listeler");
            settings.SourcesNav.IsChecked = true;
            await SettleAsync(200);
            SelfTest.Snapshot(settings, "ayarlar Kaynaklar aktif");
            Check(settings.SourceList.SelectedItem is not null, "Ayarlar › Kaynaklar aktif kaynağı seçili gösterir");
            Check(settings.RefreshListsButton.IsVisible && settings.RefreshListsButton.IsEnabled, "Ayarlar › Kaynaklar'da 'Listeleri yenile' kullanılabilir");
            Check(!settings.AccountPanel.IsVisible, "M3U kaynağında abonelik satırı gösterilmez");
            settings.Close();

            var history = new WatchHistoryWindow("Demo", [], new Dictionary<string, long>(), resumeEnabled: true)
                { Owner = this, ShowActivated = false };
            history.Show();
            history.ResumeTab.IsChecked = true;
            await SettleAsync(200);
            Check(history.HistoryTitleBar.IsVisible && history.EmptyHistory.IsVisible && !history.PlayHistoryButton.IsVisible,
                "izleme geçmişinde başlık ve boş durum görünür");
            SelfTest.Snapshot(history, "izleme geçmişi");
            history.Close();

            foreach (var window in new Window[] { new ShortcutsWindow { Owner = this, ShowActivated = false }, new LegalNoticeWindow(requireAcceptance: false) { Owner = this, ShowActivated = false } })
            {
                window.Show();
                await SettleAsync(200);
                Check(window.ActualHeight > 100, $"{window.Title} penceresi açılır");
                SelfTest.Snapshot(window, window.Title);
                window.Close();
            }

            await CheckGuideGridAsync();

            // ── videos from this computer: empty made-up files in the temporary data folder, opened without playing
            var localFolder = System.IO.Path.Combine(AppPaths.Root, "yerel");
            System.IO.Directory.CreateDirectory(localFolder);
            foreach (var name in new[] { "Bölüm 10.mp4", "Bölüm 2.mp4", "notlar.txt", "Bölüm 2.srt" })
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(localFolder, name), []);
            var opened = await OpenLocalAsync([localFolder, System.IO.Path.Combine(localFolder, "Bölüm 2.srt")], play: false);
            Check(opened is { IsOnDemand: true } && opened.Name == "Bölüm 2" && LocalMedia.IsLocal(opened) && LocalQueueCount == 1 && PlayableHere(opened),
                $"bırakılan klasörün videoları doğal sırayla açılır, sıradakiler kuyruğa girer ({opened?.Name}, kuyruk {LocalQueueCount})");
            Check(!LogTarget(opened).Contains(localFolder, StringComparison.OrdinalIgnoreCase), "günlüğe yerel dosyanın yolu yazılmaz");
            var unsupported = await OpenLocalAsync([System.IO.Path.Combine(localFolder, "notlar.txt")], play: false);
            Check(unsupported is null && LocalQueueCount == 1, "video olmayan dosya oynatılmaz, kuyruk bozulmaz");
            DropHint.Visibility = Visibility.Visible;
            await SettleAsync(150);
            Check(DropHint.ActualHeight > 300, "dosya sürüklenirken 'bırak' ipucu pencereyi kaplar");
            SelfTest.Snapshot(this, "sürükle bırak");
            DropHint.Visibility = Visibility.Collapsed;

            WindowState = WindowState.Normal;
            Width = MinWidth;
            await SettleAsync();
            CheckLibraryIndicator("en dar pencere");
            var fullscreenRight = FullscreenButton.TranslatePoint(new Point(FullscreenButton.ActualWidth, 0), PlayerControls).X;
            Check(FullscreenButton.IsVisible && fullscreenRight <= PlayerControls.ActualWidth - 4,
                $"en dar pencerede tam ekran düğmesi kumanda çubuğuna sığar ({fullscreenRight:0}/{PlayerControls.ActualWidth:0} px)");
            Check(PlayerTitle.ActualWidth >= PlayerTitle.DesiredSize.Width - 1 && !IsTrimmed(PlayerTitle), "en dar pencerede oynatıcı başlığı kesilmez");
            SelfTest.Snapshot(this, "en dar pencere");
            Check(Mouse.Captured is null, "fare hiçbir denetimde takılı kalmadı");
        }
        catch (Exception exception)
        {
            SelfTest.Fail("öz denetim yarıda kesildi", exception);
        }
    }

    /// <summary>Sixty made-up movies in the poster grid: virtualization, columns, selection, scrolling to the end, favourites.</summary>
    private async Task CheckPosterGridAsync()
    {
        static void Check(bool passed, string what) => SelfTest.Check(passed, what);
        var source = _activeSourceId ?? Guid.Empty;
        _vodItems = Enumerable.Range(1, 60).Select(number => new VodItem(number, $"Film {number}", number % 2 == 0 ? "Dram" : "Komedi", "2021",
            new Channel($"{source:N}:demo-film-{number}", source, $"Film {number}", "Film", new Uri($"http://example.com/demo/film-{number}.mp4"),
                IsOnDemand: true))).ToList();
        var demo = _vodItems.ToList();
        demo[0] = demo[0] with
        {
            Plot = "Öz denetim için uydurulmuş bir konu. Küçük bir kasabada geçen, iki arkadaşın yolculuğunu anlatan sakin bir hikâye.",
            Facts = new VodFacts("Komedi, Dram", "1 sa 52 dk", "★ 7,4", "Yönetmen A", "Oyuncu A, Oyuncu B, Oyuncu C")
        };
        _vodItems = demo;
        var partial = _vodItems.Take(4).Select((item, index) => (Entry: new WatchEntry(item.Playable!, DateTimeOffset.Now.AddMinutes(-index)), Seconds: 43 + index * 60)).ToList();
        foreach (var (entry, seconds) in Enumerable.Reverse(partial))
        {
            _state.WatchHistory.Insert(0, entry);
            _state.WatchPositions[entry.Channel.Id] = seconds * 1000L;
        }
        ResetVodCategories();
        UpdateVodRows();
        await SettleAsync();

        int Realized() => Enumerable.Range(0, VodList.Items.Count).Count(index => VodList.ItemContainerGenerator.ContainerFromIndex(index) is not null);
        Check(VodList.Items.Count == 60, $"afiş ızgarası 60 filmi listeler ({VodList.Items.Count})");
        var realized = Realized();
        Check(realized is > 0 and < 60, $"yalnızca görünen karolar oluşturulur ({realized}/60)");
        CheckVodBrowsingDimensions("film kütüphanesi");
        CheckVodInfoPanel("film kütüphanesi");
        Check(ReferenceEquals(ContinuePanel.Parent, ContinueHostInfo) && ContinuePanel.IsVisible && !VodInfoScroll.IsVisible,
            "seçim yokken oynatıcının altında 'izlemeye devam et' kartları görünür");
        var continueHost = ContinuePanel.Parent;
        SelfTest.Snapshot(this, "film ızgarası");
        CheckContinueShelf("film kütüphanesi");
        if (VodList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem firstTile)
        {
            var columns = (int)Math.Round(VodList.ActualWidth / Math.Max(1, firstTile.ActualWidth));
            Check(columns >= 3, $"ızgara birden çok sütunlu ({columns} sütun, karo {firstTile.ActualWidth:0}×{firstTile.ActualHeight:0})");
            Check(firstTile.ActualHeight > firstTile.ActualWidth, "karolar afiş oranında (dikey)");
        }
        else Check(false, "ilk karo oluşturuldu");

        var originalWidth = Width;
        var originalHeight = Height;
        Width = MinWidth;
        await SettleAsync();
        CheckVodBrowsingDimensions("dar film kütüphanesi");
        SelfTest.Snapshot(this, "film ızgarası dar");

        Width = 1920;
        Height = 1008;
        await SettleAsync();
        CheckVodBrowsingDimensions("geniş film kütüphanesi");
        SelfTest.Snapshot(this, "film ızgarası geniş");
        Width = originalWidth;
        Height = originalHeight;
        foreach (var (entry, _) in partial)
        {
            _state.WatchHistory.Remove(entry);
            _state.WatchPositions.Remove(entry.Channel.Id);
        }

        // "Şansıma": the reel spins over the window, stops on a random title and opens its preview
        Check(LuckyVodButton.IsVisible, "film listesinin başlığında 'Şansıma' şimşeği görünür");
        var lucky = RunLuckyAsync(1400);
        await Task.Delay(500);
        Check(IsLuckySpinning && LuckyReelView is { ActualWidth: > 400, ActualHeight: > 300 }, "şimşeğe basınca kartlar kayan bir makarada döner");
        if (LuckyReelView is { } reelView) SelfTest.Snapshot(reelView, "şansıma makara");
        var luckyPick = await lucky;
        await SettleAsync();
        Check(!IsLuckySpinning && luckyPick is not null && ReferenceEquals(VodList.SelectedItem, luckyPick) && VodPreviewPanel.IsVisible,
            $"makara durunca seçilen film açılır ({(luckyPick as VodRow)?.Title})");
        SelfTest.Snapshot(this, "şansıma sonucu");
        VodList.SelectedItem = null;
        await SettleAsync(200);

        VodList.SelectedIndex = 0;
        await SettleAsync();
        Check(VodPreviewPanel.IsVisible, "karo seçilince yanında ayrıntı paneli açılır");
        CheckVodInfoPanel("film seçiliyken");
        Check(VodInfoChips.Items.Count == 4 && VodInfoPlot.IsVisible && VodInfoCast.IsVisible && VodInfoDirector.IsVisible,
            $"oynatıcının altında tür, süre, puan, konu ve oyuncular görünür ({VodInfoChips.Items.Count} etiket)");
        Check(!VodPreviewSummary.IsVisible, "konu önizleme kartında tekrarlanmaz");
        Check(ReferenceEquals(ContinuePanel.Parent, continueHost), "film seçince 'izlemeye devam et' yer değiştirmez");
        if (ContinueHostInfo.IsVisible) CheckContinueShelf("film seçiliyken");
        SelfTest.Snapshot(this, "film önizleme");
        Check(!PlayerControls.IsVisible, "önizlemede oynatma kumandaları içeriğin düğmelerini tekrarlamaz");
        Check(VodFavoriteToggle.IsVisible && VodWatchlistButton.IsVisible, "ayrıntı panelinde şimşek ve 'Sonra izle' görünür");

        Width = MinWidth;
        await SettleAsync();
        var watchlistPosition = VodWatchlistButton.TranslatePoint(new Point(), VodPreviewPanel);
        var playPosition = VodPreviewPlayButton.TranslatePoint(new Point(), VodPreviewPanel);
        Check(watchlistPosition.Y > playPosition.Y + 20 && watchlistPosition.X + VodWatchlistButton.ActualWidth <= VodPreviewPanel.ActualWidth,
            "dar önizlemede 'Sonra izle' düğmesi alt satıra sığar");
        SelfTest.Snapshot(this, "film önizleme dar");
        Width = originalWidth;
        await SettleAsync(200);

        VodList.ScrollIntoView(VodList.Items[59]);
        await SettleAsync();
        Check(VodList.ItemContainerGenerator.ContainerFromIndex(59) is not null, "listenin sonuna kaydırılabilir");
        Check(VodList.ItemContainerGenerator.ContainerFromIndex(0) is null, "ekrandan çıkan karolar bırakılır");
        VodList.ScrollIntoView(VodList.Items[0]);
        await SettleAsync();

        OnVodRowFavoriteToggled(new FrameworkElement { DataContext = VodList.Items[0] }, true);
        await SettleAsync(1000);
        VodFavoritesTab.IsChecked = true;
        await SettleAsync();
        Check(VodList.Items.Count == 1, $"Favorilerim yalnızca favori filmi gösterir ({VodList.Items.Count})");
        if (VodList.Items.Count == 1) OnVodRowFavoriteToggled(new FrameworkElement { DataContext = VodList.Items[0] }, false);
        await SettleAsync();
        Check(VodList.Items.Count == 0 && VodEmpty.IsVisible, "favori kaldırılınca liste boş mesajı gösterir");
        VodAllTab.IsChecked = true;
        VodList.SelectedItem = null;
        await SettleAsync();

        // one-column episode list uses the same panel
        _selectedSeries = new VodItem(999, "Dizi 1", "Dram", "2022", null);
        UpdateVodRows();
        await SettleAsync();
        if (VodList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem row)
            Check(row.ActualWidth > VodList.ActualWidth * 0.8 && row.ActualHeight < 90, $"bölüm listesi tek sütun satırlardır ({row.ActualWidth:0}×{row.ActualHeight:0})");
        else Check(false, "bölüm satırı oluşturuldu");
        var shortHeight = Height;
        Height = 1150; // inside a series the player is wider, so the panel needs a taller window
        VodList.SelectedIndex = 0;
        await SettleAsync();
        SelfTest.Snapshot(this, "bölüm seçili");
        Check(VodInfoPanel.IsVisible && VodNextEpisodes.Items.Count == 3 && VodNextEpisodesPanel.IsVisible,
            $"seçili bölümün altında sıradaki bölümler listelenir ({VodNextEpisodes.Items.Count})");
        VodList.SelectedItem = null;
        Height = shortHeight;
        await SettleAsync(200);
        SelfTest.Snapshot(this, "dizi bölümleri");
        Check(VodPane.ActualWidth <= 412, $"dizi içindeyken liste daralır ({VodPane.ActualWidth:0} px)");
        _selectedSeries = null;
        _vodItems = [];
        ResetVodCategories();
        UpdateVodRows();
        await SettleAsync();
    }

    private async Task CheckVodSectionSwitchAsync()
    {
        var original = _state.Sources[0];
        var movie = new Channel($"{original.Id:N}:demo-movie", original.Id, "Film 1", "Film",
            new Uri("http://example.com/demo/film.mp4"), IsOnDemand: true);
        _vodCatalogs[(original.Id, LibrarySection.Movies)] = [new VodItem(1, movie.Name, "Film", "2025", movie)];
        _vodCatalogs[(original.Id, LibrarySection.Series)] = [new VodItem(1, "Dizi 1", "Dizi", "2025", null)];
        // Both catalogs are already in memory and the detail request is cancelled before its debounce elapses.
        _state.Sources[0] = original with { Kind = SourceKind.Xtream };
        _lastChannel = movie;
        try
        {
            MoviesTab.IsChecked = true;
            VodList.SelectedIndex = 0;
            SelfTest.Check(SelectedVodPreview?.Title == movie.Name, "film seçimi kendi önizlemesini açar");
            SeriesTab.IsChecked = true;
            await SettleAsync();
            SelfTest.Check(VodList.Items.Count == 1 && VodList.SelectedItem is null && !VodPreviewPanel.IsVisible,
                "aynı numaralı dizi filmden geçerken kendiliğinden seçilmez");
            SelfTest.Check(PlayerTitle.Text == "Diziler" && PlaceholderTitle.Text == "Bir dizi seç" && !PlayButton.IsEnabled,
                "dizi kütüphanesinde önceki filmin başlığı ve oynatma düğmesi etkin kalmaz");
        }
        finally
        {
            _lastChannel = null;
            _state.Sources[0] = original;
            _vodCatalogs.Remove((original.Id, LibrarySection.Movies));
            _vodCatalogs.Remove((original.Id, LibrarySection.Series));
        }
    }

    /// <summary>Cards under the player fill the panel's width in equal columns, none cut off; above the list they keep their size.</summary>
    private void CheckContinueShelf(string when)
    {
        var cards = Enumerable.Range(0, ContinueList.Items.Count).Select(index => ContinueList.ItemContainerGenerator.ContainerFromIndex(index))
            .OfType<FrameworkElement>().ToList();
        if (!ContinuePanel.IsVisible || cards.Count == 0)
        {
            SelfTest.Check(false, $"{when}: izlemeye devam et kartları görünür");
            return;
        }
        var first = cards[0];
        if (ReferenceEquals(ContinuePanel.Parent, ContinueHostInfo))
        {
            var width = ContinueHostInfo.ActualWidth;
            var shown = Math.Min(cards.Count, _continueShelf.Columns);
            var lastVisible = cards[Math.Min(cards.Count, _continueShelf.Columns * _continueShelf.Rows) - 1];
            SelfTest.Check(lastVisible.TranslatePoint(new Point(0, lastVisible.ActualHeight - ContinueShelfLayout.Gap), ContinueHostInfo).Y <= ContinueHostInfo.ActualHeight + 1,
                $"{when}: devam kartları satırlara dizilir, sığan satırlar tam görünür ({_continueShelf.Columns}×{_continueShelf.Rows})");
            var used = shown * first.ActualWidth; // each container includes the card's right margin
            var left = first.TranslatePoint(new Point(), ContinueHostInfo).X;
            SelfTest.Check(Math.Abs(left) <= 1 && used <= width + 1 && (_continueShelf.CardWidth >= ContinueShelfLayout.MaximumCardWidth || width - used <= ContinueShelfLayout.Gap + 15),
                $"{when}: devam kartları panelin genişliğini eşit sütunlarla doldurur ({shown} sütun, {first.ActualWidth:0} px, {used:0}/{width:0} px)");
            SelfTest.Check(first.ActualHeight >= ContinueShelfLayout.MinimumCardHeight - 1 &&
                first.TranslatePoint(new Point(0, first.ActualHeight), VodInfoPanel).Y <= VodInfoPanel.ActualHeight + 1,
                $"{when}: devam kartları panele sığar ({first.ActualWidth:0}×{first.ActualHeight:0})");
        }
        else SelfTest.Check(first is { ActualWidth: >= 230 and <= 248, ActualHeight: >= 80 and <= 96 },
            $"{when}: listenin üstündeki devam kartı okunur boyutta ({first.ActualWidth:0}×{first.ActualHeight:0})");
    }

    /// <summary>
    /// The real background copy behind recording a video from this computer: the bundled intro (a local file, no network) is
    /// copied between two times into a temporary .mkv, without a picture. Nothing is written to the user's Videos folder.
    /// </summary>
    private async Task CheckLocalClipAsync()
    {
        var intro = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Intro.mp4");
        if (!System.IO.File.Exists(intro))
        {
            SelfTest.Check(false, "kayıt denemesi için açılış videosu bulundu");
            return;
        }
        var folder = System.IO.Path.Combine(AppPaths.Root, "kayit");
        System.IO.Directory.CreateDirectory(folder);
        var source = System.IO.Path.Combine(folder, "kaynak.mp4");
        System.IO.File.Copy(intro, source, overwrite: true);
        var target = System.IO.Path.Combine(folder, "kesit" + RecordingPlan.Extension);
        _engine ??= await EnsureEngineAsync();

        _recordFile = target;
        _clipChannel = LocalMedia.ToChannel(source);
        _clipStart = 0;
        _clipLast = 2_000;
        await FinishClipAsync(target);
        var size = System.IO.File.Exists(target) ? new System.IO.FileInfo(target).Length : 0;
        SelfTest.Check(size > 10_000 && !IsRecording && FooterStatus.Text.StartsWith("Kayıt kaydedildi", StringComparison.Ordinal),
            $"bilgisayardaki videonun kaydı durmadan, arka planda .mkv olarak kopyalanır ({size / 1024} KB)");
    }

    private void CheckVodInfoPanel(string when)
    {
        SelfTest.Check(VodInfoPanel.IsVisible, $"{when}: oynatıcının altındaki ayrıntı paneli görünür");
        var top = VodInfoPanel.TranslatePoint(new Point(), PlayerPane).Y;
        var bottom = top + VodInfoPanel.ActualHeight;
        var column = ((FrameworkElement)PlayerPane.Parent).ActualHeight - PlayerPane.TranslatePoint(new Point(), (UIElement)PlayerPane.Parent).Y;
        SelfTest.Check(top >= PlayerPane.ActualHeight + 4 && bottom <= column + 1,
            $"{when}: ayrıntı paneli oynatıcıyla çakışmaz ve pencereden taşmaz ({top:0}–{bottom:0} / {column:0} px)");
        SelfTest.Check(ContinuePanel.Parent is not null && (ReferenceEquals(ContinuePanel.Parent, ContinueHostInfo) || ReferenceEquals(ContinuePanel.Parent, ContinueHostList)),
            $"{when}: 'izlemeye devam et' bir yerde duruyor ({(ReferenceEquals(ContinuePanel.Parent, ContinueHostInfo) ? "oynatıcının altında" : "listenin üstünde")})");
    }

    private void CheckVodBrowsingDimensions(string when)
    {
        var playerColumn = PlayerPane.ActualWidth + PlayerPane.Margin.Left + PlayerPane.Margin.Right;
        SelfTest.Check(Math.Abs(VodPane.ActualWidth - playerColumn) <= 2, $"{when}: kütüphane ve oynatıcı eşit alan alır");
        SelfTest.Check(Math.Abs(VideoFrame.ActualHeight - VideoFrame.ActualWidth * 9 / 16) <= 2,
            $"{when}: önizleme 16:9 ({VideoFrame.ActualWidth:0}×{VideoFrame.ActualHeight:0})");
        var controlsTop = PlayerControls.TranslatePoint(new Point(), VideoFrame).Y;
        SelfTest.Check(Math.Abs(controlsTop - VideoFrame.ActualHeight - PlayerControls.Margin.Top) <= 2,
            $"{when}: kumandalar önizlemenin hemen altında");
        var top = PlayerPane.TranslatePoint(new Point(), (UIElement)PlayerPane.Parent).Y;
        SelfTest.Check(Math.Abs(top - PlayerPane.Margin.Top) <= 2, $"{when}: oynatıcı sütunun üstüne hizalı (üstte {top:0} px)");
    }

    /// <summary>The grid guide with a made-up guide for the demo channels: rows, shared time scroll, what a click plays.</summary>
    private async Task CheckGuideGridAsync()
    {
        static void Check(bool passed, string what) => SelfTest.Check(passed, what);
        if (ActiveSource is not { } source)
        {
            Check(false, "ızgara rehber için aktif kaynak var");
            return;
        }
        Check(GuideGridButton.IsVisible, "Canlı TV başlığında 'Tüm rehber' düğmesi görünür");

        var now = DateTimeOffset.Now;
        var guide = new XmlTvGuide();
        for (var channel = 0; channel < 8; channel++) // the last four demo channels have no guide
            for (var hour = -4; hour < 20; hour++)
                guide.AddProgramme($"demo{channel}", new GuideProgramme(now.AddHours(hour).AddMinutes(-20), now.AddHours(hour + 1).AddMinutes(-20),
                    $"Program {hour + 5}", "Öz denetim için uydurulmuş program."));
        guide.SortProgrammes();

        var played = new List<(Channel Channel, GuideGrid.Cell? Cell)>();
        var window = new GuideGridWindow("Tüm kanallar", VisibleChannels, guide.ForChannel, _ => false,
            (channel, cell) => { played.Add((channel, cell)); return Task.CompletedTask; }) { Owner = this, ShowActivated = false };
        window.Show();
        await SettleAsync(600);
        Check(window.RowCount is > 0 and < 12, $"ızgara rehber yalnızca programı olan kanalları listeler ({window.RowCount} satır)");
        Check(window.Rows.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem { ActualHeight: > 40 }, "rehber satırları çizilir");
        Check(window.TimeScroll.Maximum > 1000, $"saat ekseni kaydırılabilir ({window.TimeScroll.Maximum:0} px)");
        Check(window.TimeOffset > 0, $"rehber şu anki saate açılır ({window.TimeOffset:0} px)");
        SelfTest.Snapshot(window, "tüm rehber");
        var before = window.TimeOffset;
        window.ScrollTime(480);
        await SettleAsync(200);
        Check(Math.Abs(window.TimeOffset - before - 480) < 1, "saat ekseni tüm satırlarla birlikte kayar");
        window.ScrollToNow();

        if (window.Rows.Items.Count > 0 && window.Rows.Items[0] is GuideGridWindow.Row row)
        {
            var onAir = row.Cells.FirstOrDefault(cell => cell.IsOnAir);
            var past = row.Cells.FirstOrDefault(cell => cell.IsPast);
            var upcoming = row.Cells.FirstOrDefault(cell => !cell.IsPast && !cell.IsOnAir);
            Check(onAir is not null && past is not null && upcoming is not null, $"satırda geçmiş, yayında ve gelecek programlar var ({row.Cells.Count} hücre)");
            if (onAir is not null) await window.ActivateAsync(row, onAir);
            Check(played.Count == 1 && played[0].Cell is null, "yayındaki programa tıklamak kanalı canlı açar");
            if (upcoming is not null) await window.ActivateAsync(row, upcoming);
            if (past is not null) await window.ActivateAsync(row, past);
            Check(played.Count == 1, "gelecek program ve arşivsiz geçmiş program bir şey oynatmaz");
        }
        else Check(false, "ızgara rehberde en az bir satır var");
        window.Close();
        await SettleAsync(200);
    }

    private static bool IsTrimmed(TextBlock text)
    {
        var typeface = new System.Windows.Media.Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        var formatted = new System.Windows.Media.FormattedText(text.Text, System.Globalization.CultureInfo.CurrentUICulture, text.FlowDirection,
            typeface, text.FontSize, System.Windows.Media.Brushes.White, System.Windows.Media.VisualTreeHelper.GetDpi(text).PixelsPerDip);
        return formatted.WidthIncludingTrailingWhitespace > text.ActualWidth + 1;
    }

    private static string AutomationOrContent(RadioButton tab) =>
        tab.Content as string ?? System.Windows.Automation.AutomationProperties.GetName(tab);

    private void CheckLiveLayout(string when)
    {
        SelfTest.Check(ChannelList.ActualHeight > 150, $"{when}: kanal listesine yer kalır ({ChannelList.ActualHeight:0} px)");
        SelfTest.Check(!VodInfoPanel.IsVisible, $"{when}: film ayrıntı paneli canlı yayında görünmez");
        var groupsBottom = GroupList.TranslatePoint(new Point(0, GroupList.ActualHeight), NavigationPane).Y;
        SelfTest.Check(NavigationPane.ActualHeight - groupsBottom < 30,
            $"{when}: grup listesi panelin altına kadar uzanır, kaynak kartı yok ({NavigationPane.ActualHeight - groupsBottom:0} px boşluk)");
        CheckLibraryIndicator(when);
    }

    private void CheckVodLayout(string when)
    {
        SelfTest.Check(VodListTabs.ActualHeight is > 24 and < 70, $"{when}: sekme satırı normal boyda ({VodListTabs.ActualHeight:0} px)");
        SelfTest.Check(VodList.ActualHeight > 150, $"{when}: listeye yer kalır ({VodList.ActualHeight:0} px)");
        SelfTest.Check(VodCategoryPicker.IsVisible && VodSearchBox.IsVisible && VodSearchBox.ActualWidth > 120,
            $"{when}: kategori ve arama görünür (arama {VodSearchBox.ActualWidth:0} px)");
        var listBottom = VodList.TranslatePoint(new Point(0, VodList.ActualHeight), VodPane).Y;
        SelfTest.Check(VodPane.ActualHeight - listBottom < 30,
            $"{when}: liste panelin altına kadar uzanır, kaynak kartı yok ({VodPane.ActualHeight - listBottom:0} px boşluk)");
        CheckIndicator(VodListIndicator, when + ": liste sekme göstergesi");
        CheckLibraryIndicator(when);
    }

    private void CheckLibraryIndicator(string when)
    {
        var tabs = LibraryTabsPanel.Children.OfType<RadioButton>().ToArray();
        var widths = tabs.Select(tab => tab.ActualWidth).ToArray();
        SelfTest.Check(widths.Min() >= 87 && widths.Max() - widths.Min() <= 1, $"{when}: üst sekmeler eşit genişlikte");

        var selected = tabs.Single(tab => tab.IsChecked == true);
        var marker = LibraryIndicator.TranslatePoint(new Point(), LibraryTabs);
        var slot = selected.TranslatePoint(new Point(), LibraryTabs);
        var inset = LibraryTabs.Padding.Left + LibraryTabs.BorderThickness.Left;
        SelfTest.Check(Math.Abs(marker.X - slot.X) <= 1 && Math.Abs(marker.Y - slot.Y) <= 1 &&
            Math.Abs(LibraryIndicator.ActualWidth - selected.ActualWidth) <= 1 && LibraryIndicator.ActualHeight >= 31 &&
            marker.X >= inset - 1 && marker.X + LibraryIndicator.ActualWidth <= LibraryTabs.ActualWidth - inset + 1 &&
            marker.Y >= inset - 1 && marker.Y + LibraryIndicator.ActualHeight <= LibraryTabs.ActualHeight - inset + 1,
            $"{when}: üst sekme vurgusu seçili sekmeyle hizalı ve kapsülün içinde");
    }

    private static void CheckIndicator(FrameworkElement indicator, string what) =>
        SelfTest.Check(!double.IsNaN(indicator.Width) && indicator.Width is > 20 and < 400 && indicator.ActualHeight is > 16 and < 60,
            $"{what} yerleşti ({indicator.Width:0}×{indicator.ActualHeight:0})");
}
