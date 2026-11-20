using Microsoft.EntityFrameworkCore;
using Moodle.Application.Errors;
using Moodle.Application.Files;
using Moodle.Domain.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Gerçek <see cref="IFileService"/> gerçeklemesi (WBS-2 §3.6). Taslak/yükleme (FILE-FR-01, 07, 08, 10, 11)
/// temel bir baz olarak burada yazılmıştır; Aslı Ceren bunun üzerine ad benzersizleştirme, boyut/tür
/// doğrulaması ve antivirüs taramasını ekleyecektir (aynı sınıf, sıradaki PR). Bu PR'ın kapsamı:
/// kalıcı alana kaydetme (FR-02), yetkili indirme (FR-03), yetkisiz erişimde 404 (FR-04, BR-11, 12),
/// silme ve çöp (FR-05, BR-13, 14), kullanıcı kotası (FR-09, BR-10) ve kota hesabı hatasının (BUG-01)
/// yeni sistemde tekrarlanmaması.
/// </summary>
public sealed class FileService(FilesDbContext db, IFileStore store, IFilePolicyRegistry policies, TimeProvider clock) : IFileService
{
    public async Task<DraftInfo> CreateDraftAsync(int userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var draft = new FileDraft { OwnerUserId = userId, CreatedAt = now, LastActivityAt = now };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(ct);
        var settings = await SettingsAsync(ct);
        return new DraftInfo(draft.DraftId, now.AddHours(settings.DraftTtlHours));
    }

    public async Task<FileMeta> AddToDraftAsync(
        int userId, long draftId, string name, Stream content,
        string path = "/", bool overwrite = false, CancellationToken ct = default)
    {
        var draft = await RequireOwnDraftAsync(userId, draftId, ct);
        var area = DraftArea(userId, draftId);

        // FILE-BR-06: geçersiz/boş ad reddedilir. Ad benzersizleştirme (FR-07) Aslı Ceren'in kapsamıdır;
        // burada yalnızca çakışma reddedilir (overwrite istenmedikçe).
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
            throw AppException.Validation("invalid_name", "Geçersiz dosya adı.");

        var existing = await FindEntryAsync(area, path, name, ct);
        if (existing is not null)
        {
            if (!overwrite) throw AppException.Conflict("name_conflict", "Bu ad bu konumda zaten kullanılıyor.");
            await SoftDeleteAsync(existing, ct);
        }

        var blob = await store.WriteAsync(content, ct);
        if (blob.SizeBytes == 0) throw AppException.Validation("empty_file", "Boş dosya yüklenemez.");
        await EnsureBlobRowAsync(blob, ct);

        var entry = new FileEntry
        {
            ContentHash = blob.ContentHash, ContextId = area.ContextId, Component = area.Component,
            Area = area.Area, ItemId = area.ItemId, Path = path, Name = name, OwnerUserId = userId,
            SizeBytes = blob.SizeBytes, MimeType = "application/octet-stream", Status = "active",
            CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
        };
        db.Entries.Add(entry);
        draft.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ToMeta(entry);
    }

    public async Task<IReadOnlyList<FileMeta>> ListDraftAsync(int userId, long draftId, string path = "/", CancellationToken ct = default)
    {
        await RequireOwnDraftAsync(userId, draftId, ct);
        var area = DraftArea(userId, draftId);
        return await ActiveEntries(area, path).ToListAsync(ct) is var rows
            ? rows.Select(ToMeta).ToList() : [];
    }

    public async Task DeleteFromDraftAsync(int userId, long draftId, long fileId, CancellationToken ct = default)
    {
        var draft = await RequireOwnDraftAsync(userId, draftId, ct);
        var area = DraftArea(userId, draftId);
        var entry = await db.Entries.SingleOrDefaultAsync(
            e => e.Id == fileId && e.DeletedAt == null && e.ContextId == area.ContextId
                && e.Component == area.Component && e.Area == area.Area && e.ItemId == area.ItemId, ct)
            ?? throw AppException.NotFound();
        await SoftDeleteAsync(entry, ct);
        draft.LastActivityAt = clock.GetUtcNow(); // FILE-BUG-02: taslaktan silme de etkinliktir
        await db.SaveChangesAsync(ct);
    }

    // FILE-FR-02: taslaktaki dosyalar kalıcı alana taşınır.
    public async Task<IReadOnlyList<FileMeta>> CommitAsync(
        int userId, long draftId, FileArea target, bool replace = true, CancellationToken ct = default)
    {
        var draft = await RequireOwnDraftAsync(userId, draftId, ct);
        if (!await CanWriteAsync(userId, target, ct)) throw AppException.NotFound();

        var sourceArea = DraftArea(userId, draftId);
        var draftEntries = await ActiveEntries(sourceArea, null).ToListAsync(ct);

        if (replace)
            foreach (var old in await ActiveEntries(target, null).ToListAsync(ct))
                await SoftDeleteAsync(old, ct);

        var now = clock.GetUtcNow();
        draft.LastActivityAt = now; // FILE-BUG-02: kaydetme de etkinliktir
        foreach (var entry in draftEntries)
        {
            entry.ContextId = target.ContextId;
            entry.Component = target.Component;
            entry.Area = target.Area;
            entry.ItemId = target.ItemId;
            entry.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return draftEntries.Select(ToMeta).ToList();
    }

    public async Task<IReadOnlyList<FileMeta>> ListAsync(int userId, FileArea area, string path = "/", CancellationToken ct = default)
    {
        if (!await CanReadAsync(userId, area, ct)) throw AppException.NotFound();
        return (await ActiveEntries(area, path).ToListAsync(ct)).Select(ToMeta).ToList();
    }

    public async Task<FileMeta> GetMetaAsync(int userId, long fileId, CancellationToken ct = default) =>
        ToMeta(await RequireReadableEntryAsync(userId, fileId, ct));

    // FILE-FR-03, BR-11, 12: yetkili indirme; taslak dosyalar her zaman indirmeye zorlanır.
    public async Task<FileContent> OpenAsync(int userId, long fileId, bool download = false, CancellationToken ct = default)
    {
        var entry = await RequireReadableEntryAsync(userId, fileId, ct);
        var stream = await store.OpenReadAsync(entry.ContentHash, ct) ?? throw AppException.NotFound();
        var forceDownload = download || entry.Area == "draft";
        return new FileContent(ToMeta(entry), stream, forceDownload);
    }

    // FILE-FR-05, BR-13, 14: silme çöpe taşır (DeletedAt); içerik başka etkin kayıt kullanmıyorsa boşa düşer.
    public async Task DeleteAsync(int userId, long fileId, CancellationToken ct = default)
    {
        var entry = await db.Entries.SingleOrDefaultAsync(e => e.Id == fileId && e.DeletedAt == null, ct)
            ?? throw AppException.NotFound();
        if (!await CanWriteAsync(userId, AreaOf(entry), ct)) throw AppException.NotFound();
        await SoftDeleteAsync(entry, ct);
        await db.SaveChangesAsync(ct);
    }

    // FILE-FR-09, BR-10, BUG-01: kalıcı (taslak olmayan) dosyaların toplamı; her kayıt tekil sayılır
    // (Moodle (contenthash, filename) çiftine göre tekilleştirip eksik sayıyordu, bkz. WBS-1 §4.6.5).
    public async Task<(long UsedBytes, long LimitBytes)> GetQuotaAsync(int userId, CancellationToken ct = default)
    {
        var used = await db.Entries.AsNoTracking()
            .Where(e => e.OwnerUserId == userId && e.Component == "user" && e.Area != "draft" && e.DeletedAt == null)
            .SumAsync(e => (long?)e.SizeBytes, ct) ?? 0;
        var limit = (await SettingsAsync(ct)).UserQuotaBytes;
        return (used, limit);
    }

    private async Task<FileEntry> RequireReadableEntryAsync(int userId, long fileId, CancellationToken ct)
    {
        var entry = await db.Entries.SingleOrDefaultAsync(e => e.Id == fileId && e.DeletedAt == null, ct)
            ?? throw AppException.NotFound();
        if (!await CanReadAsync(userId, AreaOf(entry), ct)) throw AppException.NotFound();
        return entry;
    }

    private async Task<FileDraft> RequireOwnDraftAsync(int userId, long draftId, CancellationToken ct)
    {
        var draft = await db.Drafts.FindAsync([draftId], ct) ?? throw AppException.NotFound();
        // FILE-BR-12: taslak yalnızca sahibine görünür; başkasının taslağı "bulunamadı" sayılır.
        if (draft.OwnerUserId != userId) throw AppException.NotFound();
        return draft;
    }

    private Task<bool> CanReadAsync(int userId, FileArea area, CancellationToken ct) =>
        policies.Find(area.Component)?.CanReadAsync(userId, area, ct) ?? Task.FromResult(false);

    private Task<bool> CanWriteAsync(int userId, FileArea area, CancellationToken ct) =>
        policies.Find(area.Component)?.CanWriteAsync(userId, area, ct) ?? Task.FromResult(false);

    private static FileArea DraftArea(int userId, long draftId) => new(userId, "user", "draft", draftId);
    private static FileArea AreaOf(FileEntry e) => new(e.ContextId, e.Component, e.Area, e.ItemId);

    private IQueryable<FileEntry> ActiveEntries(FileArea area, string? path) => db.Entries.Where(e =>
        e.ContextId == area.ContextId && e.Component == area.Component && e.Area == area.Area
        && e.ItemId == area.ItemId && e.DeletedAt == null && (path == null || e.Path == path));

    private Task<FileEntry?> FindEntryAsync(FileArea area, string path, string name, CancellationToken ct) =>
        db.Entries.SingleOrDefaultAsync(e =>
            e.ContextId == area.ContextId && e.Component == area.Component && e.Area == area.Area
            && e.ItemId == area.ItemId && e.Path == path && e.Name == name && e.DeletedAt == null, ct);

    private async Task SoftDeleteAsync(FileEntry entry, CancellationToken ct)
    {
        entry.DeletedAt = clock.GetUtcNow();
        var stillUsed = await db.Entries.AnyAsync(
            e => e.ContentHash == entry.ContentHash && e.Id != entry.Id && e.DeletedAt == null, ct);
        if (!stillUsed)
        {
            var blob = await db.Blobs.FindAsync([entry.ContentHash], ct);
            if (blob is not null) blob.OrphanedAt = clock.GetUtcNow();
        }
    }

    private async Task EnsureBlobRowAsync(StoredBlob blob, CancellationToken ct)
    {
        var row = await db.Blobs.FindAsync([blob.ContentHash], ct);
        if (row is null)
            db.Blobs.Add(new FileBlob
            {
                ContentHash = blob.ContentHash, SizeBytes = blob.SizeBytes,
                StorageKey = blob.StorageKey, CreatedAt = clock.GetUtcNow(),
            });
        else
            row.OrphanedAt = null; // içerik yeniden kullanılmaya başladı (ör. aynı içerik tekrar yüklendi)
    }

    private Task<FileSetting> SettingsAsync(CancellationToken ct) => db.Settings.AsNoTracking().SingleAsync(ct);

    private static FileMeta ToMeta(FileEntry e) =>
        new(e.Id, e.Name, e.Path, e.SizeBytes, e.MimeType, e.ContentHash, e.OwnerUserId, e.CreatedAt);
}
