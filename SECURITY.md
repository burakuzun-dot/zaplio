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
