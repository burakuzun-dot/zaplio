# Değişiklikler

Sürüm numaraları [Anlamsal Sürümleme](https://semver.org/lang/tr/) kurallarını izler. Yeni değişiklikler önce
*Yayınlanmamış* bölümüne yazılır; sürüm etiketi gönderilirken bu başlık sürüm numarasıyla değiştirilir.

## Yayınlanmamış

### Eklenenler
- **Öz denetim** (`tools/SelfTest.ps1`): uygulama ekran dışında, geçici bir veri klasörü ve yerel bir demo listeyle kendi ekranlarını gezip
  yerleşim ve animasyon hatalarını raporlar. Gerçek veriye dokunmaz, ağ isteği yapmaz. Veri klasörü `ZAPLIO_DATA_DIR` ile değiştirilebilir.
  `-Shots <klasör>` ile gezdiği her ekranın görüntüsünü PNG olarak kaydeder.
- **İzlemeye devam et:** Filmler ve Diziler'in üstünde yarıda kalan film ve bölümlerin afişli sırası; tıklayınca önizlemesi açılır, *Devam et* ile kaldığın yerden oynar.
  Şerit fareyle sürüklenerek ya da tekerlekle sağa sola kaydırılır.
  Dizilerde her dizinin yalnızca son izlenen bölümü görünür.
- **Windows medya kartı:** oynayan kanalın veya içeriğin adı Windows medya denetimlerinde görünür. Zaplio arka plandayken de
  desteklenen oynat/duraklat, durdur ve canlı kanallar arasında geçiş komutlarını kullanabilirsin. Görev çubuğu önizlemesinde
  önceki kanal, oynat/duraklat ve sonraki kanal düğmeleri bulunur.
- **Ayrıntı paneli:** Filmler ve Diziler'de oynatıcının altındaki boş alanda seçtiğin içeriğin yılı, türü, süresi, puanı, konusu,
  yönetmeni ve oyuncuları görünür. Dizide bir bölüm seçiliyken sıradaki üç bölüm de listelenir, tıklayınca seçilir. Bilgiler
  sağlayıcının zaten gönderdiği ayrıntı yanıtından okunur; aynı içeriği yeniden seçince sağlayıcıya tekrar sorulmaz. Pencere
  yeterince uzunsa *İzlemeye devam et* de bu panele taşınır. Oynatma başlayınca panel çekilir, video sütunu doldurur.
- Film ya da bölüm oynarken başlıkta da şimşek (favori) ve *Sonra izle* düğmeleri.
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
- İzleme geçmişi koyu başlık çubuğuyla açılır. Yarım kalanları ayrı sekmede bulabilir, seçtiğin içeriğe *Devam et* ile dönebilirsin.
- Ayarlar › Kaynaklar’da kayıtlı kaynakların varsa tek bir *Kaynakları yönet* düğmesi görünür.
- Film ve dizi kütüphanesi oynatıcıyla alanı dengeli paylaşır. Gezinirken önizleme 16:9 oranına göre boyutlanır.
- *İzlemeye devam et* kartları büyüdü: afiş daha büyük, başlık okunur boyutta. Dizilerde dizinin adının altında bölüm adı da yazar.
- **Düzen temizliği:** kaynak seçimi ve *Listeleri yenile* üst çubuktan sol alttaki tek bir *Kaynak* kartına taşındı (Filmler ve Diziler'de
  liste panelinin altında); abonelik bilgisi aynı kartta. Üst çubuk inceldi, yalnızca izleme geçmişi ve ayarlar kaldı. Panel boşlukları,
  başlık boyutları ve köşe yuvarlaklıkları tek ölçeğe bağlandı. *Lisanslar* alt çubuktan Ayarlar › Hakkında'ya taşındı.
- Canlı TV başlığındaki favori yıldızı, kıvılcım animasyonlu şimşeğe dönüştü.
- Sonuna kadar izlenen film izleme listesinden kendiliğinden çıkar.
- Logo ve afiş önbelleği en çok 200 MB tutulur; eskiyen dosyalar açılışta arka planda silinir (önceden sınırsız büyüyordu).
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
- Filmler ve Diziler'de oynatıcı sütunun ortasında yüzmüyor; başlığı kütüphane başlığıyla aynı hizada, üstte duruyor.
- Dar pencerede kumanda çubuğu taşmıyor: ses kaydırıcısı gizlenir, tam ekran düğmesi görünür kalır. Rehber düğmeleri yalnızca
  simgeyle gösterilir, böylece başlık kesilmez.
- Oynatma alanının yuvarlak köşeleri ilk liste ekranında kaybolmuyor.
- Kanal seçilince oynatma alanı bir sonraki adımı gösterir. Yayın rehberi hazır değilse *Tüm rehber* düğmesi yükleme veya XMLTV kurulumunu açar.
- Filmler ve Diziler arasında geçerken diğer bölümün seçimi ve durmuş içerik başlığı taşınmaz. Önizleme düğmeleri dar alanda alt satıra geçer.
- Üstteki Canlı TV, Filmler ve Diziler sekmeleri eşit genişlikte. Seçim vurgusu kapsülün köşelerine uyuyor ve geçişte dışarı taşmıyor.
- Açılışta üstteki seçili sekmenin (Canlı TV) vurgusu görünmüyordu; ilk sekme değişimine kadar boş kalıyordu.
- "İzlemeye devam et" şeridini sürükledikten sonra imleç çift oklu kalıyor ve tıklamalar çalışmıyordu.
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
