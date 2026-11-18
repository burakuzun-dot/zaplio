using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace Zaplio;

/// <summary>
/// Sets up the kids profile for the active source: its PIN and which categories of each section a child may see.
/// Nothing is changed until Kaydet, "Çocuk profilini aç" or "Çocuk profilinden çık" is pressed.
/// </summary>
public partial class ChildProfileWindow : Window
{
    internal enum Outcome { Save, Start, Stop }

    internal sealed class CategoryRow(string category, bool allowed)
    {
        public string Category { get; } = category;
        public string Label { get; } = category.Length == 0 ? Lang.T(ChildProfile.Uncategorized) : DisplayText.CleanLabel(category);
        public bool IsAllowed { get; set; } = allowed;
    }

    private sealed record SectionChoice(LibrarySection Section, string Label);

    private static readonly StringComparer Comparer = StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);
    private readonly Dictionary<LibrarySection, CategoryRow[]?> _rows = new();
    private readonly bool _hasPin;
    private readonly bool _active;

    /// <param name="categories">
    /// Each section the source offers, with its categories; null when that list is not loaded yet (the section is then
    /// shown with a hint and what was allowed before stays as it is).
    /// </param>
    internal ChildProfileWindow(string sourceName, ChildProfile profile, Guid source,
        IReadOnlyDictionary<LibrarySection, IReadOnlyList<string>?> categories)
    {
        InitializeComponent();
        _hasPin = profile.HasPin;
        _active = profile.Active;
        foreach (var (section, names) in categories)
        {
            var allowed = profile.AllowedIn(source, section).ToHashSet(Comparer);
            _rows[section] = names?.Distinct(Comparer).OrderBy(name => name.Length == 0 ? 1 : 0).ThenBy(name => name, Comparer)
                .Select(name => new CategoryRow(name, allowed.Contains(name))).ToArray();
        }

        SectionChoice[] choices = [.. categories.Keys.Order().Select(section => new SectionChoice(section, section switch
        {
            LibrarySection.Movies => Lang.T("Filmler"),
            LibrarySection.Series => Lang.T("Diziler"),
            _ => Lang.T("Canlı TV")
        }))];
        SectionPicker.ItemsSource = choices;
        SectionPicker.SelectedItem = choices.FirstOrDefault();
        Tag = sourceName;

        PinState.Text = _hasPin ? Lang.T("PIN belirlendi.") : Lang.T("Önce bir PIN belirle.");
        ChangePinButton.Visibility = _hasPin ? Visibility.Visible : Visibility.Collapsed;
        PinPanel.Visibility = _hasPin ? Visibility.Collapsed : Visibility.Visible;
        StopButton.Visibility = _active ? Visibility.Visible : Visibility.Collapsed;
        // While the profile is on, saving is the main action; "aç" would mean nothing.
        StartButton.Visibility = _active ? Visibility.Collapsed : Visibility.Visible;
        if (_active)
        {
            SaveButton.IsDefault = true;
            SaveButton.Margin = new Thickness(0);
        }
        Loaded += (_, _) => (_hasPin ? (IInputElement)Search : Pin).Focus();
    }

    internal Outcome Result { get; private set; }

    /// <summary>A new PIN typed in this window, or null to keep the one there is.</summary>
    internal string? NewPin { get; private set; }

    /// <summary>Allowed categories of every section whose list was shown.</summary>
    internal IReadOnlyDictionary<LibrarySection, string[]> Allowed =>
        _rows.Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!.Where(row => row.IsAllowed).Select(row => row.Category).ToArray());

    private LibrarySection? Section => (SectionPicker.SelectedItem as SectionChoice)?.Section;

    private void OnSectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        Search.Text = "";
        ShowRows();
    }

    private void OnSearch(object sender, System.Windows.Controls.TextChangedEventArgs e) => ShowRows();

    private CategoryRow[] VisibleRows()
    {
        if (Section is not { } section || _rows.GetValueOrDefault(section) is not { } rows) return [];
        var query = Search.Text.Trim();
        return query.Length == 0 ? rows
            : rows.Where(row => row.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private void ShowRows()
    {
        var loaded = Section is { } section && _rows.GetValueOrDefault(section) is not null;
        var rows = VisibleRows();
        Categories.ItemsSource = rows;
        CategoryScroll.ScrollToTop();
        Search.IsEnabled = AllowAll.IsEnabled = AllowNone.IsEnabled = loaded;
        EmptyText.Text = !loaded
            ? Lang.F($"{(SectionPicker.SelectedItem as SectionChoice)?.Label} listesi henüz yüklenmedi. Ana pencerede bu bölümü bir kez aç; kategoriler burada görünür. O zamana kadar bu bölümde önceki izinler geçerli.")
            : rows.Length == 0 ? Lang.T("Bu aramaya uyan kategori yok.") : "";
        EmptyText.Visibility = EmptyText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCount();
    }

    private void OnToggled(object sender, RoutedEventArgs e) => UpdateCount();

    private void UpdateCount()
    {
        var rows = Section is { } section ? _rows.GetValueOrDefault(section) : null;
        AllowedCount.Text = rows is null ? "" : Lang.F($"{rows.Count(row => row.IsAllowed)} / {rows.Length} kategoriye izin var");
    }

    private void OnAllowAll(object sender, RoutedEventArgs e) => SetVisible(true);
    private void OnAllowNone(object sender, RoutedEventArgs e) => SetVisible(false);

    private void SetVisible(bool allowed)
    {
        foreach (var row in VisibleRows()) row.IsAllowed = allowed;
        var rows = VisibleRows();
        Categories.ItemsSource = null;
        Categories.ItemsSource = rows;
        UpdateCount();
    }

    private void OnChangePin(object sender, RoutedEventArgs e)
    {
        PinPanel.Visibility = Visibility.Visible;
        ChangePinButton.Visibility = Visibility.Collapsed;
        Pin.Focus();
    }

    private void OnPinChanged(object sender, RoutedEventArgs e) => ShowProblem(null);

    private void OnSave(object sender, RoutedEventArgs e) => Finish(Outcome.Save);
    private void OnStart(object sender, RoutedEventArgs e) => Finish(Outcome.Start);
    private void OnStop(object sender, RoutedEventArgs e) => Finish(Outcome.Stop);

    private void Finish(Outcome outcome)
    {
        string? pin = null;
        var changingPin = PinPanel.Visibility == Visibility.Visible && (!_hasPin || Pin.Password.Length > 0 || PinAgain.Password.Length > 0);
        if (changingPin && outcome != Outcome.Stop)
        {
            if (!ChildProfile.IsValidPin(Pin.Password)) { ShowProblem(Lang.T("PIN 4–8 rakamdan oluşmalı.")); return; }
            if (Pin.Password != PinAgain.Password) { ShowProblem(Lang.T("İki PIN aynı değil.")); return; }
            pin = Pin.Password;
        }
        if (outcome == Outcome.Start && !_rows.Values.Any(rows => rows?.Any(row => row.IsAllowed) == true))
        {
            ShowProblem(Lang.T("En az bir kategoriye izin ver; yoksa çocuk profilinde hiçbir şey görünmez."));
            return;
        }
        NewPin = pin;
        Pin.Clear();
        PinAgain.Clear();
        Result = outcome;
        DialogResult = true;
    }

    private void ShowProblem(string? text)
    {
        Problem.Text = text;
        Problem.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
