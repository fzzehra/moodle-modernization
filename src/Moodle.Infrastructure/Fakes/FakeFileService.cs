using Moodle.Application.Errors;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Fakes;

/// <summary>
/// Sahte gerçekleme: bellek içi dosya servisi, yetki kontrolü yok. İrem, Aslı Ceren ve Fatma Zehra gerçek servisi
/// bağlayana kadar ödev ve ders modüllerinin derlenip denenmesini sağlar. Üretimde kullanılmaz.
/// </summary>
public sealed class FakeFileService(IFileStore store) : IFileService
{
    private sealed record Row(FileMeta Meta, FileArea Area, long? DraftId);

    private static readonly List<Row> Rows = [];
    private static long _nextId = 1;
    private static long _nextDraft = 1;
    private static readonly object Gate = new();

    public Task<DraftInfo> CreateDraftAsync(int userId, CancellationToken ct = default)
        => Task.FromResult(new DraftInfo(Interlocked.Increment(ref _nextDraft), DateTimeOffset.UtcNow.AddDays(4)));

    public async Task<FileMeta> AddToDraftAsync(int userId, long draftId, string name, Stream content,
        string path = "/", bool overwrite = false, CancellationToken ct = default)
    {
        var blob = await store.WriteAsync(content, ct);
        lock (Gate)
        {
            var meta = new FileMeta(_nextId++, name, path, blob.SizeBytes, "application/octet-stream",
                blob.ContentHash, userId, DateTimeOffset.UtcNow);
            Rows.Add(new Row(meta, new FileArea(0, "user", "draft", draftId), draftId));
            return meta;
        }
    }

    public Task<IReadOnlyList<FileMeta>> ListDraftAsync(int userId, long draftId, string path = "/", CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FileMeta>>(Snapshot(r => r.DraftId == draftId && r.Meta.Path == path));

    public Task DeleteFromDraftAsync(int userId, long draftId, long fileId, CancellationToken ct = default)
    {
        lock (Gate) Rows.RemoveAll(r => r.Meta.FileId == fileId && r.DraftId == draftId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FileMeta>> CommitAsync(int userId, long draftId, FileArea target, bool replace = true,
        CancellationToken ct = default)
    {
        lock (Gate)
        {
            if (replace) Rows.RemoveAll(r => r.Area == target);
            var moved = Rows.Where(r => r.DraftId == draftId).ToList();
            Rows.RemoveAll(r => r.DraftId == draftId);
            Rows.AddRange(moved.Select(r => r with { Area = target, DraftId = null }));
            return Task.FromResult<IReadOnlyList<FileMeta>>(moved.Select(r => r.Meta).ToList());
        }
    }

    public Task<IReadOnlyList<FileMeta>> ListAsync(int userId, FileArea area, string path = "/", CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FileMeta>>(Snapshot(r => r.Area == area && r.Meta.Path == path));

    public Task<FileMeta> GetMetaAsync(int userId, long fileId, CancellationToken ct = default)
        => Task.FromResult(Find(fileId).Meta);

    public async Task<FileContent> OpenAsync(int userId, long fileId, bool download = false, CancellationToken ct = default)
    {
        var row = Find(fileId);
        var stream = await store.OpenReadAsync(row.Meta.ContentHash, ct) ?? throw AppException.NotFound();
        return new FileContent(row.Meta, stream, download || row.DraftId is not null);
    }

    public Task DeleteAsync(int userId, long fileId, CancellationToken ct = default)
    {
        lock (Gate) Rows.RemoveAll(r => r.Meta.FileId == fileId);
        return Task.CompletedTask;
    }

    private static List<FileMeta> Snapshot(Func<Row, bool> where)
    {
        lock (Gate) return Rows.Where(where).Select(r => r.Meta).ToList();
    }

    private static Row Find(long fileId)
    {
        lock (Gate) return Rows.FirstOrDefault(r => r.Meta.FileId == fileId) ?? throw AppException.NotFound();
    }
}
