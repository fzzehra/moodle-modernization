using System.Text;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Files;
using Moodle.Application.Seeding;
using Moodle.Infrastructure.Courses;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Örnek veriye (<c>ortam/ornek-veri.feature</c>, kaynak "SENG101 Ders Notları") karşılık SENG101'in ders dosyaları
/// alanına küçük bir PDF ekler (FILE-FR-13; T-FILE-03, T-FILE-05). Dosyayı dersin düzenleyen öğretmeni
/// <c>ogretmen1</c> ekler; yazma yetkisi <see cref="CourseFilePolicy"/>'den geçer. Ders kimliği CRS tablosundan
/// kısa adla bulunur; bu yüzden CRS tohumlamasından (Order 20) sonra çalışır. Tekrar çalıştırılabilir
/// (<c>replace: true</c>), yinelenen kayıt birikmez.
/// </summary>
public sealed class CourseFilesSeedContributor(IFileService files, CrsDbContext courses) : ISeedContributor
{
    public const string CourseShortName = "SENG101";
    public const string FileName = "SENG101 Ders Notları.pdf";

    public int Order => 31;
    public string Name => "FILE: SENG101 ders dosyası";

    public async Task SeedAsync(SeedDataset data, CancellationToken ct = default)
    {
        var courseId = await courses.Courses.AsNoTracking()
            .Where(c => c.ShortName == CourseShortName).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Örnek ders bulunamadı: {CourseShortName}");
        var teacherId = data.UserId("ogretmen1");

        var draft = await files.CreateDraftAsync(teacherId, ct);
        await files.AddToDraftAsync(teacherId, draft.DraftId, FileName, SamplePdf(), overwrite: true, ct: ct);
        await files.CommitAsync(teacherId, draft.DraftId, CourseFilePolicy.AreaFor(courseId), replace: true, ct: ct);
    }

    /// <summary>Tek sayfalık, geçerli ve deterministik küçük bir PDF.</summary>
    private static MemoryStream SamplePdf()
    {
        const string text = "SENG101 Ders Notlari - ornek dosya";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {31 + text.Length} >>\nstream\nBT /F1 18 Tf 72 760 Td ({text}) Tj ET\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return new MemoryStream(Encoding.ASCII.GetBytes(pdf.ToString()));
    }
}
