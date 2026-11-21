namespace Moodle.Application.Files;

/// <summary>
/// Bir temizlik çalıştırmasının sonucu (FILE-FR-12, WBS-2 §3.6.4 <c>POST /admin/file-maintenance/run</c>).
/// </summary>
/// <param name="ExpiredDrafts">Süresi dolduğu için silinen taslak alanı sayısı (FILE-BR-05, FILE-BUG-02).</param>
/// <param name="PurgedEntries">Çöpte bekleme süresi dolduğu için kalıcı silinen dosya kaydı sayısı (FILE-BR-04, FILE-BUG-03).</param>
/// <param name="PurgedBlobs">Hiçbir kaydın kullanmadığı ve bekleme süresi dolan içerik sayısı (FILE-BR-03, FILE-BUG-04).</param>
/// <param name="Errors">Çalıştırmayı durdurmayan hatalar (ör. diskten silinemeyen içerik). Boşsa sorun yoktur.</param>
/// <param name="Skipped">Başka bir örnek temizliği zaten yürütüyorsa <c>true</c>; bu turda hiçbir şey yapılmamıştır.</param>
public sealed record FileMaintenanceResult(
    int ExpiredDrafts,
    int PurgedEntries,
    int PurgedBlobs,
    IReadOnlyList<string> Errors,
    bool Skipped = false)
{
    public static FileMaintenanceResult SkippedRun { get; } = new(0, 0, 0, [], Skipped: true);
}

/// <summary>
/// Dosya temizlik görevi (FILE-FR-12). <see cref="IFileService"/>'ten ayrı tutulur: diğer modüllerin çağırdığı
/// dosya kapısı değildir, yalnızca arka plan görevi ve yönetici bakım ucu tarafından çağrılır.
/// </summary>
public interface IFileMaintenance
{
    Task<FileMaintenanceResult> RunAsync(CancellationToken ct = default);
}
