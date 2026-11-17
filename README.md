<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest">
    <img src="docs/images/zaplio-banner.gif" alt="Zaplio" width="560">
  </a>
</p>

<p align="center">
  <b>Windows için hızlı, sade ve açık kaynak medya oynatıcı.</b><br>
  Kendi M3U ve Xtream API kaynaklarını aç; aynı akışın kopyalarıyla, donan yayınlarla ve kaçan anlarla uğraşma.
</p>

<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest"><img alt="Son sürüm" src="https://img.shields.io/github/v/release/burakuzun-dot/zaplio?label=s%C3%BCr%C3%BCm&color=7B61FF&style=flat-square"></a>
  <a href="https://github.com/burakuzun-dot/zaplio/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/burakuzun-dot/zaplio/ci.yml?branch=main&label=CI&style=flat-square"></a>
  <a href="https://github.com/burakuzun-dot/zaplio/releases"><img alt="İndirme" src="https://img.shields.io/github/downloads/burakuzun-dot/zaplio/total?label=indirme&color=FFC83D&style=flat-square"></a>
  <img alt="Windows 10 ve 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0E1020?style=flat-square">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square">
  <a href="LICENSE"><img alt="Lisans: MIT" src="https://img.shields.io/badge/lisans-MIT-C4B8FF?style=flat-square"></a>
</p>

<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest"><b>⬇ İndir</b></a> ·
  <a href="#-neden-zaplio">Neden Zaplio?</a> ·
  <a href="docs/features.md">Tüm özellikler</a> ·
  <a href="docs/keyboard-shortcuts.md">Kısayollar</a> ·
  <a href="#-sık-sorulanlar">SSS</a> ·
  <a href="CHANGELOG.md">Değişiklikler</a>
</p>

> [!IMPORTANT]
> **Zaplio yalnızca bir oynatıcıdır.** Kanal, liste ya da abonelik sağlamaz, satmaz ve önermez. Kendi M3U listeni ya da yasal olarak
> erişim hakkına sahip olduğun bir Xtream API hesabını eklersin; eklediğin içeriklerden sen sorumlusun ([Hukuki bilgilendirme](docs/legal.md)).
> Zaplio'yu yalnızca bu deponun [Sürümler](https://github.com/burakuzun-dot/zaplio/releases) sayfasından indir.

<!-- Ekran görüntüleri: demo içerikle çekilen görüntüleri docs/images/ altına ekleyip bu bloğu aç.
<p align="center">
  <img src="docs/images/ekran-canli.png" alt="Canlı TV" width="860">
</p>
-->

## ⚡ Neden Zaplio?

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>🎚️ Akıllı kalite, yedek yayın</h3>
      Listende <i>Kanal A HD / FHD / 4K / H.265</i> diye dört satır yok: hepsi <b>tek satır</b>. Zaplio bağlantı hızına göre doğru
      kaliteyi seçer; yayın açılmaz ya da koparsa hata göstermek yerine <b>diğer kaliteye kendiliğinden geçer</b>.
    </td>
    <td width="50%" valign="top">
      <h3>⏸️ Canlı yayını durdur, geri sar</h3>
      Telefon mu çaldı? <kbd>Boşluk</kbd> ile canlı yayını durdur, kaldığın yerden devam et. Arşivi olan kanallarda
      <kbd>←</kbd> ile 30 saniye geri sar, kaçırdığın sahneyi yeniden izle; <b>Canlıya dön</b> tek tık.
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>🚀 Bekletmeyen açılış</h3>
      Kanal, film ve rehber listeleri diskte şifreli önbellekte: uygulama <b>sunucuyu beklemeden</b> açılır. VLC motoru arka planda
      hazırlanır; ilk kanal yarım saniyenin altında başlar. Kopan yayına otomatik yeniden bağlanır.
    </td>
    <td width="50%" valign="top">
      <h3>🖥️ İkinci ekran ve mini pencere</h3>
      Yayın diğer monitörde tam ekran, kanal listesi ve rehber ana ekranda (<kbd>Shift</kbd>+<kbd>F11</kbd>). Ya da her zaman üstte
      duran mini pencere (<kbd>P</kbd>). Alt çubukta bağlantı durumu ve aboneliğinin kaç gün kaldığı.
    </td>
  </tr>
</table>

## ⬇ İndir

| Windows 10 / 11 · 64 bit | |
|---|---|
| **Kurulum** | `Zaplio-<sürüm>-setup.exe` — yönetici izni istemez, Başlat menüsüne ekler |
| **Taşınabilir** | `Zaplio-<sürüm>-win-x64.zip` — bir klasöre çıkar, `Zaplio.exe`'yi çalıştır |

İki paket de .NET ve VLC'yi içinde taşır, ayrıca bir şey kurman gerekmez.

> [!NOTE]
> Paketler henüz kod imzalı değil. Windows *Akıllı Uygulama Denetimi* açıksa uygulamayı engelleyebilir. Ücretsiz açık kaynak
> imzası için SignPath Foundation'a başvuruldu; bkz. [kod imzalama politikası](docs/code-signing.md).

## 🏁 3 adımda başla

1. **Kaynak ekle** — sağ üstteki **Ayarlar** (⚙) → **Kaynaklar** → *Kaynak ekle*: M3U dosyası, M3U bağlantısı ya da Xtream (sunucu, kullanıcı adı, şifre). İlk açılışta ekrandaki *Liste ekle* düğmesi de aynı yere götürür.
2. **Kanal seç** — tıkla ya da <kbd>Ctrl</kbd>+<kbd>↑</kbd>/<kbd>↓</kbd> ile gez, rakamla numaradan aç; <kbd>Backspace</kbd> önceki kanal.
3. **Rehbere bak** — **Rehber** düğmesiyle programları gör; *ARŞİV* etiketli geçmiş programa çift tıkla.

Tüm kısayollar uygulamada <kbd>F1</kbd>'de ve [docs/keyboard-shortcuts.md](docs/keyboard-shortcuts.md)'de.

## 🧩 Özellikler

<details>
<summary><b>Kaynaklar ve canlı TV</b></summary>

- M3U / M3U8 (dosya veya bağlantı; `tvg-*`, `group-title`, user-agent/referrer, `x-tvg-url`) ve Xtream API
- Birden fazla kaynak, kaynak başına hatırlanan grup / arama / kanal
- Gruplar, Türkçe karakter duyarsız arama, favoriler ve sıralama, kanal logoları
- Grup gizleme, kanal yeniden adlandırma, aynı kanalın kalitelerini birleştirme ve elle sabitleme
- Xtream abonelik bilgisi (Ayarlar › Kaynaklar): bitiş tarihi, bağlantı sayısı, son haftada uyarı

</details>

<details>
<summary><b>Rehber ve arşiv</b></summary>

- XMLTV (`.xml`, `.xml.gz`), şimdi / sırada özeti, 7 günlük program
- Xtream arşivi: geçmiş programları izle, rehberde arşiv süresi kadar geriye git
- Canlı yayında <kbd>←</kbd> ile arşivden geri sarma

</details>

<details>
<summary><b>Filmler ve diziler</b></summary>

- Afişler, kategoriler, arama, detay penceresi
- Kaldığın yerden devam, izlendi işareti, sonraki bölüm
- Liste önbelleği: sekme anında açılır

</details>

<details>
<summary><b>Oynatma</b></summary>

- VLC 3 motoru; ses ve altyazı parçası, görüntü oranı, kırpma, ses/altyazı gecikmesi, oynatma hızı
- Canlı yayını duraklatma (zaman kaydırma), ekran görüntüsü
- Tam ekran, mini pencere, ikinci ekran
- Otomatik yeniden bağlanma, yedek kaliteye geçiş, bağlantı durumu ve anlık hız göstergesi

</details>

Ayrıntılı liste: [docs/features.md](docs/features.md)

## ❓ Sık sorulanlar

<details>
<summary><b>"Sağlayıcı erişimi reddetti (403)" diyor</b></summary>

Çoğunlukla bilgisayarda açık bir **VPN** (ör. Cloudflare WARP) yüzündendir; birçok sağlayıcı VPN bağlantılarını reddeder. VPN'i kapatıp
tekrar dene. Devam ederse bağlantı sınırın dolmuş, IP adresin geçici olarak engellenmiş ya da aboneliğin bitmiş olabilir
(Ayarlar › Kaynaklar'daki abonelik bilgisine bak).
</details>

<details>
<summary><b>Windows uygulamayı açmıyor / "engellendi" diyor</b></summary>

Windows *Akıllı Uygulama Denetimi* açıksa imzasız paketlerden yüklenen Zaplio'nun açılmasını engelleyebilir.
</details>

<details>
<summary><b>Geri sarma çalışmıyor</b></summary>

Geri sarma için sağlayıcının o kanalda **arşiv** tutması gerekir (rehberde *ARŞİV* etiketi görünür). Arşivi olmayan kanallarda
yalnızca duraklatıp kaldığın yerden devam edebilirsin.
</details>

<details>
<summary><b>Aynı kanalın kalitelerinin birleşmesini istemiyorum</b></summary>

Ayarlar → *Aynı kanalın kalitelerini tek satırda birleştir* anahtarını kapat. Kalite tercihini (Otomatik / En yüksek / Veri
tasarrufu) aynı yerden değiştirebilir, bir kanalın kalitesini sağ tık → *Kalite / yayın* ile sabitleyebilirsin.
</details>

<details>
<summary><b>Verilerim nerede? Zaplio'yu kaldırınca silinir mi?</b></summary>

Kaynakların, favorilerin ve önbellek `%LocalAppData%\Zaplio` klasöründe, Windows hesabına bağlı olarak şifreli durur.
Zaplio'yu kaldırmak bu klasörü silmez; tamamen temizlemek istersen klasörü elle sil.
</details>

## 🔒 Gizlilik

- Kaynak bilgilerin ve önbellek Windows DPAPI ile yalnızca senin Windows hesabına bağlı olarak şifrelenir.
- Hata günlüklerinde kullanıcı adı ve şifreler otomatik gizlenir.
- **Kullanım verisi toplanmaz.** Zaplio'nun kendi sunucusu yoktur.
- Yalnızca şunlarla iletişim kurulur: eklediğin kaynaklar, kaynak listelerinde adı geçen logo/afiş sunucuları
  (Ayarlar'dan kapatılır) ve sürüm denetimi için GitHub (Ayarlar'dan kapatılır). Bu sunucular IP adresini görebilir.
- Sürüm dosyalarının nasıl imzalanacağı: [Kod imzalama politikası](docs/code-signing.md).
- Telif, markalar ve sorumluluk: [Hukuki bilgilendirme](docs/legal.md).

## 🛠️ Geliştirme

```powershell
dotnet run --project src/Zaplio      # uygulamayı çalıştır
dotnet test                         # birim testleri
```

.NET 10 SDK yeterli; VLC NuGet'ten gelir. Ayrıntılar: [docs/building.md](docs/building.md) ·
Hukuki bilgilendirme: [docs/legal.md](docs/legal.md) · Katkı kuralları: [CONTRIBUTING.md](CONTRIBUTING.md) · Kodlama ajanları: [AGENTS.md](AGENTS.md)

Hata bildirirken `Ayarlar → Günlük klasörünü aç` ile açılan o günün `.log` dosyasını eklemek çözümü çok hızlandırır.
[Hata bildir](https://github.com/burakuzun-dot/zaplio/issues/new/choose) · Güvenlik açığı: [SECURITY.md](SECURITY.md)

## 📄 Lisans

Zaplio [MIT lisansı](LICENSE) ile dağıtılır. Kullandığı açık kaynak bileşenler (VLC / LibVLCSharp — LGPL-2.1,
Inter — OFL-1.1, Fluent System Icons — MIT) ve lisans metinleri [Licenses](src/Zaplio/Licenses/ThirdPartyNotices.txt) klasöründedir.

<p align="center"><sub>Zaplio bir yayın sağlayıcısı değildir ve herhangi bir sağlayıcı, yayın kuruluşu ya da markayla bağlantısı yoktur.</sub></p>
