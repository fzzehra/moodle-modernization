namespace Moodle.Application.Files;

/// <summary>
/// Diğer modüllerin (ödev, ders dosyaları vb.) dosya işlemleri için çağırdığı tek kapı (WBS-2 §3.6.4).
/// Yetki denetimi <see cref="IFilePolicy"/> üzerinden bu serviste yapılır. Yetkisiz veya olmayan dosyada
/// <c>AppException.NotFound()</c> fırlatılır; varlık sızdırılmaz (FILE-BR-11).
/// </summary>
public interface IFileService
{
    // --- Taslak (FILE-FR-01, FILE-BR-05, 12) ---
    Task<DraftInfo> CreateDraftAsync(int userId, CancellationToken ct = default);

    Task<FileMeta> AddToDraftAsync(
        int userId, long draftId, string name, Stream content,
        string path = "/", bool overwrite = false, CancellationToken ct = default);

    Task<IReadOnlyList<FileMeta>> ListDraftAsync(int userId, long draftId, string path = "/", CancellationToken ct = default);

    Task DeleteFromDraftAsync(int userId, long draftId, long fileId, CancellationToken ct = default);

    // --- Kalıcı alan (FILE-FR-02) ---
    /// <summary>Taslaktaki dosyaları hedef alana kaydeder; <paramref name="replace"/> ise alandaki eskileri kaldırır.</summary>
    Task<IReadOnlyList<FileMeta>> CommitAsync(
        int userId, long draftId, FileArea target, bool replace = true, CancellationToken ct = default);

    Task<IReadOnlyList<FileMeta>> ListAsync(int userId, FileArea area, string path = "/", CancellationToken ct = default);

    // --- Okuma ve silme (FILE-FR-03, 04, 05) ---
    Task<FileMeta> GetMetaAsync(int userId, long fileId, CancellationToken ct = default);

    Task<FileContent> OpenAsync(int userId, long fileId, bool download = false, CancellationToken ct = default);

    Task DeleteAsync(int userId, long fileId, CancellationToken ct = default);
}
