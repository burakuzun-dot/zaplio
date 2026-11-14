using System.Windows;
using System.Windows.Controls;

namespace Zaplio;

public partial class MainWindow
{
    private int _customizationVersion;
    private (LoadedCatalog? Catalog, int Version, IReadOnlyList<Channel> Channels) _customizedView;
    /// <summary>Every variant id → id of the list row that stands for it (same id when not merged).</summary>
    private Dictionary<string, string> _rowIdByVariant = new();
    /// <summary>Every variant id → its quality group (only for channels that have alternatives).</summary>
    private Dictionary<string, VariantGroup> _groupByVariant = new();

    private static string GroupKey(Guid sourceId, string group) => $"{sourceId:N}:{group}";

    private bool IsGroupHidden(string group) =>
        _activeSourceId is Guid id && _state.HiddenGroups.Contains(GroupKey(id, group));

    private string[] HiddenGroupsOfActiveSource()
    {
        if (_activeSourceId is not Guid id) return [];
        var prefix = $"{id:N}:";
        return _state.HiddenGroups.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..]).Order(CategoryComparer).ToArray();
    }

    /// <summary>
    /// The source's channels as the list shows them: hidden groups removed, quality variants merged into one row
    /// (the variant that suits the connection, or the one the user picked) and user renames applied.
    /// </summary>
    private IReadOnlyList<Channel> CustomizedChannels(Guid sourceId)
    {
        if (_catalog.GetValueOrDefault(sourceId) is not { } catalog) return [];
        if (ReferenceEquals(_customizedView.Catalog, catalog) && _customizedView.Version == _customizationVersion)
            return _customizedView.Channels;

        var prefix = $"{sourceId:N}:";
        var hidden = _state.HiddenGroups.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..]).ToHashSet(CategoryComparer);
        var visible = hidden.Count == 0 ? catalog.Channels
            : catalog.Channels.Where(channel => channel.Categories.Length == 0 || !channel.Categories.All(hidden.Contains)).ToList();

        var rows = new List<Channel>(visible.Count);
        var rowIds = new Dictionary<string, string>(visible.Count);
        var groups = new Dictionary<string, VariantGroup>();
        var mode = _state.Settings.QualityMode;
        IEnumerable<VariantGroup> variantGroups = _state.Settings.MergeQualities
            ? ChannelVariants.Group(visible)
            : visible.Select(channel => new VariantGroup(channel.Id, channel.Name, [channel]));
        foreach (var group in variantGroups)
        {
            var pinned = _state.PreferredVariants.GetValueOrDefault(VariantPreferenceKey(sourceId, group.Key));
            var chosen = group.HasAlternatives ? ChannelVariants.Choose(group, mode, _peakDownload, pinned) : group.Channels[0];
            var renamed = group.Channels.Select(channel => _state.ChannelNames.GetValueOrDefault(channel.Id))
                .FirstOrDefault(name => name is not null);
            var row = chosen;
            if (group.HasAlternatives)
            {
                var label = ChannelVariants.Quality(chosen.Name).Label;
                var categories = string.Join(";", group.Channels.SelectMany(channel => channel.Categories).Distinct(CategoryComparer));
                row = new Channel(chosen.Id, chosen.SourceId, renamed ?? group.DisplayName, categories, chosen.Stream,
                    chosen.UserAgent, chosen.Referrer,
                    chosen.GuideId ?? group.Channels.Select(channel => channel.GuideId).FirstOrDefault(id => id is not null),
                    chosen.GuideName ?? chosen.Name, chosen.IsOnDemand, chosen.SeriesId, chosen.EpisodeId,
                    chosen.Logo ?? group.Channels.Select(channel => channel.Logo).FirstOrDefault(logo => logo is not null),
                    group.Channels.Max(channel => channel.CatchupDays),
                    Badge: $"{(label.Length > 0 ? label + " · " : "")}{group.Channels.Count} seçenek");
                foreach (var channel in group.Channels) groups[channel.Id] = group;
            }
            else if (renamed is not null)
            {
                // Keep the provider's name for EPG matching.
                row = chosen with { Name = renamed, GuideName = chosen.GuideName ?? chosen.Name };
            }
            foreach (var channel in group.Channels) rowIds[channel.Id] = row.Id;
            rows.Add(row);
        }

        _rowIdByVariant = rowIds;
        _groupByVariant = groups;
        _customizedView = (catalog, _customizationVersion, rows);
        return rows;
    }

    private static string VariantPreferenceKey(Guid sourceId, string variantKey) => $"{sourceId:N}:{variantKey}";

    /// <summary>The list row id for any variant id (a playing fallback stream still highlights its row).</summary>
    private string? RowId(string? id) => id is null ? null : _rowIdByVariant.GetValueOrDefault(id, id);

    private IReadOnlyList<Channel> VariantsOf(Channel channel) =>
        _groupByVariant.TryGetValue(channel.Id, out var group) ? group.Channels : [channel];

    /// <summary>A merged row is a favourite when any of its variants was starred.</summary>
    private bool IsFavorite(Channel channel) =>
        _state.Favorites.Contains(channel.Id) || VariantsOf(channel).Any(variant => _state.Favorites.Contains(variant.Id));

    private string FavoriteId(Channel channel) =>
        VariantsOf(channel).Select(variant => variant.Id).FirstOrDefault(_state.Favorites.Contains) ?? channel.Id;

    /// <summary>Adds or removes a favourite (removing clears every variant and its manual position); returns the undo.</summary>
    private Action ToggleFavorite(Channel channel)
    {
        var favorites = _state.Favorites.ToArray();
        var order = _state.FavoriteOrder.ToArray();
        if (IsFavorite(channel))
        {
            foreach (var variant in VariantsOf(channel).Append(channel))
            {
                _state.Favorites.Remove(variant.Id);
                _state.FavoriteOrder.Remove(variant.Id);
            }
        }
        else _state.Favorites.Add(channel.Id);
        return () =>
        {
            _state.Favorites.Clear();
            _state.Favorites.UnionWith(favorites);
            _state.FavoriteOrder.Clear();
            _state.FavoriteOrder.AddRange(order);
        };
    }

    private IEnumerable<Channel> OrderFavorites(IEnumerable<Channel> favorites)
    {
        var order = _state.FavoriteOrder.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
        return favorites.Select((channel, index) => (channel, index))
            .OrderBy(item => order.TryGetValue(FavoriteId(item.channel), out var position) ? position : int.MaxValue)
            .ThenBy(item => item.index)
            .Select(item => item.channel);
    }

    /// <summary>Applies a state change, persists it and rolls it back if saving fails.</summary>
    private bool ChangeCustomization(Action apply, Action rollback, string? done = null)
    {
        apply();
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            rollback();
            SetFooter(exception.Message, isError: true);
            return false;
        }
        _customizationVersion++;
        var selected = (ChannelList.SelectedItem as Channel)?.Id;
        UpdateCatalog(selectedChannel: selected);
        if (done is not null) SetFooter(done);
        return true;
    }

    private void OnGroupMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = GroupList.ContextMenu!;
        menu.Items.Clear();
        var entry = (e.OriginalSource as DependencyObject) is { } target &&
            ItemsControl.ContainerFromElement(GroupList, target) is ListBoxItem { DataContext: GroupEntry item } ? item : null;

        if (_activeSourceId is Guid sourceId && entry?.Key.StartsWith("group:", StringComparison.Ordinal) == true)
        {
            var key = GroupKey(sourceId, entry.Label);
            Add(menu.Items, $"“{entry.Label}” grubunu gizle", () => ChangeCustomization(
                () => _state.HiddenGroups.Add(key), () => _state.HiddenGroups.Remove(key),
                $"“{entry.Label}” gizlendi. Sağ tıklayıp “Gizlenen gruplar” menüsünden geri getirebilirsin."));
        }

        var hidden = HiddenGroupsOfActiveSource();
        if (hidden.Length > 0 && _activeSourceId is Guid activeId)
        {
            var restore = new MenuItem { Header = $"Gizlenen gruplar ({hidden.Length})" };
            foreach (var group in hidden)
            {
                var key = GroupKey(activeId, group);
                Add(restore.Items, group, () => ChangeCustomization(
                    () => _state.HiddenGroups.Remove(key), () => _state.HiddenGroups.Add(key), $"“{group}” yeniden gösteriliyor."));
            }
            restore.Items.Add(new Separator());
            var keys = hidden.Select(group => GroupKey(activeId, group)).ToArray();
            Add(restore.Items, "Tümünü göster", () => ChangeCustomization(
                () => _state.HiddenGroups.ExceptWith(keys), () => _state.HiddenGroups.UnionWith(keys), "Tüm gruplar yeniden gösteriliyor."));
            menu.Items.Add(restore);
        }

        if (menu.Items.Count == 0) e.Handled = true;
    }

    private void OnChannelMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = ChannelList.ContextMenu!;
        menu.Items.Clear();
        if ((e.OriginalSource as DependencyObject) is not { } target ||
            ItemsControl.ContainerFromElement(ChannelList, target) is not ListBoxItem { DataContext: Channel channel })
        {
            e.Handled = true;
            return;
        }
        ChannelList.SelectedItem = channel;

        Add(menu.Items, "Oynat", () => _ = PlayChannelAsync(channel));
        if (BuildQualityMenu(channel) is { } quality) menu.Items.Add(quality);
        Add(menu.Items, IsFavorite(channel) ? "Favorilerden çıkar" : "Favorilere ekle", () =>
        {
            Action? undo = null;
            ChangeCustomization(() => undo = ToggleFavorite(channel), () => undo?.Invoke());
        });
        menu.Items.Add(new Separator());
        Add(menu.Items, "Yeniden adlandır…", () => RenameChannel(channel));
        var renamed = VariantsOf(channel).Append(channel).Where(variant => _state.ChannelNames.ContainsKey(variant.Id))
            .Select(variant => variant.Id).Distinct().ToArray();
        if (renamed.Length > 0)
        {
            var names = renamed.ToDictionary(id => id, id => _state.ChannelNames[id]);
            Add(menu.Items, "Özgün adı geri yükle", () => ChangeCustomization(
                () => { foreach (var id in renamed) _state.ChannelNames.Remove(id); },
                () => { foreach (var (id, name) in names) _state.ChannelNames[id] = name; },
                "Kanalın özgün adı geri yüklendi."));
        }

        if ((GroupList.SelectedItem as GroupEntry)?.Key == "favorites")
        {
            menu.Items.Add(new Separator());
            var visible = VisibleChannels;
            var index = visible.FindIndex(item => item.Id == channel.Id);
            Add(menu.Items, "Yukarı taşı", () => MoveFavorite(visible, index, -1), index > 0);
            Add(menu.Items, "Aşağı taşı", () => MoveFavorite(visible, index, 1), index >= 0 && index < visible.Count - 1);
            Add(menu.Items, "En üste taşı", () => MoveFavorite(visible, index, -index), index > 0);
        }
    }

    private void MoveFavorite(List<Channel> visible, int index, int offset)
    {
        var target = index + offset;
        if (index < 0 || target < 0 || target >= visible.Count) return;
        var previous = _state.FavoriteOrder.ToArray();
        // Persist the full visible order so moves stay stable even for favourites that were never ordered.
        var order = visible.Select(FavoriteId).ToList();
        var moved = visible[index].Id;
        var movedKey = order[index];
        order.RemoveAt(index);
        order.Insert(target, movedKey);
        var others = _state.FavoriteOrder.Where(id => !order.Contains(id)).ToArray();
        ChangeCustomization(() =>
        {
            _state.FavoriteOrder.Clear();
            _state.FavoriteOrder.AddRange(order.Concat(others));
        }, () =>
        {
            _state.FavoriteOrder.Clear();
            _state.FavoriteOrder.AddRange(previous);
        });
        ChannelList.SelectedItem = VisibleChannels.FirstOrDefault(channel => channel.Id == moved);
    }

    private void RenameChannel(Channel channel)
    {
        var variants = VariantsOf(channel);
        var original = variants.Count > 1
            ? ChannelVariants.DisplayName(variants[0].Name)
            : _catalog.GetValueOrDefault(channel.SourceId)?.Channels.FirstOrDefault(item => item.Id == channel.Id)?.Name ?? channel.Name;
        var dialog = new TextPromptWindow("Kanalı yeniden adlandır", $"Özgün ad: {original}", channel.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var name = dialog.Value.Trim();
        // A merged row keeps one custom name, stored on the variant it currently shows.
        var previous = variants.Append(channel).Select(variant => variant.Id).Distinct()
            .Where(_state.ChannelNames.ContainsKey).ToDictionary(id => id, id => _state.ChannelNames[id]);
        if ((name.Length == 0 || name == original) && previous.Count == 0) return;
        void Restore()
        {
            foreach (var id in variants.Select(variant => variant.Id).Append(channel.Id)) _state.ChannelNames.Remove(id);
            foreach (var (id, value) in previous) _state.ChannelNames[id] = value;
        }
        ChangeCustomization(() =>
        {
            foreach (var id in previous.Keys) _state.ChannelNames.Remove(id);
            if (name.Length > 0 && name != original) _state.ChannelNames[channel.Id] = name;
        }, Restore, name.Length == 0 || name == original ? "Kanalın özgün adı geri yüklendi." : $"Kanal adı “{name}” olarak değiştirildi.");
        if (RowId(_playingChannel?.Id) == channel.Id) UpdateSelection();
    }

    /// <summary>"Kalite / yayın" submenu for a merged channel; null when the channel has no alternatives.</summary>
    private MenuItem? BuildQualityMenu(Channel row)
    {
        if (!_groupByVariant.TryGetValue(row.Id, out var group) || !group.HasAlternatives) return null;
        var key = VariantPreferenceKey(row.SourceId, group.Key);
        var pinned = _state.PreferredVariants.GetValueOrDefault(key);
        var playing = _playingChannel?.Id;
        var menu = new MenuItem { Header = "Kalite / yayın" };

        var automatic = new MenuItem { Header = "Otomatik (bağlantıya göre)", IsCheckable = true, IsChecked = pinned is null };
        automatic.Click += (_, _) => PinVariant(row, group, null);
        menu.Items.Add(automatic);
        menu.Items.Add(new Separator());
        foreach (var variant in group.Channels)
        {
            var label = ChannelVariants.Quality(variant.Name).Label;
            var header = $"{(label.Length > 0 ? label : "Standart")}  ·  {variant.Name}" + (variant.Id == playing ? "  ▶" : "");
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = pinned == variant.Id };
            item.Click += (_, _) => PinVariant(row, group, variant.Id);
            menu.Items.Add(item);
        }
        return menu;
    }

    /// <summary>Remembers the chosen variant (null = automatic) and switches the stream if this channel is playing.</summary>
    private void PinVariant(Channel row, VariantGroup group, string? variantId)
    {
        var key = VariantPreferenceKey(row.SourceId, group.Key);
        var hadPin = _state.PreferredVariants.TryGetValue(key, out var previous);
        var wasPlaying = _playingChannel is { } current && group.Channels.Any(variant => variant.Id == current.Id);
        var changed = ChangeCustomization(() =>
        {
            if (variantId is null) _state.PreferredVariants.Remove(key);
            else _state.PreferredVariants[key] = variantId;
        }, () =>
        {
            if (hadPin) _state.PreferredVariants[key] = previous!;
            else _state.PreferredVariants.Remove(key);
        }, variantId is null ? "Kalite otomatik seçilecek." : "Kalite tercihi kaydedildi.");
        if (!changed || !wasPlaying) return;
        var target = ActiveChannels.FirstOrDefault(channel => channel.Id == RowId(group.Channels[0].Id));
        if (target is not null && target.Id != _playingChannel?.Id) _ = PlayChannelAsync(target);
    }

    private static void Add(ItemCollection items, string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => action();
        items.Add(item);
    }
}
