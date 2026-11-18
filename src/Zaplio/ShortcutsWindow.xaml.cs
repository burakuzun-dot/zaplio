using System.Windows;
using System.Windows.Input;

namespace Zaplio;

internal sealed record Shortcut(string Keys, string Action);

internal sealed record ShortcutSection(string Title, IReadOnlyList<Shortcut> Items);

/// <summary>The single list of shortcuts shown by F1 (keep docs/keyboard-shortcuts.md in step with it).</summary>
internal static class Shortcuts
{
    /// <summary>Built on each read so the texts follow the interface language.</summary>
    public static IReadOnlyList<ShortcutSection> All =>
    [
        new(Lang.T("CANLI TV"),
        [
            new("Ctrl + ↑ / ↓", Lang.T("Önceki / sonraki kanal")),
            new("PageUp / PageDown", Lang.T("Önceki / sonraki kanal (tam ekran ve mini pencere)")),
            new("0 – 9", Lang.T("Listedeki sıra numarasıyla kanal seç")),
            new("Backspace", Lang.T("Bir önceki kanala dön")),
            new("Enter", Lang.T("Seçili kanalı oynat")),
            new("Ctrl + F", Lang.T("Aramaya git")),
            new("Esc", Lang.T("Aramayı temizle"))
        ]),
        new(Lang.T("OYNATMA"),
        [
            new("F11", Lang.T("Tam ekran")),
            new("Shift + F11", Lang.T("İkinci ekranda oynat / geri al")),
            new("Esc", Lang.T("Tam ekrandan veya mini pencereden çık")),
            new("P", Lang.T("Mini pencere")),
            new("C", Lang.T("Sinema modu: çevreyi karart / ışıkları aç")),
            new("A", Lang.T("Görüntü oranını değiştir")),
            new("M", Lang.T("Sesi kapat / aç")),
            new("S", Lang.T("Ekran görüntüsü al")),
            new("R", Lang.T("Kaydı başlat / durdur (kalite korunur, .mkv, Videolar\\Zaplio)")),
            new(", / .", Lang.T("Yavaşlat / hızlandır (film, dizi, dosya)")),
            new("E", Lang.T("Kare kare ilerle")),
            new(Lang.T("Boşluk"), Lang.T("Duraklat / devam et; her zaman önce gelir (yazı yazarken hariç). Durmuşsa son izleneni yeniden açar")),
            new("← / →", Lang.T("10 saniye geri / ileri (film, dizi, arşiv)")),
            new("Shift + ← / →", Lang.T("1 saniye geri / ileri; zaman çubuğunda tekerlek 5 sn")),
            new(Lang.T("← (canlı)"), Lang.T("30 saniye geri sar (arşivi olan kanallar)")),
            new(Lang.T("→ (canlı)"), Lang.T("Canlı yayına dön"))
        ]),
        new(Lang.T("FARE"),
        [
            new(Lang.T("Çift tık (video)"), Lang.T("Tam ekran; mini pencerede geri dön")),
            new(Lang.T("Tekerlek (mini)"), Lang.T("Mini pencereyi büyüt / küçült")),
            new(Lang.T("Çift tık (rehber)"), Lang.T("Geçmiş programı arşivden aç")),
            new(Lang.T("Sağ tık"), Lang.T("Kanal ve grup menüleri"))
        ]),
        new(Lang.T("GENEL"),
        [
            new("F1", Lang.T("Bu pencere")),
            new("F5", Lang.T("Listeleri yenile")),
            new("Ctrl + O", Lang.T("Bilgisayardan video aç (sürükle-bırak da olur)")),
            new("Ctrl + ,", Lang.T("Ayarlar (aktif kaynak, abonelik)"))
        ])
    ];
}

public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();
        Sections.ItemsSource = Shortcuts.All;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.F1) Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e) => DragMove();
}
