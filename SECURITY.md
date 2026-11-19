# Security policy

*Türkçesi aşağıda.*

## Supported versions
Security fixes are released for the [latest version](https://github.com/burakuzun-dot/zaplio/releases/latest) only.

## Reporting a vulnerability
**Do not open a public issue for a vulnerability.** Use GitHub's private form instead: the repository's **Security** tab →
**Report a vulnerability**. Include the affected Zaplio and Windows versions, what the issue is with steps to reproduce, and
its possible impact. I aim to answer within 7 days and to fix confirmed issues in a patch release as soon as possible.

## Scope
Especially: source details (username, password, playlist address) exposed on disk, in the log or on screen; a malicious
M3U / XMLTV / Xtream response running code in or crashing the app; the update check being sent to a wrong release.
Problems in the providers' own systems are out of scope.

---

# Güvenlik politikası

## Desteklenen sürümler
Güvenlik düzeltmeleri yalnızca [en son sürüm](https://github.com/burakuzun-dot/zaplio/releases/latest) için yayınlanır.

## Güvenlik açığı bildirmek
Güvenlik açıklarını **herkese açık issue olarak açma.** Bunun yerine GitHub'ın özel bildirim formunu kullan:
deponun **Security** sekmesi → **Report a vulnerability**.

Bildirimde şunlara yer ver:
- Etkilenen sürüm ve Windows sürümü
- Açığın ne olduğu ve yeniden üretme adımları
- Olası etkisi (ör. hesap bilgilerinin açığa çıkması, uzaktan kod çalıştırma)

Bildirimi en geç 7 gün içinde yanıtlamaya, doğrulanan açıkları en kısa sürede bir yama sürümüyle düzeltmeye çalışırım.

## Kapsam
Özellikle önemli olanlar:
- Kaynak bilgilerinin (kullanıcı adı, şifre, liste adresi) diskte, günlükte ya da ekranda açığa çıkması
- Kötü niyetli bir M3U / XMLTV / Xtream yanıtının uygulamada kod çalıştırması ya da çökertmesi
- Güncelleme denetiminin yanlış bir sürüme yönlendirilmesi

Yayın sağlayıcılarının kendi sistemlerindeki sorunlar bu politikanın kapsamı dışındadır.
