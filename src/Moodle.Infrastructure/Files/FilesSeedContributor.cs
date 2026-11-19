using System.Text;
using Moodle.Application.Files;
using Moodle.Application.Seeding;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Örnek veriyi (<c>ortam/ornek-veri.feature</c>) FILE tablolarına yükler: <c>ogretmen1</c>'in özel
/// dosyalarına, performans ölçümü (k6, T-FILE-15) ve elle deneme için tek bir örnek dosya ekler.
/// Ders dosyaları (<c>component="course"</c>) burada eklenmez — o alanın politikası henüz kayıtlı
/// değil (Fatma Zehra'nın kapsamı). Tekrar çalıştırılabilir: hedef alan her seferinde değiştirilir
/// (<c>replace: true</c>), böylece yinelenen kayıt birikmez.
/// </summary>
public sealed class FilesSeedContributor(IFileService files) : ISeedContributor
{
    public int Order => 30;
    public string Name => "FILE: örnek özel dosya";

    public async Task SeedAsync(SeedDataset data, CancellationToken ct = default)
    {
        var ownerId = data.UserId("ogretmen1");
        var draft = await files.CreateDraftAsync(ownerId, ct);
        await files.AddToDraftAsync(ownerId, draft.DraftId, "performans-ornek.txt", GenerateContent(), overwrite: true, ct: ct);
        await files.CommitAsync(ownerId, draft.DraftId, new FileArea(ownerId, "user", "private", 0), replace: true, ct: ct);
    }

    /// <summary>
    /// Gerçek bir ders notu dosyasına benzer boyutta (~200 KB), deterministik metin üretir;
    /// k6 ile akış hâlinde indirmenin (NFR-01) anlamlı bir yükle ölçülebilmesi içindir.
    /// </summary>
    private static Stream GenerateContent()
    {
        const string line = "Bu, FILE modülünün performans ölçümü (T-FILE-15) için üretilen örnek ders notu satırıdır.\n";
        var builder = new StringBuilder();
        while (builder.Length < 200 * 1024) builder.Append(line);
        return new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
    }
}
