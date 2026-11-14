# Zaplio — geliştirici ve yapay zekâ ajanı kuralları

Bu dosya, depoda çalışan insanlar ve kodlama ajanları için kısa bir rehberdir.

## Proje
- Windows için WPF medya oynatıcısı (M3U ve Xtream API kaynakları), .NET 10, oynatma motoru LibVLCSharp / VLC 3.
- `MainWindow` birden fazla dosyaya bölünmüş tek sınıftır (`MainWindow.Guide.cs`, `MainWindow.Library.cs` …).
  Arayüzden bağımsız mantık ayrı sınıflardadır: `CatalogLoader` (M3U/Xtream), `XmlTvLoader`, `CatalogCache`, `StreamHealth`,
  `LogoLoader`, `AppLog`, `UpdateChecker`. Yeni mantığı mümkünse bu tarz ayrı, test edilebilir sınıflara koy.

## Komutlar
| İş | Komut |
|---|---|
| Çalıştır | `dotnet run --project src/Zaplio` |
| Derle | `dotnet build` (kökten; uyarısız olmalı) |
| Test | `dotnet test` |
| Paket | bkz. [docs/building.md](docs/building.md) |

Uygulama kodu `src/Zaplio/`, testler `tests/Zaplio.Tests/` altındadır; kökteki `Zaplio.slnx` ikisini birlikte açar.
Köke kod dosyası koyma.

## Kurallar
- **Gizli bilgi yok:** kullanıcı adı, şifre ve liste/sağlayıcı adresleri asla commit edilmez, ekranda ya da günlükte açık yazılmaz.
  Günlüğe yalnızca `AppLog` ile yaz; o, adreslerdeki hesap bilgilerini maskeler.
- Kalıcı kullanıcı verisi `LocalState` + DPAPI ile, büyük önbellekler `CatalogCache` ile şifreli saklanır.
- Sağlayıcıya gereksiz istek atma: yeniden deneme, toplu indirme ve test betikleri sağlayıcıların IP engellemesine yol açabilir.
- **Hukuk:** örnek, test ve belge metinlerinde gerçek yayıncı/kanal/marka adı kullanma (`Kanal A`, `Belgesel 1`); "IPTV" ve "Xtream Codes"
  yerine "medya oynatıcı" ve "Xtream API" yaz; spor/ücretli içerik çağrışımı yapma; varsayılan kaynak ya da liste ekleme.
  Bkz. [docs/legal.md](docs/legal.md).
- Kullanıcıya görünen tüm metinler Türkçedir; mevcut üslubu ("sen" dili, kısa cümleler) koru.
- Uzun işler UI iş parçacığını kilitlememeli (`Task.Run`, `async`); kapanışta `_lifetime` iptal belirteci kullanılır.
- Yeni mantık için `tests/Zaplio.Tests` altına test ekle; testler kullanıcının gerçek klasörlerine dokunmamalı.
- Değişiklikleri `CHANGELOG.md` → *Yayınlanmamış* altına yaz.

## Windows notu
Akıllı Uygulama Denetimi açık makinelerde imzasız yeni derlemeler engellenir; imzalama notları depoda tutulmaz.
