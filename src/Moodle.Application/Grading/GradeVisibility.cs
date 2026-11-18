using Moodle.Domain.Grading;

namespace Moodle.Application.Grading;

/// <summary>
/// Not raporlarında görünürlük kuralları (WBS-2 §3.5.1). Saat parametreyle verilir;
/// çağıran sunucu saatini <see cref="TimeProvider"/> üzerinden geçirir.
/// </summary>
public static class GradeVisibility
{
    /// <summary>
    /// <c>Hidden=true</c> süresiz gizler. <c>HiddenUntil</c> gelecekteyse o ana kadar gizler;
    /// o an geldiğinde (sınır dahil) görünür olur.
    /// </summary>
    public static bool IsHidden(bool hidden, DateTimeOffset? hiddenUntil, DateTimeOffset now) =>
        hidden || (hiddenUntil.HasValue && hiddenUntil.Value > now);

    /// <summary>
    /// Raporlarda listelenen kalemler: doğrudan puanlanan ödev ve manuel kalemler.
    /// Ders ve kategori toplamları 1. sürümde hesaplanmadığı için listelenmez; boş toplam
    /// "notlandırılmadı" gibi görünüp yanıltmasın (WBS-2 §3.5.1: toplam politikası açıkça tanımlanmalı).
    /// </summary>
    public static bool IsReportable(GradeItemType type) =>
        type is GradeItemType.Assignment or GradeItemType.Manual;
}
