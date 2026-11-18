# Derleme, test ve sürüm

## Gerekenler
- Windows 10 / 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Çalıştırma

```powershell
dotnet run --project src/Zaplio
```

İlk derlemede VLC'nin eklenti önbelleği (`libvlc\win-x64\plugins\plugins.dat`) bir kez oluşturulur; bu birkaç saniye ile
yarım dakika sürebilir ve sonraki açılışları hızlandırır.

## Testler

```powershell
dotnet test
```

Testler M3U ayrıştırıcıyı, arşiv adreslerini, günlük maskelemeyi, bağlantı durumu kararlarını ve şifreli önbelleği kapsar.
Önbellek testleri geçici bir klasör kullanır.

> Kökteki `Zaplio.slnx` uygulamayı ve testleri birlikte açar: `dotnet build` ve `dotnet test` kökten çalışır; Visual Studio'da da bu dosyayı aç.

## Paket

```powershell
dotnet publish src/Zaplio -c Release -r win-x64 --self-contained true -o artifacts\Zaplio-0.14.1-win-x64
```

Kurulum dosyası için [Inno Setup 6](https://jrsoftware.org/isinfo.php) ile `installer\Zaplio.iss` derlenir.

## Sürüm yayınlama (GitHub Actions)

1. `CHANGELOG.md` dosyasındaki *Yayınlanmamış* bölümünü yeni sürüm numarasıyla adlandır.
2. Etiketi gönder:
   ```powershell
   git tag v0.14.1
   git push origin v0.14.1
   ```
3. *Release* iş akışı testleri çalıştırır, taşınabilir ZIP ve kurulum dosyasını üretir ve GitHub sürümünü oluşturur.
   Sürüm notları `CHANGELOG.md` dosyasındaki o sürümün bölümünden alınır; bölüm yoksa sürüm oluşturulmaz.

Uygulama içi güncelleme denetimi en son **ön sürüm olmayan** GitHub sürümüne bakar; etiket `v` + sürüm biçiminde olmalıdır.
Depo gizliyse uygulama sürümleri göremez.

## Klasör yapısı

| Yol | İçerik |
|---|---|
| `src/Zaplio/` | Uygulama (WPF, .NET 10). `MainWindow.*.cs` dosyaları ana pencerenin bölümleridir |
| `src/Zaplio/Assets/` | Simge, marka görselleri, açılış animasyonu ve yazı tipleri |
| `src/Zaplio/Licenses/` | Üçüncü taraf lisansları (pakete kopyalanır) |
| `tests/Zaplio.Tests` | xUnit birim testleri |
| `tools/` | VLC eklenti önbelleği ve kod imzalama betikleri |
| `installer/` | Inno Setup betiği |
| `docs/` | Belgeler ve görseller |
| `.github/` | CI, sürüm, CodeQL iş akışları; issue ve PR şablonları; Dependabot |
