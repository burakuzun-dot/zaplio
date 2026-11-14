# Zaplio'ya katkı

Zaplio'ya ilgin için teşekkürler! Hata bildirimi, öneri ve kod katkısı memnuniyetle karşılanır.

## Hata bildirmek ve öneri yapmak
- [Yeni issue](https://github.com/burakuzun-dot/zaplio/issues/new/choose) açarken formu doldur.
- Hata bildirimine o günün günlük dosyasını ekle: *Ayarlar → Günlük klasörünü aç* → `zaplio-YYYYMMDD.log`.
  Günlükte kullanıcı adı ve şifreler zaten gizlenir. Yine de göndermeden önce bir göz at.
- **Kullanıcı adı, şifre ya da liste/sağlayıcı adresi paylaşma.**
- Kanal, liste ya da sağlayıcı talepleri kapatılır: Zaplio yalnızca bir oynatıcıdır.

## Kod katkısı
1. Büyük bir değişiklikten önce issue açıp fikrini yaz; aynı işin iki kez yapılmasını önler.
2. Depoyu fork'la, bir dal aç (`ozellik/kisa-ad` ya da `duzeltme/kisa-ad`).
3. Değişikliği yap:
   ```powershell
   dotnet build                       # uyarısız olmalı
   dotnet test                        # testler geçmeli
   ```
4. Yeni mantık için `tests/Zaplio.Tests` altına test ekle.
5. Kullanıcının fark edeceği değişiklikleri `CHANGELOG.md` → *Yayınlanmamış* altına yaz.
6. PR aç; şablondaki kontrol listesini doldur.

## Kurallar
- Kullanıcıya görünen metinler Türkçe; "sen" dili, kısa ve net cümleler.
- Gizli bilgi asla commit edilmez, ekrana ya da günlüğe açık yazılmaz. Günlüğe yalnızca `AppLog` ile yazılır.
- Örnek, test ve belge metinlerinde gerçek yayıncı, kanal ya da marka adı kullanma; kurgusal adlar kullan (Kanal A, Belgesel 1).
  Liste, sağlayıcı ya da kanal ekleyen katkılar kabul edilmez.
- Uzun işler arayüzü kilitlememeli (`async` / `Task.Run`).
- Sağlayıcılara gereksiz istek atılmaz: yeniden deneme ve toplu indirme IP engellemesine yol açabilir.
- Arayüzden bağımsız mantık ayrı, test edilebilir sınıflara konur.

Ayrıntılı geliştirici notları: [AGENTS.md](AGENTS.md) · Derleme: [docs/building.md](docs/building.md)

Katkın [MIT lisansı](LICENSE) altında yayımlanır.
