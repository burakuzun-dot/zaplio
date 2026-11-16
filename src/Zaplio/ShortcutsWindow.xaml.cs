using System.Windows;
using System.Windows.Input;

namespace Zaplio;

internal sealed record Shortcut(string Keys, string Action);

internal sealed record ShortcutSection(string Title, IReadOnlyList<Shortcut> Items);

/// <summary>The single list of shortcuts shown by F1 (keep docs/keyboard-shortcuts.md in step with it).</summary>
internal static class Shortcuts
{
    public static IReadOnlyList<ShortcutSection> All { get; } =
    [
        new("CANLI TV",
        [
            new("Ctrl + ↑ / ↓", "Önceki / sonraki kanal"),
            new("PageUp / PageDown", "Önceki / sonraki kanal (tam ekran ve mini pencere)"),
            new("0 – 9", "Listedeki sıra numarasıyla kanal seç"),
            new("Backspace", "Bir önceki kanala dön"),
            new("Enter", "Seçili kanalı oynat"),
            new("Ctrl + F", "Aramaya git"),
            new("Esc", "Aramayı temizle")
        ]),
        new("OYNATMA",
        [
            new("F11", "Tam ekran"),
            new("Shift + F11", "İkinci ekranda oynat / geri al"),
            new("Esc", "Tam ekrandan veya mini pencereden çık"),
            new("P", "Mini pencere"),
            new("C", "Sinema modu: çevreyi karart / ışıkları aç"),
            new("A", "Görüntü oranını değiştir"),
            new("M", "Sesi kapat / aç"),
            new("S", "Ekran görüntüsü al"),
            new("R", "Kaydı başlat / durdur (kalite korunur, .mkv, Videolar\\Zaplio)"),
            new(", / .", "Yavaşlat / hızlandır (film, dizi, dosya)"),
            new("E", "Kare kare ilerle"),
            new("Boşluk", "Duraklat / devam et; her zaman önce gelir (yazı yazarken hariç). Durmuşsa son izleneni yeniden açar"),
            new("← / →", "10 saniye geri / ileri (film, dizi, arşiv)"),
            new("Shift + ← / →", "1 saniye geri / ileri; zaman çubuğunda tekerlek 5 sn"),
            new("← (canlı)", "30 saniye geri sar (arşivi olan kanallar)"),
            new("→ (canlı)", "Canlı yayına dön")
        ]),
        new("FARE",
        [
            new("Çift tık (video)", "Tam ekran; mini pencerede geri dön"),
            new("Tekerlek (mini)", "Mini pencereyi büyüt / küçült"),
            new("Çift tık (rehber)", "Geçmiş programı arşivden aç"),
            new("Sağ tık", "Kanal ve grup menüleri")
        ]),
        new("GENEL",
        [
            new("F1", "Bu pencere"),
            new("F5", "Listeleri yenile"),
            new("Ctrl + O", "Bilgisayardan video aç (sürükle-bırak da olur)"),
            new("Ctrl + ,", "Ayarlar (aktif kaynak, abonelik)")
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
