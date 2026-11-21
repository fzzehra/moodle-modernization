using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Dosya temizlik görevi (FILE-FR-12; WBS-2 §3.6.1, §3.6.2). Moodle'daki <c>file_storage::cron</c> karşılığıdır,
/// ama üç hatası düzeltilmiştir. Sıra:
/// <list type="number">
/// <item>Son etkinliği (<c>LastActivityAt</c>) taslak süresinden eski taslaklar ve onların taslak kayıtları silinir
/// (FILE-BR-05; FILE-BUG-02: oluşturulma anı değil son etkinlik esas alınır).</item>
/// <item>Çöpe atıldığı an (<c>DeletedAt</c>) çöp bekleme süresinden eski kayıtlar kalıcı silinir
/// (FILE-BR-04; FILE-BUG-03: çöp toptan değil, her kayıt kendi çöpe atılma anına göre boşaltılır).</item>
/// <item>Boşa düştüğü an (<c>OrphanedAt</c>) çöp bekleme süresinden eski içerikler silinir. <c>file_blob</c> satırı
/// <c>FOR UPDATE</c> ile kilitlenir; kullanım (etkin veya çöpteki herhangi bir kayıt) ve <c>OrphanedAt</c> aynı işlemde
/// yeniden denetlenir; hâlâ sahipsizse satır silinir, işlem onaylandıktan sonra disk dosyası silinir (FILE-BR-03, FILE-BUG-04).
/// İçerik bazlı oturum kilidi bu iki adım boyunca yükleyicinin transaction kilidiyle aynı anahtarı kullanır.
/// Diskten silme başarısız olursa satır geri gelmez; hata günlüğe ve sonuca yazılır, <see cref="FileConsistencyChecker"/>
/// raporunda "dosya var, kayıt yok" olarak görünür.</item>
/// </list>
/// Her çalıştırma PostgreSQL oturum düzeyi danışma kilidiyle (<c>pg_try_advisory_lock</c>) korunur; kilit başka bir
/// örnekteyse tur atlanır. Zaman <see cref="TimeProvider"/>'dan gelir.
/// </summary>
public sealed class FileMaintenanceService(
    FilesDbContext db, IFileStore store, TimeProvider clock, ILogger<FileMaintenanceService> logger) : IFileMaintenance
{
    /// <summary>Danışma kilidi anahtarı ("MFILECLN"). Bütün API örnekleri aynı anahtarı kullanır.</summary>
    public const long AdvisoryLockKey = 0x4D46_494C_4543_4C4E;

    public async Task<FileMaintenanceResult> RunAsync(CancellationToken ct = default)
    {
        // Oturum düzeyi kilit bağlantıya bağlıdır: bütün çalıştırma aynı açık bağlantıda yürür.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var acquired = (await db.Database
                .SqlQuery<bool>($"""SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS "Value" """)
                .ToListAsync(ct)).Single();
            if (!acquired)
            {
                logger.LogInformation("Dosya temizliği atlandı: başka bir örnek temizliği yürütüyor.");
                return FileMaintenanceResult.SkippedRun;
            }

            try
            {
                return await RunLockedAsync(ct);
            }
            finally
            {
                await ReleaseLockAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<FileMaintenanceResult> RunLockedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);
        var errors = new List<string>();

        var drafts = await PurgeExpiredDraftsAsync(FileRetentionRules.Cutoff(now, settings.DraftTtlHours), now, ct);
        var entries = await PurgeExpiredTrashAsync(FileRetentionRules.Cutoff(now, settings.TrashRetentionHours), now, ct);
        var blobs = await PurgeOrphanedBlobsAsync(settings.TrashRetentionHours, now, errors, ct);

        var result = new FileMaintenanceResult(drafts, entries, blobs, errors);
        logger.LogInformation(
            "Dosya temizliği bitti: {ExpiredDrafts} taslak, {PurgedEntries} çöp kaydı, {PurgedBlobs} içerik silindi, {ErrorCount} hata.",
            drafts, entries, blobs, errors.Count);
        return result;
    }

    // (1) FILE-BR-05, FILE-BUG-02
    private async Task<int> PurgeExpiredDraftsAsync(DateTimeOffset cutoff, DateTimeOffset now, CancellationToken ct)
    {
        var ids = await db.Drafts.AsNoTracking()
            .Where(d => d.LastActivityAt <= cutoff)
            .OrderBy(d => d.DraftId)
            .Select(d => d.DraftId)
            .ToListAsync(ct);

        var purged = 0;
        foreach (var draftId in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Seçimden bu yana etkinlik olduysa (AddToDraft/DeleteFromDraft/Commit) taslak artık süresi dolmuş değildir.
            var owner = await db.Database.SqlQuery<int>($"""
                SELECT "OwnerUserId" AS "Value" FROM file_draft
                WHERE "DraftId" = {draftId} AND "LastActivityAt" <= {cutoff}
                FOR UPDATE
                """).ToListAsync(ct);
            if (owner.Count == 0)
            {
                await tx.RollbackAsync(ct);
                continue;
            }

            var draftEntries = db.Entries.Where(e => e.ContextId == owner[0] && e.Component == "user"
                && e.Area == "draft" && e.ItemId == draftId);
            var hashes = await draftEntries.Select(e => e.ContentHash).Distinct().ToListAsync(ct);
            await draftEntries.ExecuteDeleteAsync(ct);
            await db.Drafts.Where(d => d.DraftId == draftId).ExecuteDeleteAsync(ct);
            await MarkOrphansAsync(hashes, now, ct);
            await tx.CommitAsync(ct);
            purged++;
        }
        return purged;
    }

    // (2) FILE-BR-04, FILE-BUG-03
    private async Task<int> PurgeExpiredTrashAsync(DateTimeOffset cutoff, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var expired = db.Entries.Where(e => e.DeletedAt != null && e.DeletedAt <= cutoff);
        var hashes = await expired.Select(e => e.ContentHash).Distinct().ToListAsync(ct);
        var purged = await expired.ExecuteDeleteAsync(ct);
        await MarkOrphansAsync(hashes, now, ct);
        await tx.CommitAsync(ct);
        return purged;
    }

    // (3) FILE-BR-03, FILE-BUG-04
    private async Task<int> PurgeOrphanedBlobsAsync(int retentionHours, DateTimeOffset now, List<string> errors, CancellationToken ct)
    {
        var cutoff = FileRetentionRules.Cutoff(now, retentionHours);
        var candidates = await db.Blobs.AsNoTracking()
            .Where(b => b.OrphanedAt != null && b.OrphanedAt <= cutoff)
            .OrderBy(b => b.ContentHash)
            .Select(b => b.ContentHash)
            .ToListAsync(ct);

        var purged = 0;
        foreach (var hash in candidates)
        {
            // Same bigint key as DraftUploadService. Session lock spans the DB commit AND disk delete;
            // otherwise an upload can reuse content between those two operations and lose its file.
            var contentLockKey = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(hash));
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({contentLockKey})", ct);
            try
            {
                bool deleted;
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    deleted = await TryDeleteBlobRowAsync(hash, retentionHours, now, ct);
                    if (deleted) await tx.CommitAsync(ct);
                    else await tx.RollbackAsync(ct);
                }
                if (!deleted) continue;
                purged++;

                // Satır silindi ve işlem onaylandı; disk ondan sonra silinir. Başarısızlıkta satır geri gelmez:
                // en kötü sonuç diskte kayıtsız içeriktir (güvenle silinebilir, tutarlılık raporunda görünür).
                try
                {
                    await store.DeleteAsync(hash, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "İçerik {ContentHash} veritabanından silindi ama diskten silinemedi.", hash);
                    errors.Add($"{hash}: veritabanından silindi ama diskten silinemedi ({ex.Message}).");
                }
            }
            finally
            {
                // Cancellation must not leave a session-level lock on this open connection.
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({contentLockKey})", CancellationToken.None);
            }
        }
        return purged;
    }

    private async Task<bool> TryDeleteBlobRowAsync(string hash, int retentionHours, DateTimeOffset now, CancellationToken ct)
    {
        var locked = await db.Database.SqlQuery<DateTimeOffset?>($"""
            SELECT "OrphanedAt" AS "Value" FROM file_blob WHERE "ContentHash" = {hash} FOR UPDATE
            """).ToListAsync(ct);
        if (locked.Count == 0) return false;                                                   // başka biri sildi
        if (!FileRetentionRules.IsBlobPurgeable(locked[0], retentionHours, now)) return false; // yeniden kullanıldı
        if (await db.Entries.AnyAsync(e => e.ContentHash == hash, ct)) return false;           // etkin veya çöpte kaydı var

        await db.Database.ExecuteSqlAsync($"""DELETE FROM file_blob WHERE "ContentHash" = {hash}""", ct);
        return true;
    }

    /// <summary>Kayıtları kalıcı silinen içeriklerden etkin kaydı kalmayanlar o andan itibaren boşa düşmüş sayılır.</summary>
    private Task MarkOrphansAsync(List<string> hashes, DateTimeOffset now, CancellationToken ct)
    {
        if (hashes.Count == 0) return Task.CompletedTask;
        return db.Blobs
            .Where(b => hashes.Contains(b.ContentHash) && b.OrphanedAt == null
                && !db.Entries.Any(e => e.ContentHash == b.ContentHash && e.DeletedAt == null))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.OrphanedAt, now), ct);
    }

    private async Task ReleaseLockAsync()
    {
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Bağlantı kapanınca oturum kilidi zaten bırakılır.
            logger.LogWarning(ex, "Dosya temizliği danışma kilidi açıkça bırakılamadı.");
        }
    }
}
