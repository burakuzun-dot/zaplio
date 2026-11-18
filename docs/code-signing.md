# Kod imzalama politikası

> **Durum:** Zaplio, açık kaynak projelere ücretsiz kod imzalama sağlayan [SignPath Foundation](https://signpath.org)'a
> başvuru aşamasındadır. Onaylanana kadar sürümler imzasızdır.
>
> *Code signing policy — Zaplio has applied to the SignPath Foundation. Until approved, releases are unsigned.
> Once approved: free code signing provided by [SignPath.io](https://signpath.io), certificate by
> [SignPath Foundation](https://signpath.org).*

## Neler imzalanır

- Yalnızca bu depodaki kaynak koddan, GitHub Actions'taki *Release* iş akışıyla derlenen dosyalar imzalanır:
  `Zaplio.exe` ve kurulum dosyası `Zaplio-<sürüm>-setup.exe`.
- Elle, yerel bir bilgisayarda derlenen hiçbir dosya imzalanmaz.
- Paketteki üçüncü taraf dosyalar (VLC / LibVLC) kendi yayıncılarının imzasını taşır ya da imzasız kalır; Zaplio adına
  yeniden imzalanmaz.

## Roller

| Rol | Kişi |
|---|---|
| Yazarlar ve gözden geçirenler (*committers and reviewers*) | [burakuzun-dot](https://github.com/burakuzun-dot) |
| İmzayı onaylayan (*approvers*) | [burakuzun-dot](https://github.com/burakuzun-dot) |

Dışarıdan gelen her katkı birleştirilmeden önce gözden geçirilir.

## Gizlilik

Zaplio, kullanıcı ya da kurulumu yapan kişi istemedikçe başka sistemlere bilgi göndermez. İstisnalar şunlardır ve hepsi
Ayarlar'dan kapatılabilir ya da kullanıcının kendi eklediği adreslerdir:

- Kullanıcının eklediği M3U ve Xtream API kaynakları ile bu listelerde adı geçen logo ve afiş sunucuları.
- Yeni sürüm denetimi için GitHub (Ayarlar › Genel › Yeni sürümü denetle).

Kullanım verisi toplanmaz; Zaplio'nun kendi sunucusu yoktur. Ayrıntılar: [README › Gizlilik](../README.tr.md#-gizlilik) · [Privacy](../README.md#-privacy).

*Privacy: this program will not transfer any information to other networked systems unless specifically requested by
the user or the person installing or operating it. The only connections are the playlist sources the user adds (and the
logo and poster servers named in them) and an optional update check against GitHub; both can be turned off in Settings.*
