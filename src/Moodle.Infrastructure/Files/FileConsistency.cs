using Microsoft.EntityFrameworkCore;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>Veritabanı ile depo arasındaki tutarsızlık raporu (FILE-NFR-02).</summary>
public sealed record ConsistencyReport(
    IReadOnlyList<string> BlobsMissingOnDisk,
    IReadOnlyList<string> FilesWithoutBlob)
{
    public bool IsConsistent => BlobsMissingOnDisk.Count == 0 && FilesWithoutBlob.Count == 0;
}

/// <summary>
/// Veritabanı kaydı ile fiziksel dosyanın tutarlılığını denetler. İki tür tutarsızlık vardır:
/// <list type="bullet">
/// <item><b>Kayıt var, dosya yok</b> (<c>BlobsMissingOnDisk</c>): kullanıcı dosyası açılamaz. Ciddidir.</item>
/// <item><b>Dosya var, kayıt yok</b> (<c>FilesWithoutBlob</c>): yazma sonrası kayıt eklenemedi (yarım iş). Disk israfıdır, güvenle silinebilir.</item>
/// </list>
/// Kural: <b>önce depoya yaz, sonra veritabanına kaydet</b>. Böylece çökme olursa en kötü sahipsiz dosya kalır, asla kayıtsız referans kalmaz.
/// </summary>
public sealed class FileConsistencyChecker(FilesDbContext db, IFileStore store)
{
    public static ConsistencyReport Compare(IReadOnlyCollection<string> dbHashes, IReadOnlyCollection<string> diskHashes)
    {
        var disk = diskHashes.ToHashSet(StringComparer.Ordinal);
        var rows = dbHashes.ToHashSet(StringComparer.Ordinal);
        return new ConsistencyReport(
            rows.Where(h => !disk.Contains(h)).Order(StringComparer.Ordinal).ToList(),
            disk.Where(h => !rows.Contains(h)).Order(StringComparer.Ordinal).ToList());
    }

    public async Task<ConsistencyReport> CheckAsync(CancellationToken ct = default)
    {
        var dbHashes = await db.Blobs.Select(b => b.ContentHash).ToListAsync(ct);
        var diskHashes = new List<string>();
        await foreach (var h in store.ListHashesAsync(ct)) diskHashes.Add(h);
        return Compare(dbHashes, diskHashes);
    }
}
