namespace Moodle.Application.Files;

/// <summary>
/// Temizlik görevinin saf süre kuralları (FILE-FR-12; WBS-2 §3.6.1). Veritabanına bağlı değildir.
/// Bir süre, başlangıç anına süre eklendiği <b>an</b> dolmuş sayılır (sınır dahil).
/// Veritabanı sorguları aynı kuralı <see cref="Cutoff"/> ile uygular: <c>başlangıç &lt;= kesim</c>.
/// </summary>
public static class FileRetentionRules
{
    /// <summary>Bu andan önce veya bu anda başlamış süreler dolmuştur.</summary>
    public static DateTimeOffset Cutoff(DateTimeOffset now, int hours) => now - TimeSpan.FromHours(hours);

    /// <summary>
    /// FILE-BR-05, FILE-BUG-02 düzeltmesi: taslak süresi <b>son etkinliğe</b> göre dolar, oluşturulma anına göre değil.
    /// </summary>
    public static bool IsDraftExpired(DateTimeOffset lastActivityAt, int draftTtlHours, DateTimeOffset now) =>
        lastActivityAt <= Cutoff(now, draftTtlHours);

    /// <summary>
    /// FILE-BR-04, FILE-BUG-03 düzeltmesi: çöpteki kaydın süresi <b>çöpe atıldığı andan</b> sayılır;
    /// dosyanın yaşı ve son temizlik zamanı önemsizdir.
    /// </summary>
    public static bool IsTrashExpired(DateTimeOffset deletedAt, int trashRetentionHours, DateTimeOffset now) =>
        deletedAt <= Cutoff(now, trashRetentionHours);

    /// <summary>
    /// FILE-BR-03, FILE-BUG-04: içerik, hiçbir kaydın kullanmadığı andan (<c>OrphanedAt</c>) bekleme süresi sonra
    /// kalıcı silinmeye aday olur. Kullanımdaysa (<c>null</c>) asla aday değildir. Silmeden önce kullanım
    /// kilit altında yeniden denetlenir; bu kural yalnızca aday seçimidir.
    /// </summary>
    public static bool IsBlobPurgeable(DateTimeOffset? orphanedAt, int trashRetentionHours, DateTimeOffset now) =>
        orphanedAt is { } at && at <= Cutoff(now, trashRetentionHours);
}
