using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace Zaplio;

internal enum AppLanguage { System, Turkish, English }

/// <summary>
/// The interface language. Turkish text stays where it is written and is its own key; <see cref="English"/> maps each
/// Turkish text to its English version. Code wraps user-visible text in <see cref="T"/> (plain) or <see cref="F"/>
/// (interpolated: the format with its {0}… placeholders is the key). XAML needs nothing: every element is translated as it
/// loads (<see cref="Hook"/>). Text without an English entry simply stays Turkish; a test keeps that list empty.
/// </summary>
internal static partial class Lang
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-GB");

    public static bool IsEnglish { get; private set; }

    /// <summary>Culture for dates and numbers shown to the user.</summary>
    public static CultureInfo Culture => IsEnglish ? EnglishCulture : TurkishCulture;

    /// <summary>"Windows ile aynı": Turkish on a Turkish Windows, English everywhere else.</summary>
    public static bool Resolve(AppLanguage language) => language switch
    {
        AppLanguage.English => true,
        AppLanguage.Turkish => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "tr"
    };

    /// <summary>Set once at start-up; a change needs a restart (windows already built keep their text).</summary>
    public static void Use(AppLanguage language) => IsEnglish = Resolve(language);

    /// <summary>For tests: switch without touching Windows' language.</summary>
    internal static void UseEnglish(bool english) => IsEnglish = english;

    public static string T(string turkish) =>
        IsEnglish && English.TryGetValue(turkish, out var english) ? english : turkish;

    public static string F(FormattableString text) =>
        string.Format(Culture, IsEnglish && English.TryGetValue(text.Format, out var english) ? english : text.Format, text.GetArguments());

    /// <summary>
    /// Translates XAML text when a window loads (its whole tree, collapsed pages included), and menus and tooltips as they
    /// open. WPF sends Loaded only to elements with a handler of their own, so it cannot be caught element by element.
    /// </summary>
    public static void Hook()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnMenuOpened));
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.ToolTipOpeningEvent, new ToolTipEventHandler(OnToolTipOpening));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (IsEnglish && sender is Window window) TranslateTree(window);
    }

    private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (IsEnglish && sender is DependencyObject element) Translate(element);
    }

    /// <summary>Every element under <paramref name="root"/>: the logical tree (pages not shown yet) and the visual tree (templates).</summary>
    internal static void TranslateTree(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>([root]);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (!seen.Add(element)) continue;
            Translate(element);
            foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) pending.Push(child);
            if (element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D)
                for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
                    pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(element, i));
        }
    }

    private static void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        if (!IsEnglish || sender is not ContextMenu menu) return;
        foreach (var item in Descendants(menu)) Translate(item);
    }

    private static IEnumerable<DependencyObject> Descendants(ItemsControl menu)
    {
        foreach (var item in menu.Items.OfType<DependencyObject>())
        {
            yield return item;
            if (item is ItemsControl inner)
                foreach (var child in Descendants(inner)) yield return child;
        }
    }

    private static readonly DependencyProperty[] TextProperties =
    [
        TextBlock.TextProperty, Run.TextProperty, ContentControl.ContentProperty, HeaderedItemsControl.HeaderProperty,
        HeaderedContentControl.HeaderProperty, FrameworkElement.ToolTipProperty, FrameworkContentElement.ToolTipProperty,
        AutomationProperties.NameProperty, AutomationProperties.HelpTextProperty, Window.TitleProperty
    ];

    /// <summary>Replaces Turkish text set in XAML (never bound or computed values) with its English version.</summary>
    internal static void Translate(DependencyObject element)
    {
        foreach (var property in TextProperties)
        {
            // Only text written in XAML is a local string value; bindings and styles are left alone.
            if (element.ReadLocalValue(property) is not string text || !English.TryGetValue(text, out var english)) continue;
            try { element.SetCurrentValue(property, english); }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { }
        }
    }
}
