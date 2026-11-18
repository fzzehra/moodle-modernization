using Moodle.Application.Files;

namespace Moodle.Infrastructure.Fakes;

/// <summary>
/// Gerçek dosya servisi ve deposu bağlanana kadar üretimde kullanılan, "kapalı" varsayılan: dosya işlemleri
/// açıkça hata verir; sahte veriyle çalışıyormuş gibi davranmaz.
/// </summary>
public sealed class UnavailableFiles : IFileStore, IFileService
{
    private static InvalidOperationException Off() => new("FILE modülü henüz bağlı değil.");

    public Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default) => throw Off();
    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default) => throw Off();
    public Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default) => throw Off();
    public Task DeleteAsync(string contentHash, CancellationToken ct = default) => throw Off();

    public Task<DraftInfo> CreateDraftAsync(int userId, CancellationToken ct = default) => throw Off();
    public Task<FileMeta> AddToDraftAsync(int userId, long draftId, string name, Stream content, string path = "/", bool overwrite = false, CancellationToken ct = default) => throw Off();
    public Task<IReadOnlyList<FileMeta>> ListDraftAsync(int userId, long draftId, string path = "/", CancellationToken ct = default) => throw Off();
    public Task DeleteFromDraftAsync(int userId, long draftId, long fileId, CancellationToken ct = default) => throw Off();
    public Task<IReadOnlyList<FileMeta>> CommitAsync(int userId, long draftId, FileArea target, bool replace = true, CancellationToken ct = default) => throw Off();
    public Task<IReadOnlyList<FileMeta>> ListAsync(int userId, FileArea area, string path = "/", CancellationToken ct = default) => throw Off();
    public Task<FileMeta> GetMetaAsync(int userId, long fileId, CancellationToken ct = default) => throw Off();
    public Task<FileContent> OpenAsync(int userId, long fileId, bool download = false, CancellationToken ct = default) => throw Off();
    public Task DeleteAsync(int userId, long fileId, CancellationToken ct = default) => throw Off();
}
