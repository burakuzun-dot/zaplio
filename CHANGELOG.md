# Değişiklikler

Sürüm numaraları [Anlamsal Sürümleme](https://semver.org/lang/tr/) kurallarını izler. Yeni değişiklikler önce
*Yayınlanmamış* bölümüne yazılır; sürüm etiketi gönderilirken bu başlık sürüm numarasıyla değiştirilir.

## Yayınlanmamış

## 0.10.2 — 2026-11-14

### Düzeltilenler
- İkinci ekranda oynatırken ana pencere küçültülünce video penceresi de kayboluyordu; artık ikinci ekran bağımsız kalır.

## 0.10.1 — 2026-11-14

### Eklenenler
- **Hukuki bilgilendirme:** ilk kaynak eklenirken içerik sorumluluğu uyarısı (bir kez onaylanır, Ayarlar → Hakkında → *Hukuki*
  bölümünden yeniden okunur); [docs/legal.md](docs/legal.md).
- Gizlilik belgesi: iletişim kurulan üçüncü taraf sunucular (logo/afiş adresleri, sürüm denetimi) açıkça yazıldı.

### Değişenler
- Zaplio artık bir **medya oynatıcı** olarak tanıtılıyor; "Xtream Codes" yerine "Xtream API" yazılıyor (kaynak türü etiketi dahil).
- Belgelerde, ipuçlarında ve örneklerde gerçek yayıncı ve kanal adları kaldırıldı; testler bu kuralı denetliyor.

## 0.10.0 — 2026-11-14

Zaplio'nun ilk herkese açık sürümü.

### Özellikler
- **Kaynaklar:** M3U / M3U8 (dosya ya da bağlantı) ve Xtream API; birden fazla kaynak, kaynak başına hatırlanan görünüm.
- **Canlı TV:** gruplar, Türkçe karakter duyarsız arama, favoriler ve sıralama, kanal logoları, grup gizleme, kanal yeniden
  adlandırma. Kanal değiştirme kısayolları (`Ctrl`+`↑`/`↓`, rakamlar, `Backspace`) ve ekranda kanal bilgisi.
- **Akıllı kalite ve yedek yayın:** aynı kanalın HD / FHD / 4K / HEVC kopyaları tek satırda; bağlantı hızına göre uygun kalite
  seçilir, sağ tık → *Kalite / yayın* ile sabitlenebilir. Açılmayan ya da kopan yayında otomatik olarak diğer kaliteye geçilir.
- **Canlı yayını duraklat ve geri sar:** `Boşluk` canlı yayını duraklatır (kaldığın yerden devam); arşivi olan kanallarda `←`
  30 saniye geri sarar. *Canlıya dön* düğmesi ve `→` canlı yayına getirir.
- **Yayın rehberi ve arşiv:** XMLTV desteği, şimdi / sırada özeti, 7 günlük program; Xtream arşivi olan kanallarda geçmiş
  programlar *ARŞİV* etiketiyle görünür ve izlenir.
- **Filmler ve diziler:** afişler, kategoriler, arama, detay penceresi, kaldığın yerden devam, izlendi işareti, sonraki bölüm.
- **Oynatma:** VLC motoru; ses ve altyazı parçası, görüntü oranı, kırpma, ses/altyazı gecikmesi, oynatma hızı, ekran görüntüsü.
- **İkinci ekran ve mini pencere:** yayın diğer monitörde tam ekran açılır, kanal listesi ve rehber ana pencerede kalır
  (`Shift`+`F11`); ya da her zaman üstte duran mini pencere (`P`).
- **Abonelik kartı:** Xtream hesabının bitiş tarihi ve bağlantı sayısı sol altta; son 7 günde uyarı, tıklayınca yenilenir.
- **Dayanıklılık:** kopan yayına otomatik yeniden bağlanma, bağlantı durumu göstergesi ve anlık indirme / yükleme hızı;
  sağlayıcının reddettiği yayın art arda denenmez, hızlı kanal değiştirirken yalnızca durulan kanal açılır.
- **Hız:** kanal, film ve rehber listeleri diskte şifreli önbellekte; VLC motoru arka planda hazırlanır, ilk kanal
  yarım saniyenin altında başlar.
- **Görsel kimlik:** Zaplio simgesi ve logosu, animasyonlu açılış (tıkla ya da tuşa bas: atla; Ayarlar'dan kapatılabilir),
  koyu tema, <kbd>F1</kbd> ile kısayol listesi.
- **Bakım:** hata günlüğü (kullanıcı adı ve şifreler gizlenir), isteğe bağlı güncelleme denetimi, önbelleği temizleme.

### Geliştirme
- Depo düzeni: uygulama kodu, `Assets` ve `Licenses` → `src/Zaplio/`; kökte `Zaplio.slnx` (`dotnet build` ve `dotnet test`
  kökten çalışır); belgeler `docs/` altında.
- Birim testleri; GitHub Actions: CI, CodeQL ve etiketle çalışan sürüm iş akışı (kurulum dosyası + taşınabilir ZIP).
- `.gitignore`: sertifika, anahtar, `.env`, günlük ve M3U listeleri kazara commit edilemez.
