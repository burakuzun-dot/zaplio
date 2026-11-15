# Özellikler

## Kaynaklar
- **M3U / M3U8** — dosya ya da bağlantı. `tvg-id`, `tvg-name`, `tvg-logo`, `group-title` (`;` ile birden fazla grup), `#EXTGRP`,
  `#EXTVLCOPT` ile user-agent ve referrer, `x-tvg-url` ile rehber adresi desteklenir.
- **Xtream API** — canlı kanallar, filmler, diziler, rehber (`xmltv.php`) ve arşiv.
- Birden fazla kaynak eklenebilir; sol alttaki *Kaynak* kartından geçilir (yanındaki düğme listeleri yeniler). Her kaynak için grup, arama ve seçili kanal hatırlanır.

## Canlı TV
- **Abonelik kartı** (Xtream): sol altta bitiş tarihi ve açık/izin verilen bağlantı sayısı; son 7 günde uyarı, tıklayınca yenilenir.
- Gruplar, Türkçe karakter duyarsız arama, favoriler.
- Kanal logoları (diskte 30 gün önbellek; Ayarlar'dan kapatılabilir).
- Sağ tık menüsü: favorilere ekle/çıkar, **yeniden adlandır**, özgün adı geri yükle, favorileri **sırala**.
- Grup sağ tık menüsü: **grubu gizle**, gizlenen grupları geri getir.
- Kanal değiştirme kısayolları ve ekranda kanal bilgisi (numara, ad, şu anki program).

## Akıllı kalite ve yedek yayın
- Aynı kanalın kopyaları (*Kanal A HD / FHD / 4K / H.265*) tek satırda görünür; satırdaki etiket seçili kaliteyi ve seçenek sayısını gösterir.
- **Otomatik** modda bu oturumda ölçülen en yüksek indirme hızına göre seçilir (≥35 Mb/s 4K, ≥10 Mb/s FHD, ≥4 Mb/s HD).
  Ayarlar'dan *En yüksek* ya da *Veri tasarrufu* seçilebilir; birleştirme tamamen kapatılabilir.
- Sağ tık → *Kalite / yayın*: bir kaliteyi sabitle ya da otomatiğe döndür. Aynı menü oynatırken *Seçenekler*'de de var.
- Yayın açılmaz ya da kalıcı olarak koparsa önce daha düşük kaliteye, sonra diğerlerine geçilir (en fazla 2 deneme).

## Canlı yayını duraklat ve geri sar
- `Boşluk` ya da *Duraklat* canlı yayını durdurur; VLC yayını arkada geçici bir dosyaya yazar, devam edince kaldığın yerden oynar.
  Alt çubukta "canlıdan X geride" yazar.
- Arşivi olan Xtream kanallarında `←` 30 saniye geri sarar; arşiv film gibi oynar, `←`/`→` ile 10 saniye sarılır.
- *Canlıya dön* (ya da canlı yayında `→`) en güncel yayına getirir.

## İkinci ekran
- *2. ekran* düğmesi ya da `Shift`+`F11`: yayın diğer monitörde tam ekran açılır; ana pencerede liste ve rehber kullanılabilir.
- Kanal değiştirme kısayolları her iki ekranda da çalışır. `Esc` ya da çift tık videoyu ana pencereye geri alır.

## Yayın rehberi
- XMLTV (`.xml`, `.xml.gz`) — kaynağın bildirdiği ya da elle girilen adres/dosya.
- Şimdi / sırada özeti, 7 günlük program listesi, program açıklamaları.
- **Arşiv (catch-up):** sağlayıcının arşiv tuttuğu Xtream kanallarında geçmiş programlar *ARŞİV* etiketiyle görünür;
  çift tıklayınca film gibi (sarılabilir) oynar. Saat, sunucunun saat dilimine çevrilir.
- Rehber 6 saat önbellekte tutulur; yenile düğmesi sunucudan yeniden indirir.

## Filmler ve diziler (Xtream)
- Afişler, kategoriler, arama, detay penceresi (özet, yıl, sezon sayısı).
- Kaldığın yerden devam, izlendi işareti, *Sonraki bölüm* düğmesi.
- Liste önbelleği: sekme anında açılır, eskiyse arka planda yenilenir.

## Oynatma
- VLC 3 motoru; açılışta arka planda hazırlanır ve eklenti önbelleği sayesinde ilk oynatma bekletmez.
- *Seçenekler* menüsü: ses ve altyazı parçası, görüntü oranı, kırpma, ses/altyazı gecikmesi, oynatma hızı (film/dizi), ekran görüntüsü.
- Tam ekran, mini pencere (her zaman üstte, sürüklenebilir, tekerlekle boyutlanır).
- Ses düzeyi ve sessiz, otomatik oynatma seçenekleri.

## Bağlantı ve dayanıklılık
- Kopan canlı yayına 3 kez artan aralıklarla otomatik yeniden bağlanma. Sunucunun en baştan reddettiği yayın tekrar denenmez.
- Alt çubukta **bağlantı durumu** (Stabil / Dalgalı / Zayıf) ve bilgisayarın anlık **indirme / yükleme hızı**.
- 401 / 403 / 429 gibi sağlayıcı yanıtları için anlaşılır mesajlar (ör. VPN uyarısı).

## Bakım
- Hata günlüğü (`%LocalAppData%\Zaplio\logs`, 14 gün), şifreler maskelenir.
- Açılışta isteğe bağlı güncelleme denetimi (GitHub Releases).
- Önbelleği temizle ve günlük klasörünü aç düğmeleri.
