namespace Moodle.Application.Files;

/// <summary>
/// Fiziksel içerik deposu (WBS-2 §2.5, §3.6.1). İçerik adresli: aynı içerik tek kopya olarak saklanır.
/// Veritabanını bilmez; yalnızca baytları tutar. Gerçek gerçekleme: yerel disk (Seray). Nesne deposu sonradan eklenebilir.
/// </summary>
public interface IFileStore
{
    /// <summary>Akışı SHA-256'sını hesaplayarak yazar. İçerik zaten varsa yeniden yazmaz.</summary>
    Task<StoredBlob> WriteAsync(Stream content, CancellationToken ct = default);

    /// <returns>İçerik yoksa <c>null</c>.</returns>
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken ct = default);

    Task<bool> ExistsAsync(string contentHash, CancellationToken ct = default);

    /// <summary>Fiziksel içeriği siler. Çağıran, hiçbir kaydın kullanmadığını önceden doğrulamış olmalıdır (FILE-BUG-04).</summary>
    Task DeleteAsync(string contentHash, CancellationToken ct = default);

    /// <summary>Depodaki tüm içeriklerin özetleri. Veritabanı ile tutarlılık denetimi içindir (FILE-NFR-02).</summary>
    IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct = default);
}
