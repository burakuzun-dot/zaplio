# Değişiklikler

Sürüm numaraları [Anlamsal Sürümleme](https://semver.org/lang/tr/) kurallarını izler. Yeni değişiklikler önce
*Yayınlanmamış* bölümüne yazılır; sürüm etiketi gönderilirken bu başlık sürüm numarasıyla değiştirilir.

## Yayınlanmamış

### İyileştirmeler
- **Film ve dizilerde sarma ve duraklatma yeniden hızlı:** "Kayda hazır tut" yayının arka planda bir kopyasını
  çıkarıyordu. Bu kopya film, dizi ve arşiv programlarında her sarmada ve duraklatmada gecikmeye yol açıyordu. Artık yalnızca
  canlı yayınlarda kullanılıyor. Film ve dizide kayıt yine kalitesini bozmadan alınır; kayda başlarken ve bitirirken görüntü
  bir an yeniden yüklenir.
- **Daha küçük paket:** Zaplio'nun hiçbir yoldan ulaşamadığı VLC eklentileri (SRT, RIST, SMB, NFS, SFTP, Blu-ray, DCP,
  CD/VCD, TV kartı ve ekran yakalama erişimleri; x264, QuickSync ve MP2 kodlayıcıları; ağda kaynak bulma, müzik
  görselleştirmeleri, Chromecast, ses parmak izi ve eklenti yöneticisi) artık pakete girmiyor. Uygulama klasörü yaklaşık
  33 MB, ZIP yaklaşık 18 MB küçüldü. Çözücüler, altyazı, şifreli bağlantı ve kaydın kullandığı modüller olduğu gibi duruyor.

### Belgeler
- Kod imzalama politikası eklendi (SignPath Foundation başvurusu). `LICENSE` dosyası yalnızca MIT metnini içeriyor;
  üçüncü taraf lisans notu README ve `Licenses` klasöründe.

## 0.13.0 — 2026-11-18

### Eklenenler
- **Açık tema ve vurgu rengi:** Ayarlar › Görünüm'de koyu, açık ya da "Windows ile aynı" temayı ve altı vurgu renginden birini
  seçebilirsin. Değişiklik pencereyi yeniden açmadan hemen görünür; Vazgeç'e basarsan eski görünüm geri gelir. Açık temada da
  video alanı koyu kalır. Her tema ve renk için yazıların okunabilirliği (kontrast) testlerle denetlenir.
- **Yedekle ve taşı:** Ayarlar › Genel'den kaynakları (kullanıcı adı ve şifreleriyle), favorileri, listeleri, kaldığın yerleri,
  geçmişi ve ayarları tek bir `.zaplio` dosyasına alabilirsin. Dosya senin belirlediğin parolayla (en az 8 karakter)
  AES-256-GCM ile şifrelenir; parola hiçbir yerde saklanmaz. Başka bir bilgisayarda "Yedekten geri yükle" ile önce yedekte ne
  olduğunu görür, onaylayınca Zaplio her şeyi yerine koyup yeniden açılır.
- **Çocuk profili:** Ayarlar › Genel › Çocuk profili'nden bir PIN belirleyip her bölüm (Canlı TV, Filmler, Diziler) için hangi
  kategorilerin görüneceğini seçersin. Profil açıkken yalnızca izin verilen kategoriler listelenir; sağlayıcının sonradan
  eklediği kategoriler sen izin verene kadar gizli kalır. Kaldığın yerler ve izleme geçmişi de aynı kurala uyar. Ayarlar,
  kaynaklar ve profilden çıkmak (üst çubuktaki "Çocuk profili" işareti) PIN ister; art arda yanlış PIN girilince bekleme
  süresi uzar. Profil yeniden başlatınca da açık kalır. Kategori listeleri yalnızca bilgisayardaki kayıtlı listelerden
  okunur, sağlayıcıya ek istek gitmez.

## 0.12.0 — 2026-11-17

### Eklenenler
- **Şansıma:** kanal ve film/dizi listelerinin başlığındaki şimşeğe basınca listedeki içerikler kayan bir makarada döner,
  yavaşlar ve rastgele birinde durur. Film ya da dizi seçilirse önizlemesi açılır, kanal seçilirse oynamaya başlar. Liste
  neye süzülmüşse (kategori, arama, Favorilerim) yalnızca onların arasından seçer; arka arkaya aynı şeye düşmemeye çalışır.
  Tıklayarak ya da Esc ile beklemeden sonuca geçebilirsin. Windows'ta animasyonlar kapalıysa makara dönmeden doğrudan açılır.
- **Sinema modu:** oynatırken kumanda çubuğundaki ampule ya da **C**'ye basınca ışıklar söner: videonun çevresindeki her şey
  neredeyse siyaha döner, yalnızca görüntü ve ortam ışığı kalır. Fareyle karartılmış bir bölümün üstüne gelince o bölüm
  kullanman için aydınlanır. **C** ya da **Esc** ışıkları açar; oynatma bitince ışıklar kendiliğinden, yavaşça geri gelir.

- **Bilgisayardaki videolar:** bir video dosyasını ya da klasörü pencereye sürükle, **Ctrl + O** ile seç ya da Windows'ta
  dosyayı Zaplio ile aç; aynı oynatıcıda oynar (sinema modu, mini pencere, ikinci ekran, kaldığın yerden devam). Klasör ya da
  birden çok dosya bırakırsan videolar ad sırasıyla (Bölüm 2, Bölüm 10) art arda oynar. Oynayan videonun üstüne bırakılan
  `.srt`, `.ass` ya da `.vtt` dosyası altyazı olarak eklenir; videoyla aynı adlı altyazı kendiliğinden bulunur. Dosyalar
  bilgisayarından çıkmaz ve yolları günlüğe yazılmaz.

- **Canlı sarma önizlemesi:** film, bölüm ya da bilgisayardaki bir videoda zaman çubuğunu sürüklerken görüntü de seninle kayar;
  bıraktığın yerden oynamaya devam eder. Çubuğun üstündeki balon gittiğin zamanı gösterir. Bilgisayardaki dosyalar
  neredeyse kare kare izler; sağlayıcıdan gelen yayınlar sağlayıcıyı yormamak için en çok yarım saniyede bir atlar.
  Zaman çubuğunun üstünde tekerlek 5 saniye, **Shift + ← / →** 1 saniye kaydırır. Zaman çubuğu ve geçen süre artık
  adım adım değil, akıcı ilerler.

- **Kayıt:** oynatırken kumanda çubuğundaki kayıt düğmesine ya da **R**'ye bas; görüntü ve ses yeniden kodlanmadan, kaynakla
  birebir aynı kalitede `Videolar\Zaplio` klasörüne `.mkv` dosyası olarak kaydedilir (Matroska her kodeki taşır). Kayıt sürerken
  videonun köşesinde kırmızı **KAYIT** rozeti ve süre görünür.
  - Bilgisayardaki bir videoda oynatma hiç durmaz: kayıt başladığın ve bitirdiğin an arasını arka planda dosyadan kopyalar.
    Kayıt sıfırdan başlar ve tam bittiği yerde biter (büyük 4K VP9 videolarda kayıt videonun ortasındaki zamanla başlıyor, boş
    gibi görünüyordu; bir kez de bitiş yok sayılıp videonun sonuna kadar kopyalanmıştı).
  - Sağlayıcıdan gelen canlı yayında ve filmlerde de yayın durmaz, takılmaz, yeniden yüklenmez (**Kayda hazır tut**, Ayarlar ›
    Oynatma, varsayılan açık): yayının dokunulmamış bir kopyası yalnızca bu bilgisayarda Zaplio'ya akar; kayıt yokken atılır
    (diske bir şey yazılmaz, sağlayıcıya ek bağlantı açılmaz). Kayıt tuşu bir sonraki anahtar kareden yazmaya başlar, bitince
    dosya arka planda kayıpsız `.mkv`'ye çevrilir. Canlı yayın kendiliğinden yeniden bağlanırsa kayıt aynı dosyada sürer.
  - Ayar kapalıysa eski yönteme döner: kayıt başlarken ve biterken yayın bir an yeniden açılır, bu sırada son kare ekranda kalır.
  - Başka kanala geçince ya da oynatma bitince kayıt kendiliğinden kapanır.
- **Daha ince hız ayarı ve yavaş oynatma:** film, bölüm ve dosyalarda **,** yavaşlatır, **.** hızlandırır (0,25× ile 3× arası,
  yavaş çekimde ince adımlar). Hız tek seferde değil kısa bir geçişle değişir; ses perdesi korunur. **E** kare kare ilerletir.

- **Uygulama içinden güncelleme:** alttaki "Yeni sürüm hazır" düğmesine iki kez bas; Zaplio yeni sürümün kurulum dosyasını
  GitHub'dan kendisi indirir, GitHub'ın verdiği boyut ve SHA-256 özetiyle doğrular (tutmazsa siler, çalıştırmaz), küçük bir ilerleme
  penceresiyle sessizce kurar ve yeniden açılır. Kurulum kullanıcıya özel olduğu için yönetici izni istemez. ZIP sürümünde eskisi
  gibi sürüm sayfası açılır. Windows imzasız kurulumu engellerse Zaplio kapanmaz, nedenini söyler. (Bu özellik 0.12.0 ile gelir;
  0.11.0'dan 0.12.0'a bir kez elle geçmek gerekir.)

### Değişenler
- **Tek pencere:** Zaplio açıkken Windows'ta bir videoyu "Birlikte aç" ile açınca ikinci bir pencere açılmaz; video açık olan
  pencerede oynar ve pencere öne gelir. İki pencerenin aynı ayar dosyasına yazıp birbirinin değişikliğini silmesi de böylece önlendi.
- **Duraklatıp devam ettirmek anında:** her duraklatma ve devamda oynatıcı alanı baştan kuruluyor, konum hemen diske yazılıyor
  ve görüntü ayarları yeniden uygulanıyordu (VLC'nin görüntü çıkışını yeniden kurmasına yol açıyordu, 4K'da belirgindi). Artık
  yalnızca oynat/duraklat düğmesi değişir; konum, duraklama iki saniye sürerse kaydedilir.
- **Boşluk tuşu her zaman önce gelir:** bir şey oynarken Boşluk, odak nerede olursa olsun (düğme, liste, zaman çubuğu, ikinci ekran)
  duraklatır ve devam ettirir; önceden odaktaki düğmeye basabiliyor ya da zaman çubuğuna tıkladıktan sonra hiç çalışmıyordu.
  Yalnızca bir yazı kutusuna yazarken boşluk yazıya gider. Basılı tutunca titremez. Oynatma durmuşsa son izleneni yeniden
  açar (film kaldığı yerden). Videonun ortasında büyük bir oynat/duraklat işareti kısa süre belirir.
- **Zaman çubuğu yenilendi:** tıklama alanı genişledi, çubuk üzerine gelince kalınlaşır ve tutamaç belirir; fareyle üzerinde
  gezinince imlecin altındaki zaman balonda görünür. Zaman çubuğu ve ses kaydırıcısı tıklanınca artık tuşları kendine almaz.
  Çubuğa dışarıdan gelip basılı tutarak sürükleyince bir kez kıpırdayıp duruyor, ikinci kez tutmak gerekiyordu; artık tek basışta
  sürükleme başlar ve bırakana kadar (pencerenin dışına çıksa da) fareyi izler. Ses kaydırıcısı da aynı şekilde.
- En dar pencerede mini pencere düğmesi de gizlenir, tam ekran düğmesi sığmaya devam eder (P kısayolu çalışır).
- İndirme ve derleme belgeleri güncellendi.

## 0.11.0 — 2026-11-16

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
  sağlayıcının zaten gönderdiği ayrıntı yanıtından okunur; aynı içeriği yeniden seçince sağlayıcıya tekrar sorulmaz. Hiçbir şey
  seçili değilken panelde *İzlemeye devam et* kartları durur; bir içerik seçince yer varsa ayrıntıların altında kalır. Kartlar
  panelin genişliğini eşit sütunlarla doldurur, sığmayanlar alt satıra geçer ve yükseklik yetiyorsa kartlar büyür; sığmayan
  satırlar aşağı kaydırılır. Oynatma başlayınca panel çekilir, video sütunu doldurur.
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
- **Kaynak seçimi Ayarlar'a taşındı:** sayfaların altındaki kaynak kartı kaldırıldı; listeler ve gruplar artık pencerenin altına
  kadar uzanıyor. Aktif kaynağı Ayarlar › Kaynaklar'da listeden tıklayarak seçersin (kullanılan kaynakta *Kullanılıyor* yazar).
  *Listeleri yenile* ve Xtream abonelik bilgisi (*Aboneliği kontrol et*) de orada. Yenilemek için **F5**, ayarları açmak için
  **Ctrl + ,** kısayolları eklendi. Kayıtlı kaynakların varsa tek bir *Kaynakları yönet* düğmesi görünür.
- Film ve dizi kütüphanesi oynatıcıyla alanı dengeli paylaşır. Gezinirken önizleme 16:9 oranına göre boyutlanır.
- *İzlemeye devam et* kartları büyüdü: afiş daha büyük, başlık okunur boyutta. Dizilerde dizinin adının altında bölüm adı da yazar.
- **Düzen temizliği:** kaynak seçimi ve *Listeleri yenile* üst çubuktan kaldırıldı (şimdi Ayarlar › Kaynaklar'da). Üst çubuk inceldi, yalnızca izleme geçmişi ve ayarlar kaldı. Panel boşlukları,
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
