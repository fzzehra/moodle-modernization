using Moodle.Domain.Files;

namespace Moodle.Infrastructure.Files.Comparisons;

/// <summary>
/// FILE-FR-09, BR-10, BUG-01 için <b>ikinci, bağımsız</b> bir uygulama: kullanıcı kota hesabı.
/// WBS-2 §4.4'teki YZ/elle karşılaştırması (K17) için yazılmıştır. <b>Üretimde kullanılmaz</b> —
/// gerçek, çalışan mantık <see cref="FileService.GetQuotaAsync"/> içindedir.
///
/// <para>
/// <b>Yazarlık notu (önemli):</b> Bu dosya da Claude (YZ) tarafından, kullanıcının açık isteği üzerine
/// yazılmıştır. "Elle" (insan tarafından YZ yardımı almadan) yazılmamıştır. Karşılaştırma tablosunda
/// "Elle" değil, "YZ (ikinci/karşılaştırma uygulaması)" olarak raporlanmalıdır. Efor (adam-saat)
/// tahmini bu PR'da hesaplanmamıştır.
/// </para>
/// <para>
/// Kasıtlı olarak <see cref="FileService"/>'ten farklı bir üslupla: LINQ zinciri yerine açık <c>foreach</c>
/// döngüsü ve erken <c>continue</c>'larla, her satırın neyi elediğini ayrı ayrı açıklayan yorumlarla.
/// </para>
/// </summary>
public static class ManualQuotaCalculator
{
    /// <summary>
    /// FILE-BUG-01 düzeltmesi: Moodle'ın orijinal hatası, aynı <c>(contenthash, filename)</c> çiftini
    /// tekilleştirip kullanılan alanı olduğundan az gösteriyordu. Burada her etkin kayıt, içeriği başka
    /// bir kayıtla aynı olsa bile, kendi başına ve bir kez sayılır.
    /// </summary>
    public static long CalculateUsedBytes(IEnumerable<FileEntry> entries, int ownerUserId)
    {
        long totalBytes = 0;

        foreach (var entry in entries)
        {
            if (entry.OwnerUserId != ownerUserId)
            {
                // Başka bir kullanıcının dosyası; bu kotaya dahil değil.
                continue;
            }

            if (entry.Component != "user")
            {
                // Kota yalnızca kullanıcının kendi (özel) dosyalarını kapsar (WBS-2 §3.6.2).
                continue;
            }

            if (entry.Area == "draft")
            {
                // Taslak alanı kalıcı değildir, kotaya sayılmaz.
                continue;
            }

            if (entry.DeletedAt != null)
            {
                // Çöpteki (silinmiş) dosya kullanılan alana dahil değildir.
                continue;
            }

            totalBytes = totalBytes + entry.SizeBytes;
        }

        return totalBytes;
    }

    public static (long UsedBytes, long LimitBytes) Calculate(
        IEnumerable<FileEntry> entries, int ownerUserId, long limitBytes)
    {
        var used = CalculateUsedBytes(entries, ownerUserId);
        return (used, limitBytes);
    }
}
