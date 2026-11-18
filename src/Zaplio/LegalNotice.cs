namespace Zaplio;

/// <summary>The content-responsibility notice shown before the first source is added and in Settings → About.</summary>
internal static class LegalNotice
{
    public static string Title => Lang.T("Önemli: içerik sorumluluğu");

    public static IReadOnlyList<string> Paragraphs =>
    [
        Lang.T("Zaplio bir medya oynatıcıdır. Kanal, liste, abonelik ya da başka içerik sağlamaz, satmaz ve önermez."),
        Lang.T("Yalnızca erişim hakkına sahip olduğun kaynakları ekle: kendi aboneliğin, kendi dosyaların, serbest lisanslı ya da herkese açık yayınlar."),
        Lang.T("Eklediğin kaynakların içeriğinden, telif hakkı dahil, sen sorumlusun. Hakkın olmayan yayınları izlemek için Zaplio'yu kullanma."),
        Lang.T("Zaplio yayın içeriğini kendiliğinden kaydetmez, dağıtmaz ya da başkalarına iletmez; yayınlar doğrudan senin sağlayıcınla bilgisayarın arasında akar.")
    ];

    public static string MoreInfo => Lang.T("Ayrıntılar: depodaki \"Hukuki bilgilendirme\" belgesi (docs/legal.md).");
}
