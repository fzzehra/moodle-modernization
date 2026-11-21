using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// CRS <c>course.deleted</c> olayında o dersin dosyalarını (<c>component = course</c>, bağlam = ders kimliği) çöpe taşır.
/// Kalıcı silme, temizlik görevinin işidir: o görev bekleme süresi sonunda içeriğin hâlâ kullanılmadığını yeniden denetler
/// (FILE-BUG-04), bu yüzden burada yalnızca <c>DeletedAt</c> ve gerekirse <c>OrphanedAt</c> yazılır, fiziksel içeriğe dokunulmaz.
/// <para>Tekrar çalıştırmaya dayanıklıdır (CRS aynı olayı yeniden iletebilir): yalnızca <c>DeletedAt = null</c> kayıtlar işlenir,
/// ikinci çalıştırmada işlenecek kayıt kalmaz.</para>
/// Ödev teslim dosyaları (<c>mod_assign</c>) bu sınıfın kapsamı dışındadır; onları ASG modülü kendi uzantısıyla temizler.
/// </summary>
public sealed class FileCourseDeletedExtension(FilesDbContext configured, TimeProvider clock) : ICrsEventExtension
{
    public const string ExtensionId = "file.course-deleted";
    public const string CourseDeletedKind = "course.deleted";

    public string Id => ExtensionId;
    public int Order => 100;

    public async Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default)
    {
        if (notification.Kind != CourseDeletedKind) return;

        await using var db = new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql(configured.Database.GetConnectionString()).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var entries = await db.Entries
            .Where(e => e.Component == CourseFilePolicy.ComponentName && e.ContextId == notification.CourseId && e.DeletedAt == null)
            .ToListAsync(ct);
        if (entries.Count == 0) return;

        foreach (var hash in entries.Select(e => e.ContentHash).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            var key = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(hash));
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        }
        var now = clock.GetUtcNow();
        var ids = entries.Select(e => e.Id).ToList();
        foreach (var entry in entries) entry.DeletedAt = now;

        // Başka (silinmemiş) kayıt kullanmıyorsa içerik boşa düşer; kalıcı silme temizlik görevinde yeniden denetlenir.
        foreach (var hash in entries.Select(e => e.ContentHash).Distinct(StringComparer.Ordinal))
        {
            var stillUsed = await db.Entries.AnyAsync(
                e => e.ContentHash == hash && e.DeletedAt == null && !ids.Contains(e.Id), ct);
            if (stillUsed) continue;
            var blob = await db.Blobs.FindAsync([hash], ct);
            if (blob is { OrphanedAt: null }) blob.OrphanedAt = now;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
