namespace Moodle.Application.Grading;

/// <summary>
/// Not yazma isteği mevcut durumla çakışıyor (API'de 409): kilitli veya elle değiştirilmiş not,
/// ya da istemcinin bildirdiği sürüm güncel değil.
/// </summary>
public sealed class GrdConflictException(string code, string message) : InvalidOperationException(message)
{
    public const string GradeLocked = "grade_locked";
    public const string GradeOverridden = "grade_overridden";
    public const string VersionConflict = "version_conflict";

    public string Code { get; } = code;

    public static GrdConflictException Locked() =>
        new(GradeLocked, "Not defterindeki not kilitli; puan değiştirilemez.");

    public static GrdConflictException Overridden() =>
        new(GradeOverridden, "Not defterindeki not elle değiştirilmiş; puan aktarılamaz.");

    public static GrdConflictException Version() =>
        new(VersionConflict, "Değerlendirme başka bir istekle değişti; sayfayı yenileyip yeniden deneyin.");
}
