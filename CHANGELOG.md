# Değişiklikler

Sürüm numaraları [Anlamsal Sürümleme](https://semver.org/lang/tr/) kurallarını izler. Yeni değişiklikler önce
*Yayınlanmamış* bölümüne yazılır; sürüm etiketi gönderilirken bu başlık sürüm numarasıyla değiştirilir.

## Yayınlanmamış

### Eklenenler
- Film, dizi ve bölümleri seçince afişi ve mevcut detayları oynatma alanında görünür. Oynatmayı karttaki düğmeyle başlatabilirsin.
- Canlı kanal geçişlerine kısa karıncalanma, pencere kapanışına eski televizyonlardaki gibi sönme efekti eklendi.
- Oynayan videodan alınan renkler pencerenin zeminine hafifçe yansıyor. Efekti Seçenekler menüsünden kapatabilirsin.
- **Filmler ve dizilerde favoriler ve "Sonra izle":** listenin üstünde *Tümü · Favorilerim · İzleme listem* sekmeleri var. Satırlarda kanallardaki
  gibi şimşek düğmesi (kıvılcım animasyonuyla) ve afişin köşesinde favori rozeti; seçilen içerikte "Oynat"ın yanında şimşek ve *Sonra izle*,
  sağ tık menüsünde de aynı seçenekler.
- **Animasyonlar ve canlı arka plan:** oynatma alanı boşken yavaşça kayan mor-lacivert ışıklar ve süzülen kıvılcımlar; Canlı TV / Filmler / Diziler
  arasında panel kayarak beliriyor, grup ya da kategori değişince satırlar sırayla geliyor, afiş ve logolar yüklenince yumuşakça beliriyor.
  Düğmeler basınca hafifçe içeri çöküyor, ana düğmeler üzerine gelince parlıyor, sekmelerde seçili işaret kayarak yer değiştiriyor.
  Listeler yüklenirken boş ekran yerine parıldayan iskelet satırlar ve atan bir şimşek görünüyor. Windows'ta animasyonlar kapalıysa hiçbiri oynamaz.
- Kanal satırlarında favori şimşek düğmesi ve logonun köşesinde favori rozeti; favoriye eklerken kıvılcım animasyonu oynar.
- **Kanal uykuda** ekranı: yayın açılamayınca gösterilir; "Kanalı uyandır" ve "Sonraki kanal" düğmeleri vardır. Sağlayıcıyı yormamak için
  kendiliğinden en çok iki kez yeniden dener, sonra seni bekler.
- **Bağlantı koptu** ekranı: internet gidince yayın durur, bağlantı geri gelince kaldığın kanal yeniden açılır.
- İlk liste ekranı: hiç kaynak yokken "İlk M3U listeni ekle" ekranı gösterilir.

### Değişenler
- **Ayarlar yenilendi:** solda bölümler (Kaynaklar, Oynatma, Görünüm, Genel, Hakkında), her ayarın altında kısa bir açıklama.
  **Kaynak ekleme ayarlara taşındı;** başlık çubuğundaki *Kaynaklar* düğmesi kaldırıldı. Ortam ışığı da Ayarlar › Görünüm'den açılıp kapanabiliyor.
- Oynatma çubuğundaki ve başlıktaki düğmeler tek bir stile geçti: ses, ayarlar, mini pencere, 2. ekran ve tam ekran tek kapsülde,
  üzerine gelince yumuşakça doluyor. Rehber ve favori düğmeleri aynı boyutta.
- Kanal satırlarındaki favori şimşeği yalnızca üzerine gelince, satır seçiliyken ya da favoriyse görünür.
- Sağlayıcının kategori adlarının başındaki ▶, ★ gibi süslemeler listede gösterilmiyor (gruplama etkilenmez).
- Başlık biraz küçüldü, alt bilgi satırı büyüdü. Ortam ışığı paneller üzerinde daha hafif.
- **Ortam ışığı** yenilendi: ışık artık dört kenardan (üst, alt, sol, sağ) içeri doğru yayılıyor, renkler daha canlı, geçişler daha
  akıcı; sahne kesilince ışık sıçramıyor. Video duraklayınca ışık kısılıyor.
- Açılış animasyonu şeffaf bir pencerede gösteriliyor. Animasyonlar kapalıysa şeffaf son kare kullanılıyor.
- Film ve dizi ekranında afişler büyütüldü, içerik alanı genişletildi ve üstteki sekmeler bir araya getirildi.
- Ana pencere ve oynatma alanının köşeleri yuvarlatıldı. Oynatma çubuğu ve düğmeleri daha yumuşak bir görünüme kavuştu.

### Düzeltilenler
- Video ikinci ekrana taşınırken ve geri alınırken yayın bağlantısı, oynatma konumu ve duraklatma durumu korunur.

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
